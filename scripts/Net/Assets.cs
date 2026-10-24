// Question images and maps by asset key ("q3/image", "q3/map"). The host decodes them from the
// quiz; players ask the host for each one once (only for questions already shown) and cache it.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Godot;
using QuizNight.Server;
using QuizNight.Shared;
using QuizNight.Storage;

namespace QuizNight.Net;

public abstract class AssetSource
{
	protected readonly Dictionary<string, Texture2D?> Cache = new();
	/// <summary>Sounds by asset key (interactive questions' recordings).</summary>
	protected readonly Dictionary<string, AudioStream?> Sounds = new();

	/// <summary>A recording if it's here; otherwise starts fetching it and returns null (watch Loaded).</summary>
	public abstract AudioStream? GetSound(string key);

	/// <summary>An asset finished loading.</summary>
	public event Action<string>? Loaded;

	protected void RaiseLoaded(string key) => Loaded?.Invoke(key);

	/// <summary>The texture if it's here; otherwise starts fetching it and returns null (watch Loaded).</summary>
	public abstract Texture2D? Get(string key);

	/// <summary>A question's map: built-in maps come from the game files, custom ones from the source.</summary>
	public Texture2D? Map(PublicMap map) =>
		MapPresets.Get(map.PresetId) is { } preset ? Images.BuiltinTexture(preset.ImagePath) : map.AssetKey != null ? Get(map.AssetKey) : null;
}

public sealed class HostAssets(Func<Room?> room) : AssetSource
{
	public override Texture2D? Get(string key)
	{
		if (Cache.TryGetValue(key, out var tex)) return tex;
		var dataUrl = room()?.Game?.AssetDataUrl(key, host: true);
		if (dataUrl == null) return null;
		tex = Images.TextureFromDataUrl(dataUrl);
		Cache[key] = tex;
		return tex;
	}

	public override AudioStream? GetSound(string key)
	{
		if (Sounds.TryGetValue(key, out var sound)) return sound;
		var dataUrl = room()?.Game?.AssetDataUrl(key, host: true);
		if (dataUrl == null || DataUrl.Bytes(dataUrl, out var mime) is not { } bytes) return null;
		return Sounds[key] = Media.LoadSound(bytes, mime);
	}
}

/// <summary>
/// A player's pictures. Most arrived ahead of time with the quiz's files (encrypted, ClientMedia); once a question is
/// shown its pictures' keys come with it (PublicQuestion.Files) and they're unlocked from there. A picture that isn't
/// all here yet (joined late, slow connection) is also asked for by its asset key, whichever comes first.
/// </summary>
public sealed class ClientAssets(Action<string> request, ClientMedia media) : AssetSource
{
	readonly Dictionary<string, (string Mime, string?[] Parts)> _incoming = new();
	readonly HashSet<string> _requested = new();
	readonly Dictionary<string, SharedFileRef> _files = new();
	readonly HashSet<string> _unlocking = new();

	/// <summary>The shown question's pictures: which downloaded file each is, and its key.</summary>
	public void Learn(Dictionary<string, SharedFileRef>? files)
	{
		if (files == null) return;
		foreach (var (key, file) in files) _files[key] = file;
	}

	public override Texture2D? Get(string key)
	{
		if (Cache.TryGetValue(key, out var tex)) return tex;
		if (_files.TryGetValue(key, out var file))
		{
			if (Unlocked(key, file) is { } unlocked) return unlocked;
			_unlocking.Add(key);
			// (not all here: ask the host for it directly too)
			if ((media.Progress(file.Id) < 1 || media.Failed(file.Id)) && _requested.Add(key))
			{
				GD.Print($"Picture {key}: not downloaded yet ({media.Progress(file.Id):P0}), asking the host for it");
				request(key);
			}
			return null;
		}
		if (_requested.Add(key)) request(key);
		return null;
	}

	public override AudioStream? GetSound(string key)
	{
		if (Sounds.TryGetValue(key, out var sound)) return sound;
		if (_requested.Add(key)) request(key);
		return null;
	}

	Texture2D? Unlocked(string key, SharedFileRef file)
	{
		if (media.GetImage(file.Id, file.Key, file.Mime) is not { } tex) return null;
		Cache[key] = tex;
		return tex;
	}

	/// <summary>Pictures being unlocked from their downloaded files (that takes a moment): tell the screen once ready.</summary>
	public void Poll()
	{
		if (_unlocking.Count == 0) return;
		foreach (var key in _unlocking.ToList())
		{
			if (Cache.ContainsKey(key)) _unlocking.Remove(key);
			else if (Unlocked(key, _files[key]) != null)
			{
				_unlocking.Remove(key);
				RaiseLoaded(key);
			}
			else if (media.Failed(_files[key].Id) && _requested.Add(key)) request(key);
		}
	}

	/// <summary>Asks again for anything that didn't arrive (after a reconnect).</summary>
	public void RetryMissing()
	{
		foreach (var key in _requested)
			if (!Cache.ContainsKey(key) && !Sounds.ContainsKey(key)) request(key);
	}

	public void Receive(AssetPart part)
	{
		if (part.Parts < 1 || part.Parts > 10_000 || part.Part < 0 || part.Part >= part.Parts || !_requested.Contains(part.Key)) return;
		if (!_incoming.TryGetValue(part.Key, out var entry) || entry.Parts.Length != part.Parts)
		{
			entry = (part.Mime, new string?[part.Parts]);
			_incoming[part.Key] = entry;
		}
		entry.Parts[part.Part] = part.Data;
		if (Array.Exists(entry.Parts, p => p == null)) return;
		_incoming.Remove(part.Key);
		if (Cache.ContainsKey(part.Key)) return; // (unlocked from the downloaded file in the meantime)
		var sb = new StringBuilder();
		foreach (var p in entry.Parts) sb.Append(p);
		if (entry.Mime.StartsWith("audio/"))
		{
			Sounds[part.Key] = Media.LoadSound(Convert.FromBase64String(sb.ToString()), entry.Mime);
			RaiseLoaded(part.Key);
			return;
		}
		GD.Print($"Picture {part.Key}: arrived from the host directly");
		Cache[part.Key] = Images.TextureFromDataUrl($"data:{entry.Mime};base64,{sb}");
		RaiseLoaded(part.Key);
	}
}
