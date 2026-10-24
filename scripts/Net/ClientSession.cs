// A player's side of a room: joins, receives the state, sends buzzes and answers, fetches
// question images, and reconnects on its own if the connection drops.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;
using QuizNight.Shared;

namespace QuizNight.Net;

public sealed class ClientSession : IDisposable
{
	/// <summary>How long we keep trying to get back in after the connection drops.</summary>
	const double ReconnectForSec = 60;
	const double ReconnectEverySec = 3;

	public readonly NetMode Mode;
	/// <summary>Room code (Steam) or host address (LAN).</summary>
	public readonly string Target;
	string _name;
	readonly string _clientId;
	readonly Func<Task<(IClientTransport? Transport, string? Error)>> _connect;

	IClientTransport? _net;
	readonly Queue<Action<Result>> _pendingAnswers = new();
	bool _ended;
	double? _lostAt;
	double _nextRetryAt;
	bool _connecting;

	public RoomState? State { get; private set; }
	public string? MyId { get; private set; }
	/// <summary>True while the connection is down and we are trying to get back in.</summary>
	public bool Reconnecting => _lostAt != null;
	public readonly ClientAssets Assets;
	/// <summary>The game's videos, for playing them on this screen too.</summary>
	public readonly ClientMedia Media = new();

	public event Action? Changed;
	/// <summary>The session is over (left, kicked, room closed, couldn't join). Carries the reason to show.</summary>
	public event Action<string?>? Ended;

	public ClientSession(NetMode mode, string target, string name, string clientId,
		Func<Task<(IClientTransport? Transport, string? Error)>> connect)
	{
		Mode = mode;
		Target = target;
		_name = name;
		_clientId = clientId;
		_connect = connect;
		Assets = new ClientAssets(key => _net?.Send(Msg.Encode(Msg.AssetRequest, new AssetRequest { Key = key })), Media);
		Media.ProgressChanged += (id, percent) => _net?.Send(Msg.Encode(Msg.MediaProgress, new MediaProgress { Id = id, Percent = percent }));
	}

	public static ClientSession Lan(string address, int port, string name, string clientId) =>
		new(NetMode.Lan, address, name, clientId,
			() => Task.FromResult<(IClientTransport?, string?)>((new LanClientTransport(address, port), null)));

	// (the client id is this player's own Steam ID: the host falls back on it if Steam doesn't tell it who connected)
	public static ClientSession Steam(string code, string name) =>
		new(NetMode.Steam, code, name, SteamService.MySteamId is var me and not 0 ? "steam-" + me : "", async () =>
		{
			var (t, error) = await SteamClientTransport.Connect(code);
			return (t, error);
		});

	static double Now() => Time.GetTicksMsec() / 1000.0;

	public async void Start() => await Connect();

	async Task Connect()
	{
		if (_connecting || _ended) return;
		_connecting = true;
		var (net, error) = await _connect();
		_connecting = false;
		if (_ended)
		{
			net?.Dispose();
			return;
		}
		if (net == null)
		{
			if (MyId == null) End(error);
			else _nextRetryAt = Now() + ReconnectEverySec; // was in the room: keep trying for a while
			return;
		}
		_net = net;
		net.Connected += () => net.Send(Msg.Encode(Msg.Join, new JoinRequest { Name = _name, ClientId = _clientId }));
		net.Disconnected += reason => OnLost(net, reason);
		net.Received += OnReceived;
	}

	void OnLost(IClientTransport net, string reason)
	{
		if (net != _net || _ended) return;
		_net = null;
		net.Dispose();
		FailPendingAnswers("The connection dropped. Try again in a moment.");
		if (MyId == null)
		{
			End(reason); // never got in: nothing to come back to
			return;
		}
		_lostAt ??= Now();
		_nextRetryAt = Now() + ReconnectEverySec;
		Changed?.Invoke();
	}

	public void Poll()
	{
		if (_ended) return;
		_net?.Poll();
		Assets.Poll();
		if (_lostAt is { } lost && _net == null && !_connecting)
		{
			if (Now() - lost > ReconnectForSec) End("Lost the connection to the host.");
			else if (Now() >= _nextRetryAt) _ = Connect();
		}
	}

