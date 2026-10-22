// Small pieces of the game screens: the room code you click to copy, a picture shown big, the download ring.

using System.Linq;
using Godot;
using QuizNight.Shared;

namespace QuizNight.Ui;

/// <summary>"Room ABCD" in a game's top bar; clicking copies the code (to send it to someone who wants to join).</summary>
public partial class RoomCodeButton : Button
{
	public RoomCodeButton() : this("") { }

	public RoomCodeButton(string code)
	{
		ThemeTypeVariation = "LinkButton2";
		MouseDefaultCursorShape = CursorShape.PointingHand;
		SizeFlagsVertical = SizeFlags.ShrinkCenter;
		TooltipText = Lang.T("Click to copy the room code");
		var row = U.H(8, U.Muted("Room", 15, false), U.Text(code, 20, C.Accent, Fonts.Mono));
		row.MouseFilter = MouseFilterEnum.Ignore;
		foreach (var c in row.GetChildren()) ((Control)c).MouseFilter = MouseFilterEnum.Ignore;
		var copied = U.Muted("Copied!", 13, false).With(l =>
		{
			l.Visible = false;
			l.MouseFilter = MouseFilterEnum.Ignore;
		});
		row.AddChild(copied);
		AddChild(row);
		CustomMinimumSize = row.GetCombinedMinimumSize();
		row.MinimumSizeChanged += () => CustomMinimumSize = row.GetCombinedMinimumSize();
		Pressed += () =>
		{
			DisplayServer.ClipboardSet(code);
			copied.Visible = true;
			GetTree().CreateTimer(1.5).Timeout += () =>
			{
				if (IsInstanceValid(copied)) copied.Visible = false;
			};
		};
	}
}

/// <summary>A picture as big as the window allows, over everything. A click or Esc closes it.</summary>
public partial class ImagePopup : Control
{
	readonly Texture2D? _texture;

	public ImagePopup() : this(null) { }

	ImagePopup(Texture2D? texture)
	{
		_texture = texture;
		SetAnchorsPreset(LayoutPreset.FullRect);
		MouseFilter = MouseFilterEnum.Stop;
		MouseDefaultCursorShape = CursorShape.PointingHand;
		// (pictures are scaled down a lot here: smooth, not the project's nearest filter)
		TextureFilter = TextureFilterEnum.LinearWithMipmaps;
		var hint = U.Muted("Click anywhere or press Esc to close", 14, false, HorizontalAlignment.Center);
		hint.SetAnchorsAndOffsetsPreset(LayoutPreset.BottomWide, LayoutPresetMode.KeepHeight, 12);
		AddChild(hint);
	}

	public static void Open(Texture2D texture) => App.I.AddChild(new ImagePopup(texture));

	public override void _GuiInput(InputEvent e)
	{
		if (e is InputEventMouseButton { Pressed: true }) QueueFree();
	}

	public override void _UnhandledKeyInput(InputEvent e)
	{
		if (e is not InputEventKey { Pressed: true, Keycode: Key.Escape }) return;
		QueueFree();
		GetViewport().SetInputAsHandled();
	}

	public override void _Draw()
	{
		DrawRect(new Rect2(Vector2.Zero, Size), new Color(0, 0, 0, 0.85f));
		if (_texture == null) return;
		var room = Size - new Vector2(48, 96);
		var tex = _texture.GetSize();
		var scale = Mathf.Min(room.X / tex.X, room.Y / tex.Y);
		var size = tex * scale;
		DrawTextureRect(_texture, new Rect2((Size - size) / 2 - new Vector2(0, 12), size), false);
	}
}

/// <summary>
/// A player's name (and the "left at Q4" note under it) with small things right after it, e.g. the download ring.
/// A name too long for the space is cut off with "…", so what comes after it always stays in view.
/// </summary>
public partial class NameRow : Container
{
	const float Sep = 6;
	readonly VBoxContainer _text = U.V(0);
	readonly Label[] _labels;
	readonly Control[] _after;

	public NameRow() : this(new PublicPlayer(), null) { }

	public NameRow(PublicPlayer p, Label? note, params Control[] after)
	{
		_labels = note != null ? new[] { U.Text(p.Name, 15).Raw(), note } : new[] { U.Text(p.Name, 15).Raw() };
		foreach (var l in _labels)
		{
			l.ClipText = true;
			l.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
			_text.AddChild(l);
		}
		TooltipText = p.Name;
		MouseFilter = MouseFilterEnum.Pass;
		AddChild(_text);
		_after = after;
		foreach (var a in after) AddChild(a);
	}

	/// <summary>How wide the text wants to be (a clipped label reports no width of its own).</summary>
	float TextWidth() => _labels.Max(l => l.GetThemeFont("font").GetStringSize(l.Text, HorizontalAlignment.Left, -1, l.GetThemeFontSize("font_size")).X) + 2;

	float AfterWidth() => _after.Sum(a => a.GetCombinedMinimumSize().X + Sep);

