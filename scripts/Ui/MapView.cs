// A map you can zoom (wheel, touchpad pinch, +/− buttons) and pan (drag). A click that doesn't
// move places the pin. Used by the editor, the players' map pin screen and the reveal.

using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using QuizNight.Shared;

namespace QuizNight.Ui;

public sealed record MapPin(NormPoint At, Color Color, string Label);
public sealed record MapMarker(NormPoint At, string Label, Action OnClick);

public partial class MapCanvas : Control
{
	const float MaxZoom = 8;
	/// <summary>Pointer movement (px) below which a press counts as a click, not a drag.</summary>
	const float ClickSlop = 5;

	Texture2D? _texture;
	float _aspect = 0.5625f;
	float _scale = 1;
	Vector2 _offset; // image top-left inside the drawing area, in px
	Vector2? _pressAt;
	bool _moved;
	(string Text, Vector2 At)? _hover;

	public NormPoint? Target;
	public List<MapPin> Pins = new();
	public List<MapMarker> Markers = new();
	public Action<NormPoint>? OnPick;
	/// <summary>Right-click, like zooming out of a zone in WoW.</summary>
	public Action? OnBack;
	/// <summary>Text to show next to the cursor, e.g. the zone under it.</summary>
	public Func<NormPoint, string?>? HoverLabel;
	public float MaxHeight = 560;
	public event Action? ZoomChanged;

	public MapCanvas()
	{
		ClipContents = true;
		SizeFlagsHorizontal = SizeFlags.ExpandFill;
		MouseFilter = MouseFilterEnum.Stop;
		FocusMode = FocusModeEnum.None;
		MouseExited += () =>
		{
			_hover = null;
			QueueRedraw();
		};
	}

	public float Zoom => _scale;

	public void SetTexture(Texture2D? texture, int width, int height)
	{
		_texture = texture;
		if (width > 0 && height > 0) _aspect = (float)height / width;
		else if (texture != null) _aspect = (float)texture.GetHeight() / texture.GetWidth();
		ResetView();
		UpdateHeight();
	}

	public void ResetView()
	{
		_scale = 1;
		_offset = Vector2.Zero;
		QueueRedraw();
		ZoomChanged?.Invoke();
	}

	/// <summary>The image's size at zoom 1, fitted into the control.</summary>
	Vector2 BaseSize
	{
		get
		{
			var w = Mathf.Min(Size.X, Size.Y / _aspect);
			return new Vector2(w, w * _aspect);
		}
	}

	Vector2 Origin => new((Size.X - BaseSize.X) / 2, 0);

	void UpdateHeight()
	{
		var h = Mathf.Min(MaxHeight, Size.X * _aspect);
		if (Mathf.Abs(CustomMinimumSize.Y - h) > 0.5f) CustomMinimumSize = new Vector2(0, h);
	}

	public override void _Notification(int what)
	{
		if (what == NotificationResized)
		{
			UpdateHeight();
			Clamp();
			QueueRedraw();
		}
	}

	void Clamp()
	{
		var b = BaseSize;
		_scale = Mathf.Clamp(_scale, 1, MaxZoom);
		_offset = new Vector2(
			Mathf.Clamp(_offset.X, b.X - b.X * _scale, 0),
			Mathf.Clamp(_offset.Y, b.Y - b.Y * _scale, 0));
	}

	/// <summary>Zoom by factor, keeping the point under the cursor fixed.</summary>
	public void ZoomAt(float factor, Vector2 at)
	{
		var p = at - Origin;
		var scale = Mathf.Clamp(_scale * factor, 1, MaxZoom);
		var k = scale / _scale;
		_offset = p - (p - _offset) * k;
		_scale = scale;
		Clamp();
		QueueRedraw();
		ZoomChanged?.Invoke();
	}

	public void ZoomCenter(float factor) => ZoomAt(factor, Origin + BaseSize / 2);

	NormPoint ToNorm(Vector2 at)
	{
		var b = BaseSize * _scale;
		var p = at - Origin - _offset;
		return new NormPoint(Math.Clamp(p.X / b.X, 0, 1), Math.Clamp(p.Y / b.Y, 0, 1));
	}

	Vector2 FromNorm(NormPoint n) => Origin + _offset + new Vector2((float)n.X, (float)n.Y) * BaseSize * _scale;

