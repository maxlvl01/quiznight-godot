using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using QuizNight.Net;
using QuizNight.Shared;
using QuizNight.Storage;

namespace QuizNight.Ui.Screens;

/// <summary>A player's screen: buzz button, A–D buttons, number field or map — then the reveal.</summary>
public partial class PlayerGameScreen : Control, IRoomScreen
{
	readonly ClientSession _client;
	readonly Keyed _bar = new();
	readonly Keyed _question = new();
	readonly Keyed _panel = new();
	RoomState _room = new();

	public PlayerGameScreen() : this(null!) { }

	public PlayerGameScreen(ClientSession client)
	{
		_client = client;
		SetAnchorsPreset(LayoutPreset.FullRect);
		var page = U.V(16, _bar, _question, _panel);
		AddChild(U.Page(page, 860, 20));
	}

	string MyId => _client.MyId ?? "";

	int _heardIndex = -1;
	int _heardBuzzes;

	public void Refresh(RoomState room)
	{
		_room = room;
		var game = room.Game!;
		var q = game.Question;
		var me = room.Player(MyId);
		// A new buzz in this question plays the host's buzzer sound, so everyone hears that someone buzzed.
		// Not on the first state after (re)joining, which may already contain old buzzes.
		var heard = game.Index == _heardIndex ? _heardBuzzes : 0;
		if (_heardIndex >= 0 && game.Buzzes.Count > heard) Sfx.PlayRoomBuzzer(room.Settings.BuzzerSound);
		_heardIndex = game.Index;
		_heardBuzzes = game.Buzzes.Count;
		_bar.Set($"{me.Name}|{room.Score(MyId)}", () => GameParts.Row(10,
			new Avatar(me, 30), U.Bold(me.Name, 17).Raw(), U.Fill(), U.Pill(Lang.F("{0} pts", room.Score(MyId)), C.Accent)));

		var key = $"{game.Index}|{game.Stage}|{game.Shown}";
		_question.Set(key, () =>
		{
			var box = U.V(10, GameParts.Row(10,
				U.Muted(Lang.F("Question {0} / {1}", game.Index + 1, game.Total), 14, false), Chips.Question(q), U.Fill(),
				game.Stage == Stage.Open ? new Countdown(game.EndsAt) : U.Gap(0)));
			box.AddChild(U.Text(q.Prompt, 28, C.Text, Fonts.Black, true).Raw());
			if (q.ImageKey != null && q.Type != QuestionType.MapPin && game.Stage == Stage.Open)
				box.AddChild(new AssetImage(_client.Assets, q.ImageKey, 360));
			return box;
		});

		if (game.Stage == Stage.Reveal)
		{
			_panel.Set($"{key}|reveal", () => Reveal(room));
			return;
		}
		if (!game.Shown)
		{
			_panel.Set($"{key}|hidden", () => Status(U.Bold("Get ready…", 28), U.Muted("The host is about to show the next question.", 16)));
			return;
		}
		if (q.Buzzer)
		{
			var mine = game.Buzzes.FirstOrDefault(b => b.PlayerId == MyId);
			var answering = game.Buzzes.FirstOrDefault(b => b.Status == BuzzStatus.Answering);
			var place = game.Buzzes.Where(b => b.Status == BuzzStatus.Waiting).ToList().FindIndex(b => b.PlayerId == MyId);
			_panel.Set($"{key}|{mine?.Status}|{place}|{answering?.PlayerId}|{game.BuzzersOpen}", () => Buzzer(room));
		}
		else if (q.Choices != null) _panel.Set($"{key}|{game.MyAnswer?.Num}|{string.Join(",", game.MyAnswer?.Picks ?? new List<int>())}", () => Choices(room));
		else if (game.Timeline is { } tl)
			_panel.Set($"{key}|tl|{tl.Placed.Count}|{tl.TurnPlayerId}|{string.Join(",", tl.WrongGaps)}|{tl.Last?.Kind}{tl.Last?.PlayerId}{tl.Last?.Item}", () => Timeline(room));
		else if (q.Type == QuestionType.Order) _panel.Set($"{key}|{game.MyAnswer?.Picks != null}", () => Order(room));
		else if (q.Type == QuestionType.Write) _panel.Set($"{key}|{game.MyAnswer?.Text}", () => Write(room));
		else if (q.Type == QuestionType.Estimation) _panel.Set($"{key}|{game.MyAnswer?.Num}", () => Estimate(room));
		else _panel.Set($"{key}|{game.MyAnswer?.Point != null}", () => MapPin(room));
	}

