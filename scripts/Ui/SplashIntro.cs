// The splash screen: the "Quiznight?" logo on the game's dark purple with a warm glow (no text that changes with the
// language or version: the loading picture can't follow either). Godot shows it as a still picture
// while the game loads (application/boot_splash/image, made from this very layout with --make-splash), then the game
// opens on the same picture and plays it out: the "?" hops and wobbles, and the splash zooms and fades into the home
// screen. A click or a key skips it; dev and test runs (any arguments after --) don't show it at all.

using System.Threading.Tasks;
using Godot;

namespace QuizNight.Ui;

public partial class SplashIntro : Control
{
	/// <summary>The splash's background, also the boot splash's bg_color in project.godot (the default skin's Bg).</summary>
	public static readonly Color Background = new("#150023");

	readonly Control _content;
	readonly Label _question;
	Tween? _tween;

	public SplashIntro()
	{
		SetAnchorsPreset(LayoutPreset.FullRect);
		MouseFilter = MouseFilterEnum.Stop;
		(_content, _question) = Build();
		AddChild(_content);
	}

	/// <summary>The layout itself (full rect): background, glow and the logo in the middle.</summary>
	static (Control Root, Label Question) Build()
	{
		var root = new Control { MouseFilter = MouseFilterEnum.Ignore };
		root.SetAnchorsPreset(LayoutPreset.FullRect);
		root.AddChild(new ColorRect { Color = Background, MouseFilter = MouseFilterEnum.Ignore }.With(r => r.SetAnchorsPreset(LayoutPreset.FullRect)));
		var glow = new TextureRect
		{
			Texture = new GradientTexture2D
			{
				Width = 128, Height = 128, Fill = GradientTexture2D.FillEnum.Radial, FillFrom = new Vector2(0.5f, 0.5f), FillTo = new Vector2(1f, 0.5f),
				Gradient = new Gradient { Colors = new[] { new Color("#ff8201", 0.32f), new Color("#9d3cff", 0.12f), new Color(Background, 0) }, Offsets = new[] { 0f, 0.45f, 1f } },
			},
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.Scale, MouseFilter = MouseFilterEnum.Ignore,
		};
		glow.SetAnchorsPreset(LayoutPreset.FullRect);
		root.AddChild(glow);
		var question = U.Text("?", 120, C.Accent, Fonts.Black).Raw();
		var logo = U.H(0, U.Text("Quiznight", 120, C.Text, Fonts.Black).Raw(), question);
		logo.Alignment = BoxContainer.AlignmentMode.Center;
		var box = U.V(0, logo);
		box.Alignment = BoxContainer.AlignmentMode.Center;
		box.SetAnchorsPreset(LayoutPreset.FullRect);
		box.MouseFilter = MouseFilterEnum.Ignore;
		root.AddChild(box);
		return (root, question);
	}

	public override void _Ready()
	{
		// (wait one frame for the layout, so the "?" turns around its own middle)
		Callable.From(Play).CallDeferred();
	}

	void Play()
	{
		_question.PivotOffset = _question.Size / 2;
		_content.PivotOffset = Size / 2;
		_tween = CreateTween();
		// the "?" hops up and wobbles, lands with a little squash …
		_tween.TweenProperty(_question, "position:y", _question.Position.Y - 34, 0.22).SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out).SetDelay(0.25);
		_tween.Parallel().TweenProperty(_question, "rotation_degrees", -14f, 0.22);
		_tween.TweenProperty(_question, "position:y", _question.Position.Y, 0.32).SetTrans(Tween.TransitionType.Bounce).SetEase(Tween.EaseType.Out);
		_tween.Parallel().TweenProperty(_question, "rotation_degrees", 8f, 0.18);
		_tween.TweenProperty(_question, "rotation_degrees", 0f, 0.2).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
		// … then the splash zooms in a touch and fades away into the home screen
		_tween.TweenInterval(0.35);
		_tween.TweenCallback(Callable.From(Leave));
	}

	void Leave()
	{
		if (!IsInsideTree() || IsQueuedForDeletion()) return;
		_tween?.Kill();
		MouseFilter = MouseFilterEnum.Ignore;
		_content.PivotOffset = Size / 2;
		var t = CreateTween();
		t.TweenProperty(_content, "scale", new Vector2(1.06f, 1.06f), 0.4).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.In);
		t.Parallel().TweenProperty(this, "modulate:a", 0f, 0.4);
		t.TweenCallback(Callable.From(QueueFree));
	}

	public override void _GuiInput(InputEvent e)
	{
		if (e is InputEventMouseButton { Pressed: true }) Leave();
	}

	public override void _UnhandledInput(InputEvent e)
	{
		if (e is InputEventKey { Pressed: true }) Leave();
	}

	/// <summary>Dev (--make-splash FILE): the splash as a picture for the boot splash (2560×1440, laid out at 1280×720).</summary>
	public static async Task Save(Node owner, string path)
	{
		var viewport = new SubViewport
		{
			Size = new Vector2I(2560, 1440), Size2DOverride = new Vector2I(1280, 720), Size2DOverrideStretch = true,
			TransparentBg = false, RenderTargetUpdateMode = SubViewport.UpdateMode.Always,
		};
		var (root, _) = Build();
		var frame = new Control { Size = new Vector2(1280, 720), Theme = App.I.Theme };
		frame.AddChild(root);
		viewport.AddChild(frame);
		owner.AddChild(viewport);
		await owner.ToSignal(owner.GetTree().CreateTimer(0.5), SceneTreeTimer.SignalName.Timeout);
		await owner.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
		viewport.GetTexture().GetImage().SavePng(path);
		viewport.QueueFree();
	}
}
