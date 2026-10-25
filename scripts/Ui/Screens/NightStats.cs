// Tonight's numbers per player (the "Stats" button by the scores, and a tab on the players' "Get ready" screen), and the
// one-line "how it works" for each kind of question.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Godot;
using QuizNight.Shared;

namespace QuizNight.Ui.Screens;

public static class NightStatsView
{
	static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

	/// <summary>Place, points, questions scored, average answer and buzz time, average distance on maps and % off on estimations.</summary>
	public static Control Table(RoomState room, string? me = null)
	{
		var grid = new LinedGrid { Columns = 8 };
		grid.AddThemeConstantOverride("h_separation", 16);
		grid.AddThemeConstantOverride("v_separation", 12);
		foreach (var h in new[] { "#", "Player", "Points", "Scored", "Ø answer time", "Ø buzz time", "Ø map off", "Ø % off" })
			grid.AddChild(U.Eyebrow(h));
		foreach (var p in room.Ranked())
		{
			var s = room.Stats.GetValueOrDefault(p.Id) ?? new NightStats();
			grid.AddChild(GameParts.PlaceBox(room.Place(p.Id)));
			grid.AddChild(GameParts.Row(6, new Avatar(p, 22), U.Text(p.Name, 15, p.Id == me ? C.Teal : C.Text, p.Id == me ? Fonts.Bold : null).Raw()));
			grid.AddChild(U.Mono($"{room.Score(p.Id)}", 15, C.Accent));
			grid.AddChild(U.Mono($"{s.Scored}", 14));
			grid.AddChild(U.Mono(Seconds(s.AvgAnswerS), 14, C.Muted));
			grid.AddChild(U.Mono(Seconds(s.AvgBuzzS), 14, C.Muted));
			grid.AddChild(U.Mono(s.AvgKm is { } km ? Distance(km) : "–", 14, C.Muted));
			grid.AddChild(U.Mono(s.AvgPercentOff is { } pct ? Percent(pct) : "–", 14, C.Muted));
		}
		return grid;
	}

	/// <summary>"1.23 s", or "–" for none.</summary>
	public static string Seconds(double? s) => s is { } v ? v.ToString("0.00", Inv) + " s" : "–";

	public static string Distance(double km) => km < 1 ? (km * 1000).ToString("0", Inv) + " m" : km.ToString(km < 10 ? "0.00" : "0.0", Inv) + " km";

	public static string Percent(double pct) => pct.ToString(pct < 10 ? "0.0" : "0", Inv) + " %";

	/// <summary>The table in its own window (from the "Stats" button by the scores).</summary>
	public static void Popup(Node owner, RoomState room, string? me)
	{
		var d = new AcceptDialog { Title = Lang.T("Tonight so far"), OkButtonText = Lang.T("Close") };
		var scroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled, CustomMinimumSize = new Vector2(700, 260) };
		scroll.AddChild(U.Pad(Table(room, me), 4, 4, 4, 4));
		d.AddChild(U.V(10, scroll, U.Muted("Ø answer time: until you locked in an answer or sent a recording. Ø buzz time: until you buzzed in. Ø map off: how far your pins were on maps with a scale. Ø % off: how far your estimations were, in % of the right number.", 12).MinSize(700, 0)));
		d.Confirmed += d.QueueFree;
		d.Canceled += d.QueueFree;
		owner.AddChild(d);
		d.PopupCentered(new Vector2I(760, 420));
	}

	/// <summary>One sentence on how the coming question works (known before it's shown: its kind, not its content).</summary>
	public static string HowItWorks(PublicQuestion q)
	{
		var costs = q.WrongCosts ? " " + Lang.T("Careful: a wrong answer costs points.") : "";
		return q.Type switch
		{
			QuestionType.Steps => Lang.T("Hints come one by one, the hardest first. Buzz in as soon as you know it: the earlier you're right, the more points."),
			QuestionType.Interactive => Lang.T(q.Judging == Judging.Host
				? "Do the task with your microphone and send your recording. The host plays them all and picks a favourite."
				: "Do the task with your microphone and send your recording. Everyone hears them all, then votes for the best (not your own)."),
			QuestionType.Estimation => Lang.T(q.RankScoring ? "Guess the number. The closer you are, the better your place and the more points." : "Guess the number. The closest guesses score."),
			QuestionType.MapPin => Lang.T("Look at the screenshot and place your pin on the map where you think it was taken. The closest pins score."),
			QuestionType.Order => Lang.T(q.OrderMode == OrderMode.Timeline
				? "Take turns placing items on the timeline, fewest points first. A right spot scores; a wrong one passes the item on."
				: q.WrongCosts ? "Put the items in the right order on your screen, then lock in. Every item in its right place scores, every wrong one costs points."
				: "Put the items in the right order on your screen, then lock in. Every item in its right place scores."),
			QuestionType.Write => Lang.T("Type the answer before the time runs out. Small typos are fine."),
			QuestionType.Sound when q.Buzzer => Lang.T("A song or sound plays. Buzz in as soon as you know it; it pauses while someone answers."),
			QuestionType.Video when q.Buzzer => Lang.T("A clip plays. Buzz in as soon as you know it; it pauses while someone answers."),
			QuestionType.Image when q.Buzzer => Lang.T("A picture slowly gets clearer. Buzz in as soon as you know what it is."),
			QuestionType.Sound or QuestionType.Video or QuestionType.Image => Lang.T("Watch or listen, and type the answer before the time runs out."),
			_ when q.Mode == ClassicMode.Multi => Lang.T("Several answers can be right: pick all you think are, then lock in. Only exactly the right ones score.") + costs,
			_ when q.Mode == ClassicMode.Choice => Lang.T("Pick one of the answers before the time runs out.") + costs,
			_ when q.Written => Lang.T("Type the answer before the time runs out. Small typos are fine."),
			QuestionType.Picture => Lang.T("Look at the picture (or the pictures the host flips through). Buzz in when you know it and say the answer out loud."),
			_ => Lang.T("Buzz in when you know it and say the answer out loud. The host decides if it's right."),
		};
	}
}
