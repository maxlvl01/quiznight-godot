// The game's own sounds: music on the menu screens and in the lobby (it fades out when a quiz starts), and a sound when
// the mouse goes over a button and when one is clicked. All from files in the assets folder, picked up by name; a file
// that isn't there is simply silent:
//   assets/music/<place>.ogg (or .mp3 / .wav), the place being home, editor (my quizzes, a quiz, the question editor),
//     statistics (Ewige Tabelle, match history, profiles), settings or lobby (host lobby, waiting room); a place without
//     its own file plays menu.ogg. More tracks for a place: <place>2 … <place>9 (shuffled, one after another).
//   assets/soundeffects/ui/hover.ogg and click.ogg (or .mp3 / .wav); back.ogg for "←" links, if there is one
//   assets/soundeffects/voice/intro.ogg: a voice line once the home screen first shows (intro2 … intro9: one at random);
//     the music ducks under it
// Music plays on the game music bus ("Game music" in Settings), the button sounds on the SFX bus.

using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using QuizNight.Storage;
using QuizNight.Ui.Screens;

namespace QuizNight.Ui;

public partial class UiAudio : Node
{
	static readonly string[] Exts = { "ogg", "mp3", "wav" };

	static string? Find(string pathWithoutExt) =>
		Exts.Select(e => $"{pathWithoutExt}.{e}").FirstOrDefault(p => ResourceLoader.Exists(p));

	/// <summary>A place's tracks (lobby, lobby2 … lobby9), or menu's when it has none of its own.</summary>
	static List<string> Tracks(string place)
	{
		List<string> Of(string name) =>
			Enumerable.Range(1, 9).Select(i => Find($"res://assets/music/{name}{(i == 1 ? "" : i.ToString())}")).OfType<string>().ToList();
		var own = Of(place);
		var list = own.Count > 0 ? own : Of("menu");
		return list.OrderBy(_ => Random.Shared.Next()).ToList();
	}

	/// <summary>Where a screen's music comes from (null: no music, e.g. during a quiz).</summary>
	static string? Place(Control screen) => screen switch
	{
		HomeScreen => "home",
		QuizListScreen or QuizOverviewScreen or QuestionEditorScreen => "editor",
		EternalTableScreen or MatchHistoryScreen or MatchNightScreen or ProfileScreen => "statistics",
		SettingsScreen => "settings",
		HostLobbyScreen or WaitingRoomScreen => "lobby",
		_ => null,
	};

	readonly AudioStreamPlayer _music = new() { Bus = Prefs.GameMusicBus };
	readonly AudioStreamPlayer _hover = new() { Bus = Prefs.SfxBus, MaxPolyphony = 2 };
	readonly AudioStreamPlayer _click = new() { Bus = Prefs.SfxBus, MaxPolyphony = 3 };
	readonly AudioStreamPlayer _voice = new() { Bus = Prefs.SfxBus };
	bool _greeted;
	readonly AudioStream? _clickSound, _backSound;
	List<string> _tracks = new();
	int _track;
	Tween? _fade;

	public UiAudio()
	{
		if (Find("res://assets/soundeffects/ui/hover") is { } hover) _hover.Stream = GD.Load<AudioStream>(hover);
		if (Find("res://assets/soundeffects/ui/click") is { } click) _clickSound = GD.Load<AudioStream>(click);
		if (Find("res://assets/soundeffects/ui/back") is { } back) _backSound = GD.Load<AudioStream>(back);
		_music.Finished += () =>
		{
			// (one track after another; a single one starts over)
			if (_tracks.Count == 0) return;
			_track = (_track + 1) % _tracks.Count;
			StartTrack();
		};
	}

	public override void _Ready()
	{
		AddChild(_music);
		AddChild(_hover);
		AddChild(_click);
		AddChild(_voice);
		// (the music comes back up once the voice line is over)
		_voice.Finished += () => _music.VolumeDb = 0;
		// (every button, now and later, gets the hover and click sound)
		GetTree().NodeAdded += Hook;
	}

	public override void _ExitTree() => GetTree().NodeAdded -= Hook;

	void Hook(Node node)
	{
		if (node is not BaseButton button) return;
		button.MouseEntered += () =>
		{
			if (_hover.Stream != null && !button.Disabled && button.IsVisibleInTree()) _hover.Play();
		};
		button.Pressed += () =>
		{
			var sound = _backSound != null && button is Button { Text: var t } && t.StartsWith("←") ? _backSound : _clickSound;
			if (sound == null) return;
			_click.Stream = sound;
			_click.Play();
		};
	}

	void StartTrack()
	{
		if (_tracks.Count == 0 || GD.Load<AudioStream>(_tracks[_track]) is not { } stream) return;
		_music.Stream = stream;
		_music.Play();
	}

	/// <summary>The voice line for opening the game, once per run (on the first home screen, after the splash).</summary>
	public void Greet()
	{
		if (_greeted) return;
		_greeted = true;
		var lines = Enumerable.Range(1, 9).Select(i => Find($"res://assets/soundeffects/voice/intro{(i == 1 ? "" : i.ToString())}")).OfType<string>().ToList();
		if (lines.Count == 0 || GD.Load<AudioStream>(lines[Random.Shared.Next(lines.Count)]) is not { } line) return;
		_fade?.Kill();
		_music.VolumeDb = -12;
		_voice.Stream = line;
		_voice.Play();
	}

	/// <summary>
	/// Which screen shows now: menus and the lobby have music, a running quiz (and its podium) not. The same music
	/// carries on from screen to screen; another place's music fades over.
	/// </summary>
	public void ScreenChanged(Control screen)
	{
		var tracks = Place(screen) is { } place ? Tracks(place) : new();
		// (same set of tracks as now, playing: nothing to do)
		if (_music.Playing && tracks.Count > 0 && tracks.ToHashSet().SetEquals(_tracks)) return;
		// (nothing playing and nothing to play: an empty tween would complain)
		if (!_music.Playing && tracks.Count == 0) return;
		_fade?.Kill();
		_fade = CreateTween();
		if (_music.Playing)
		{
			_fade.TweenProperty(_music, "volume_db", -40f, tracks.Count > 0 ? 0.6 : 1.0);
			_fade.TweenCallback(Callable.From(_music.Stop));
		}
		_tracks = tracks;
		_track = 0;
		if (tracks.Count == 0) return;
		_fade.TweenCallback(Callable.From(() =>
		{
			_music.VolumeDb = -30;
			StartTrack();
		}));
		_fade.TweenProperty(_music, "volume_db", 0f, 1.2);
	}
}
