// The host's side of a room: runs the Room, talks to players over the transport, and sends
// everyone their own view of the state (players never see answers before the reveal).

using System;
using System.Collections.Generic;
using System.Linq;
using QuizNight.Net;
using QuizNight.Shared;

namespace QuizNight.Server;

public sealed class HostSession : IDisposable
{
	public readonly Room Room;
	public readonly NetMode Mode;
	/// <summary>LAN mode: the port players connect to.</summary>
	public readonly int LanPort;
	readonly IHostTransport _net;
	/// <summary>Connection → player id, for connections that have joined.</summary>
	readonly Dictionary<string, string> _peerPlayer = new();

	/// <summary>Something changed: the host screen should redraw.</summary>
	public event Action? Changed;

	public HostSession(Room room, IHostTransport net, NetMode mode, int lanPort = 0)
	{
		Room = room;
		Mode = mode;
		LanPort = lanPort;
		_net = net;
		_net.PeerDisconnected += OnPeerDisconnected;
		_net.Received += OnReceived;
	}

	public RoomState State => Room.ToState("host");

	public void Poll()
	{
		_net.Poll();
		if (Room.Tick()) Broadcast();
	}

	string? PeerOf(string playerId) => _peerPlayer.FirstOrDefault(kv => kv.Value == playerId).Key;

	/// <summary>Everyone gets their own view. The host's screen reads the state directly.</summary>
	public void Broadcast()
	{
		Changed?.Invoke();
		foreach (var (peer, playerId) in _peerPlayer.ToList()) _net.Send(peer, Msg.Encode(Msg.State, Room.ToState(playerId)));
	}

	void OnPeerDisconnected(string peer)
	{
		if (!_peerPlayer.Remove(peer, out var playerId)) return;
		// A newer connection may already have taken over this seat.
		if (PeerOf(playerId) != null) return;
		Room.Disconnect(playerId);
		Broadcast();
	}

	void OnReceived(string peer, byte[] bytes)
	{
		if (Msg.Decode(bytes) is not { } msg) return;
		_peerPlayer.TryGetValue(peer, out var playerId);
		switch (msg.Type)
		{
			case Msg.Join:
				HandleJoin(peer, Msg.Read<JoinRequest>(msg.Data) ?? new JoinRequest());
				break;
			case Msg.Leave when playerId != null:
				_peerPlayer.Remove(peer);
				Room.Remove(playerId);
				_net.Disconnect(peer);
				Broadcast();
				break;
			case Msg.Buzz when playerId != null && Playing():
				if (Room.Game!.BuzzIn(playerId)) Broadcast();
				break;
			case Msg.Answer when playerId != null:
			{
				var req = Msg.Read<AnswerRequest>(msg.Data);
				var result = !Playing() ? Result.Fail("The quiz is not running.")
					: req == null ? Result.Fail("That answer didn't arrive properly.")
					: Room.Game!.Answer(playerId, req.Index, req.Value);
				_net.Send(peer, Msg.Encode(Msg.AnswerResult, result));
				if (result.Ok) Broadcast();
				break;
			}
			case Msg.AssetRequest when playerId != null:
				if (Msg.Read<AssetRequest>(msg.Data) is { } ar) SendAsset(peer, ar.Key);
				break;
		}
	}

	bool Playing() => Room.Game != null && Room.Phase == Phase.Playing;

	void HandleJoin(string peer, JoinRequest req)
	{
		// Steam vouches for who is connecting; on LAN the player's app says who it is.
		var id = _net.PeerSteamId(peer) is { } steamId ? "steam:" + steamId
			: req.ClientId.Trim() is { Length: > 0 and <= 64 } cid ? "lan:" + cid
			: "lan:" + peer;
		var (player, error) = Room.Join(id, req.Name);
		if (player == null)
		{
			_net.Send(peer, Msg.Encode(Msg.Joined, new JoinReply { Ok = false, Error = error }));
			return;
		}
		// Coming back on a new connection: the old one is stale.
		if (PeerOf(id) is { } old && old != peer)
		{
			_peerPlayer.Remove(old);
			_net.Disconnect(old);
		}
		_peerPlayer[peer] = id;
		_net.Send(peer, Msg.Encode(Msg.Joined, new JoinReply { Ok = true, PlayerId = id }));
		Broadcast();
	}

