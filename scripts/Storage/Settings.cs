// Small things remembered between sessions (user://settings.cfg).

using Godot;
using QuizNight.Net;

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
}
