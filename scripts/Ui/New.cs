// Yellow dots on what's new since the player's last update (a new option in Settings, say), so they can find it.
// What counts: each entry below came in a version; it's new while that version is newer than the one the player had
// before updating (Settings.NewSince, noted at startup before "What's new" moves Settings.LastSeenVersion on). "Mark all
// as read" on the home screen clears them all. A fresh install has nothing new: everything is.

using System.Linq;
using Godot;
using QuizNight.Shared;
using QuizNight.Storage;

namespace QuizNight.Ui;

public static class New
{
	/// <summary>Everything that gets a dot: its key (a place.thing name) and the version it came in.</summary>
	static readonly (string Key, string Since)[] Items =
	{
		("settings.intro", "0.12.0d1"),
		("settings.microphone", "0.12.0d4"),
		("settings.micvolume", "0.12.0d5"),
		("settings.background", "0.12.0d5"),
	};

	/// <summary>At startup, before "What's new": an update since the last run makes what came since then new.</summary>
	public static void NoteUpdate()
	{
		var last = Settings.LastSeenVersion;
		if (last == "") Settings.NewSince = App.Version;
		else if (AppVersion.Parse(last) is { } was && AppVersion.Parse(App.Version) is { } now && now > was) Settings.NewSince = last;
	}

	/// <summary>Whether the thing (or anything under it: "settings." for every new setting) is new for this player.</summary>
	public static bool IsNew(string key)
	{
		if (AppVersion.Parse(Settings.NewSince) is not { } since) return false;
		return Items.Any(i => (i.Key == key || key.EndsWith('.') && i.Key.StartsWith(key)) && AppVersion.Parse(i.Since) is { } v && v > since);
	}

	public static bool Any => Items.Any(i => IsNew(i.Key));

	/// <summary>Nothing is new any more (home screen's "Mark all as read").</summary>
	public static void MarkAllRead() => Settings.NewSince = App.Version;

	/// <summary>A yellow dot in the control's top right corner while the thing is new; returns the control.</summary>
	public static T Mark<T>(T control, string key) where T : Control
	{
		if (!IsNew(key)) return control;
		control.Draw += () =>
		{
			if (!IsNew(key)) return;
			var at = new Vector2(control.Size.X - 5, 5);
			control.DrawCircle(at, 5.5f, C.Bg);
			control.DrawCircle(at, 4f, C.Gold);
		};
		var tip = Lang.T("New since your last update");
		control.TooltipText = control.TooltipText == "" ? tip : control.TooltipText + "\n" + tip;
		control.QueueRedraw();
		return control;
	}
}
