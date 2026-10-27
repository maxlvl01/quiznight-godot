// Reads and writes .quiznight.json files. Reading is forgiving: missing fields get their
// defaults (that is also how files from older versions are migrated), wrong types are ignored.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace QuizNight.Shared;

public static class QuizJson
{
	public const string NotAQuiz = "That file is not a quiz file.";

	/// <summary>Parses a quiz file. Returns null (and an error) if it isn't one.</summary>
	public static Quiz? Parse(string text, out string? error)
	{
		error = null;
		JsonNode? root;
		try
		{
			root = JsonNode.Parse(text);
		}
		catch (JsonException)
		{
			error = NotAQuiz;
			return null;
		}
		if (root is not JsonObject o || o["questions"] is not JsonArray qs || o["maps"] is not JsonArray maps)
		{
			error = NotAQuiz;
			return null;
		}
		var quiz = new Quiz
		{
			Version = QuizRules.FormatVersion,
			Id = Str(o, "id"),
			Title = Str(o, "title"),
			CreatedAt = (long)Num(o, "createdAt", 0),
			UpdatedAt = (long)Num(o, "updatedAt", 0),
			Maps = maps.OfType<JsonObject>().Select(ReadMap).ToList(),
			Questions = qs.OfType<JsonObject>().Select(ReadQuestion).ToList(),
		};
		SkyrimCrop.Migrate(quiz);
		return quiz;
	}

	public static string Write(Quiz quiz, bool indented = false)
	{
		var o = new JsonObject
		{
			["version"] = QuizRules.FormatVersion,
			["id"] = quiz.Id,
			["title"] = quiz.Title,
			["createdAt"] = quiz.CreatedAt,
			["updatedAt"] = quiz.UpdatedAt,
			["maps"] = new JsonArray(quiz.Maps.Select(m => (JsonNode)WriteMap(m)).ToArray()),
			["questions"] = new JsonArray(quiz.Questions.Select(q => (JsonNode)WriteQuestion(q)).ToArray()),
		};
		return o.ToJsonString(new JsonSerializerOptions { WriteIndented = indented });
	}

