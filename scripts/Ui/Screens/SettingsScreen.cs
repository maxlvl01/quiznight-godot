using System.Linq;
using Godot;
using QuizNight.Shared;
using QuizNight.Storage;

namespace QuizNight.Ui.Screens;

/// <summary>Audio (master, music, sound effects), video (window mode, size) and language.</summary>
public partial class SettingsScreen : Control
{
	public SettingsScreen()
	{
		SetAnchorsPreset(LayoutPreset.FullRect);

		var audio = U.Card(14,
			U.Eyebrow("Audio"),
			Volume("Master volume", "Everything Quiznight plays.", Settings.MasterVolume, v => Settings.MasterVolume = v),
			Volume("Music", "Songs and clips in sound and video questions.", Settings.MusicVolume, v => Settings.MusicVolume = v),
			Volume("Game music", "Menu music and the podium jingle.", Settings.GameMusicVolume, v => Settings.GameMusicVolume = v),
			Volume("Sound effects", "Buzzer and other sound effects.", Settings.SfxVolume, v => Settings.SfxVolume = v),
			new HSeparator(),
			BuzzerSection()).Grow();

		var sizes = Prefs.WindowSizes.Select(s => ($"{s.X}x{s.Y}", $"{s.X} × {s.Y}")).ToArray();
		var current = Settings.WindowSize;
		var size = U.Select(sizes, $"{current.X}x{current.Y}", id =>
		{
			var p = id.Split('x');
			Settings.WindowSize = new Vector2I(int.Parse(p[0]), int.Parse(p[1]));
			Prefs.ApplyWindow();
		});
		var sizeHint = U.Muted("", 12);
		void UpdateSize()
		{
			var windowed = Settings.WindowMode == "windowed";
			size.Disabled = !windowed;
			sizeHint.Text = windowed ? "" : Lang.T("Fullscreen always uses your monitor's resolution.");
			sizeHint.Visible = !windowed;
		}
		UpdateSize();
		var mode = U.Segmented(new[] { ("windowed", "Windowed"), ("fullscreen", "Fullscreen"), ("borderless", "Borderless") }, Settings.WindowMode, id =>
		{
			Settings.WindowMode = id;
			Prefs.ApplyWindow();
			UpdateSize();
		});
		var video = U.Card(12,
			U.Eyebrow("Video"),
			U.Field("Window mode", mode),
			U.Field("Window size", size),
			sizeHint,
			U.Muted("Frame rate is capped at 60 FPS.", 12));

		var language = U.Card(12,
			U.Eyebrow("Language"),
			U.Segmented(Lang.All, Settings.Language, code =>
			{
				Settings.Language = code;
				Lang.Use(code);
				// Rebuild so text made from parts (Lang.F) switches too.
				Callable.From(App.I.GoSettings).CallDeferred();
			}),
			U.Muted("Changes all menus and game screens. Quiz questions stay in the language they were written in.", 12));

		var page = U.V(14,
			U.Link("← Home", () => App.I.GoHome()).With(b => b.SizeFlagsHorizontal = SizeFlags.ShrinkBegin),
			U.Heading("Settings", 34),
			U.H(14, audio, U.V(14, video, language).Grow()));
		AddChild(U.Page(page, 980));
	}

	Control BuzzerSection()
	{
		var error = U.Error();
		var own = U.Small("Use my own sound…", () => Dialogs.OpenFile(this, "Pick a sound file", Sfx.FileFilters, path =>
		{
			try
			{
				Sfx.SetCustom(path);
				Settings.BuzzerSound = Sfx.Custom;
				Sfx.PlayBuzzer(Sfx.Custom);
				App.I.GoSettings();
			}
			catch (UserError e)
			{
				error.Text = e.Message;
				error.Visible = true;
			}
		}));
		return U.V(6,
			U.Text("Buzzer sound", 15),
			BuzzerPicker.Make(Settings.BuzzerSound, id => Settings.BuzzerSound = id, true),
			U.Muted("Plays on the host's screen when someone buzzes in. This is the default for new lobbies; the host can still change it in the lobby for one night.", 12),
			U.H(10, own, U.Muted(Lang.F("MP3, OGG or WAV, up to {0} MB.", Sfx.CustomMaxMb), 12, false).With(l => l.SizeFlagsVertical = SizeFlags.ShrinkCenter)),
			error);
	}

	static Control Volume(string label, string hint, int value, System.Action<int> save)
	{
		var shown = U.Mono($"{value}%", 15, C.Muted);
		var slider = new HSlider { MinValue = 0, MaxValue = 100, Step = 1, Value = value, MouseDefaultCursorShape = CursorShape.PointingHand };
		slider.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		slider.ValueChanged += v =>
		{
			shown.Text = $"{(int)v}%";
			save((int)v);
			Prefs.ApplyAudio();
		};
		return U.V(4, U.H(8, U.Text(label, 15).Grow(), shown), slider, U.Muted(hint, 12));
	}
}
