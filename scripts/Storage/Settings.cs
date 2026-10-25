// Small things remembered between sessions (user://settings.cfg).

using System.Linq;
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

	/// <summary>The version that last showed "What's new" (so it shows once after each update).</summary>
	public static string LastSeenVersion
	{
		get => Get("last_seen_version", "");
		set => Set("last_seen_version", value);
	}

	/// <summary>The quiz maker's own scoring presets (points per place), saved on this PC.</summary>
	public static System.Collections.Generic.List<ScoringPreset> ScoringPresets
	{
		get
		{
			try
			{
				var node = System.Text.Json.Nodes.JsonNode.Parse(Get("scoring_presets", "[]")) as System.Text.Json.Nodes.JsonArray;
				return node?.OfType<System.Text.Json.Nodes.JsonObject>()
					.Select(o => new ScoringPreset(o["name"]?.GetValue<string>() ?? "",
						(o["points"] as System.Text.Json.Nodes.JsonArray)?.Select(p => p?.GetValue<int>() ?? 0).ToArray() ?? System.Array.Empty<int>(), false))
					.Where(p => p.Name != "" && p.Points.Length > 0).ToList() ?? new();
			}
			catch (System.Exception)
			{
				return new();
			}
		}
		set => Set("scoring_presets", new System.Text.Json.Nodes.JsonArray(value.Select(p => (System.Text.Json.Nodes.JsonNode)new System.Text.Json.Nodes.JsonObject
		{
			["name"] = p.Name,
			["points"] = new System.Text.Json.Nodes.JsonArray(p.Points.Select(n => (System.Text.Json.Nodes.JsonNode)System.Text.Json.Nodes.JsonValue.Create(n)!).ToArray()),
		}).ToArray()).ToJsonString());
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
	public sealed record Values(string Language, int Master, int Music, int GameMusic, int Sfx, string Buzzer, string WindowMode, Vector2I WindowSize, int Screen, bool BackgroundMotion, string Background, string Podium, bool PodiumFollowHost);

	public static Values Defaults => new(Lang.SystemDefault, 80, 80, 60, 100, Storage.Sfx.DefaultBuzzer, "windowed", new Vector2I(1600, 900), DisplayServer.GetPrimaryScreen(), true, Backgrounds.Default, Sfx.DefaultPodium, true);

	public static Values Current => new(Language, MasterVolume, MusicVolume, GameMusicVolume, SfxVolume, BuzzerSound, WindowMode, WindowSize, Screen, BackgroundMotion, Background, PodiumSound, PodiumFollowHost);

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
		BackgroundMotion = v.BackgroundMotion;
		Background = v.Background;
		PodiumSound = v.Podium;
		PodiumFollowHost = v.PodiumFollowHost;
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

	/// <summary>The podium sound: what you pick for your room as host, and what you hear as a player if you don't follow the host.</summary>
	public static string PodiumSound
	{
		get => Sfx.ValidPodium(Get("podium_sound", Sfx.DefaultPodium));
		set => Set("podium_sound", value);
	}

	/// <summary>Ask before deleting a quiz (off once "Don't ask me again" was ticked; Settings turns it back on).</summary>
	/// <summary>The colour skin (a Ui.Skins id). Saved as soon as it's picked, like the background picture's look.</summary>
	public static string Skin
	{
		get => Ui.Skins.Valid(Get("skin", Ui.Skins.Default));
		set => Set("skin", value);
	}

	/// <summary>Colour vision help (a Ui.ColourFilter mode: "off", "deuteranopia", "protanopia", "tritanopia"). Saved when picked.</summary>
	public static string ColourVision
	{
		get => Get("colour_vision", "off") is var m && Ui.ColourFilter.Modes.Any(x => x.Id == m) ? m : "off";
		set => Set("colour_vision", value);
	}

	/// <summary>All text in the dyslexia-friendly font. Saved when ticked.</summary>
	public static bool DyslexicFont
	{
		get => Get("dyslexic_font", "off") == "on";
		set => Set("dyslexic_font", value ? "on" : "off");
	}

	public static bool AskBeforeDeletingQuiz
	{
		get => Get("ask_delete_quiz", "on") != "off";
		set => Set("ask_delete_quiz", value ? "on" : "off");
	}

	/// <summary>As a player: hear the podium sound the host picked (else your own).</summary>
	public static bool PodiumFollowHost
	{
		get => Get("podium_follow_host", "on") != "off";
		set => Set("podium_follow_host", value ? "on" : "off");
	}

	/// <summary>Whether the space picture beside the screens (wide monitors) moves with the mouse.</summary>
	public static bool BackgroundMotion
	{
		get => Get("background_motion", "on") != "off";
		set => Set("background_motion", value ? "on" : "off");
	}

	/// <summary>The picture beside the screens on wide monitors (a Backgrounds id).</summary>
	public static string Background
	{
		get => Backgrounds.Valid(Get("background", Backgrounds.Default));
		set => Set("background", value);
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

/// <summary>The pictures to choose from for the space beside the screens on wide monitors.</summary>
public static class Backgrounds
{
	public const string Default = "pinke-geilheit";

	public static readonly (string Id, string Label)[] All =
	{
		("pinke-geilheit", "Pinke Geilheit Spacing Team"),
		("cosmic-cliffs", "Cosmic Cliffs (James Webb Space Telescope)"),
	};

	/// <summary>One side's part of a picture: centred on Anchor (0–1 across the picture), Zoom times as tall as the window.</summary>
	public readonly record struct Side(Vector2 Anchor, float Zoom = 1);

	/// <summary>Pictures with a subject: each side of the screens shows its own part. Others (null) cover the whole window.</summary>
	public static (Side Left, Side Right)? Sides(string? id) => id switch
	{
		// the astronaut on the left; on the right, the ship's top corner (zoomed in, so her suit doesn't peek in at the bottom)
		"pinke-geilheit" => (new Side(new Vector2(0.53f, 0.45f)), new Side(new Vector2(0.9f, 0.28f), 1.8f)),
		_ => null,
	};

	public static string Valid(string id) => All.Any(b => b.Id == id) ? id : Default;

	public static string Path(string id) => $"res://assets/backgrounds/{Valid(id)}.webp";
}
