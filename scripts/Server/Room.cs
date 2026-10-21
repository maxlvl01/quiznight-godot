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
	/// <summary>Joined after the start: the question (1-based) they came in at.</summary>
	public int? JoinedAt;
	/// <summary>Left during the game and not back yet: the question (1-based) they left at.</summary>
	public int? LeftAt;
}

/// <summary>What breaks ties between players with the same score (see Room.Ranking).</summary>
public sealed class PlayerStats
{
	/// <summary>When their score last changed: of two with the same score, the one who got there first is ahead.</summary>
	public long? ScoreChangedAt;
	public long AnswerMs;
	public int Answers;
	public double Km;
	public int KmAnswers;
	/// <summary>Questions they scored on.</summary>
	public int Scored;

	public double? AvgAnswerMs => Answers > 0 ? (double)AnswerMs / Answers : null;
	public double? AvgKm => KmAnswers > 0 ? Km / KmAnswers : null;
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
	/// <summary>Per player, for the ranking's tiebreaks and the saved results.</summary>
	public readonly Dictionary<string, PlayerStats> Stats = new();

	public PlayerStats StatsOf(string playerId) => Stats.TryGetValue(playerId, out var s) ? s : Stats[playerId] = new PlayerStats();

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
	public bool JokerAllowed(string kind) => Settings.Jokers.Contains(kind);
	public int Score(string playerId) => Scores.GetValueOrDefault(playerId);
	public void Award(string playerId, int points)
	{
		Scores[playerId] = (Scores.TryGetValue(playerId, out var s) ? s : 0) + points;
		if (points != 0) StatsOf(playerId).ScoreChangedAt = Now();
	}

	public void NoteAnswerTime(string playerId, long ms)
	{
		var s = StatsOf(playerId);
		s.AnswerMs += Math.Max(0, ms);
		s.Answers++;
	}

	public void NoteDistance(string playerId, double km)
	{
		var s = StatsOf(playerId);
		s.Km += km;
		s.KmAnswers++;
	}

	public void NoteScored(string playerId) => StatsOf(playerId).Scored++;

	/// <summary>
	/// Everyone's position: most points first; same points, whoever got there first; then the faster average
	/// buzz/answer time; then the closer average map pin; then who joined first. Players equal in all of these share a place.
	/// </summary>
	public (List<string> Order, Dictionary<string, int> Places) Ranking()
	{
		(int, long, double, double) Key(Player p)
		{
			var s = StatsOf(p.Id);
			return (-Score(p.Id), s.ScoreChangedAt ?? long.MaxValue, s.AvgAnswerMs ?? double.MaxValue, s.AvgKm ?? double.MaxValue);
		}
		var order = Players.Select((p, joined) => (P: p, Key: Key(p), Joined: joined)).OrderBy(x => x.Key).ThenBy(x => x.Joined).ToList();
		var places = new Dictionary<string, int>();
		for (var i = 0; i < order.Count; i++)
			places[order[i].P.Id] = i > 0 && order[i].Key == order[i - 1].Key ? places[order[i - 1].P.Id] : i + 1;
		return (order.Select(x => x.P.Id).ToList(), places);
	}

	/// <summary>The room as one viewer may see it: "host", or a player id.</summary>
	public RoomState ToState(string viewer)
	{
		var (order, places) = Ranking();
		return new()
		{
			Ranking = order,
			Places = places,
			AvgAnswerMs = Stats.Where(s => s.Value.AvgAnswerMs != null).ToDictionary(s => s.Key, s => s.Value.AvgAnswerMs!.Value),
			Code = Code,
			Quiz = new QuizInfo { Id = Quiz.Id, Title = Quiz.Title, QuestionCount = Quiz.Questions.Count },
			Game = Game != null && Phase == Shared.Phase.Playing ? Game.View(viewer) : null,
			Scores = new Dictionary<string, int>(Scores),
			ServerNow = Now(),
			Phase = Phase,
			Settings = Settings.Clone(),
			HostConnected = true,
			Players = Players.Select(p => new PublicPlayer { Id = p.Id, Name = p.Name, Color = p.Color, Connected = p.Connected, JoinedAt = p.JoinedAt, LeftAt = p.LeftAt }).ToList(),
		};
	}

