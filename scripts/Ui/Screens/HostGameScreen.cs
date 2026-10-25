using System;
using System.Linq;
using Godot;
using QuizNight.Net;
using QuizNight.Server;
using QuizNight.Shared;
using QuizNight.Storage;

namespace QuizNight.Ui.Screens;

/// <summary>The host's screen (on the TV or in the Discord stream): question, media, buzz order, controls, scores.</summary>
public partial class HostGameScreen : Control, IRoomScreen
{
	readonly HostSession _host;
	readonly HostAssets _assets;
	readonly Keyed _bar = new();
	readonly Keyed _head = new();
	readonly Keyed _media = new();
	readonly Keyed _content = new();
	readonly Keyed _controls = new();
	readonly VBoxContainer _side = U.V(10);
	HostMedia? _player;
	HostImageReveal? _imageReveal;
	/// <summary>Interactive questions: plays the recording when the host presses play (players' PCs play it too).</summary>
	readonly ClipPlayer _clips;
	readonly HintTracker _hints = new();

	public HostGameScreen() : this(null!, null!) { }

	public HostGameScreen(HostSession host, HostAssets assets)
	{
		_host = host;
		_assets = assets;
		_clips = new ClipPlayer(assets);
		AddChild(_clips);
		SetAnchorsPreset(LayoutPreset.FullRect);

		_content.AddThemeConstantOverride("separation", 14);
		// The card fills the column; pictures and maps in it take the height that's left.
		// (a question players can't see yet is shown to the host as it is: players see everything on their own
		// screens, so the host's screen needn't be streamed and they can read it out before showing it)
		var stageCard = U.Panel(U.V(14, _head, _media, _content).GrowV()).GrowV();
		var stage = U.V(12, stageCard, _controls).Grow(3).GrowV();
		var stageScroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled }.Grow(3).GrowV();
		stageScroll.AddChild(stage);
		var side = U.Panel(_side).MinSize(330).GrowV();
		var sideScroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled }.MinSize(340).GrowV();
		sideScroll.AddChild(side.Grow());
		var body = U.Pad(U.H(16, stageScroll, sideScroll), 16, 16, 16, 16).GrowV();
		var layout = U.V(0, _bar, body);
		layout.SetAnchorsPreset(LayoutPreset.FullRect);
		AddChild(layout);
	}

	// Buzzes already heard: a new one in the same question plays the buzzer sound.
	int _heardIndex = -1;
	int _heardBuzzes;

	public void Refresh(RoomState room)
	{
		var game = room.Game!;
		var q = game.Question;
		if (game.Index != _heardIndex)
		{
			_heardIndex = game.Index;
			_heardBuzzes = 0;
		}
		if (game.Buzzes.Count > _heardBuzzes) Sfx.PlayBuzzer(_host.BuzzerSound);
		_heardBuzzes = game.Buzzes.Count;
		var reveal = game.Stage == Stage.Reveal ? game.Reveal : null;
		HostNotes.Update(game);
		_game = game;
		_clips.Update(game);
		var ia = game.Interactive;
		var iaKey = ia == null ? "" : $"|{ia.Phase}|{ia.Clip}|{ia.Clips.Count}|{string.Join(",", ia.DoneIds)}";
		// (picture questions: the shown picture is part of it, so flipping redraws the picture)
		var key = $"{game.Index}|{game.Stage}|{q.Slide}|{q.Hints?.Count}";
		var flipHint = _hints.JustFlipped(game);
		var tl = game.Stage == Stage.Open ? game.Timeline : null;
		// Timeline: rebuilt on every move.
		var tlKey = tl == null ? "" : $"|{tl.Phase}|{tl.PendingGap}|{tl.Placed.Count}|{tl.TurnPlayerId}|{string.Join(",", tl.WrongGaps)}|{tl.Last?.Kind}{tl.Last?.PlayerId}{tl.Last?.Item}";

		_bar.Set($"{game.Index}|{game.JokerIndex}", () =>
		{
			var row = GameParts.Row(12,
				U.H(0, U.Text("Quiznight", 20, C.Text, Fonts.Black), U.Text("?", 20, C.Accent, Fonts.Black)),
				U.Pill(Lang.F("Question {0} / {1}", game.Index + 1, game.Total)),
				Chips.Question(q),
				U.Pill(GameParts.PointsPill(q)));
			if (GameParts.NextPill(game) is { } next) row.AddChild(next);
			row.AddChild(U.Fill());
			row.AddChild(new RoomCodeButton(room.Code));
			return U.Panel(row, "Bar");
		});

		_head.Set(key + ia?.Phase, () => GameParts.Row(16,
			U.Text(q.Prompt, 34, C.Text, Fonts.Black, true).Raw(),
			game.Stage == Stage.Open ? new Countdown(game.EndsAt, null, 34) : U.Gap(0)));

		// The player stays alive through the reveal so the music doesn't restart.
		// Sound, video and image reveals start once the question is shown.
		_media.Set($"{game.Index}|{game.Shown}", () =>
		{
			_player = null;
			_imageReveal = null;
			if (!game.Shown) return null;
			if (q.Type == QuestionType.Image)
				return _imageReveal = new HostImageReveal(_host, _host.Room.Quiz.Questions[game.Index], U.Flexible);
			if (!q.IsMedia) return null;
			var media = _host.Room.Quiz.Questions[game.Index].Media;
			_player = new HostMedia(_host, q, media, game.Index);
			return _player;
		});
		_player?.Update(game);
		_imageReveal?.Update(game);

		// (rebuilt when the host ticks an answer or gives a bonus)
		_content.Set((reveal != null ? $"{key}|{string.Join(",", reveal.Entries.Select(e => $"{e.PlayerId}:{e.Points}:{e.Bonus}"))}" : key + tlKey) + iaKey, () =>
		{
			var box = U.V(14);
			if (ia != null) Interactive(box, room, ia, reveal != null);
			if (reveal == null && GameParts.BonusLine(q, 18) is { } bonusLine) box.AddChild(bonusLine);
			if (q.ImageKey != null && q.Type != QuestionType.Image && !(q.Type == QuestionType.MapPin && reveal != null))
				box.AddChild(new AssetImage(_assets, q.ImageKey, U.Flexible) { Zoomable = true });
			if (q is { SlideCount: > 1, Slide: { } slide }) box.AddChild(SlideControls(slide, q.SlideCount.Value));
			if (q.HintCount != null)
			{
				box.AddChild(StepsParts.Cards(q, flipHint, 24));
				if (reveal == null) box.AddChild(StepsParts.Worth(q, 20));
			}
			if (q.Choices != null) box.AddChild(GameParts.ChoiceGrid(game));
			if (reveal == null && q.Mode == ClassicMode.Multi) box.AddChild(U.Muted("Several answers can be right — players pick all they think are.", 16));
			if (reveal == null && q.Type == QuestionType.Estimation)
				box.AddChild(U.Muted(string.IsNullOrEmpty(q.Unit) ? Lang.T("Everyone types a number on their own screen.") : Lang.F("Everyone types a number in {0} on their own screen.", q.Unit), 16));
			if (reveal == null && q.Type == QuestionType.MapPin) box.AddChild(U.Muted("Everyone places a pin on the map on their own screen.", 16));
			if (tl != null)
			{
				var current = tl.Current != null
					? GameParts.Row(14, U.Muted("Where does it go?", 17, false), GameParts.TimelineCard(tl.Current, "", false, true),
						U.Muted(tl.Left == 0 ? Lang.T("The last one!") : tl.Left == 1 ? Lang.T("1 more after this") : Lang.F("{0} more after this", tl.Left), 15, false))
					: U.Gap(0);
				if (current is BoxContainer currentRow) currentRow.Alignment = BoxContainer.AlignmentMode.Center;
				box.AddChild(current);
				box.AddChild(GameParts.TimelineRow(q, tl, tl.PendingGap));
				var status = GameParts.Row(10);
				status.Alignment = BoxContainer.AlignmentMode.Center;
				if (tl.Last != null)
				{
					var color = tl.Last.Kind == TimelineEventKind.Right ? C.Teal : tl.Last.Kind is TimelineEventKind.Wrong or TimelineEventKind.TimeUp ? C.Coral : C.Muted;
					status.AddChild(U.Text(GameParts.TimelineEventText(room, tl.Last), tl.Phase == TimelinePhase.Choosing ? 17 : 22, color, Fonts.Bold).Raw());
				}
				if (tl.Phase == TimelinePhase.Choosing && tl.TurnPlayerId is { } turn)
				{
					var p = room.Player(turn);
					status.AddChild(new Avatar(p, 28));
					status.AddChild(U.Bold(Lang.F("{0}'s turn", p.Name), 18).Raw());
					status.AddChild(new Countdown(tl.TurnEndsAt, null, 22));
				}
				box.AddChild(status);
			}
			else if (reveal == null && q.Type == QuestionType.Order)
			{
				box.AddChild(GameParts.OrderList(q));
				box.AddChild(U.Muted("Everyone puts these in order on their own screen.", 16));
			}
			if (reveal == null && q.Written) box.AddChild(U.Muted("Everyone types their answer on their own screen.", 16));
			if (q.Type == QuestionType.MapPin && reveal != null && q.Map != null)
			{
				// Map and results side by side, so everything fits on the TV.
				var map = GameMap.Make(q.Map, _assets.Map(q.Map), reveal.Target, null, GameParts.RevealPins(room), U.Flexible).Grow(1.6f);
				box.AddChild(U.H(20, map, GameParts.RevealPanel(room, null, _host.ToggleBonus).Grow()));
			}
			else if (reveal != null) box.AddChild(GameParts.RevealPanel(room, q.Written ? _host.ToggleWritten : null, _host.ToggleBonus));
			return box;
		});

		var answering = game.Buzzes.FirstOrDefault(b => b.Status == BuzzStatus.Answering);
		var last = game.Index + 1 >= game.Total;
		_controls.Set($"{key}{iaKey}|{answering?.PlayerId}|{answering?.AtSec}|{game.Buzzes.Count}|{game.Buzzes.Count(b => b.Status == BuzzStatus.Wrong)}|{game.Shown}|{game.BuzzersOpen}|{tl?.TurnPlayerId}|{tl?.Current}|{tl?.Phase}|{GameParts.PausedText(room)}", () =>
		{
			var row = GameParts.Row(10);
			row.Alignment = BoxContainer.AlignmentMode.End;
			// Someone left: everything waits until they're back, or the host goes on without them.
			if (GameParts.PausedText(room) is { } paused)
			{
				row.AddChild(U.Text(paused, 17, C.Accent, Fonts.Bold, true).Raw().Grow());
				row.AddChild(U.Btn("Continue without them", "AccentButton", _host.Continue, 18).Tip("Timers, buzzers and answers carry on; they can still come back later"));
				return row;
			}
			if (game.Stage == Stage.Open && q.Buzzer && answering != null)
			{
				var p = room.Player(answering.PlayerId);
				row.AddChild(GameParts.Row(8, new Avatar(p, 28), U.Bold(Lang.F("{0} is answering", p.Name), 17), new Countdown(game.AnswerEndsAt, null, 22)));
				row.AddChild(U.Fill());
				row.AddChild(U.Btn("Correct", "TealButton", () => _host.Judge(true), 20));
				row.AddChild(U.Btn("Wrong", "CoralButton", () => _host.Judge(false), 20));
			}
			if (game.Stage == Stage.Open && !game.Shown)
			{
				row.AddChild(U.Muted("Players don't see this question yet.", 15, false).Grow());
				row.AddChild(U.Btn("Host notes", "", HostNotes.Open).Tip("The question and answer in a separate window (not in your stream if you share only the game window)"));
				row.AddChild(U.Btn("Show question  (Space)", "AccentButton", _host.ShowQuestion, 20));
				return row;
			}
			if (game.Stage == Stage.Open && q.Buzzer && answering == null)
			{
				// (everyone who buzzed was wrong: they can all have another go, or the host reveals)
				if (game.Buzzes.Count > 0 && game.Buzzes.All(b => b.Status == BuzzStatus.Wrong))
					row.AddChild(U.Btn("Let everyone buzz again", "", _host.ReopenBuzzers).Tip("Clears the buzzes so far: everyone, also who was wrong, can buzz again"));
				// (step by step: flip the next hint; who was wrong can buzz again)
				if (q.HintCount is { } hintCount && (q.Hints?.Count ?? 0) < hintCount)
					row.AddChild(U.Btn("Next hint  (→)", "AccentButton", _host.NextHint, 20).Tip("Flips the next hint; everyone can buzz again"));
				if (game.BuzzersOpen) row.AddChild(U.Btn("Lock buzzers", "", () => _host.SetBuzzers(false)));
				else row.AddChild(U.Btn("Unlock buzzers  (Space)", "AccentButton", () => _host.SetBuzzers(true), 20));
			}
			if (tl is { Phase: TimelinePhase.Choosing, TurnPlayerId: { } turnId })
				row.AddChild(U.Btn(Lang.F("Skip {0}", room.Player(turnId).Name), "", _host.SkipTurn).Tip("The next player tries the same item"));
			if (tl is { Current: not null, Phase: not TimelinePhase.Locked })
				row.AddChild(U.Btn("Place it for free", "", _host.PlaceForFree).Tip("Puts the item in its place without points; the next player gets the next one"));
			if (game.Stage == Stage.Open && ia != null)
			{
				InteractiveControls(row, room, ia);
				return row;
			}
			if (game.Stage == Stage.Open)
			{
				// (not on a timeline: the order is right there on the cards as they are placed)
				if (game.HostAnswer != null && tl == null) row.AddChild(new PeekButton(game.HostAnswer));
				row.AddChild(U.Btn("Reveal answer", "", _host.Reveal).Tip(tl != null ? "Ends the timeline now and shows the right order" : null));
				// The timeline waits for the host: reveal the locked-in spot, then start the next turn.
				if (tl?.Phase == TimelinePhase.Locked) row.AddChild(U.Btn("Reveal  (Space)", "AccentButton", _host.RevealPlacement, 20));
				if (tl?.Phase == TimelinePhase.Result)
					row.AddChild(U.Btn(tl.Current == null && tl.Left == 0 ? "Show results  (Space)" : "Next turn  (Space)", "AccentButton", _host.NextTurn, 20));
			}
			else row.AddChild(U.Btn(last ? "Show final results" : "Next question", "AccentButton", _host.Next, 20));
			return row;
		});

		_side.Clear();
		GameParts.SideInfo(_side, room, null, _host.AdjustScore);
		_side.AddChild(U.Gap(6));
		_side.AddChild(U.Link("End quiz early", () => Dialogs.Confirm(this, "End the quiz now and show the final results?", "End quiz", _host.Finish), 14)
			.With(b => b.SizeFlagsHorizontal = SizeFlags.ShrinkBegin));
	}


	GameView? _game;

	/// <summary>Picture slideshows: ◀ "Picture 2 / 5" ▶ under the picture (the arrow keys do the same).</summary>
	Control SlideControls(int slide, int count)
	{
		var back = U.Btn("◀", "SmallButton", () => _host.SetSlide(slide - 1), 18).MinSize(56, 0).Tip("Previous picture (←)");
		back.Disabled = slide == 0;
		var next = U.Btn("▶", "SmallButton", () => _host.SetSlide(slide + 1), 18).MinSize(56, 0).Tip("Next picture (→)");
		next.Disabled = slide >= count - 1;
		var row = GameParts.Row(14, back, U.Text(Lang.F("Picture {0} / {1}", slide + 1, count), 17, C.Text, Fonts.Bold), next);
		row.Alignment = BoxContainer.AlignmentMode.Center;
		return row;
	}

	/// <summary>Interactive: who has sent a recording; then the recording the host is on, with ◀ ▶ and play.</summary>
	void Interactive(VBoxContainer box, RoomState room, InteractiveView ia, bool revealed)
	{
		if (ia.Phase == InteractivePhase.Recording && !revealed)
		{
			var active = room.Players.Where(p => p.Connected && !_game!.LateIds.Contains(p.Id)).ToList();
			box.AddChild(U.Muted("Everyone records themselves doing the task with their microphone, on their own PC.", 17));
			var done = GameParts.Row(8, U.Bold(Lang.F("{0} / {1} sent", ia.DoneIds.Count, active.Count), 18));
			foreach (var id in ia.DoneIds) done.AddChild(new Avatar(room.Player(id), 30));
			box.AddChild(done);
			return;
		}
		if (ia.Clips.Count == 0)
		{
			box.AddChild(U.Muted("Nobody sent a recording.", 17));
			return;
		}
		box.AddChild(U.Panel(U.V(12, InteractiveParts.ClipCard(room, ia), ClipControls(ia)), "Card2"));
		if (ia.Phase == InteractivePhase.Voting && !revealed)
			box.AddChild(U.Bold(Lang.F("{0} have voted", ia.DoneIds.Count), 18).Center());
	}

	Control ClipControls(InteractiveView ia)
	{
		var back = U.Btn("◀", "SmallButton", () => _host.SetClip(ia.Clip - 1), 18).MinSize(56, 0).Tip("Previous recording (←)");
		back.Disabled = ia.Clip == 0;
		var next = U.Btn("▶", "SmallButton", () => _host.SetClip(ia.Clip + 1), 18).MinSize(56, 0).Tip("Next recording (→)");
		next.Disabled = ia.Clip >= ia.Clips.Count - 1;
		var row = GameParts.Row(14, back, U.Btn("Play it  (Space)", "AccentButton", _host.PlayClip, 20).Tip("Plays it here and on every player's PC"), next);
		row.Alignment = BoxContainer.AlignmentMode.Center;
		return row;
	}

	void InteractiveControls(HBoxContainer row, RoomState room, InteractiveView ia)
	{
		if (ia.Phase == InteractivePhase.Recording)
		{
			row.AddChild(U.Btn("End recording now", "", _host.EndRecording).Tip("Whoever hasn't sent a recording yet is left out"));
			return;
		}
		if (ia.Phase == InteractivePhase.Voting)
		{
			row.AddChild(U.Btn("End voting", "AccentButton", _host.Reveal, 20).Tip("Shows the result with the votes so far"));
			return;
		}
		if (ia.Judging == Judging.Host && ia.Clips.Count > 0)
		{
			var clip = ia.Clips[Math.Clamp(ia.Clip, 0, ia.Clips.Count - 1)];
			var name = room.Player(clip.PlayerId).Name;
			row.AddChild(U.Btn(Lang.F("{0} is my favourite", name), "AccentButton", () => _host.PickFavourite(clip.PlayerId), 20)
				.Tip("They get the question's points"));
			row.AddChild(U.Btn("Reveal answer", "", _host.Reveal).Tip("No favourite: nobody scores"));
			return;
		}
		row.AddChild(U.Btn("Start voting", "AccentButton", _host.StartVoting, 20).Tip("Once you've played them all: everyone votes on their PC"));
	}

	/// <summary>Space: show the hidden question, then unlock the buzzers. ← →: flip a picture slideshow.</summary>
	public override void _UnhandledKeyInput(InputEvent e)
	{
		if (e is InputEventKey { Pressed: true, Keycode: Key.Left or Key.Right or Key.Space } k && _game?.Interactive is { Phase: not InteractivePhase.Recording } ia
			&& _game.Shown && ia.Clips.Count > 0)
		{
			if (k.Keycode == Key.Space) _host.PlayClip();
			else _host.SetClip(ia.Clip + (k.Keycode == Key.Right ? 1 : -1));
			GetViewport().SetInputAsHandled();
			return;
		}
		if (e is InputEventKey { Pressed: true, Echo: false, Keycode: Key.Right } && _game?.Question.HintCount != null)
		{
			_host.NextHint();
			GetViewport().SetInputAsHandled();
			return;
		}
		if (e is InputEventKey { Pressed: true, Keycode: Key.Left or Key.Right } arrow && _game?.Question is { SlideCount: > 1, Slide: { } slide })
		{
			_host.SetSlide(slide + (arrow.Keycode == Key.Right ? 1 : -1));
			GetViewport().SetInputAsHandled();
			return;
		}
		if (e is not InputEventKey { Pressed: true, Echo: false, Keycode: Key.Space } || _game is not { Stage: Stage.Open, Paused: false } game) return;
		if (!game.Shown) _host.ShowQuestion();
		else if (game.Timeline?.Phase == TimelinePhase.Locked) _host.RevealPlacement();
		else if (game.Timeline?.Phase == TimelinePhase.Result) _host.NextTurn();
		else if (game.Question.Buzzer && !game.BuzzersOpen && game.Buzzes.All(b => b.Status != BuzzStatus.Answering)) _host.SetBuzzers(true);
		else return;
		GetViewport().SetInputAsHandled();
	}

	public override void _ExitTree() => HostNotes.Close();
}

