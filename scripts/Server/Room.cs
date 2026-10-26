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
	/// <summary>Answers locked in and recordings sent: time from the question's start.</summary>
	public long AnswerMs;
	public int Answers;
	/// <summary>Buzzes: time to buzz in (much quicker than typing, so it's its own average).</summary>
	public long BuzzMs;
	public int Buzzes;
	public double Km;
	public int KmAnswers;
	/// <summary>Questions they scored on.</summary>
	public int Scored;

	/// <summary>Points the host handed out: bonuses and score changes by hand (taking some away counts against it).</summary>
	public int HostPoints;

	/// <summary>Estimations: how far off, in % of the right number.</summary>
	public double PercentOff;
	public int PercentAnswers;

	public double? AvgAnswerMs => Answers > 0 ? (double)AnswerMs / Answers : null;
	public double? AvgBuzzMs => Buzzes > 0 ? (double)BuzzMs / Buzzes : null;
	public double? AvgKm => KmAnswers > 0 ? Km / KmAnswers : null;
	public double? AvgPercentOff => PercentAnswers > 0 ? PercentOff / PercentAnswers : null;
}

/// <summary>One player's go at one question, for the per-question rows of the saved results (stats per question type).</summary>
public sealed class QuestionStat
{
	/// <summary>Points from the question itself (with Double, bonus and hand-ticked answers; not the host's score changes).</summary>
	public int Points;
	public bool Right;
	/// <summary>Answered, buzzed, recorded or took a timeline turn (else they just let it pass).</summary>
	public bool Answered;
	public int Buzzes, WrongBuzzes, Placements, PlacementsRight;
	public long? BuzzMs, AnswerMs;
	public double? Km, PercentOff;
	/// <summary>Step by step: the hint (1 = first) they got it right at.</summary>
	public int? Hint;
	public string Joker = "";
	/// <summary>Typed answers: letters off from the closest accepted answer (0 = spelled perfectly).</summary>
	public int? Typos;
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

	/// <summary>Per question (0-based index) and player: how it went, for the saved results.</summary>
	public readonly Dictionary<(int Question, string Player), QuestionStat> QuestionStats = new();

	/// <summary>The current question's record for a player.</summary>
	QuestionStat Q(string playerId)
	{
		var key = (Game?.Index ?? 0, playerId);
		return QuestionStats.TryGetValue(key, out var s) ? s : QuestionStats[key] = new QuestionStat();
	}

	public readonly string Code;
	/// <summary>Full quiz including answers and media — never send this to players as-is.</summary>
	public readonly Quiz Quiz;
	readonly Func<long> _clock;

	/// <summary>The files players get ahead of time, from the lobby on (see Game.SharedMedia).</summary>
	public readonly IReadOnlyList<Game.SharedMedia> SharedFiles;

