// Drag and drop on the timeline (a player's turn): the item is a card you drag into a gap. It can be dragged
// again to another gap until you lock in. Clicking a gap still works too.

using System;
using Godot;
using QuizNight.Shared;

namespace QuizNight.Ui.Screens;

/// <summary>The item to place: in the tray above the timeline, or in the gap you dropped it in ("Your guess").</summary>
public partial class TimelineDragCard : PanelContainer
{
	public const string DragId = "timeline-item";
	readonly string _text;
	/// <summary>In a gap of the timeline (not in the tray above it).</summary>
	public bool Placed { get; }

	public TimelineDragCard() : this("", false) { }

	public TimelineDragCard(string text, bool placed)
	{
		_text = text;
		Placed = placed;
		MouseDefaultCursorShape = CursorShape.Drag;
		TooltipText = Lang.T(placed ? "Drag it to another gap if you change your mind" : "Drag it into the timeline");
		// (keys like the timeline's cards: orange in the tray, teal once it sits in a gap)
		Control content = placed
			? U.V(2, U.Text(text, 17, C.TealInk, Fonts.Bold, true, HorizontalAlignment.Center).Raw(),
				U.Text("Your guess", 12, new Color(C.TealInk, 0.75f), Fonts.Bold, false, HorizontalAlignment.Center))
			: GameParts.Row(10, U.Text("⋮⋮", 18, new Color(C.AccentInk, 0.6f), Fonts.Bold), U.Text(text, 24, C.AccentInk, Fonts.Bold, true, HorizontalAlignment.Center).Raw().Grow());
		AddChild(content);
		this.MinSize(placed ? 140 : 230, placed ? 64 : 0);
		AddThemeStyleboxOverride("panel", new KeyBox(placed ? C.Teal : C.Accent, 14, 14, 12, 3, 5, false));
		SizeFlagsVertical = SizeFlags.ShrinkCenter;
		SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
		foreach (var c in content.FindChildren("*", "Control", true, false))
			((Control)c).MouseFilter = MouseFilterEnum.Ignore;
		content.MouseFilter = MouseFilterEnum.Ignore;
	}

	public override Variant _GetDragData(Vector2 atPosition)
	{
		// The card follows the mouse (half see-through); the original fades until it's dropped.
		var preview = GameParts.TimelineCard(_text, "", false, true);
		preview.Modulate = new Color(1, 1, 1, 0.85f);
		var holder = new Control();
		holder.AddChild(preview);
		preview.Position = -atPosition;
		SetDragPreview(holder);
		Modulate = new Color(1, 1, 1, 0.35f);
		return DragId;
	}

	public override void _Notification(int what)
	{
		if (what == NotificationDragEnd && IsInstanceValid(this)) Modulate = Colors.White;
	}
}

/// <summary>A gap in the timeline: click it, or drop the item on it. Gaps widen and light up while you drag.</summary>
public partial class TimelineGap : Button
{
	readonly Action? _drop;
	readonly bool _wrong;
	bool _dragging, _hot;

	public TimelineGap() : this(false, null) { }

	public TimelineGap(bool wrong, Action? drop)
	{
		_wrong = wrong;
		_drop = drop;
		Text = wrong ? "✗" : "+";
		ThemeTypeVariation = "SmallButton";
		AddThemeFontSizeOverride("font_size", 20);
		Disabled = wrong;
		MouseDefaultCursorShape = CursorShape.PointingHand;
		SizeFlagsVertical = SizeFlags.ShrinkCenter;
		if (!wrong) Pressed += () => _drop?.Invoke();
		Restyle();
	}

	public override bool _CanDropData(Vector2 atPosition, Variant data)
	{
		var ok = !_wrong && _drop != null && data.VariantType == Variant.Type.String && data.AsString() == TimelineDragCard.DragId;
		if (ok && !_hot)
		{
			_hot = true;
			Restyle();
		}
		return ok;
	}

	// (deferred: dropping rebuilds the row, this gap included)
	public override void _DropData(Vector2 atPosition, Variant data) => Callable.From(() => _drop?.Invoke()).CallDeferred();

	public override void _Notification(int what)
	{
		switch ((long)what)
		{
			case NotificationDragBegin:
				_dragging = true;
				Restyle();
				break;
			case NotificationDragEnd:
				_dragging = _hot = false;
				Restyle();
				break;
			case NotificationMouseExit when _hot:
				_hot = false;
				Restyle();
				break;
		}
	}

	void Restyle()
	{
		if (!IsInstanceValid(this)) return;
		var live = _dragging && !_wrong;
		CustomMinimumSize = new Vector2(_hot ? 110 : live ? 72 : 52, 64);
		if (!live)
		{
			RemoveThemeStyleboxOverride("normal");
			RemoveThemeStyleboxOverride("hover");
			RemoveThemeColorOverride("font_color");
			return;
		}
		var box = UiTheme.Box(_hot ? new Color(C.Accent, 0.18f) : C.Panel2, 12, C.Accent, 2, 12, 5);
		AddThemeStyleboxOverride("normal", box);
		AddThemeStyleboxOverride("hover", box);
		AddThemeColorOverride("font_color", C.Accent);
	}
}
