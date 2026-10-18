// A room on the host: code, players, settings, scores, start/next/finish. The host app owns
// exactly one room at a time; HostSession connects it to the network.

using System;
using System.Collections.Generic;
using System.Linq;
using QuizNight.Shared;

namespace QuizNight.Server;

public sealed class Player
{
	public string Id = "";
	public string Name = "";
	public string Color = "";
	public bool Connected;
	public long? DisconnectedAt;
}

public sealed class Room : IGameHooks
{
	/// <summary>How long a disconnected player keeps their lobby seat.</summary>
	public const long PlayerGraceMs = 60_000;

	public string Phase { get; private set; } = Shared.Phase.Lobby;
	public readonly RoomSettings Settings = new();
	/// <summary>In join order.</summary>
	public readonly List<Player> Players = new();
	public Game? Game { get; private set; }
	public readonly Dictionary<string, int> Scores = new();

	public readonly string Code;
	/// <summary>Full quiz including answers and media — never send this to players as-is.</summary>
	public readonly Quiz Quiz;
	readonly Func<long> _clock;

	public Room(string code, Quiz quiz, Func<long>? clock = null)
	{
		Code = code;
		Quiz = quiz;
		_clock = clock ?? (() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
	}

	public static string NewCode(Random? rng = null)
	{
		rng ??= Random.Shared;
		return string.Concat(Enumerable.Range(0, Limits.CodeLength).Select(_ => Limits.CodeAlphabet[rng.Next(Limits.CodeAlphabet.Length)]));
	}

	public Player? GetPlayer(string id) => Players.Find(p => p.Id == id);

	// ---------- IGameHooks ----------

	public long Now() => _clock();
	public IReadOnlyList<string> ActivePlayerIds() => Players.Where(p => p.Connected).Select(p => p.Id).ToList();
	public int AnswerTimeSec() => Settings.AnswerTimeSec;
	public bool HostShowsQuestions() => Settings.HostShowsQuestions;
	public bool HostUnlocksBuzzers() => Settings.HostUnlocksBuzzers;
	public int Score(string playerId) => Scores.GetValueOrDefault(playerId);
	public void Award(string playerId, int points) => Scores[playerId] = (Scores.TryGetValue(playerId, out var s) ? s : 0) + points;

	/// <summary>The room as one viewer may see it: "host", or a player id.</summary>
	public RoomState ToState(string viewer) => new()
	{
		Code = Code,
		Quiz = new QuizInfo { Id = Quiz.Id, Title = Quiz.Title, QuestionCount = Quiz.Questions.Count },
		Game = Game != null && Phase == Shared.Phase.Playing ? Game.View(viewer) : null,
		Scores = new Dictionary<string, int>(Scores),
		ServerNow = Now(),
		Phase = Phase,
		Settings = Settings.Clone(),
		HostConnected = true,
		Players = Players.Select(p => new PublicPlayer { Id = p.Id, Name = p.Name, Color = p.Color, Connected = p.Connected }).ToList(),
	};

	public static string CleanName(string raw)
	{
		var name = string.Join(' ', raw.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
		return name.Length > Limits.NameMaxLength ? name[..Limits.NameMaxLength].TrimEnd() : name;
	}

	/// <summary>Seats a new player, or gives a returning one (same id) their seat back.</summary>
	public (Player? Player, string? Error) Join(string id, string rawName)
	{
		var existing = GetPlayer(id);
		if (existing != null)
		{
			existing.Connected = true;
			existing.DisconnectedAt = null;
			return (existing, null);
		}
		var name = CleanName(rawName);
		if (name == "") return (null, "Please enter a name.");
		if (Phase != Shared.Phase.Lobby) return (null, "This quiz has already started.");
		if (Players.Count >= Settings.MaxPlayers) return (null, "This room is full.");
		if (Players.Any(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase)))
			return (null, "Someone in this room already uses that name.");
		var used = Players.Select(p => p.Color).ToHashSet();
		var player = new Player
		{
			Id = id,
			Name = name,
			Color = Limits.PlayerColors.FirstOrDefault(c => !used.Contains(c)) ?? Limits.PlayerColors[0],
			Connected = true,
		};
		Players.Add(player);
		return (player, null);
	}

	public void Disconnect(string id)
	{
		var p = GetPlayer(id);
		if (p == null || !p.Connected) return;
		p.Connected = false;
		p.DisconnectedAt = Now();
		Game?.CheckAllAnswered();
	}

	public void Remove(string id)
	{
		Players.RemoveAll(p => p.Id == id);
		Game?.CheckAllAnswered();
	}

	public void UpdateSettings(int? maxPlayers = null, int? answerTimeSec = null, bool? hostShows = null, bool? hostUnlocks = null)
	{
		if (maxPlayers is { } mp)
		{
			// Never shrink below the number of people already seated.
			var min = Math.Max(Limits.MaxPlayersMin, Players.Count);
			Settings.MaxPlayers = Math.Clamp(mp, min, Limits.MaxPlayersMax);
		}
		if (answerTimeSec is { } at) Settings.AnswerTimeSec = Math.Clamp(at, Limits.AnswerTimeMin, Limits.AnswerTimeMax);
		if (hostShows is { } shows) Settings.HostShowsQuestions = shows;
		if (hostUnlocks is { } unlocks) Settings.HostUnlocksBuzzers = unlocks;
	}

	public Result Start()
	{
		if (Phase != Shared.Phase.Lobby) return Result.Fail("The quiz is already running.");
		var connected = Players.Count(p => p.Connected);
		if (connected < Limits.MinPlayersToStart) return Result.Fail(Lang.F("You need at least {0} connected players.", Limits.MinPlayersToStart));
		Phase = Shared.Phase.Playing;
		Scores.Clear();
		foreach (var p in Players) Scores[p.Id] = 0;
		Game = new Game(Quiz, this);
		return Result.Success();
	}

	/// <summary>Reveal if still open, otherwise the next question (or the podium after the last one).</summary>
	public void Next()
	{
		if (Game == null || Phase != Shared.Phase.Playing) return;
		if (Game.Stage == Stage.Open) Game.HostReveal();
		else
		{
			Game.Next();
			if (Game.Finished) Phase = Shared.Phase.Finished;
		}
	}

	public void Finish()
	{
		if (Phase != Shared.Phase.Playing) return;
		Phase = Shared.Phase.Finished;
	}

	public void BackToLobby()
	{
		Game = null;
		Scores.Clear();
		Phase = Shared.Phase.Lobby;
	}

	/// <summary>Runs game timers and drops lobby players whose grace period ran out. True if anything changed.</summary>
	public bool Tick()
	{
		var changed = Phase == Shared.Phase.Playing && Game != null && Game.Tick();
		if (Phase == Shared.Phase.Lobby)
		{
			var now = Now();
			changed |= Players.RemoveAll(p => !p.Connected && p.DisconnectedAt is { } at && now - at > PlayerGraceMs) > 0;
		}
		return changed;
	}
}
