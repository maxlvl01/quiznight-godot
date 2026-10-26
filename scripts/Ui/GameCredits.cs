// The credits: who made the music, the maps, the engine and the fonts, and their licences. Two looks: CreditsWall, the
// intro's logo wall (like racing games show the car makers: names in chunky tiles, a few seconds), and CreditsWindow, the
// full list with the licence texts (the "i" in the home music player).

using System.Collections.Generic;
using System.Linq;
using Godot;
using QuizNight.Shared;

namespace QuizNight.Ui;

public static class GameCredits
{
	/// <summary>The names on the intro's wall: whose work is in the game (engine, Steam, the maps' games). The music has its own card.</summary>
	public static readonly string[] Wall =
	{
		"Godot Engine", "Steamworks", "Bethesda", "Rockstar Games", "Nintendo", "FromSoftware",
		"Bandai Namco", "CD PROJEKT RED", "Playground Games", "Xbox Game Studios", "KRAFTON", "Blizzard Entertainment",
	};

	/// <summary>Every song the game can play (also the seasonal ones), as title and artist.</summary>
	public static List<(string Title, string Artist)> Songs() =>
		UiAudio.SongsIn("res://assets/music").Concat(UiAudio.SongsIn("res://assets/music/christmas"))
			.Concat(UiAudio.SongsIn("res://assets/music/halloween"))
			.Select(UiAudio.Describe).OrderBy(s => s.Title).ToList();

	/// <summary>A text file shipped with the game (the fonts' licences), or "" if it's missing.</summary>
	public static string File(string path) => Godot.FileAccess.FileExists(path) ? Godot.FileAccess.GetFileAsString(path).Trim() : "";

	public const string Disclaimer = "All names, games and trademarks belong to their owners. Quiznight? is a fan-made game for friends, not affiliated with or endorsed by any of them.";
}

/// <summary>The intro's credits card: the names in tiles, the trademark note underneath.</summary>
public partial class CreditsWall : VBoxContainer
{
	public CreditsWall()
	{
		Alignment = AlignmentMode.Center;
		AddThemeConstantOverride("separation", 22);
		AddChild(U.Text(Lang.T("With maps and tech from").ToUpper(), 15, C.Muted, Fonts.Spaced, false, HorizontalAlignment.Center));
		var grid = new HFlowContainer { Alignment = FlowContainer.AlignmentMode.Center, CustomMinimumSize = new Vector2(980, 0), SizeFlagsHorizontal = SizeFlags.ShrinkCenter };
		grid.AddThemeConstantOverride("h_separation", 14);
		grid.AddThemeConstantOverride("v_separation", 14);
		foreach (var name in GameCredits.Wall)
		{
			var tile = U.Panel(U.Text(name, 22, C.Text, Fonts.Black, false, HorizontalAlignment.Center).Raw(), "Clear");
			tile.AddThemeStyleboxOverride("panel", UiTheme.Chunky(C.Panel2, 14, 22, 12));
			grid.AddChild(tile);
		}
		AddChild(grid);
		AddChild(U.Muted(GameCredits.Disclaimer, 12, true, HorizontalAlignment.Center).With(l => l.CustomMinimumSize = new Vector2(760, 0)).With(l => l.SizeFlagsHorizontal = SizeFlags.ShrinkCenter));
	}
}

/// <summary>The full credits in a window over the screen: music, maps, the game's tech and fonts, the licence texts.</summary>
public partial class CreditsWindow : Control
{
	public static CreditsWindow? Current { get; private set; }

	readonly PanelContainer _panel;

	public CreditsWindow()
	{
		SetAnchorsPreset(LayoutPreset.FullRect);
		MouseFilter = MouseFilterEnum.Stop;
		var dim = new ColorRect { Color = new Color(0, 0, 0, 0.6f), MouseFilter = MouseFilterEnum.Ignore };
		dim.SetAnchorsPreset(LayoutPreset.FullRect);
		AddChild(dim);
		_panel = U.Panel(Build(), "Clear");
		_panel.AddThemeStyleboxOverride("panel", UiTheme.Chunky(C.Panel, 22, 0, 0, 4, 7));
		_panel.ClipContents = true;
		AddChild(_panel);
	}

