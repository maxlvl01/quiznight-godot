// The intro's last screen (IntroSequence): the animated logo and "Press any button", with the sponsor in the corner.

using System;
using Godot;
using QuizNight.Shared;

namespace QuizNight.Ui;

/// <summary>The title screen: the animated logo big in the middle, "Press any button" blinking underneath.</summary>
public partial class TitleScreen : Control
{
	public readonly LogoField Logo;
	public readonly Label Prompt;
	public readonly TextureRect Glow;
	public readonly Button Sponsor;
	/// <summary>Started: the prompt and logo are the glide's now.</summary>
	public bool Leaving;
	double _t;

	public TitleScreen()
	{
		SetAnchorsPreset(LayoutPreset.FullRect);
		MouseFilter = MouseFilterEnum.Ignore;
		Glow = new TextureRect
		{
			Texture = new GradientTexture2D
			{
				Width = 128, Height = 128, Fill = GradientTexture2D.FillEnum.Radial, FillFrom = new Vector2(0.5f, 0.5f), FillTo = new Vector2(1f, 0.5f),
				Gradient = new Gradient { Colors = new[] { new Color(C.Accent, 0.28f), new Color(C.Purple, 0.1f), new Color(C.Bg, 0) }, Offsets = new[] { 0f, 0.45f, 1f } },
			},
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.Scale, MouseFilter = MouseFilterEnum.Ignore,
		};
		Glow.SetAnchorsPreset(LayoutPreset.FullRect);
		AddChild(Glow);
		Logo = new LogoField(104);
		var center = new CenterContainer { MouseFilter = MouseFilterEnum.Ignore };
		center.SetAnchorsPreset(LayoutPreset.FullRect);
		center.OffsetBottom = -60;
		center.AddChild(Logo);
		AddChild(center);
		Prompt = U.Text(Lang.T("Press any button").ToUpper(), 22, C.Text, Fonts.Spaced, false, HorizontalAlignment.Center);
		Prompt.SetAnchorsPreset(LayoutPreset.CenterBottom);
		Prompt.GrowHorizontal = GrowDirection.Both;
		Prompt.GrowVertical = GrowDirection.Begin;
		Prompt.OffsetBottom = -90;
		AddChild(Prompt);
		// the sponsor, bottom right: opens their Patreon
		Sponsor = new Button { Flat = true, MouseDefaultCursorShape = CursorShape.PointingHand, TooltipText = IntroSequence.SponsorUrl };
		Sponsor.AddChild(U.V(2, U.Text(Lang.T("Sponsored by").ToUpper(), 11, C.Muted, Fonts.Spaced, false, HorizontalAlignment.Right), new TrackGlowLogo(0.7f))
			.With(v => { v.MouseFilter = MouseFilterEnum.Ignore; v.SetAnchorsPreset(LayoutPreset.FullRect); }));
		Sponsor.CustomMinimumSize = new Vector2(250, 70);
		Sponsor.SetAnchorsPreset(LayoutPreset.BottomRight);
		Sponsor.GrowHorizontal = GrowDirection.Begin;
		Sponsor.GrowVertical = GrowDirection.Begin;
		Sponsor.OffsetRight = -28;
		Sponsor.OffsetBottom = -24;
		Sponsor.Pressed += () => OS.ShellOpen(IntroSequence.SponsorUrl);
		AddChild(Sponsor);
	}

	public override void _Process(double delta)
	{
		if (Leaving) return;
		_t += delta;
		// (fades in, then breathes)
		var a = _t < 0.6 ? _t / 0.6 * 0.5 : 0.55 + 0.45 * Mathf.Cos((float)(_t - 0.6) * 2.6f);
		if (Prompt.Modulate.A > 0 || _t < 1) Prompt.Modulate = new Color(1, 1, 1, (float)a);
		if (_t < 0.6) Logo.Modulate = new Color(1, 1, 1, (float)(_t / 0.6));
		else if (Logo.Modulate.A < 1 && _t < 0.7) Logo.Modulate = Colors.White;
	}
}

/// <summary>
/// TrackGlow's logo: its app icon (three glowing dots, assets/intro/trackglow.png, from TrackGlow.exe) and the name beside
/// it in the game's chunky letters, glowing in the icon's purple to blue.
/// </summary>
public partial class TrackGlowLogo : Control
{
	readonly float _k;
	double _t;
	static readonly Color Purple = new("#b07cff");
	static readonly Color Blue = new("#4a90ff");
	static readonly Texture2D? Icon = ResourceLoader.Exists("res://assets/intro/trackglow.png") ? GD.Load<Texture2D>("res://assets/intro/trackglow.png") : null;
	const string Wordmark = "TrackGlow";

	public TrackGlowLogo() : this(1) { }

	public TrackGlowLogo(float k)
	{
		_k = k;
		MouseFilter = MouseFilterEnum.Ignore;
		var text = Fonts.Black.GetStringSize(Wordmark, HorizontalAlignment.Left, -1, (int)(48 * k)).X;
		CustomMinimumSize = new Vector2(72 * k + 16 * k + text, 76 * k);
		SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
	}

	public override void _Process(double delta)
	{
		_t += delta;
		QueueRedraw();
	}

	public override void _Draw()
	{
		var icon = 72 * _k;
		var top = (Size.Y - icon) / 2;
		if (Icon != null) DrawTextureRect(Icon, new Rect2(0, top, icon, icon), false);
		var size = (int)(48 * _k);
		var at = new Vector2(icon + 16 * _k, Size.Y / 2 + size * 0.36f);
		var pulse = 0.7f + 0.3f * Mathf.Sin((float)_t * 2.2f);
		// (the glow: the name drawn wider and see-through behind itself, purple fading into blue)
		for (var r = 3; r >= 1; r--)
			DrawStringOutline(Fonts.Black, at, Wordmark, HorizontalAlignment.Left, -1, size, (int)(r * 5 * _k), new Color(Purple.Lerp(Blue, r / 3f), 0.1f * pulse));
		DrawString(Fonts.Black, at, Wordmark, HorizontalAlignment.Left, -1, size, Colors.White);
	}
}
