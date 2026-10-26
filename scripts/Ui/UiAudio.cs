// The game's own sounds: music on the menu screens and in the lobby (it fades out when a quiz starts), and a sound when
// the mouse goes over a button and when one is clicked. All from files in the assets folder, picked up by name; a file
// that isn't there is simply silent:
//   assets/music/: every song in there plays, shuffled, in the menus, the lobby and during quizzes (where it pauses
//     while a sound, video or interactive question is open, so it never covers the clip or the microphone). Songs keep
//     their Epidemic Sound names, "ES_Title - Artist.ogg": the "Now playing" card reads title and artist from that.
//     IntroSong ("Easy Breezy") always comes first when the game opens, with the voice line.
//     assets/music/christmas/: songs that only join in at Christmas time (1 to 26 December).
//     Optional, for a place of its own: <place>.ogg (<place>2 … <place>9), the place being home, editor, statistics,
//     settings, lobby or game; that place then plays only those.
//   assets/soundeffects/ui/hover.ogg and click.ogg (or .mp3 / .wav); back.ogg for "←" links, if there is one
//   assets/soundeffects/voice/intro.ogg: a voice line once the home screen first shows (intro2 … intro9: one at random), the music after it;
// Music plays on the game music bus ("Music" in Settings), the button sounds on the SFX bus. When a new song
// starts, a "Now playing" card shows its title and artist (from assets/music/CREDITS.md) in the bottom left.

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

	static bool _introDone;

	/// <summary>The song that always opens the game (its title, as in the file name).</summary>
	const string IntroSong = "Easy Breezy";

	static readonly string[] Places = { "home", "editor", "statistics", "settings", "lobby", "game" };

	/// <summary>
	/// Every song in assets/music (in an exported game the folder lists each one's .import/.remap file; the song is the
	/// name without that).
	/// </summary>
	static List<string> AllSongs()
	{
		var songs = SongsIn("res://assets/music");
		if (IsChristmas(DateTime.Now)) songs.AddRange(SongsIn("res://assets/music/christmas"));
		return songs;
	}

	/// <summary>Christmas time: 1 to 26 December (the christmas folder's songs join the others then).</summary>
	public static bool IsChristmas(DateTime day) => day.Month == 12 && day.Day <= 26;

	static List<string> SongsIn(string dir)
	{
		if (!DirAccess.DirExistsAbsolute(dir)) return new();
		return DirAccess.GetFilesAt(dir).Select(f => f.EndsWith(".import") ? f[..^7] : f.EndsWith(".remap") ? f[..^6] : f)
			.Where(f => Exts.Any(e => f.EndsWith("." + e, StringComparison.OrdinalIgnoreCase))).Distinct()
			.Select(f => $"{dir}/{f}").Where(f => ResourceLoader.Exists(f)).ToList();
	}

	/// <summary>A place's own songs (lobby.ogg, lobby2 …), else every other song in the folder.</summary>
	static List<string> Tracks(string place)
	{
		var all = AllSongs();
		bool IsPlace(string path, string name) => System.IO.Path.GetFileNameWithoutExtension(path) is var n
			&& (n == name || n.Length == name.Length + 1 && n.StartsWith(name) && char.IsDigit(n[^1]));
		var own = all.Where(t => IsPlace(t, place)).ToList();
		var list = own.Count > 0 ? own : all.Where(t => !Places.Any(pl => IsPlace(t, pl))).ToList();
		var shuffled = list.OrderBy(_ => Random.Shared.Next()).ToList();
		// (the very first music of a run starts with the intro song)
		if (!_introDone && shuffled.FirstOrDefault(t => t.Contains(IntroSong, StringComparison.OrdinalIgnoreCase)) is { } intro)
		{
			_introDone = true;
			shuffled.Remove(intro);
			shuffled.Insert(0, intro);
		}
		return shuffled;
	}

	/// <summary>
	/// A song's title and artist: from CREDITS.md if it's listed there, else from its Epidemic Sound file name
	/// ("ES_Easy Breezy - Megan Wofford.ogg" → Easy Breezy, Megan Wofford).
	/// </summary>
	public static (string Title, string Artist) Describe(string path)
	{
		var file = System.IO.Path.GetFileName(path);
		if (Credits().TryGetValue(file, out var credit)) return credit;
		var name = System.IO.Path.GetFileNameWithoutExtension(file);
		if (name.StartsWith("ES_")) name = name[3..];
		var dash = name.LastIndexOf(" - ", StringComparison.Ordinal);
		return dash > 0 ? (name[..dash].Trim(), name[(dash + 3)..].Trim()) : (name, "");
	}

	/// <summary>Where a screen's music comes from (null: no music, e.g. during a quiz).</summary>
	static string? Place(Control screen) => screen switch
	{
		HomeScreen => "home",
		QuizListScreen or QuizOverviewScreen or QuestionEditorScreen => "editor",
		EternalTableScreen or MatchHistoryScreen or MatchNightScreen or ProfileScreen => "statistics",
		SettingsScreen => "settings",
		HostLobbyScreen or WaitingRoomScreen => "lobby",
		HostGameScreen or PlayerGameScreen => "game",
		_ => null,
	};

	readonly AudioStreamPlayer _music = new() { Bus = Prefs.GameMusicBus };
	readonly AudioStreamPlayer _hover = new() { Bus = Prefs.SfxBus, MaxPolyphony = 2 };
	readonly AudioStreamPlayer _click = new() { Bus = Prefs.SfxBus, MaxPolyphony = 3 };
	readonly AudioStreamPlayer _voice = new() { Bus = Prefs.SfxBus };
	bool _greeted;
	/// <summary>The voice line is on: the music waits for it to end, then starts from the top.</summary>
	bool _voiceFirst;
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
		// (the music starts once the voice line is over)
		_voice.Finished += () =>
		{
			_voiceFirst = false;
			_music.VolumeDb = 0;
			if (!_music.Playing) StartTrack();
		};
		// (every button, now and later, gets the hover and click sound)
		GetTree().NodeAdded += Hook;
	}

	public override void _ExitTree() => GetTree().NodeAdded -= Hook;

	/// <summary>Meta for a button that shouldn't grow under the mouse (list rows, cards in a grid).</summary>
	public const string NoBounce = "no_bounce";

	/// <summary>The party show bounce: buttons grow a little under the mouse and squash when clicked (links don't move).</summary>
	static void Bounce(BaseButton button, float to, double time)
	{
		if (button.ThemeTypeVariation == "LinkButton2" || button is Ui.Screens.BuzzButton or CheckBox || !button.IsVisibleInTree() || Announcer.ReduceMotion) return;
		// (big rows and cards keep still: grown, they'd poke out of their list and get cut off at its edge)
		if (button.HasMeta(NoBounce) || button.Size.X > 420) return;
		button.PivotOffset = button.Size / 2;
		var t = button.CreateTween();
		t.TweenProperty(button, "scale", new Vector2(to, to), time).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
	}

	void Hook(Node node)
	{
		if (node is not BaseButton button) return;
		button.MouseEntered += () =>
		{
			if (!button.Disabled) Bounce(button, 1.04f, 0.15);
		};
		button.MouseExited += () => Bounce(button, 1f, 0.15);
		button.ButtonDown += () => Bounce(button, 0.94f, 0.06);
		button.ButtonUp += () => Bounce(button, button.IsHovered() ? 1.04f : 1f, 0.25);
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
		// (music switched off in the jukebox: the song is lined up, and starts when it's switched on again; during the
		// voice line it starts when that's over)
		if (!Settings.MusicOn || _voiceFirst) return;
		_music.Play();
		TrackStarted?.Invoke(_tracks[_track]);
	}

	// ---------- the jukebox (Ui/Jukebox) ----------

	/// <summary>The songs this screen's place plays, and the one playing (or lined up) now.</summary>
	public IReadOnlyList<string> Songs => _tracks;
	public string? CurrentSong => _tracks.Count > 0 ? _tracks[_track] : null;

	/// <summary>Music on or off (saved). Off fades it out; on carries on with the same song.</summary>
	public void SetMusicOn(bool on)
	{
		Settings.MusicOn = on;
		if (on && !_music.Playing && _tracks.Count > 0)
		{
			_music.VolumeDb = -30;
			StartTrack();
		}
		Apply();
	}

	/// <summary>The next or previous song in this place's list (switching the music on if it was off).</summary>
	public void Skip(int by)
	{
		if (_tracks.Count == 0) return;
		PlaySong((_track + by + _tracks.Count) % _tracks.Count);
	}

	public void PlaySong(int index)
	{
		if (index < 0 || index >= _tracks.Count) return;
		_fade?.Kill();
		_track = index;
		Settings.MusicOn = true;
		_music.StreamPaused = false;
		_music.VolumeDb = 0;
		_quiet = false;
		StartTrack();
		// (fades straight out again if a question or the podium sound wants quiet now)
		Apply();
	}

	/// <summary>A song started (its res:// path): App shows the "Now playing" card.</summary>
	public event Action<string>? TrackStarted;

	/// <summary>
	/// Title and artist per music file name ("home.ogg"), from the tables in assets/music/CREDITS.md: rows like
	/// "| `home.ogg` | Title | Artist | link | where |". Empty rows and the header are skipped.
	/// </summary>
	public static Dictionary<string, (string Title, string Artist)> Credits()
	{
		var credits = new Dictionary<string, (string, string)>(StringComparer.OrdinalIgnoreCase);
		const string path = "res://assets/music/CREDITS.md";
		if (!Godot.FileAccess.FileExists(path)) return credits;
		foreach (var line in Godot.FileAccess.GetFileAsString(path).Split('\n'))
		{
			var cells = line.Trim().Trim('|').Split('|').Select(c => c.Trim().Trim('`')).ToArray();
			if (cells.Length < 3 || cells[0] == "" || cells[0] == "File" || cells[0].StartsWith("-") || cells[1] == "") continue;
			// (music rows are plain file names; "ui/click.ogg" or "voice/intro.ogg" are sound effects, not songs)
			if (!cells[0].Contains('/')) credits[cells[0]] = (cells[1], cells[2]);
		}
		return credits;
	}

	bool _quiet;
	bool _questionQuiet;

	/// <summary>
	/// During a quiz: a question that plays its own sound (sound, video) or records the microphone (interactive) wants
	/// quiet. The music fades and pauses, and carries on from there once that question is over.
	/// </summary>
	public void Quiet(bool quiet)
	{
		_questionQuiet = quiet;
		Apply();
	}

	// (a ▶ preview of a buzzer or podium sound, or the podium sound at the end, also wants quiet while it plays)
	public override void _Process(double delta) => Apply();

	void Apply()
	{
		var quiet = !Settings.MusicOn || _questionQuiet || Storage.Sfx.IsPlaying(true) || (Storage.Sfx.Previewing && Storage.Sfx.IsPlaying(false));
		if (quiet == _quiet) return;
		_quiet = quiet;
		if (_music.Stream == null) return;
		_fade?.Kill();
		_fade = CreateTween();
		if (quiet)
		{
			_fade.TweenProperty(_music, "volume_db", -40f, 0.5);
			_fade.TweenCallback(Callable.From(() => _music.StreamPaused = true));
		}
		else
		{
			_music.StreamPaused = false;
			_fade.TweenProperty(_music, "volume_db", 0f, 1.0);
		}
	}

	/// <summary>The voice line for opening the game, once per run (on the first home screen, after the splash).</summary>
	public void Greet()
	{
		if (_greeted) return;
		_greeted = true;
		var lines = Enumerable.Range(1, 9).Select(i => Find($"res://assets/soundeffects/voice/intro{(i == 1 ? "" : i.ToString())}")).OfType<string>().ToList();
		if (lines.Count == 0 || GD.Load<AudioStream>(lines[Random.Shared.Next(lines.Count)]) is not { } line) return;
		// (the music stops for it and starts from the top of the song once the voice line is over)
		_fade?.Kill();
		_voiceFirst = true;
		_music.Stop();
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
		// (a new screen starts out loud: only a quiz question asks for quiet)
		_questionQuiet = false;
		if (_quiet && Settings.MusicOn)
		{
			_quiet = false;
			_music.StreamPaused = false;
			if (tracks.ToHashSet().SetEquals(_tracks)) _music.VolumeDb = 0;
		}
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
