// Pieces shared by the host (TV) and player game screens.

using System;
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

	/// <summary>
	/// The side column on the host's and the players' screens: the buzz order, the timeline's turn order or who has
	/// answered, then the scores. <paramref name="me"/> (a player) is highlighted.
	/// </summary>
	public static void SideInfo(VBoxContainer side, RoomState room, string? me = null)
	{
		var game = room.Game!;
		var q = game.Question;
		var tl = game.Stage == Stage.Open ? game.Timeline : null;
		if (q.Buzzer && game.Stage == Stage.Open)
		{
			side.AddChild(U.Eyebrow("Buzz order"));
			if (game.Shown && !game.BuzzersOpen) side.AddChild(U.Pill("Buzzers locked", C.Coral));
			side.AddChild(BuzzQueue(room));
		}
		else if (tl != null)
		{
			side.AddChild(U.Eyebrow("Turn order — fewest points first"));
			for (var i = 0; i < tl.TurnOrder.Count; i++)
			{
				var p = room.Player(tl.TurnOrder[i]);
				var now = p.Id == tl.TurnPlayerId;
				var missed = tl.Missed.Contains(p.Id);
				var status = now ? Lang.T("Placing now") : missed ? Lang.T("Missed this one") : !p.Connected ? Lang.T("(offline)") : "";
				var row = Row(8, RankBadge(i + 1), new Avatar(p, 26), U.V(0, U.Text(p.Name, 15).Raw(), U.Muted(status, 12, false)).Grow());
				var panel = U.Panel(row, "Clear");
				panel.AddThemeStyleboxOverride("panel", UiTheme.Box(now ? new Color(C.Accent, 0.1f) : C.Panel2, 10, now ? C.Accent : p.Id == me ? C.Teal : C.Border, 1, 10, 6));
				if (missed || !p.Connected) panel.Modulate = new Color(1, 1, 1, 0.55f);
				side.AddChild(panel);
			}
		}
		else if (game.Stage == Stage.Open && game.Shown)
		{
			var connected = room.Players.Count(p => p.Connected);
			side.AddChild(U.Eyebrow(Lang.F("Answers: {0} / {1}", game.AnsweredIds.Count, connected)));
			var chips = U.Flow(6);
			foreach (var p in room.Players) chips.AddChild(Chips.Player(p, game.AnsweredIds.Contains(p.Id) ? "✓" : null, p.Id == me, !game.AnsweredIds.Contains(p.Id)));
			side.AddChild(chips);
		}
		if (side.GetChildCount() > 0) side.AddChild(U.Gap(4));
		side.AddChild(U.Eyebrow("Scores"));
		side.AddChild(Scoreboard(room, me));
	}

	/// <summary>Who buzzed, in order, with their status and time.</summary>
	public static Control BuzzQueue(RoomState room)
	{
		var game = room.Game!;
		if (game.Buzzes.Count == 0) return U.Muted("Nobody has buzzed yet.", 14);
		var list = U.V(6);
		for (var i = 0; i < game.Buzzes.Count; i++)
		{
			var b = game.Buzzes[i];
			var p = room.Player(b.PlayerId);
			var status = b.Status switch
			{
				BuzzStatus.Answering => "Answering now",
				BuzzStatus.Waiting => "Next in line",
				BuzzStatus.Wrong => "Wrong",
				_ => "Correct",
			};
			var row = Row(8, RankBadge(i + 1), new Avatar(p, 26),
				U.V(0, U.Text(p.Name, 15).Raw(), U.Muted(status, 12, false)).Grow(),
				U.Mono(game.Question.HasPlayClock ? U.FormatSeconds(b.AtSec) : $"{b.AtSec:0.00} s", 13, C.Muted));
			var border = b.Status switch { BuzzStatus.Answering => C.Accent, BuzzStatus.Correct => C.Teal, _ => C.Border };
			var bg = b.Status == BuzzStatus.Answering ? new Color(C.Accent, 0.1f) : C.Panel2;
			var panel = U.Panel(row, "Clear");
			panel.AddThemeStyleboxOverride("panel", UiTheme.Box(bg, 10, border, 1, 10, 6));
			if (b.Status == BuzzStatus.Wrong) panel.Modulate = new Color(1, 1, 1, 0.55f);
			list.AddChild(panel);
		}
		return list;
	}

	/// <summary>The answer and everyone's result, shown on the host screen after the reveal.</summary>
	/// <param name="toggle">Host, write-the-answer: tick or untick a player's answer.</param>
	public static Control RevealPanel(RoomState room, Action<string>? toggle = null)
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
			if (toggle != null)
			{
				var id = e.PlayerId;
				row.AddChild(U.Btn(e.Correct ? "✓ Counts" : "Doesn't count", e.Correct ? "TealButton" : "SmallButton", () => toggle(id), 14).Tip("Click to change"));
			}
			list.AddChild(U.Panel(row, e.Points > 0 ? "Scored" : "Card2").With(c => c.AddThemeStyleboxOverride("panel",
				UiTheme.Box(e.Points > 0 ? new Color(C.Teal, 0.1f) : C.Panel2, 10, e.Points > 0 ? C.Teal : C.Border, 1, 10, 6))));
		}
		box.AddChild(list);
		return box;
	}

	/// <summary>Put in order: the shuffled items between what the top and the bottom mean.</summary>
	public static Control OrderList(PublicQuestion q, float size = 22)
	{
		var box = U.V(8);
		if (!string.IsNullOrWhiteSpace(q.OrderTop)) box.AddChild(U.Muted("▲ " + q.OrderTop, 15).Raw());
		foreach (var item in q.Items ?? new List<string>())
			box.AddChild(U.Panel(U.Text(item, (int)size, null, Fonts.Bold, true).Raw(), "Card2"));
		if (!string.IsNullOrWhiteSpace(q.OrderBottom)) box.AddChild(U.Muted("▼ " + q.OrderBottom, 15).Raw());
		return box;
	}

	// ---------- timeline ----------

	/// <summary>One item on the timeline: its text and (once placed) its label, e.g. the year.</summary>
	public static Control TimelineCard(string text, string label, bool start = false, bool big = false)
	{
		var box = U.V(2, U.Text(text, big ? 24 : 17, C.Text, Fonts.Bold, true, HorizontalAlignment.Center).Raw());
		if (label != "") box.AddChild(U.Mono(label, big ? 20 : 16, C.Accent).Raw().With(l => l.HorizontalAlignment = HorizontalAlignment.Center));
		var card = U.Panel(box, "Clear").MinSize(big ? 220 : 120, big ? 0 : 64);
		card.AddThemeStyleboxOverride("panel", UiTheme.Box(C.Panel2, 12, start ? C.Lime : C.Border, start ? 2 : 1, 12, 10));
		card.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
		return card;
	}

	/// <summary>
	/// The timeline left to right, with a gap before, between and after the cards. With <paramref name="pick"/>
	/// the gaps are buttons (gaps already tried are crossed out); without it only tried gaps show (✗). The ends
	/// ("◀ Oldest", "Newest ▶") are the first and last items of the row.
	/// </summary>
	public static Control TimelineRow(PublicQuestion q, TimelineView tl, int? picked = null, Action<int>? pick = null)
	{
		var flow = U.Flow(8);
		flow.Alignment = FlowContainer.AlignmentMode.Center;
		// The ends sit right next to the first and last gap, so the arrows line up with the timeline.
		Control End(string text) => U.Muted(text, 14, false).Raw().With(l => l.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter);
		flow.AddChild(End("◀ " + (q.OrderTop ?? "")));
		for (var g = 0; g <= tl.Placed.Count; g++)
		{
			var gap = g;
			var wrong = tl.WrongGaps.Contains(g);
			if (pick != null)
			{
				var b = U.Btn(wrong ? "✗" : gap == picked ? "▼" : "+", gap == picked ? "TealButton" : "SmallButton", () => pick(gap), 20).MinSize(52, 64);
				b.Disabled = wrong;
				b.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
				b.TooltipText = Lang.T(gap == 0 ? "Before the first one" : gap == tl.Placed.Count ? "After the last one" : "Between these two");
				flow.AddChild(b);
			}
			else if (wrong) flow.AddChild(U.Text("✗", 24, C.Coral, Fonts.Black).With(l => l.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter));
			else if (gap == picked) flow.AddChild(U.Text("▼", 26, C.Teal, Fonts.Black).With(l => l.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter));
			if (g < tl.Placed.Count)
			{
				var c = tl.Placed[g];
				flow.AddChild(TimelineCard(c.Text, c.Label, c.Start));
			}
		}
		flow.AddChild(End((q.OrderBottom ?? "") + " ▶"));
		return flow;
	}

	/// <summary>"Ann put Elden Ring in the wrong place." and the like.</summary>
	public static string TimelineEventText(RoomState room, TimelineEvent e)
	{
		var name = e.PlayerId != null ? room.Player(e.PlayerId).Name : "";
		return e.Kind switch
		{
			TimelineEventKind.Right => Lang.F("{0} placed {1} right!", name, e.Item),
			TimelineEventKind.Wrong => Lang.F("{0} put {1} in the wrong place.", name, e.Item),
			TimelineEventKind.Locked => Lang.F("{0} locked in where {1} goes.", name, e.Item),
			TimelineEventKind.TimeUp => Lang.F("{0} ran out of time.", name),
			TimelineEventKind.Skipped => Lang.F("{0} was skipped.", name),
			_ => Lang.F("Nobody got {0}, so it's placed for free.", e.Item),
		};
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
