using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using QuizNight.Net;
using QuizNight.Shared;
using QuizNight.Storage;

namespace QuizNight.Ui.Screens;

/// <summary>
/// A player's screen: buzz button, A–D buttons, number field or map — then the reveal. Laid out like the host's
/// screen: a bar on top, the question and the answer area in a card, the buzz order / answers and scores on the side.
/// </summary>
public partial class PlayerGameScreen : Control, IRoomScreen
{
	readonly ClientSession _client;
	readonly Keyed _bar = new();
	readonly Keyed _notice = new();
	readonly Keyed _question = new();
	readonly Keyed _panel = new();
	readonly VBoxContainer _side = U.V(10);
	/// <summary>Interactive questions: plays the recordings when the host does.</summary>
	readonly ClipPlayer _clips;
	readonly HintTracker _hints = new();
	/// <summary>"Get ready" screen: how it works, or tonight's stats.</summary>
	string _readyTab = "how";
	/// <summary>Slideshows: the picture this player looks at once they may flip themselves (question index, picture).</summary>
	(int Index, int Slide)? _mySlide;
	RoomState _room = new();

	public PlayerGameScreen() : this(null!) { }

	public PlayerGameScreen(ClientSession client)
	{
		_client = client;
		_clips = new ClipPlayer(client.Assets);
		AddChild(_clips);
		SetAnchorsPreset(LayoutPreset.FullRect);
		// The card fills the column; pictures and maps in it take the height that's left.
		var card = U.Panel(U.V(14, _notice, _question, _panel).GrowV()).GrowV();
		var stageScroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled }.Grow(3).GrowV();
		stageScroll.AddChild(card.Grow());
		var sideScroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled }.MinSize(310).GrowV();
		sideScroll.AddChild(U.Panel(_side).Grow().GrowV());
		var body = U.Pad(U.H(16, stageScroll, sideScroll), 16, 16, 16, 16).GrowV();
		var layout = U.V(0, _bar, body);
		layout.SetAnchorsPreset(LayoutPreset.FullRect);
		AddChild(layout);
	}

	string MyId => _client.MyId ?? "";

	int _heardIndex = -1;
	int _heardBuzzes;
	readonly AnswerSounds _answerSounds = new();

	public void Refresh(RoomState room)
	{
		_room = room;
		var game = room.Game!;
		// (the game music pauses for questions with their own sound: sound, video, interactive)
		App.I.Audio.Quiet(game.Stage == Stage.Open && game.Shown && game.Question.Type is QuestionType.Sound or QuestionType.Video or QuestionType.Interactive);
		var q = game.Question;
		var me = room.Player(MyId);
		App.I.Announcer.Follow(room, MyId);
		// A new buzz in this question plays the host's buzzer sound, so everyone hears that someone buzzed.
		// Not on the first state after (re)joining, which may already contain old buzzes.
		var heard = game.Index == _heardIndex ? _heardBuzzes : 0;
		if (_heardIndex >= 0 && game.Buzzes.Count > heard) Sfx.PlayRoomBuzzer(room.Settings.BuzzerSound);
		_heardIndex = game.Index;
		_heardBuzzes = game.Buzzes.Count;
		_answerSounds.Update(game, MyId);
		_clips.Update(game);
		_bar.Set($"{game.Index}|{me.Name}|{room.Score(MyId)}|{game.JokerIndex}", () =>
		{
			var row = GameParts.Row(12,
				U.H(0, U.Text("Quiznight", 20, C.Text, Fonts.Black), U.Text("?", 20, C.Accent, Fonts.Black)),
				U.Pill(Lang.F("Question {0} / {1}", game.Index + 1, game.Total)),
				Chips.Question(q),
				U.Pill(GameParts.PointsPill(q)));
			// (the category, when the lobby shows them)
			if (q.Category != "") row.AddChild(GameParts.CategoryPill(q.Category));
			if (GameParts.NextPill(game) is { } next) row.AddChild(next);
			row.AddChild(U.Fill());
			row.AddChild(new RoomCodeButton(room.Code));
			row.AddChild(new Avatar(me, 28));
			row.AddChild(U.Bold(me.Name, 17).Raw());
			row.AddChild(U.Pill(Lang.F("{0} pts", room.Score(MyId)), C.Accent));
			row.AddChild(MediaIcon.SettingsGear());
			return U.Panel(row, "Bar");
		});

		_side.Clear();
		if (JokerBox(room) is { } jokers)
		{
			_side.AddChild(jokers);
			_side.AddChild(U.Gap(4));
		}
		GameParts.SideInfo(_side, room, MyId);

		var late = game.LateIds.Contains(MyId) && game.Stage == Stage.Open;
		var notice = GameParts.PausedText(room) ?? (late ? Lang.T("You joined during this question: you play from the next one. Watch along until then.") : null);
		_notice.Set(notice ?? "", () => notice == null ? null
			: U.Panel(U.Text(notice, 16, C.Accent, Fonts.Bold, true).Raw(), "Clear").With(p => p.AddThemeStyleboxOverride("panel", UiTheme.Box(new Color(C.Accent, 0.1f), 12, C.Accent, 1, 14, 10))));

		var key = $"{game.Index}|{game.Stage}|{game.Shown}|{q.Slide}";
		var flipHint = _hints.JustFlipped(game);
		// (a slideshow the host has shown all of: this player flips through it on their own, starting where the host is)
		var ownSlide = q is { AllSlidesShown: true, SlideCount: > 1 } && game.Stage == Stage.Open ? (_mySlide is { } ms && ms.Index == game.Index ? ms.Slide : q.Slide ?? 0) : (int?)null;
		if (ownSlide is { } own) _mySlide = (game.Index, own);
		_question.Set($"{key}|{game.Interactive?.Phase}|{q.Hints?.Count}|{ownSlide}", () =>
		{
			var box = U.V(14, GameParts.Row(16,
				U.Text(game.Shown || game.Stage == Stage.Reveal ? q.Prompt : "", 30, C.Text, Fonts.Black, true).Raw(),
				game.Stage == Stage.Open && game.Shown ? new Countdown(game.EndsAt, null, 30) : U.Gap(0)));
			if (game.Stage == Stage.Open && game.Shown && GameParts.BonusLine(q) is { } bonusLine) box.AddChild(bonusLine);
			if (q.HintCount != null && (game.Shown || game.Stage == Stage.Reveal))
			{
				box.AddChild(StepsParts.Cards(q, flipHint, 16));
				if (game.Stage == Stage.Open) box.AddChild(StepsParts.Worth(q, 15));
			}
			// What the host's screen shows plays here too, in step with it (bots without a window skip it).
			var open = game.Stage == Stage.Open && game.Shown;
			var window = DisplayServer.GetName() != "headless";
			if (q.ImageKey != null && q.Type == QuestionType.Image && open)
				box.AddChild(new PlayerImageReveal(_client.Assets, () => _room, q.ImageKey));
			else if (ownSlide is { } mine)
			{
				box.AddChild(new AssetImage(_client.Assets, Server.Game.SlideKey(game.Index, mine), U.Flexible) { Zoomable = true });
				var back = U.Btn("◀", "SmallButton", () => FlipOwn(-1), 18).MinSize(56, 0);
				back.Disabled = mine == 0;
				var next = U.Btn("▶", "SmallButton", () => FlipOwn(1), 18).MinSize(56, 0);
				next.Disabled = mine >= q.SlideCount!.Value - 1;
				var row = GameParts.Row(14, back, U.Text(Lang.F("Picture {0} / {1}", mine + 1, q.SlideCount.Value), 16, C.Text, Fonts.Bold), next,
					U.Muted("All shown: flip through them yourself", 14, false));
				row.Alignment = BoxContainer.AlignmentMode.Center;
				box.AddChild(row);
			}
			else if (q.ImageKey != null && q.Type != QuestionType.MapPin && game.Stage == Stage.Open)
			{
				box.AddChild(new AssetImage(_client.Assets, q.ImageKey, U.Flexible) { Zoomable = true });
				// (the host flips the slideshow; this screen follows)
				if (q is { SlideCount: > 1, Slide: { } slide })
					box.AddChild(U.Muted(Lang.F("Picture {0} / {1}", slide + 1, q.SlideCount.Value), 15, false, HorizontalAlignment.Center).Center());
			}
			if (q.MediaId != null && q.MediaKey != null && open && window)
				box.AddChild(q.Type == QuestionType.Video
					? new PlayerVideo(_client, () => _room, q.MediaId, q.MediaKey).MarkFlexible()
					: new PlayerSound(_client, () => _room, q.MediaId, q.MediaKey, q.MediaMime ?? ""));
			return box;
		});

		if (game.Stage == Stage.Reveal)
		{
			// (rebuilt when the host ticks an answer or gives a bonus)
			_panel.Set($"{key}|reveal|{string.Join(",", game.Reveal!.Entries.Select(e => $"{e.PlayerId}:{e.Points}:{e.Bonus}"))}", () => Reveal(room));
			return;
		}
		if (!game.Shown)
		{
			// (the space under "Get ready" explains the coming question, or shows tonight's stats)
			_panel.Set($"{key}|hidden|{_readyTab}|{string.Join(",", room.Ranking.Select(id => room.Score(id)))}", () => U.V(14,
				Status(U.Bold("Get ready…", 28), U.Muted("The host is about to show the next question.", 16)),
				U.Segmented(new[] { ("how", "How it works"), ("stats", "Tonight so far") }, _readyTab, id =>
				{
					_readyTab = id;
					Refresh(_room);
				}).With(s => s.SizeFlagsHorizontal = SizeFlags.ShrinkCenter),
				_readyTab == "stats"
					? U.Panel(NightStatsView.Table(room, MyId), "Card2")
					: U.Panel(U.V(8, GameParts.Row(10, new TypeIcon(q.Type, 26), U.Bold(Chips.TypeLabel(q), 20).Raw(),
							q.Category != "" ? GameParts.CategoryPill(q.Category) : U.Gap(0)),
						U.Text(NightStatsView.HowItWorks(q), 17, C.Text, null, true).Raw()), "Card2")));
			return;
		}
		if (late)
		{
			_panel.Set($"{key}|late", () => Status(U.Bold("Watch along", 26), U.Muted("You're in from the next question.", 16)));
			return;
		}
		if (game.Interactive is { } ia)
		{
			_panel.Set($"{key}|ia|{ia.Phase}|{(ia.Phase == InteractivePhase.Recording ? "" : ia.Clip.ToString())}|{ia.MyVote}", () => Interactive(room, ia));
			return;
		}
		if (q.Buzzer)
		{
			var mine = game.Buzzes.FirstOrDefault(b => b.PlayerId == MyId);
			var answering = game.Buzzes.FirstOrDefault(b => b.Status == BuzzStatus.Answering);
			var place = game.Buzzes.Where(b => b.Status == BuzzStatus.Waiting).ToList().FindIndex(b => b.PlayerId == MyId);
			_panel.Set($"{key}|{mine?.Status}|{place}|{answering?.PlayerId}|{game.BuzzersOpen}|{game.Paused}", () => Buzzer(room));
		}
		else if (q.Choices != null) _panel.Set($"{key}|{game.MyAnswer?.Num}|{string.Join(",", game.MyAnswer?.Picks ?? new List<int>())}", () => Choices(room));
		else if (game.Timeline is { } tl)
			_panel.Set($"{key}|tl|{tl.Phase}|{tl.PendingGap}|{tl.Placed.Count}|{tl.TurnPlayerId}|{string.Join(",", tl.WrongGaps)}|{tl.Last?.Kind}{tl.Last?.PlayerId}{tl.Last?.Item}", () => Timeline(room));
		else if (q.Type == QuestionType.Order) _panel.Set($"{key}|{game.MyAnswer?.Picks != null}", () => Order(room));
		else if (q.Written) _panel.Set($"{key}|{game.MyAnswer?.Text}", () => Write(room));
		else if (q.Type == QuestionType.Estimation) _panel.Set($"{key}|{game.MyAnswer?.Num}", () => Estimate(room));
		else _panel.Set($"{key}|{game.MyAnswer?.Point != null}", () => MapPin(room));
	}

	/// <summary>
	/// The jokers the host allows: play one for the next question (during a reveal, or while a question is still hidden),
	/// take it back until that question is shown. Each one once per match.
	/// </summary>
	Control? JokerBox(RoomState room)
	{
		var game = room.Game!;
		var allowed = Joker.All.Where(room.Settings.Jokers.Contains).ToList();
		if (allowed.Count == 0) return null;
		// (small keys with just a drawn icon: the bolt for the Fast Pass, a big ×2 for Double points; the name and what to do
		// with it are the tooltip, the line underneath says the one that matters now)
		var row = U.H(8);
		var lines = new System.Collections.Generic.List<string>();
		foreach (var kind in allowed)
		{
			var k = kind;
			var mine = game.Jokers.FirstOrDefault(j => j.PlayerId == MyId && j.Kind == k);
			string hint;
			Button button;
			var used = false;
			if (mine != null && (mine.Index < game.Index || mine.Index == game.Index && (game.Shown || game.Stage == Stage.Reveal)))
			{
				button = U.Btn("", "SmallButton", null, 14);
				button.Disabled = used = true;
				hint = Lang.F("Used on question {0}.", mine.Index + 1);
			}
			else if (mine != null)
			{
				button = U.Btn("", "TealButton", () => _client.PlayJoker(k, false), 14);
				hint = Lang.F("Played on question {0}. Click to take it back.", mine.Index + 1);
				lines.Insert(0, hint);
			}
			else
			{
				button = U.Btn("", "SmallButton", () => _client.PlayJoker(k, true), 14);
				var fastOff = k == Joker.FastPass && !game.JokerBuzzer;
				var taken = k == Joker.FastPass && game.FastPassTaken;
				button.Disabled = used = game.JokerIndex == null || fastOff || taken;
				hint = game.JokerIndex is not { } next ? Lang.T("Play it before a question starts.")
					: fastOff ? Lang.T("Only for buzzer questions: the next one isn't.")
					: taken ? Lang.T("Someone else already has the Fast Pass for this question.")
					: Lang.F("Plays on question {0}.", next + 1);
				lines.Add(hint);
			}
			button.CustomMinimumSize = new Vector2(58, 46);
			button.TooltipText = Lang.T(Joker.Label(k)) + "\n" + hint;
			button.AccessibilityName = Lang.T(Joker.Label(k));
			var icon = new JokerIcon(k, used);
			icon.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect, LayoutPresetMode.Minsize, 6);
			// (on the key's face: its side is the bottom few pixels)
			icon.OffsetBottom -= 3;
			button.AddChild(icon);
			row.AddChild(button);
		}
		var box = U.V(6, U.Eyebrow("Your jokers"), row);
		if (lines.Count > 0) box.AddChild(U.Muted(lines[0], 12));
		return box;
	}

	/// <summary>Flips this player's own view of a fully shown slideshow.</summary>
	void FlipOwn(int by)
	{
		if (_mySlide is not { } ms || _room.Game?.Question.SlideCount is not { } count) return;
		_mySlide = (ms.Index, Math.Clamp(ms.Slide + by, 0, count - 1));
		Refresh(_room);
	}

	/// <summary>Interactive: record the task, then listen along while the host plays everyone's, then vote.</summary>
	Control Interactive(RoomState room, InteractiveView ia)
	{
		var game = room.Game!;
		if (ia.Phase == InteractivePhase.Recording)
			return U.Panel(new RecorderPanel(_client, game.Index, ia.DoneIds.Contains(MyId)), "Card2");
		if (ia.Phase == InteractivePhase.Voting && ia.MyVote == null && ia.Clips.Any(c => c.PlayerId != MyId))
		{
			var error = U.Error();
			return U.V(8, InteractiveParts.VoteList(room, ia, MyId, _clips, n => Submit(game.Index, AnswerValue.Number(n), error)), error);
		}
		if (ia.Phase == InteractivePhase.Voting)
			return Status(U.Bold(ia.MyVote is { } v && v < ia.Clips.Count ? Lang.F("You voted for {0}", room.Player(ia.Clips[v].PlayerId).Name) : Lang.T("Voting"), 26),
				U.Muted("Waiting for the others to vote…", 16));
		if (ia.Clips.Count == 0) return Status(U.Muted("Nobody sent a recording.", 16));
		return Status(InteractiveParts.ClipCard(room, ia, 22),
			U.Muted(ia.Judging == Judging.Host ? "The host plays everyone's recording and picks a favourite." : "The host plays everyone's recording. Then everyone votes for the best.", 16));
	}

	static Control Status(params Control[] kids)
	{
		var box = U.V(8, kids);
		box.Alignment = BoxContainer.AlignmentMode.Center;
		foreach (var k in kids)
			if (k is Label l) l.HorizontalAlignment = HorizontalAlignment.Center;
		return U.Panel(box.MinSize(0, 180), "Card2");
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
				new Countdown(game.AnswerEndsAt, "Time to answer", 26, C.AccentInk).Center());
			panel.AddThemeStyleboxOverride("panel", UiTheme.Box(C.Accent, 14, null, 0, 24, 22));
			return panel;
		}
		if (mine != null)
		{
			if (mine.Status == BuzzStatus.Wrong)
				return Status(U.Text("Not quite.", 30, C.Text, Fonts.Black), U.Muted("The others get a go now.", 16), U.Muted(Lang.F("You buzzed after {0}.", U.FormatSeconds(mine.AtSec)), 13));
			var kids = new System.Collections.Generic.List<Control> { U.Text(Lang.Ordinal(place + 2), 56, C.Accent, Fonts.Black).Raw(), U.Bold("You're in line", 20) };
			if (answering != null) kids.Add(U.Muted(Lang.F("{0} is answering.", room.Player(answering.PlayerId).Name), 16));
			kids.Add(U.Muted(Lang.F("You buzzed after {0}.", U.FormatSeconds(mine.AtSec)), 13));
			return Status(kids.ToArray());
		}
		var area = U.V(14);
		area.Alignment = BoxContainer.AlignmentMode.Center;
		// (a video or picture plays on this screen too, above: then the buzzer is smaller to leave it room)
		var big = game.Question.Type is QuestionType.Video or QuestionType.Image;
		var watch = WatchHint(game.Question);
		if (watch != null) area.AddChild(U.Muted(watch, 16, false, HorizontalAlignment.Center).Center());
		if (answering != null) area.AddChild(U.Muted(Lang.F("{0} is answering — buzz to get in line.", room.Player(answering.PlayerId).Name), 14, false, HorizontalAlignment.Center).Center());
		var buzz = new BuzzButton(_client.Buzz, big ? 130 : 240) { Disabled = !game.BuzzersOpen || game.Paused };
		area.AddChild(buzz.Center());
		if (!game.BuzzersOpen) area.AddChild(U.Muted("Buzzers are locked — the host unlocks them in a moment.", 15, false, HorizontalAlignment.Center).Center());
		area.AddChild(U.Muted("or press Space", 13, false, HorizontalAlignment.Center).Center());
		return U.Pad(area, 0, 2, 0, 2);
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
		if (game.Question.WrongCosts && locked == null)
			list.AddChild(U.Text(Lang.F("Careful: a wrong answer costs {0}.", U.Plural(game.Question.Points, "point", "points")), 15, C.Coral, Fonts.Bold).Center());
		Button? lockIn = null;
		for (var i = 0; i < (game.Question.Choices?.Count ?? 0); i++)
		{
			if (game.Question.Choices![i] is not { } text) continue;
			var index = i;
			var b = new Button { MouseDefaultCursorShape = CursorShape.PointingHand, CustomMinimumSize = new Vector2(0, 72) };
			// (a key in the answer's own colour, like every button: KeyBox; a picked answer stays pushed in)
			void Style(bool picked)
			{
				var color = C.Choice[index];
				b.AddThemeStyleboxOverride("normal", new KeyBox(picked ? color.Darkened(0.06f) : color, 16, 14, 10, 3, 7, picked));
				b.AddThemeStyleboxOverride("hover", new KeyBox(color.Lightened(0.12f), 16, 14, 10, 3, 7, picked));
				b.AddThemeStyleboxOverride("pressed", new KeyBox(color.Darkened(0.06f), 16, 14, 10, 3, 7, true));
				b.AddThemeStyleboxOverride("hover_pressed", new KeyBox(color.Darkened(0.03f), 16, 14, 10, 3, 7, true));
				b.AddThemeStyleboxOverride("disabled", new KeyBox(color, 16, 14, 10, 3, 7, picked));
				b.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
				// (the text sits on the key's face: it moves down with it when pushed in)
				if (b.GetChildCount() > 0 && b.GetChild(0) is MarginContainer m) (m.OffsetTop, m.OffsetBottom) = picked ? (5, -3) : (0, -7);
			}
			Style(picks.Contains(i));
			b.Disabled = locked != null;
			if (locked != null && !picks.Contains(i)) b.Modulate = new Color(1, 1, 1, 0.4f);
			var row = GameParts.Row(14, GameParts.Letter(i, 44, true), U.Text(text, 20, GameParts.ChoiceInk, Fonts.Bold, true).Raw());
			row.MouseFilter = MouseFilterEnum.Ignore;
			foreach (var c in row.GetChildren().OfType<Control>()) c.MouseFilter = MouseFilterEnum.Ignore;
			b.AddChild(U.Pad(row, 14, 0, 14, 0).With(m =>
			{
				m.SetAnchorsPreset(LayoutPreset.FullRect);
				m.MouseFilter = MouseFilterEnum.Ignore;
			}));
			Style(picks.Contains(i));
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
		return U.V(12, U.Field(unit != "" ? Lang.F("Your guess ({0})", unit) : Lang.T("Your guess"), field), lockIn, error);
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
			if (q.WrongCosts) rows.AddChild(U.Text(Lang.T("Careful: every item in a wrong place costs points."), 15, C.Coral, Fonts.Bold));
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
		if (tl.Last != null && tl.Phase == TimelinePhase.Choosing)
			box.AddChild(U.Text(GameParts.TimelineEventText(room, tl.Last), 16, tl.Last.Kind == TimelineEventKind.Right ? C.Teal : C.Muted, null, true, HorizontalAlignment.Center).Raw());
		if (tl.Phase != TimelinePhase.Choosing)
		{
			// Waiting for the host: the locked-in spot, then the result.
			var mine = tl.TurnPlayerId == MyId;
			var info = new List<Control>();
			if (tl.Phase == TimelinePhase.Locked)
			{
				info.Add(U.Bold(mine ? Lang.T("Locked in!") : Lang.F("{0} locked in.", room.Player(tl.TurnPlayerId ?? "").Name), 24).Raw());
				info.Add(U.Muted(Lang.F("Is that where {0} goes? The host reveals it in a moment.", tl.Current ?? ""), 16, false, HorizontalAlignment.Center).Raw());
			}
			else if (tl.Last != null)
			{
				var right = tl.Last.Kind == TimelineEventKind.Right;
				info.Add(U.Text(mine && right ? Lang.F("Right! +{0}", game.Question.Points) : mine ? Lang.T("Not quite.") : GameParts.TimelineEventText(room, tl.Last), 26,
					right ? C.Teal : C.Coral, Fonts.Black, true, HorizontalAlignment.Center).Raw());
				info.Add(U.Muted("Waiting for the host to continue…", 14, false, HorizontalAlignment.Center));
			}
			box.AddChild(Status(info.ToArray()));
			box.AddChild(GameParts.TimelineRow(game.Question, tl, tl.PendingGap));
			return box;
		}
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
			U.Text(Lang.F("Where does {0} go? Drag it into the timeline, then lock in.", tl.Current), 17, C.AccentInk, null, true).Raw().Grow(),
			new Countdown(tl.TurnEndsAt, null, 24)));
		banner.AddThemeStyleboxOverride("panel", UiTheme.Box(C.Accent, 14, null, 0, 20, 14));
		box.AddChild(banner);
		var error = U.Error();
		var lockIn = U.Btn("Lock in", "TealButton", null, 20).Center();
		lockIn.Disabled = true;
		int? picked = null;
		// The tray above the timeline holds the item until it's dropped in a gap; it can be moved until you lock in.
		var tray = U.V(6).MinSize(0, 96);
		tray.Alignment = BoxContainer.AlignmentMode.Center;
		var rowSlot = U.V(0);
		void Build()
		{
			tray.Clear();
			if (picked == null)
			{
				tray.AddChild(new TimelineDragCard(tl.Current, false));
				tray.AddChild(U.Muted("Drag it into the timeline · or click a gap", 13, false, HorizontalAlignment.Center).Center());
			}
			else tray.AddChild(U.Muted(Lang.F("Not sure? Drag {0} to another gap, or lock in.", tl.Current), 15, false, HorizontalAlignment.Center).Raw().Center());
			rowSlot.Clear();
			rowSlot.AddChild(GameParts.TimelineRow(game.Question, tl, picked, gap =>
			{
				picked = gap;
				lockIn.Disabled = false;
				Build();
			}, tl.Current));
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
		box.AddChild(tray);
		box.AddChild(rowSlot);
		box.AddChild(lockIn);
		box.AddChild(error);
		return box;
	}

	/// <summary>Write the answer: type it and lock it in.</summary>
	/// <summary>Only when the sound or video couldn't come to this screen (e.g. a format only the host can play).</summary>
	static string? WatchHint(PublicQuestion q) =>
		q.MediaId != null ? null
		: q.Type == QuestionType.Sound ? "Listen to the host's screen"
		: q.Type == QuestionType.Video ? "Watch the host's screen"
		: null;

	Control Write(RoomState room)
	{
		var game = room.Game!;
		if (game.MyAnswer?.Text is { } mine)
			return Status(U.Muted("Locked in", 15), U.Text(mine, 32, C.Text, Fonts.Bold, true).Raw(), U.Muted("Waiting for the others…", 14));
		var error = U.Error();
		var watch = WatchHint(game.Question);
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
		var form = U.V(12, U.Field("Your answer", field), lockIn, error);
		if (watch != null) form.AddChild(U.Muted(watch, 14, false, HorizontalAlignment.Center).Center());
		return form;
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
			}, null, U.Flexible)
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
		// Like GeoGuessr: the screenshot big, the map in the corner (grows on hover).
		if (q.ImageKey != null)
		{
			var shot = new AssetImage(_client.Assets, q.ImageKey, U.Flexible) { Zoomable = true };
			box.AddChild(new GeoLayout(shot, map, lockIn));
			box.AddChild(error);
			return box;
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
		if (mine is { Doubled: true, Points: > 0 }) kids.Add(U.Text(Lang.T("×2 Double points!"), 18, C.Accent, Fonts.Black, false, HorizontalAlignment.Center));
		if (mine is { Bonus: true }) kids.Add(U.Text(Lang.F("★ Including the bonus: {0}", q.BonusText ?? ""), 16, C.Accent, Fonts.Bold, true, HorizontalAlignment.Center).Raw());
		kids.Add(U.Muted("Waiting for the host to continue…", 13));
		var panel = Status(kids.ToArray());
		if (mine is { Points: > 0 }) panel.AddThemeStyleboxOverride("panel", UiTheme.Box(new Color(C.Teal, 0.08f), 14, C.Teal, 1, 24, 22));
		var box = U.V(14, panel);
		// Everyone's answers, like on the host's screen (buzzer questions: who got it is said above).
		if (!q.Buzzer && q.Type != QuestionType.MapPin && reveal.Entries.Count > 0) box.AddChild(GameParts.RevealPanel(room, heading: false));
		// Image reveal: everyone gets the whole picture now.
		if (q.Type == QuestionType.Image && q.ImageKey != null) box.AddChild(new AssetImage(_client.Assets, q.ImageKey, U.Flexible) { Zoomable = true });
		if (q.Type == QuestionType.MapPin && q.Map != null)
		{
			// The answer on the left, the map with everyone's pins as big as the rest of the card.
			box.RemoveChild(panel);
			return U.H(16, U.V(0, panel).MinSize(280), GameMap.Make(q.Map, _client.Assets.Map(q.Map), reveal.Target, null, GameParts.RevealPins(room), U.Flexible).Grow());
		}
		return box;
	}
}