	static Control Status(params Control[] kids)
	{
		var box = U.V(8, kids);
		box.Alignment = BoxContainer.AlignmentMode.Center;
		foreach (var k in kids)
			if (k is Label l) l.HorizontalAlignment = HorizontalAlignment.Center;
		return U.Panel(box.MinSize(0, 180));
	}

	void Submit(int index, AnswerValue value, Label error)
	{
		error.Visible = false;
		_client.Answer(index, value, result =>
		{
			if (result.Ok || !IsInstanceValid(error)) return;
			error.Text = result.Error ?? "";
			error.Visible = true;
		});
	}

	Control Buzzer(RoomState room)
	{
		var game = room.Game!;
		var mine = game.Buzzes.FirstOrDefault(b => b.PlayerId == MyId);
		var answering = game.Buzzes.FirstOrDefault(b => b.Status == BuzzStatus.Answering);
		var place = game.Buzzes.Where(b => b.Status == BuzzStatus.Waiting).ToList().FindIndex(b => b.PlayerId == MyId);
		if (mine?.Status == BuzzStatus.Answering)
		{
			var panel = Status(U.Text("Your turn!", 44, C.AccentInk, Fonts.Black), U.Text("Say your answer out loud.", 18, C.AccentInk),
				new Countdown(game.AnswerEndsAt, "Time to answer").Center());
			panel.AddThemeStyleboxOverride("panel", UiTheme.Box(C.Accent, 14, null, 0, 24, 22));
			return panel;
		}
		if (mine != null)
		{
			if (mine.Status == BuzzStatus.Wrong)
				return Status(U.Text("Not quite.", 30, C.Text, Fonts.Black), U.Muted("The others get a go now.", 16), U.Muted(Lang.F("You buzzed after {0}.", U.FormatSeconds(mine.AtSec)), 13));
			var kids = new System.Collections.Generic.List<Control> { U.Text($"#{place + 2}", 56, C.Accent, Fonts.Black), U.Bold("You're in line", 20) };
			if (answering != null) kids.Add(U.Muted(Lang.F("{0} is answering.", room.Player(answering.PlayerId).Name), 16));
			kids.Add(U.Muted(Lang.F("You buzzed after {0}.", U.FormatSeconds(mine.AtSec)), 13));
			return Status(kids.ToArray());
		}
		var area = U.V(14);
		area.Alignment = BoxContainer.AlignmentMode.Center;
		var watch = game.Question.Type == QuestionType.Sound ? "Listen to the host's screen" : game.Question.Type is QuestionType.Video or QuestionType.Image ? "Watch the host's screen" : null;
		if (watch != null) area.AddChild(U.Muted(watch, 16, false, HorizontalAlignment.Center).Center());
		if (answering != null) area.AddChild(U.Muted(Lang.F("{0} is answering — buzz to get in line.", room.Player(answering.PlayerId).Name), 14, false, HorizontalAlignment.Center).Center());
		var buzz = new BuzzButton(_client.Buzz) { Disabled = !game.BuzzersOpen };
		area.AddChild(buzz.Center());
		if (!game.BuzzersOpen) area.AddChild(U.Muted("Buzzers are locked — the host unlocks them in a moment.", 15, false, HorizontalAlignment.Center).Center());
		area.AddChild(U.Muted("or press Space", 13, false, HorizontalAlignment.Center).Center());
		return U.Pad(area, 0, 10, 0, 10);
	}

