// Applies the saved settings (Settings.cs) to the engine: audio buses, window, language.

using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using QuizNight.Shared;

namespace QuizNight.Storage;

public static class Prefs
{
	/// <summary>Bus for songs and clips in sound/video questions.</summary>
	public const string MusicBus = "Music";
	/// <summary>Bus for the buzzer and other sound effects.</summary>
	public const string SfxBus = "SFX";
	/// <summary>Bus for the game's own music: menu music and the podium jingle.</summary>
	public const string GameMusicBus = "GameMusic";

	/// <summary>Common window sizes by aspect ratio (the UI stretches to any of them).</summary>
	public static readonly Vector2I[] WindowSizes =
	{
		new(1280, 720), new(1600, 900), new(1920, 1080), new(2560, 1440), new(3840, 2160),   // 16:9
		new(1280, 800), new(1680, 1050), new(1920, 1200), new(2560, 1600),                   // 16:10
		new(2560, 1080), new(3440, 1440), new(5120, 2160),                                   // 21:9
		new(3840, 1080), new(5120, 1440),                                                    // 32:9
		new(1024, 768), new(1280, 960), new(1600, 1200),                                     // 4:3
	};

	/// <summary>"16:9", "21:9" …, by the closest common ratio (3440 × 1440 is sold as 21:9).</summary>
	public static string AspectName(Vector2I size)
	{
		var r = (double)size.X / size.Y;
		var named = new[] { ("4:3", 4 / 3.0), ("16:10", 1.6), ("16:9", 16 / 9.0), ("21:9", 2.37), ("32:9", 32 / 9.0) };
		return named.OrderBy(n => Math.Abs(n.Item2 - r)).First().Item1;
	}

	/// <summary>The monitors, numbered from 0, with their resolution.</summary>
	public static int ScreenCount => DisplayServer.GetScreenCount();
	public static Vector2I ScreenSize(int screen) => DisplayServer.ScreenGetSize(Math.Clamp(screen, 0, ScreenCount - 1));

	/// <summary>A saved screen that still exists (a monitor may have been unplugged), else the main one.</summary>
	public static int ValidScreen(int screen) => screen >= 0 && screen < ScreenCount ? screen : DisplayServer.GetPrimaryScreen();

	/// <summary>Window sizes that fit on this monitor (never bigger than its resolution), its own resolution included.</summary>
	public static List<Vector2I> SizesFor(int screen)
	{
		var native = ScreenSize(ValidScreen(screen));
		var list = WindowSizes.Where(s => s.X <= native.X && s.Y <= native.Y).ToList();
		if (!list.Contains(native)) list.Add(native);
		return list.OrderBy(s => AspectName(s)).ThenBy(s => s.X).ToList();
	}

	/// <summary>At startup. Leaves the window alone when it can't or shouldn't be changed (headless, --resolution).</summary>
	public static void ApplyAll(bool window)
	{
		Lang.Use(Settings.Language);
		EnsureBuses();
		ApplyAudio();
		if (window) ApplyWindow();
	}

	static void EnsureBuses()
	{
		foreach (var name in new[] { MusicBus, GameMusicBus, SfxBus })
		{
			if (AudioServer.GetBusIndex(name) >= 0) continue;
			AudioServer.AddBus();
			var i = AudioServer.BusCount - 1;
			AudioServer.SetBusName(i, name);
			AudioServer.SetBusSend(i, "Master");
		}
	}

	/// <summary>The saved volumes, or <paramref name="preview"/> (the settings screen's unsaved ones).</summary>
	public static void ApplyAudio(Settings.Values? preview = null)
	{
		var v = preview ?? Settings.Current;
		SetVolume("Master", v.Master);
		SetVolume(MusicBus, v.Music);
		SetVolume(GameMusicBus, v.GameMusic);
		SetVolume(SfxBus, v.Sfx);
	}

	static void SetVolume(string bus, int percent)
	{
		var i = AudioServer.GetBusIndex(bus);
		if (i < 0) return;
		AudioServer.SetBusMute(i, percent <= 0);
		AudioServer.SetBusVolumeDb(i, Mathf.LinearToDb(Mathf.Max(percent, 1) / 100f));
	}

	public static void ApplyWindow()
	{
		var window = ((SceneTree)Engine.GetMainLoop()).Root;
		var target = ValidScreen(Settings.Screen);
		// Moving to another monitor only works for a normal window: leave fullscreen first.
		if (window.CurrentScreen != target)
		{
			window.Mode = Window.ModeEnum.Windowed;
			window.CurrentScreen = target;
		}
		switch (Settings.WindowMode)
		{
			case "fullscreen":
				window.Mode = Window.ModeEnum.ExclusiveFullscreen;
				break;
			case "borderless":
				window.Mode = Window.ModeEnum.Fullscreen;
				break;
			default:
				window.Mode = Window.ModeEnum.Windowed;
				var screen = DisplayServer.ScreenGetUsableRect(target);
				var size = Settings.WindowSize;
				// Never bigger than the screen (taskbar and title bar included).
				size = new Vector2I(Mathf.Min(size.X, screen.Size.X), Mathf.Min(size.Y, screen.Size.Y - 40));
				window.Size = size;
				window.Position = screen.Position + (screen.Size - size) / 2;
				break;
		}
	}
}