/// <summary>
/// The big buzzer: a big red key with rounded corners on a silver base plate, like the buttons (KeyBox), the logo's Q? embossed on it, drawn (so it stays
/// sharp at any size). It goes down when pressed and is grey while the buzzers are locked. Space works too.
/// </summary>
public partial class BuzzButton : Button
{
	readonly Action _onBuzz;
	bool _sent;
	float _press;
	readonly float _size;

	public BuzzButton() : this(() => { }) { }

	public BuzzButton(Action onBuzz, float size = 240)
	{
		_onBuzz = onBuzz;
		_size = size;
		// (the button is the hit area; everything is drawn in _Draw)
		CustomMinimumSize = new Vector2(size * 1.3f, size * 1.1f);
		AccessibilityName = Lang.T("Buzz in");
		AccessibilityDescription = Lang.T("Click or press Space to buzz in.");
		MouseDefaultCursorShape = CursorShape.PointingHand;
		foreach (var state in new[] { "normal", "hover", "pressed", "hover_pressed", "disabled", "focus" }) AddThemeStyleboxOverride(state, new StyleBoxEmpty());
		ButtonDown += () =>
		{
			Buzz();
			Squash();
		};
		MouseEntered += QueueRedraw;
		MouseExited += QueueRedraw;
	}