	bool InsideImage(Vector2 at)
	{
		var p = at - Origin;
		var b = BaseSize;
		return p.X >= 0 && p.X <= b.X && p.Y >= 0 && p.Y <= b.Y;
	}

	Rect2 MarkerRect(MapMarker m)
	{
		var font = Fonts.Bold;
		var text = m.Label + " ›";
		var w = font.GetStringSize(text, HorizontalAlignment.Left, -1, 13).X + 16;
		var c = FromNorm(m.At);
		return new Rect2(c.X - w / 2, c.Y - 13, w, 26);
	}

	public override void _GuiInput(InputEvent e)
	{
		switch (e)
		{
			case InputEventMouseButton { ButtonIndex: MouseButton.WheelUp or MouseButton.WheelDown } wheel when wheel.Pressed:
			{
				var f = wheel.Factor > 0 ? wheel.Factor : 1;
				ZoomAt(wheel.ButtonIndex == MouseButton.WheelUp ? 1 + 0.15f * f : 1 / (1 + 0.15f * f), wheel.Position);
				AcceptEvent();
				break;
			}
			case InputEventMouseButton { ButtonIndex: MouseButton.Right } right when right.Pressed:
				if (OnBack != null)
				{
					OnBack();
					AcceptEvent();
				}
				break;
			case InputEventMouseButton { ButtonIndex: MouseButton.Left } left:
				if (left.Pressed)
				{
					_pressAt = left.Position;
					_moved = false;
				}
				else if (_pressAt != null)
				{
					var click = !_moved;
					_pressAt = null;
					if (click) Click(left.Position);
				}
				AcceptEvent();
				break;
			case InputEventMouseMotion motion:
				if (_pressAt is { } start && (motion.ButtonMask & MouseButtonMask.Left) != 0)
				{
					if ((motion.Position - start).Length() > ClickSlop) _moved = true;
					if (_moved)
					{
						_offset += motion.Relative;
						Clamp();
						QueueRedraw();
					}
				}
				if (HoverLabel != null)
				{
					var text = InsideImage(motion.Position) ? HoverLabel(ToNorm(motion.Position)) : null;
					_hover = text != null ? (text, motion.Position) : null;
					QueueRedraw();
				}
				break;
			case InputEventMagnifyGesture pinch:
				ZoomAt(pinch.Factor, pinch.Position);
				AcceptEvent();
				break;
			case InputEventPanGesture pan:
				_offset -= pan.Delta * 8;
				Clamp();
				QueueRedraw();
				AcceptEvent();
				break;
		}
	}

	void Click(Vector2 at)
	{
		foreach (var m in Markers)
			if (MarkerRect(m).HasPoint(at))
			{
				m.OnClick();
				return;
			}
		if (OnPick != null && InsideImage(at)) OnPick(ToNorm(at));
	}

	public override void _Draw()
	{
		var b = BaseSize;
		var rect = new Rect2(Origin + _offset, b * _scale);
		DrawRect(new Rect2(Origin, b), C.Bg);
		if (_texture != null) DrawTextureRect(_texture, rect, false);
		else DrawString(Fonts.Regular, new Vector2(0, b.Y / 2), "Loading map…", HorizontalAlignment.Center, Size.X, 15, C.Muted);

		foreach (var pin in Pins)
		{
			var c = FromNorm(pin.At);
			DrawCircle(c, 13, new Color("#14151b"), true, -1, true);
			DrawCircle(c, 11, pin.Color, true, -1, true);
			var initials = pin.Label.Length >= 2 ? pin.Label[..2].ToUpperInvariant() : pin.Label.ToUpperInvariant();
			var font = Fonts.Bold;
			DrawString(font, new Vector2(c.X - 13, c.Y - font.GetHeight(10) / 2 + font.GetAscent(10)), initials, HorizontalAlignment.Center, 26, 10, new Color("#14151b"));
		}

		foreach (var m in Markers)
		{
			var r = MarkerRect(m);
			DrawStyleBox(UiTheme.Box(new Color(C.Bg, 0.85f), 8, C.Accent, 1), r);
			var font = Fonts.Bold;
			DrawString(font, new Vector2(r.Position.X, r.Position.Y + 18), m.Label + " ›", HorizontalAlignment.Center, r.Size.X, 13, C.Accent);
		}

		if (Target is { } t)
		{
			var c = FromNorm(t);
			// A yellow map pin with its tip on the spot.
			var head = c + new Vector2(0, -22);
			DrawColoredPolygon(new[] { c, head + new Vector2(-8, 4), head + new Vector2(8, 4) }, C.Accent);
			DrawCircle(head, 11, C.AccentInk, true, -1, true);
			DrawCircle(head, 9.5f, C.Accent, true, -1, true);
			DrawCircle(head, 3.5f, C.AccentInk, true, -1, true);
		}

		if (_hover is { } hover)
		{
			var font = Fonts.Bold;
			var size = font.GetStringSize(hover.Text, HorizontalAlignment.Left, -1, 13);
			var r = new Rect2(hover.At + new Vector2(14, 10), size + new Vector2(16, 10));
			DrawStyleBox(UiTheme.Box(new Color(C.Bg, 0.92f), 6, C.Border, 1), r);
			DrawString(font, r.Position + new Vector2(8, 5 + font.GetAscent(13)), hover.Text, HorizontalAlignment.Left, -1, 13, C.Text);
		}
	}

}

