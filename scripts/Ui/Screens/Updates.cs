using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Godot;
using QuizNight.Shared;
using QuizNight.Storage;

namespace QuizNight.Ui.Screens;

/// <summary>
/// "What's new": the changelog of the versions since the one last opened, grouped into New / Changed /
/// Fixed / Removed. Shown once after an update (or a first install), and from the version link on Home.
/// </summary>
public static class WhatsNew
{
	const string ChangelogPath = "res://CHANGELOG.md";

	static List<Changelog.Release> Read()
	{
		using var file = Godot.FileAccess.Open(ChangelogPath, Godot.FileAccess.ModeFlags.Read);
		return file == null ? new List<Changelog.Release>() : Changelog.Parse(file.GetAsText());
	}

	/// <summary>After an update: shows what changed since the last version opened, then remembers this one.</summary>
	public static void ShowIfUpdated(Node owner)
	{
		var current = AppVersion.Parse(App.Version);
		var seen = AppVersion.Parse(Settings.LastSeenVersion);
		if (current is not { } now || seen is { } s && !(now > s)) return;
		var releases = Changelog.Between(Read(), seen, now);
		Settings.LastSeenVersion = App.Version;
		if (releases.Count > 0) Show(owner, releases.Take(3).ToList());
	}

	/// <summary>The notes of this version (from the version link on Home).</summary>
	public static void ShowCurrent(Node owner)
	{
		var releases = Read();
		var current = releases.FirstOrDefault(r => r.Version is { } v && AppVersion.Parse(App.Version) is { } now && v.CompareTo(now) == 0) ?? releases.FirstOrDefault();
		if (current != null) Show(owner, new List<Changelog.Release> { current });
	}

	static void Show(Node owner, List<Changelog.Release> releases)
	{
		var overlay = new VideoGuide.GuideOverlay();
		var box = U.V(16);
		box.AddChild(U.Eyebrow("What's new"));
		box.AddChild(U.Heading(Lang.F("Quiznight? {0}", App.Version), 32, false));
		foreach (var release in releases)
		{
			if (releases.Count > 1 || release.Title != "")
				box.AddChild(U.Bold($"{release.Shown}" + (release.Title != "" ? $" — {release.Title}" : ""), 18).Raw());
			foreach (var section in release.Sections.Where(s => s.Items.Count > 0))
			{
				var (label, color) = Kind(section.Kind);
				box.AddChild(U.Pill(label, color).With(p => p.SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin));
				var list = U.V(10);
				foreach (var item in section.Items) list.AddChild(Bullet(item, color));
				box.AddChild(list);
			}
		}
		box.AddChild(U.Btn("Let's go!", "AccentButton", overlay.QueueFree, 18).With(b => b.SizeFlagsHorizontal = Control.SizeFlags.ShrinkEnd));
		overlay.SetContent(U.Pad(box, 28, 26, 28, 26));
		owner.AddChild(overlay);
	}

	static (string Label, Color Color) Kind(string kind) => kind.ToLowerInvariant() switch
	{
		"added" => ("New", C.Teal),
		"changed" => ("Changed", C.Sky),
		"fixed" => ("Fixed", C.Accent),
		"removed" => ("Removed", C.Coral),
		// (a peek at the next big update, at the end of a release's notes)
		"coming next" => ("Coming next", C.Purple),
		_ => (kind, C.Muted),
	};

	/// <summary>One point: coloured dot, text with **bold** and `code` from the markdown.</summary>
	static Control Bullet(string markdown, Color color)
	{
		var text = Regex.Replace(markdown.Replace("[", "[lb]"), @"\*\*(.+?)\*\*", "[b]$1[/b]");
		text = Regex.Replace(text, "`(.+?)`", "[code]$1[/code]");
		var label = new RichTextLabel { BbcodeEnabled = true, FitContent = true, ScrollActive = false, Text = text, AutoTranslateMode = Node.AutoTranslateModeEnum.Disabled };
		label.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		label.AddThemeFontSizeOverride("normal_font_size", 16);
		label.AddThemeFontSizeOverride("bold_font_size", 16);
		label.AddThemeFontOverride("bold_font", Fonts.Bold);
		label.AddThemeColorOverride("default_color", C.Text);
		var dot = new Panel { CustomMinimumSize = new Vector2(8, 8), SizeFlagsVertical = Control.SizeFlags.ShrinkBegin };
		dot.AddThemeStyleboxOverride("panel", UiTheme.Box(color, 4, null, 0, 0, 0));
		return U.H(12, U.Pad(dot, 0, 8, 0, 0), label);
	}
}

/// <summary>
/// "Update to 0.9.4b" in the top bar of the quiz screens and the question editor, once a newer version is out (hidden
/// until then). It warns first: the game restarts to update, and a question that isn't saved yet would be lost.
/// </summary>
public partial class UpdateBadge : Button
{
	public UpdateBadge()
	{
		ThemeTypeVariation = "TealButton";
		AddThemeFontSizeOverride("font_size", 14);
		MouseDefaultCursorShape = CursorShape.PointingHand;
		SizeFlagsVertical = SizeFlags.ShrinkCenter;
		Pressed += () =>
		{
			if (UpdatePrompt.Available is not { } release) return;
			Dialogs.Confirm(this, Lang.F("Quiznight? {0} is out. The game restarts to update: save what you're working on first, unsaved changes are lost.", release.Version),
				"Update now", () => UpdatePrompt.Install(GetTree().Root, release), "Update");
		};
		Show();
	}

