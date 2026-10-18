// Sound effects: the buzzer sounds (built-in ones from assets/soundeffects, or the player's own file)
// and playing them on the SFX bus. Like question media, they play on the host's PC — the buzzer also on players' PCs.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using Godot;
using QuizNight.Shared;

namespace QuizNight.Storage;

public static class Sfx
{
	public record Sound(string Id, string Label, string Path);

	public static readonly Sound[] Buzzers =
	{
		new("soft-beep", "Soft beep", "res://assets/soundeffects/Soft Beep Buzzer.ogg"),
		new("metal-pipe", "Metal pipe", "res://assets/soundeffects/metal pipe buzzer.ogg"),
	};

	/// <summary>assets/soundeffects/Podium.mp3, .wav or .ogg — whichever is there (MP3 and WAV first).</summary>
	public static string PodiumJingle =>
		new[] { "mp3", "wav", "ogg" }.Select(e => $"res://assets/soundeffects/Podium.{e}").FirstOrDefault(p => ResourceLoader.Exists(p)) ?? "res://assets/soundeffects/Podium.ogg";

	public const string DefaultBuzzer = "soft-beep";
	public const string Custom = "custom";
	public const string Off = "off";
	public const int CustomMaxMb = 2;

	static readonly string[] CustomExts = { "mp3", "ogg", "wav" };
	static string CustomPath(string ext) => $"user://buzzer-custom.{ext}";
	static string? CustomFile => CustomExts.Select(CustomPath).FirstOrDefault(Godot.FileAccess.FileExists);

	public static readonly string[] FileFilters = { "*.mp3, *.ogg, *.wav ; Sound files" };

	/// <summary>Choices for a dropdown: built-in sounds, "My sound" once one was added, and Off.</summary>
	public static (string Id, string Label)[] BuzzerChoices()
	{
		var list = Buzzers.Select(b => (b.Id, b.Label)).ToList();
		if (CustomFile != null) list.Add((Custom, "My sound"));
		list.Add((Off, "No sound"));
		return list.ToArray();
	}

	/// <summary>A choice that still exists (a removed custom file falls back to the default).</summary>
	public static string Valid(string id) => BuzzerChoices().Any(c => c.Id == id) ? id : DefaultBuzzer;

	static readonly Dictionary<string, AudioStream?> Cache = new();

	static AudioStream? Load(string id)
	{
		if (Cache.TryGetValue(id, out var cached)) return cached;
		AudioStream? stream = null;
		if (Buzzers.FirstOrDefault(b => b.Id == id) is { } builtin) stream = GD.Load<AudioStream>(builtin.Path);
		else if (id == Custom && CustomFile is { } file) stream = FromBytes(Godot.FileAccess.GetFileAsBytes(file), file.GetExtension());
		Cache[id] = stream;
		return stream;
	}

	static AudioStream? FromBytes(byte[] bytes, string ext)
	{
		try
		{
			return ext.ToLowerInvariant() switch
			{
				"mp3" => new AudioStreamMP3 { Data = bytes },
				"ogg" => AudioStreamOggVorbis.LoadFromBuffer(bytes),
				"wav" => AudioStreamWav.LoadFromBuffer(bytes),
				_ => null,
			};
		}
		catch (System.Exception)
		{
			return null;
		}
	}

	/// <summary>Makes a sound file the player's own buzzer ("My sound"). Throws UserError.</summary>
	public static void SetCustom(string path)
	{
		var ext = path.GetExtension().ToLowerInvariant();
		if (!CustomExts.Contains(ext)) throw new UserError("Please pick a sound file (mp3, ogg or wav).");
		if (new FileInfo(path).Length > CustomMaxMb * 1024L * 1024L)
			throw new UserError(Lang.F("A buzzer sound can be at most {0} MB. Pick a short sound.", CustomMaxMb));
		var bytes = File.ReadAllBytes(path);
		if (FromBytes(bytes, ext) == null) throw new UserError("Godot couldn't read that sound file. Try converting it to MP3 or OGG.");
		foreach (var e in CustomExts)
			if (Godot.FileAccess.FileExists(CustomPath(e))) DirAccess.RemoveAbsolute(CustomPath(e));
		using (var f = Godot.FileAccess.Open(CustomPath(ext), Godot.FileAccess.ModeFlags.Write)) f?.StoreBuffer(bytes);
		Cache.Remove(Custom);
	}

	static AudioStreamPlayer? _player;

	/// <summary>Plays a buzzer sound (restarting it if it's still playing).</summary>
	public static void PlayBuzzer(string id)
	{
		if (id == Off || Load(Valid(id)) is not { } stream) return;
		Play(stream);
	}

	/// <summary>
	/// A player hears the host's buzzer. The host's own sound file only exists on the host's PC,
	/// so for "My sound" players hear their own default buzzer instead.
	/// </summary>
	public static void PlayRoomBuzzer(string hostChoice) =>
		PlayBuzzer(hostChoice == Custom ? Settings.BuzzerSound : hostChoice);

	/// <summary>Plays the jingle when the final results show (host only).</summary>
	public static void PlayPodium()
	{
		if (GD.Load<AudioStream>(PodiumJingle) is { } stream) Play(ref _musicPlayer, Prefs.GameMusicBus, stream);
	}

	static AudioStreamPlayer? _musicPlayer;

	static void Play(AudioStream stream) => Play(ref _player, Prefs.SfxBus, stream);

	static void Play(ref AudioStreamPlayer? player, string bus, AudioStream stream)
	{
		if (player == null || !GodotObject.IsInstanceValid(player))
		{
			player = new AudioStreamPlayer { Bus = bus };
			App.I.AddChild(player);
		}
		player.Stream = stream;
		player.Play();
	}
}