	/// <summary>Down and back up, with a little bounce.</summary>
	void Squash()
	{
		var t = CreateTween();
		t.TweenMethod(Callable.From<float>(v => { _press = v; QueueRedraw(); }), 0f, 1f, 0.06);
		t.TweenMethod(Callable.From<float>(v => { _press = v; QueueRedraw(); }), 1f, 0f, 0.35).SetTrans(Tween.TransitionType.Elastic).SetEase(Tween.EaseType.Out);
	}

	public override void _Draw()
	{
		var k = _size / 240f;
		var ink = C.Ink;
		var off = Disabled;
		var hover = !off && IsHovered();
		// (drawn like the buttons, KeyBox: every part has a lit face and a side in its own colour's shadow)
		var red = off ? C.Panel2.Lightened(0.15f) : hover ? new Color("#ff4a64") : new Color("#e8233f");
		var metal = off ? C.Panel2.Lightened(0.3f) : new Color("#d9dee6");
		var light = off ? 0.15f : 1f;
		var plate = 236 * k;
		var plateDepth = 16 * k;
		var origin = new Vector2((Size.X - plate) / 2, (Size.Y - plate - plateDepth) / 2);
		// (shadow on the table, then the silver base plate)
		DrawStyleBox(KeyBox.Fill(new Color(0, 0, 0, 0.3f), (int)(40 * k)), new Rect2(origin + new Vector2(-6, 14) * k, new Vector2(plate + 12 * k, plate + plateDepth)));
		DrawKey(new Rect2(origin, new Vector2(plate, plate + plateDepth)), metal, plateDepth, 36 * k, 5 * k, light);
		// (the sponsor engraved in the plate's front edge, under the key: cut into the metal, not in its colours, so a dark
		// edge on top and a lit one underneath, like light falling into the letters from above)
		var engraving = (int)(11 * k);
		if (engraving >= 6)
		{
			var brand = Fonts.Spaced;
			const string Sponsor = "TRACKGLOW";
			var width = brand.GetStringSize(Sponsor, HorizontalAlignment.Left, -1, engraving).X;
			var at = new Vector2(origin.X + (plate - width) / 2, origin.Y + plate - 5.5f * k);
			DrawString(brand, at + new Vector2(0, -0.9f * k), Sponsor, HorizontalAlignment.Left, -1, engraving, new Color(metal.Darkened(0.45f), 0.55f * light));
			DrawString(brand, at + new Vector2(0, 0.9f * k), Sponsor, HorizontalAlignment.Left, -1, engraving, new Color(metal.Lightened(0.6f), 0.7f * light));
			DrawString(brand, at, Sponsor, HorizontalAlignment.Left, -1, engraving, new Color(metal.Darkened(0.12f), light));
		}
		// (the darker hollow the key sits in)
		var well = new Rect2(origin + new Vector2(20, 18) * k, new Vector2(plate - 40 * k, plate - 36 * k));
		DrawStyleBox(KeyBox.Fill(ink, (int)(28 * k)), well);
		DrawStyleBox(KeyBox.Fill(metal.Darkened(0.55f), (int)(24 * k)), well.Grow(-4 * k));
		// (the red key: its side gets shorter as it's pressed, so the face goes down)
		var depth = (26 - 18 * _press) * k;
		var key = new Rect2(well.Position + new Vector2(10, 4 + 26 - depth / k) * k, new Vector2(well.Size.X - 20 * k, well.Size.Y - 14 * k - (26 * k - depth)));
		var face = DrawKey(key, red, depth, 26 * k, 6 * k, light);
		// (the brand embossed on the key instead of a label: the logo's Q and its orange ?, raised out of the face, lit from
		// above: a light edge on top, a shadow under)
		var font = Fonts.Black;
		var size = (int)(92 * k);
		var q = font.GetStringSize("Q", HorizontalAlignment.Left, -1, size).X;
		var mark = font.GetStringSize("?", HorizontalAlignment.Left, -1, size).X;
		var x = face.GetCenter().X - (q + mark) / 2;
		var y = face.GetCenter().Y + size * 0.3f;
		var question = off ? C.Muted : C.Accent;
		void Raised(string text, float at, Color color)
		{
			var pos = new Vector2(at, y);
			DrawString(font, pos + new Vector2(0, 4 * k), text, HorizontalAlignment.Left, -1, size, new Color(color.Darkened(0.55f), 0.9f));
			DrawString(font, pos + new Vector2(0, -2.5f * k), text, HorizontalAlignment.Left, -1, size, new Color(color.Lightened(0.55f), light * 0.9f));
			DrawString(font, pos, text, HorizontalAlignment.Left, -1, size, color);
		}
		Raised("Q", x, red.Lightened(0.12f));
		Raised("?", x + q, question);
	}