	public static JsonObject WriteQuestion(Question q)
	{
		var o = new JsonObject
		{
			["id"] = q.Id,
			["type"] = q.Type,
			["prompt"] = q.Prompt,
			["points"] = q.Points,
		};
		if (q.CreatedAt > 0) o["createdAt"] = q.CreatedAt;
		if (q.HasBonus)
		{
			o["bonusPoints"] = q.BonusPoints;
			o["bonusText"] = q.BonusText;
		}
		if (q.Category != "")
		{
			o["category"] = q.Category;
			if (q.CategorySecret) o["categorySecret"] = true;
		}
		if (q.CanBeTyped) o["typed"] = q.Typed;
		// (typed questions have a time limit; classic writes it anyway)
		if (q.IsTyped && !q.IsClassicLike) o["timeLimitSec"] = q.TimeLimitSec;
		switch (q.Type)
		{
			case QuestionType.Classic:
			case QuestionType.Picture:
				if (q.Type == QuestionType.Picture) o["slides"] = new JsonArray(q.Slides.Select(i => WriteImage(i)).ToArray());
				else o["image"] = WriteImage(q.Image);
				o["mode"] = q.Mode;
				o["answer"] = q.AnswerText;
				o["choices"] = new JsonArray(q.Choices.Select(c => (JsonNode)JsonValue.Create(c)!).ToArray());
				o["correctChoice"] = q.CorrectChoice;
				o["correctChoices"] = new JsonArray(q.CorrectChoices.Select(i => (JsonNode)JsonValue.Create(i)!).ToArray());
				if (q.WrongCosts) o["wrongCosts"] = true;
				o["timeLimitSec"] = q.TimeLimitSec;
				break;
			case QuestionType.Sound:
			case QuestionType.Video:
				o["media"] = q.Media == null
					? null
					: new JsonObject
					{
						["dataUrl"] = q.Media.DataUrl,
						["mime"] = q.Media.Mime,
						["name"] = q.Media.Name,
						["durationSec"] = q.Media.DurationSec,
					};
				o["startSec"] = q.StartSec;
				o["answer"] = q.AnswerText;
				break;
			case QuestionType.Order:
				o["items"] = new JsonArray(q.Items.Select(i => (JsonNode)JsonValue.Create(i)!).ToArray());
				o["orderTop"] = q.OrderTop;
				o["orderBottom"] = q.OrderBottom;
				o["timeLimitSec"] = q.TimeLimitSec;
				o["orderMode"] = q.OrderMode;
				if (q.WrongCosts && !q.IsTimeline) o["wrongCosts"] = true;
				o["itemLabels"] = new JsonArray(q.Items.Select((_, i) => (JsonNode)JsonValue.Create(q.ItemLabel(i))!).ToArray());
				if (q.IsTimeline) o["keepGoing"] = q.KeepGoing;
				break;
			case QuestionType.Write:
				o["image"] = WriteImage(q.Image);
				o["answer"] = q.AnswerText;
				o["timeLimitSec"] = q.TimeLimitSec;
				break;
			case QuestionType.Image:
				o["image"] = WriteImage(q.Image);
				o["answer"] = q.AnswerText;
				o["revealStyle"] = q.RevealStyle;
				o["revealSec"] = q.RevealSec;
				o["focus"] = new JsonObject { ["x"] = q.Focus.X, ["y"] = q.Focus.Y };
				break;
			case QuestionType.Estimation:
				o["image"] = WriteImage(q.Image);
				o["answer"] = q.AnswerNumber;
				o["unit"] = q.Unit;
				WriteCloseness(o, q);
				break;
			case QuestionType.Steps:
				o["answer"] = q.AnswerText;
				o["hints"] = new JsonArray(q.Hints.Select(h => (JsonNode)JsonValue.Create(h)!).ToArray());
				if (q.IsSentence) o["stepsMode"] = q.StepsMode;
				break;
			case QuestionType.Interactive:
				o["timeLimitSec"] = q.TimeLimitSec;
				o["judging"] = q.Judging;
				break;
			case QuestionType.MapPin:
				o["image"] = WriteImage(q.Image);
				o["mapId"] = q.MapId;
				o["target"] = q.Target is { } t ? new JsonObject { ["x"] = t.X, ["y"] = t.Y } : null;
				if (q.MapCheckedIn != null) o["mapCheckedIn"] = q.MapCheckedIn;
				o["buzzFirst"] = q.BuzzFirst;
				WriteCloseness(o, q);
				break;
		}
		return o;
	}

