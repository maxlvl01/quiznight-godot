using System;
using Godot;
using QuizNight.Net;
using QuizNight.Shared;
using QuizNight.Storage;

namespace QuizNight.Ui.Screens;

/// <summary>
/// A video question's video on a player's screen, in step with the host's: it plays, pauses and jumps
/// with the host's video (the play clock in the state). Blurred until the host starts it for the first
/// time. With sound (players can turn it off).
/// </summary>
public partial class PlayerVideo : VBoxContainer
{
	static readonly Shader Blur = new()
	{
		Code = """
			shader_type canvas_item;
			uniform float amount = 1.0;
			void fragment() {
				if (amount <= 0.001) {
					COLOR = texture(TEXTURE, UV);
				} else {
					// A wide box blur of the video, a little darker.
					vec3 sum = vec3(0.0);
					for (int x = -4; x <= 4; x++)
						for (int y = -4; y <= 4; y++)
							sum += texture(TEXTURE, UV + vec2(float(x), float(y)) * TEXTURE_PIXEL_SIZE * 7.0 * amount).rgb;
					COLOR = vec4(sum / 81.0 * mix(1.0, 0.7, amount), 1.0);
				}
			}
			""",
	};

	/// <summary>The player's choice for sound and video, for the rest of this session.</summary>
	public static bool SoundOn = true;

	readonly ClientSession _client;
	readonly Func<RoomState> _room;
	readonly string _id;
	readonly string _key;
	readonly VideoStreamPlayer _video = new() { Bus = Prefs.MusicBus };
	readonly ShaderMaterial _blur = new() { Shader = Blur };
	readonly Label _status = U.Muted("", 15, false, HorizontalAlignment.Center);
	readonly Button _sound;
	float _blurAmount = 1;
	double _seekedAt = -10;

	public PlayerVideo() : this(null!, () => new RoomState(), "", "") { }

	public PlayerVideo(ClientSession client, Func<RoomState> room, string id, string key)
	{
		_client = client;
		_room = room;
		_id = id;
		_key = key;
		AddThemeConstantOverride("separation", 8);
		_video.Material = _blur;
		var frame = new VideoFrame(_video) { CustomMinimumSize = new Vector2(0, 160) }.MarkFlexible();
		var stage = new Control { CustomMinimumSize = new Vector2(0, 160) }.MarkFlexible();
		stage.AddChild(frame.With(f => f.SetAnchorsPreset(LayoutPreset.FullRect)));
		stage.AddChild(_status.With(l =>
		{
			l.SetAnchorsPreset(LayoutPreset.FullRect);
			l.VerticalAlignment = VerticalAlignment.Center;
		}));
		AddChild(stage);
		_sound = U.Small("", () =>
		{
			SoundOn = !SoundOn;
			ApplySound();
		});
		AddChild(GameParts.Row(10, _sound, U.Muted("The video plays along with the host's.", 13)));
		ApplySound();
	}

	void ApplySound()
	{
		_video.VolumeDb = SoundOn ? 0 : -80;
		_sound.Text = SoundOn ? Lang.T("Sound: on") : Lang.T("Sound: off");
	}

	public override void _Process(double delta)
	{
		var room = _room();
		if (room.Game is not { } game) return;
		if (_video.Stream == null)
		{
			if (_client.Media.GetVideo(_id, _key) is { } stream)
			{
				_video.Stream = stream;
				_status.Text = "";
			}
			else
			{
				var p = _client.Media.Progress(_id);
				_status.Text = p >= 1 ? Lang.T("Getting the video ready…") : Lang.F("The video is still on its way: {0}%", (int)(p * 100));
				return;
			}
		}

		// Where the host's video is now: its start point plus how long it has played.
		var playing = game.MediaPlaying && game.Stage == Stage.Open;
		var sinceState = playing ? Math.Max(0, ServerClock.Now() - room.ServerNow) : 0;
		var at = (game.Question.StartSec ?? 0) + (game.MediaPlayedMs + sinceState) / 1000.0;
		var now = Time.GetTicksMsec() / 1000.0;
		if (!_video.IsPlaying())
		{
			// (playing and pausing straight away shows a frame, blurred until the host starts)
			_video.Play();
			_video.Paused = !playing;
			if (at > 0.2) _video.StreamPosition = at;
			_seekedAt = now;
		}
		_video.Paused = !playing;
		// Back in step if it drifted (not too often: a jump takes a moment itself).
		if (Math.Abs(_video.StreamPosition - at) > 0.75 && now - _seekedAt > 2)
		{
			_video.StreamPosition = at;
			_seekedAt = now;
		}

		_blurAmount = game.MediaStarted ? Math.Max(0, _blurAmount - (float)delta * 1.5f) : 1;
		_blur.SetShaderParameter("amount", _blurAmount);
		_status.Text = game.MediaStarted ? "" : Lang.T("The host starts the video in a moment…");
	}

	public override void _ExitTree() => _video.Stop();
}