	Control Choices(RoomState room)
	{
		var game = room.Game!;
		var multi = game.Question.Mode == ClassicMode.Multi;
		var locked = game.MyAnswer?.Picks ?? (game.MyAnswer?.Num is { } n ? new List<int> { (int)n } : null);
		// Multi: answers toggle on and off until "Lock in".
		var picks = new HashSet<int>(locked ?? new List<int>());
		var error = U.Error();
		var list = U.V(10);
		if (multi && locked == null) list.AddChild(U.Muted("Several answers can be right. Pick all you think are, then lock in.", 15, false, HorizontalAlignment.Center).Center());
		Button? lockIn = null;
		for (var i = 0; i < (game.Question.Choices?.Count ?? 0); i++)
		{
			if (game.Question.Choices![i] is not { } text) continue;
			var index = i;
			var b = new Button { MouseDefaultCursorShape = CursorShape.PointingHand, CustomMinimumSize = new Vector2(0, 72) };
			void Style(bool picked)
			{
				b.AddThemeStyleboxOverride("normal", UiTheme.Box(picked && multi ? C.Panel2.Lerp(C.Choice[index], 0.2f) : C.Panel2, 14, picked ? C.Choice[index] : C.Border, picked ? 3 : 1, 14, 10));
				b.AddThemeStyleboxOverride("hover", UiTheme.Box(C.Panel2.Lerp(C.Choice[index], picked && multi ? 0.3f : 0.15f), 14, C.Choice[index], picked ? 3 : 1, 14, 10));
				b.AddThemeStyleboxOverride("pressed", UiTheme.Box(C.Panel2.Lerp(C.Choice[index], 0.25f), 14, C.Choice[index], 2, 14, 10));
				b.AddThemeStyleboxOverride("disabled", UiTheme.Box(C.Panel2, 14, picked ? C.Choice[index] : C.Border, picked ? 3 : 1, 14, 10));
			}
			Style(picks.Contains(i));
			b.Disabled = locked != null;
			if (locked != null && !picks.Contains(i)) b.Modulate = new Color(1, 1, 1, 0.4f);
			var row = GameParts.Row(14, GameParts.Letter(i, 44), U.Text(text, 20, C.Text, Fonts.Bold, true).Raw());
			row.MouseFilter = MouseFilterEnum.Ignore;
			foreach (var c in row.GetChildren().OfType<Control>()) c.MouseFilter = MouseFilterEnum.Ignore;
			b.AddChild(U.Pad(row, 14, 0, 14, 0).With(m =>
			{
				m.SetAnchorsPreset(LayoutPreset.FullRect);
				m.MouseFilter = MouseFilterEnum.Ignore;
			}));
			b.Pressed += () =>
			{
				if (!multi)
				{
					Submit(game.Index, AnswerValue.Number(index), error);
					return;
				}
				if (!picks.Remove(index)) picks.Add(index);
				Style(picks.Contains(index));
				lockIn!.Disabled = picks.Count == 0;
			};
			list.AddChild(b);
		}
		if (multi && locked == null)
		{
			lockIn = U.Btn("Lock in", "AccentButton", () => Submit(game.Index, AnswerValue.Picked(picks), error)).Center();
			lockIn.Disabled = true;
			list.AddChild(lockIn);
		}
		if (locked != null)
		{
			var letters = string.Join(", ", locked.Select(i => QuizRules.ChoiceLetters[i]));
			list.AddChild(U.Muted(Lang.F("Locked in: {0}. Waiting for the others…", letters), 15, false, HorizontalAlignment.Center).Center());
		}
		list.AddChild(error);
		return list;
	}

	Control Estimate(RoomState room)
	{
		var game = room.Game!;
		var unit = game.Question.Unit ?? "";
		if (game.MyAnswer?.Num is { } mine)
			return Status(U.Muted("Locked in", 15), U.Text($"{Server.Game.Fmt(mine)} {unit}".Trim(), 44, C.Text, Fonts.Mono).Raw(), U.Muted("Waiting for the others…", 14));
		var error = U.Error();
		var lockIn = U.Btn("Lock in guess", "TealButton", null, 20);
		lockIn.Disabled = true;
		var field = new NumberField(null, v => lockIn.Disabled = v == null, placeholder: "Type a number") { ThemeTypeVariation = "BigInput" };
		void Send()
		{
			if (field.Value is { } v) Submit(game.Index, AnswerValue.Number(v), error);
		}
		lockIn.Pressed += Send;
		field.TextSubmitted += _ => Send();
		field.CallDeferred(Control.MethodName.GrabFocus);
		return U.Card(12, U.Field(unit != "" ? Lang.F("Your guess ({0})", unit) : Lang.T("Your guess"), field), lockIn, error);
	}

