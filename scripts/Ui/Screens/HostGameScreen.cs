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
	readonly Curtain _curtain = new();

	public HostGameScreen() : this(null!, null!) { }

	public HostGameScreen(HostSession host, HostAssets assets)
	{
		_host = host;
		_assets = assets;
		SetAnchorsPreset(LayoutPreset.FullRect);

		_content.AddThemeConstantOverride("separation", 14);
		// The card fills the column; pictures and maps in it take the height that's left.
		var stageCard = U.Panel(U.V(14, _head, _media, _content).GrowV()).GrowV();
		// The curtain covers the question until the host shows it.
		var stageStack = new MarginContainer().GrowV();
		stageStack.AddChild(stageCard);
		stageStack.AddChild(_curtain);
		var stage = U.V(12, stageStack, _controls).Grow(3).GrowV();
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
		_curtain.Visible = game.Stage == Stage.Open && !game.Shown;
		_curtain.Set(Lang.F("Question {0} / {1}", game.Index + 1, game.Total), Chips.TypeLabel(q));
		HostNotes.Update(game);
		_game = game;
		var key = $"{game.Index}|{game.Stage}";
		var tl = game.Stage == Stage.Open ? game.Timeline : null;
		// Timeline: rebuilt on every move.
		var tlKey = tl == null ? "" : $"|{tl.Phase}|{tl.PendingGap}|{tl.Placed.Count}|{tl.TurnPlayerId}|{string.Join(",", tl.WrongGaps)}|{tl.Last?.Kind}{tl.Last?.PlayerId}{tl.Last?.Item}";

		_bar.Set($"{game.Index}", () => U.Panel(GameParts.Row(12,
			U.H(0, U.Text("Quiznight", 20, C.Text, Fonts.Black), U.Text("!", 20, C.Accent, Fonts.Black)),
			U.Pill(Lang.F("Question {0} / {1}", game.Index + 1, game.Total)),
			Chips.Question(q),
			U.Pill(q.Points == 1 ? Lang.T("1 point") : Lang.F("{0} points", q.Points)),
			U.Fill(),
			U.Muted("Room", 15, false),
			U.Text(room.Code, 20, C.Accent, Fonts.Mono)), "Bar"));

		_head.Set(key, () => GameParts.Row(16,
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
		_content.Set(reveal != null ? $"{key}|{string.Join(",", reveal.Entries.Select(e => $"{e.PlayerId}:{e.Points}:{e.Bonus}"))}" : key + tlKey, () =>
		{
			var box = U.V(14);
			if (reveal == null && GameParts.BonusLine(q, 18) is { } bonusLine) box.AddChild(bonusLine);
			if (q.ImageKey != null && q.Type != QuestionType.Image && !(q.Type == QuestionType.MapPin && reveal != null))
				box.AddChild(new AssetImage(_assets, q.ImageKey, U.Flexible));
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
		_controls.Set($"{key}|{answering?.PlayerId}|{answering?.AtSec}|{game.Shown}|{game.BuzzersOpen}|{tl?.TurnPlayerId}|{tl?.Current}|{tl?.Phase}", () =>
		{
			var row = GameParts.Row(10);
			row.Alignment = BoxContainer.AlignmentMode.End;
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
				row.AddChild(U.Btn("Host notes", "", HostNotes.Open).Tip("The question and answer in a separate window (not in your stream if you share only the game window)"));
				row.AddChild(U.Btn("Show question  (Space)", "AccentButton", _host.ShowQuestion, 20));
				return row;
			}
			if (game.Stage == Stage.Open && q.Buzzer && answering == null)
			{
				if (game.BuzzersOpen) row.AddChild(U.Btn("Lock buzzers", "", () => _host.SetBuzzers(false)));
				else row.AddChild(U.Btn("Unlock buzzers  (Space)", "AccentButton", () => _host.SetBuzzers(true), 20));
			}
			if (tl is { Phase: TimelinePhase.Choosing, TurnPlayerId: { } turnId })
				row.AddChild(U.Btn(Lang.F("Skip {0}", room.Player(turnId).Name), "", _host.SkipTurn).Tip("The next player tries the same item"));
			if (tl is { Current: not null, Phase: not TimelinePhase.Locked })
				row.AddChild(U.Btn("Place it for free", "", _host.PlaceForFree).Tip("Puts the item in its place without points; the next player gets the next one"));
			if (game.Stage == Stage.Open)
			{
				if (game.HostAnswer != null) row.AddChild(new PeekButton(game.HostAnswer));
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

	/// <summary>Space: show the hidden question, then unlock the buzzers.</summary>
	public override void _UnhandledKeyInput(InputEvent e)
	{
		if (e is not InputEventKey { Pressed: true, Echo: false, Keycode: Key.Space } || _game is not { Stage: Stage.Open } game) return;
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
		ThemeTypeVariation = "SmallButton";
		MouseDefaultCursorShape = CursorShape.PointingHand;
		ButtonDown += () => Text = _answer;
		ButtonUp += () => Text = HiddenText;
		MouseExited += () => Text = HiddenText;
	}
}

/// <summary>
/// Plays the question's sound or video on the host screen. Tells the host session when it plays or
/// pauses (that clock measures how long it took to guess), pauses while someone is answering after
/// a buzz and carries on if they were wrong.
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
	bool _open = true;

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
		}
		else
		{
			var stream = Media.LoadVideo(media, $"q{index}");
			if (stream == null)
			{
				AddChild(U.Error(Media.VideoHelp));
				return;
			}
			_video = new VideoStreamPlayer { Stream = stream, Bus = Prefs.MusicBus, Expand = true, CustomMinimumSize = new Vector2(0, 200) }.MarkFlexible();
			AddChild(U.Panel(_video, "Clear").With(p => p.AddThemeStyleboxOverride("panel", UiTheme.Box(Colors.Black, 12, null, 0, 0, 0))));
			_toggle = U.Small("Pause", Toggle);
			_time = U.Mono("0:00", 15, C.Muted);
			AddChild(GameParts.Row(10, _toggle, _time));
		}
		// Autoplay from the start point.
		CallDeferred(MethodName.Start);
	}

	void Start()
	{
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

	/// <summary>Follows the game: pause while someone answers, stop at the reveal.</summary>
	public void Update(GameView game)
	{
		_open = game.Stage == Stage.Open;
		var answering = _open && game.Buzzes.Any(b => b.Status == BuzzStatus.Answering);
		if (answering && Playing)
		{
			_autoPaused = true;
			SetPaused(true);
		}
		else if (!answering && _autoPaused && _open)
		{
			_autoPaused = false;
			SetPaused(false);
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
		if (_toggle != null) _toggle.Text = _audio != null ? (playing ? "❚❚" : "▶") : (playing ? "Pause" : "Play");
		if (_bars != null) _bars.Active = playing;
		var pos = _audio != null ? _audio.GetPlaybackPosition() : _video?.StreamPosition ?? 0;
		if (_time != null) _time.Text = U.Clock(pos) + (_duration is { } d ? $" / {U.Clock(d)}" : "");
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

/// <summary>
/// Frosted glass over the hidden question: what's behind it shows only as a blur, with the question
/// number and type on top.
/// </summary>
public partial class Curtain : PanelContainer
{
	static readonly Shader Frost = new()
	{
		Code = """
			shader_type canvas_item;
			uniform sampler2D screen : hint_screen_texture, filter_linear;
			void fragment() {
				// A wide box blur of what's behind, darkened.
				vec3 sum = vec3(0.0);
				for (int x = -4; x <= 4; x++)
					for (int y = -4; y <= 4; y++)
						sum += texture(screen, SCREEN_UV + vec2(float(x), float(y)) * SCREEN_PIXEL_SIZE * 7.0).rgb;
				COLOR = vec4(sum / 81.0 * 0.55, 1.0);
			}
			""",
	};

	readonly Label _title = U.Text("", 30, C.Text, Fonts.Black);
	readonly Label _type = U.Muted("", 18, false);

	public Curtain()
	{
		Material = new ShaderMaterial { Shader = Frost };
		AddThemeStyleboxOverride("panel", UiTheme.Box(Colors.White, 16, C.Border, 1, 0, 0));
		var box = U.V(10, _title.Center(), _type.Center(), U.Muted("Hidden from everyone. Read it in Host notes, then press Show question.", 15, false, HorizontalAlignment.Center).Center());
		box.Alignment = BoxContainer.AlignmentMode.Center;
		// (the text is drawn normally, not frosted)
		foreach (var c in box.GetChildren().OfType<CanvasItem>()) c.UseParentMaterial = false;
		AddChild(box);
		MouseFilter = MouseFilterEnum.Stop;
	}

	public void Set(string title, string type)
	{
		_title.Text = title;
		_type.Text = Lang.T(type);
	}
}