/// <summary>Map canvas with its name above and zoom buttons below.</summary>
public partial class MapView : VBoxContainer
{
	public readonly MapCanvas Canvas = new();
	readonly Label _zoom = U.Mono("1.0×", 13, C.Muted);
	readonly Button _in;
	readonly Button _out;
	readonly Button _fit;

	public MapView() : this("", null, 0, 0) { }

	public MapView(string name, Texture2D? texture, int width, int height, float maxHeight = 560)
	{
		AddThemeConstantOverride("separation", 6);
		SizeFlagsHorizontal = SizeFlags.ExpandFill;
		if (name != "") AddChild(U.Muted($"Map: {name}", 13, false));
		Canvas.MaxHeight = maxHeight;
		AddChild(U.Panel(Canvas, "Inset").With(p => p.AddThemeStyleboxOverride("panel", UiTheme.Box(C.Bg, 10, C.Border, 1, 0, 0))));
		_in = U.Small("+", () => Canvas.ZoomCenter(1.5f)).Tip("Zoom in");
		_out = U.Small("−", () => Canvas.ZoomCenter(1 / 1.5f)).Tip("Zoom out");
		_fit = U.Small("Fit", Canvas.ResetView);
		AddChild(U.H(6, _in, _out, _fit, _zoom, U.Fill(), U.Muted("Scroll to zoom, drag to move", 12, false)));
		Canvas.ZoomChanged += UpdateZoom;
		Canvas.SetTexture(texture, width, height);
	}

	void UpdateZoom()
	{
		_zoom.Text = $"{Canvas.Zoom:0.0}×";
		_in.Disabled = Canvas.Zoom >= 7.99f;
		_out.Disabled = Canvas.Zoom <= 1.001f;
		_fit.Disabled = Canvas.Zoom <= 1.001f;
	}
}

/// <summary>
/// The WoW Classic map like in the game: world → click a continent → click a zone → click to pin.
/// Right-click (or the path above) goes back up. Pins are only placed on zone maps and stored as a
/// point on the world map (see WowAtlas).
/// </summary>
public partial class WowAtlasView : VBoxContainer
{
	NormPoint? _target;
	readonly Action<NormPoint>? _onPick;
	readonly List<MapPin> _pins;
	readonly float _maxHeight;
	WowAtlas.Map _current;

	public WowAtlasView() : this(null, null) { }

	public WowAtlasView(NormPoint? target, Action<NormPoint>? onPick, List<MapPin>? pins = null, float maxHeight = 560)
	{
		_target = target;
		_onPick = onPick;
		_pins = pins ?? new List<MapPin>();
		_maxHeight = maxHeight;
		AddThemeConstantOverride("separation", 8);
		SizeFlagsHorizontal = SizeFlags.ExpandFill;
		// Start in the zone of an existing pin, otherwise on the world map.
		var loc = target is { } t ? WowAtlas.WorldToLocation(t) : null;
		_current = (loc is { } l ? WowAtlas.ZoneAt(l) : null) ?? WowAtlas.Get(WowAtlas.World);
		Build();
	}

	public void SetTarget(NormPoint? target)
	{
		_target = target;
		Build();
	}

