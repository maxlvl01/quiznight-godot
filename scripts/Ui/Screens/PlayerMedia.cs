// What the host's screen plays, on a player's screen too, in step with it: the sound of a sound question and
// the picture of an image reveal (videos: PlayerVideo). Everything follows the host's play clock in the state.

using System;
using Godot;
using QuizNight.Net;
using QuizNight.Shared;
using QuizNight.Storage;

namespace QuizNight.Ui.Screens;

public static class PlayClock
{
	/// <summary>How long the host's sound, video or image reveal has run, in seconds, right now.</summary>
	public static double Seconds(RoomState room)
	{
		var game = room.Game!;
		var playing = game.MediaPlaying && game.Stage == Stage.Open;
		var sinceState = playing ? Math.Max(0, ServerClock.Now() - room.ServerNow) : 0;
		return (game.MediaPlayedMs + sinceState) / 1000.0;
	}

	/// <summary>Whether the host's media runs right now.</summary>
	public static bool Running(RoomState room) => room.Game is { MediaPlaying: true, Stage: Stage.Open };
}

/// <summary>A sound question's sound, playing along with the host's (the player can turn it off).</summary>
public partial class PlayerSound : VBoxContainer
{
	readonly ClientSession _client;
	readonly Func<RoomState> _room;
	readonly string _id;
	readonly string _key;
	readonly string _mime;
	readonly AudioStreamPlayer _audio = new() { Bus = Prefs.MusicBus };
	readonly SoundBars _bars = new();
	readonly Label _status = U.Muted("", 15, false, HorizontalAlignment.Center);
	readonly Button _sound;
	double _seekedAt = -10;

	public PlayerSound() : this(null!, () => new RoomState(), "", "", "") { }

	public PlayerSound(ClientSession client, Func<RoomState> room, string id, string key, string mime)
	{
		_client = client;
		_room = room;
		_id = id;
		_key = key;
		_mime = mime;
		AddThemeConstantOverride("separation", 8);
		AddChild(_audio);
		AddChild(U.Panel(_bars, "Inset"));
		AddChild(_status);
		_sound = U.Small("", () =>
		{
			PlayerVideo.SoundOn = !PlayerVideo.SoundOn;
			ApplySound();
		});
		AddChild(GameParts.Row(10, _sound, U.Muted("The sound plays along with the host's.", 13)));
		ApplySound();
	}

	void ApplySound()
	{
		_audio.VolumeDb = PlayerVideo.SoundOn ? 0 : -80;
		_sound.Text = PlayerVideo.SoundOn ? Lang.T("Sound: on") : Lang.T("Sound: off");
	}

	public override void _Process(double delta)
	{
		var room = _room();
		if (room.Game is not { } game) return;
		if (_audio.Stream == null)
		{
			if (_client.Media.GetSound(_id, _key, _mime) is { } stream) _audio.Stream = stream;
			else
			{
				var p = _client.Media.Progress(_id);
				_status.Text = p >= 1 ? Lang.T("Getting the sound ready…") : Lang.F("The sound is still on its way: {0}%", (int)(p * 100));
				return;
			}
		}
		var playing = PlayClock.Running(room);
		var at = (game.Question.StartSec ?? 0) + PlayClock.Seconds(room);
		var now = Time.GetTicksMsec() / 1000.0;
		if (playing && !_audio.Playing)
		{
			_audio.Play((float)at);
			_seekedAt = now;
		}
		_audio.StreamPaused = !playing;
		// Back in step if it drifted (not too often).
		if (_audio.Playing && Math.Abs(_audio.GetPlaybackPosition() - at) > 0.5 && now - _seekedAt > 2)
		{
			_audio.Seek((float)at);
			_seekedAt = now;
		}
		_bars.Active = playing;
		_status.Text = game.MediaStarted ? "" : Lang.T("The host starts the sound in a moment…");
	}

	public override void _ExitTree() => _audio.Stop();
}

/// <summary>An image reveal's picture, getting clearer in step with the host's.</summary>
public partial class PlayerImageReveal : VBoxContainer
{
	readonly AssetSource _assets;
	readonly Func<RoomState> _room;
	readonly string _key;
	readonly ImageRevealView _view = new() { MaxHeight = U.Flexible };
	bool _loaded;

	public PlayerImageReveal() : this(null!, () => new RoomState(), "") { }

	public PlayerImageReveal(AssetSource assets, Func<RoomState> room, string key)
	{
		_assets = assets;
		_room = room;
		_key = key;
		this.MarkFlexible();
		AddChild(U.Panel(_view, "Clear").With(p =>
		{
			p.AddThemeStyleboxOverride("panel", UiTheme.Box(C.Bg, 12, C.Border, 1, 0, 0));
			p.SizeFlagsVertical = SizeFlags.ExpandFill;
		}));
	}

	public override void _Process(double delta)
	{
		var room = _room();
		if (room.Game is not { } game) return;
		var q = game.Question;
		if (!_loaded && _assets.Get(_key) is { } tex)
		{
			_view.Set(tex, q.RevealStyle ?? RevealStyle.Zoom, q.Focus ?? new NormPoint(0.5, 0.5));
			_loaded = true;
		}
		var duration = Math.Max(1, q.RevealSec ?? 30);
		_view.Progress = game.Stage == Stage.Open ? (float)Math.Min(1, PlayClock.Seconds(room) / duration) : 1;
	}
}
