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
		var left = U.V(14, Buzzers(), Microphone(), Questions(), DropDownTests());
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

	/// <summary>
	/// The buzzer, free to press: it goes down like in a game and plays your buzzer sound (Settings). "Reaction test"
	/// locks it until a random moment, then times how fast you buzz.
	/// </summary>
	Control Buzzers()
	{
		var result = U.Text(Lang.T("Press it: your buzzer sound from Settings."), 14, C.Muted, null, true);
		ulong armedAt = 0;
		var testing = false;
		BuzzButton? button = null;
		button = new BuzzButton(() =>
		{
			Sfx.PlayBuzzer(Settings.BuzzerSound);
			if (!testing || armedAt == 0) return;
			var ms = Time.GetTicksMsec() - armedAt;
			armedAt = 0;
			testing = false;
			result.Text = Lang.F("Your reaction: {0} ms", ms);
		}, 120) { Repeatable = true };
		var start = U.Small("Reaction test", () =>
		{
			testing = true;
			armedAt = 0;
			button.Disabled = true;
			result.Text = Lang.T("Wait for it …");
			GetTree().CreateTimer(1.5 + GD.Randf() * 2.5).Timeout += () =>
			{
				if (!IsInstanceValid(button)) return;
				button.Disabled = false;
				armedAt = Time.GetTicksMsec();
			};
		}).Tip("The buzzer locks until a random moment; then buzz as fast as you can");
		var sounds = U.H(6);
		foreach (var (id, label) in Sfx.BuzzerChoices().Where(c => c.Id != Sfx.Off))
			sounds.AddChild(U.Small("▶ " + Lang.T(label), () => Sfx.PlayBuzzer(id)));
		sounds.AddChild(U.Small("▶ Right", () => Sfx.PlayAnswer(true)));
		sounds.AddChild(U.Small("▶ Wrong", () => Sfx.PlayAnswer(false)));
		var closeUp = U.Small("🔍 Up close", ShowBuzzerUpClose).Tip("The buzzer big, to look at it in detail");
		return U.Card(10, U.Eyebrow("Buzzers"), U.H(14, button, U.V(8, start, result, closeUp).Grow()), sounds);
	}

	/// <summary>The buzzer as big as the window allows, with a size slider, still pressable (and playing its sound).</summary>
	static void ShowBuzzerUpClose()
	{
		var overlay = new VideoGuide.GuideOverlay();
		var holder = new CenterContainer();
		var size = new HSlider { MinValue = 200, MaxValue = 520, Step = 10, Value = 460, CustomMinimumSize = new Vector2(320, 0) };
		void Make()
		{
			foreach (var child in holder.GetChildren()) child.QueueFree();
			holder.AddChild(new BuzzButton(() => Sfx.PlayBuzzer(Settings.BuzzerSound), (float)size.Value) { Repeatable = true });
		}
		size.ValueChanged += _ => Make();
		Make();
		overlay.SetContent(U.Pad(U.V(10,
			U.Eyebrow("The buzzer up close"),
			holder,
			U.H(10, U.Muted("Size", 13), size, U.Small("Close", overlay.QueueFree))), 20, 16, 20, 16));
		App.I.AddChild(overlay);
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

	/// <summary>
	/// Record up to 10 seconds like an Interactive question (Stop ends it sooner) and listen back. The microphone is only
	/// open while recording.
	/// </summary>
	Control Microphone()
	{
		const double MaxSec = 10;
		var status = U.Muted(Lang.F("Checks your microphone works (like an Interactive question). Microphone: {0}.", Settings.Microphone == "Default" ? Lang.T("Windows' default") : Settings.Microphone), 13);
		var play = U.Small("▶ Listen back", () =>
		{
			if (_recording == null) return;
			_player.Stream = _recording;
			_player.Play();
		}).With(b => b.Disabled = true);
		var record = U.Small("● Record", null);
		var stop = U.Small("■ Stop", null).With(b => b.Disabled = true);
		void Finish()
		{
			if (_mic == null) return;
			var wav = _mic.Stop();
			// (closing the microphone: it's only on while recording)
			_mic.QueueFree();
			_mic = null;
			record.Disabled = false;
			stop.Disabled = true;
			_recording = wav is { Length: > 44 } ? AudioStreamWav.LoadFromBuffer(wav) : null;
			play.Disabled = _recording == null;
			status.Text = _recording != null ? Lang.T("Got it. Listen back to check.") : Lang.T("Nothing recorded: is a microphone connected, allowed and picked in Settings?");
		}
		record.Pressed += () =>
		{
			_mic = new MicRecorder();
			AddChild(_mic);
			_mic.Start();
			_mic.Full += Finish;
			record.Disabled = true;
			stop.Disabled = false;
			var startedAt = Time.GetTicksMsec();
			void Count()
			{
				if (_mic == null || !IsInstanceValid(this)) return;
				var left = MaxSec - (Time.GetTicksMsec() - startedAt) / 1000.0;
				if (left <= 0)
				{
					Finish();
					return;
				}
				status.Text = Lang.F("Recording … say something ({0} s left).", Math.Ceiling(left));
				GetTree().CreateTimer(0.25).Timeout += Count;
			}
			Count();
		};
		stop.Pressed += Finish;
		return U.Card(10, U.Eyebrow("Microphone"), status, U.H(8, record, stop, play));
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

	// ---------- drop-downs ----------

	/// <summary>Drop-downs of every length, to see them roll out and back in (a long one scrolls).</summary>
	static Control DropDownTests()
	{
		static OptionButton Make(int count, string what)
		{
			var box = new OptionButton();
			for (var i = 1; i <= count; i++) box.AddItem(Lang.F(what, i));
			return box;
		}
		return U.Card(10, U.Eyebrow("Drop-downs"),
			U.Muted("A short one, a medium one and a long one that scrolls.", 12),
			U.H(8, Make(3, "Choice {0}"), Make(9, "Item {0}"), Make(60, "Entry number {0}")));
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
			U.Small("Welcome (first start)", () => App.I.ShowWelcomeFrom(() => App.I.GoTestLab())),
			// (the intro ends by gliding its logo into the home screen's: it plays over the home screen)
			U.Small("Intro", () =>
			{
				App.I.GoHome();
				App.I.AddChild(new IntroSequence(null));
			}),
			U.Small("What's new", () => WhatsNew.ShowCurrent(App.I))),
		U.H(8,
			U.Small("Yellow new dots again", () =>
			{
				Settings.NewSince = VersionCheck.BeforeVersions;
				App.I.GoHome();
			}).Tip("As if you'd just updated from 0.11.4b (changes a setting)")),
		U.Muted("The intro follows Settings → Intro and ends on the home screen; Escape skips the cards. Welcome and What's new close with Escape or a right-click.", 12));
}
