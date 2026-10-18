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
	public const int NameMaxLength = 20;
	public const int MinPlayersToStart = 2;
	public const int MaxPlayersMin = 4, MaxPlayersMax = 24, MaxPlayersDefault = 8;
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

public sealed class RoomSettings
{
	public int MaxPlayers = Limits.MaxPlayersDefault;
	public int AnswerTimeSec = Limits.AnswerTimeDefault;
	/// <summary>Buzzer sound (an Sfx id) — played on the host and on every player's screen when someone buzzes.</summary>
	public string BuzzerSound = "soft-beep";
	/// <summary>Each question stays hidden until the host shows it (so they can read it out first).</summary>
	public bool HostShowsQuestions = true;
	/// <summary>Buzzers stay locked until the host unlocks them.</summary>
	public bool HostUnlocksBuzzers = true;
	public RoomSettings Clone() => new() { MaxPlayers = MaxPlayers, AnswerTimeSec = AnswerTimeSec, BuzzerSound = BuzzerSound, HostShowsQuestions = HostShowsQuestions, HostUnlocksBuzzers = HostUnlocksBuzzers };
}

public sealed class PublicPlayer
{
	public string Id = "";
	public string Name = "";
	public string Color = "#8b8fa3";
	public bool Connected;
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

	public int Score(string playerId) => Scores.TryGetValue(playerId, out var s) ? s : 0;

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

/// <summary>What players may know about the current question (never the answer before the reveal).</summary>
public sealed class PublicQuestion
{
	public string Type = QuestionType.Classic;
	public string Prompt = "";
	public int Points;
	/// <summary>Players buzz in (classic buzzer, sound, video) instead of everyone answering.</summary>
	public bool Buzzer;
	/// <summary>Asset key of the question image, if it has one.</summary>
	public string? ImageKey;
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

	public bool IsMedia => Type is QuestionType.Sound or QuestionType.Video;
	public bool HasPlayClock => IsMedia || Type == QuestionType.Image;
	public bool IsTimeline => Type == QuestionType.Order && OrderMode == Shared.OrderMode.Timeline;
}

/// <summary>A running timeline (put in order, taking turns): what's placed, what comes next, whose turn it is.</summary>
public sealed class TimelineView
{
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
	/// <summary>False while the host hasn't shown the question yet (players get no content then).</summary>
	public bool Shown = true;
	/// <summary>Buzzer questions: whether players can buzz right now.</summary>
	public bool BuzzersOpen = true;
	public RevealView? Reveal;
	/// <summary>Timeline questions, once shown.</summary>
	public TimelineView? Timeline;
	/// <summary>Only in the state sent to that player.</summary>
	public AnswerValue? MyAnswer;
	/// <summary>Only in the host's state: the correct answer, before the reveal.</summary>
	public string? HostAnswer;
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
	/// <summary>LAN mode only: who this is, so a dropped connection gets the same seat back. Steam uses the Steam ID.</summary>
	public string ClientId = "";
}

public sealed class JoinReply
{
	public bool Ok;
	public string? Error;
	public string PlayerId = "";
}

public sealed class AnswerRequest
{
	public int Index;
	public AnswerValue? Value;
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