	/// <summary>Put in order: move items up and down, then lock in. Top of the list = OrderTop.</summary>
	Control Order(RoomState room)
	{
		var game = room.Game!;
		var q = game.Question;
		var items = q.Items ?? new List<string>();
		if (game.MyAnswer?.Picks is { } locked)
		{
			var mine = U.V(6, U.Muted("Locked in", 15));
			for (var i = 0; i < locked.Count; i++) mine.AddChild(U.Text($"{i + 1}. {items[locked[i]]}", 18).Raw());
			mine.AddChild(U.Muted("Waiting for the others…", 14));
			return Status(mine);
		}
		// Starts in the order the host shows them.
		var order = Enumerable.Range(0, items.Count).ToList();
		var error = U.Error();
		var rows = U.V(8);
		void Build()
		{
			rows.Clear();
			if (!string.IsNullOrWhiteSpace(q.OrderTop)) rows.AddChild(U.Muted("▲ " + q.OrderTop, 15).Raw());
			for (var i = 0; i < order.Count; i++)
			{
				var at = i;
				var up = U.Small("▲", () => { (order[at - 1], order[at]) = (order[at], order[at - 1]); Build(); });
				var down = U.Small("▼", () => { (order[at + 1], order[at]) = (order[at], order[at + 1]); Build(); });
				up.Disabled = at == 0;
				down.Disabled = at == order.Count - 1;
				rows.AddChild(U.Panel(GameParts.Row(10, U.Mono($"{at + 1}", 18, C.Muted), U.Text(items[order[at]], 19, C.Text, Fonts.Bold, true).Raw().Grow(), up, down), "Card2"));
			}
			if (!string.IsNullOrWhiteSpace(q.OrderBottom)) rows.AddChild(U.Muted("▼ " + q.OrderBottom, 15).Raw());
		}
		Build();
		var lockIn = U.Btn("Lock in order", "TealButton", () => Submit(game.Index, AnswerValue.Picked(order), error), 20);
		return U.V(12, U.Muted("Use ▲ and ▼ to put them in the right order.", 15), rows, lockIn, error);
	}

	/// <summary>Timeline: on your turn, pick the gap the item goes in and lock in; otherwise watch whose turn it is.</summary>
	Control Timeline(RoomState room)
	{
		var game = room.Game!;
		var tl = game.Timeline!;
		var box = U.V(14);
		if (tl.Last != null)
			box.AddChild(U.Text(GameParts.TimelineEventText(room, tl.Last), 16, tl.Last.Kind == TimelineEventKind.Right ? C.Teal : C.Muted, null, true, HorizontalAlignment.Center).Raw());
		if (tl.TurnPlayerId != MyId || tl.Current == null)
		{
			var info = new List<Control>();
			if (tl.TurnPlayerId is { } turn && tl.Current != null)
			{
				var p = room.Player(turn);
				info.Add(GameParts.Row(10, new Avatar(p, 30), U.Bold(Lang.F("{0}'s turn", p.Name), 22).Raw(), new Countdown(tl.TurnEndsAt, null, 22)).With(r => r.Alignment = BoxContainer.AlignmentMode.Center));
				info.Add(U.Muted(Lang.F("Where does {0} go?", tl.Current), 16, false, HorizontalAlignment.Center).Raw());
			}
			if (tl.Missed.Contains(MyId)) info.Add(U.Muted("You missed this one — the others get a go.", 14, false, HorizontalAlignment.Center));
			if (info.Count > 0) box.AddChild(Status(info.ToArray()));
			box.AddChild(GameParts.TimelineRow(game.Question, tl));
			return box;
		}

		// My turn.
		var banner = U.Panel(GameParts.Row(16,
			U.Text("Your turn!", 30, C.AccentInk, Fonts.Black),
			U.Text(Lang.F("Where does {0} go? Pick a gap, then lock in.", tl.Current), 17, C.AccentInk, null, true).Raw().Grow(),
			new Countdown(tl.TurnEndsAt, null, 24)));
		banner.AddThemeStyleboxOverride("panel", UiTheme.Box(C.Accent, 14, null, 0, 20, 14));
		box.AddChild(banner);
		box.AddChild(GameParts.TimelineCard(tl.Current, "", false, true).With(c => c.SizeFlagsHorizontal = SizeFlags.ShrinkCenter));
		var error = U.Error();
		var lockIn = U.Btn("Lock in", "TealButton", null, 20).Center();
		lockIn.Disabled = true;
		int? picked = null;
		var rowSlot = U.V(0);
		void Build()
		{
			rowSlot.Clear();
			rowSlot.AddChild(GameParts.TimelineRow(game.Question, tl, picked, gap =>
			{
				picked = gap;
				lockIn.Disabled = false;
				Build();
			}));
		}
		Build();
		lockIn.Pressed += () =>
		{
			if (picked is not { } gap) return;
			lockIn.Disabled = true;
			_client.Answer(game.Index, AnswerValue.Number(gap), result =>
			{
				if (result.Ok || !IsInstanceValid(error)) return;
				error.Text = result.Error ?? "";
				error.Visible = true;
				lockIn.Disabled = false;
			});
		};
		box.AddChild(rowSlot);
		box.AddChild(lockIn);
		box.AddChild(error);
		return box;
	}