	public static Question ReadQuestion(JsonObject o)
	{
		var type = Str(o, "type");
		var q = QuestionType.IsKnown(type) ? QuizRules.NewQuestion(type, Str(o, "id")) : new Question { Id = Str(o, "id"), Type = type };
		q.Prompt = Str(o, "prompt", q.Prompt);
		// (read as a whole number first: a question copied in memory still holds it as one)
		q.CreatedAt = o["createdAt"] is JsonValue made && made.TryGetValue<long>(out var madeMs) ? Math.Max(0, madeMs) : (long)Math.Max(0, Num(o, "createdAt", 0));
		q.Points = (int)Num(o, "points", q.Points);
		q.Image = ReadImage(o["image"]);
		if (o["slides"] is JsonArray slides)
			q.Slides = slides.Select(ReadImage).OfType<ImageAsset>().Take(QuestionLimits.SlidesMax).ToList();
		var mode = Str(o, "mode", ClassicMode.Buzzer);
		q.Mode = mode is ClassicMode.Choice or ClassicMode.Multi ? mode : ClassicMode.Buzzer;
		if (o["answer"] is JsonValue a)
		{
			if (a.TryGetValue<string>(out var s)) q.AnswerText = s;
			else if (a.TryGetValue<double>(out var n)) q.AnswerNumber = n;
		}
		if (o["choices"] is JsonArray choices)
		{
			var list = choices.Select(c => c is JsonValue v && v.TryGetValue<string>(out var s) ? s : "").Take(4).ToList();
			while (list.Count < 4) list.Add("");
			q.Choices = list;
		}
		q.CorrectChoice = (int)Num(o, "correctChoice", 0);
		if (o["correctChoices"] is JsonArray correct)
			q.CorrectChoices = correct.Select(c => c is JsonValue v && v.TryGetValue<double>(out var n) ? (int)n : -1).Where(i => i is >= 0 and < 4).Distinct().ToList();
		q.TimeLimitSec = (int)Num(o, "timeLimitSec", q.TimeLimitSec);
		q.OrderMode = Str(o, "orderMode", OrderMode.All) == OrderMode.Timeline ? OrderMode.Timeline : OrderMode.All;
		if (o["items"] is JsonArray items)
			q.Items = items.Select(i => i is JsonValue v && v.TryGetValue<string>(out var s) ? s : "")
				.Take(q.IsTimeline ? QuestionLimits.TimelineItemsMax : QuestionLimits.OrderItemsMax).ToList();
		if (o["itemLabels"] is JsonArray labels)
			q.ItemLabels = labels.Select(i => i is JsonValue v && v.TryGetValue<string>(out var s) ? s : "").Take(q.Items.Count).ToList();
		q.KeepGoing = o["keepGoing"] is not JsonValue kg || !kg.TryGetValue<bool>(out var keep) || keep;
		q.OrderTop = Str(o, "orderTop", q.OrderTop);
		q.OrderBottom = Str(o, "orderBottom", q.OrderBottom);
		q.RevealStyle = Str(o, "revealStyle", RevealStyle.Zoom) == RevealStyle.Pixelate ? RevealStyle.Pixelate : RevealStyle.Zoom;
		q.RevealSec = Math.Clamp((int)Num(o, "revealSec", q.RevealSec), QuestionLimits.RevealSecMin, QuestionLimits.RevealSecMax);
		if (o["focus"] is JsonObject focus) q.Focus = new NormPoint(Math.Clamp(Num(focus, "x", 0.5), 0, 1), Math.Clamp(Num(focus, "y", 0.5), 0, 1));
		if (o["media"] is JsonObject m)
		{
			q.Media = new MediaAsset
			{
				DataUrl = Str(m, "dataUrl"),
				Mime = Str(m, "mime"),
				Name = Str(m, "name"),
				DurationSec = m["durationSec"] is JsonValue d && d.TryGetValue<double>(out var dur) ? dur : null,
			};
		}
		q.StartSec = Num(o, "startSec", 0);
		q.Unit = Str(o, "unit");
		q.TieRule = Str(o, "tieRule", TieRule.AllScore) is var tie && TieRule.All.Any(t => t.Id == tie) ? tie : TieRule.AllScore;
		q.WrongCosts = o["wrongCosts"] is JsonValue wc && wc.TryGetValue<bool>(out var costs) && costs;
		q.Scoring = Str(o, "scoring", Scoring.ClosestWins) == Scoring.Top3 ? Scoring.Top3 : Scoring.ClosestWins;
		if (o["rankPoints"] is JsonArray rankPoints)
			q.RankPoints = rankPoints.Select(p => p is JsonValue v && (v.TryGetValue<double>(out var n) || v.TryGetValue<int>(out var i) && (n = i) == i) ? (int)Math.Clamp(n, 0, ScoringPresets.PointsMax) : 0)
				.Take(ScoringPresets.PlacesMax).ToList();
		q.MapId = o["mapId"] is JsonValue mid && mid.TryGetValue<string>(out var mapId) ? mapId : null;
		q.Target = ReadPoint(o["target"]);
		q.MapCheckedIn = o["mapCheckedIn"] is JsonValue mc && mc.TryGetValue<string>(out var checkedIn) && AppVersion.Parse(checkedIn) != null ? checkedIn : null;
		if (o["hints"] is JsonArray hints)
		{
			q.Hints = hints.Select(h => h is JsonValue v && v.TryGetValue<string>(out var s) ? s : "").Take(QuestionLimits.HintsMax).ToList();
			while (q.Hints.Count < QuestionLimits.HintsMax) q.Hints.Add("");
		}
		q.Judging = Str(o, "judging", Judging.Vote) == Judging.Host ? Judging.Host : Judging.Vote;
		q.StepsMode = Str(o, "stepsMode", StepsMode.Hints) == StepsMode.Sentence ? StepsMode.Sentence : StepsMode.Hints;
		q.BuzzFirst = o["buzzFirst"] is JsonValue bf && bf.TryGetValue<bool>(out var b) && b;
		q.Typed = o["typed"] is JsonValue ty && ty.TryGetValue<bool>(out var typed) && typed;
		q.BonusPoints = Math.Clamp((int)Num(o, "bonusPoints", 0), 0, QuestionLimits.BonusPointsMax);
		q.BonusText = Str(o, "bonusText");
		if (q.BonusText.Length > QuestionLimits.BonusTextMaxLength) q.BonusText = q.BonusText[..QuestionLimits.BonusTextMaxLength];
		q.Category = Categories.Clean(Str(o, "category"));
		// "Write the answer" was its own type until 0.10.3b; it's the classic question's "Everyone types" now
		if (q.Type == QuestionType.Write)
		{
			q.Type = QuestionType.Classic;
			q.Mode = ClassicMode.Buzzer;
			q.Typed = true;
		}
		q.CategorySecret = q.Category != "" && o["categorySecret"] is JsonValue cs && cs.TryGetValue<bool>(out var secret) && secret;
		return q;
	}