/// <summary>Hold to show the answer, so it doesn't sit on a shared screen.</summary>
public partial class PeekButton : Button
{
	readonly string _answer;
	const string HiddenText = "Hold to peek at the answer";

	public PeekButton() : this("") { }

	public PeekButton(string answer)
	{
		_answer = answer;
		Text = HiddenText;
		MouseDefaultCursorShape = CursorShape.PointingHand;
		// (the answer shows in a box above the button: a long one would stretch the whole row)
		ButtonDown += Show;
		ButtonUp += Hide;
		MouseExited += Hide;
	}

	PanelContainer? _box;

	void Show()
	{
		Hide();
		var label = U.Text(_answer, 17, C.Text, Fonts.Bold, true).Raw();
		label.CustomMinimumSize = new Vector2(Mathf.Min(560, _answer.Length * 9 + 20), 0);
		_box = U.Panel(label, "Clear");
		_box.AddThemeStyleboxOverride("panel", UiTheme.Box(C.Panel2, 10, C.Accent, 2, 14, 10));
		_box.TopLevel = true;
		_box.MouseFilter = MouseFilterEnum.Ignore;
		AddChild(_box);
		Callable.From(() =>
		{
			if (_box == null || !IsInstanceValid(_box)) return;
			var at = GlobalPosition + new Vector2(0, -_box.Size.Y - 8);
			// (kept inside the window)
			at.X = Mathf.Clamp(at.X, 8, GetViewportRect().Size.X - _box.Size.X - 8);
			_box.GlobalPosition = at;
		}).CallDeferred();
	}