	void SendAsset(string peer, string key)
	{
		var dataUrl = Room.Game?.AssetDataUrl(key);
		if (dataUrl == null || !DataUrl.TryParse(dataUrl, out var mime, out var base64)) return;
		// base64 grows by 4/3 and the message needs some room for its envelope.
		var chunk = (_net.MaxMessageBytes - 1024) / 4 * 4;
		var parts = Math.Max(1, (base64.Length + chunk - 1) / chunk);
		for (var i = 0; i < parts; i++)
		{
			var data = base64.Substring(i * chunk, Math.Min(chunk, base64.Length - i * chunk));
			_net.Send(peer, Msg.Encode(Msg.Asset, new AssetPart { Key = key, Mime = mime, Part = i, Parts = parts, Data = data }));
		}
	}

	// ---------- host actions ----------

	public void Kick(string playerId)
	{
		if (Room.GetPlayer(playerId) == null) return;
		Room.Remove(playerId);
		if (PeerOf(playerId) is { } peer)
		{
			_net.Send(peer, Msg.Encode(Msg.Kicked));
			_peerPlayer.Remove(peer);
			_net.Disconnect(peer);
		}
		Broadcast();
	}

	public void UpdateSettings(int? maxPlayers = null, int? answerTimeSec = null)
	{
		if (Room.Phase != Phase.Lobby) return;
		Room.UpdateSettings(maxPlayers, answerTimeSec);
		Broadcast();
	}

	public Result Start()
	{
		var result = Room.Start();
		if (result.Ok) Broadcast();
		return result;
	}

	public void BackToLobby()
	{
		Room.BackToLobby();
		Broadcast();
	}

	void GameAction(Func<Game, bool> action)
	{
		if (Playing() && action(Room.Game!)) Broadcast();
	}

	/// <summary>The host's sound or video started or stopped playing.</summary>
	public void SetMedia(bool playing) => GameAction(g => g.Stage == Stage.Open && g.SetMedia(playing));
	public void Judge(bool correct) => GameAction(g => g.Judge(correct));
	public void Reveal() => GameAction(g => g.HostReveal());

	public void Next()
	{
		if (!Playing()) return;
		Room.Next();
		Broadcast();
	}

	public void Finish()
	{
		if (!Playing()) return;
		Room.Finish();
		Broadcast();
	}

	/// <summary>Tells everyone the room is closed and stops listening.</summary>
	public void Close(string reason = "The host closed the room.")
	{
		foreach (var peer in _peerPlayer.Keys.ToList()) _net.Send(peer, Msg.Encode(Msg.Closed, new ClosedNotice { Reason = reason }));
		_peerPlayer.Clear();
		Dispose();
	}

	bool _disposed;

	public void Dispose()
	{
		if (_disposed) return;
		_disposed = true;
		_net.Dispose();
	}
}

/// <summary>data:[mime];base64,[data] helpers.</summary>
public static class DataUrl
{
	public static bool TryParse(string dataUrl, out string mime, out string base64)
	{
		mime = "";
		base64 = "";
		if (!dataUrl.StartsWith("data:")) return false;
		var comma = dataUrl.IndexOf(',');
		if (comma < 0) return false;
		var meta = dataUrl[5..comma];
		if (!meta.EndsWith(";base64")) return false;
		mime = meta[..^7];
		base64 = dataUrl[(comma + 1)..];
		return true;
	}

	public static byte[]? Bytes(string dataUrl, out string mime)
	{
		if (!TryParse(dataUrl, out mime, out var b64)) return null;
		try
		{
			return Convert.FromBase64String(b64);
		}
		catch (FormatException)
		{
			return null;
		}
	}

	public static string Make(string mime, byte[] bytes) => $"data:{mime};base64,{Convert.ToBase64String(bytes)}";
}
