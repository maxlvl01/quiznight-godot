// Steam mode. Runs as Spacewar (app 480), Valve's public test app: fine for a private game
// among friends, but Steam shows everyone as "playing Spacewar".
//
// The host creates a public lobby tagged with the room code and its Steam ID, then listens on a
// relay socket. Players look the code up in the lobby list and connect to the host through
// Steam's relay network (no port forwarding, works across the internet).

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Godot;
using QuizNight.Shared;
using Steamworks;
using Steamworks.Data;

namespace QuizNight.Net;

public static class SteamService
{
	public const uint AppId = 480; // Spacewar
	const string KeyGame = "quiznight";
	const string KeyCode = "quiznight_code";
	const string KeyHost = "quiznight_host";

	public static bool Available { get; private set; }
	/// <summary>Why Steam isn't available, for the home screen.</summary>
	public static string? InitError { get; private set; }

	public static string PlayerName => Available ? SteamClient.Name : "";
	public static ulong MySteamId => Available ? SteamClient.SteamId.Value : 0;

	public static void Init()
	{
		if (Available) return;
		try
		{
			// Callbacks are run by hand on the main thread (RunCallbacks), so Godot code is safe in them.
			SteamClient.Init(AppId, false);
			Available = SteamClient.IsValid;
			if (!Available) InitError = "Steam didn't start.";
			else SteamNetworkingUtils.InitRelayNetworkAccess();
		}
		catch (Exception e)
		{
			Available = false;
			InitError = e is DllNotFoundException ? "steam_api64.dll is missing next to the game." : "Steam isn't running. Start Steam and restart Quiznight to play over Steam.";
			GD.Print($"Steam not available: {e.Message}");
		}
	}

	public static void RunCallbacks()
	{
		if (Available) SteamClient.RunCallbacks();
	}

	public static void Shutdown()
	{
		if (!Available) return;
		Available = false;
		SteamClient.Shutdown();
	}

	static LobbyQuery CodeQuery(string code) =>
		SteamMatchmaking.LobbyList.WithKeyValue(KeyCode, code).FilterDistanceWorldwide().WithMaxResults(10);

	/// <summary>The Steam ID of whoever hosts this code, or null if there is no such room.</summary>
	public static async Task<ulong?> FindHost(string code)
	{
		var lobbies = await CodeQuery(code).RequestAsync();
		foreach (var lobby in lobbies ?? Array.Empty<Lobby>())
			if (lobby.GetData(KeyGame) == "1" && ulong.TryParse(lobby.GetData(KeyHost), out var host)) return host;
		return null;
	}

	/// <summary>Creates the lobby that makes this code findable. Null if Steam refused.</summary>
	public static async Task<Lobby?> CreateLobby(string code)
	{
		var lobby = await SteamMatchmaking.CreateLobbyAsync(250);
		if (lobby is not { } l) return null;
		l.SetPublic();
		l.SetJoinable(true);
		l.SetData(KeyGame, "1");
		l.SetData(KeyCode, code);
		l.SetData(KeyHost, SteamClient.SteamId.Value.ToString());
		return l;
	}

	/// <summary>True if some lobby already uses this code (codes must be unique across all Spacewar lobbies we see).</summary>
	public static async Task<bool> CodeTaken(string code) => (await CodeQuery(code).RequestAsync())?.Length > 0;

	internal static byte[] Copy(IntPtr data, int size)
	{
		var bytes = new byte[size];
		Marshal.Copy(data, bytes, 0, size);
		return bytes;
	}

	internal static string Describe(NetConnectionEnd reason) => reason switch
	{
		NetConnectionEnd.App_Generic => "The host closed the connection.",
		NetConnectionEnd.Remote_Timeout or NetConnectionEnd.Misc_Timeout => "The connection to the host timed out.",
		NetConnectionEnd.Local_OfflineMode or NetConnectionEnd.Local_ManyRelayConnectivity or NetConnectionEnd.Local_HostedServerPrimaryRelay
			or NetConnectionEnd.Local_NetworkConfig => "Steam couldn't reach its relay network. Check your internet connection.",
		_ => Lang.F("Lost the connection to the host ({0}).", reason),
	};
}

public sealed class SteamHostTransport : IHostTransport
{
	public event Action<string>? PeerConnected;
	public event Action<string>? PeerDisconnected;
	public event Action<string, byte[]>? Received;

	sealed class Socket : SocketManager
	{
		public SteamHostTransport Owner = null!;

		public override void OnConnecting(Connection connection, ConnectionInfo info) => connection.Accept();

		public override void OnConnected(Connection connection, ConnectionInfo info)
		{
			base.OnConnected(connection, info);
			Owner.OnPeerConnected(connection, info);
		}

		public override void OnDisconnected(Connection connection, ConnectionInfo info)
		{
			base.OnDisconnected(connection, info);
			Owner.OnPeerDisconnected(connection);
		}

