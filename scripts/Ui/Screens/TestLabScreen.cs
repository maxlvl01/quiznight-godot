// The Test lab (developer mode only: test builds, the editor, or after typing the developer code on the home screen):
// quick checks without hosting a whole night or typing command-line flags. Buzzers with a reaction test, every song and
// sound side by side, the microphone, each question type's editor, a LAN game with bots (also one on an older version,
// for the lobby's version check), the map switches, and screens that only show once (welcome, intro, What's new, the
// yellow new dots).

using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using QuizNight.Net;
using QuizNight.Shared;
using QuizNight.Storage;

namespace QuizNight.Ui.Screens;

public partial class TestLabScreen : Control
{
	readonly AudioStreamPlayer _player = new() { Bus = Prefs.GameMusicBus };
	MicRecorder? _mic;
	AudioStreamWav? _recording;

	public TestLabScreen()
	{
		SetAnchorsPreset(LayoutPreset.FullRect);
		AddChild(_player);
		var left = U.V(14, Buzzers(), Microphone(), Questions());
		var right = U.V(14, Sounds(), Maps(), OneOffs());
		var page = U.V(14,
			U.H(16, U.Link("← Home", () => App.I.GoHome()).With(b => b.SizeFlagsVertical = SizeFlags.ShrinkCenter), U.Heading("Test lab", 34)),
			U.Muted("Developer mode only. Nothing here changes your quizzes or settings, except where it says so.", 13),
			U.H(14, left.Grow(), right.Grow()));
		AddChild(U.Page(page, 1100, 20));
	}

	public override void _ExitTree()
	{
		_player.Stop();
		_mic?.Stop();
	}

	// ---------- buzzers ----------

	/// <summary>Every buzzer sound, and a reaction test: wait for the button to turn on, then buzz as fast as you can.</summary>
	Control Buzzers()
	{
		var result = U.Text("Press Start, wait for the buzzer to unlock, then buzz.", 14, C.Muted, null, true);
		ulong armedAt = 0;
		BuzzButton? button = null;
		button = new BuzzButton(() =>
		{
			if (armedAt == 0) return;
			var ms = Time.GetTicksMsec() - armedAt;
			armedAt = 0;
			button!.Disabled = true;
			Sfx.PlayBuzzer(Settings.BuzzerSound);
			result.Text = Lang.F("Your reaction: {0} ms", ms);
		}, 120) { Disabled = true };
		var start = U.Small("Start", () =>
		{
			button.Disabled = true;
			armedAt = 0;
			result.Text = Lang.T("Wait for it …");
			GetTree().CreateTimer(1.5 + GD.Randf() * 2.5).Timeout += () =>
			{
				if (!IsInstanceValid(button)) return;
				button.Disabled = false;
				armedAt = Time.GetTicksMsec();
			};
		});
		var sounds = U.H(6);
		foreach (var (id, label) in Sfx.BuzzerChoices().Where(c => c.Id != Sfx.Off))
			sounds.AddChild(U.Small("▶ " + Lang.T(label), () => Sfx.PlayBuzzer(id)));
		sounds.AddChild(U.Small("▶ Right", () => Sfx.PlayAnswer(true)));
		sounds.AddChild(U.Small("▶ Wrong", () => Sfx.PlayAnswer(false)));
		return U.Card(10, U.Eyebrow("Buzzers"), U.H(14, button, U.V(8, start, result).Grow()), sounds);
	}

	// ---------- songs and sounds ----------

	/// <summary>Every song (the seasonal ones too), the podium jingles and the voice line, to hear them side by side.</summary>
	Control Sounds()
	{
		var list = U.V(4);
		var songs = UiAudio.SongsIn("res://assets/music").Concat(UiAudio.SongsIn("res://assets/music/christmas"))
			.Concat(UiAudio.SongsIn("res://assets/music/halloween"));
		foreach (var path in songs)
		{
			var (title, artist) = UiAudio.Describe(path);
			list.AddChild(Row(title + " · " + artist, path));
		}
		foreach (var podium in Sfx.Podiums) list.AddChild(Row(Lang.T(podium.Label) + " (podium)", podium.Path));
		if (ResourceLoader.Exists("res://assets/soundeffects/voice/intro.ogg")) list.AddChild(Row("Voice line", "res://assets/soundeffects/voice/intro.ogg"));
		var stop = U.Small("■ Stop", () => _player.Stop());
		return U.Card(10, U.H(8, U.Eyebrow("Songs and sounds").Grow(), stop),
			U.Muted("All levelled to the same loudness (tools/normalise_audio.py). Each plays at your Music volume.", 12), list);

		Control Row(string name, string path) => U.H(8, U.Small("▶", () =>
		{
			if (GD.Load<AudioStream>(path) is not { } stream) return;
			_player.Stream = stream;
			_player.Play();
		}), U.Text(name, 14, C.Text).Raw().Grow());
	}

	// ---------- microphone ----------

