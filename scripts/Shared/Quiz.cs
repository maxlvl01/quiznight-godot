// The quiz a host builds in the editor. Saved as a .quiznight.json file in the user folder
// (same format as the old web version, so its exported quizzes import as-is).

using System;
using System.Collections.Generic;
using System.Linq;

namespace QuizNight.Shared;

public static class QuestionType
{
	public const string Classic = "classic";
	public const string Estimation = "estimation";
	public const string MapPin = "mapPin";
	public const string Sound = "sound";
	public const string Video = "video";

	/// <summary>Order the editor shows them in.</summary>
	public static readonly string[] All = { MapPin, Estimation, Classic, Sound, Video };

	public record Info(string Label, string Short, string Blurb);

	public static readonly Dictionary<string, Info> Infos = new()
	{
		[MapPin] = new("Map pin", "map pin", "Show a screenshot. Players pin the spot on a map. Closest wins."),
		[Estimation] = new("Estimation", "estimation", "Players guess a number. Closest guess wins."),
		[Classic] = new("Classic question", "classic", "Buzz in and say the answer, or everyone picks from up to 4 answers (one or several right)."),
		[Sound] = new("Sound", "sound", "Play a song or sound effect. Players buzz in; shows how fast it was guessed."),
		[Video] = new("Video", "video", "Play a trailer or clip. Players buzz in; shows how fast it was guessed."),
	};

	public static bool IsKnown(string type) => Infos.ContainsKey(type);
}

/// <summary>Classic questions: players buzz and say the answer, or everyone picks A–D.</summary>
public static class ClassicMode
{
	public const string Buzzer = "buzzer";
	public const string Choice = "choice";
	/// <summary>Like choice, but several answers can be right; players pick all they think are.</summary>
	public const string Multi = "multi";
}

/// <summary>What happens when several players are equally close.</summary>
public static class TieRule
{
	public const string AllScore = "allScore";
	public const string NobodyScores = "nobodyScores";

	public static readonly (string Id, string Label)[] All =
	{
		(AllScore, "Both get the point"),
		(NobodyScores, "Nobody gets the point"),
	};
}

/// <summary>How closeness questions (estimation, map pin) award points.</summary>
public static class Scoring
{
	public const string ClosestWins = "closestWins";
	public const string Top3 = "top3";

	public static readonly (string Id, string Label)[] All =
	{
		(ClosestWins, "Closest wins the round"),
		(Top3, "Top 3 score (3 / 2 / 1)"),
	};
}

public sealed class ImageAsset
{
	/// <summary>data:image/...;base64,... — empty for built-in maps, which ship with the game.</summary>
	public string DataUrl = "";
	public int Width;
	public int Height;

	public ImageAsset Clone() => new() { DataUrl = DataUrl, Width = Width, Height = Height };
}

/// <summary>A sound or video file. Media only plays on the host's screen and is never sent to players.</summary>
public sealed class MediaAsset
{
	public string DataUrl = "";
	public string Mime = "";
	public string Name = "";
	public double? DurationSec;

	public MediaAsset Clone() => new() { DataUrl = DataUrl, Mime = Mime, Name = Name, DurationSec = DurationSec };
}

/// <summary>A point on an image, stored relative to its size (0–1) so resolution never matters.</summary>
public struct NormPoint
{
	public double X;
	public double Y;

	public NormPoint(double x, double y)
	{
		X = x;
		Y = y;
	}

	public override string ToString() => $"({X:0.###}, {Y:0.###})";
}

public sealed class MapAsset
{
	public string Id = "";
	public string Name = "";
	public ImageAsset Image = new();
	/// <summary>Real width of the whole map image. Null = no scale, distances shown in % of the map.</summary>
	public double? WidthKm;
	/// <summary>Set when the map came from a built-in game map (see MapPresets).</summary>
	public string? PresetId;

	public MapAsset Clone() => new() { Id = Id, Name = Name, Image = Image.Clone(), WidthKm = WidthKm, PresetId = PresetId };
}

/// <summary>
/// One question. All types share this class; which fields matter depends on <see cref="Type"/>
/// (QuizJson only writes the ones that belong to it).
/// </summary>
public sealed class Question
{
	public string Id = "";
	public string Type = QuestionType.Classic;
	public string Prompt = "";
	/// <summary>Points for winning this question (top3: 1st gets 3×, 2nd 2×, 3rd 1×).</summary>
	public int Points = 1;

	/// <summary>Classic and estimation: optional image. Map pin: the screenshot players have to locate.</summary>
	public ImageAsset? Image;

	// classic
	public string Mode = ClassicMode.Buzzer;
	/// <summary>Classic buzzer, sound and video: the correct answer (only the host sees it).</summary>
	public string AnswerText = "";
	/// <summary>Choice mode: answers A–D (empty ones are left out).</summary>
	public List<string> Choices = new() { "", "", "", "" };
	public int CorrectChoice;
	/// <summary>Multi mode: every correct answer (indexes into Choices).</summary>
	public List<int> CorrectChoices = new();

