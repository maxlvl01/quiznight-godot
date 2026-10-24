// The host's side of a room: runs the Room, talks to players over the transport, and sends
// everyone their own view of the state (players never see answers before the reveal).

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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
	/// <summary>What each connection's game said it is (JoinRequest.ClientId), to tell people apart.</summary>
	readonly Dictionary<string, string> _peerClient = new();

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

	public RoomState State => StateFor("host");

	/// <summary>The room as one viewer sees it, with everyone's download progress on a sound/video question.</summary>
	RoomState StateFor(string viewer)
	{
		var state = Room.ToState(viewer);
		if (state.Game is { Stage: Stage.Open } view && CurrentMediaId() is { } mediaId)
			view.MediaProgress = Room.Players.ToDictionary(p => p.Id, p => MediaPercent(p.Id, mediaId));
		return state;
	}

	string? CurrentMediaId() => Playing() ? Room.Game!.SharedFiles.FirstOrDefault(v => v.Index == Room.Game.Index)?.Id : null;

	public void Poll()
	{
		_net.Poll();
		if (Room.Tick()) Broadcast();
		SendMedia();
	}

	// ---------- videos for the players' screens ----------
	// From the start of a game, every player gets the game's sound and video files (Game.SharedFiles), encrypted, in question
	// order, from the current question on: as fast as their connection takes them (TrySend), in the
	// background. The key comes with the question once it's shown, so nobody can watch ahead.

	/// <summary>What players said they have: player id → file id → percent.</summary>
	readonly Dictionary<string, Dictionary<string, int>> _mediaProgress = new();

	/// <summary>How much of a sound/video file a player has, 0–100.</summary>
	public int MediaPercent(string playerId, string mediaId) =>
		_mediaProgress.TryGetValue(playerId, out var got) && got.TryGetValue(mediaId, out var p) ? p : 0;

	Game? _videoGame;
	readonly Dictionary<string, Task<byte[]>> _encrypted = new();
	/// <summary>Per connection: how far each video has been sent.</summary>
	readonly Dictionary<string, Dictionary<string, long>> _videoSent = new();

	void SendMedia()
	{
		var game = Playing() ? Room.Game : null;
		if (game != _videoGame)
		{
			_videoGame = game;
			_encrypted.Clear();
			_videoSent.Clear();
			_mediaProgress.Clear();
			foreach (var v in game?.SharedFiles ?? Array.Empty<Game.SharedMedia>()) _encrypted[v.Id] = Task.Run(() => Encrypt(v));
		}
		if (game == null || game.SharedFiles.Count == 0) return;
		foreach (var peer in _peerPlayer.Keys)
		{
			if (!_videoSent.TryGetValue(peer, out var sent)) _videoSent[peer] = sent = new();
			// (at most 8 MB per frame and connection; usually the connection says "full" much sooner)
			for (var n = 0; n < 32; n++)
			{
				var next = game.SharedFiles.Where(v => v.Index >= game.Index)
					.Select(v => (v.Id, Data: _encrypted[v.Id] is { IsCompletedSuccessfully: true } t ? t.Result : null))
					.FirstOrDefault(v => v.Data != null && sent.GetValueOrDefault(v.Id) < v.Data.Length);
				if (next.Data == null) break;
				var offset = sent.GetValueOrDefault(next.Id);
				var size = (int)Math.Min(MediaChunk.MaxData, next.Data.Length - offset);
				if (!_net.TrySend(peer, MediaChunk.Encode(next.Id, next.Data.Length, offset, next.Data.AsSpan((int)offset, size)))) break;
				sent[next.Id] = offset + size;
			}
		}
	}

	/// <summary>The video encrypted with AES-CBC, the 16-byte IV first.</summary>
	static byte[] Encrypt(Game.SharedMedia video)
	{
		var plain = DataUrl.Bytes(video.DataUrl, out _) ?? Array.Empty<byte>();
		using var aes = System.Security.Cryptography.Aes.Create();
		aes.Key = video.Key;
		var iv = System.Security.Cryptography.RandomNumberGenerator.GetBytes(16);
		var cipher = aes.EncryptCbc(plain, iv);
		var data = new byte[16 + cipher.Length];
		iv.CopyTo(data, 0);
		cipher.CopyTo(data, 16);
		return data;
	}

	string? PeerOf(string playerId) => _peerPlayer.FirstOrDefault(kv => kv.Value == playerId).Key;

	/// <summary>Everyone gets their own view. The host's screen reads the state directly.</summary>
	public void Broadcast()
	{
		Changed?.Invoke();
		foreach (var (peer, playerId) in _peerPlayer.ToList()) _net.Send(peer, Msg.Encode(Msg.State, StateFor(playerId)));
	}

	void OnPeerDisconnected(string peer)
	{
		_videoSent.Remove(peer);
		_peerClient.Remove(peer);
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
				Room.Leave(playerId);
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
			case Msg.MediaProgress when playerId != null:
				if (Msg.Read<MediaProgress>(msg.Data) is { } mp)
				{
					if (!_mediaProgress.TryGetValue(playerId, out var got)) _mediaProgress[playerId] = got = new();
					got[mp.Id] = Math.Clamp(mp.Percent, 0, 100);
					// (everyone sees the current question's download rings fill up)
					if (mp.Id == CurrentMediaId()) Broadcast();
					else Changed?.Invoke();
				}
				break;
			case Msg.Rename when playerId != null:
				if (Msg.Read<RenameRequest>(msg.Data) is { } rr && Room.Rename(playerId, rr.Name).Ok) Broadcast();
				break;
			case Msg.Joker when playerId != null && Playing():
				if (Msg.Read<JokerRequest>(msg.Data) is { } jr && Room.Game!.PlayJoker(playerId, jr.Kind, jr.On).Ok) Broadcast();
				break;
			case Msg.AssetRequest when playerId != null:
				if (Msg.Read<AssetRequest>(msg.Data) is { } ar) SendAsset(peer, ar.Key);
				break;
		}
	}

	bool Playing() => Room.Game != null && Room.Phase == Phase.Playing;

	void HandleJoin(string peer, JoinRequest req)
	{
		// Steam vouches for who is connecting; on LAN the player's app says who it is (over Steam too, if Steam didn't).
		var cid = req.ClientId.Trim();
		var id = _net.PeerSteamId(peer) is { } steamId ? "steam:" + steamId
			: cid.StartsWith("steam-") && ulong.TryParse(cid.AsSpan(6), out var claimed) && claimed != 0 ? "steam:" + claimed
			: cid is { Length: > 0 and <= 64 } ? "lan:" + cid
			: "lan:" + peer;
		// Two people can't be one player: if this id's seat is held right now by a connection whose game said it is
		// someone else, the ids came out the same by mistake; this one gets a seat of its own.
		if (PeerOf(id) is { } holderPeer && holderPeer != peer && _peerClient.TryGetValue(holderPeer, out var holderCid)
			&& holderCid != "" && cid != "" && holderCid != cid)
			id = "peer:" + peer;
		_peerClient[peer] = cid;
		Godot.GD.Print($"Join: \"{req.Name}\" on connection {peer} (Steam ID {_net.PeerSteamId(peer) ?? "unknown"}, client id \"{cid}\") -> {id}");
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

	/// <summary>Buzzer sound for this room (an Sfx id), sent to players so they hear it too.</summary>
	public string BuzzerSound
	{
		get => Room.Settings.BuzzerSound;
		set
		{
			Room.Settings.BuzzerSound = value;
			Broadcast();
		}
	}

	/// <summary>Podium sound for this room (an Sfx podium id).</summary>
	public string PodiumSound
	{
		get => Room.Settings.PodiumSound;
		set
		{
			Room.Settings.PodiumSound = value;
			Broadcast();
		}
	}

	public void UpdateSettings(int? answerTimeSec = null, bool? hostShows = null, bool? hostUnlocks = null, (string Kind, bool On)? joker = null)
	{
		if (Room.Phase != Phase.Lobby) return;
		Room.UpdateSettings(answerTimeSec, hostShows, hostUnlocks, joker);
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
		_resultsSent = false;
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
	public void ToggleWritten(string playerId) => GameAction(g => g.ToggleWritten(playerId));
	public void ToggleBonus(string playerId) => GameAction(g => g.ToggleBonus(playerId));

	/// <summary>The host adds or takes away points at will (any time during the game).</summary>
	public void AdjustScore(string playerId, int delta)
	{
		if (!Playing() || delta == 0 || Room.Players.All(p => p.Id != playerId)) return;
		Room.Award(playerId, delta);
		Broadcast();
	}
	public void ShowQuestion() => GameAction(g => g.ShowQuestion());
	/// <summary>Picture questions: show another picture (every player's screen follows).</summary>
	public void SetSlide(int slide) => GameAction(g => g.SetSlide(slide));
	/// <summary>Someone left and the game waits: go on without them.</summary>
	public void Continue() => GameAction(g => g.Continue());
	public void SetBuzzers(bool open) => GameAction(g => g.SetBuzzers(open));
	public void SkipTurn() => GameAction(g => g.SkipTurn());
	public void PlaceForFree() => GameAction(g => g.PlaceCurrentForFree());
	public void RevealPlacement() => GameAction(g => g.RevealPlacement());
	public void NextTurn() => GameAction(g => g.NextTurn());

	public void Next()
	{
		if (!Playing()) return;
		Room.Next();
		SaveResultsIfFinished();
		Broadcast();
	}

	public void Finish()
	{
		if (!Playing()) return;
		Room.Finish();
		SaveResultsIfFinished();
		Broadcast();
	}

	bool _resultsSent;

	/// <summary>The game is over: its results go to Max's results repo (real games over Steam only, not LAN tests).</summary>
	void SaveResultsIfFinished()
	{
		if (Room.Phase != Phase.Finished || _resultsSent) return;
		_resultsSent = true;
		if (Mode != NetMode.Steam || !Storage.ResultsUpload.Enabled) return;
		var (name, csv) = Storage.Results.Make(Room);
		Storage.ResultsUpload.Send(name, csv);
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
