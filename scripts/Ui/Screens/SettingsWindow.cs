// Settings as a window over a lobby or a running game (the gear in their top bar), so changing the volume or the window
// size doesn't mean leaving the room: the Settings screen opened full-screen from Home would replace the game.
// Close, Escape or the mouse's back button close it (asking first about changes not applied); clicks outside do nothing.

using Godot;

namespace QuizNight.Ui.Screens;

public partial class SettingsWindow : Control
{
	/// <summary>The window open now, if any.</summary>
	public static SettingsWindow? Current { get; private set; }

	readonly PanelContainer _panel;
	SettingsScreen _settings = null!;

	public SettingsWindow()
	{
		SetAnchorsPreset(LayoutPreset.FullRect);
		MouseFilter = MouseFilterEnum.Stop;
		FocusMode = FocusModeEnum.All;
		var dim = new ColorRect { Color = new Color(0, 0, 0, 0.6f), MouseFilter = MouseFilterEnum.Ignore };
		dim.SetAnchorsPreset(LayoutPreset.FullRect);
		AddChild(dim);
		_panel = U.Panel(new Control(), "Clear");
		_panel.AddThemeStyleboxOverride("panel", UiTheme.Chunky(C.Panel, 22, 0, 0, 4, 7));
		_panel.ClipContents = true;
		AddChild(_panel);
		Fill();
	}

	/// <summary>Opens the window over everything (or leaves the one already open).</summary>
	public static void Open()
	{
		if (Current != null && IsInstanceValid(Current)) return;
		Current = new SettingsWindow();
		App.I.AddChild(Current);
	}

	/// <summary>The settings built again (a language change switches all text).</summary>
	public static void Rebuild() => Current?.Fill();

	public static void Close()
	{
		Current?.QueueFree();
		Current = null;
	}

	void Fill()
	{
		foreach (var child in _panel.GetChildren()) child.QueueFree();
		_settings = new SettingsScreen(true);
		_panel.AddChild(_settings);
	}

	/// <summary>Escape or the mouse's back button: close (asking first about changes not applied).</summary>
	public bool Leave() => _settings.Leave();

	/// <summary>Ctrl+S: Apply.</summary>
	public bool ApplyKey() => _settings.ApplyKey();

	public override void _Ready()
	{
		Place();
		// (the window holds the keyboard focus from the start, invisibly: else a lobby or game screen rebuilt underneath could
		// hand it to a control further down in here, and the page scrolled down to it)
		CallDeferred(Control.MethodName.GrabFocus);
	}

	public override void _Notification(int what)
	{
		if (what == NotificationResized) Place();
	}

	// (nearly the whole window, at most as wide as the Settings screen's page)
	void Place()
	{
		var size = new Vector2(Mathf.Min(1040, Size.X - 48), Size.Y - 48);
		_panel.Position = (Size - size) / 2;
		_panel.Size = size;
	}
}