	void Hide()
	{
		_box?.QueueFree();
		_box = null;
	}
}

/// <summary>
/// Plays the question's sound or video on the host screen. Tells the host session when it plays or
/// pauses (that clock measures how long it took to guess), pauses while someone is answering after
/// a buzz; after a wrong answer (and nobody else in line) it stays paused until the host resumes it or reveals.
/// </summary>
public partial class HostMedia : VBoxContainer
{
	readonly HostSession _host;
	readonly PublicQuestion _q;
	readonly AudioStreamPlayer? _audio;
	readonly VideoStreamPlayer? _video;
	readonly Button? _toggle;
	readonly Label? _time;
	readonly SoundBars? _bars;
	readonly double? _duration;
	bool _reported;
	bool _autoPaused;
	bool _answering;
	bool _paused;
	bool _open = true;
	bool _started;
	double _readyCheck;
	/// <summary>Before the start: who still downloads the file (it plays on the players' screens too).</summary>
	readonly Label _ready = U.Muted("", 14);
	readonly Label _hint = U.Muted("Nobody's answering now: resume it, or reveal the answer if nobody knows it.", 14).With(l => l.Visible = false);

	public HostMedia() : this(null!, new PublicQuestion(), null, 0) { }

	public HostMedia(HostSession host, PublicQuestion q, MediaAsset? media, int index)
	{
		_host = host;
		_q = q;
		_duration = media?.DurationSec;
		AddThemeConstantOverride("separation", 10);
		if (media == null || media.DataUrl == "")
		{
			AddChild(U.Error(q.Type == QuestionType.Sound ? "This question has no sound file." : "This question has no video."));
			return;
		}
		if (q.Type == QuestionType.Sound)
		{
			var stream = Media.LoadSound(media);
			if (stream == null)
			{
				AddChild(U.Error("Godot can't play this sound format. Replace it with an MP3, OGG or WAV file in the editor."));
				return;
			}
			_audio = new AudioStreamPlayer { Stream = stream, Bus = Prefs.MusicBus };
			AddChild(_audio);
			_bars = new SoundBars();
			_toggle = U.Btn("▶", "AccentButton", Toggle, 30).MinSize(84, 84);
			_time = U.Mono("0:00", 18, C.Muted);
			AddChild(U.Panel(GameParts.Row(18, _toggle, _bars.Grow(), _time), "Inset"));
			AddChild(_hint);
			AddChild(_ready);
		}
		else
		{
			var stream = Media.LoadVideo(media, $"q{index}");
			if (stream == null)
			{
				AddChild(U.Error(Media.VideoHelp));
				return;
			}
			_video = new VideoStreamPlayer { Stream = stream, Bus = Prefs.MusicBus };
			var frame = new VideoFrame(_video) { CustomMinimumSize = new Vector2(0, 200) }.MarkFlexible();
			AddChild(U.Panel(frame, "Clear").With(p => p.AddThemeStyleboxOverride("panel", UiTheme.Box(Colors.Black, 12, null, 0, 0, 0))));
			_toggle = U.Btn("▶ Start video", "AccentButton", Toggle, 17);
			_time = U.Mono("0:00", 15, C.Muted);
			AddChild(GameParts.Row(10, _toggle, _time, _hint, _ready));
		}
		// (no autoplay: the host starts it once everyone has the file; until then players see it blurred)
	}

