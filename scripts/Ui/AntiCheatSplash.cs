// The Pinke Geilheit Neural-Level Anti-Cheat: the small window the game opens as, before the intro (IntroSequence). A joke
// in the style of Easy Anti-Cheat's launcher: it checks nothing.

using System;
using Godot;
using QuizNight.Shared;

namespace QuizNight.Ui;

/// <summary>
/// The Pinke Geilheit Anti-Cheat, like Easy Anti-Cheat's launcher: the game opens as a small borderless window with a banner
/// (the woman from the publisher's logo, moving), a progress bar and very serious status lines, then becomes the game's
/// real window (App). It checks nothing.
/// </summary>
public partial class AntiCheatSplash : Control
{
	static readonly string[] Steps =
	{
		"Initialising…", "Connecting to your brain…", "Verifying game files…", "Scanning for big brains…", "Reading your thoughts…",
		"No cheats found. Probably.",
	};

	/// <summary>The small window it shows in (project.godot's window size override and borderless are the same).</summary>
	public static readonly Vector2I WindowSize = new(800, 450);

	readonly ColorRect _fill;
	readonly Label _status;
	readonly Label _percent;
	readonly Action? _done;
	double _t;
	public bool Frozen;
	const double Duration = 3.2;
	static readonly Color Pink = new("#ff4f8b");

	public AntiCheatSplash() : this(null) { }

	public static bool Showing { get; private set; }

	public override void _EnterTree() => Showing = true;

	public override void _ExitTree() => Showing = false;

	public AntiCheatSplash(Action? done)
	{
		_done = done;
		SetAnchorsPreset(LayoutPreset.FullRect);
		MouseFilter = MouseFilterEnum.Stop;
		var bg = new ColorRect { Color = new Color("#1c1b22"), MouseFilter = MouseFilterEnum.Ignore };
		bg.SetAnchorsPreset(LayoutPreset.FullRect);
		AddChild(bg);
		// the banner: the woman's face and shoulders out of the GIF, darkened towards the bottom for the title
		var banner = new IntroSequence.GifPlayer(new Rect2(0, 58, 360, 141)) { StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered };
		banner.SetAnchorsPreset(LayoutPreset.TopWide);
		banner.OffsetBottom = 500;
		AddChild(banner);
		var shade = new TextureRect
		{
			Texture = new GradientTexture2D
			{
				Width = 4, Height = 64, FillFrom = new Vector2(0, 0), FillTo = new Vector2(0, 1),
				Gradient = new Gradient { Colors = new[] { new Color("#1c1b22", 0), new Color("#1c1b22", 0.15f), new Color("#1c1b22", 1) }, Offsets = new[] { 0f, 0.45f, 1f } },
			},
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.Scale, MouseFilter = MouseFilterEnum.Ignore,
		};
		shade.SetAnchorsPreset(LayoutPreset.TopWide);
		shade.OffsetBottom = 502;
		AddChild(shade);
		var title = U.H(22,
			new ShieldIcon { CustomMinimumSize = new Vector2(70, 84), SizeFlagsVertical = SizeFlags.ShrinkCenter },
			U.V(-6, U.Text("PINKE GEILHEIT", 66, Colors.White, Fonts.Black).Raw(), U.Text("NEURAL-LEVEL ANTI-CHEAT", 30, Pink, Fonts.Spaced).Raw()));
		title.Position = new Vector2(56, 330);
		AddChild(title);
		_status = U.Text(Lang.T(Steps[0]), 28, new Color("#d8d6e0")).Raw();
		_percent = U.Text("0 %", 28, new Color("#8e8a9c"), Fonts.Bold).Raw();
		var track = new ColorRect { Color = new Color("#2e2c37"), CustomMinimumSize = new Vector2(0, 16), MouseFilter = MouseFilterEnum.Ignore };
		_fill = new ColorRect { Color = Pink, MouseFilter = MouseFilterEnum.Ignore };
		track.AddChild(_fill);
		track.Resized += () => _fill.Size = new Vector2(_fill.Size.X, track.Size.Y);
		var foot = U.V(14, U.H(8, _status.Grow(), _percent), track, U.Text(Lang.T("Protected game: Quiznight?"), 22, new Color("#6f6b7d")).Raw());
		foot.SetAnchorsPreset(LayoutPreset.BottomWide);
		foot.OffsetLeft = 56;
		foot.OffsetRight = -56;
		foot.OffsetTop = -190;
		foot.OffsetBottom = -44;
		AddChild(foot);
	}

	public override void _Process(double delta)
	{
		if (Frozen) return;
		_t += delta;
		var f = (float)Mathf.Min(1, _t / Duration);
		// (it stalls a little at the scary step, like the real thing)
		var shown = f < 0.5f ? f : f < 0.62f ? 0.5f : (f - 0.12f) / 0.88f;
		_fill.Size = new Vector2(((Control)_fill.GetParent()).Size.X * shown, _fill.Size.Y);
		_percent.Text = $"{Mathf.RoundToInt(shown * 100)} %";
		_status.Text = Lang.T(Steps[Mathf.Min(Steps.Length - 1, (int)(f * (Steps.Length - 1) + 0.0001f))]);
		if (_t >= Duration + 0.5 && _done != null)
		{
			Frozen = true;
			QueueFree();
			_done();
		}
	}

	partial class ShieldIcon : Control
	{
		public ShieldIcon() => MouseFilter = MouseFilterEnum.Ignore;

		public override void _Draw()
		{
			var s = Size;
			Vector2 P(float x, float y) => new(x * s.X, y * s.Y);
			DrawColoredPolygon(new[] { P(0.5f, 0), P(1, 0.15f), P(0.95f, 0.55f), P(0.5f, 1), P(0.05f, 0.55f), P(0, 0.15f) }, Pink);
			DrawPolyline(new[] { P(0.27f, 0.5f), P(0.44f, 0.67f), P(0.75f, 0.33f) }, Colors.White, 6f, true);
		}
	}
}
