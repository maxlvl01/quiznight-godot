// Tutorials: short tours the first time a screen opens (the quiz overview of a new quiz, the question editor). The screen
// goes dark except for one thing at a time, which gets a glowing frame, and a bubble next to it says what it is for.
// Screens mark what a tour points at (Mark); a step whose thing isn't on this screen (a type without bonus points) is
// left out. Skip (or Esc) ends a tour, "Turn off tutorials" ends them all; Settings turns them on again and "Show
// tutorials again" forgets which were seen. Dev: --tutorial ID shows that tour every time its screen opens.

using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using QuizNight.Shared;
using QuizNight.Storage;

namespace QuizNight.Ui;

public static class Tutorial
{
	/// <summary>One step: what it points at (a Mark key; null = nothing, the bubble in the middle), a title and the text.</summary>
	public record Step(string? Target, string Title, string Text);

	const string MetaKey = "tutorial_target";

	public static readonly Dictionary<string, Step[]> Tours = new()
	{
		["overview"] = new Step[]
		{
			new(null, "Your first quiz!", "A quick tour of how a quiz is made, in a few short steps. Skip it any time (Esc); \"Turn off tutorials\" stops them all, and Settings brings them back."),
			new("overview.title", "Name your quiz", "Click the title to rename it. It's saved as soon as you leave the field."),
			new("overview.types", "Add a question", "Pick a type to add a question to the quiz. The ? on a card tells what the type does and which ways it can be played."),
			new("overview.list", "Your questions", "They're listed in the order they're played. Click one to edit it; drag it by its dots, or type another number, to move it. \"Show\" sorts the list by type or by when you made them."),
			new("overview.foot", "Host it or share it", "When nothing is left to fix, Open lobby starts a quiz night. Copy share code and Export file give the quiz to a friend."),
			new(null, "Shortcuts", "Ctrl+Z takes back the last change (moving, deleting, renaming), Ctrl+Y does it again. Esc goes back one screen, and so does your mouse's back button."),
			new("bug", "Found a bug? Got an idea?", "The bug button in the bottom left is on every screen. \"Report a bug\" if something doesn't work or looks wrong, \"Give feedback\" for an idea: a map, a question type, anything. Max gets it straight away, and ideas that make it into the game are credited in the changelog."),
		},
		["editor"] = new Step[]
		{
			new("editor.issues", "Fill in the blanks", "Write the question and its answer. This line says what's still missing; you can save anyway and finish it later."),
			new("editor.round", "Round settings", "Points, time limit and how the question is scored. Hover over a setting to learn more about it."),
			new("editor.bonus", "Bonus points", "Optional extra points for something on top, e.g. \"Also name the game\". At the reveal you give them to the players who earned them. 0 = no bonus."),
			new("editor.category", "Categories", "Put the question in a category, or write your own. Players only see it if \"Show categories\" is on in the lobby (it makes questions easier); \"Keep it secret\" hides it even then. Categories always count for the statistics."),
			new("editor.save", "Save", "Save question (Ctrl+S) keeps it and goes back to the quiz. \"← Back to quiz\" (or Esc) leaves without saving, and asks first if you changed something."),
			new(null, "Shortcuts", "Ctrl+Z takes back a change, Ctrl+Y does it again. Ctrl+V pastes a picture (a screenshot, say), and you can drop picture, sound and video files onto the window."),
		},
	};

	/// <summary>Marks what a tour step points at; returns the control.</summary>
	public static T Mark<T>(T control, string key) where T : Control
	{
		control.SetMeta(MetaKey, key);
		return control;
	}

	/// <summary>The visible control marked with the key (searched again each frame: forms rebuild themselves).</summary>
	public static Control? Find(Node root, string key)
	{
		foreach (var child in root.GetChildren())
		{
			if (child is Control c && !c.IsVisibleInTree()) continue;
			if (child is Control m && m.HasMeta(MetaKey) && m.GetMeta(MetaKey).AsString() == key) return m;
			if (Find(child, key) is { } found) return found;
		}
		return null;
	}

	static string[] DevArgs => OS.GetCmdlineUserArgs();

	/// <summary>The tour forced with --tutorial ID (dev).</summary>
	static string? Forced => Array.IndexOf(DevArgs, "--tutorial") is var i && i >= 0 && i + 1 < DevArgs.Length ? DevArgs[i + 1] : null;