	/// <summary>Like pressing Start (the autopilot uses it).</summary>
	public void StartNow()
	{
		if (!_started) Toggle();
	}

	void Start()
	{
		_started = true;
		var from = (float)(_q.StartSec ?? 0);
		if (_audio != null) _audio.Play(from);
		if (_video != null)
		{
			_video.Play();
			if (from > 0) _video.StreamPosition = from;
		}
	}

	bool Playing => _audio != null ? _audio.Playing && !_audio.StreamPaused : _video != null && _video.IsPlaying() && !_video.Paused;

	void SetPaused(bool paused)
	{
		if (!_started)
		{
			if (!paused) Start();
			return;
		}
		if (_audio != null)
		{
			if (!_audio.Playing && !paused) _audio.Play((float)(_q.StartSec ?? 0));
			else _audio.StreamPaused = paused;
		}
		if (_video != null)
		{
			if (!_video.IsPlaying() && !paused) _video.Play();
			else _video.Paused = paused;
		}
	}

	void Toggle()
	{
		_autoPaused = false;
		SetPaused(Playing);
	}

	/// <summary>
	/// Follows the game: pauses when someone buzzes. It doesn't carry on by itself after a wrong answer: the
	/// host resumes it (or reveals the answer if nobody knows it).
	/// </summary>
	public void Update(GameView game)
	{
		_open = game.Stage == Stage.Open;
		_answering = _open && game.Buzzes.Any(b => b.Status == BuzzStatus.Answering);
		// (someone left: it waits for them, the host resumes it once the game goes on)
		_paused = game.Paused;
		if ((_answering || _paused) && Playing)
		{
			_autoPaused = true;
			SetPaused(true);
		}
	}