	/// <summary>Classic choice, estimation and map pin: how long everyone has to answer.</summary>
	public int TimeLimitSec = QuestionLimits.TimeLimitDefault;

	// sound / video
	public MediaAsset? Media;
	/// <summary>Start playing this many seconds into the file (skip intros).</summary>
	public double StartSec;

	// estimation
	public double? AnswerNumber;
	public string Unit = "";

	// estimation + map pin
	public string TieRule = Shared.TieRule.AllScore;
	public string Scoring = Shared.Scoring.ClosestWins;

	// map pin
	public string? MapId;
	public NormPoint? Target;
	/// <summary>Players buzz first and only then place their pin.</summary>
	public bool BuzzFirst;

	public bool IsMedia => Type is QuestionType.Sound or QuestionType.Video;
	public bool IsCloseness => Type is QuestionType.Estimation or QuestionType.MapPin;
	/// <summary>Everyone picks from the answers (one correct, or several in multi mode).</summary>
	public bool IsChoice => Type == QuestionType.Classic && Mode is ClassicMode.Choice or ClassicMode.Multi;
	public bool IsMulti => Type == QuestionType.Classic && Mode == ClassicMode.Multi;

	/// <summary>Indexes of the right answers, in either choice mode.</summary>
	public List<int> CorrectSet => IsMulti ? CorrectChoices.Distinct().OrderBy(i => i).ToList() : new List<int> { CorrectChoice };
	/// <summary>Players buzz in (classic buzzer, sound, video) instead of everyone answering.</summary>
	public bool IsBuzzer => IsMedia || (Type == QuestionType.Classic && !IsChoice);
	public bool HasImageField => Type is QuestionType.Classic or QuestionType.Estimation or QuestionType.MapPin;

	// Through text: in-memory JsonValues don't convert between number types when read back.
	public Question Clone() =>
		QuizJson.ReadQuestion((System.Text.Json.Nodes.JsonObject)System.Text.Json.Nodes.JsonNode.Parse(QuizJson.WriteQuestion(this).ToJsonString())!);
}

public sealed class Quiz
{
	public int Version = QuizRules.FormatVersion;
	public string Id = "";
	public string Title = "";
	public long CreatedAt;
	public long UpdatedAt;
	public List<MapAsset> Maps = new();
	public List<Question> Questions = new();

	public MapAsset? MapById(string? id) => id == null ? null : Maps.FirstOrDefault(m => m.Id == id);
}

public static class QuestionLimits
{
	public const int TimeLimitMin = 5;
	public const int TimeLimitMax = 300;
	public const int TimeLimitDefault = 30;
	public const int ChoiceTimeLimitSec = 20;
	public const int MapTimeLimitSec = 60;
	public const int PointsMin = 1;
	public const int PointsMax = 10;
	public const int TitleMaxLength = 60;
	public const int PromptMaxLength = 300;
}

public static class QuizRules
{
	/// <summary>
	/// 2: classic questions got an A–D mode, new sound and video types (old files are migrated).
	/// 3: classic "multi" mode with several correct answers (correctChoices).
	/// </summary>
	public const int FormatVersion = 3;

	public static readonly string[] ChoiceLetters = { "A", "B", "C", "D" };

	/// <summary>
	/// A copy of a choice question with its answers in random order and empty ones left out, so the
	/// right answer isn't always where the quiz maker typed it. Done once per game on the host.
	/// </summary>
	public static Question ShuffleChoices(Question q, Random rng)
	{
		var copy = q.Clone();
		var filled = Enumerable.Range(0, q.Choices.Count).Where(i => !string.IsNullOrWhiteSpace(q.Choices[i])).ToList();
		var order = filled.OrderBy(_ => rng.Next()).ToList();
		var correct = q.CorrectSet;
		copy.Choices = order.Select(i => q.Choices[i]).ToList();
		copy.CorrectChoices = order.Select((old, now) => (old, now)).Where(p => correct.Contains(p.old)).Select(p => p.now).ToList();
		copy.CorrectChoice = copy.CorrectChoices.FirstOrDefault();
		return copy;
	}

	/// <summary>Largest media file the editor accepts (it is stored in the quiz file).</summary>
	public static int MediaMaxMb(string type) => type == QuestionType.Sound ? 25 : 80;

	public static Question NewQuestion(string type, string id)
	{
		var q = new Question { Id = id, Type = type, Points = QuestionLimits.PointsMin };
		switch (type)
		{
			case QuestionType.Classic:
				q.TimeLimitSec = QuestionLimits.ChoiceTimeLimitSec;
				break;
			case QuestionType.Sound:
				q.Prompt = "Name this song!";
				break;
			case QuestionType.Video:
				q.Prompt = "Which game is this trailer from?";
				break;
			case QuestionType.MapPin:
				q.Prompt = "Where is this?";
				q.TimeLimitSec = QuestionLimits.MapTimeLimitSec;
				break;
		}
		return q;
	}

