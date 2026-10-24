// Types and constants shared by the host (which runs the game) and the players.
// Messages are JSON: {"t": "<type>", "d": <payload>}.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace QuizNight.Shared;

public static class Limits
{
	public const int CodeLength = 4;
	public const int NameMaxLength = 12;
	/// <summary>A clan tag in square brackets before the name ("[OTB] maxlvl") doesn't count: up to this many inside the brackets.</summary>
	public const int ClanTagMaxLength = 6;
	/// <summary>Name fields: a name and a clan tag with its brackets and space.</summary>
	public const int NameInputMax = NameMaxLength + ClanTagMaxLength + 3;

	/// <summary>"[OTB] maxlvl" → ("OTB", "maxlvl"); a name without a tag → (null, name).</summary>
	public static (string? Tag, string Name) SplitClanTag(string name)
	{
		var m = System.Text.RegularExpressions.Regex.Match(name.Trim(), @"^\[([^\[\]]{1,24})\]\s*(\S.*)$");
		return m.Success ? (m.Groups[1].Value.Trim(), m.Groups[2].Value) : (null, name);
	}
	public const int MinPlayersToStart = 2;
	/// <summary>Seats in a room (no setting: big enough for big nights; one colour each in PlayerColors).</summary>
	public const int MaxPlayers = 24;
	public const int AnswerTimeMin = 3, AnswerTimeMax = 60, AnswerTimeDefault = 10;

	// Room codes skip look-alike characters (0/O, 1/I) so they are easy to read out loud.
	public const string CodeAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

	public static readonly string[] PlayerColors =
	{
		"#ff6b81", "#6f8cff", "#b07cff", "#ffad5c", "#4fd1a5", "#ffd84d",
		"#5cc8ff", "#ff8fd0", "#9be15d", "#ff7a45", "#8f7dff", "#3ddbd9",
		"#e0a3ff", "#ffb3a7", "#8ad4ff", "#c6e377", "#ff5c8a", "#a3b1ff",
		"#f7c873", "#5ce1b5", "#d68cff", "#ff9966", "#66e0ff", "#b8f28a",
	};

	public static string NormalizeCode(string raw)
	{
		var sb = new StringBuilder();
		foreach (var ch in raw.ToUpperInvariant())
			if (ch is >= 'A' and <= 'Z' or >= '0' and <= '9' && sb.Length < CodeLength) sb.Append(ch);
		return sb.ToString();
	}
}

public static class Phase
{
	public const string Lobby = "lobby";
	public const string Playing = "playing";
	public const string Finished = "finished";
}

/// <summary>'open': players answer or buzz. 'reveal': answer and points are shown.</summary>
public static class Stage
{
	public const string Open = "open";
	public const string Reveal = "reveal";
}

public static class BuzzStatus
{
	public const string Waiting = "waiting";
	public const string Answering = "answering";
	public const string Wrong = "wrong";
	public const string Correct = "correct";
}

/// <summary>
/// Jokers: each player can play each kind the host allows once per match, before a question is shown (during the
/// reveal before it, or while it's still hidden).
/// </summary>
public static class Joker
{
	/// <summary>Double points: whatever the question gives (or the host takes) counts twice.</summary>
	public const string Double = "double";
	/// <summary>Fast Pass (buzzer questions only): your buzz goes ahead of everyone waiting in line. One player per question (first come); Double: anyone.</summary>
	public const string FastPass = "fastPass";
	public static readonly string[] All = { Double, FastPass };

	public static string Label(string kind) => kind == Double ? "×2 Double points" : "⚡ Fast Pass";
	public static string Icon(string kind) => kind == Double ? "×2" : "⚡";
}

public sealed class JokerPlay
{
	public string PlayerId = "";
	public string Kind = Joker.Double;
	/// <summary>The question (index) it's for.</summary>
	public int Index;
}

