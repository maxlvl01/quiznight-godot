// Pieces shared by the host (TV) and player game screens.

using System.Collections.Generic;
using System.Linq;
using Godot;
using QuizNight.Shared;

namespace QuizNight.Ui.Screens;

public static class GameParts
{
	public static HBoxContainer Row(int sep, params Control[] kids)
	{
		var row = U.H(sep, kids);
		foreach (var k in kids) k.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
		return row;
	}

	public static Control RankBadge(int rank) =>
		U.Mono($"{rank}", 14, C.Muted).MinSize(22).With(l => l.HorizontalAlignment = HorizontalAlignment.Center);

	/// <summary>Players by score; at the reveal it shows what each one just won.</summary>
	public static Control Scoreboard(RoomState room, string? highlight = null)
	{
		var gained = (room.Game?.Reveal?.Entries ?? new List<RevealEntry>()).ToDictionary(e => e.PlayerId, e => e.Points);
		var list = U.V(6);
		var rows = room.Players.OrderByDescending(p => room.Score(p.Id)).ToList();
		for (var i = 0; i < rows.Count; i++)
		{
			var p = rows[i];
			var row = Row(8, RankBadge(i + 1), new Avatar(p, 26), U.Text(p.Name, 15).Raw().Grow());
			if (gained.TryGetValue(p.Id, out var g) && g != 0) row.AddChild(U.Text($"+{g}", 14, C.Teal, Fonts.Bold));
			row.AddChild(U.Mono($"{room.Score(p.Id)}", 16));
			list.AddChild(U.Panel(row, p.Id == highlight ? "ChipMe" : "Card2").With(c => c.AddThemeStyleboxOverride("panel",
				UiTheme.Box(C.Panel2, 10, p.Id == highlight ? C.Teal : C.Border, 1, 10, 6))));
		}
		return list;
	}

	/// <summary>The answer and everyone's result, shown on the host screen after the reveal.</summary>
	public static Control RevealPanel(RoomState room)
	{
		var game = room.Game!;
		var reveal = game.Reveal!;
		var q = game.Question;
		var box = U.V(10, new HSeparator(), U.Eyebrow("The answer"), U.Text(reveal.AnswerText, 34, C.Accent, Fonts.Black, true).Raw());
		if (q.Buzzer)
		{
			if (reveal.WinnerIds.FirstOrDefault() is { } winnerId)
			{
				var w = room.Player(winnerId);
				var gotIt = q.HasPlayClock && reveal.GuessTimeSec is { } t
					? Lang.F(q.Type == QuestionType.Sound ? "got it after {0} of listening" : "got it after {0} of watching", U.FormatSeconds(t))
					: Lang.T("got it");
				var row = Row(6, new Avatar(w, 26), U.Bold(w.Name, 17).Raw(), U.Text(gotIt, 17));
				row.AddChild(U.Text($"· +{q.Points}", 17, C.Teal, Fonts.Bold));
				box.AddChild(row);
			}
			else box.AddChild(U.Muted("Nobody got it this time.", 16));
			return box;
		}
		if (reveal.Entries.Count == 0)
		{
			box.AddChild(U.Muted("Nobody answered.", 16));
			return box;
		}
		var list = U.V(6);
		foreach (var e in reveal.Entries)
		{
			var p = room.Player(e.PlayerId);
			var row = Row(8);
			if (e.Rank is { } rank) row.AddChild(RankBadge(rank));
			row.AddChild(new Avatar(p, 26));
			row.AddChild(U.Text(p.Name, 15).Raw().MinSize(140));
			row.AddChild(U.Text(e.Text, 15, C.Muted).Raw().Grow());
			row.AddChild(U.Text(e.Points > 0 ? $"+{e.Points}" : "", 15, C.Teal, Fonts.Bold).MinSize(36));
			list.AddChild(U.Panel(row, e.Points > 0 ? "Scored" : "Card2").With(c => c.AddThemeStyleboxOverride("panel",
				UiTheme.Box(e.Points > 0 ? new Color(C.Teal, 0.1f) : C.Panel2, 10, e.Points > 0 ? C.Teal : C.Border, 1, 10, 6))));
		}
		box.AddChild(list);
		return box;
	}

	/// <summary>A–D tiles on the host screen (with counts at the reveal) — 2 columns.</summary>
	public static Control ChoiceGrid(GameView game)
	{
		var q = game.Question;
		var reveal = game.Stage == Stage.Reveal ? game.Reveal : null;
		var grid = new GridContainer { Columns = 2 };
		grid.AddThemeConstantOverride("h_separation", 12);
		grid.AddThemeConstantOverride("v_separation", 12);
		for (var i = 0; i < (q.Choices?.Count ?? 0); i++)
		{
			if (q.Choices![i] is not { } text) continue;
			var color = C.Choice[i];
			var correct = reveal?.CorrectIndices?.Contains(i) == true;
			var bg = correct ? C.Panel2.Lerp(color, 0.22f) : C.Panel2;
			var tile = U.Panel(GameParts.Row(14, Letter(i, 44), U.Text(text, 22, null, Fonts.Bold, true).Raw(),
				reveal?.Counts is { } counts ? U.Mono($"{counts[i]}", 20, C.Muted) : U.Gap(0)), "Clear").Grow();
			tile.AddThemeStyleboxOverride("panel", UiTheme.Box(bg, 14, correct ? color : C.Border, correct ? 3 : 1, 16, 14));
			if (reveal != null && !correct) tile.Modulate = new Color(1, 1, 1, 0.35f);
			grid.AddChild(tile);
		}
		return grid;
	}

	public static Control Letter(int i, int size) =>
		U.Panel(U.Text(QuizRules.ChoiceLetters[i], size / 2, new Color("#14151b"), Fonts.Black, false, HorizontalAlignment.Center), "Clear")
			.With(p => p.AddThemeStyleboxOverride("panel", UiTheme.Box(C.Choice[i], 10, null, 0, 0, 2))).MinSize(size, size);

	public static List<MapPin> RevealPins(RoomState room) =>
		room.Game!.Reveal!.Entries
			.Where(e => e.Value?.Point != null)
			.Select(e =>
			{
				var p = room.Player(e.PlayerId);
				return new MapPin(e.Value!.Point!.Value, new Color(p.Color), p.Name);
			}).ToList();
}