	/// <summary>Whether the tour would show now: tutorials on, not seen yet, a window, and no dev arguments (bots, screenshots).</summary>
	public static bool Due(string id)
	{
		if (Forced == id) return true;
		if (DisplayServer.GetName() == "headless" || DevArgs.Length > 0) return false;
		return Settings.TutorialsOn && !Settings.TutorialsSeen.Contains(id);
	}

	/// <summary>A screen opened: its tour starts once the screen has its layout (only if still on that screen). when = the
	/// screen's own condition (the overview: a quiz without questions); --tutorial ID skips it.</summary>
	public static void Offer(string id, Control screen, bool when = true)
	{
		if (!(when || Forced == id) || !Due(id) || !Tours.ContainsKey(id)) return;
		screen.GetTree().CreateTimer(0.5).Timeout += () =>
		{
			if (!GodotObject.IsInstanceValid(screen) || App.I.Screen != screen || TutorialOverlay.Current != null) return;
			App.I.AddChild(new TutorialOverlay(id, screen));
		};
	}

	public static void MarkSeen(string id)
	{
		if (!Settings.TutorialsSeen.Contains(id)) Settings.TutorialsSeen = Settings.TutorialsSeen.Append(id).ToArray();
	}

	/// <summary>Settings' "Show tutorials again": every tour shows again, and tutorials are on.</summary>
	public static void Reset()
	{
		Settings.TutorialsSeen = Array.Empty<string>();
		Settings.TutorialsOn = true;
	}
}

/// <summary>A tour running: the dark screen with a hole around the step's thing, and the bubble.</summary>
public partial class TutorialOverlay : Control
{
	public static TutorialOverlay? Current { get; private set; }

	readonly string _id;
	readonly Control _screen;
	readonly List<Tutorial.Step> _steps;
	int _at;
	readonly PanelContainer _bubble;
	readonly VBoxContainer _content = U.V(10);
	/// <summary>The hole (where the step's thing is, a little bigger), or null for a step without one.</summary>
	Rect2? _hole;
	float _time;
	const float Gap = 14, BubbleWidth = 420;

	public TutorialOverlay() : this("overview", new Control()) { }

	public TutorialOverlay(string id, Control screen)
	{
		_id = id;
		_screen = screen;
		// (only the steps whose thing is on this screen)
		_steps = Tutorial.Tours[id].Where(s => s.Target == null || Tutorial.Find(App.I, s.Target) != null).ToList();
		SetAnchorsPreset(LayoutPreset.FullRect);
		MouseFilter = MouseFilterEnum.Stop;
		_bubble = U.Panel(U.Pad(_content, 22, 18, 22, 18));
		_bubble.AddThemeStyleboxOverride("panel", UiTheme.Box(C.Panel, 14, C.Accent, 2, 0, 0));
		_bubble.CustomMinimumSize = new Vector2(BubbleWidth, 0);
		AddChild(_bubble);
	}

	public override void _Ready()
	{
		Current = this;
		Trace.Log("tutorial", _id);
		// (keys go to the tour, not to a field that had the focus)
		GetViewport().GuiReleaseFocus();
		// (dev: --tutorial-step N starts at that step, for screenshots)
		var args = OS.GetCmdlineUserArgs();
		var arg = Array.IndexOf(args, "--tutorial-step");
		Show(arg >= 0 && arg + 1 < args.Length && int.TryParse(args[arg + 1], out var n) ? Math.Clamp(n - 1, 0, _steps.Count - 1) : 0);
	}

	public override void _ExitTree()
	{
		if (Current == this) Current = null;
	}

	void Show(int at)
	{
		_at = at;
		var step = _steps[at];
		_content.Clear();
		_content.AddChild(U.Eyebrow(Lang.F("Tutorial · {0} of {1}", at + 1, _steps.Count)));
		_content.AddChild(U.Bold(step.Title, 19, C.Text, true));
		_content.AddChild(U.Muted(step.Text, 14));
		var last = at == _steps.Count - 1;
		var next = U.Btn(last ? "Done" : "Next ›", "AccentButton", () => Next(1), 15);
		var back = U.Small("‹ Back", () => Next(-1));
		back.Disabled = at == 0;
		var buttons = U.H(8, U.Link("Turn off tutorials", TurnOff, 13).Tip("Settings can turn them on again"), new Control().Grow(), back);
		if (!last) buttons.AddChild(U.Small("Skip", Close));
		buttons.AddChild(next);
		foreach (var b in buttons.GetChildren().OfType<Button>()) b.FocusMode = FocusModeEnum.None;
		_content.AddChild(U.Gap(2));
		_content.AddChild(buttons);
		// (the thing can be scrolled out of sight: bring it in)
		if (step.Target != null && Tutorial.Find(App.I, step.Target) is { } target)
			for (var p = target.GetParent(); p != null; p = p.GetParent())
				if (p is ScrollContainer scroll)
				{
					scroll.EnsureControlVisible(target);
					break;
				}
		_bubble.Modulate = new Color(1, 1, 1, 0);
		CreateTween().TweenProperty(_bubble, "modulate:a", 1f, 0.18);
		_bubble.Size = Vector2.Zero;
	}

