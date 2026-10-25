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
	public const string Image = "image";
	public const string Order = "order";
	public const string Write = "write";
	/// <summary>One picture or a slideshow the host flips through, answered like a classic question.</summary>
	public const string Picture = "picture";
	/// <summary>A task players do with their microphone (e.g. imitate a sound); then everyone votes, or the host picks.</summary>
	public const string Interactive = "interactive";
	/// <summary>Up to 5 hints, hardest first, that the host flips one by one; the earlier someone buzzes in right, the more points.</summary>
	public const string Steps = "steps";

	/// <summary>Order the editor shows them in.</summary>
	public static readonly string[] All = { MapPin, Estimation, Classic, Picture, Sound, Video, Image, Order, Write, Steps, Interactive };

	/// <summary>Help: what it does, then its variants and options (the "?" on the type cards).</summary>
	public record Info(string Label, string Short, string Blurb, string Intro = "", string[]? Details = null);

	public static readonly Dictionary<string, Info> Infos = new()
	{
		[MapPin] = new("Map pin", "map pin", "Show a screenshot. Players pin the spot on a map. Closest wins.",
			"Show a screenshot (or none), and everyone places a pin on a map. The closest pin wins.",
			new[]
			{
				"Maps: built-in game maps with real distances (Skyrim, the seamless WoW Classic map and more), or any picture you upload.",
				"Scoring: only the closest wins, or the top 3 score (3, 2 and 1 times the points).",
				"Ties: all tied players score, or nobody does.",
				"Players can click the screenshot to see it big.",
			}),
		[Estimation] = new("Estimation", "estimation", "Players guess a number. Closest guess wins.",
			"Everyone types a number. The closest guess wins.",
			new[]
			{
				"Add a unit (e.g. km or hours) and a picture if you like.",
				"Scoring: only the closest wins, or the top 3 score (3, 2 and 1 times the points).",
				"Ties: all tied players score, or nobody does.",
			}),
		[Classic] = new("Classic question", "classic", "Buzz in and say the answer, or everyone picks from up to 4 answers (one or several right).",
			"Three ways to play:",
			new[]
			{
				"Buzzer: players buzz in and say the answer out loud, and you judge Correct or Wrong. Or pick \"Everyone types\": everyone types it instead, and matching answers are ticked for you.",
				"A B C D: up to 4 answers, one is right. Everyone picks one within the time limit.",
				"Multiple choice: several answers can be right. Only exactly the right ones score.",
				"All of them can have a picture, and a bonus you give at the reveal.",
			}),
		[Picture] = new("Picture", "picture", "Show a picture (e.g. a Steam review) or a slideshow you flip through. Answered like a classic question.",
			"Show one picture, or several as a slideshow, on everyone's screen. Players answer like a classic question.",
			new[]
			{
				"One picture: e.g. a Steam review, a meme or a chart. Players read it and buzz in.",
				"Slideshow: add several pictures and flip through them with ◀ ▶ (or the arrow keys); every player's screen follows.",
				"Answer it like a classic question: buzzer, everyone types, A B C D or multiple choice. A bonus works too.",
			}),
		[Sound] = new("Sound", "sound", "Play a song or sound effect. Players buzz in; shows how fast it was guessed.",
			"Play a song or sound effect (MP3, OGG or WAV). It plays on the players' PCs too, once you press Start.",
			new[]
			{
				"Buzz in: players buzz when they know it; the sound pauses while someone answers. The reveal shows how much listening it took.",
				"Everyone types: everyone types the answer within the time limit.",
				"You can start it at any second, and add a bonus (e.g. \"also name the game\").",
			}),
		[Video] = new("Video", "video", "Play a trailer or clip. Players buzz in; shows how fast it was guessed.",
			"Play a trailer or clip. It plays on the players' PCs too; until you press Start they see it blurred.",
			new[]
			{
				"Buzz in: players buzz when they know it; the video pauses while someone answers. The reveal shows how much watching it took.",
				"Everyone types: everyone types the answer within the time limit.",
				"Videos must be .ogv: the editor helps you turn an MP4 into one with AnyConverter.",
			}),
		[Image] = new("Image reveal", "image reveal", "A picture starts zoomed right in or pixelated and slowly gets clearer. Players buzz in; shows how fast it was guessed.",
			"A picture is hidden and slowly gets clearer, on everyone's screen.",
			new[]
			{
				"Zoom: it starts zoomed right in on a spot you pick, then zooms out.",
				"Pixelated: it starts as big blocks that get finer.",
				"You set how many seconds until it's fully clear. Players buzz in (or everyone types); it pauses while someone answers.",
			}),
		[Order] = new("Put in order", "order", "Players put a few things in the right order, e.g. oldest to newest. Everyone at once, or taking turns on a timeline.",
			"Players put a few things in the right order, e.g. oldest to newest.",
			new[]
			{
				"Everyone at once: everyone sorts the list on their own screen within the time limit. All in the right order scores.",
				"Timeline: players take turns placing one item between the cards already on the timeline (fewest points go first). A right spot scores; a wrong one passes the item on. Labels like years show once an item is placed.",
			}),
		[Write] = new("Write the answer", "write", "Everyone types their answer. Matching answers are ticked for you; you can tick or untick any at the reveal.",
			"Everyone types their answer within the time limit.",
			new[]
			{
				"Accepted answers: list several, separated by / (e.g. \"Canberra / ACT\"). Small typos are forgiven, and capitals and accents don't matter.",
				"Matching answers are ticked for you. At the reveal you can tick or untick any answer, and give a bonus.",
			}),
		[Steps] = new("Step by step", "step by step", "Up to 5 hints, hardest first, flipped one by one. The earlier someone buzzes in right, the more points.",
			"Players guess something (a historical event, a Bundesliga season, an F1 driver…) from hints you flip over one by one.",
			new[]
			{
				"Up to 5 hints, the hardest first. Players buzz in when they know it and say the answer; you judge Correct or Wrong.",
				"Right on the first hint gets as many points as there are hints (5 with five), each hint later one less.",
				"Every new hint gives everyone another go, also who was wrong before.",
			}),
		[Interactive] = new("Interactive", "interactive", "Give everyone a task, like imitating a sound. They record it with their microphone; then everyone votes, or you pick your favourite.",
			"Give everyone a task they do with their microphone, e.g. \"Imitate the Super Mario jump sound\".",
			new[]
			{
				"Players record themselves within the time you set (each recording up to 15 seconds), listen back and send it.",
				"Then you play the recordings one by one, on your screen and on every player's PC.",
				"Players vote: every vote is a point for that recording (not your own). Or you pick your favourite, who gets the question's points.",
				"Double points doubles what a player gets.",
			}),
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

/// <summary>How a put-in-order question is played.</summary>
public static class OrderMode
{
	/// <summary>Everyone sorts all the items at once on their own screen.</summary>
	public const string All = "all";
	/// <summary>Players take turns placing one item at a time on a timeline that starts with one item.</summary>
	public const string Timeline = "timeline";

	public static readonly (string Id, string Label)[] Modes =
	{
		(All, "Everyone at once"),
		(Timeline, "Timeline — take turns"),
	};
}

/// <summary>How an interactive question's recordings are judged.</summary>
public static class Judging
{
	/// <summary>Players vote for the best (not their own): a point per vote.</summary>
	public const string Vote = "vote";
	/// <summary>The host picks a favourite, who gets the question's points.</summary>
	public const string Host = "host";

	public static readonly (string Id, string Label)[] All =
	{
		(Vote, "Players vote"),
		(Host, "I pick my favourite"),
	};
}

/// <summary>How an image reveal question uncovers its picture.</summary>
public static class RevealStyle
{
	public const string Zoom = "zoom";
	public const string Pixelate = "pixelate";

	public static readonly (string Id, string Label)[] All =
	{
		(Zoom, "Zoomed in, zooms out"),
		(Pixelate, "Pixelated, gets sharper"),
	};
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
	/// <summary>Optional bonus the host can give at the reveal, e.g. "Also name the game" (0 = no bonus).</summary>
	public int BonusPoints;
	public string BonusText = "";
	/// <summary>Classic buzzer, sound, video, image reveal: everyone types their answer instead of buzzing in.</summary>
	public bool Typed;

	/// <summary>Classic and estimation: optional image. Map pin: the screenshot players have to locate.</summary>
	public ImageAsset? Image;
	/// <summary>Picture: the pictures, in order (one = a still picture, more = a slideshow the host flips through).</summary>
	public List<ImageAsset> Slides = new();

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

	// image reveal
	public string RevealStyle = Shared.RevealStyle.Zoom;
	/// <summary>How long the picture takes to become fully clear.</summary>
	public int RevealSec = QuestionLimits.RevealSecDefault;
	/// <summary>Zoom: the spot it starts zoomed in on (0–1 on the picture).</summary>
	public NormPoint Focus = new(0.5, 0.5);

	// put in order: the items in the right order (top first), and what the two ends mean
	public List<string> Items = new() { "", "", "" };
	public string OrderTop = "";
	public string OrderBottom = "";
	/// <summary>In a running game only (not saved): the shown, shuffled items' indexes in the right order.</summary>
	public List<int> CorrectOrder = new();
	public string OrderMode = Shared.OrderMode.All;
	/// <summary>Timeline: what each item shows once it's placed, e.g. its year (same index as Items).</summary>
	public List<string> ItemLabels = new();
	/// <summary>Timeline: the item (index into Items) that starts on the timeline. Only in a game: picked at random per game (ShuffleOrder).</summary>
	public int TimelineStart;
	/// <summary>Timeline: whoever places an item right places the next one too (else the turn passes on).</summary>
	public bool KeepGoing = true;

	// estimation
	public double? AnswerNumber;
	public string Unit = "";

	// estimation + map pin
	public string TieRule = Shared.TieRule.AllScore;
	public string Scoring = Shared.Scoring.ClosestWins;
	/// <summary>Points per place (1st, 2nd, …), e.g. F1's 25 / 18 / 15. Empty: Scoring and Points decide (ScoringPresets).</summary>
	public List<int> RankPoints = new();
	public bool HasRankPoints => IsCloseness && RankPoints.Count > 0;
	/// <summary>The most a player can get: the question's points, 3× with top 3, or the best place's points.</summary>
	public int MaxPoints => HasRankPoints ? RankPoints.Max() : IsCloseness && Scoring == Shared.Scoring.Top3 ? Points * 3 : Points;

	// map pin
	public string? MapId;
	public NormPoint? Target;
	/// <summary>Players buzz first and only then place their pin.</summary>
	public bool BuzzFirst;

	// step by step: the hints, hardest first
	public List<string> Hints = new() { "", "", "", "", "" };
	public bool IsSteps => Type == QuestionType.Steps;

	// interactive
	public string Judging = Shared.Judging.Vote;
	public bool IsInteractive => Type == QuestionType.Interactive;

	public bool IsMedia => Type is QuestionType.Sound or QuestionType.Video;
	/// <summary>Sound, video and image reveal: buzz times count on a clock that only runs while it plays.</summary>
	public bool HasPlayClock => IsMedia || Type == QuestionType.Image;
	public bool IsCloseness => Type is QuestionType.Estimation or QuestionType.MapPin;
	/// <summary>Answered like a classic question (buzzer, everyone types, A–D, multiple choice): classic and picture.</summary>
	public bool IsClassicLike => Type is QuestionType.Classic or QuestionType.Picture;
	/// <summary>Everyone picks from the answers (one correct, or several in multi mode).</summary>
	public bool IsChoice => IsClassicLike && Mode is ClassicMode.Choice or ClassicMode.Multi;
	public bool IsMulti => IsClassicLike && Mode == ClassicMode.Multi;
	/// <summary>Put in order, played in turns on a timeline.</summary>
	public bool IsTimeline => Type == QuestionType.Order && OrderMode == Shared.OrderMode.Timeline;
	/// <summary>Timeline: the label for item i (empty if none).</summary>
	public string ItemLabel(int i) => i >= 0 && i < ItemLabels.Count ? ItemLabels[i] : "";

	/// <summary>Indexes of the right answers, in either choice mode.</summary>
	public List<int> CorrectSet => IsMulti ? CorrectChoices.Distinct().OrderBy(i => i).ToList() : new List<int> { CorrectChoice };
	/// <summary>Played with buzzers unless the quiz maker chose "everyone types" (classic buzzer, sound, video, image reveal).</summary>
	public bool CanBeTyped => HasPlayClock || (IsClassicLike && !IsChoice);
	public bool IsTyped => Typed && CanBeTyped;
	/// <summary>Players buzz in (classic buzzer, sound, video, image reveal) instead of everyone answering.</summary>
	public bool IsBuzzer => CanBeTyped && !Typed || IsSteps;
	/// <summary>Step by step: the hints that are filled in, in order.</summary>
	public List<string> FilledHints => Hints.Where(h => !string.IsNullOrWhiteSpace(h)).ToList();
	/// <summary>Everyone types an answer that is checked against AnswerText (write the answer, or a typed question).</summary>
	public bool IsWritten => Type == QuestionType.Write || IsTyped;
	/// <summary>Has a bonus the host can give at the reveal.</summary>
	public bool HasBonus => BonusPoints > 0;
	/// <summary>Everyone answers within a time limit (instead of buzzing in).</summary>
	public bool HasTimeLimit => IsCloseness || IsChoice || IsWritten || (Type == QuestionType.Order && !IsTimeline) || IsInteractive;
	public bool HasImageField => Type is QuestionType.Classic or QuestionType.Estimation or QuestionType.MapPin or QuestionType.Image or QuestionType.Write;

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
	public const int RevealSecMin = 5;
	public const int RevealSecMax = 120;
	public const int RevealSecDefault = 20;
	public const int OrderItemsMin = 3;
	public const int OrderItemsMax = 6;
	public const int TimelineItemsMax = 10;
	public const int TimelineLabelMaxLength = 20;
	public const int WrittenMaxLength = 80;
	public const int BonusPointsMax = 10;
	public const int BonusTextMaxLength = 60;
	/// <summary>Picture questions: at most this many pictures in a slideshow.</summary>
	public const int SlidesMax = 12;
	/// <summary>Interactive questions: how long one recording can be, and the default time to do the task.</summary>
	public const int RecordingMaxSec = 15;
	/// <summary>Step by step: how many hints (the first is worth this many points).</summary>
	public const int HintsMin = 2;
	public const int HintsMax = 5;
	public const int HintMaxLength = 160;
	public const int InteractiveTimeDefault = 90;
}

public static class QuizRules
{
	/// <summary>
	/// 2: classic questions got an A–D mode, new sound and video types (old files are migrated).
	/// 3: classic "multi" mode with several correct answers (correctChoices); image reveal, order and write questions.
	/// 4: put in order can be played as a timeline (orderMode, itemLabels, timelineStart, keepGoing).
	/// 5: buzzer questions can be typed by everyone (typed); every question can have a bonus (bonusPoints, bonusText).
	/// 6: picture questions (type "picture": slides, answered like classic).
	/// 7: interactive questions (type "interactive": judging) and step by step (type "steps": hints).
	/// 8: estimation and map pin can score by place (rankPoints, e.g. F1's 25 / 18 / 15 …).
	/// </summary>
	public const int FormatVersion = 8;

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

	/// <summary>A copy of a put-in-order question with its items shuffled for showing (CorrectOrder says the right order).</summary>
	public static Question ShuffleOrder(Question q, Random rng)
	{
		var copy = q.Clone();
		var filled = Enumerable.Range(0, q.Items.Count).Where(i => !string.IsNullOrWhiteSpace(q.Items[i])).ToList();
		var items = filled.Select(i => q.Items[i]).ToList();
		var shown = Enumerable.Range(0, items.Count).OrderBy(_ => rng.Next()).ToList();
		// Never shown already in the right order (that would give it away).
		if (items.Count > 1 && shown.SequenceEqual(Enumerable.Range(0, items.Count))) shown.Reverse();
		copy.Items = shown.Select(i => items[i]).ToList();
		copy.ItemLabels = shown.Select(i => q.ItemLabel(filled[i])).ToList();
		copy.CorrectOrder = Enumerable.Range(0, items.Count).Select(i => shown.IndexOf(i)).ToList();
		// Timeline: a random item starts on it.
		copy.TimelineStart = items.Count > 0 ? rng.Next(items.Count) : 0;
		return copy;
	}

	/// <summary>
	/// Whether a written answer matches one of the accepted ones ("Canberra / Canberra City"): ignores case,
	/// accents, punctuation, a leading "the" and small typos (1 letter in 5+, 2 in 9+).
	/// </summary>
	public static bool WrittenMatches(string answer, string accepted)
	{
		var a = Normalize(answer);
		if (a == "") return false;
		foreach (var option in accepted.Split('/', ';', '\n'))
		{
			var o = Normalize(option);
			if (o == "") continue;
			var allowed = o.Length >= 9 ? 2 : o.Length >= 5 ? 1 : 0;
			if (Distance(a, o) <= allowed) return true;
		}
		return false;
	}

	static string Normalize(string s)
	{
		var decomposed = s.Trim().ToLowerInvariant().Normalize(System.Text.NormalizationForm.FormD);
		var kept = new System.Text.StringBuilder();
		foreach (var ch in decomposed)
		{
			if (char.IsLetterOrDigit(ch)) kept.Append(ch);
			else if (char.IsWhiteSpace(ch) || ch is '-' or '_') kept.Append(' ');
		}
		var words = kept.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();
		if (words.Count > 1 && words[0] is "the" or "der" or "die" or "das") words.RemoveAt(0);
		return string.Join(' ', words);
	}

	/// <summary>Edit distance (insert, delete, change one letter).</summary>
	static int Distance(string a, string b)
	{
		var prev = Enumerable.Range(0, b.Length + 1).ToArray();
		for (var i = 1; i <= a.Length; i++)
		{
			var cur = new int[b.Length + 1];
			cur[0] = i;
			for (var j = 1; j <= b.Length; j++)
				cur[j] = Math.Min(Math.Min(cur[j - 1] + 1, prev[j] + 1), prev[j - 1] + (a[i - 1] == b[j - 1] ? 0 : 1));
			prev = cur;
		}
		return prev[b.Length];
	}

	/// <summary>Largest media file the editor accepts (it is stored in the quiz file).</summary>
	public static int MediaMaxMb(string type) => type == QuestionType.Sound ? 25 : 80;

	public static Question NewQuestion(string type, string id)
	{
		var q = new Question { Id = id, Type = type, Points = QuestionLimits.PointsMin };
		switch (type)
		{
			case QuestionType.Classic:
			case QuestionType.Picture:
				q.TimeLimitSec = QuestionLimits.ChoiceTimeLimitSec;
				break;
			case QuestionType.Sound:
				q.Prompt = "Name this song!";
				break;
			case QuestionType.Video:
				q.Prompt = "Which game is this trailer from?";
				break;
			case QuestionType.Image:
				q.Prompt = "What is this?";
				break;
			case QuestionType.Order:
				q.OrderTop = "Oldest";
				q.OrderBottom = "Newest";
				break;
			case QuestionType.MapPin:
				q.Prompt = "Where is this?";
				q.TimeLimitSec = QuestionLimits.MapTimeLimitSec;
				// (map pins: the closest three score 3, 2 and 1 by default)
				q.Scoring = Scoring.Top3;
				break;
			case QuestionType.Interactive:
				q.TimeLimitSec = QuestionLimits.InteractiveTimeDefault;
				break;
			case QuestionType.Steps:
				q.Prompt = "What are we looking for?";
				break;
		}
		return q;
	}

	/// <summary>Short subtitle for lists, e.g. "map pin · 60 s".</summary>
	public static string Summary(Question q)
	{
		var kind = Lang.T(QuestionType.Infos[q.Type].Short);
		if (q.Type == QuestionType.Picture && q.Slides.Count > 1) kind += " · " + Lang.F("{0} pictures", q.Slides.Count);
		if (q.IsClassicLike)
			return q.IsMulti ? $"{kind} · {Lang.T("several correct")} · {q.TimeLimitSec} s"
				: q.IsChoice ? $"{kind} · A–D · {q.TimeLimitSec} s" : q.IsTyped ? $"{kind} · {Lang.T("everyone types")} · {q.TimeLimitSec} s" : $"{kind} · {Lang.T("buzzer")}";
		if (q.IsSteps) return $"{kind} · {Lang.F("{0} hints", q.FilledHints.Count)}";
		if (q.IsInteractive) return $"{kind} · {Lang.T(q.Judging == Judging.Host ? "host picks" : "vote")} · {q.TimeLimitSec} s";
		if (q.Type == QuestionType.Order)
			return $"{kind} · {(q.IsTimeline ? Lang.T("timeline") + " · " : "")}{Lang.F("{0} items", q.Items.Count(i => !string.IsNullOrWhiteSpace(i)))} · {q.TimeLimitSec} s";
		if (q.IsTyped) return $"{kind} · {Lang.T("everyone types")} · {q.TimeLimitSec} s";
		if (q.HasTimeLimit) return $"{kind} · {q.TimeLimitSec} s";
		return $"{kind} · {Lang.T("buzzer")}";
	}

	/// <summary>Problems that stop a question from being played. Empty = ready.</summary>
	public static List<string> QuestionIssues(Question q, IReadOnlyList<MapAsset> maps)
	{
		var issues = new List<string>();
		if (string.IsNullOrWhiteSpace(q.Prompt)) issues.Add(Lang.T(q.IsInteractive ? "Say what the task is." : "Question text is missing."));
		if (q.Type == QuestionType.Picture && q.Slides.Count == 0) issues.Add(Lang.T("Add at least one picture."));
		if (q.IsClassicLike)
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
		if (q.Type == QuestionType.Order && q.Items.Count(i => !string.IsNullOrWhiteSpace(i)) < QuestionLimits.OrderItemsMin)
			issues.Add(Lang.F("Fill in at least {0} items.", QuestionLimits.OrderItemsMin));
		if (q.Type == QuestionType.Write && string.IsNullOrWhiteSpace(q.AnswerText)) issues.Add(Lang.T("The correct answer is missing."));
		if (q.IsSteps)
		{
			if (string.IsNullOrWhiteSpace(q.AnswerText)) issues.Add(Lang.T("The correct answer is missing."));
			if (q.FilledHints.Count < QuestionLimits.HintsMin) issues.Add(Lang.F("Fill in at least {0} hints.", QuestionLimits.HintsMin));
		}
		if (q.Type == QuestionType.Image)
		{
			if (q.Image == null) issues.Add(Lang.T("Upload a picture."));
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
		if (q.HasBonus && string.IsNullOrWhiteSpace(q.BonusText)) issues.Add(Lang.T("Say what the bonus is for."));
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