	public override void _EnterTree() => UpdatePrompt.Changed += Show;
	public override void _ExitTree() => UpdatePrompt.Changed -= Show;

	new void Show()
	{
		Visible = UpdatePrompt.Available != null;
		if (UpdatePrompt.Available is { } release)
		{
			Text = Lang.F("Update to {0}", release.Version);
			TooltipText = Lang.T("A new version is out: click to update (the game restarts)");
		}
	}
}

/// <summary>At startup (exported builds): looks for a newer version and installs it, restarting the game.</summary>
public static class UpdatePrompt
{
	/// <summary>A newer version, once a check found one (the home screen shows it: the logo's outline pulses green, a green arrow).</summary>
	public static Updater.Release? Available { get; private set; }
	public static bool Checking { get; private set; }
	public static event Action? Changed;

	static void Set(Updater.Release? release, bool checking)
	{
		Available = release;
		Checking = checking;
		Changed?.Invoke();
	}

	/// <summary>At startup: if there's a newer version, install it (it can be put off with "Not now").</summary>
	public static async void CheckAndInstall(Node owner)
	{
		if (!Updater.Enabled) return;
		Set(Available, true);
		var release = await Updater.CheckAsync();
		Set(release, false);
		if (release == null || !GodotObject.IsInstanceValid(owner)) return;
		// Not in the middle of a game: only from the home screen.
		if (App.I.Host != null || App.I.Client != null) return;
		Install(owner, release);
	}

	/// <summary>The "Check for updates" button: look now; tells you if you already have the newest version.</summary>
	public static async void CheckNow(Node owner)
	{
		if (Checking) return;
		Set(Available, true);
		var release = Updater.Enabled ? await Updater.CheckAsync() : null;
		Set(release ?? Available, false);
		if (!GodotObject.IsInstanceValid(owner)) return;
		if (Available == null) Dialogs.Info(owner, Updater.Enabled ? Lang.F("You have the newest version ({0}).", App.Version) : "Updates only work in the installed game.", "Update");
	}

	/// <summary>
	/// Every minute while the game is open: is there a newer version? Asks the releases page (no limit), and only when
	/// it has a newer number GitHub's API for the download. Quiet: no dialog, the home screen and editors show a button.
	/// </summary>
	public static async void CheckQuietly()
	{
		if (!Updater.Enabled || Checking || Available != null || OS.GetEnvironment("QUIZNIGHT_UPDATE_FEED") != "") return;
		if (await Updater.LatestVersionAsync() is not { } latest || AppVersion.Parse(App.Version) is not { } current || !(latest > current)) return;
		if (await Updater.CheckAsync() is { } release) Set(release, false);
	}

	/// <summary>A test run (dev tools): the home screen as if a newer version were out.</summary>
	public static void Pretend(string version) =>
		Set(AppVersion.Parse(version) is { } v ? new Updater.Release(v, "", 0, null) : null, false);

	/// <summary>Only pretended (dev tools): there's nothing to download.</summary>
	public static bool IsPretend(Updater.Release release) => release.ZipUrl == "";

	/// <summary>Downloads and installs the release, then restarts.</summary>
	public static async void Install(Node owner, Updater.Release release)
	{
		if (!Updater.Enabled || IsPretend(release)) return;
		var overlay = new VideoGuide.GuideOverlay { Closable = false };
		var status = U.Muted(Lang.F("Downloading version {0}…", release.Version), 15);
		var bar = new ProgressBar { MinValue = 0, MaxValue = 1, Step = 0, ShowPercentage = false, CustomMinimumSize = new Vector2(0, 10) };
		bar.AddThemeStyleboxOverride("background", UiTheme.Box(C.Panel2, 5, null, 0, 0, 0));
		bar.AddThemeStyleboxOverride("fill", UiTheme.Box(C.Accent, 5, null, 0, 0, 0));
		var error = U.Error();
		var later = U.Small("Not now", overlay.QueueFree);
		var box = U.V(14,
			U.Eyebrow("Update"),
			U.Heading(Lang.F("Quiznight? {0} is here!", release.Version), 30, false),
			U.Muted(Lang.F("You have {0}. The game downloads the new version, restarts and then shows you what's new.", App.Version), 15),
			bar, status, error, later.With(b => b.SizeFlagsHorizontal = Control.SizeFlags.ShrinkEnd));
		overlay.SetContent(U.Pad(box, 28, 26, 28, 26));
		owner.AddChild(overlay);
		try
		{
			await Updater.InstallAsync(release, p =>
			{
				bar.Value = p;
				if (p >= 1) status.Text = Lang.T("Restarting…");
			});
			later.Disabled = true;
		}
		catch (Exception e)
		{
			error.Text = Lang.F("The update didn't work: {0} You can keep playing this version.", e.Message);
			error.Visible = true;
			status.Visible = false;
		}
	}
}