public sealed class RoomSettings
{
	public int AnswerTimeSec = Limits.AnswerTimeDefault;
	/// <summary>Buzzer sound (an Sfx id) — played on the host and on every player's screen when someone buzzes.</summary>
	public string BuzzerSound = "soft-beep";
	/// <summary>Podium sound (an Sfx podium id) for the final results; players hear it unless they chose their own.</summary>
	public string PodiumSound = "f1-podium";
	/// <summary>Each question stays hidden until the host shows it (so they can read it out first).</summary>
	public bool HostShowsQuestions = true;
	/// <summary>Buzzers stay locked until the host unlocks them.</summary>
	public bool HostUnlocksBuzzers = true;
	/// <summary>The jokers players may play this match (Joker kinds).</summary>
	public List<string> Jokers = new(Joker.All);
	public RoomSettings Clone() => new() { AnswerTimeSec = AnswerTimeSec, BuzzerSound = BuzzerSound, PodiumSound = PodiumSound, HostShowsQuestions = HostShowsQuestions, HostUnlocksBuzzers = HostUnlocksBuzzers, Jokers = Jokers.ToList() };
}

public sealed class PublicPlayer
{
	public string Id = "";
	public string Name = "";
	public string Color = "#8b8fa3";
	public bool Connected;
	/// <summary>Joined after the start: the question (1-based) they came in at.</summary>
	public int? JoinedAt;
	/// <summary>Left during the game (and not back yet): the question (1-based) they left at.</summary>
	public int? LeftAt;
}

public sealed class QuizInfo
{
	public string Id = "";
	public string Title = "";
	public int QuestionCount;
}

public sealed class RoomState
{
	public string Code = "";
	public string Phase = Shared.Phase.Lobby;
	public RoomSettings Settings = new();
	public List<PublicPlayer> Players = new();
	public bool HostConnected = true;
	public QuizInfo Quiz = new();
	public GameView? Game;
	/// <summary>Points per player id, kept until the room goes back to the lobby.</summary>
	public Dictionary<string, int> Scores = new();
	/// <summary>Host clock when this state was made, so players can turn deadlines into a countdown.</summary>
	public long ServerNow;

	/// <summary>Player ids by position (points, then the tiebreaks: Room.Ranking), and each one's place (equal ones share).</summary>
	public List<string> Ranking = new();
	public Dictionary<string, int> Places = new();
	/// <summary>Average buzz/answer time per player (ms), shown with the scores.</summary>
	public Dictionary<string, double> AvgAnswerMs = new();
	/// <summary>How much of the quiz's pictures, sounds and videos each player has, 0–100 (all of it, ahead of time).</summary>
	public Dictionary<string, int> Downloads = new();

	public int Score(string playerId) => Scores.TryGetValue(playerId, out var s) ? s : 0;

	/// <summary>Players by position (falls back to score when the host sent no ranking).</summary>
	public List<PublicPlayer> Ranked() =>
		Ranking.Count == Players.Count && Ranking.All(id => Players.Any(p => p.Id == id))
			? Ranking.Select(Player).ToList()
			: Players.OrderByDescending(p => Score(p.Id)).ToList();

	public int Place(string playerId) => Places.TryGetValue(playerId, out var place) ? place : Ranked().FindIndex(p => p.Id == playerId) + 1;

	public PublicPlayer Player(string id) =>
		Players.Find(p => p.Id == id) ?? new PublicPlayer { Id = id, Name = "Someone", Color = "#8b8fa3" };
}

/// <summary>A number (estimation), a choice index (A–D), several choice indexes (multi) or a point on the map (map pin).</summary>
public sealed class AnswerValue
{
	public double? Num;
	public NormPoint? Point;
	public List<int>? Picks;
	/// <summary>Write the answer: what the player typed.</summary>
	public string? Text;

	public static AnswerValue Number(double n) => new() { Num = n };
	public static AnswerValue At(NormPoint p) => new() { Point = p };
	public static AnswerValue Picked(IEnumerable<int> picks) => new() { Picks = picks.ToList() };
	public static AnswerValue Written(string text) => new() { Text = text };
}