	public static string CleanName(string raw)
	{
		var name = string.Join(' ', raw.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
		return name.Length > Limits.NameMaxLength ? name[..Limits.NameMaxLength].TrimEnd() : name;
	}

	bool Playing => Phase == Shared.Phase.Playing && Game != null;

	/// <summary>
	/// Seats a new player, or gives a returning one (same id) their seat back (and the game goes on if it waited
	/// for them). During a game, new players join too: they play from the next question.
	/// </summary>
	public (Player? Player, string? Error) Join(string id, string rawName)
	{
		var existing = GetPlayer(id);
		if (existing != null)
		{
			existing.Connected = true;
			existing.DisconnectedAt = null;
			existing.LeftAt = null;
			if (Playing) Game!.PlayerBack(id);
			return (existing, null);
		}
		var name = CleanName(rawName);
		if (name == "") return (null, "Please enter a name.");
		if (Phase == Shared.Phase.Finished) return (null, "This quiz has already ended.");
		if (Players.Count >= Limits.MaxPlayers) return (null, "This room is full.");
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
		if (Playing)
		{
			player.JoinedAt = Game!.Index + 1;
			Scores[id] = 0;
			Game.LateJoin(id);
		}
		return (player, null);
	}

	/// <summary>A player changes their name (in the lobby only, and not to one someone else uses).</summary>
	public Result Rename(string id, string rawName)
	{
		if (Phase != Shared.Phase.Lobby || GetPlayer(id) is not { } player) return Result.Fail("Names can only be changed in the waiting room.");
		var name = CleanName(rawName);
		if (name == "") return Result.Fail("Please enter a name.");
		if (Players.Any(p => p != player && string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase)))
			return Result.Fail("Someone in this room already uses that name.");
		player.Name = name;
		return Result.Success();
	}

	/// <summary>The connection dropped. During a game the seat and score stay, and the game waits for them.</summary>
	public void Disconnect(string id)
	{
		var p = GetPlayer(id);
		if (p == null || !p.Connected) return;
		p.Connected = false;
		p.DisconnectedAt = Now();
		if (!Playing) return;
		p.LeftAt = Game!.Index + 1;
		Game.PlayerLeft(id);
	}

	/// <summary>A player leaves: in the lobby their seat goes; during a game it's kept so they can come back.</summary>
	public void Leave(string id)
	{
		if (Playing) Disconnect(id);
		else Remove(id);
	}

	public void Remove(string id)
	{
		Players.RemoveAll(p => p.Id == id);
		if (Game == null) return;
		Game.PlayerBack(id);
		Game.CheckAllAnswered();
	}

	public void UpdateSettings(int? answerTimeSec = null, bool? hostShows = null, bool? hostUnlocks = null, (string Kind, bool On)? joker = null)
	{
		if (joker is { } j && Joker.All.Contains(j.Kind))
		{
			Settings.Jokers.Remove(j.Kind);
			if (j.On) Settings.Jokers.Add(j.Kind);
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
		Stats.Clear();
		foreach (var p in Players)
		{
			Scores[p.Id] = 0;
			p.JoinedAt = p.LeftAt = null;
		}
		Game = new Game(Quiz, this);
		return Result.Success();
	}

	/// <summary>Reveal if still open, otherwise the next question (or the podium after the last one).</summary>
	public void Next()
	{
		if (Game == null || Phase != Shared.Phase.Playing || Game.Paused) return;
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
		Stats.Clear();
		foreach (var p in Players) p.JoinedAt = p.LeftAt = null;
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