	/// <summary>A key like <see cref="KeyBox"/>, at this size: outline, side, face with a bevel and a shine. Returns the face.</summary>
	Rect2 DrawKey(Rect2 r, Color color, float depth, float radius, float outline, float light)
	{
		DrawStyleBox(KeyBox.Fill(C.Ink, (int)radius), r);
		var inner = r.Grow(-outline);
		var rad = (int)Mathf.Max(2, radius - outline);
		DrawStyleBox(KeyBox.Fill(color.Darkened(0.42f), rad), inner);
		var face = new Rect2(inner.Position, new Vector2(inner.Size.X, inner.Size.Y - depth));
		var faceBox = KeyBox.Fill(color, rad);
		faceBox.BorderWidthTop = (int)Mathf.Max(2, 4 * _size / 240f);
		faceBox.BorderColor = color.Lightened(0.45f);
		faceBox.BorderBlend = true;
		DrawStyleBox(faceBox, face);
		var inset = face.Size.X * 0.07f;
		DrawStyleBox(KeyBox.Fill(new Color(1, 1, 1, 0.2f * light), (int)Mathf.Max(2, rad - inset)),
			new Rect2(face.Position + new Vector2(inset, inset * 0.6f), new Vector2(face.Size.X - 2 * inset, face.Size.Y * 0.4f)));
		return face;
	}

