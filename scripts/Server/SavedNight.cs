// A quiz night saved while it runs (after every question), so a crash or an update mid-game doesn't cost the night:
// the quiz's page offers "Continue", which opens a lobby with everyone's points up to the last finished question
// (Room.Snapshot/Restore; the file: Storage.NightSaves).

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using QuizNight.Shared;

namespace QuizNight.Server;

public sealed class SavedNight
{
	public const int CurrentFormat = 1;
	public int Format = CurrentFormat;
	/// <summary>The game that saved it.</summary>
	public string Version = "";
	public string QuizId = "";
	public string QuizTitle = "";
	/// <summary>How many questions the quiz had then.</summary>
	public int Questions;
	/// <summary>The question to play next (0-based): every question before it is done and counted.</summary>
	public int Next;
	public DateTime? StartedAt;
	public RoomSettings Settings = new();
	/// <summary>In join order.</summary>
	public List<SavedPlayer> Players = new();
	public List<SavedQuestionStat> QuestionStats = new();
	/// <summary>Jokers used on the questions before Next (each kind once per player and match).</summary>
	public List<JokerPlay> Jokers = new();

	/// <summary>When the file was written (from the file, not stored in it).</summary>
	[JsonIgnore] public DateTime SavedAt;

	static readonly JsonSerializerOptions Json = new()
	{
		IncludeFields = true,
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
		IgnoreReadOnlyProperties = true,
		DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
		WriteIndented = true,
	};

	public string ToJson() => JsonSerializer.Serialize(this, Json);

	/// <summary>Null if it isn't a saved night this game can read.</summary>
	public static SavedNight? FromJson(string text)
	{
		try
		{
			var save = JsonSerializer.Deserialize<SavedNight>(text, Json);
			return save is { Format: <= CurrentFormat, QuizId.Length: > 0, Players.Count: > 0 } ? save : null;
		}
		catch (Exception)
		{
			return null;
		}
	}
}

public sealed class SavedPlayer
{
	public string Id = "";
	public string Name = "";
	public string Color = "";
	public int Score;
	public int? JoinedAt;
	public int? LeftAt;
	public PlayerStats Stats = new();
}

public sealed class SavedQuestionStat
{
	public int Question;
	public string Player = "";
	public QuestionStat Stat = new();
}