	/// <summary>Short subtitle for lists, e.g. "map pin · 60 s".</summary>
	public static string Summary(Question q)
	{
		var kind = Lang.T(QuestionType.Infos[q.Type].Short);
		if (q.Type == QuestionType.Classic)
			return q.IsMulti ? $"{kind} · {Lang.T("several correct")} · {q.TimeLimitSec} s"
				: q.IsChoice ? $"{kind} · A–D · {q.TimeLimitSec} s" : $"{kind} · {Lang.T("buzzer")}";
		if (q.IsCloseness) return $"{kind} · {q.TimeLimitSec} s";
		return $"{kind} · {Lang.T("buzzer")}";
	}

	/// <summary>Problems that stop a question from being played. Empty = ready.</summary>
	public static List<string> QuestionIssues(Question q, IReadOnlyList<MapAsset> maps)
	{
		var issues = new List<string>();
		if (string.IsNullOrWhiteSpace(q.Prompt)) issues.Add(Lang.T("Question text is missing."));
		if (q.Type == QuestionType.Classic)
		{
			if (q.IsChoice)
			{
				var filled = q.Choices.Count(c => !string.IsNullOrWhiteSpace(c));
				bool Filled(int i) => i >= 0 && i < q.Choices.Count && !string.IsNullOrWhiteSpace(q.Choices[i]);
				if (filled < 2) issues.Add(Lang.T("Fill in at least two answers."));
				else if (q.IsMulti && q.CorrectSet.Count == 0) issues.Add(Lang.T("Mark at least one answer as correct."));
				else if (!q.CorrectSet.All(Filled)) issues.Add(Lang.T("The correct answer (marked) is empty."));
			}
			else if (string.IsNullOrWhiteSpace(q.AnswerText)) issues.Add(Lang.T("The correct answer is missing."));
		}
		if (q.IsMedia)
		{
			if (q.Media == null || q.Media.DataUrl == "") issues.Add(Lang.T(q.Type == QuestionType.Sound ? "Upload a sound file." : "Upload a video."));
			if (string.IsNullOrWhiteSpace(q.AnswerText)) issues.Add(Lang.T("The correct answer is missing."));
		}
		if (q.Type == QuestionType.Estimation && (q.AnswerNumber is not { } n || !double.IsFinite(n)))
			issues.Add(Lang.T("The correct number is missing."));
		if (q.Type == QuestionType.MapPin)
		{
			if (q.Image == null) issues.Add(Lang.T("Upload a screenshot."));
			if (q.MapId == null || maps.All(m => m.Id != q.MapId)) issues.Add(Lang.T("Pick a map."));
			else if (q.Target == null) issues.Add(Lang.T("Click the map to set the correct spot."));
		}
		return issues;
	}

	/// <summary>Problems that stop the whole quiz from being hosted.</summary>
	public static List<string> QuizIssues(Quiz quiz)
	{
		var issues = new List<string>();
		if (string.IsNullOrWhiteSpace(quiz.Title)) issues.Add(Lang.T("Give the quiz a title."));
		if (quiz.Questions.Count == 0) issues.Add(Lang.T("Add at least one question."));
		for (var i = 0; i < quiz.Questions.Count; i++)
		{
			var question = quiz.Questions[i];
			if (!QuestionType.IsKnown(question.Type))
			{
				issues.Add(Lang.F("Question {0} has an unknown type.", i + 1));
				continue;
			}
			foreach (var issue in QuestionIssues(question, quiz.Maps)) issues.Add(Lang.F("Question {0}: {1}", i + 1, issue));
		}
		return issues;
	}

	/// <summary>
	/// Distance between two points on a map. With a scale it's in km, otherwise in % of the
	/// map's diagonal. Takes the aspect ratio into account, so wide maps measure correctly.
	/// </summary>
	public static (double? Km, double Percent) MapDistance(MapAsset map, NormPoint a, NormPoint b) =>
		MapDistance(map.Image.Width, map.Image.Height, map.WidthKm, map.PresetId, a, b);

	public static (double? Km, double Percent) MapDistance(int width, int height, double? widthKm, string? presetId, NormPoint a, NormPoint b)
	{
		var aspect = (double)height / width;
		var dx = a.X - b.X;
		var dy = (a.Y - b.Y) * aspect; // in units of map width
		var dist = Math.Sqrt(dx * dx + dy * dy);
		var diagonal = Math.Sqrt(1 + aspect * aspect);
		var percent = dist / diagonal * 100;
		// The WoW atlas has a different scale per continent, so it measures in game yards instead.
		if (presetId == WowAtlas.PresetId) return (WowAtlas.DistanceKm(a, b), percent);
		return (widthKm is { } w && w > 0 ? dist * w : null, percent);
	}
}