		public override void OnMessage(Connection connection, NetIdentity identity, IntPtr data, int size, long messageNum, long recvTime, int channel) =>
			Owner.Received?.Invoke(connection.Id.ToString(), SteamService.Copy(data, size));
	}

	readonly Lobby _lobby;
	readonly Socket _socket;
	readonly Dictionary<string, (Connection Conn, ulong SteamId)> _peers = new();

	// Steam allows 512 KB per reliable message; stay well below it.
	public int MaxMessageBytes => 300 * 1024;

	SteamHostTransport(Lobby lobby, Socket socket)
	{
		_lobby = lobby;
		_socket = socket;
		_socket.Owner = this;
	}

	/// <summary>Opens a room on Steam under a fresh code. Returns the transport and code, or an error.</summary>
	public static async Task<(SteamHostTransport? Transport, string Code, string? Error)> Create(Func<string> newCode)
	{
		if (!SteamService.Available) return (null, "", SteamService.InitError ?? "Steam isn't available.");
		var code = newCode();
		for (var i = 0; i < 3 && await SteamService.CodeTaken(code); i++) code = newCode();
		var lobby = await SteamService.CreateLobby(code);
		if (lobby is not { } l) return (null, "", "Steam couldn't create a lobby. Try again in a moment.");
		var socket = SteamNetworkingSockets.CreateRelaySocket<Socket>(0);
		if (socket == null)
		{
			l.Leave();
			return (null, "", "Steam couldn't open a relay socket.");
		}
		return (new SteamHostTransport(l, socket), code, null);
	}

	void OnPeerConnected(Connection conn, ConnectionInfo info)
	{
		var id = conn.Id.ToString();
		_peers[id] = (conn, info.Identity.SteamId.Value);
		PeerConnected?.Invoke(id);
	}

	void OnPeerDisconnected(Connection conn)
	{
		var id = conn.Id.ToString();
		if (_peers.Remove(id)) PeerDisconnected?.Invoke(id);
	}

	public string? PeerSteamId(string peer) => _peers.TryGetValue(peer, out var p) ? p.SteamId.ToString() : null;

	public void Send(string peer, byte[] data)
	{
		if (_peers.TryGetValue(peer, out var p)) p.Conn.SendMessage(data, SendType.Reliable);
	}

	public void Disconnect(string peer)
	{
		if (!_peers.Remove(peer, out var p)) return;
		p.Conn.Flush();
		p.Conn.Close(true, 0, "Closed by host");
		PeerDisconnected?.Invoke(peer);
	}

	public void Poll() => _socket.Receive();

	public void Dispose()
	{
		foreach (var p in _peers.Values)
		{
			p.Conn.Flush();
			p.Conn.Close(true, 0, "Room closed");
		}
		_peers.Clear();
		_socket.Close();
		_lobby.Leave();
	}
}

public sealed class SteamClientTransport : IClientTransport
{
	public event Action? Connected;
	public event Action<string>? Disconnected;
	public event Action<byte[]>? Received;

	sealed class Conn : ConnectionManager
	{
		public SteamClientTransport Owner = null!;

		public override void OnConnected(ConnectionInfo info)
		{
			base.OnConnected(info);
			Owner.Connected?.Invoke();
		}

		public override void OnDisconnected(ConnectionInfo info)
		{
			base.OnDisconnected(info);
			Owner.Fail(SteamService.Describe(info.EndReason));
		}

		public override void OnMessage(IntPtr data, int size, long messageNum, long recvTime, int channel) =>
			Owner.Received?.Invoke(SteamService.Copy(data, size));
	}

	readonly Conn _conn;
	bool _done;

	SteamClientTransport(Conn conn)
	{
		_conn = conn;
		_conn.Owner = this;
	}

	/// <summary>Looks up the room code and connects to its host.</summary>
	public static async Task<(SteamClientTransport? Transport, string? Error)> Connect(string code)
	{
		if (!SteamService.Available) return (null, SteamService.InitError ?? "Steam isn't available.");
		var host = await SteamService.FindHost(code);
		if (host is not { } h) return (null, "No room with that code. Check the code and try again.");
		if (h == SteamService.MySteamId)
			return (null, "That's your own room. Join from another PC (another Steam account), or use LAN mode to test on one PC.");
		var conn = SteamNetworkingSockets.ConnectRelay<Conn>(h, 0);
		if (conn == null) return (null, "Steam couldn't start the connection.");
		return (new SteamClientTransport(conn), null);
	}

	void Fail(string reason)
	{
		if (_done) return;
		_done = true;
		Disconnected?.Invoke(reason);
	}

	public void Send(byte[] data)
	{
		if (!_done) _conn.Connection.SendMessage(data, SendType.Reliable);
	}

	public void Poll()
	{
		if (!_done) _conn.Receive();
	}

	public void Dispose()
	{
		_done = true;
		_conn.Connection.Flush();
		_conn.Close();
	}
}
