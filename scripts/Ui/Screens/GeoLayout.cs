// Map pin questions on a player's screen, like GeoGuessr: the screenshot fills the space, the map sits small in the
// bottom-right corner and grows while the mouse is over it (⤢ keeps it big, ⤡ small again; ⛶ takes the whole space).

using Godot;
using QuizNight.Shared;

namespace QuizNight.Ui.Screens;

public partial class GeoLayout : Control
{
	enum Size3 { Small, Big, Full }

	readonly Control _corner;
	readonly Button _pin;
	readonly Button _full;
	Size3 _kept = Size3.Small;
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
		_pin = U.Btn("⤢", "SmallButton", () => Keep(_kept == Size3.Big ? Size3.Small : Size3.Big), 15).Tip("Keep the map big");
		_full = U.Btn("Full", "SmallButton", () => Keep(_kept == Size3.Full ? Size3.Small : Size3.Full), 15).Tip("Map over the whole picture");
		var tools = GameParts.Row(6, U.Muted("Map", 13, false).Grow(), _pin, _full);
		_corner = U.Panel(U.V(6, tools, map, lockIn), "Card2");
		_corner.AddThemeStyleboxOverride("panel", UiTheme.Box(C.Panel, 12, C.Blue, 2, 8, 8));
		AddChild(_corner);
	}

	void Keep(Size3 size)
	{
		_kept = size;
		_pin.Text = _kept == Size3.Big ? "⤡" : "⤢";
		_pin.TooltipText = Lang.T(_kept == Size3.Big ? "Make the map small again" : "Keep the map big");
		_full.TooltipText = Lang.T(_kept == Size3.Full ? "Back to the picture" : "Map over the whole picture");
	}

	public override void _Process(double delta)
	{
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
		var area = Size;
		var target = _kept == Size3.Full ? area - new Vector2(16, 16)
			: _kept == Size3.Big || _hover ? new Vector2(area.X * 0.62f, area.Y * 0.78f)
			: new Vector2(Mathf.Max(260, area.X * 0.32f), Mathf.Max(220, area.Y * 0.42f));
		_size = _size == Vector2.Zero ? target : _size.Lerp(target, (float)Mathf.Min(1, delta * 12));
		_corner.Size = _size;
		// (its contents may need more than asked for: placed by the size it really took, so it never pokes out)
		_corner.Position = (area - _corner.Size - new Vector2(8, 8)).Max(Vector2.Zero);
	}
}
