// Containers that keep things in shape on any screen: the screens at most 16:9 wide (ultrawide monitors
// get a space picture at the sides instead of stretched-out screens), and videos at their own aspect ratio
// (as big as they fit, centred, with nothing painted around them) instead of filling whatever box they're in.

using System;
using Godot;

namespace QuizNight.Ui;

/// <summary>Lays its children out in the middle, at most 16:9 wide (full height; narrower windows use all of it).</summary>
public partial class ScreenFrame : Container
{
	public const float MaxAspect = 16f / 9f;

	Rect2 Frame
	{
		get
		{
			var width = Mathf.Min(Size.X, Size.Y * MaxAspect);
			return new Rect2((Size.X - width) / 2, 0, width, Size.Y);
		}
	}

	public override void _Notification(int what)
	{
		if (what != NotificationSortChildren) return;
		foreach (var child in GetChildren())
			if (child is Control c) FitChildInRect(c, Frame);
		QueueRedraw();
	}

	// The screens' own background, with a soft shadow onto the space picture beside it.
	public override void _Draw()
	{
		var frame = Frame;
		if (frame.Size.X < Size.X)
			for (var i = 1; i <= 12; i++)
				DrawRect(frame.Grow(i * 2), new Color(0, 0, 0, 0.05f));
		DrawRect(frame, C.Bg);
		// (the party show stage: a warm spotlight glow from above the middle)
		DrawTextureRect(StageGlow(), frame, false);
		// (seasonal skins: snow and fairy lights, or cobwebs, bats and pumpkins, behind the screens' cards)
		if (Seasonal.OnScreen) Seasonal.DrawScreen(this, frame, _t);
	}

	double _t;
	bool _wasOn;

	public override void _Process(double delta)
	{
		// (redrawn while on, and once more when it goes off: another screen then has a clean background)
		var on = Seasonal.OnScreen;
		if (on || _wasOn) QueueRedraw();
		_wasOn = on;
		if (on && !Announcer.ReduceMotion) _t += delta;
	}

	static GradientTexture2D? _glow;
	static Color _glowFor;

	/// <summary>The stage's glow in the skin's colours (made again when the skin changes).</summary>
	static GradientTexture2D StageGlow()
	{
		if (_glow != null && _glowFor == C.Accent) return _glow;
		_glowFor = C.Accent;
		return _glow = new GradientTexture2D
		{
			Width = 256, Height = 144, Fill = GradientTexture2D.FillEnum.Radial, FillFrom = new Vector2(0.5f, 0.32f), FillTo = new Vector2(1.05f, 0.95f),
			Gradient = new Gradient
			{
				Colors = new[] { new Color(C.Purple, 0.22f), new Color(C.Accent, 0.05f), new Color(C.Bg, 0) },
				Offsets = new[] { 0f, 0.55f, 1f },
			},
		};
	}
}

/// <summary>
/// The space picture behind everything, seen beside the 16:9 screens on wider monitors. Slightly out of
/// focus, sharp around the mouse (the focus follows it, softly), and it shifts a little with the mouse
/// (unless Settings.BackgroundMotion is off).
/// Picture: the Cosmic Cliffs in the Carina Nebula (NASA, ESA, CSA, STScI).
/// </summary>
public partial class SpaceBackdrop : Control
{
	// The sharp picture on top of a blurred one, faded out away from the mouse (in window coordinates, so it also
	// works when each side shows its own part of the picture).
	static readonly Shader Focus = new()
	{
		Code = """
			shader_type canvas_item;
			uniform vec2 focus = vec2(0.5);
			uniform float aspect = 1.0;
			void fragment() {
				float d = length((SCREEN_UV - focus) * vec2(aspect, 1.0));
				COLOR = texture(TEXTURE, UV) * COLOR;
				COLOR.a = 1.0 - smoothstep(0.07, 0.5, d);
			}
			""",
	};

	/// <summary>The out-of-focus layer for the animated background: the video's frame, blurred as it's drawn.</summary>
	static readonly Shader Blur = new()
	{
		Code = """
			shader_type canvas_item;
			void fragment() {
				vec4 sum = vec4(0.0);
				vec2 step = TEXTURE_PIXEL_SIZE * 5.0;
				for (int x = -3; x <= 3; x++)
					for (int y = -3; y <= 3; y++)
						sum += texture(TEXTURE, UV + vec2(float(x), float(y)) * step);
				COLOR = sum / 49.0 * COLOR;
			}
			""",
	};

