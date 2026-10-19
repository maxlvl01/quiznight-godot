// Containers that keep things in shape on any screen: the screens at most 16:9 wide (ultrawide monitors
// get a space picture at the sides instead of stretched-out screens), and videos at their own aspect ratio
// (letterboxed in black) instead of filling whatever box they're in.

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
	}
}

/// <summary>
/// The space picture behind everything, seen beside the 16:9 screens on wider monitors. Slightly out of
/// focus, sharp around the mouse (the focus follows it, softly), and it shifts a little with the mouse.
/// Picture: the Cosmic Cliffs in the Carina Nebula (NASA, ESA, CSA, STScI).
/// </summary>
public partial class SpaceBackdrop : Control
{
	// The sharp picture on top of a blurred one, faded out away from the mouse.
	static readonly Shader Focus = new()
	{
		Code = """
			shader_type canvas_item;
			uniform vec2 focus = vec2(0.5);
			uniform float aspect = 1.0;
			void fragment() {
				float d = length((UV - focus) * vec2(aspect, 1.0));
				COLOR = texture(TEXTURE, UV);
				COLOR.a = 1.0 - smoothstep(0.05, 0.38, d);
			}
			""",
	};

	readonly Texture2D? _sharp;
	readonly Texture2D? _blurred;
	readonly ShaderMaterial _material = new() { Shader = Focus };
	readonly Control _top = new() { MouseFilter = MouseFilterEnum.Ignore };
	Vector2 _focus = new(0.5f, 0.5f);

	public SpaceBackdrop()
	{
		MouseFilter = MouseFilterEnum.Ignore;
		// (smooth scaling: the project's default filter is nearest)
		TextureFilter = TextureFilterEnum.Linear;
		var source = DisplayServer.GetName() == "headless" ? null : GD.Load<Texture2D>("res://assets/backgrounds/cosmic-cliffs.webp")?.GetImage();
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
		_top.Material = _material;
		_top.TextureFilter = TextureFilterEnum.Linear;
		_top.SetAnchorsPreset(LayoutPreset.FullRect);
		_top.Draw += () => DrawPicture(_top, _sharp);
		AddChild(_top);
	}

	public override void _Process(double delta)
	{
		// Only on windows wider than 16:9: elsewhere the screens cover it all.
		Visible = _sharp != null && Size.X > Size.Y * ScreenFrame.MaxAspect + 2;
		if (!Visible) return;
		var mouse = GetLocalMousePosition() / Size;
		_focus = _focus.Lerp(mouse.Clamp(Vector2.Zero, Vector2.One), (float)Mathf.Min(1, delta * 4));
		QueueRedraw();
		_top.QueueRedraw();
	}

	public override void _Draw() => DrawPicture(this, _blurred);

	void DrawPicture(CanvasItem on, Texture2D? texture)
	{
		if (texture == null) return;
		// Covers the window, a little bigger than it, so it can shift with the mouse without showing its edge.
		var tex = texture.GetSize();
		var scale = Mathf.Max(Size.X / tex.X, Size.Y / tex.Y) * 1.04f;
		var size = tex * scale;
		var at = (Size - size) / 2 + (_focus - new Vector2(0.5f, 0.5f)) * (size - Size) * -0.8f;
		if (on == _top)
		{
			// (where the mouse is on the picture)
			_material.SetShaderParameter("focus", (_focus * Size - at) / size);
			_material.SetShaderParameter("aspect", size.X / size.Y);
		}
		// A little darker, so the screens in the middle stand out.
		on.DrawTextureRect(texture, new Rect2(at, size), false, new Color(0.75f, 0.75f, 0.75f));
	}
}

/// <summary>A video player kept at the video's own aspect ratio, centred on black.</summary>
public partial class VideoFrame : Container
{
	readonly VideoStreamPlayer? _video;
	Vector2 _videoSize = new(16, 9);

	public VideoFrame() : this(null) { }

	public VideoFrame(VideoStreamPlayer? video)
	{
		_video = video;
		ClipContents = true;
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

	public override void _Draw() => DrawRect(new Rect2(Vector2.Zero, Size), Colors.Black);

	public override void _Notification(int what)
	{
		if (what != NotificationSortChildren || _video == null) return;
		var scale = Mathf.Min(Size.X / _videoSize.X, Size.Y / _videoSize.Y);
		var fit = _videoSize * scale;
		FitChildInRect(_video, new Rect2((Size - fit) / 2, fit));
		QueueRedraw();
	}
}