/// <summary>Where players can get a question's map. Built-in maps ship with the game.</summary>
public sealed class PublicMap
{
	public string Name = "";
	public int Width;
	public int Height;
	public double? WidthKm;
	public string? PresetId;
	/// <summary>Asset key to request it from the host (custom maps only).</summary>
	public string? AssetKey;
}

/// <summary>A file a player was sent ahead of time (MediaChunk), and the key that unlocks it.</summary>
public sealed class SharedFileRef
{
	public string Id = "";
	public string Key = "";
	public string Mime = "";
}

/// <summary>What players may know about the current question (never the answer before the reveal).</summary>
public sealed class PublicQuestion
{
	public string Type = QuestionType.Classic;
	public string Prompt = "";
	public int Points;
	/// <summary>Players buzz in (classic buzzer, sound, video) instead of everyone answering.</summary>
	public bool Buzzer;
	/// <summary>Everyone types an answer (write the answer, or a buzzer question played as "everyone types").</summary>
	public bool Written;
	/// <summary>The bonus the host can give at the reveal (0 = none) and what it's for.</summary>
	public int BonusPoints;
	public string? BonusText;
	/// <summary>Asset key of the question image, if it has one (picture questions: the slide showing now).</summary>
	public string? ImageKey;
	/// <summary>
	/// The shown question's pictures (asset key → file): players got them ahead of time, encrypted; this unlocks them.
	/// A picture not listed (or not downloaded yet) is fetched by its asset key instead.
	/// </summary>
	public Dictionary<string, SharedFileRef>? Files;
	/// <summary>Picture questions: which picture shows (0-based) and how many there are.</summary>
	public int? Slide;
	public int? SlideCount;
	/// <summary>The host has shown every picture: players can flip through them on their own screen.</summary>
	public bool AllSlidesShown;
	/// <summary>Step by step: the hints flipped so far (all of them at the reveal), how many there are, and what a right answer is worth now.</summary>
	public List<string>? Hints;
	public int? HintCount;
	public int? WorthNow;
	public string? Mode;
	/// <summary>Choice mode: answers by letter index; empty answers are null.</summary>
	public List<string?>? Choices;
	/// <summary>Put in order: the items, shuffled, and what the top and bottom of the list mean.</summary>
	public List<string>? Items;
	public string? OrderTop;
	public string? OrderBottom;
	/// <summary>Put in order: "all" or "timeline".</summary>
	public string? OrderMode;
	public string? Unit;
	public PublicMap? Map;
	public int? TimeLimitSec;
	public double? StartSec;
	/// <summary>
	/// Sound and video questions: the file the players' screens play too. It's sent to them ahead of time, encrypted
	/// (MediaChunk); this id and the key to unlock it only come once the question is shown.
	/// </summary>
	public string? MediaId;
	public string? MediaKey;
	public string? MediaMime;
	/// <summary>Image reveal: how the picture is revealed (players reveal it in step with the host).</summary>
	public string? RevealStyle;
	public int? RevealSec;
	public NormPoint? Focus;

	public bool IsMedia => Type is QuestionType.Sound or QuestionType.Video;
	public bool HasPlayClock => IsMedia || Type == QuestionType.Image;
	public bool IsTimeline => Type == QuestionType.Order && OrderMode == Shared.OrderMode.Timeline;
}

/// <summary>Where an interactive question is: players record, then the host plays the recordings, then (maybe) everyone votes.</summary>
public static class InteractivePhase
{
	public const string Recording = "recording";
	public const string Listening = "listening";
	public const string Voting = "voting";
}

/// <summary>A player's recording: fetched by its asset key ("q3/rec0") like a picture.</summary>
public sealed class InteractiveClip
{
	public string PlayerId = "";
	public string Key = "";
	public double Sec;
}

