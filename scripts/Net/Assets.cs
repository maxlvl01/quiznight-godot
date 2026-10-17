// Question images and maps by asset key ("q3/image", "q3/map"). The host decodes them from the
// quiz; players ask the host for each one once (only for questions already shown) and cache it.

using System;
using System.Collections.Generic;
using System.Text;
using Godot;
using QuizNight.Server;
using QuizNight.Shared;
using QuizNight.Storage;

namespace QuizNight.Net;

public abstract class AssetSource
{
	protected readonly Dictionary<string, Texture2D?> Cache = new();

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
		var dataUrl = room()?.Game?.AssetDataUrl(key);
		if (dataUrl == null) return null;
		tex = Images.TextureFromDataUrl(dataUrl);
		Cache[key] = tex;
		return tex;
	}
}

public sealed class ClientAssets(Action<string> request) : AssetSource
{
	readonly Dictionary<string, (string Mime, string?[] Parts)> _incoming = new();
	readonly HashSet<string> _requested = new();

	public override Texture2D? Get(string key)
	{
		if (Cache.TryGetValue(key, out var tex)) return tex;
		if (_requested.Add(key)) request(key);
		return null;
	}

	/// <summary>Asks again for anything that didn't arrive (after a reconnect).</summary>
	public void RetryMissing()
	{
		foreach (var key in _requested)
			if (!Cache.ContainsKey(key)) request(key);
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
		var sb = new StringBuilder();
		foreach (var p in entry.Parts) sb.Append(p);
		Cache[part.Key] = Images.TextureFromDataUrl($"data:{entry.Mime};base64,{sb}");
		RaiseLoaded(part.Key);
	}
}
