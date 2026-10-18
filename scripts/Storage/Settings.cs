// Small things remembered between sessions (user://settings.cfg).

using Godot;
using QuizNight.Net;
using QuizNight.Shared;

namespace QuizNight.Storage;

public static class Settings
{
	const string FilePath = "user://settings.cfg";
	static ConfigFile? _cfg;

	static ConfigFile Cfg
	{
		get
		{
			if (_cfg != null) return _cfg;
			_cfg = new ConfigFile();
			_cfg.Load(FilePath); // missing on first start: fine
			return _cfg;
		}
	}

	static string Get(string key, string fallback) => Cfg.GetValue("app", key, fallback).AsString();

	static void Set(string key, string value)
	{
		Cfg.SetValue("app", key, value);
		Cfg.Save(FilePath);
	}

	public static string PlayerName
	{
		get => Get("player_name", "");
		set => Set("player_name", value);
	}

	public static string LanAddress
	{
		get => Get("lan_address", "127.0.0.1");
		set => Set("lan_address", value);
	}

	public static NetMode Mode
	{
		get => Get("net_mode", "steam") == "lan" ? NetMode.Lan : NetMode.Steam;
		set => Set("net_mode", value == NetMode.Lan ? "lan" : "steam");
	}

	// ---------- settings screen ----------

	/// <summary>Everything on the settings screen as one value: the screen edits a copy and saves it with Apply.</summary>
	public sealed record Values(string Language, int Master, int Music, int GameMusic, int Sfx, string Buzzer, string WindowMode, Vector2I WindowSize, int Screen);

	public static Values Defaults => new(Lang.SystemDefault, 80, 80, 60, 100, Storage.Sfx.DefaultBuzzer, "windowed", new Vector2I(1600, 900), DisplayServer.GetPrimaryScreen());

	public static Values Current => new(Language, MasterVolume, MusicVolume, GameMusicVolume, SfxVolume, BuzzerSound, WindowMode, WindowSize, Screen);

	public static void Save(Values v)
	{
		Language = v.Language;
		MasterVolume = v.Master;
		MusicVolume = v.Music;
		GameMusicVolume = v.GameMusic;
		SfxVolume = v.Sfx;
		BuzzerSound = v.Buzzer;
		WindowMode = v.WindowMode;
		WindowSize = v.WindowSize;
		Screen = v.Screen;
	}

	static int GetPercent(string key, int fallback) => int.TryParse(Get(key, ""), out var v) ? Mathf.Clamp(v, 0, 100) : fallback;

	public static string Language
	{
		get => Get("language", Lang.SystemDefault);
		set => Set("language", value);
	}

	public static int MasterVolume
	{
		get => GetPercent("volume_master", Defaults.Master);
		set => Set("volume_master", value.ToString());
	}

	public static int MusicVolume
	{
		get => GetPercent("volume_music", Defaults.Music);
		set => Set("volume_music", value.ToString());
	}

	public static int GameMusicVolume
	{
		get => GetPercent("volume_game_music", Defaults.GameMusic);
		set => Set("volume_game_music", value.ToString());
	}

	public static int SfxVolume
	{
		get => GetPercent("volume_sfx", Defaults.Sfx);
		set => Set("volume_sfx", value.ToString());
	}

	/// <summary>Default buzzer sound for new lobbies (an Sfx id).</summary>
	public static string BuzzerSound
	{
		get => Sfx.Valid(Get("buzzer_sound", Sfx.DefaultBuzzer));
		set => Set("buzzer_sound", value);
	}

	/// <summary>"windowed", "fullscreen" (exclusive) or "borderless" (borderless fullscreen window).</summary>
	public static string WindowMode
	{
		get => Get("window_mode", Defaults.WindowMode);
		set => Set("window_mode", value);
	}

	/// <summary>Window size in windowed mode, e.g. "1600x900".</summary>
	public static Vector2I WindowSize
	{
		get
		{
			var parts = Get("window_size", "1600x900").Split('x');
			return parts.Length == 2 && int.TryParse(parts[0], out var w) && int.TryParse(parts[1], out var h) && w >= 640 && h >= 360
				? new Vector2I(w, h)
				: new Vector2I(1600, 900);
		}
		set => Set("window_size", $"{value.X}x{value.Y}");
	}

	/// <summary>Which monitor the game opens on (0 = first). A missing monitor falls back to the main one.</summary>
	public static int Screen
	{
		get => Prefs.ValidScreen(int.TryParse(Get("screen", ""), out var s) ? s : -1);
		set => Set("screen", value.ToString());
	}
}
