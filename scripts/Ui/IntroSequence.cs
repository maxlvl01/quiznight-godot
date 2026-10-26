// The intro when the game starts, like a console game's. Before it, while the game loads, the Pinke Geilheit
// Neural-Level Anti-Cheat shows in a small window of its own (AntiCheatSplash; the boot splash is its first frame). Then in
// the game's window a few cards one after the other: the Xbox "World Premiere" video, Made with Godot (on black),
// maxlvl01 presents, Pinke Geilheit Publishing (its logo is an animated GIF's frames, assets/intro/publisher.webp), music
// by Epidemic Sound, sponsored by TrackGlow, the credits wall, then the title screen (TitleScreen) with the animated logo.
// A key or a click skips the card showing (Escape skips them all); on the title screen it starts: the voice line plays,
// the logo glides up into its place on the home screen and the home screen fades in around it. Dev and test runs (any
// arguments after --) and "less motion" don't show any of it.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;
using QuizNight.Shared;
using QuizNight.Ui.Screens;

namespace QuizNight.Ui;

public partial class IntroSequence : Control
{
	/// <summary>The first cards' background (the video and Godot's).</summary>
	public static readonly Color Black = new("#000000");

	record Card(Func<Control> Build, double Hold, Color Background);

	readonly List<Card> _cards;
	readonly ColorRect _bg;
	readonly Action? _started;
	int _index = -1;
	Control? _shown;
	double _t;
	/// <summary>Where the card showing is: fading in, held, fading out.</summary>
	enum Phase { In, Hold, Out }
	Phase _phase;
	TitleScreen? _title;
	bool _leaving;

	const double Fade = 0.45;

	public IntroSequence() : this(null) { }

	/// <param name="started">Called when the title screen is left (the voice line plays then).</param>
	public IntroSequence(Action? started)
	{
		_started = started;
		SetAnchorsPreset(LayoutPreset.FullRect);
		MouseFilter = MouseFilterEnum.Stop;
		_bg = new ColorRect { Color = Black, MouseFilter = MouseFilterEnum.Ignore };
		_bg.SetAnchorsPreset(LayoutPreset.FullRect);
		AddChild(_bg);
		_cards = new()
		{
			new(VideoCard, VideoSec, Black),
			new(GodotCard, 2.0, Black),
			new(PresentsCard, 2.2, SplashBg),
			new(PublisherCard, PublisherFrames / PublisherFps, SplashBg),
			new(EpidemicCard, 2.2, Black),
			new(SponsorCard, 2.4, SplashBg),
			new(() => new CreditsWall(), 3.0, SplashBg),
		};
	}

	static Color SplashBg => C.Bg;

	/// <summary>An intro card or the title screen is showing: no music (it starts after the voice line, once started).</summary>
	public static bool HoldsMusic { get; private set; }

	/// <summary>Dev (--intro-from N): the card to start at.</summary>
	public int StartAt;

	public override void _Ready()
	{
		HoldsMusic = true;
		_index = StartAt - 1;
		Next();
	}

	public override void _ExitTree() => HoldsMusic = false;

	void Next()
	{
		_shown?.QueueFree();
		_shown = null;
		_index++;
		_t = 0;
		if (_index >= _cards.Count)
		{
			ShowTitle();
			return;
		}
		var card = _cards[_index];
		_bg.Color = card.Background;
		_shown = Centered(card.Build());
		_shown.Modulate = new Color(1, 1, 1, 0);
		_phase = Phase.In;
		AddChild(_shown);
	}

	static Control Centered(Control content)
	{
		var center = new CenterContainer { MouseFilter = MouseFilterEnum.Ignore };
		center.SetAnchorsPreset(LayoutPreset.FullRect);
		center.AddChild(content);
		return center;
	}

	public override void _Process(double delta)
	{
		if (_shown == null || _index >= _cards.Count) return;
		_t += delta;
		var card = _cards[_index];
		switch (_phase)
		{
			case Phase.In:
				_shown.Modulate = new Color(1, 1, 1, (float)Mathf.Min(1, _t / Fade));
				if (_t >= Fade) (_phase, _t) = (Phase.Hold, 0);
				break;
			case Phase.Hold:
				if (_t >= card.Hold) (_phase, _t) = (Phase.Out, 0);
				break;
			case Phase.Out:
				_shown.Modulate = new Color(1, 1, 1, (float)Mathf.Max(0, 1 - _t / Fade));
				// (the background turns into the next card's along with the fade)
				if (_index + 1 < _cards.Count) _bg.Color = card.Background.Lerp(_cards[_index + 1].Background, (float)Mathf.Min(1, _t / Fade));
				else _bg.Color = card.Background.Lerp(SplashBg, (float)Mathf.Min(1, _t / Fade));
				if (_t >= Fade) Next();
				break;
		}
	}

