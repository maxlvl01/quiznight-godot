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
		// (the mini games: everyone in the lobby whose game has them, and the host)
		Arcade.Players = () => Room.Players.Where(p => p.Connected && p.Version != "").Select(p => p.Id).Append(ArcadeGame.HostId);
	}

	public RoomState State => StateFor("host");

	/// <summary>
	/// The room as one viewer sees it, with everyone's downloads: of the whole quiz (lobby), and of the current question's
	/// pictures, sound or video (the rings next to the names during the game).
	/// </summary>
	RoomState StateFor(string viewer)
	{
		var state = Room.ToState(viewer);
		if (Room.Phase == Phase.Lobby) state.Arcade = Arcade.View();
		if (Room.SharedFiles.Count > 0) state.Downloads = Room.Players.ToDictionary(p => p.Id, p => DownloadPercent(p.Id, Room.SharedFiles));
		if (state.Game is { Stage: Stage.Open } view && Playing() && Room.Game!.CurrentFiles().ToList() is { Count: > 0 } current)
			view.MediaProgress = Room.Players.ToDictionary(p => p.Id, p => DownloadPercent(p.Id, current));
		return state;
	}

	/// <summary>How much of these files a player has, 0–100, weighted by size.</summary>
	int DownloadPercent(string playerId, IReadOnlyCollection<Game.SharedMedia> files)
	{
		double total = 0, got = 0;
		foreach (var f in files)
		{
			// (as double: size × percent overflows an int for files over ~20 MB)
			total += f.DataUrl.Length;
			got += (double)f.DataUrl.Length * MediaPercent(playerId, f.Id) / 100.0;
		}
		return total <= 0 ? 100 : Math.Clamp((int)Math.Floor(got * 100 / total), 0, 100);
	}

	string? CurrentMediaId() => Playing() ? Room.Game!.CurrentMedia?.Id : null;

	/// <summary>Download progress came in: everyone's rings update, a few times a second at most.</summary>
	bool _progressChanged;
	double _progressSentAt;

	public void Poll()
	{
		_net.Poll();
		if (Room.Tick()) Broadcast();
		if (Room.Phase == Phase.Lobby) ArcadeDone(Arcade.Tick(Godot.Engine.GetMainLoop() is Godot.SceneTree tree ? tree.Root.GetProcessDeltaTime() : 1 / 60.0));
		SendMedia();
		var now = Godot.Time.GetTicksMsec() / 1000.0;
		if (_progressChanged && now - _progressSentAt > 0.4)
		{
			_progressChanged = false;
			_progressSentAt = now;
			Broadcast();
		}
	}

	// ---------- the quiz's files for the players' screens ----------
	// From the lobby on, every player gets the quiz's pictures, sounds and videos (Room.SharedFiles), encrypted, in
	// question order (during the game: from the current question on), as fast as their connection takes them (TrySend),
	// in the background. The key comes with the question once it's shown, so nobody can look ahead.

	/// <summary>What players said they have: player id → file id → percent.</summary>
	readonly Dictionary<string, Dictionary<string, int>> _mediaProgress = new();

	/// <summary>How much of a sound/video file a player has, 0–100.</summary>
	public int MediaPercent(string playerId, string mediaId) =>
		_mediaProgress.TryGetValue(playerId, out var got) && got.TryGetValue(mediaId, out var p) ? p : 0;

	readonly Dictionary<string, Task<byte[]>> _encrypted = new();
	/// <summary>Per connection: how far each file has been sent.</summary>
	readonly Dictionary<string, Dictionary<string, long>> _videoSent = new();

	void SendMedia()
	{
		var files = Room.SharedFiles;
		if (files.Count == 0) return;
		// (encrypted once, in the background, as soon as the room opens)
		if (_encrypted.Count == 0)
			foreach (var v in files) _encrypted[v.Id] = Task.Run(() => Encrypt(v));
		var from = Playing() ? Room.Game!.Index : 0;
		foreach (var peer in _peerPlayer.Keys)
		{
			if (!_videoSent.TryGetValue(peer, out var sent)) _videoSent[peer] = sent = new();
			// (at most 8 MB per frame and connection; usually the connection says "full" much sooner)
			for (var n = 0; n < 32; n++)
			{
				var next = files.Where(v => v.Index >= from)
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
		Trace.Log("host", $"connection {peer} closed");
		_videoSent.Remove(peer);
		_peerClient.Remove(peer);
		if (!_peerPlayer.Remove(peer, out var playerId)) return;
		// A newer connection may already have taken over this seat.
		if (PeerOf(playerId) != null) return;
		Room.Disconnect(playerId);
		ArcadeDone(Arcade.Gone(playerId));
		Broadcast();
	}

	void OnReceived(string peer, byte[] bytes)
	{
		if (Msg.Decode(bytes) is not { } msg) return;
		_peerPlayer.TryGetValue(peer, out var playerId);
		// (download progress comes several times a second: not worth a line each)
		if (msg.Type != Msg.MediaProgress) Trace.Log("host", $"from {playerId ?? peer}: {msg.Type}");
		switch (msg.Type)
		{
			case Msg.Join:
				HandleJoin(peer, Msg.Read<JoinRequest>(msg.Data) ?? new JoinRequest());
				break;
			case Msg.Leave when playerId != null:
				_peerPlayer.Remove(peer);
				Room.Leave(playerId);
				ArcadeDone(Arcade.Gone(playerId));
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
				if (Msg.Read<MediaProgress>(msg.Data) is { } mp && Room.SharedFiles.Any(f => f.Id == mp.Id))
				{
					if (!_mediaProgress.TryGetValue(playerId, out var got)) _mediaProgress[playerId] = got = new();
					got[mp.Id] = Math.Clamp(mp.Percent, 0, 100);
					// (everyone sees the download rings fill up: in the lobby, and on the current question)
					_progressChanged = true;
				}
				break;
			case Msg.Rename when playerId != null:
				if (Msg.Read<RenameRequest>(msg.Data) is { } rr && Room.Rename(playerId, rr.Name).Ok) Broadcast();
				break;
			case Msg.Joker when playerId != null && Playing():
				if (Msg.Read<JokerRequest>(msg.Data) is { } jr && Room.Game!.PlayJoker(playerId, jr.Kind, jr.On).Ok) Broadcast();
				break;
			case Msg.Recording when playerId != null:
				if (Msg.Read<RecordingPart>(msg.Data) is { } rp) ReceiveRecording(peer, playerId, rp);
				break;
			case Msg.Arcade when playerId != null && Room.Phase == Phase.Lobby:
				if (Msg.Read<ArcadeRequest>(msg.Data) is { } arcade) ArcadeDone(Arcade.Request(playerId, arcade));
				break;
			case Msg.AssetRequest when playerId != null:
				if (Msg.Read<AssetRequest>(msg.Data) is { } ar) SendAsset(peer, ar.Key);
				break;
		}
	}

	bool Playing() => Room.Game != null && Room.Phase == Phase.Playing;

	/// <summary>Who the host removed (player ids, and what their game called itself): they can't come back into this room.</summary>
	readonly HashSet<string> _kicked = new();

	/// <summary>
	/// Whether a Steam account is the host's Steam friend. Steam mode only; a stand-in for the self-test.
	/// </summary>
	public Func<ulong, bool> IsFriend = id => SteamService.Available && new Steamworks.Friend(id).IsFriend;

	void Refuse(string peer, string error)
	{
		Godot.GD.Print($"Join refused on connection {peer}: {error}");
		_net.Send(peer, Msg.Encode(Msg.Joined, new JoinReply { Ok = false, Error = error }));
		_net.Disconnect(peer);
	}

	void HandleJoin(string peer, JoinRequest req)
	{
		// Over Steam, Steam vouches for who is connecting, and nobody else gets a seat: a game claiming to be some Steam
		// account could otherwise take that player's seat (and points). On LAN the player's game says who it is.
		var cid = (req.ClientId ?? "").Trim();
		var steamId = _net.PeerSteamId(peer);
		if (Mode == NetMode.Steam)
		{
			if (steamId == null)
			{
				Refuse(peer, "Steam couldn't confirm who you are. Try joining again.");
				return;
			}
			// (the code isn't a password, but without it nobody gets in by guessing the host's Steam ID)
			if (!string.Equals(Limits.NormalizeCode(req.Code ?? ""), Room.Code, StringComparison.Ordinal))
			{
				Refuse(peer, "That room code doesn't match. Check it and try again.");
				return;
			}
			if (Room.Settings.FriendsOnly && !(ulong.TryParse(steamId, out var sid) && IsFriend(sid)))
			{
				Refuse(peer, "Only the host's Steam friends can join this room.");
				return;
			}
		}
		// (a game that can't read this one's messages; games before 0.12 send no number and may join)
		if (req.Protocol != 0 && req.Protocol != Limits.Protocol)
		{
			Refuse(peer, Lang.F("Your Quiznight ({0}) doesn't fit the host's ({1}). Update to the same version, then join again.", req.Version, Godot.ProjectSettings.GetSetting("application/config/version").AsString()));
			return;
		}
		var id = steamId != null ? "steam:" + steamId
			: cid is { Length: > 0 and <= 64 } && !cid.StartsWith("steam") ? "lan:" + cid
			: "lan:" + peer;
		if (_kicked.Contains(id) || cid != "" && _kicked.Contains("client:" + cid))
		{
			Refuse(peer, "The host removed you from this room.");
			return;
		}
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
		player.Version = AppVersion.Parse(req.Version) != null ? req.Version : "";
		// Coming back on a new connection: the old one is stale.
		if (PeerOf(id) is { } old && old != peer)
		{
			_peerPlayer.Remove(old);
			_net.Disconnect(old);
		}
		_peerPlayer[peer] = id;
		(_net as LanHostTransport)?.MarkJoined(peer);
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
		ArcadeDone(Arcade.Gone(playerId));
		// (and they stay out: rejoining with the code would otherwise just bring them back)
		_kicked.Add(playerId);
		if (PeerOf(playerId) is { } peer)
		{
			if (_peerClient.TryGetValue(peer, out var cid) && cid != "") _kicked.Add("client:" + cid);
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

	public void UpdateSettings(int? answerTimeSec = null, bool? hostShows = null, bool? hostUnlocks = null, (string Kind, bool On)? joker = null,
		bool? categories = null, bool? friendsOnly = null)
	{
		if (Room.Phase != Phase.Lobby) return;
		Room.UpdateSettings(answerTimeSec, hostShows, hostUnlocks, joker, categories, friendsOnly);
		Broadcast();
	}

	public Result Start()
	{
		var result = Room.Start();
		if (!result.Ok) return result;
		// (the quiz starts: every mini game ends, counting for nobody)
		SendFrames(Arcade.StopAll());
		Broadcast();
		return result;
	}

	// ---------- the lobby's mini games (Shared/Arcade.cs, ArcadeHost) ----------

	/// <summary>The mini games: challenges, matches and this lobby's records.</summary>
	public readonly ArcadeHost Arcade = new();

	/// <summary>A match the host is in moved on (the host's screen draws it).</summary>
	public event Action<ArcadeFrame>? ArcadeFrame;

	/// <summary>The host's own mini game requests (plays as ArcadeGame.HostId).</summary>
	public void ArcadeRequest(ArcadeRequest request)
	{
		if (Room.Phase == Phase.Lobby) ArcadeDone(Arcade.Request(ArcadeGame.HostId, request));
	}

	void ArcadeDone((bool Changed, List<ArcadeFrame> Frames) done)
	{
		SendFrames(done.Frames);
		if (done.Changed) Broadcast();
	}

	void SendFrames(List<ArcadeFrame> frames)
	{
		foreach (var frame in frames)
		foreach (var id in new[] { frame.A, frame.B })
		{
			if (id == ArcadeGame.HostId) ArcadeFrame?.Invoke(frame);
			else if (PeerOf(id) is { } peer) _net.Send(peer, Msg.Encode(Msg.ArcadeFrame, frame));
		}
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
		Room.AdjustScore(playerId, delta);
		Broadcast();
	}
	public void ShowQuestion() => GameAction(g => g.ShowQuestion());
	/// <summary>Picture questions: show another picture (every player's screen follows).</summary>
	public void SetSlide(int slide) => GameAction(g => g.SetSlide(slide));
	/// <summary>Someone left and the game waits: go on without them.</summary>
	public void Continue() => GameAction(g => g.Continue());
	public void SetBuzzers(bool open) => GameAction(g => g.SetBuzzers(open));
	public void EndRecording() => GameAction(g => g.EndRecordingNow());
	public void NextHint() => GameAction(g => g.NextHint());
	public void SetClip(int clip) => GameAction(g => g.SetClip(clip));
	public void PlayClip() => GameAction(g => g.PlayClip());
	public void StartVoting() => GameAction(g => g.StartVoting());
	public void PickFavourite(string playerId) => GameAction(g => g.PickFavourite(playerId));

	/// <summary>Interactive recordings come in parts; the whole one goes to the game, and the player hears back once.</summary>
	readonly Dictionary<string, (int Index, string?[] Parts)> _recordingParts = new();

	void ReceiveRecording(string peer, string playerId, RecordingPart part)
	{
		// (a recording is at most 15 s of 16 kHz sound: about 640 KB as base64, sent in 200 KB parts)
		if (part.Parts < 1 || part.Parts > 8 || part.Part < 0 || part.Part >= part.Parts || (part.Data?.Length ?? 0) > 200_000 || !Playing()) return;
		if (!_recordingParts.TryGetValue(playerId, out var entry) || entry.Index != part.Index || entry.Parts.Length != part.Parts)
			_recordingParts[playerId] = entry = (part.Index, new string?[part.Parts]);
		entry.Parts[part.Part] = part.Data;
		if (Array.Exists(entry.Parts, p => p == null)) return;
		_recordingParts.Remove(playerId);
		Result result;
		try
		{
			result = !Playing() ? Result.Fail("The quiz is not running.")
				: Room.Game!.SubmitRecording(playerId, part.Index, Convert.FromBase64String(string.Concat(entry.Parts)));
		}
		catch (FormatException)
		{
			result = Result.Fail("That recording didn't arrive properly.");
		}
		_net.Send(peer, Msg.Encode(Msg.AnswerResult, result));
		if (result.Ok) Broadcast();
	}
	public void ReopenBuzzers() => GameAction(g => g.ReopenBuzzers());
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
		var (name, csv) = Storage.Results.Make(Room, (SteamService.PlayerName, "steam:" + SteamService.MySteamId));
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