	public override void _Process(double delta)
	{
		var playing = Playing;
		if (playing != _reported)
		{
			_reported = playing;
			if (_open) _host.SetMedia(playing);
		}
		var waiting = _autoPaused && !playing && !_answering && !_paused && _open;
		_hint.Visible = waiting;
		if (_toggle != null)
		{
			_toggle.Disabled = _paused;
			_toggle.Text = _audio != null ? (playing ? "❚❚" : "▶") : playing ? Lang.T("Pause") : !_started ? Lang.T("▶ Start video") : waiting ? Lang.T("▶ Resume") : Lang.T("Play");
			_toggle.ThemeTypeVariation = _started && _video != null ? "SmallButton" : "AccentButton";
		}
		_readyCheck -= delta;
		if (_readyCheck <= 0)
		{
			_readyCheck = 0.25;
			UpdateReady();
		}
		if (_bars != null) _bars.Active = playing;
		var pos = _audio != null ? _audio.GetPlaybackPosition() : _video?.StreamPosition ?? 0;
		if (_time != null) _time.Text = U.Clock(pos) + (_duration is { } d ? $" / {U.Clock(d)}" : "");
	}

	/// <summary>Before the start: "Everyone has it" or who is still downloading, and how far.</summary>
	void UpdateReady()
	{
		_ready.Visible = !_started && _open && _q.MediaId != null;
		if (!_ready.Visible) return;
		var missing = _host.Room.Players.Where(p => p.Connected)
			.Select(p => (p.Name, Percent: _host.MediaPercent(p.Id, _q.MediaId!)))
			.Where(p => p.Percent < 100).ToList();
		_ready.Text = missing.Count == 0
			? Lang.T("Everyone has it: start it when you're ready.")
			: Lang.F("Still downloading: {0}", string.Join(", ", missing.Select(m => $"{m.Name} {m.Percent}%")));
		_ready.AddThemeColorOverride("font_color", missing.Count == 0 ? C.Teal : C.Muted);
	}

	public override void _ExitTree()
	{
		_audio?.Stop();
		_video?.Stop();
	}
}

/// <summary>Bouncing bars while the sound plays.</summary>
public partial class SoundBars : Control
{
	public bool Active;
	double _t;

	public SoundBars() => CustomMinimumSize = new Vector2(0, 64);

	public override void _Process(double delta)
	{
		if (Active) _t += delta;
		QueueRedraw();
	}

	public override void _Draw()
	{
		const int n = 28;
		var w = Size.X / n;
		for (var i = 0; i < n; i++)
		{
			var phase = (i * 137 % 900) / 900.0 * Mathf.Tau;
			var h = Active ? (float)(0.25 + 0.75 * Mathf.Abs(Mathf.Sin(_t * 3.2 + phase))) : 0.15f;
			var bh = h * Size.Y;
			DrawRect(new Rect2(i * w + w * 0.2f, (Size.Y - bh) / 2, w * 0.6f, bh), new Color(C.Coral, Active ? 0.9f : 0.35f));
		}
	}
}