	/// <summary>Record a few seconds like an Interactive question, and listen back.</summary>
	Control Microphone()
	{
		var status = U.Muted("Checks your microphone works (like an Interactive question).", 13);
		var play = U.Small("▶ Listen back", () =>
		{
			if (_recording == null) return;
			_player.Stream = _recording;
			_player.Play();
		}).With(b => b.Disabled = true);
		var record = U.Small("● Record 4 seconds", null);
		record.Pressed += () =>
		{
			_mic ??= new MicRecorder();
			if (_mic.GetParent() == null) AddChild(_mic);
			_mic.Start();
			record.Disabled = true;
			status.Text = Lang.T("Recording … say something.");
			GetTree().CreateTimer(4).Timeout += () =>
			{
				if (!IsInstanceValid(this)) return;
				var wav = _mic.Stop();
				record.Disabled = false;
				_recording = wav is { Length: > 44 } ? AudioStreamWav.LoadFromBuffer(wav) : null;
				play.Disabled = _recording == null;
				status.Text = _recording != null ? Lang.T("Got it. Listen back to check.") : Lang.T("Nothing recorded: is a microphone connected and allowed?");
			};
		};
		return U.Card(10, U.Eyebrow("Microphone"), status, U.H(8, record, play));
	}

	// ---------- question screens and games ----------

	/// <summary>Each question type's editor on the sample quiz, and a LAN game with bots.</summary>
	Control Questions()
	{
		var types = new GridContainer { Columns = 3 };
		types.AddThemeConstantOverride("h_separation", 6);
		types.AddThemeConstantOverride("v_separation", 6);
		foreach (var (type, label) in new[]
		{
			("classic", "Classic"), ("choice", "A B C D"), ("multi", "Multiple choice"), ("picture", "Picture"),
			("image", "Picture · reveal"), ("estimation", "Estimation"), ("mapPin", "Map pin"), ("wow", "WoW Forever map"),
			("sound", "Sound"), ("video", "Video"), ("order", "Put in order"), ("timeline", "Timeline"),
			("steps", "Step by step"), ("interactive", "Interactive"),
		})
			types.AddChild(U.Small(label, () => OpenSample(type)));
		return U.Card(10, U.Eyebrow("Question screens"),
			U.Muted("Opens the sample quiz's question in the editor (the sample quiz is saved to your quizzes).", 12), types,
			U.Gap(4),
			U.Eyebrow("A game with bots"),
			U.Muted("Hosts the sample quiz over LAN; bots join from 127.0.0.1 in the background and answer by themselves.", 12),
			U.H(8,
				U.Small("Host with 2 bots", () => HostWithBots(false)),
				U.Small("… one on an old version", () => HostWithBots(true)).Tip("The second bot says it's 0.11.4b: the lobby's version check shows it")));
	}

	static void OpenSample(string type)
	{
		var quiz = QuizStore.Save(Dev.SampleQuiz.Build());
		var q = quiz.Questions.FirstOrDefault(x => x.Type == type || (type == "choice" && x.Mode == ClassicMode.Choice) || (type == "multi" && x.IsMulti)
			|| (type == "timeline" && x.IsTimeline) || (type == "wow" && quiz.MapById(x.MapId)?.PresetId == WowAtlas.PresetId)) ?? quiz.Questions[0];
		App.I.GoQuestion(quiz.Id, q.Id, null);
	}

	void HostWithBots(bool oldOne)
	{
		var quiz = QuizStore.Save(Dev.SampleQuiz.Build());
		App.I.HostQuiz(quiz, NetMode.Lan);
		// (the room is open in a moment; bots keep trying to join for a while anyway)
		App.I.GetTree().CreateTimer(2).Timeout += () =>
		{
			StartBot("Bot Ann", null);
			StartBot("Bot Ben", oldOne ? "0.11.4b" : null);
		};
	}

	/// <summary>Another copy of the game as a bot player, without a window (`--bot`).</summary>
	static void StartBot(string name, string? pretendVersion)
	{
		var args = new List<string>();
		if (!OS.HasFeature("template")) args.AddRange(new[] { "--path", ProjectSettings.GlobalizePath("res://") });
		args.AddRange(new[] { "--headless", "--", "--no-steam", "--bot", name });
		if (pretendVersion != null) args.AddRange(new[] { "--pretend-version", pretendVersion });
		OS.CreateProcess(OS.GetExecutablePath(), args.ToArray());
	}

	// ---------- maps ----------

	Control Maps()
	{
		var changed = new CheckBox { Text = Lang.T("Pretend the WoW map changed (map pin questions on it need a check)"), ButtonPressed = MapPresets.PretendChanged != null };
		changed.Toggled += on => MapPresets.PretendChanged = on ? WowAtlas.PresetId : null;
		var isNew = new CheckBox { Text = Lang.T("Pretend the WoW map is new (NEW in the map grid)"), ButtonPressed = MapPresets.PretendNew != null };
		isNew.Toggled += on => MapPresets.PretendNew = on ? WowAtlas.PresetId : null;
		return U.Card(10, U.Eyebrow("Maps"), changed, isNew,
			U.Muted("Until the game closes. Then open a map pin question (Question screens) to see it.", 12));
	}

	// ---------- screens that only show once ----------

	Control OneOffs() => U.Card(10, U.Eyebrow("Screens that only show once"),
		U.H(8,
			U.Small("Welcome (first start)", App.I.ShowWelcomeAgain),
			U.Small("Intro", () => App.I.AddChild(new IntroSequence(null))),
			U.Small("What's new", () => WhatsNew.ShowCurrent(App.I))),
		U.H(8,
			U.Small("Yellow new dots again", () =>
			{
				Settings.NewSince = VersionCheck.BeforeVersions;
				App.I.GoHome();
			}).Tip("As if you'd just updated from 0.11.4b (changes a setting)")),
		U.Muted("The intro follows Settings → Intro; Escape skips it.", 12));
}
