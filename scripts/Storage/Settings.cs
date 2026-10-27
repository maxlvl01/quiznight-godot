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

	/// <summary>
	/// 0.10.2b: every volume starts at 50 %, also for players who had others (once; the home screen says so). Returns
	/// whether that changed anything someone had (not on the very first start).
	/// </summary>
	public static bool ResetVolumesOnce(bool firstStart)
	{
		if (Get("volumes_reset", "") == "0.10.2b") return false;
		foreach (var key in new[] { "volume_master", "volume_music", "volume_game_music", "volume_sfx" }) Cfg.SetValue("app", key, "50");
		Set("volumes_reset", "0.10.2b");
		return !firstStart;
	}

	/// <summary>No settings saved on this PC yet: the very first start (anyone who played before has some, even after an update).</summary>
	public static bool NothingSaved => !Godot.FileAccess.FileExists(FilePath);

	/// <summary>The first start's accessibility screen was shown (and Let's go! pressed): never again.</summary>
	public static bool AccessibilityAsked
	{
		get => Get("accessibility_asked", "off") == "on";
		set => Set("accessibility_asked", value ? "on" : "off");
	}

	/// <summary>How much of the intro the game shows when it starts: "all", "nojokes" (no anti-cheat, World Premiere,
	/// publisher or sponsor) or "none" (straight to the title screen). Asked on the first start, changed in Settings.</summary>
	public static string Intro
	{
		get => Get("intro", "all") is var v && v is "nojokes" or "none" ? v : "all";
		set => Set("intro", value);
	}

	/// <summary>The choices for <see cref="Intro"/>, as Settings and the first start show them.</summary>
	public static readonly (string Id, string Label)[] IntroChoices =
	{
		("all", "Everything"), ("nojokes", "Skip the joke parts"), ("none", "Skip it all"),
	};

	/// <summary>The version the player had before their last update: what came after it gets a yellow dot (Ui/New).</summary>
	public static string NewSince
	{
		get => Get("new_since", "");
		set => Set("new_since", value);
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

	public static Values Defaults => new(Lang.SystemDefault, 50, 50, 50, 50, Storage.Sfx.DefaultBuzzer, "windowed", new Vector2I(1600, 900), DisplayServer.GetPrimaryScreen(), true, Backgrounds.Default, Sfx.DefaultPodium, true);

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

	/// <summary>The microphone interactive questions record with ("Default" = the one Windows uses).</summary>
	public static string Microphone
	{
		get => Get("microphone", "Default");
		set => Set("microphone", value);
	}

	/// <summary>How loud recordings are (0–100): 50 = as the microphone gives it, more = louder (up to +24 dB), less = quieter.</summary>
	public static int MicVolume
	{
		get => GetPercent("volume_mic", 50);
		set => Set("volume_mic", value.ToString());
	}

	/// <summary>MicVolume as a factor for the samples (50 → 1, 100 → 16, 0 → 1/16).</summary>
	public static float MicGain => Godot.Mathf.DbToLinear((MicVolume - 50) / 50f * 24);

	/// <summary>Uses the chosen microphone, if it's still there (else the system's default).</summary>
	public static void ApplyMicrophone()
	{
		var wanted = Microphone;
		Godot.AudioServer.InputDevice = System.Array.IndexOf(Godot.AudioServer.GetInputDeviceList(), wanted) >= 0 ? wanted : "Default";
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

	/// <summary>The colour skin the player chose (a Ui.Skins id; Ui.Skins.Effective says which one shows). Saved as soon as it's picked.</summary>
	public static string Skin
	{
		get => Ui.Skins.Valid(Get("skin", Ui.Skins.Default));
		set => Set("skin", value);
	}

	/// <summary>Halloween and Christmas skins come on by themselves in their season (on unless switched off in Settings).</summary>
	public static bool SeasonalSkins
	{
		get => Get("seasonal_skins", "on") != "off";
		set => Set("seasonal_skins", value ? "on" : "off");
	}

	/// <summary>The skin that shows now: the chosen one, or the season's.</summary>
	public static string ShownSkin => Ui.Skins.Effective(Skin, SeasonalSkins);

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

	/// <summary>Tutorials (Ui/Tutorial) show the first time their screen opens; "Turn off tutorials" stops them all.</summary>
	public static bool TutorialsOn
	{
		get => Get("tutorials", "on") != "off";
		set => Set("tutorials", value ? "on" : "off");
	}

	/// <summary>The tutorials already shown (finished or skipped), by id.</summary>
	public static string[] TutorialsSeen
	{
		get => Get("tutorials_seen", "").Split(',', System.StringSplitOptions.RemoveEmptyEntries);
		set => Set("tutorials_seen", string.Join(',', value));
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

	/// <summary>The menu and lobby music (off from the jukebox). Saved when switched.</summary>
	public static bool MusicOn
	{
		get => Get("music_on", "on") != "off";
		set => Set("music_on", value ? "on" : "off");
	}

	/// <summary>The jukebox plays the songs shuffled (on) or by title (off).</summary>
	public static bool MusicShuffle
	{
		get => Get("music_shuffle", "on") != "off";
		set => Set("music_shuffle", value ? "on" : "off");
	}

	/// <summary>The jukebox plays the same song again and again.</summary>
	public static bool MusicRepeat
	{
		get => Get("music_repeat", "off") == "on";
		set => Set("music_repeat", value ? "on" : "off");
	}

	/// <summary>Whether the space picture beside the screens (wide monitors) moves with the mouse.</summary>
	public static bool BackgroundMotion
	{
		get => Get("background_motion", "on") != "off";
		set => Set("background_motion", value ? "on" : "off");
	}

	/// <summary>The picture beside the screens on wide monitors (a Backgrounds id).</summary>
	/// <summary>Dev (`--background ID`): this run shows that one, without saving it.</summary>
	public static string? BackgroundThisRun;

	public static string Background
	{
		get => Backgrounds.Valid(BackgroundThisRun ?? Get("background", Backgrounds.Default));
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
	/// <summary>The animated one: a looping video (assets/backgrounds/&lt;id&gt;.ogv) instead of a picture.</summary>
	public const string Animated = "pinke-geilheit-animated";
	/// <summary>The player's own picture, copied to <see cref="CustomFile"/>.</summary>
	public const string Custom = "custom";
	public const string CustomFile = "user://backgrounds/custom.webp";
	/// <summary>Own pictures are kept at most this big (4K's width): plenty for any monitor's sides.</summary>
	const int CustomMaxSide = 3840;

	static readonly (string Id, string Label)[] BuiltIn =
	{
		("pinke-geilheit", "Pinke Geilheit Spacing Team"),
		(Animated, "Pinke Geilheit Spacing Team (animated)"),
		("cosmic-cliffs", "Cosmic Cliffs (James Webb Space Telescope)"),
	};

	/// <summary>The choices: the built-in ones, and "My own picture" once one was picked.</summary>
	public static (string Id, string Label)[] All =>
		HasCustom ? BuiltIn.Append((Custom, "My own picture")).ToArray() : BuiltIn;

	public static bool HasCustom => FileAccess.FileExists(CustomFile);

	public static bool IsVideo(string? id) => id == Animated;

	/// <summary>
	/// Takes a picture from the player's PC as their background: checked like every outside picture (Guard), made at most
	/// 4K wide, and kept in the game's own folder (so it stays if the original moves). Throws with a readable message.
	/// </summary>
	public static void SetCustom(string path)
	{
		byte[] bytes;
		try
		{
			using var stream = System.IO.File.OpenRead(path);
			bytes = Shared.Guard.ReadCapped(stream, 60_000_000);
		}
		catch (System.Exception)
		{
			throw new UserError("Could not read that picture (it may be too big: up to 60 MB).");
		}
		var img = Images.Decode(bytes, "") ?? throw new UserError("Could not read that image. Try a PNG, JPG or WebP file.");
		if (img.IsCompressed()) img.Decompress();
		var scale = System.Math.Min(1.0, (double)CustomMaxSide / System.Math.Max(img.GetWidth(), img.GetHeight()));
		if (scale < 1) img.Resize((int)System.Math.Round(img.GetWidth() * scale), (int)System.Math.Round(img.GetHeight() * scale), Image.Interpolation.Lanczos);
		DirAccess.MakeDirRecursiveAbsolute("user://backgrounds");
		if (img.SaveWebp(CustomFile, false, 0.9f) != Error.Ok) throw new UserError("Could not save the picture.");
		Changed++;
	}

	/// <summary>Goes up when the own picture is replaced (the backdrop loads it again).</summary>
	public static int Changed { get; private set; }

	/// <summary>The picture (not for the animated one): a built-in one from the game, or the player's own.</summary>
	public static Image? LoadImage(string id)
	{
		if (id == Custom) return Image.LoadFromFile(ProjectSettings.GlobalizePath(CustomFile));
		return GD.Load<Texture2D>(Path(id))?.GetImage();
	}

	/// <summary>One side's part of a picture: centred on Anchor (0–1 across the picture), Zoom times as tall as the window.</summary>
	public readonly record struct Side(Vector2 Anchor, float Zoom = 1);

	/// <summary>Pictures with a subject: each side of the screens shows its own part. Others (null) cover the whole window.</summary>
	public static (Side Left, Side Right)? Sides(string? id) => id switch
	{
		// the astronaut on the left; on the right, the ship's top corner (zoomed in, so her suit doesn't peek in at the bottom)
		"pinke-geilheit" or Animated => (new Side(new Vector2(0.53f, 0.45f)), new Side(new Vector2(0.9f, 0.28f), 1.8f)),
		_ => null,
	};

	public static string Valid(string id) => All.Any(b => b.Id == id) ? id : Default;

	public static string Path(string id) => $"res://assets/backgrounds/{Valid(id)}.{(IsVideo(id) ? "ogv" : "webp")}";
}