/// <summary>A running interactive question.</summary>
public sealed class InteractiveView
{
	public string Phase = InteractivePhase.Recording;
	public string Judging = Shared.Judging.Vote;
	/// <summary>Everyone's recordings, once recording is over.</summary>
	public List<InteractiveClip> Clips = new();
	/// <summary>The recording the host is on, and how often the host pressed play (each press plays it everywhere).</summary>
	public int Clip;
	public int Plays;
	/// <summary>Who has sent a recording (recording), or voted (voting).</summary>
	public List<string> DoneIds = new();
	/// <summary>Only in the state sent to that player: the clip they voted for.</summary>
	public int? MyVote;
}

/// <summary>A running timeline (put in order, taking turns): what's placed, what comes next, whose turn it is.</summary>
public sealed class TimelineView
{
	/// <summary>Choosing (the player picks a gap), Locked (waiting for the host to reveal), Result (waiting for the next turn).</summary>
	public string Phase = TimelinePhase.Choosing;
	/// <summary>Locked: the gap the player picked.</summary>
	public int? PendingGap;
	/// <summary>The items on the timeline so far, left to right.</summary>
	public List<TimelineCard> Placed = new();
	/// <summary>The item to place now (null once all are placed).</summary>
	public string? Current;
	/// <summary>Items still to come after the current one.</summary>
	public int Left;
	public string? TurnPlayerId;
	/// <summary>Host-clock deadline for the current turn.</summary>
	public long? TurnEndsAt;
	/// <summary>Gaps already tried for the current item (gap g = before Placed[g]; Placed.Count = after the last).</summary>
	public List<int> WrongGaps = new();
	/// <summary>Turn order (fewest points first when the question was shown).</summary>
	public List<string> TurnOrder = new();
	/// <summary>Players who already missed the current item.</summary>
	public List<string> Missed = new();
	public TimelineEvent? Last;
}

public sealed class TimelineCard
{
	public string Text = "";
	/// <summary>What it shows once placed, e.g. its year.</summary>
	public string Label = "";
	/// <summary>Who placed it (null: it started there, or nobody got it).</summary>
	public string? PlacedBy;
	public bool Start;
}

/// <summary>The steps of one turn; the host moves it from Locked and from Result.</summary>
public static class TimelinePhase
{
	public const string Choosing = "choosing";
	public const string Locked = "locked";
	public const string Result = "result";
}

/// <summary>What just happened on the timeline.</summary>
public sealed class TimelineEvent
{
	public string Kind = TimelineEventKind.Right;
	public string? PlayerId;
	public string Item = "";
}

public static class TimelineEventKind
{
	public const string Right = "right";
	public const string Wrong = "wrong";
	/// <summary>The player locked in a gap; the host hasn't revealed it yet.</summary>
	public const string Locked = "locked";
	/// <summary>The turn's time ran out.</summary>
	public const string TimeUp = "timeUp";
	/// <summary>The host skipped the player (or they left).</summary>
	public const string Skipped = "skipped";
	/// <summary>Nobody got it (or the host placed it): it was put in its place for free.</summary>
	public const string Free = "free";
}

public sealed class BuzzEntry
{
	public string PlayerId = "";
	public string Status = BuzzStatus.Waiting;
	/// <summary>Sound/video: seconds of playback heard before buzzing. Classic: seconds since the question opened.</summary>
	public double AtSec;
	/// <summary>Buzzed with a Fast Pass (went ahead of the line).</summary>
	public bool FastPass;
}

public sealed class RevealEntry
{
	public string PlayerId = "";
	public AnswerValue? Value;
	/// <summary>Shown next to the player, e.g. "312 m", "B" or "1.2 km off".</summary>
	public string Text = "";
	public bool Correct;
	public int Points;
	public int? Rank;
	/// <summary>The host gave this player the question's bonus (its points are in Points).</summary>
	public bool Bonus;
	/// <summary>Played Double points on this question (Points are already doubled).</summary>
	public bool Doubled;
}