	public Room(string code, Quiz quiz, Func<long>? clock = null)
	{
		Code = code;
		Quiz = quiz;
		SharedFiles = Game.ShareFiles(quiz);
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
	public bool ShowCategories() => Settings.ShowCategories;
	public int Score(string playerId) => Scores.GetValueOrDefault(playerId);
	/// <summary>Points from the current question (they count for its per-question stats too).</summary>
	public void Award(string playerId, int points)
	{
		ChangeScore(playerId, points);
		if (Game != null) Q(playerId).Points += points;
	}

	/// <summary>The host changes a score by hand: it counts for the night (and the host's points), not for any question.</summary>
	public void AdjustScore(string playerId, int points)
	{
		ChangeScore(playerId, points);
		NoteHostPoints(playerId, points);
	}

	public void NoteHostPoints(string playerId, int points) => StatsOf(playerId).HostPoints += points;

	void ChangeScore(string playerId, int points)
	{
		Scores[playerId] = (Scores.TryGetValue(playerId, out var s) ? s : 0) + points;
		if (points != 0) StatsOf(playerId).ScoreChangedAt = Now();
	}

	public void NoteAnswerTime(string playerId, long ms)
	{
		ms = Math.Max(0, ms);
		var s = StatsOf(playerId);
		var q = Q(playerId);
		// (a new recording replaces the last one: its time too)
		if (q.AnswerMs is { } before) s.AnswerMs -= before;
		else s.Answers++;
		s.AnswerMs += ms;
		q.AnswerMs = ms;
		q.Answered = true;
	}

	public void NoteBuzzTime(string playerId, long ms)
	{
		ms = Math.Max(0, ms);
		var s = StatsOf(playerId);
		s.BuzzMs += ms;
		s.Buzzes++;
		var q = Q(playerId);
		q.BuzzMs ??= ms;
		q.Buzzes++;
		q.Answered = true;
	}

	public void NoteWrongBuzz(string playerId) => Q(playerId).WrongBuzzes++;

	public void NoteTypos(string playerId, int typos) => Q(playerId).Typos = typos;

	public void NotePlacement(string playerId, bool right)
	{
		var q = Q(playerId);
		q.Placements++;
		if (right) q.PlacementsRight++;
		q.Answered = true;
	}

	public void NoteTookPart(string playerId, bool answered, string? joker, int? hint)
	{
		var q = Q(playerId);
		q.Answered |= answered;
		q.Joker = joker ?? "";
		q.Hint = hint ?? q.Hint;
	}

	public void NoteDistance(string playerId, double km)
	{
		var s = StatsOf(playerId);
		s.Km += km;
		s.KmAnswers++;
		Q(playerId).Km = km;
	}

	public void NoteScored(string playerId)
	{
		StatsOf(playerId).Scored++;
		Q(playerId).Right = true;
	}

	public void NotePercentOff(string playerId, double percent)
	{
		var s = StatsOf(playerId);
		s.PercentOff += percent;
		s.PercentAnswers++;
		Q(playerId).PercentOff = percent;
	}

	/// <summary>
	/// Everyone's position: most points first; same points, whoever got there first; then the faster average
	/// answer time, then the faster average buzz time; then the closer average map pin; then who joined first. Players
	/// equal in all of these share a place.
	/// </summary>
	public (List<string> Order, Dictionary<string, int> Places) Ranking()
	{
		(int, long, double, double, double) Key(Player p)
		{
			var s = StatsOf(p.Id);
			return (-Score(p.Id), s.ScoreChangedAt ?? long.MaxValue, s.AvgAnswerMs ?? double.MaxValue, s.AvgBuzzMs ?? double.MaxValue, s.AvgKm ?? double.MaxValue);
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
			Stats = Stats.ToDictionary(s => s.Key, s => new NightStats
			{
				Scored = s.Value.Scored, Answers = s.Value.Answers, AvgAnswerS = s.Value.AvgAnswerMs / 1000, Buzzes = s.Value.Buzzes,
				AvgBuzzS = s.Value.AvgBuzzMs / 1000, AvgKm = s.Value.AvgKm, AvgPercentOff = s.Value.AvgPercentOff,
			}),
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
		static string Cut(string s, int max) => s.Length > max ? s[..max].TrimEnd() : s;
		// (a clan tag in front has its own room: "[OTB] maxlvl01" is fine)
		var (tag, rest) = Limits.SplitClanTag(name);
		return tag is { Length: > 0 } t ? $"[{Cut(t, Limits.ClanTagMaxLength)}] {Cut(rest, Limits.NameMaxLength)}" : Cut(name, Limits.NameMaxLength);
	}

	bool Playing => Phase == Shared.Phase.Playing && Game != null;

	/// <summary>
	/// Seats a new player, or gives a returning one (same id) their seat back (and the game goes on if it waited
	/// for them). During a game, new players join too: they play from the next question.
	/// </summary>
	public (Player? Player, string? Error) Join(string id, string rawName)
	{
		Trace.Log("room", $"join {id} as \"{rawName}\" ({Phase}, {Players.Count} in the room)");
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
		Trace.Log("room", $"disconnected {id} ({Phase})");
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
		Trace.Log("room", $"leave {id} ({Phase})");
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

	public void UpdateSettings(int? answerTimeSec = null, bool? hostShows = null, bool? hostUnlocks = null, (string Kind, bool On)? joker = null,
		bool? categories = null)
	{
		if (categories is { } cats) Settings.ShowCategories = cats;
		if (joker is { } j && Joker.All.Contains(j.Kind))
		{
			Settings.Jokers.Remove(j.Kind);
			if (j.On) Settings.Jokers.Add(j.Kind);
		}
		if (answerTimeSec is { } at) Settings.AnswerTimeSec = Math.Clamp(at, Limits.AnswerTimeMin, Limits.AnswerTimeMax);
		if (hostShows is { } shows) Settings.HostShowsQuestions = shows;
		if (hostUnlocks is { } unlocks) Settings.HostUnlocksBuzzers = unlocks;
	}

	/// <summary>When the quiz started (local time), for the results: the match history shows the night's start.</summary>
	public DateTime? StartedAt;

	public Result Start()
	{
		Trace.Log("room", $"start: {string.Join(", ", Players.Select(p => $"{p.Name}{(p.Connected ? "" : " (away)")}"))}");
		if (Phase != Shared.Phase.Lobby) return Result.Fail("The quiz is already running.");
		var connected = Players.Count(p => p.Connected);
		if (connected < Limits.MinPlayersToStart) return Result.Fail(Lang.F("You need at least {0} connected players.", Limits.MinPlayersToStart));
		Phase = Shared.Phase.Playing;
		StartedAt = DateTime.Now;
		Scores.Clear();
		Stats.Clear();
		QuestionStats.Clear();
		foreach (var p in Players)
		{
			Scores[p.Id] = 0;
			p.JoinedAt = p.LeftAt = null;
		}
		Game = new Game(Quiz, this, null, SharedFiles);
		return Result.Success();
	}

	/// <summary>Reveal if still open, otherwise the next question (or the podium after the last one).</summary>
	public void Next()
	{
		Trace.Log("room", $"next (question {(Game?.Index ?? -1) + 1}, {Game?.Stage})");
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
		QuestionStats.Clear();
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
