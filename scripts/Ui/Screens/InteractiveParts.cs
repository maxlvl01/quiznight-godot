// Interactive questions (a task players do with their microphone): the recorder on a player's screen, the voting list,
// the host's recording card, and the clip player that plays a recording on every screen when the host presses play.

using System;
using System.Linq;
using Godot;
using QuizNight.Net;
using QuizNight.Shared;

namespace QuizNight.Ui.Screens;

/// <summary>
/// Plays the recordings: the one the host plays (each press of play, on every screen at once), or one a player listens
/// to again. Lives as long as its screen, so it remembers how often the host has pressed play.
/// </summary>
public partial class ClipPlayer : Node
{
	readonly AssetSource _assets;
	readonly AudioStreamPlayer _audio = new();
	int _index = -1;
	int _plays;
	string? _pending;

	public ClipPlayer() : this(null!) { }

	public ClipPlayer(AssetSource assets)
	{
		_assets = assets;
		AddChild(_audio);
	}

	public override void _EnterTree() => _assets.Loaded += OnLoaded;
	public override void _ExitTree() => _assets.Loaded -= OnLoaded;

	public bool Playing => _audio.Playing;

	/// <summary>Follows the game: fetches everyone's recordings ahead, and plays the current one when the host presses play.</summary>
	public void Update(GameView game)
	{
		var ia = game.Interactive;
		if (game.Index != _index)
		{
			// (a new question, or joined in the middle: what was played before doesn't play again)
			_index = game.Index;
			_plays = ia?.Plays ?? 0;
			Stop();
		}
		if (ia == null || ia.Clips.Count == 0) return;
		foreach (var c in ia.Clips) _assets.GetSound(c.Key);
		if (ia.Plays == _plays) return;
		_plays = ia.Plays;
		Play(ia.Clips[Math.Clamp(ia.Clip, 0, ia.Clips.Count - 1)].Key);
	}

	public void Play(string key)
	{
		if (_assets.GetSound(key) is not { } sound)
		{
			_pending = key;
			return;
		}
		_pending = null;
		_audio.Stream = sound;
		_audio.Play();
	}

	public void Stop()
	{
		_pending = null;
		_audio.Stop();
	}

	void OnLoaded(string key)
	{
		if (key == _pending) Play(key);
	}
}

/// <summary>A player records the task: record, listen back, send (again, until the time is up).</summary>
public partial class RecorderPanel : VBoxContainer
{
	readonly ClientSession _client;
	readonly int _index;
	readonly MicRecorder _mic = new();
	readonly AudioStreamPlayer _listen = new();
	readonly Button _record;
	readonly Button _play;
	readonly Button _send;
	readonly ProgressBar _level;
	readonly Label _time;
	readonly Label _status;
	readonly Label _error = U.Error();
	byte[]? _wav;
	bool _sending;

	public RecorderPanel() : this(null!, 0, false) { }

	public RecorderPanel(ClientSession client, int index, bool sentBefore)
	{
		_client = client;
		_index = index;
		AddThemeConstantOverride("separation", 12);
		AddChild(_mic);
		AddChild(_listen);
		_mic.Full += () => Stopped();
		_record = U.Btn("● Record", "AccentButton", ToggleRecord, 22).MinSize(220, 0);
		_play = U.Btn("▶ Listen", "", Listen, 18);
		_send = U.Btn("Send it", "TealButton", Send, 20);
		_level = new ProgressBar { MaxValue = 1, ShowPercentage = false, CustomMinimumSize = new Vector2(260, 10) };
		_level.SizeFlagsVertical = SizeFlags.ShrinkCenter;
		_time = U.Mono("0.0 s", 15, C.Muted);
		_status = U.Muted("", 14, true, HorizontalAlignment.Center);
		AddChild(U.Muted(Lang.F("Record yourself doing the task (up to {0} s), listen back and send it. You can send a new one until the time's up.", QuestionLimits.RecordingMaxSec), 15, true, HorizontalAlignment.Center));
		AddChild(GameParts.Row(12, _record).With(r => r.Alignment = AlignmentMode.Center));
		AddChild(GameParts.Row(10, new TypeIcon(QuestionType.Interactive, 20), _level, _time).With(r => r.Alignment = AlignmentMode.Center));
		AddChild(GameParts.Row(12, _play, _send).With(r => r.Alignment = AlignmentMode.Center));
		AddChild(_status);
		AddChild(_error);
		_status.Text = Lang.T(sentBefore ? "✓ Sent. The host plays everyone's once the time is up." : "Nothing sent yet.");
		UpdateButtons();
	}