	void Next(int by)
	{
		var to = _at + by;
		if (to >= _steps.Count) Close();
		else if (to >= 0) Show(to);
	}

	void Close()
	{
		Tutorial.MarkSeen(_id);
		QueueFree();
	}

	void TurnOff()
	{
		Settings.TutorialsOn = false;
		Close();
	}

	public override void _Process(double delta)
	{
		_time += (float)delta;
		// (the screen changed under the tour, e.g. by a mouse button: the tour ends, seen)
		if (!IsInstanceValid(_screen) || App.I.Screen != _screen)
		{
			Close();
			return;
		}
		var target = _steps[_at].Target is { } key ? Tutorial.Find(App.I, key) : null;
		_hole = target?.GetGlobalRect().Grow(8);
		PlaceBubble();
		QueueRedraw();
	}

	/// <summary>The bubble below the hole if it fits, else above, else beside it; in the middle without one; always on screen.</summary>
	void PlaceBubble()
	{
		var size = _bubble.GetCombinedMinimumSize();
		_bubble.Size = size;
		var screen = Size;
		Vector2 at;
		if (_hole is not { } h) at = (screen - size) / 2;
		else if (h.End.Y + Gap + size.Y <= screen.Y - 8) at = new Vector2(h.GetCenter().X - size.X / 2, h.End.Y + Gap);
		else if (h.Position.Y - Gap - size.Y >= 8) at = new Vector2(h.GetCenter().X - size.X / 2, h.Position.Y - Gap - size.Y);
		else if (h.End.X + Gap + size.X <= screen.X - 8) at = new Vector2(h.End.X + Gap, h.GetCenter().Y - size.Y / 2);
		else if (h.Position.X - Gap - size.X >= 8) at = new Vector2(h.Position.X - Gap - size.X, h.GetCenter().Y - size.Y / 2);
		// (the thing fills the screen: the bubble sits in its bottom right)
		else at = screen - size - new Vector2(24, 24);
		_bubble.Position = new Vector2(Mathf.Clamp(at.X, 8, Mathf.Max(8, screen.X - size.X - 8)), Mathf.Clamp(at.Y, 8, Mathf.Max(8, screen.Y - size.Y - 8)));
	}

	public override void _Draw()
	{
		var dim = new Color(0, 0, 0, 0.62f);
		var all = new Rect2(Vector2.Zero, Size);
		if (_hole is not { } h)
		{
			DrawRect(all, dim);
			return;
		}
		h = h.Intersection(all);
		// (four dark bands around the hole)
		DrawRect(new Rect2(0, 0, Size.X, h.Position.Y), dim);
		DrawRect(new Rect2(0, h.End.Y, Size.X, Size.Y - h.End.Y), dim);
		DrawRect(new Rect2(0, h.Position.Y, h.Position.X, h.Size.Y), dim);
		DrawRect(new Rect2(h.End.X, h.Position.Y, Size.X - h.End.X, h.Size.Y), dim);
		// (a glowing frame that breathes)
		var glow = C.Accent with { A = 0.55f + 0.45f * Mathf.Sin(_time * 4) };
		var frame = new StyleBoxFlat { BgColor = Colors.Transparent, BorderColor = glow, DrawCenter = false };
		frame.SetBorderWidthAll(3);
		frame.SetCornerRadiusAll(10);
		DrawStyleBox(frame, h);
	}

	public override void _Input(InputEvent e)
	{
		if (e is not InputEventKey { Pressed: true, Echo: false } key) return;
		switch (key.Keycode)
		{
			case Key.Escape: Close(); break;
			case Key.Right or Key.Enter or Key.KpEnter or Key.Space: Next(1); break;
			case Key.Left: Next(-1); break;
			default: return;
		}
		GetViewport().SetInputAsHandled();
	}
}