	Texture2D? _sharp;
	Texture2D? _blurred;
	/// <summary>The picture shown (a Backgrounds id); a new choice in Settings swaps it.</summary>
	string? _picture;
	int _customVersion;
	/// <summary>Plays the animated background (looping, silent); its frame is both layers.</summary>
	VideoStreamPlayer? _video;
	readonly ShaderMaterial _material = new() { Shader = Focus };
	readonly Control _top = new() { MouseFilter = MouseFilterEnum.Ignore };
	Vector2 _focus = new(0.5f, 0.5f);

	public SpaceBackdrop()
	{
		MouseFilter = MouseFilterEnum.Ignore;
		// (smooth scaling: the project's default filter is nearest)
		TextureFilter = TextureFilterEnum.Linear;
		_top.Material = _material;
		_top.TextureFilter = TextureFilterEnum.Linear;
		_top.SetAnchorsPreset(LayoutPreset.FullRect);
		_top.Draw += () => DrawPicture(_top, _sharp);
		AddChild(_top);
	}

	void Load(string picture)
	{
		_picture = picture;
		_customVersion = Storage.Backgrounds.Changed;
		_sharp = _blurred = null;
		_video?.QueueFree();
		_video = null;
		Material = null;
		if (DisplayServer.GetName() == "headless") return;
		if (Storage.Backgrounds.IsVideo(picture))
		{
			if (GD.Load<VideoStream>(Storage.Backgrounds.Path(picture)) is not { } stream) return;
			// (not shown itself: its frames are drawn like the picture, in both layers; tiny and see-through just in case)
			_video = new VideoStreamPlayer { Stream = stream, Loop = true, Autoplay = true, VolumeDb = -80, Modulate = new Color(1, 1, 1, 0), Size = Vector2.One, MouseFilter = MouseFilterEnum.Ignore };
			_video.Finished += () => _video?.Play();
			AddChild(_video);
			MoveChild(_video, 0);
			Material = new ShaderMaterial { Shader = Blur };
			return;
		}
		var source = Storage.Backgrounds.LoadImage(picture);
		if (source != null)
		{
			// Made once: a smoothly enlarged copy, and a blurred one: halved a few times and doubled back up
			// step by step (one big jump comes out blocky).
			if (source.IsCompressed()) source.Decompress();
			source.ClearMipmaps();
			int w = source.GetWidth() * 2, h = source.GetHeight() * 2;
			var sharp = (Image)source.Duplicate();
			sharp.Resize(w, h, Image.Interpolation.Cubic);
			var blurred = (Image)source.Duplicate();
			var sizes = new System.Collections.Generic.Stack<Vector2I>();
			for (var i = 0; i < 3; i++)
			{
				sizes.Push(blurred.GetSize());
				blurred.Resize(Math.Max(1, blurred.GetWidth() / 2), Math.Max(1, blurred.GetHeight() / 2), Image.Interpolation.Bilinear);
			}
			while (sizes.Count > 0)
			{
				var size = sizes.Pop();
				blurred.Resize(size.X, size.Y, Image.Interpolation.Bilinear);
			}
			blurred.Resize(w, h, Image.Interpolation.Bilinear);
			_sharp = ImageTexture.CreateFromImage(sharp);
			_blurred = ImageTexture.CreateFromImage(blurred);
		}
	}

	public override void _Process(double delta)
	{
		// (made on first use, and again when Settings picks another one)
		if (Storage.Settings.Background is var picture && (picture != _picture || (picture == Storage.Backgrounds.Custom && _customVersion != Storage.Backgrounds.Changed))
			&& Size.X > Size.Y * ScreenFrame.MaxAspect + 2) Load(picture);
		if (_video != null) _sharp = _blurred = _video.GetVideoTexture();
		// Only on windows wider than 16:9: elsewhere the screens cover it all.
		Visible = _sharp != null && Size.X > Size.Y * ScreenFrame.MaxAspect + 2;
		// (the setting off: still, evenly out of focus; the animated one stops on its frame)
		var moving = Storage.Settings.BackgroundMotion;
		if (_video != null) _video.Paused = !moving || !Visible;
		if (!Visible) return;
		_top.Visible = moving;
		var target = moving ? (GetLocalMousePosition() / Size).Clamp(Vector2.Zero, Vector2.One) : new Vector2(0.5f, 0.5f);
		// (slow to follow, so quick mouse moves don't swing it around)
		_focus = _focus.Lerp(target, (float)Mathf.Min(1, delta * 1.5));
		_material.SetShaderParameter("focus", _focus);
		_material.SetShaderParameter("aspect", Size.X / Size.Y);
		QueueRedraw();
		_top.QueueRedraw();
	}