	void OnReceived(byte[] bytes)
	{
		if (bytes.Length > 0 && bytes[0] == MediaChunk.Marker)
		{
			Media.Receive(bytes);
			return;
		}
		if (Msg.Decode(bytes) is not { } msg) return;
		switch (msg.Type)
		{
			case Msg.Joined:
				var reply = Msg.Read<JoinReply>(msg.Data);
				if (reply is not { Ok: true })
				{
					// Coming back after a drop, but the seat is gone (e.g. the lobby gave it away).
					End(MyId != null ? "Your seat in that room is gone. Join again with the code." : reply?.Error ?? "Couldn't join.");
					return;
				}
				var cameBack = MyId != null;
				MyId = reply.PlayerId;
				_lostAt = null;
				if (cameBack) Assets.RetryMissing();
				// (the host forgets a connection's downloads when it drops)
				foreach (var (id, percent) in Media.AllProgress()) _net?.Send(Msg.Encode(Msg.MediaProgress, new MediaProgress { Id = id, Percent = percent }));
				Changed?.Invoke();
				break;
			case Msg.State:
				if (Msg.Read<RoomState>(msg.Data) is not { } state) return;
				ServerClock.Sync(state.ServerNow);
				State = state;
				Assets.Learn(state.Game?.Question.Files);
				Changed?.Invoke();
				break;
			case Msg.AnswerResult:
				var result = Msg.Read<Result>(msg.Data) ?? Result.Fail("Something went wrong.");
				if (_pendingAnswers.Count > 0) _pendingAnswers.Dequeue()(result);
				break;
			case Msg.Asset:
				if (Msg.Read<AssetPart>(msg.Data) is { } part) Assets.Receive(part);
				break;
			case Msg.Closed:
				End(Msg.Read<ClosedNotice>(msg.Data)?.Reason ?? "The host closed the room.");
				break;
			case Msg.Kicked:
				End("The host removed you from the room.");
				break;
		}
	}

	void FailPendingAnswers(string error)
	{
		while (_pendingAnswers.Count > 0) _pendingAnswers.Dequeue()(Result.Fail(error));
	}

	// ---------- player actions ----------

	public void Buzz() => _net?.Send(Msg.Encode(Msg.Buzz));

	/// <summary>Change your name (waiting room only); it's also the name you join with next time.</summary>
	public void Rename(string name)
	{
		_name = name;
		_net?.Send(Msg.Encode(Msg.Rename, new RenameRequest { Name = name }));
	}

	/// <summary>Play a joker for the next question, or take it back.</summary>
	public void PlayJoker(string kind, bool on) => _net?.Send(Msg.Encode(Msg.Joker, new JokerRequest { Kind = kind, On = on }));

	public void Answer(int index, AnswerValue value, Action<Result> onResult)
	{
		if (_net == null)
		{
			onResult(Result.Fail("Not connected right now. Try again in a moment."));
			return;
		}
		_pendingAnswers.Enqueue(onResult);
		_net.Send(Msg.Encode(Msg.Answer, new AnswerRequest { Index = index, Value = value }));
	}

	public void Leave()
	{
		_net?.Send(Msg.Encode(Msg.Leave));
		End(null);
	}

	void End(string? reason)
	{
		if (_ended) return;
		_ended = true;
		FailPendingAnswers("You left the room.");
		_net?.Dispose();
		_net = null;
		Media.Dispose();
		Ended?.Invoke(reason);
	}

	public void Dispose() => End(null);
}

/// <summary>
/// Deadlines come in host time. Each state carries the host's clock, so countdowns stay right even
/// when a player's clock is off.
/// </summary>
public static class ServerClock
{
	static long _offset;

	public static long LocalNow() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
	public static void Sync(long serverNow) => _offset = LocalNow() - serverNow;
	public static void Reset() => _offset = 0;
	public static long Now() => LocalNow() - _offset;

	/// <summary>Seconds left until a host-time deadline.</summary>
	public static double? SecondsLeft(long? endsAt) => endsAt is { } e ? Math.Max(0, (e - Now()) / 1000.0) : null;
}
