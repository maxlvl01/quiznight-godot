// Applies the saved settings (Settings.cs) to the engine: audio buses, window, language.

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

	public static readonly Vector2I[] WindowSizes =
	{
		new(1280, 720), new(1600, 900), new(1920, 1080), new(2560, 1440), new(3840, 2160),
	};

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

	public static void ApplyAudio()
	{
		SetVolume("Master", Settings.MasterVolume);
		SetVolume(MusicBus, Settings.MusicVolume);
		SetVolume(GameMusicBus, Settings.GameMusicVolume);
		SetVolume(SfxBus, Settings.SfxVolume);
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
				var screen = DisplayServer.ScreenGetUsableRect(window.CurrentScreen);
				var size = Settings.WindowSize;
				// Never bigger than the screen (taskbar and title bar included).
				size = new Vector2I(Mathf.Min(size.X, screen.Size.X), Mathf.Min(size.Y, screen.Size.Y - 40));
				window.Size = size;
				window.Position = screen.Position + (screen.Size - size) / 2;
				break;
		}
	}
}