	public override void _Draw() => DrawPicture(this, _blurred);

	void DrawPicture(CanvasItem on, Texture2D? texture)
	{
		if (texture == null) return;
		// Covers the window, a little bigger than it, so it can shift with the mouse without showing its edge.
		// The shift is small and the same on any screen (it used to be most of the overhang, which on ultrawide
		// monitors meant big swings: motion sickness).
		var tex = texture.GetSize();
		// A little darker, so the screens in the middle stand out.
		var tint = new Color(0.75f, 0.75f, 0.75f);
		var move = (_focus - new Vector2(0.5f, 0.5f)) * -0.03f * Size.Y;
		if (Storage.Backgrounds.Sides(_picture) is { } sides)
		{
			// Each side of the screens shows its own part of the picture, as tall as the window.
			var gap = (Size.X - Size.Y * ScreenFrame.MaxAspect) / 2;
			DrawSide(new Rect2(0, 0, gap, Size.Y), sides.Left);
			DrawSide(new Rect2(Size.X - gap, 0, gap, Size.Y), sides.Right);

			void DrawSide(Rect2 area, Storage.Backgrounds.Side side)
			{
				var sideScale = Size.Y * 1.03f * side.Zoom / tex.Y;
				var size = tex * sideScale;
				var at = area.GetCenter() - side.Anchor * size + move;
				// (never past the picture's edge)
				at = new Vector2(Mathf.Clamp(at.X, area.End.X - size.X, area.Position.X), Mathf.Clamp(at.Y, area.End.Y - size.Y, area.Position.Y));
				on.DrawTextureRectRegion(texture, area, new Rect2((area.Position - at) / sideScale, area.Size / sideScale), tint);
			}
			return;
		}
		var scale = Mathf.Max(Size.X / tex.X, Size.Y / tex.Y) * 1.03f;
		var whole = tex * scale;
		var at = (Size - whole) / 2 + move.Clamp(-(whole - Size) / 2, (whole - Size) / 2);
		on.DrawTextureRect(texture, new Rect2(at, whole), false, tint);
	}
}

/// <summary>
/// A video player kept at the video's own aspect ratio: as big as fits, centred, with an outline around just the video.
/// Nothing is painted around it (the black bars beside a 16:9 video in a wide box looked like a broken player).
/// </summary>
public partial class VideoFrame : Container
{
	readonly VideoStreamPlayer? _video;
	Vector2 _videoSize = new(16, 9);

	public VideoFrame() : this(null) { }

	public VideoFrame(VideoStreamPlayer? video)
	{
		_video = video;
		// (not clipped: the outline sits just outside the video)
		if (video == null) return;
		video.Expand = true;
		video.CustomMinimumSize = Vector2.Zero;
		AddChild(video);
	}

	public override void _Process(double delta)
	{
		// (the video's size is only known once Godot has decoded a frame)
		if (_video?.GetVideoTexture() is not { } tex) return;
		var size = tex.GetSize();
		if (size.X < 1 || size.Y < 1 || size == _videoSize) return;
		_videoSize = size;
		QueueSort();
	}

	Rect2 _fit;

	public override void _Draw()
	{
		if (_video == null || _fit.Size.X < 1) return;
		// (black right under the video, for its first frame; the ink outline of the party look around it)
		DrawRect(_fit, Colors.Black);
		DrawStyleBox(UiTheme.Box(Colors.Transparent, 14, C.Ink, 4, 0, 0).With(b => b.DrawCenter = false), _fit.Grow(4));
	}

	public override void _Notification(int what)
	{
		if (what != NotificationSortChildren || _video == null) return;
		var scale = Mathf.Min(Size.X / _videoSize.X, Size.Y / _videoSize.Y);
		var fit = _videoSize * scale;
		_fit = new Rect2(((Size - fit) / 2).Round(), fit.Round());
		FitChildInRect(_video, _fit);
		QueueRedraw();
	}
}