	public static void Open()
	{
		if (Current != null && IsInstanceValid(Current)) return;
		Current = new CreditsWindow();
		App.I.AddChild(Current);
	}

	public static void Close()
	{
		Current?.QueueFree();
		Current = null;
	}

	static Control Section(string title, params Control[] rows) => U.V(8, new Control[] { U.Heading(title, 24) }.Concat(rows).ToArray());

	static Control Line(string name, string detail) =>
		U.V(0, U.Bold(name, 16, C.Text, true).Raw(), U.Muted(detail, 13).Raw());

	static Control Licence(string title, string text) =>
		U.V(4, U.Bold(title, 14, C.Text).Raw(), U.Panel(U.Mono(text, 11, C.Muted).With(l => l.AutowrapMode = TextServer.AutowrapMode.WordSmart).Raw(), "Clear")
			.With(p => p.AddThemeStyleboxOverride("panel", UiTheme.Box(C.Panel2, 10, null, 0, 12, 10))));

	static Control Build()
	{
		var songs = GameCredits.Songs().Select(s => Line(s.Title, s.Artist)).ToArray();
		var maps = MapPresets.All.Select(m => Line(m.Name, m.Credit)).ToArray();
		var body = U.V(26,
			U.H(12, U.Heading("Credits", 34).Grow(), U.Btn("Close", "", Close)),
			U.Muted("Quiznight? by maxlvl01 — made with friends, for friends. Published by Pinke Geilheit Publishing.", 15),
			Section("Music", new Control[] { U.Muted("All music and sound effects are licensed from Epidemic Sound (epidemicsound.com). They may only be used in Quiznight? and its streams and videos.", 14) }.Concat(songs).ToArray()),
			Section("Maps", maps),
			Section("Tech",
				Line("Godot Engine", "godotengine.org — MIT licence. Thanks to the Godot contributors."),
				Line("Godot logo", "By Andrea Calabró, CC BY 4.0 (creativecommons.org/licenses/by/4.0)."),
				Line("Steamworks", "Steam networking, lobbies and avatars © Valve Corporation; Facepunch.Steamworks (MIT)."),
				Line("TrackGlow", "Sponsor — patreon.com/cw/IBassGaming"),
				Line("AnyConverter", "The sound and video converter that comes with the game, by maxlvl01.")),
			Section("Fonts",
				Line("Fredoka", "SIL Open Font License 1.1"),
				Line("Lilita One", "SIL Open Font License 1.1"),
				Line("OpenDyslexic", "SIL Open Font License 1.1")),
			U.Muted(GameCredits.Disclaimer, 13),
			Section("Licences",
				Licence("Quiznight?", GameCredits.File("res://LICENSE")),
				Licence("Godot Engine", Engine.GetLicenseText()),
				Licence("Fredoka", GameCredits.File("res://assets/fonts/OFL-fredoka.txt")),
				Licence("Lilita One", GameCredits.File("res://assets/fonts/OFL-lilitaone.txt")),
				Licence("OpenDyslexic", GameCredits.File("res://assets/fonts/OFL-opendyslexic.txt"))));
		var scroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
		var pad = new MarginContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
		foreach (var side in new[] { "left", "right", "top", "bottom" }) pad.AddThemeConstantOverride($"margin_{side}", 32);
		pad.AddChild(body);
		scroll.AddChild(pad);
		return scroll;
	}

	public override void _Ready() => Place();

	public override void _Notification(int what)
	{
		if (what == NotificationResized) Place();
	}

	void Place()
	{
		var size = new Vector2(Mathf.Min(900, Size.X - 48), Size.Y - 48);
		_panel.Position = (Size - size) / 2;
		_panel.Size = size;
	}
}
