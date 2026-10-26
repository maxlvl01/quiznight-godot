// The jukebox: a note button in the bottom left of every screen. It opens a small panel to switch the music off (saved,
// so it stays off next time) and on, skip to the next or previous song, or pick one from what this screen plays
// (UiAudio's songs for its place). The "Now playing" card slides in beside it.

using System.Linq;
using Godot;
using QuizNight.Shared;
using QuizNight.Storage;

namespace QuizNight.Ui;

public partial class Jukebox : Control
{
	const float ButtonSize = 44;
	const float Margin = 20;
	/// <summary>Where the button ends on the left: the "Now playing" card starts after it.</summary>
	public const float Right = Margin + ButtonSize;

	readonly Button _button;
	readonly NoteIcon _icon = new();
	readonly PanelContainer _panel;
	readonly Label _title = U.Text("", 15, C.Text, Fonts.Bold).Raw();
	readonly Label _artist = U.Muted("", 12).Raw();
	readonly Button _toggle;
	readonly VBoxContainer _list = U.V(2);

	static UiAudio Audio => App.I.Audio;

	public static Jukebox? I { get; private set; }

	/// <summary>The note button in the corner (hidden on the home screen, which has its own music player).</summary>
	public bool ButtonShown
	{
		get => _button.Visible;
		set
		{
			_button.Visible = value;
			if (!value) Open(false);
		}
	}

	public Jukebox()
	{
		I = this;
		SetAnchorsPreset(LayoutPreset.FullRect);
		MouseFilter = MouseFilterEnum.Ignore;

		_button = new Button
		{
			ThemeTypeVariation = "SmallButton", FocusMode = FocusModeEnum.None, Size = new Vector2(ButtonSize, ButtonSize),
			TooltipText = "Jukebox", AccessibilityName = Lang.T("Jukebox"), MouseDefaultCursorShape = CursorShape.PointingHand,
		};
		_button.SetAnchorsAndOffsetsPreset(LayoutPreset.BottomLeft, LayoutPresetMode.KeepSize, (int)Margin);
		_icon.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect, LayoutPresetMode.Minsize, 6);
		_button.AddChild(_icon);
		_button.Pressed += () => Open(!_panel!.Visible);
		_button.SetMeta(UiAudio.NoBounce, true);

		_toggle = MediaIcon.Button(MediaIcon.Kind.Pause, "Turn music off", () =>
		{
			Audio.SetMusicOn(!Settings.MusicOn);
			Refresh();
		}, 40);
		var controls = U.H(8,
			MediaIcon.Button(MediaIcon.Kind.Previous, "Previous song", () => Audio.Skip(-1), 40),
			_toggle,
			MediaIcon.Button(MediaIcon.Kind.Next, "Next song", () => Audio.Skip(1), 40)).Center();
		var scroll = new ScrollContainer { CustomMinimumSize = new Vector2(0, 200), HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
		scroll.AddChild(_list.With(l => l.SizeFlagsHorizontal = SizeFlags.ExpandFill));
		_panel = U.Panel(U.V(10, U.Eyebrow("Jukebox"), U.V(0, _title, _artist), controls, new HSeparator(), scroll), "Clear");
		_panel.AddThemeStyleboxOverride("panel", UiTheme.Box(C.Panel, 14, C.Border, 2, 14, 12));
		_panel.CustomMinimumSize = new Vector2(340, 0);
		_panel.Visible = false;
		_panel.SetAnchorsPreset(LayoutPreset.BottomLeft);
		_panel.GrowVertical = GrowDirection.Begin;
		_panel.OffsetLeft = Margin;
		_panel.OffsetBottom = _panel.OffsetTop = -(Margin + ButtonSize + 10);
		AddChild(_panel);
		AddChild(_button);
	}

	public override void _Ready()
	{
		Audio.TrackStarted += _ => Refresh();
		Refresh();
		// (dev runs: --jukebox opens it, for screenshots)
		if (OS.GetCmdlineUserArgs().Contains("--jukebox")) Callable.From(() => Open(true)).CallDeferred();
	}

	public void Open(bool open)
	{
		_panel.Visible = open;
		if (open) Refresh();
	}

	/// <summary>The song, the on/off button and the list as they are now (the list changes with the screen's place).</summary>
	void Refresh()
	{
		_icon.Off = !Settings.MusicOn;
		MediaIcon.Of(_toggle).Shape = Settings.MusicOn ? MediaIcon.Kind.Pause : MediaIcon.Kind.Play;
		_toggle.TooltipText = Settings.MusicOn ? "Turn music off" : "Turn music on";
		var current = Audio.CurrentSong;
		if (current == null)
		{
			_title.Text = Lang.T("No music on this screen.");
			_artist.Text = "";
		}
		else
		{
			var (title, artist) = UiAudio.Describe(current);
			_title.Text = title;
			_artist.Text = Settings.MusicOn ? artist : Lang.T("Music is off");
		}
		if (!_panel.Visible) return;
		foreach (var child in _list.GetChildren()) child.QueueFree();
		var songs = Audio.Songs.Select((path, i) => (path, i, UiAudio.Describe(path))).OrderBy(s => s.Item3.Title).ToList();
		foreach (var (path, i, (title, artist)) in songs)
		{
			var playing = path == current && Settings.MusicOn;
			var row = U.Link(artist != "" ? $"{title} · {artist}" : title, () => Audio.PlaySong(i), 14).Raw();
			row.Alignment = HorizontalAlignment.Left;
			row.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
			row.ClipText = true;
			row.Grow();
			if (playing) row.AddThemeColorOverride("font_color", C.Accent);
			// (a drawn note marks the song playing; the others keep its space, so the titles line up)
			var note = new MediaIcon(MediaIcon.Kind.Note) { Color = playing ? C.Accent : Colors.Transparent, CustomMinimumSize = new Vector2(14, 14), SizeFlagsVertical = SizeFlags.ShrinkCenter };
			_list.AddChild(U.H(6, note, row));
		}
	}

	// (a click anywhere else closes the panel; the click still goes where it was meant)
	public override void _Input(InputEvent e)
	{
		if (!_panel.Visible || e is not InputEventMouseButton { Pressed: true } click) return;
		if (!_panel.GetGlobalRect().HasPoint(click.Position) && !_button.GetGlobalRect().HasPoint(click.Position)) Open(false);
	}

	public override void _UnhandledKeyInput(InputEvent e)
	{
		if (!_panel.Visible || e is not InputEventKey { Pressed: true, Keycode: Key.Escape }) return;
		Open(false);
		GetViewport().SetInputAsHandled();
	}
}