	public override Vector2 _GetMinimumSize() =>
		new(Mathf.Min(TextWidth(), 40) + AfterWidth(), Mathf.Max(_text.GetCombinedMinimumSize().Y, _after.Select(a => a.GetCombinedMinimumSize().Y).DefaultIfEmpty(0).Max()));

	public override void _Notification(int what)
	{
		if (what != NotificationSortChildren) return;
		var textW = Mathf.Min(TextWidth(), Size.X - AfterWidth());
		var textH = _text.GetCombinedMinimumSize().Y;
		FitChildInRect(_text, new Rect2(0, (Size.Y - textH) / 2, textW, textH));
		var x = textW;
		foreach (var a in _after)
		{
			var s = a.GetCombinedMinimumSize();
			x += Sep;
			FitChildInRect(a, new Rect2(x, (Size.Y - s.Y) / 2, s.X, s.Y));
			x += s.X;
		}
	}
}

/// <summary>
/// How much of a sound/video file a player has: a ring that fills clockwise; when it's all there, a full ring with a tick.
/// </summary>
public partial class DownloadRing : Control
{
	readonly int _percent;

	public DownloadRing() : this(0) { }

	public DownloadRing(int percent, float size = 18)
	{
		_percent = Mathf.Clamp(percent, 0, 100);
		CustomMinimumSize = new Vector2(size, size);
		SizeFlagsVertical = SizeFlags.ShrinkCenter;
		TooltipText = _percent >= 100 ? Lang.T("Has the file") : Lang.F("Downloading the file: {0}%", _percent);
		MouseFilter = MouseFilterEnum.Pass;
	}

	public override void _Draw()
	{
		var light = new Color("#c9ccd8");
		// (a little below the middle: lines up with the name, whose letters sit low in their line)
		var center = Size / 2 + new Vector2(0, Size.Y * 0.12f);
		var r = Mathf.Min(Size.X, Size.Y) / 2 - 1.5f;
		var width = Mathf.Max(2f, r * 0.28f);
		DrawArc(center, r, 0, Mathf.Tau, 48, new Color(C.Border, 1), width, true);
		if (_percent >= 100)
		{
			DrawArc(center, r, 0, Mathf.Tau, 48, light, width, true);
			// The tick, inside the ring.
			var s = r * 0.55f;
			DrawPolyline(new[] { center + new Vector2(-s, 0.05f * s), center + new Vector2(-0.3f * s, 0.7f * s), center + new Vector2(s, -0.6f * s) }, light, width, true);
			return;
		}
		// From the top, clockwise.
		if (_percent > 0) DrawArc(center, r, -Mathf.Pi / 2, -Mathf.Pi / 2 + Mathf.Tau * _percent / 100f, 48, light, width, true);
	}
}

/// <summary>
/// Confetti over the final results: two bursts from the bottom corners, then a few seconds of it raining down.
/// </summary>
public partial class Confetti : Control
{
	static readonly Color[] Colors = { new("#f5b942"), new("#ff6b7a"), new("#3fd0b0"), new("#6f8cff"), new("#b07cff"), new("#ecedf3") };

	public Confetti()
	{
		SetAnchorsPreset(LayoutPreset.FullRect);
		MouseFilter = MouseFilterEnum.Ignore;
	}

	// (once the layout has given it its size)
	public override void _Ready() => Callable.From(Start).CallDeferred();

	void Start()
	{
		// (a small strip: the pieces flutter as they spin)
		var piece = Image.CreateEmpty(10, 5, false, Image.Format.Rgba8);
		piece.Fill(Godot.Colors.White);
		var texture = ImageTexture.CreateFromImage(piece);
		var colors = new Gradient { InterpolationMode = Gradient.InterpolationModeEnum.Constant };
		colors.Offsets = Colors.Select((_, i) => (float)i / Colors.Length).ToArray();
		colors.Colors = Colors;

		CpuParticles2D Make(int amount, bool oneShot)
		{
			var p = new CpuParticles2D
			{
				Texture = texture,
				Amount = amount,
				OneShot = oneShot,
				Lifetime = 4.5,
				ColorInitialRamp = colors,
				Gravity = new Vector2(0, 260),
				AngularVelocityMin = -360, AngularVelocityMax = 360,
				AngleMin = 0, AngleMax = 360,
				ScaleAmountMin = 0.8f, ScaleAmountMax = 1.4f,
				DampingMin = 20, DampingMax = 60,
			};
			AddChild(p);
			return p;
		}

		// Bursts from the bottom corners, up and in.
		foreach (var left in new[] { true, false })
		{
			var burst = Make(90, true);
			burst.Position = new Vector2(left ? 0 : Size.X, Size.Y);
			burst.Explosiveness = 0.95f;
			burst.Direction = new Vector2(left ? 0.45f : -0.45f, -1);
			burst.Spread = 18;
			burst.InitialVelocityMin = 700;
			burst.InitialVelocityMax = 1050;
		}
		// Then it rains for a while.
		var rain = Make(120, false);
		rain.Position = new Vector2(Size.X / 2, -20);
		rain.EmissionShape = CpuParticles2D.EmissionShapeEnum.Rectangle;
		rain.EmissionRectExtents = new Vector2(Size.X / 2, 4);
		rain.Direction = new Vector2(0, 1);
		rain.InitialVelocityMin = 40;
		rain.InitialVelocityMax = 120;
		rain.Gravity = new Vector2(0, 90);
		GetTree().CreateTimer(6).Timeout += () =>
		{
			if (IsInstanceValid(rain)) rain.Emitting = false;
		};
	}
}

