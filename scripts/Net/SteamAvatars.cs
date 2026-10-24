// Players' Steam profile pictures, for the avatars. Player ids over Steam are "steam:<Steam ID>"; every app asks
// Steam itself for those pictures (Steam knows anyone it has a connection with), so nothing goes over the game's
// connection. LAN players and anyone Steam has no picture for keep their initials.

using System;
using System.Collections.Generic;
using Godot;
using Steamworks;

namespace QuizNight.Net;

public static class SteamAvatars
{
	/// <summary>A picture arrived (player id): avatars showing that player redraw.</summary>
	public static event Action<string>? Loaded;

	static readonly Dictionary<string, Texture2D?> _cache = new();

	/// <summary>The player's Steam picture, or null (not over Steam, not loaded yet, or none).</summary>
	public static Texture2D? Get(string playerId)
	{
		if (!playerId.StartsWith("steam:") || !SteamService.Available || DisplayServer.GetName() == "headless") return null;
		if (_cache.TryGetValue(playerId, out var tex)) return tex;
		_cache[playerId] = null;
		if (ulong.TryParse(playerId.AsSpan(6), out var steamId) && steamId != 0) Load(playerId, steamId);
		return null;
	}

	static async void Load(string playerId, ulong steamId)
	{
		try
		{
			// (continues inside Steam's callbacks, on the main thread)
			if (await SteamFriends.GetLargeAvatarAsync(steamId) is not { } img || img.Width == 0) return;
			var image = Image.CreateFromData((int)img.Width, (int)img.Height, false, Image.Format.Rgba8, img.Data);
			image.GenerateMipmaps();
			_cache[playerId] = ImageTexture.CreateFromImage(image);
			Loaded?.Invoke(playerId);
		}
		catch (Exception e)
		{
			GD.Print($"Couldn't load the Steam picture of {steamId}: {e.Message}");
		}
	}
}