	/// <summary>A key, a click or a controller button: skips the card (Escape: all of them), or starts on the title screen.</summary>
	public override void _Input(InputEvent e)
	{
		var pressed = e is InputEventKey { Pressed: true, Echo: false } or InputEventMouseButton { Pressed: true } or InputEventJoypadButton { Pressed: true };
		if (!pressed || _leaving) return;
		// (the title screen's sponsor button takes its own clicks)
		if (_title != null && e is InputEventMouseButton click && _title.Sponsor.GetGlobalRect().HasPoint(click.Position)) return;
		GetViewport().SetInputAsHandled();
		if (_title != null)
		{
			Start();
			return;
		}
		if (e is InputEventKey { Keycode: Key.Escape })
		{
			_index = _cards.Count - 1;
			_phase = Phase.Out;
			_t = Fade;
			return;
		}
		if (_phase != Phase.Out)
		{
			// (fading out from where it is: a card half faded in doesn't flash back to full first)
			_t = _phase == Phase.In ? (1 - _t / Fade) * Fade : 0;
			_phase = Phase.Out;
		}
	}

	void ShowTitle()
	{
		_bg.Color = SplashBg;
		_title = new TitleScreen();
		AddChild(_title);
	}

	/// <summary>Leaves the title screen: the logo glides into the home screen's, which fades in around it.</summary>
	void Start()
	{
		_leaving = true;
		_title!.Leaving = true;
		HoldsMusic = false;
		_started?.Invoke();
		MouseFilter = MouseFilterEnum.Ignore;
		var home = App.I.CurrentScreen as HomeScreen;
		var logo = _title!.Logo;
		var t = CreateTween().SetParallel();
		const double glide = 0.9;
		if (home != null)
		{
			var target = home.Logo.GetGlobalRect();
			var from = logo.GetGlobalRect();
			var scale = target.Size.X / Mathf.Max(1, from.Size.X);
			home.Logo.Modulate = new Color(1, 1, 1, 0);
			home.Modulate = new Color(1, 1, 1, 0);
			// (the logo leaves its centring box: it's moved by hand from here)
			logo.TopLevel = true;
			logo.GlobalPosition = from.Position;
			t.TweenProperty(logo, "global_position", target.Position, glide).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.InOut);
			t.TweenProperty(logo, "scale", new Vector2(scale, scale), glide).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.InOut);
			t.TweenProperty(home, "modulate:a", 1f, glide * 0.7).SetDelay(glide * 0.35);
		}
		t.TweenProperty(_title.Prompt, "modulate:a", 0f, 0.2);
		t.TweenProperty(_title.Glow, "modulate:a", 0f, glide * 0.6);
		t.TweenProperty(_bg, "modulate:a", 0f, glide * 0.7).SetDelay(glide * 0.3);
		t.Chain().TweenCallback(Callable.From(() =>
		{
			if (home != null && IsInstanceValid(home)) home.Logo.Modulate = Colors.White;
			QueueFree();
		}));
	}

	// ---------- the cards ----------

	static Texture2D? Picture(string name) => ResourceLoader.Exists($"res://assets/intro/{name}") ? GD.Load<Texture2D>($"res://assets/intro/{name}") : null;

	/// <summary>A picture from assets/intro at this width, as tall as its shape says.</summary>
	static TextureRect Image(string name, float width)
	{
		var picture = Picture(name);
		var aspect = picture == null ? 1f : picture.GetHeight() / (float)picture.GetWidth();
		return new TextureRect
		{
			Texture = picture, ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
			CustomMinimumSize = new Vector2(width, width * aspect), MouseFilter = MouseFilterEnum.Ignore, SizeFlagsHorizontal = SizeFlags.ShrinkCenter,
			TextureFilter = TextureFilterEnum.LinearWithMipmaps,
		};
	}

	static Label Spaced(string text, int size, Color color) => U.Text(text, size, color, Fonts.Spaced, false, HorizontalAlignment.Center).Raw();

	const double VideoSec = 6.3;

	/// <summary>The Xbox "World Premiere" sting (assets/intro/xbox.ogv, from black to black), with its sound.</summary>
	static Control VideoCard()
	{
		var player = new VideoStreamPlayer
		{
			Stream = ResourceLoader.Exists("res://assets/intro/xbox.ogv") ? GD.Load<VideoStream>("res://assets/intro/xbox.ogv") : null,
			Expand = true, Autoplay = true, Bus = Storage.Prefs.SfxBus, CustomMinimumSize = new Vector2(1280, 720), MouseFilter = MouseFilterEnum.Ignore,
		};
		return player;
	}

	/// <summary>Music by Epidemic Sound: their wordmark (assets/intro/epidemic-sound.png, white) on black.</summary>
	/// <summary>TrackGlow, the sponsor (IBassGaming's Patreon; the title screen has a button to it).</summary>
	public const string SponsorUrl = "https://www.patreon.com/cw/IBassGaming";

	static Control SponsorCard() => U.V(22, Spaced("SPONSORED BY", 16, C.Muted), new TrackGlowLogo(1.6f),
		U.Text("patreon.com/cw/IBassGaming", 16, C.Muted, null, false, HorizontalAlignment.Center).Raw());

	static Control EpidemicCard() => U.V(28, Spaced("MUSIC BY", 16, new Color(1, 1, 1, 0.55f)), Image("epidemic-sound.png", 520),
		U.Text(Lang.T("Music and sound effects licensed from Epidemic Sound"), 15, new Color(1, 1, 1, 0.55f), null, false, HorizontalAlignment.Center));

	/// <summary>Made with Godot: the engine's own splash logo (white, for a dark background; CC BY 4.0, Andrea Calabró).</summary>
	static Control GodotCard() => U.V(26, Spaced("MADE WITH", 16, new Color(1, 1, 1, 0.55f)), Image("godot.png", 300));

	static Control PresentsCard()
	{
		var ring = U.Panel(Image("maxlvl01.png", 230), "Clear").With(p =>
		{
			p.AddThemeStyleboxOverride("panel", UiTheme.Box(C.Accent, 200, null, 0, 6, 6));
			p.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
		});
		return U.V(18, ring, U.Text("maxlvl01", 52, C.Text, Fonts.Black, false, HorizontalAlignment.Center).Raw(), Spaced("PRESENTS", 18, C.Muted));
	}

	public const int PublisherFrames = 39;
	public const double PublisherFps = 10;

	static Control PublisherCard()
	{
		// (the picture rounded by a box of its own inside the pink frame: the frame's rounding doesn't reach past its padding)
		var picture = U.Panel(new GifPlayer { CustomMinimumSize = new Vector2(300, 300) }, "Clear").With(p =>
		{
			p.AddThemeStyleboxOverride("panel", UiTheme.Box(Colors.White, 21, null, 0, 0, 0));
			p.ClipChildren = ClipChildrenMode.Only;
		});
		var frame = U.Panel(picture, "Clear").With(p =>
		{
			p.AddThemeStyleboxOverride("panel", UiTheme.Box(C.Pink, 26, null, 0, 6, 6));
			p.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
		});
		return U.V(20, frame, U.Text("Pinke Geilheit", 50, C.Text, Fonts.Black, false, HorizontalAlignment.Center).Raw(), Spaced("PUBLISHING", 20, C.Pink));
	}

	/// <summary>The publisher's logo: the GIF's frames from one sheet (8 a row, 360 px each), at its 10 frames a second.</summary>
	public partial class GifPlayer : TextureRect
	{
		double _t;
		readonly AtlasTexture _atlas = new();
		/// <summary>The part of each frame shown (in the frame's 360 px): all of it, or a band (the anti-cheat's banner).</summary>
		readonly Rect2 _crop;
		public bool Frozen;

		public GifPlayer() : this(new Rect2(0, 0, 360, 360)) { }

		public GifPlayer(Rect2 crop)
		{
			_crop = crop;
			MouseFilter = MouseFilterEnum.Ignore;
			ExpandMode = ExpandModeEnum.IgnoreSize;
			StretchMode = StretchModeEnum.KeepAspectCovered;
			TextureFilter = TextureFilterEnum.Linear;
			_atlas.Atlas = Picture("publisher.webp");
			Texture = _atlas;
			SetFrame(0);
		}

		void SetFrame(int i) => _atlas.Region = new Rect2(new Vector2(i % 8 * 360, i / 8 * 360) + _crop.Position, _crop.Size);

		public override void _Process(double delta)
		{
			if (Frozen) return;
			_t += delta;
			SetFrame((int)(_t * PublisherFps) % PublisherFrames);
		}
	}

	/// <summary>Dev (--make-splash FILE): the anti-cheat window's first frame as the boot splash picture (1600×900, laid out at 1280×720).</summary>
	public static async Task Save(Node owner, string path)
	{
		var viewport = new SubViewport
		{
			Size = new Vector2I(1600, 900), Size2DOverride = new Vector2I(1280, 720), Size2DOverrideStretch = true,
			TransparentBg = false, RenderTargetUpdateMode = SubViewport.UpdateMode.Always,
		};
		var frame = new Control { Size = new Vector2(1280, 720), Theme = App.I.Theme };
		var splash = new AntiCheatSplash(null) { Frozen = true };
		frame.AddChild(splash);
		splash.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		viewport.AddChild(frame);
		owner.AddChild(viewport);
		await owner.ToSignal(owner.GetTree().CreateTimer(0.5), SceneTreeTimer.SignalName.Timeout);
		await owner.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
		viewport.GetTexture().GetImage().SavePng(path);
		viewport.QueueFree();
	}
}