/// <summary>A small trash can drawn in lines (like the question type icons), for delete buttons.</summary>
public partial class TrashIcon : Control
{
	Color _color;

	public TrashIcon() : this(C.Muted) { }

	public Color Color
	{
		get => _color;
		set
		{
			_color = value;
			QueueRedraw();
		}
	}

	public TrashIcon(Color color, float size = 18)
	{
		_color = color;
		CustomMinimumSize = new Vector2(size, size);
		MouseFilter = MouseFilterEnum.Ignore;
	}

	public override void _Draw()
	{
		var s = Mathf.Min(Size.X, Size.Y) / 24f;
		var o = (Size - new Vector2(24, 24) * s) / 2;
		Vector2 P(float x, float y) => o + new Vector2(x, y) * s;
		var w = Mathf.Max(1.5f, 2 * s);
		// lid, handle, can, two lines on the can
		DrawLine(P(3, 6), P(21, 6), _color, w, true);
		DrawPolyline(new[] { P(9, 6), P(9, 3.5f), P(15, 3.5f), P(15, 6) }, _color, w, true);
		DrawPolyline(new[] { P(5.5f, 6), P(6.5f, 20.5f), P(17.5f, 20.5f), P(18.5f, 6) }, _color, w, true);
		DrawLine(P(10, 10), P(10, 17), _color, w, true);
		DrawLine(P(14, 10), P(14, 17), _color, w, true);
	}
}

/// <summary>The update button's arrow: grey (no update known; click checks), green when a newer version is out.</summary>
public partial class UpdateArrow : Control
{
	bool _available, _hovered;

	public UpdateArrow() => MouseFilter = MouseFilterEnum.Ignore;

	public bool Available
	{
		set
		{
			_available = value;
			QueueRedraw();
		}
	}

	public bool Hovered
	{
		set
		{
			_hovered = value;
			QueueRedraw();
		}
	}

	public override void _Draw()
	{
		var color = _available ? (_hovered ? C.Teal.Lightened(0.25f) : C.Teal) : (_hovered ? C.Text : C.Muted);
		var s = Mathf.Min(Size.X, Size.Y) / 24f;
		var o = (Size - new Vector2(24, 24) * s) / 2;
		Vector2 P(float x, float y) => o + new Vector2(x, y) * s;
		var w = Mathf.Max(1.5f, 2.4f * s);
		// An arrow up out of a tray (the "download" sign, pointing up: a newer version)
		DrawLine(P(12, 16), P(12, 4), color, w, true);
		DrawPolyline(new[] { P(6.5f, 9.5f), P(12, 4), P(17.5f, 9.5f) }, color, w, true);
		DrawPolyline(new[] { P(4, 15), P(4, 20), P(20, 20), P(20, 15) }, color, w, true);
		if (_available) DrawCircle(P(20.5f, 3.5f), 2.6f * s, color, true, -1, true);
	}
}

/// <summary>
/// A scoreboard row that can slide in from a few rows above or below (someone overtook someone): its child is drawn
/// <see cref="Rows"/> row heights away from its place and eases back.
/// </summary>
public partial class SlideRow : Container
{
	const float Gap = 6;
	float _rows;
	Tween? _tween;

	/// <summary>How far from its place the row is drawn now, in rows (+ = below).</summary>
	public float Rows
	{
		get => _rows;
		set
		{
			_rows = value;
			QueueSort();
		}
	}

	/// <summary>Starts drawn <paramref name="rows"/> rows away and slides home (climbers drawn on top).</summary>
	public void SlideFrom(float rows)
	{
		if (Mathf.Abs(rows) < 0.01f) return;
		Rows = Mathf.Clamp(rows, -6, 6);
		ZIndex = rows > 0 ? 1 : 0;
		_tween = CreateTween();
		_tween.TweenProperty(this, "Rows", 0f, 0.7f).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
		_tween.TweenCallback(Callable.From(() => ZIndex = 0));
	}

	public override Vector2 _GetMinimumSize() =>
		GetChildren().OfType<Control>().Aggregate(Vector2.Zero, (size, c) => size.Max(c.GetCombinedMinimumSize()));

	public override void _Notification(int what)
	{
		if (what != NotificationSortChildren) return;
		foreach (var c in GetChildren().OfType<Control>())
			FitChildInRect(c, new Rect2(0, _rows * (Size.Y + Gap), Size));
	}
}

/// <summary>Runs an action every frame while it's in the tree (for small watchers, e.g. an opened scoreboard closing).</summary>
public partial class FrameWatch : Node
{
	readonly System.Action _tick;

	public FrameWatch() : this(() => { }) { }

	public FrameWatch(System.Action tick) => _tick = tick;

	public override void _Process(double delta) => _tick();
}