	public static JsonObject WriteMap(MapAsset m)
	{
		var o = new JsonObject
		{
			["id"] = m.Id,
			["name"] = m.Name,
			["image"] = WriteImage(m.Image),
			["widthKm"] = m.WidthKm,
		};
		if (m.PresetId != null) o["presetId"] = m.PresetId;
		return o;
	}

	public static MapAsset ReadMap(JsonObject o)
	{
		var presetId = o["presetId"] is JsonValue p && p.TryGetValue<string>(out var pid) ? pid : null;
		return new()
		{
			Id = Str(o, "id"),
			// (a built-in map renamed since: its new name)
			Name = MapPresets.CurrentName(presetId, Str(o, "name")),
			Image = ReadImage(o["image"]) ?? new ImageAsset(),
			WidthKm = o["widthKm"] is JsonValue w && w.TryGetValue<double>(out var km) ? km : null,
			PresetId = presetId,
		};
	}

	public static NormPoint? ReadPoint(JsonNode? node) =>
		node is JsonObject t && t["x"] is JsonValue x && t["y"] is JsonValue y && x.TryGetValue<double>(out var px) && y.TryGetValue<double>(out var py)
			? new NormPoint(px, py)
			: null;

	static void WriteCloseness(JsonObject o, Question q)
	{
		o["timeLimitSec"] = q.TimeLimitSec;
		o["tieRule"] = q.TieRule;
		o["scoring"] = q.Scoring;
		if (q.RankPoints.Count > 0) o["rankPoints"] = new JsonArray(q.RankPoints.Select(p => (JsonNode)JsonValue.Create(p)!).ToArray());
	}

	static JsonNode? WriteImage(ImageAsset? img) =>
		img == null ? null : new JsonObject { ["dataUrl"] = img.DataUrl, ["width"] = img.Width, ["height"] = img.Height };

	static ImageAsset? ReadImage(JsonNode? node) =>
		node is JsonObject o ? new ImageAsset { DataUrl = Str(o, "dataUrl"), Width = (int)Num(o, "width", 0), Height = (int)Num(o, "height", 0) } : null;

	static string Str(JsonObject o, string key, string fallback = "") =>
		o[key] is JsonValue v && v.TryGetValue<string>(out var s) ? s : fallback;

	static double Num(JsonObject o, string key, double fallback) =>
		o[key] is JsonValue v && v.TryGetValue<double>(out var n) && double.IsFinite(n) ? n : fallback;
}
