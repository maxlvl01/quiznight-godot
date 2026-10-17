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

	static int GetPercent(string key, int fallback) => int.TryParse(Get(key, ""), out var v) ? Mathf.Clamp(v, 0, 100) : fallback;

	public static string Language
	{
		get => Get("language", Lang.SystemDefault);
		set => Set("language", value);
	}

	public static int MasterVolume
	{
		get => GetPercent("volume_master", 80);
		set => Set("volume_master", value.ToString());
	}

	public static int MusicVolume
	{
		get => GetPercent("volume_music", 80);
		set => Set("volume_music", value.ToString());
	}

	public static int GameMusicVolume
	{
		get => GetPercent("volume_game_music", 60);
		set => Set("volume_game_music", value.ToString());
	}

	public static int SfxVolume
	{
		get => GetPercent("volume_sfx", 100);
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
		get => Get("window_mode", "windowed");
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
}