	public override void _Notification(int what)
	{
		if (what == NotificationResized) QueueRedraw();
		if (what == NotificationReady && Disabled) AccessibilityName = Lang.T("Buzzers are locked");
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

/// <summary>A joker's icon on its key: the Fast Pass bolt, or a chunky ×2 for Double points (greyed when it can't be played).</summary>
public partial class JokerIcon : Control
{
	readonly string _kind;
	readonly bool _off;

	public JokerIcon() : this(Joker.Double, false) { }

	public JokerIcon(string kind, bool off)
	{
		_kind = kind;
		_off = off;
		MouseFilter = MouseFilterEnum.Ignore;
	}

	public override void _Draw()
	{
		var m = Mathf.Min(Size.X, Size.Y);
		var color = _off ? C.Muted : _kind == Joker.FastPass ? C.Gold : C.Pink;
		if (_kind == Joker.FastPass)
		{
			if (Bolt.Icon((int)m, color) is { } tex) DrawTextureRect(tex, new Rect2((Size - new Vector2(m, m)) / 2, new Vector2(m, m)), false);
			return;
		}
		var font = Fonts.Black;
		var size = (int)(m * 0.95f);
		const string text = "×2";
		var w = font.GetStringSize(text, HorizontalAlignment.Left, -1, size);
		var pos = new Vector2((Size.X - w.X) / 2, Size.Y / 2 + size * 0.36f);
		DrawStringOutline(font, pos, text, HorizontalAlignment.Left, -1, size, Mathf.Max(3, size / 7), C.Ink);
		DrawString(font, pos, text, HorizontalAlignment.Left, -1, size, color);
	}
}