	/// <summary>Write the answer: type it and lock it in.</summary>
	Control Write(RoomState room)
	{
		var game = room.Game!;
		if (game.MyAnswer?.Text is { } mine)
			return Status(U.Muted("Locked in", 15), U.Text(mine, 32, C.Text, Fonts.Bold, true).Raw(), U.Muted("Waiting for the others…", 14));
		var error = U.Error();
		var lockIn = U.Btn("Lock in answer", "TealButton", null, 20);
		lockIn.Disabled = true;
		var field = U.Input("", "Type your answer", t => lockIn.Disabled = t.Trim() == "", QuestionLimits.WrittenMaxLength).With(e => e.ThemeTypeVariation = "BigInput");
		void Send()
		{
			if (field.Text.Trim() != "") Submit(game.Index, AnswerValue.Written(field.Text), error);
		}
		lockIn.Pressed += Send;
		field.TextSubmitted += _ => Send();
		field.CallDeferred(Control.MethodName.GrabFocus);
		return U.Card(12, U.Field("Your answer", field), lockIn, error);
	}

	Control MapPin(RoomState room)
	{
		var game = room.Game!;
		var q = game.Question;
		if (game.MyAnswer?.Point != null) return Status(U.Bold("Pin locked in", 24), U.Muted("Waiting for the others…", 15));
		NormPoint? pin = null;
		var error = U.Error();
		var lockIn = U.Btn("Tap the map to place your pin", "TealButton", null, 18);
		lockIn.Disabled = true;
		lockIn.Pressed += () =>
		{
			if (pin is { } p) Submit(game.Index, AnswerValue.At(p), error);
		};
		var map = q.Map != null
			? GameMap.Make(q.Map, _client.Assets.Map(q.Map), null, p =>
			{
				pin = p;
				lockIn.Disabled = false;
				lockIn.Text = "Lock in pin";
			}, null, 600)
			: U.Error("This question has no map.");
		// Custom maps arrive from the host; redraw once they're here.
		if (q.Map?.AssetKey is { } key && map is MapView view && view.Canvas != null && _client.Assets.Get(key) == null)
		{
			void OnLoaded(string k)
			{
				if (k != key || !IsInstanceValid(view)) return;
				view.Canvas.SetTexture(_client.Assets.Get(key), q.Map.Width, q.Map.Height);
				_client.Assets.Loaded -= OnLoaded;
			}
			_client.Assets.Loaded += OnLoaded;
		}
		var box = U.V(10);
		if (q.ImageKey != null)
		{
			var shot = new AssetImage(_client.Assets, q.ImageKey, 600);
			map.Visible = false;
			box.AddChild(U.Segmented(new[] { ("shot", "Screenshot"), ("map", "Map") }, "shot", id =>
			{
				shot.Visible = id == "shot";
				map.Visible = id == "map";
			}));
			box.AddChild(shot);
		}
		box.AddChild(map);
		box.AddChild(lockIn);
		box.AddChild(error);
		return box;
	}

