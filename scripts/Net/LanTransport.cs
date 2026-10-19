// LAN mode: the host listens on a TCP port, players connect to the host's address.
// Everything is polled on the main thread from _Process.

using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using QuizNight.Shared;

namespace QuizNight.Net;

public sealed class LanHostTransport : IHostTransport
{
	public const int DefaultPort = 7777;

	public event Action<string>? PeerConnected;
	public event Action<string>? PeerDisconnected;
	public event Action<string, byte[]>? Received;

	sealed class Peer
	{
		public required StreamPeerTcp Tcp;
		public readonly FrameReader Reader = new();
		// What's still to send: written without blocking as the network takes it (Flush), so a big
		// video never freezes the host's game.
		public readonly Queue<byte[]> Out = new();
		public int OutPos;
		public long OutBytes;
	}

	/// <summary>TrySend refuses once this much is waiting for a player.</summary>
	const long MaxQueued = 2 * 1024 * 1024;

	readonly TcpServer _server = new();
	readonly Dictionary<string, Peer> _peers = new();
	int _nextId;

	public int Port { get; private set; }
	public int MaxMessageBytes => 64 * 1024 * 1024;

	/// <summary>Starts listening. Returns an error message, or null when it worked.</summary>
	public string? Listen(int port = DefaultPort)
	{
		var err = _server.Listen((ushort)port, "*");
		if (err != Error.Ok) return Lang.F("Couldn't open port {0} ({1}). Is another Quiznight already hosting on this PC?", port, err);
		Port = port;
		return null;
	}

	/// <summary>This PC's addresses on the local network, for players on the same Wi-Fi.</summary>
	public static List<string> LanAddresses() =>
		IP.GetLocalAddresses()
			.Where(a => a.Contains('.') && !a.StartsWith("127.") && !a.StartsWith("169.254."))
			.Distinct()
			.ToList();

	public string? PeerSteamId(string peer) => null;

	public void Send(string peer, byte[] data)
	{
		if (_peers.TryGetValue(peer, out var p)) Queue(p, data);
	}

	public bool TrySend(string peer, byte[] data)
	{
		if (!_peers.TryGetValue(peer, out var p) || p.OutBytes > MaxQueued) return false;
		Queue(p, data);
		return true;
	}

	static void Queue(Peer p, byte[] data)
	{
		var framed = FrameReader.Frame(data);
		p.Out.Enqueue(framed);
		p.OutBytes += framed.Length;
		Flush(p);
	}

	/// <summary>Writes as much of the queue as the connection takes right now.</summary>
	static void Flush(Peer p)
	{
		while (p.Out.Count > 0)
		{
			var head = p.Out.Peek();
			var res = p.Tcp.PutPartialData(head.AsSpan(p.OutPos).ToArray());
			if (res[0].AsInt32() != (int)Error.Ok) return;
			var sent = res[1].AsInt32();
			p.OutPos += sent;
			p.OutBytes -= sent;
			if (p.OutPos < head.Length) return; // the connection is full for now
			p.Out.Dequeue();
			p.OutPos = 0;
		}
	}

	public void Disconnect(string peer)
	{
		if (!_peers.Remove(peer, out var p)) return;
		// (whatever is still queued, e.g. "you were removed", goes out first)
		while (p.Out.Count > 0)
		{
			p.Tcp.PutData(p.Out.Dequeue().AsSpan(p.OutPos).ToArray());
			p.OutPos = 0;
		}
		p.Tcp.DisconnectFromHost();
		PeerDisconnected?.Invoke(peer);
	}

	public void Poll()
	{
		while (_server.IsListening() && _server.IsConnectionAvailable())
		{
			var tcp = _server.TakeConnection();
			tcp.SetNoDelay(true);
			var id = $"lan{++_nextId}";
			_peers[id] = new Peer { Tcp = tcp };
			PeerConnected?.Invoke(id);
		}
		foreach (var (id, peer) in _peers.ToList())
		{
			if (!_peers.ContainsKey(id)) continue; // dropped by a handler during this loop
			peer.Tcp.Poll();
			if (peer.Tcp.GetStatus() == StreamPeerTcp.Status.Connected) Flush(peer);
			if (peer.Tcp.GetStatus() != StreamPeerTcp.Status.Connected)
			{
				_peers.Remove(id);
				PeerDisconnected?.Invoke(id);
				continue;
			}
			var available = peer.Tcp.GetAvailableBytes();
			if (available <= 0) continue;
			var res = peer.Tcp.GetData(available);
			if (res[0].AsInt32() != (int)Error.Ok) continue;
			List<byte[]> messages;
			try
			{
				messages = peer.Reader.Feed(res[1].AsByteArray());
			}
			catch (InvalidOperationException)
			{
				Disconnect(id); // not one of ours
				continue;
			}
			foreach (var m in messages)
			{
				if (!_peers.ContainsKey(id)) break;
				Received?.Invoke(id, m);
			}
		}
	}

	public void Dispose()
	{
		foreach (var p in _peers.Values) p.Tcp.DisconnectFromHost();
		_peers.Clear();
		_server.Stop();
	}
}

public sealed class LanClientTransport : IClientTransport
{
	public event Action? Connected;
	public event Action<string>? Disconnected;
	public event Action<byte[]>? Received;

	const double ConnectTimeoutSec = 6;

	readonly StreamPeerTcp _tcp = new();
	readonly FrameReader _reader = new();
	bool _connected;
	bool _done;
	readonly double _startedAt = Time.GetTicksMsec() / 1000.0;

	public LanClientTransport(string host, int port)
	{
		var err = _tcp.ConnectToHost(host, port);
		// Reported on the first Poll, once someone listens for it.
		if (err != Error.Ok) _startError = Lang.F("Couldn't reach \"{0}\". Check the address.", host);
	}

	string? _startError;

	public void Send(byte[] data)
	{
		if (_connected && !_done) _tcp.PutData(FrameReader.Frame(data));
	}

	void Fail(string reason)
	{
		if (_done) return;
		_done = true;
		_tcp.DisconnectFromHost();
		Disconnected?.Invoke(reason);
	}

	public void Poll()
	{
		if (_done) return;
		if (_startError != null)
		{
			Fail(_startError);
			return;
		}
		_tcp.Poll();
		var status = _tcp.GetStatus();
		if (status == StreamPeerTcp.Status.Connecting)
		{
			if (Time.GetTicksMsec() / 1000.0 - _startedAt > ConnectTimeoutSec) Fail("Nobody answered at that address. Is the host's lobby open, and are you on the same network?");
			return;
		}
		if (status != StreamPeerTcp.Status.Connected)
		{
			Fail(_connected ? "Lost the connection to the host." : "Couldn't connect to the host.");
			return;
		}
		if (!_connected)
		{
			_connected = true;
			Connected?.Invoke();
		}
		var available = _tcp.GetAvailableBytes();
		if (available <= 0) return;
		var res = _tcp.GetData(available);
		if (res[0].AsInt32() != (int)Error.Ok) return;
		try
		{
			foreach (var m in _reader.Feed(res[1].AsByteArray()))
			{
				if (_done) break;
				Received?.Invoke(m);
			}
		}
		catch (InvalidOperationException)
		{
			Fail("The host sent something unreadable.");
		}
	}

	public void Dispose()
	{
		_done = true;
		_tcp.DisconnectFromHost();
	}
}
