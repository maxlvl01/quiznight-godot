// How the host and the players reach each other. Two implementations: Steam (lobbies for the
// room code + Steam's relay network, works over the internet) and LAN (plain TCP, for the same
// Wi-Fi or several copies of the game on one PC while testing).

using System;
using System.Collections.Generic;

namespace QuizNight.Net;

public enum NetMode
{
	Steam,
	Lan,
}

public interface IHostTransport : IDisposable
{
	event Action<string>? PeerConnected;
	event Action<string>? PeerDisconnected;
	event Action<string, byte[]>? Received;

	/// <summary>The peer's Steam ID (Steam mode), or null (LAN mode, where players say who they are).</summary>
	string? PeerSteamId(string peer);

	void Send(string peer, byte[] data);
	/// <summary>
	/// Sends unless a lot is still waiting to go out to that peer (then returns false: try again later).
	/// For big transfers (videos), so they go out as fast as the connection takes them without piling up.
	/// </summary>
	bool TrySend(string peer, byte[] data);
	/// <summary>Closes the connection after sending what is queued.</summary>
	void Disconnect(string peer);
	void Poll();

	/// <summary>Largest message the transport can carry; bigger assets are split.</summary>
	int MaxMessageBytes { get; }
}

public interface IClientTransport : IDisposable
{
	event Action? Connected;
	event Action<string>? Disconnected;
	event Action<byte[]>? Received;

	void Send(byte[] data);
	void Poll();
}

/// <summary>Cuts a TCP byte stream into messages: [4-byte little-endian length][payload].</summary>
public sealed class FrameReader
{
	/// <summary>The most a message may be: players only send small ones (a host's are bigger: pictures, video parts).</summary>
	readonly int _maxFrame;
	byte[] _buf = new byte[64 * 1024];
	int _len;

	public FrameReader() : this(256 * 1024 * 1024) { }

	public FrameReader(int maxFrame) => _maxFrame = maxFrame;

	public static byte[] Frame(byte[] payload)
	{
		var framed = new byte[payload.Length + 4];
		BitConverter.TryWriteBytes(framed.AsSpan(0, 4), payload.Length);
		payload.CopyTo(framed, 4);
		return framed;
	}

	/// <summary>Adds received bytes; returns the messages that are now complete. Throws on garbage.</summary>
	public List<byte[]> Feed(byte[] data)
	{
		if (_len + data.Length > _buf.Length) Array.Resize(ref _buf, Math.Max(_buf.Length * 2, _len + data.Length));
		data.CopyTo(_buf, _len);
		_len += data.Length;

		var messages = new List<byte[]>();
		var pos = 0;
		while (_len - pos >= 4)
		{
			var size = BitConverter.ToInt32(_buf, pos);
			if (size < 0 || size > _maxFrame) throw new InvalidOperationException("Bad frame size.");
			if (_len - pos - 4 < size) break;
			messages.Add(_buf.AsSpan(pos + 4, size).ToArray());
			pos += 4 + size;
		}
		if (pos > 0)
		{
			Buffer.BlockCopy(_buf, pos, _buf, 0, _len - pos);
			_len -= pos;
		}
		return messages;
	}
}
