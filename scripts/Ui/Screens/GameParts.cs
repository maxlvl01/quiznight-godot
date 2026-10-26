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

	/// <param name="medal">A place in the standings: 1st to 3rd in gold, silver and bronze (not for turn or buzz order).</param>
	public static Control RankBadge(int rank, bool medal = false) =>
		U.Mono(Lang.Ordinal(rank), 14, medal ? C.Medal(rank) ?? C.Muted : C.Muted).Raw().MinSize(34).With(l => l.HorizontalAlignment = HorizontalAlignment.Center);

	/// <summary>
	/// A place in the stats tables: a small box in the medal's colour for the top 3, a plain one after that, "–" for
	/// none (centred in its row, so it lines up with the profile pictures).
	/// </summary>
	/// <summary>"Category: Geography" (the quiz maker's own categories as they wrote them).</summary>
	public static Control CategoryPill(string category) =>
		U.Pill(Lang.F("Category: {0}", Categories.Label(category)), C.Sky).Raw();

	public static Control PlaceBox(int? place)
	{
		var medal = place is { } n ? C.Medal(n) : null;
		var label = U.Mono(place is { } m ? Lang.Ordinal(m) : "–", 13, medal != null ? C.Bg : C.Muted).Raw();
		label.HorizontalAlignment = HorizontalAlignment.Center;
		var box = U.Panel(label, "Clear").MinSize(44);
		box.AddThemeStyleboxOverride("panel", UiTheme.Box(medal ?? C.Panel2, 6, null, 0, 6, 2));
		box.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
		box.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
		return box;
	}

	/// <summary>Where each player's row was on each scoreboard (by viewer), so a rebuilt board can slide the ones that moved.</summary>
	static readonly Dictionary<string, Dictionary<string, (int Slot, SlideRow Row)>> _lastSlots = new();

	/// <summary>
	/// Under a name in the lobby: "Joined", "Offline"/"Reconnecting…", or while the quiz's files come in a ring with
	/// "Downloading 45 %" (ready = everything is here).
	/// </summary>
	public static Control SeatStatus(PublicPlayer p, RoomState room, bool playerView)
	{
		if (!p.Connected) return U.Muted(playerView ? "Reconnecting…" : "Offline", 12, false);
		var percent = room.Downloads.GetValueOrDefault(p.Id, 100);
		if (percent >= 100) return U.Muted(room.Downloads.Count > 0 ? "Ready" : "Joined", 12, false);
		var row = U.H(5, new DownloadRing(percent, 14), U.Muted(Lang.F("Downloading {0} %", percent), 12, false));
		row.TooltipText = Lang.T("Getting the quiz's pictures, sounds and videos, so nothing has to load during the game");
		return row;
	}

	/// <summary>"2 points" in the bar; step by step: "up to 5 points" (it's worth less after each hint).</summary>
	public static string PointsPill(PublicQuestion q) =>
		q.HintCount != null || q.RankScoring ? Lang.F("up to {0} points", q.Points)
		: q.Points == 1 ? Lang.T("1 point") : Lang.F("{0} points", q.Points);

	/// <summary>"left at Q 4/13" / "joined at Q 3/13" under a name (or null).</summary>
	public static Label? SeatNote(PublicPlayer p, RoomState room) =>
		!p.Connected && p.LeftAt is { } left ? U.Text(Lang.F("left at Q {0}/{1}", left, room.Quiz.QuestionCount), 12, C.Coral)
		: p.JoinedAt is { } joined ? U.Muted(Lang.F("joined at Q {0}/{1}", joined, room.Quiz.QuestionCount), 12, false)
		: null;

	/// <summary>"×2" and lightning bolt pills for the jokers a player plays on the current question (once it's shown).</summary>
	static void AddJokers(HBoxContainer row, RoomState room, string playerId)
	{
		var game = room.Game;
		if (game == null) return;
		foreach (var j in game.Jokers.Where(j => j.PlayerId == playerId && j.Index == game.Index && (game.Shown || game.Stage == Stage.Reveal)))
			row.AddChild(Bolt.JokerPill(j.Kind, C.Accent, true).Tip(Joker.Label(j.Kind)));
	}

	/// <summary>
	/// Which positions a player's scoreboard shows: the top 3, then the one ahead of them, themselves and the one behind
	/// (their closest competition). With few players that's everyone.
	/// </summary>
	public static List<int> ShownPositions(int count, int me)
	{
		var shown = new SortedSet<int> { 0, 1, 2, me - 1, me, me + 1 };
		return shown.Where(i => i >= 0 && i < count).ToList();
	}

	/// <summary>Player boards opened with a click on "⋮": until when they stay open (seconds, Time.GetTicksMsec).</summary>
	static readonly Dictionary<string, double> _openUntil = new();
	/// <summary>An opened board closes again after this long without the mouse over it.</summary>
	const double OpenIdleSec = 5;

	static double Now => Time.GetTicksMsec() / 1000.0;

	/// <summary>
	/// Players by position (points, then the tiebreaks); at the reveal it shows what each one just won. A player's own
	/// screen (<paramref name="highlight"/>, no <paramref name="adjust"/>) shows the top 3 and the positions around them, with
	/// "⋮" where some are skipped: clicking it shows everyone (the side column scrolls) until the mouse has been away for
	/// a few seconds. The host's shows everyone.
	/// </summary>
	/// <param name="adjust">Host: − and + buttons to take away or add a point at will.</param>
	public static Control Scoreboard(RoomState room, string? highlight = null, Action<string, int>? adjust = null)
	{
		var list = U.V(6);
		var board = highlight ?? (adjust != null ? "host" : "");
		void Fill()
		{
			list.Clear();
			var open = _openUntil.TryGetValue(board, out var until) && Now < until;
			if (!open) _openUntil.Remove(board);
			FillScoreboard(list, room, highlight, adjust, board, open, () =>
			{
				_openUntil[board] = Now + OpenIdleSec;
				Fill();
			});
			if (!open) return;
			// Open: stays so while the mouse is over it, closes a few seconds after it leaves.
			list.AddChild(new FrameWatch(() =>
			{
				if (list.GetGlobalRect().HasPoint(list.GetGlobalMousePosition())) _openUntil[board] = Now + OpenIdleSec;
				else if (!_openUntil.TryGetValue(board, out var end) || Now >= end) Callable.From(Fill).CallDeferred();
			}));
		}
		Fill();
		return list;
	}

	static void FillScoreboard(VBoxContainer list, RoomState room, string? highlight, Action<string, int>? adjust, string board, bool open, Action expand)
	{
		var gained = (room.Game?.Reveal?.Entries ?? new List<RevealEntry>()).ToDictionary(e => e.PlayerId, e => e.Points);
		var progress = room.Game?.MediaProgress;
		var rows = room.Ranked();
		var me = rows.FindIndex(p => p.Id == highlight);
		var shown = adjust == null && me >= 0 && !open ? ShownPositions(rows.Count, me) : Enumerable.Range(0, rows.Count).ToList();
		Control More(int count) =>
			U.Link(Lang.F("⋮  {0} more", count), expand, 13).Tip("Show everyone").Center();
		var previous = -1;
		// (open and closed boards keep their own places, so opening one doesn't slide every row)
		var slotsKey = open ? board + "|open" : board;
		var last = _lastSlots.GetValueOrDefault(slotsKey) ?? new();
		var slots = new Dictionary<string, (int Slot, SlideRow Row)>();
		foreach (var i in shown)
		{
			if (i > previous + 1) list.AddChild(More(i - previous - 1));
			previous = i;
			var p = rows[i];
			// Sound/video questions: how much of the file each player has, right after the name.
			var ring = progress != null && p.Connected && progress.TryGetValue(p.Id, out var percent) ? new[] { (Control)new DownloadRing(percent, 16) } : Array.Empty<Control>();
			var row = Row(8, RankBadge(room.Place(p.Id), true), new Avatar(p, 26), new NameRow(p, SeatNote(p, room), ring).Grow());
			// (what decides between equal scores, on hover)
			if (room.Stats.GetValueOrDefault(p.Id) is { } st)
				row.TooltipText = string.Join("\n", new[]
				{
					st.AvgAnswerS is { } a ? Lang.F("Average answer time: {0} s", a.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture)) : null,
					st.AvgBuzzS is { } b ? Lang.F("Average buzz time: {0} s", b.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture)) : null,
				}.Where(t => t != null));
			AddJokers(row, room, p.Id);
			if (Moved(room, rows, gained, p.Id) is { } moved) row.AddChild(moved);
			if (gained.TryGetValue(p.Id, out var g) && g != 0) row.AddChild(U.Text($"+{g}", 14, C.Teal, Fonts.Bold));
			// (centred in the row, level with the − and + keys' faces, which sit 1–2 px above their middle)
			row.AddChild(U.Mono($"{room.Score(p.Id)}", 18).With(l =>
			{
				l.VerticalAlignment = VerticalAlignment.Center;
				l.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
				l.HorizontalAlignment = HorizontalAlignment.Right;
				l.CustomMinimumSize = new Vector2(28, 0);
			}));
			if (adjust != null)
			{
				var id = p.Id;
				row.AddChild(U.Small("−", () => adjust(id, -1)).Tip(Lang.F("Take a point from {0}", p.Name)));
				row.AddChild(U.Small("+", () => adjust(id, 1)).Tip(Lang.F("Give {0} a point", p.Name)));
			}
			var slide = new SlideRow();
			slide.AddChild(U.Panel(row, p.Id == highlight ? "ChipMe" : "Card2").With(c => c.AddThemeStyleboxOverride("panel",
				UiTheme.Box(C.Panel2, 10, p.Id == highlight ? C.Teal : C.Border, 1, 10, 6))));
			// Overtaken: rows that changed places slide from where they were (still sliding: from where they are now).
			var slot = list.GetChildCount();
			if (last.TryGetValue(p.Id, out var before))
				slide.SlideFrom(before.Slot - slot + (GodotObject.IsInstanceValid(before.Row) ? before.Row.Rows : 0));
			slots[p.Id] = (slot, slide);
			list.AddChild(slide);
		}
		_lastSlots[slotsKey] = slots;
		if (shown.Count > 0 && shown[^1] < rows.Count - 1) list.AddChild(More(rows.Count - 1 - shown[^1]));
	}

	/// <summary>At the reveal: "▲2" (green) or "▼1" (red), the places a player moved with this question's points (or null).</summary>
	public static Control? Moved(RoomState room, List<PublicPlayer> rows, Dictionary<string, int> gained, string id)
	{
		if (gained.Count == 0) return null;
		int Before(string pid) => room.Score(pid) - gained.GetValueOrDefault(pid);
		var before = 1 + rows.Count(o => Before(o.Id) > Before(id));
		var moved = before - room.Place(id);
		return moved == 0 ? null
			: U.Text(moved > 0 ? $"▲{moved}" : $"▼{-moved}", 13, moved > 0 ? C.Teal : C.Coral, Fonts.Bold)
				.Raw().Tip(moved > 0 ? Lang.F("Up {0} places with this question", moved) : Lang.F("Down {0} places with this question", -moved));
	}

	/// <summary>
	/// The side column on the host's and the players' screens: the buzz order, the timeline's turn order or who has
	/// answered, then the scores. <paramref name="me"/> (a player) is highlighted.
	/// </summary>
	public static void SideInfo(VBoxContainer side, RoomState room, string? me = null, Action<string, int>? adjust = null)
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
			var what = game.Interactive?.Phase switch
			{
				InteractivePhase.Voting => "Votes: {0} / {1}",
				not null => "Recordings: {0} / {1}",
				null => "Answers: {0} / {1}",
			};
			side.AddChild(U.Eyebrow(Lang.F(what, game.AnsweredIds.Count, connected)));
			var chips = U.Flow(6);
			foreach (var p in room.Players) chips.AddChild(Chips.Player(p, game.AnsweredIds.Contains(p.Id) ? "✓" : null, p.Id == me, !game.AnsweredIds.Contains(p.Id)));
			side.AddChild(chips);
		}
		if (side.GetChildCount() > 0) side.AddChild(U.Gap(4));
		// ("Stats": tonight's numbers for everyone, in a window)
		var stats = U.Link("Stats", null, 13).Tip("Tonight so far: points, questions scored, answer times, how far off");
		stats.Pressed += () => NightStatsView.Popup(stats, room, me);
		side.AddChild(Row(8, U.Eyebrow("Scores").Grow(), stats));
		side.AddChild(Scoreboard(room, me, adjust));
	}

	/// <summary>During a reveal: whether the next question is a buzzer one (players decide on a Fast Pass with it).</summary>
	public static Control? NextPill(GameView game) =>
		game.JokerIndex is { } next && next != game.Index
			? U.Pill(game.JokerBuzzer ? Lang.T("Next: buzzer question") : Lang.T("Next: everyone answers"), game.JokerBuzzer ? C.Coral : C.Muted)
			: null;

	/// <summary>"⏸ Paused: waiting for Ann to come back." (null when the game isn't paused).</summary>
	public static string? PausedText(RoomState room) =>
		room.Game is { Paused: true } game
			? Lang.F("⏸ Paused: waiting for {0} to come back.", string.Join(", ", game.WaitingFor.Select(id => room.Player(id).Name)))
			: null;

	/// <summary>"★ Bonus +2 · Also name the game" under the question, if it has a bonus.</summary>
	public static Control? BonusLine(PublicQuestion q, int size = 16)
	{
		if (q.BonusPoints <= 0 || string.IsNullOrWhiteSpace(q.BonusText)) return null;
		var row = Row(8, U.Text(Lang.F("★ Bonus +{0}", q.BonusPoints), size, C.Accent, Fonts.Black), U.Text(q.BonusText, size, C.Text, null, true).Raw().Grow());
		var panel = U.Panel(row, "Clear");
		panel.AddThemeStyleboxOverride("panel", UiTheme.Box(new Color(C.Accent, 0.1f), 10, C.Accent, 1, 12, 6));
		return panel;
	}

	/// <summary>Host, at the reveal: give a player the bonus or take it back.</summary>
	static Button BonusButton(PublicQuestion q, RevealEntry e, Action<string> bonus) =>
		U.Btn(e.Bonus ? Lang.F("★ Bonus +{0}", q.BonusPoints) : Lang.T("Give bonus"), e.Bonus ? "AccentButton" : "SmallButton", () => bonus(e.PlayerId), 14)
			.Tip(q.BonusText ?? "");

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
			var statusText = b.FastPass ? Lang.F("Fast Pass · {0}", Lang.T(status)) : Lang.T(status);
			var statusRow = U.H(4, U.Muted(statusText, 12, false).Raw());
			if (b.FastPass) statusRow.AddChild(Bolt.Rect(13));
			if (b.FastPass) statusRow.MoveChild(statusRow.GetChild(1), 0);
			var row = Row(8, RankBadge(i + 1), new Avatar(p, 26),
				U.V(0, U.Text(p.Name, 15).Raw(), statusRow).Grow(),
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
	/// <param name="bonus">Host, on a question with a bonus: give it to a player or take it back.</param>
	/// <param name="heading">False on players' screens, which show the answer above it already.</param>
	public static Control RevealPanel(RoomState room, Action<string>? toggle = null, Action<string>? bonus = null, bool heading = true)
	{
		var game = room.Game!;
		var reveal = game.Reveal!;
		var q = game.Question;
		var box = heading ? U.V(10, new HSeparator(), U.Eyebrow("The answer"), U.Text(reveal.AnswerText, 34, C.Accent, Fonts.Black, true).Raw()) : U.V(10, U.Eyebrow("Everyone's answers"));
		if (q.BonusPoints <= 0) bonus = null;
		if (q.Buzzer)
		{
			if (reveal.WinnerIds.FirstOrDefault() is { } winnerId)
			{
				var w = room.Player(winnerId);
				var gotIt = q.HasPlayClock && reveal.GuessTimeSec is { } t
					? Lang.F(q.Type == QuestionType.Sound ? "got it after {0} of listening" : "got it after {0} of watching", U.FormatSeconds(t))
					: Lang.T("got it");
				var row = Row(6, new Avatar(w, 26), U.Bold(w.Name, 17).Raw(), U.Text(gotIt, 17));
				var entry = reveal.Entries.FirstOrDefault(e => e.PlayerId == winnerId);
				row.AddChild(U.Text($"· +{entry?.Points ?? q.Points}", 17, C.Teal, Fonts.Bold));
				if (entry?.Doubled == true) row.AddChild(U.Pill(Lang.T("×2 Double points"), C.Accent));
				if (bonus != null && entry != null)
				{
					row.AddChild(U.Fill());
					row.AddChild(BonusButton(q, entry, bonus));
				}
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
			if (e.Doubled) row.AddChild(U.Pill("×2", C.Accent).Tip("Double points"));
			row.AddChild(U.Text(e.Points > 0 ? $"+{e.Points}" : "", 15, C.Teal, Fonts.Bold).MinSize(36));
			if (toggle != null)
			{
				var id = e.PlayerId;
				row.AddChild(U.Btn(e.Correct ? "✓ Counts" : "Doesn't count", e.Correct ? "TealButton" : "SmallButton", () => toggle(id), 14).Tip("Click to change"));
			}
			if (bonus != null) row.AddChild(BonusButton(q, e, bonus));
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
	/// the gaps are buttons you click or drop <paramref name="item"/> on (gaps already tried are crossed out), and the
	/// picked gap holds the item as a card you can drag again; without it only tried gaps show (✗). The ends
	/// ("◀ Oldest", "Newest ▶") are the first and last items of the row.
	/// </summary>
	public static Control TimelineRow(PublicQuestion q, TimelineView tl, int? picked = null, Action<int>? pick = null, string? item = null)
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
				if (gap == picked) flow.AddChild(new TimelineDragCard(item ?? "", true));
				else
				{
					var b = new TimelineGap(wrong, () => pick(gap));
					b.TooltipText = Lang.T(gap == 0 ? "Before the first one" : gap == tl.Placed.Count ? "After the last one" : "Between these two");
					flow.AddChild(b);
				}
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