	void ToggleRecord()
	{
		if (_mic.Recording)
		{
			Stopped();
			return;
		}
		_listen.Stop();
		_error.Visible = false;
		_mic.Start();
		UpdateButtons();
	}

	void Stopped()
	{
		_wav = _mic.Stop();
		if (_wav == null)
		{
			_error.Text = Lang.T("Nothing recorded. Check that your microphone works (the bar moves when you talk).");
			_error.Visible = true;
		}
		UpdateButtons();
	}

	void Listen()
	{
		if (_wav == null) return;
		if (_listen.Playing)
		{
			_listen.Stop();
			return;
		}
		_listen.Stream = Storage.Media.LoadSound(_wav, "audio/wav");
		_listen.Play();
	}

	void Send()
	{
		if (_wav == null || _sending) return;
		_sending = true;
		_error.Visible = false;
		_status.Text = Lang.T("Sending…");
		UpdateButtons();
		_client.SendRecording(_index, _wav, result =>
		{
			if (!IsInstanceValid(this)) return;
			_sending = false;
			if (result.Ok)
			{
				_status.Text = Lang.T("✓ Sent. The host plays everyone's once the time is up.");
				_wav = null;
			}
			else
			{
				_status.Text = "";
				_error.Text = result.Error ?? "";
				_error.Visible = true;
			}
			UpdateButtons();
		});
	}

	void UpdateButtons()
	{
		_record.Text = Lang.T(_mic.Recording ? "■ Stop" : _wav != null ? "● Record again" : "● Record");
		_play.Disabled = _wav == null || _mic.Recording;
		_send.Disabled = _wav == null || _mic.Recording || _sending;
	}

	public override void _Process(double delta)
	{
		_level.Value = _mic.Level;
		if (_mic.Recording) _time.Text = $"{_mic.Seconds:0.0} s / {QuestionLimits.RecordingMaxSec} s";
		_play.Text = Lang.T(_listen.Playing ? "■ Stop" : "▶ Listen");
	}
}

public static class InteractiveParts
{
	/// <summary>"Bob's recording · 2 / 5 · 4.2 s" with their avatar (the one the host is on).</summary>
	public static Control ClipCard(RoomState room, InteractiveView ia, int size = 30)
	{
		var clip = ia.Clips[Math.Clamp(ia.Clip, 0, ia.Clips.Count - 1)];
		var p = room.Player(clip.PlayerId);
		var row = GameParts.Row(14, new Avatar(p, size * 2),
			U.V(2, U.Bold(Lang.F("{0}'s recording", p.Name), size).Raw(),
				U.Muted(Lang.F("Recording {0} / {1} · {2} s", ia.Clip + 1, ia.Clips.Count, clip.Sec.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)), 15, false)));
		row.Alignment = BoxContainer.AlignmentMode.Center;
		return row;
	}

	/// <summary>Players vote for the best recording (not their own), each with ▶ to listen again.</summary>
	public static Control VoteList(RoomState room, InteractiveView ia, string myId, ClipPlayer player, Action<int> vote)
	{
		var list = U.V(8, U.Bold("Which one was the best? Vote for one (not your own).", 20, null, true));
		for (var i = 0; i < ia.Clips.Count; i++)
		{
			var clip = ia.Clips[i];
			var p = room.Player(clip.PlayerId);
			var n = i;
			var mine = clip.PlayerId == myId;
			var row = GameParts.Row(10, new Avatar(p, 30), U.Bold(p.Name, 17).Raw().Grow(),
				U.Btn("▶", "SmallButton", () => player.Play(clip.Key), 15).Tip("Listen again"));
			var button = U.Btn(mine ? "Yours" : "Vote", mine ? "SmallButton" : "AccentButton", mine ? null : () => vote(n), 16);
			button.Disabled = mine;
			row.AddChild(button);
			list.AddChild(U.Panel(row, "Card2"));
		}
		return list;
	}
}
