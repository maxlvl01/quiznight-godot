// Map pin questions on a player's screen, like GeoGuessr: the screenshot fills the space, the map sits small and
// see-through in the bottom-right corner and grows (solid) while the mouse is over it. The pin keeps it big, Full puts
// it over the whole picture, Hide folds it away into a small "Map" button to look at the screenshot.

using Godot;
using QuizNight.Shared;

namespace QuizNight.Ui.Screens;

public partial class GeoLayout : Control
{
	enum Size4 { Hidden, Small, Big, Full }

	readonly Control _corner;
	readonly Button _pin;
	readonly PinIcon _pinIcon = new();
	readonly Button _full;
	readonly Button _show;
	Size4 _kept = Size4.Small;
	bool _hover;
	double _leftAt;
	Vector2 _size;

	public GeoLayout() : this(new Control(), new Control(), new Control()) { }

	public GeoLayout(Control screenshot, Control map, Control lockIn)
	{
		ClipContents = true;
		CustomMinimumSize = new Vector2(0, 520);
		SizeFlagsVertical = SizeFlags.ExpandFill;
		SizeFlagsHorizontal = SizeFlags.ExpandFill;
		screenshot.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		AddChild(screenshot);
		map.SizeFlagsVertical = SizeFlags.ExpandFill;
		map.CustomMinimumSize = new Vector2(160, 120);
		_pin = U.Btn("", "SmallButton", () => Keep(_kept == Size4.Big ? Size4.Small : Size4.Big)).MinSize(34, 30).Tip("Keep the map big");
		_pinIcon.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		_pin.AddChild(_pinIcon);
		_full = U.Btn("Full", "SmallButton", () => Keep(_kept == Size4.Full ? Size4.Small : Size4.Full), 15).Tip("Map over the whole picture");
		var hide = U.Btn("Hide", "SmallButton", () => Keep(Size4.Hidden), 15).Tip("Fold the map away to look at the screenshot");
		var tools = GameParts.Row(6, U.Muted("Map", 13, false).Grow(), _pin, _full, hide);
		_corner = U.Panel(U.V(6, tools, map, lockIn), "Card2");
		_corner.AddThemeStyleboxOverride("panel", UiTheme.Box(C.Panel, 12, C.Blue, 2, 8, 8));
		AddChild(_corner);
		_show = U.Btn("Map", "AccentButton", () => Keep(Size4.Small), 17).Tip("Show the map again");
		_show.Visible = false;
		AddChild(_show);
	}

	void Keep(Size4 size)
	{
		_kept = size;
		_pinIcon.On = _kept == Size4.Big;
		_pinIcon.QueueRedraw();
		_pin.TooltipText = Lang.T(_kept == Size4.Big ? "Make the map small again" : "Keep the map big");
		_full.TooltipText = Lang.T(_kept == Size4.Full ? "Back to the picture" : "Map over the whole picture");
		_corner.Visible = _kept != Size4.Hidden;
		_show.Visible = _kept == Size4.Hidden;
		_hover = false;
	}

	public override void _Process(double delta)
	{
		var area = Size;
		if (_kept == Size4.Hidden)
		{
			_show.Size = _show.GetCombinedMinimumSize();
			_show.Position = area - _show.Size - new Vector2(12, 12);
			return;
		}
		var now = Time.GetTicksMsec() / 1000.0;
		// (grows while the mouse is on it; stays a moment after it leaves, so a quick slip doesn't shrink it)
		var over = _corner.GetGlobalRect().HasPoint(GetGlobalMousePosition());
		if (over) _hover = true;
		else if (_hover && _leftAt <= 0) _leftAt = now;
		if (over) _leftAt = 0;
		if (_hover && !over && now - _leftAt > 0.7)
		{
			_hover = false;
			_leftAt = 0;
		}
		var target = _kept == Size4.Full ? area - new Vector2(16, 16)
			: _kept == Size4.Big || _hover ? new Vector2(area.X * 0.62f, area.Y * 0.78f)
			: new Vector2(Mathf.Max(260, area.X * 0.32f), Mathf.Max(220, area.Y * 0.42f));
		_size = _size == Vector2.Zero ? target : _size.Lerp(target, (float)Mathf.Min(1, delta * 12));
		_corner.Size = _size;
		// (its contents may need more than asked for: placed by the size it really took, so it never pokes out)
		_corner.Position = (area - _corner.Size - new Vector2(8, 8)).Max(Vector2.Zero);
		// See-through while it's small and the mouse is elsewhere, so the screenshot shows through.
		var solid = _kept != Size4.Small || _hover;
		var alpha = Mathf.MoveToward(_corner.Modulate.A, solid ? 1f : 0.5f, (float)delta * 4);
		_corner.Modulate = new Color(1, 1, 1, alpha);
	}
}

/// <summary>A pushpin for the "keep the map big" button: outlined, filled while it's pinned.</summary>
public partial class PinIcon : Control
{
	public bool On;

	public PinIcon() => MouseFilter = MouseFilterEnum.Ignore;

	public override void _Draw()
	{
		var k = Mathf.Min(Size.X, Size.Y) / 24f;
		var o = (Size - new Vector2(24 * k, 24 * k)) / 2;
		Vector2 P(float x, float y) => o + new Vector2(x * k, y * k);
		var color = On ? C.Accent : C.Text;
		var body = new[] { P(9, 4), P(15, 4), P(14.5f, 11), P(17, 14), P(7, 14), P(9.5f, 11) };
		if (On) DrawColoredPolygon(body, color);
		DrawPolyline(new[] { body[0], body[1], body[2], body[3], body[4], body[5], body[0] }, color, 1.6f * k, true);
		DrawLine(P(12, 14), P(12, 21), color, 1.6f * k, true);
	}
}