public sealed class RevealView
{
	public string AnswerText = "";
	/// <summary>Map pin: the correct spot.</summary>
	public NormPoint? Target;
	public List<string> WinnerIds = new();
	/// <summary>Sound/video: seconds of playback it took until someone guessed it correctly.</summary>
	public double? GuessTimeSec;
	public List<RevealEntry> Entries = new();
	/// <summary>Choice modes: how many picked each letter, and which ones were right.</summary>
	public int[]? Counts;
	public List<int>? CorrectIndices;
}

public sealed class GameView
{
	public int Index;
	public int Total;
	public string Stage = Shared.Stage.Open;
	public PublicQuestion Question = new();
	/// <summary>Host-clock deadline for timed questions.</summary>
	public long? EndsAt;
	/// <summary>Host-clock deadline for the player who is answering after a buzz.</summary>
	public long? AnswerEndsAt;
	/// <summary>Who has locked in an answer (timed questions).</summary>
	public List<string> AnsweredIds = new();
	public List<BuzzEntry> Buzzes = new();
	public bool MediaPlaying;
	/// <summary>How long the host's sound/video has played so far (at the state's ServerNow), and whether it ever started.</summary>
	public long MediaPlayedMs;
	public bool MediaStarted;
	/// <summary>False while the host hasn't shown the question yet (players get no content then).</summary>
	public bool Shown = true;
	/// <summary>Buzzer questions: whether players can buzz right now.</summary>
	public bool BuzzersOpen = true;
	public RevealView? Reveal;
	/// <summary>Timeline questions, once shown.</summary>
	public TimelineView? Timeline;
	/// <summary>Interactive questions, once shown.</summary>
	public InteractiveView? Interactive;
	/// <summary>Only in the state sent to that player.</summary>
	public AnswerValue? MyAnswer;
	/// <summary>Only in the host's state: the correct answer, before the reveal.</summary>
	public string? HostAnswer;
	/// <summary>Someone left: answers, buzzers and timers wait until they're back or the host continues.</summary>
	public bool Paused;
	/// <summary>Paused: who the game waits for.</summary>
	public List<string> WaitingFor = new();
	/// <summary>Joined during this question: they play from the next one.</summary>
	public List<string> LateIds = new();
	/// <summary>Sound/video questions: how much of the file each player has (0–100).</summary>
	public Dictionary<string, int>? MediaProgress;
	/// <summary>The question jokers can be played for right now (null: not now), and whether it's a buzzer question.</summary>
	public int? JokerIndex;
	public bool JokerBuzzer;
	/// <summary>Someone else already plays the Fast Pass on that question (only one per question; who stays secret).</summary>
	public bool FastPassTaken;
	/// <summary>Jokers played: everyone's once their question is shown, your own (still takeable back) before that.</summary>
	public List<JokerPlay> Jokers = new();
}

public sealed class Result
{
	public bool Ok;
	public string? Error;
	public static Result Success() => new() { Ok = true };
	public static Result Fail(string error) => new() { Ok = false, Error = error };
}

// ---------- messages ----------

public static class Msg
{
	// player → host
	public const string Join = "join";
	public const string Leave = "leave";
	public const string Buzz = "buzz";
	public const string Answer = "answer";
	public const string AssetRequest = "assetReq";
	public const string MediaProgress = "mediaProgress";
	public const string Joker = "joker";
	public const string Rename = "rename";
	/// <summary>Part of an interactive question's recording (RecordingPart).</summary>
	public const string Recording = "recording";

	// host → player
	public const string Joined = "joined";
	public const string State = "state";
	public const string AnswerResult = "answerResult";
	public const string Closed = "closed";
	public const string Kicked = "kicked";
	public const string Asset = "asset";

	public static readonly JsonSerializerOptions Json = new()
	{
		IncludeFields = true,
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
		DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
	};

	public static byte[] Encode(string type, object? payload = null)
	{
		var o = new JsonObject { ["t"] = type };
		if (payload != null) o["d"] = JsonSerializer.SerializeToNode(payload, payload.GetType(), Json);
		return Encoding.UTF8.GetBytes(o.ToJsonString());
	}