	void Go(WowAtlas.Map map)
	{
		_current = map;
		Build();
	}

	void Build()
	{
		this.Clear();
		var nav = U.H(4);
		var ancestors = new List<WowAtlas.Map>();
		for (var m = _current.Parent is { } p ? WowAtlas.Get(p) : null; m != null; m = m.Parent is { } pp ? WowAtlas.Get(pp) : null) ancestors.Insert(0, m);
		foreach (var a in ancestors)
		{
			var target = a;
			nav.AddChild(U.Link(a.Name, () => Go(target), 14));
			nav.AddChild(U.Muted("›", 14, false).With(l => l.SizeFlagsVertical = SizeFlags.ShrinkCenter));
		}
		nav.AddChild(U.Bold(_current.Name, 15).With(l => l.SizeFlagsVertical = SizeFlags.ShrinkCenter));
		if (_current.Continent is { } continent)
		{
			nav.AddChild(U.Fill());
			var zones = WowAtlas.Maps.Where(m => m.Kind == WowAtlas.Kind.Zone && m.Continent == continent).ToList();
			var items = new List<(string, string)> { ("", "Jump to zone…") };
			items.AddRange(zones.Select(z => (z.Id.ToString(), z.Name)));
			var jump = U.Select(items.ToArray(), _current.Kind == WowAtlas.Kind.Zone ? _current.Id.ToString() : "", id =>
			{
				if (int.TryParse(id, out var zoneId)) Go(WowAtlas.Get(zoneId));
			});
			jump.SizeFlagsHorizontal = SizeFlags.ShrinkEnd;
			jump.CustomMinimumSize = new Vector2(220, 0);
			nav.AddChild(jump);
		}
		AddChild(nav);

		var view = new MapView("", Storage.Images.BuiltinTexture(WowAtlas.ImagePath(_current.Id)), WowAtlas.ImageWidth, WowAtlas.ImageHeight, _maxHeight);
		var canvas = view.Canvas;
		canvas.Target = _target is { } t ? WowAtlas.OnMap(_current, t) : null;
		canvas.Pins = _pins.Select(p => WowAtlas.OnMap(_current, p.At) is { } at ? p with { At = at } : null).OfType<MapPin>().ToList();
		canvas.Markers = WowAtlas.CitiesOn(_current).Select(c => new MapMarker(c.At, c.City.Name, () => Go(c.City))).ToList();
		var current = _current;
		canvas.OnPick = p =>
		{
			if (current.Kind == WowAtlas.Kind.Zone)
			{
				if (_onPick == null) return;
				var world = WowAtlas.LocationToWorld(WowAtlas.ToLocation(current, p));
				_target = world;
				canvas.Target = p;
				canvas.QueueRedraw();
				_onPick(world);
				return;
			}
			if (WowAtlas.ChildAt(current, p) is { } next) Go(next);
		};
		if (_current.Kind != WowAtlas.Kind.Zone) canvas.HoverLabel = p => WowAtlas.ChildAt(current, p)?.Name;
		if (_current.Parent is { } parent) canvas.OnBack = () => Go(WowAtlas.Get(parent));
		AddChild(view);

		AddChild(U.Muted(_current.Kind == WowAtlas.Kind.Zone
			? (_onPick != null ? "Click to place the pin. Right-click or use the path above to go back up." : "Right-click or use the path above to go back up.")
			: $"Click a {(_current.Kind == WowAtlas.Kind.World ? "continent" : "zone")} to open it, like the in-game map.", 12));
	}
}

/// <summary>The question's map: the WoW drill-down atlas for the WoW preset, a zoomable image otherwise.</summary>
public static class GameMap
{
	public static Control Make(PublicMap map, Texture2D? texture, NormPoint? target, Action<NormPoint>? onPick, List<MapPin>? pins = null, float maxHeight = 560)
	{
		if (map.PresetId == WowAtlas.PresetId) return new WowAtlasView(target, onPick, pins, maxHeight);
		var view = new MapView(map.Name, texture, map.Width, map.Height, maxHeight);
		view.Canvas.Target = target;
		view.Canvas.Pins = pins ?? new List<MapPin>();
		if (onPick != null)
			view.Canvas.OnPick = p =>
			{
				view.Canvas.Target = p;
				view.Canvas.QueueRedraw();
				onPick(p);
			};
		return view;
	}
}