	Control Reveal(RoomState room)
	{
		var game = room.Game!;
		var reveal = game.Reveal!;
		var q = game.Question;
		var mine = reveal.Entries.FirstOrDefault(e => e.PlayerId == MyId);
		var kids = new System.Collections.Generic.List<Control> { U.Eyebrow("The answer"), U.Text(reveal.AnswerText, 26, C.Accent, Fonts.Black, true).Raw() };
		if (q.Buzzer)
		{
			if (reveal.WinnerIds.FirstOrDefault() is { } wid)
			{
				var me = wid == MyId;
				var name = room.Player(wid).Name;
				var text = q.HasPlayClock && reveal.GuessTimeSec is { } t
					? me ? Lang.F("You got it after {0}.", U.FormatSeconds(t)) : Lang.F("{0} got it after {1}.", name, U.FormatSeconds(t))
					: me ? Lang.T("You got it.") : Lang.F("{0} got it.", name);
				kids.Add(U.Text(text, 17));
			}
			else kids.Add(U.Text("Nobody got it.", 17));
		}
		if (mine != null && !q.Buzzer) kids.Add(U.Text(Lang.F("You: {0}", mine.Text), 17));
		kids.Add(mine is { Points: > 0 } ? U.Text($"+{mine.Points}", 48, C.Accent, Fonts.Black) : U.Muted("No points this time.", 15));
		kids.Add(U.Muted("Waiting for the host to continue…", 13));
		var panel = Status(kids.ToArray());
		if (mine is { Points: > 0 }) panel.AddThemeStyleboxOverride("panel", UiTheme.Box(new Color(C.Teal, 0.08f), 14, C.Teal, 1, 24, 22));
		var box = U.V(14, panel);
		// Image reveal: everyone gets the whole picture now.
		if (q.Type == QuestionType.Image && q.ImageKey != null) box.AddChild(new AssetImage(_client.Assets, q.ImageKey, 360));
		if (q.Type == QuestionType.MapPin && q.Map != null)
			box.AddChild(GameMap.Make(q.Map, _client.Assets.Map(q.Map), reveal.Target, null, GameParts.RevealPins(room), 520));
		box.AddChild(U.Eyebrow("Scores"));
		box.AddChild(GameParts.Scoreboard(room, MyId));
		return box;
	}
}

/// <summary>The big round BUZZ! button. Space works too.</summary>
public partial class BuzzButton : Button
{
	readonly Action _onBuzz;
	bool _sent;

	public BuzzButton() : this(() => { }) { }

	public BuzzButton(Action onBuzz)
	{
		_onBuzz = onBuzz;
		Text = "BUZZ!";
		CustomMinimumSize = new Vector2(240, 240);
		MouseDefaultCursorShape = CursorShape.PointingHand;
		AddThemeFontOverride("font", Fonts.Black);
		AddThemeFontSizeOverride("font_size", 46);
		foreach (var (state, color) in new[] { ("normal", C.Coral), ("hover", C.Coral.Lightened(0.08f)), ("pressed", C.Coral.Darkened(0.12f)), ("focus", C.Coral) })
		{
			var box = UiTheme.Box(color, 999, new Color("#a33a46"), 0);
			box.BorderWidthBottom = state == "pressed" ? 4 : 12;
			AddThemeStyleboxOverride(state, box);
		}
		foreach (var c in new[] { "font_color", "font_hover_color", "font_pressed_color", "font_focus_color" }) AddThemeColorOverride(c, new Color("#2a0a0e"));
		// Locked: grey, until the host unlocks the buzzers.
		var locked = UiTheme.Box(C.Panel2, 999, C.Border, 0);
		locked.BorderWidthBottom = 12;
		AddThemeStyleboxOverride("disabled", locked);
		AddThemeColorOverride("font_disabled_color", C.Muted);
		ButtonDown += Buzz;
	}

	void Buzz()
	{
		if (_sent || Disabled) return;
		_sent = true;
		_onBuzz();
	}

	public override void _UnhandledKeyInput(InputEvent e)
	{
		if (e is InputEventKey { Pressed: true, Echo: false, Keycode: Key.Space })
		{
			Buzz();
			GetViewport().SetInputAsHandled();
		}
	}
}