	/// <summary>Splits a message into its type and payload. Null if it isn't valid.</summary>
	public static (string Type, JsonNode? Data)? Decode(byte[] bytes)
	{
		try
		{
			if (JsonNode.Parse(bytes) is JsonObject o && o["t"] is JsonValue t && t.TryGetValue<string>(out var type)) return (type, o["d"]);
		}
		catch (Exception)
		{
			// Garbage from a peer must never crash the host.
		}
		return null;
	}

	public static T? Read<T>(JsonNode? data) where T : class
	{
		if (data == null) return null;
		try
		{
			return data.Deserialize<T>(Json);
		}
		catch (Exception)
		{
			return null;
		}
	}
}

public sealed class JoinRequest
{
	public string Name = "";
	/// <summary>
	/// Who this is, so a dropped connection gets the same seat back. LAN: an id per app run. Steam: "steam-" + the
	/// player's Steam ID, used only if Steam itself doesn't tell the host who connected.
	/// </summary>
	public string ClientId = "";
}

public sealed class JoinReply
{
	public bool Ok;
	public string? Error;
	public string PlayerId = "";
}

/// <summary>A player changes their name in the waiting room.</summary>
public sealed class RenameRequest
{
	public string Name = "";
}

/// <summary>Play a joker for the next question (On), or take it back before the question is shown.</summary>
public sealed class JokerRequest
{
	public string Kind = Shared.Joker.Double;
	public bool On = true;
}

public sealed class AnswerRequest
{
	public int Index;
	public AnswerValue? Value;
}

/// <summary>How much of a sound/video file a player has (the host waits for everyone before starting it).</summary>
/// <summary>A player's recording for an interactive question, in parts (a WAV file, base64; Steam messages are limited).</summary>
public sealed class RecordingPart
{
	public int Index;
	public int Part;
	public int Parts;
	public string Data = "";
}

public sealed class MediaProgress
{
	public string Id = "";
	public int Percent;
}

public sealed class AssetRequest
{
	public string Key = "";
}

/// <summary>One piece of an image (big files are split so they fit Steam's message limit).</summary>
public sealed class AssetPart
{
	public string Key = "";
	public string Mime = "";
	public int Part;
	public int Parts;
	public string Data = ""; // base64
}

public sealed class ClosedNotice
{
	public string Reason = "";
}

/// <summary>
/// A piece of an encrypted video, as a binary message (JSON messages start with '{', these with Marker):
/// [Marker][id length][id, UTF-8][total size, int64][offset, int64][data]. The file is the video encrypted
/// with AES-CBC (16-byte IV first), so players can't watch it before the host shows its question.
/// </summary>
public static class MediaChunk
{
	public const byte Marker = 1;
	public const int MaxData = 256 * 1024;

	public static byte[] Encode(string id, long total, long offset, ReadOnlySpan<byte> data)
	{
		var idBytes = Encoding.UTF8.GetBytes(id);
		var msg = new byte[2 + idBytes.Length + 16 + data.Length];
		msg[0] = Marker;
		msg[1] = (byte)idBytes.Length;
		idBytes.CopyTo(msg, 2);
		BitConverter.TryWriteBytes(msg.AsSpan(2 + idBytes.Length), total);
		BitConverter.TryWriteBytes(msg.AsSpan(10 + idBytes.Length), offset);
		data.CopyTo(msg.AsSpan(18 + idBytes.Length));
		return msg;
	}

	public static bool TryDecode(byte[] msg, out string id, out long total, out long offset, out ReadOnlyMemory<byte> data)
	{
		id = "";
		total = offset = 0;
		data = default;
		if (msg.Length < 18 || msg[0] != Marker || msg.Length < 18 + msg[1]) return false;
		id = Encoding.UTF8.GetString(msg, 2, msg[1]);
		total = BitConverter.ToInt64(msg, 2 + msg[1]);
		offset = BitConverter.ToInt64(msg, 10 + msg[1]);
		data = msg.AsMemory(18 + msg[1]);
		return total > 0 && offset >= 0 && offset + data.Length <= total;
	}
}
