using System;
using System.Linq;
using Godot;
using QuizNight.Net;
using QuizNight.Shared;

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

	public void Refresh(RoomState room)
	{
		_room = room;
		var game = room.Game!;
		var q = game.Question;
		var me = room.Player(MyId);
		_bar.Set($"{me.Name}|{room.Score(MyId)}", () => GameParts.Row(10,
			new Avatar(me, 30), U.Bold(me.Name, 17).Raw(), U.Fill(), U.Pill(Lang.F("{0} pts", room.Score(MyId)), C.Accent)));

		var key = $"{game.Index}|{game.Stage}";
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
		if (q.Buzzer)
		{
			var mine = game.Buzzes.FirstOrDefault(b => b.PlayerId == MyId);
			var answering = game.Buzzes.FirstOrDefault(b => b.Status == BuzzStatus.Answering);
			var place = game.Buzzes.Where(b => b.Status == BuzzStatus.Waiting).ToList().FindIndex(b => b.PlayerId == MyId);
			_panel.Set($"{key}|{mine?.Status}|{place}|{answering?.PlayerId}", () => Buzzer(room));
		}
		else if (q.Choices != null) _panel.Set($"{key}|{game.MyAnswer?.Num}", () => Choices(room));
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
		var watch = game.Question.Type == QuestionType.Sound ? "Listen to the host's screen" : game.Question.Type == QuestionType.Video ? "Watch the host's screen" : null;
		if (watch != null) area.AddChild(U.Muted(watch, 16, false, HorizontalAlignment.Center).Center());
		if (answering != null) area.AddChild(U.Muted(Lang.F("{0} is answering — buzz to get in line.", room.Player(answering.PlayerId).Name), 14, false, HorizontalAlignment.Center).Center());
		area.AddChild(new BuzzButton(_client.Buzz).Center());
		area.AddChild(U.Muted("or press Space", 13, false, HorizontalAlignment.Center).Center());
		return U.Pad(area, 0, 10, 0, 10);
	}

	Control Choices(RoomState room)
	{
		var game = room.Game!;
		var mine = game.MyAnswer?.Num is { } n ? (int)n : (int?)null;
		var error = U.Error();
		var list = U.V(10);
		for (var i = 0; i < (game.Question.Choices?.Count ?? 0); i++)
		{
			if (game.Question.Choices![i] is not { } text) continue;
			var index = i;
			var b = new Button { MouseDefaultCursorShape = CursorShape.PointingHand, CustomMinimumSize = new Vector2(0, 72) };
			var picked = mine == i;
			b.AddThemeStyleboxOverride("normal", UiTheme.Box(C.Panel2, 14, picked ? C.Choice[i] : C.Border, picked ? 3 : 1, 14, 10));
			b.AddThemeStyleboxOverride("hover", UiTheme.Box(C.Panel2.Lerp(C.Choice[i], 0.15f), 14, C.Choice[i], 1, 14, 10));
			b.AddThemeStyleboxOverride("pressed", UiTheme.Box(C.Panel2.Lerp(C.Choice[i], 0.25f), 14, C.Choice[i], 2, 14, 10));
			b.AddThemeStyleboxOverride("disabled", UiTheme.Box(C.Panel2, 14, picked ? C.Choice[i] : C.Border, picked ? 3 : 1, 14, 10));
			b.Disabled = mine != null;
			if (mine != null && !picked) b.Modulate = new Color(1, 1, 1, 0.4f);
			var row = GameParts.Row(14, GameParts.Letter(i, 44), U.Text(text, 20, C.Text, Fonts.Bold, true).Raw());
			row.MouseFilter = MouseFilterEnum.Ignore;
			foreach (var c in row.GetChildren().OfType<Control>()) c.MouseFilter = MouseFilterEnum.Ignore;
			b.AddChild(U.Pad(row, 14, 0, 14, 0).With(m =>
			{
				m.SetAnchorsPreset(LayoutPreset.FullRect);
				m.MouseFilter = MouseFilterEnum.Ignore;
			}));
			b.Pressed += () => Submit(game.Index, AnswerValue.Number(index), error);
			list.AddChild(b);
		}
		if (mine is { } m) list.AddChild(U.Muted(Lang.F("Locked in: {0}. Waiting for the others…", QuizRules.ChoiceLetters[m]), 15, false, HorizontalAlignment.Center).Center());
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
				var text = q.IsMedia && reveal.GuessTimeSec is { } t
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
		ButtonDown += Buzz;
	}

	void Buzz()
	{
		if (_sent) return;
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
