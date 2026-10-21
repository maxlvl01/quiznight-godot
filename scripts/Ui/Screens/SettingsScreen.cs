using System.Linq;
using Godot;
using QuizNight.Shared;
using QuizNight.Storage;

namespace QuizNight.Ui.Screens;

/// <summary>
/// Audio (master, music, game music, sound effects, buzzer), video (window mode, size) and language.
/// Changes are a draft until Apply: volumes and the language preview right away, the window changes on
/// Apply. Discard goes back to what's saved, Reset fills in the defaults (still to be applied).
/// </summary>
public partial class SettingsScreen : Control
{
	// Kept across rebuilds of this screen (a language change rebuilds it so all text switches).
	static Settings.Values? _draft;
	static string? _note;

	static Settings.Values Draft => _draft ??= Settings.Current;
	static bool Dirty => Draft != Settings.Current;

	readonly Label _status = U.Muted("", 13, false, HorizontalAlignment.Right);
	readonly Button _apply;
	readonly Button _discard;
	readonly Button _reset;

	public SettingsScreen()
	{
		SetAnchorsPreset(LayoutPreset.FullRect);
		var d = Draft;

		var audio = U.Card(14,
			U.Eyebrow("Audio"),
			Volume("Master volume", "Everything Quiznight plays.", d.Master, v => Change(Draft with { Master = v })),
			Volume("Music", "Songs and clips in sound and video questions.", d.Music, v => Change(Draft with { Music = v })),
			Volume("Game music", "Menu music and the podium jingle.", d.GameMusic, v => Change(Draft with { GameMusic = v })),
			Volume("Sound effects", "Buzzer and other sound effects.", d.Sfx, v => Change(Draft with { Sfx = v })),
			new HSeparator(),
			BuzzerSection(),
			new HSeparator(),
			PodiumSection()).Grow();

		// Only sizes that fit on the chosen monitor (a 1440p screen can't pick 4K).
		var fitting = Prefs.SizesFor(d.Screen);
		if (!fitting.Contains(d.WindowSize))
		{
			_draft = d = d with { WindowSize = fitting.Where(s => s.X <= d.WindowSize.X && s.Y <= d.WindowSize.Y).DefaultIfEmpty(fitting[0]).MaxBy(s => s.X * s.Y) };
		}
		var native = Prefs.ScreenSize(d.Screen);
		var sizes = fitting.Select(s => ($"{s.X}x{s.Y}", $"{s.X} × {s.Y}  ·  {Prefs.AspectName(s)}" + (s == native ? "  " + Lang.T("(this screen)") : ""))).ToArray();
		var size = U.Select(sizes, $"{d.WindowSize.X}x{d.WindowSize.Y}", id =>
		{
			var p = id.Split('x');
			Change(Draft with { WindowSize = new Vector2I(int.Parse(p[0]), int.Parse(p[1])) });
		});
		var sizeHint = U.Muted("", 12);
		void UpdateSize()
		{
			var windowed = Draft.WindowMode == "windowed";
			size.Disabled = !windowed;
			sizeHint.Text = windowed ? "" : Lang.T("Fullscreen always uses your monitor's resolution.");
			sizeHint.Visible = !windowed;
		}
		UpdateSize();
		var screens = Enumerable.Range(0, Prefs.ScreenCount).Select(i =>
		{
			var s = Prefs.ScreenSize(i);
			var main = i == DisplayServer.GetPrimaryScreen() ? "  " + Lang.T("(main screen)") : "";
			return (i.ToString(), Lang.F("Screen {0}", i + 1) + $"  ·  {s.X} × {s.Y}{main}");
		}).ToArray();
		var screen = U.Select(screens, d.Screen.ToString(), id =>
		{
			Change(Draft with { Screen = int.Parse(id) });
			// The sizes depend on the screen.
			Callable.From(App.I.GoSettings).CallDeferred();
		});
		var mode = U.Segmented(new[] { ("windowed", "Windowed"), ("fullscreen", "Fullscreen"), ("borderless", "Borderless") }, d.WindowMode, id =>
		{
			Change(Draft with { WindowMode = id });
			UpdateSize();
		});
		var video = U.Card(12,
			U.Eyebrow("Video"),
			U.Field("Screen", screen),
			U.Field("Window mode", mode),
			U.Field("Window size", size),
			sizeHint,
			U.Field("Background picture", U.Select(Backgrounds.All, d.Background, id => Change(Draft with { Background = id }))),
			U.Field("Background movement", U.Segmented(new[] { ("on", "On"), ("off", "Off") }, d.BackgroundMotion ? "on" : "off", id => Change(Draft with { BackgroundMotion = id == "on" }))),
			U.Muted("On wide monitors, the space picture beside the game sways with the mouse. Off keeps it still.", 12),
			U.Field("Theme", U.Small("☀ Switch to light mode", HomeScreen.LightMode).With(b => b.SizeFlagsHorizontal = SizeFlags.ShrinkBegin)),
			U.Muted("Frame rate is capped at 60 FPS.", 12));

		var language = U.Card(12,
			U.Eyebrow("Language"),
			U.Segmented(Lang.All, d.Language, code =>
			{
				Change(Draft with { Language = code });
				// Rebuild so text made from parts (Lang.F) switches too.
				Callable.From(App.I.GoSettings).CallDeferred();
			}),
			U.Muted("Changes all menus and game screens. Quiz questions stay in the language they were written in.", 12));

		_reset = U.Btn("↺ Reset to defaults", "", () =>
		{
			_draft = Settings.Defaults;
			_note = "reset";
			Preview();
			App.I.GoSettings();
		});
		_discard = U.Btn("Discard", "", () =>
		{
			_draft = Settings.Current;
			_note = null;
			Preview();
			App.I.GoSettings();
		});
		_apply = U.Btn("Apply", "AccentButton", Apply);
		var bar = U.H(10, _reset, _status.Grow(), _discard, _apply);
		_status.SizeFlagsVertical = SizeFlags.ShrinkCenter;

		var page = U.V(14,
			U.H(16, U.Link("← Home", Leave).With(b => b.SizeFlagsVertical = SizeFlags.ShrinkCenter), U.Heading("Settings", 34)),
			U.H(14, audio, U.V(14, video, language).Grow()));
		// The buttons stay at the bottom of the screen; the settings scroll above them.
		var scroll = U.Page(page, 980, 20);
		scroll.SizeFlagsVertical = SizeFlags.ExpandFill;
		var footer = new Column { MaxWidth = 980, PadTop = 0 };
		footer.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		footer.AddChild(U.Pad(bar, 0, 12, 0, 14));
		var footerPanel = U.Panel(footer, "Clear").With(p => p.AddThemeStyleboxOverride("panel", UiTheme.Box(C.Panel, 0, C.Border, 0, 0, 0)));
		var root = U.V(0, scroll, new HSeparator(), footerPanel);
		root.SetAnchorsPreset(LayoutPreset.FullRect);
		AddChild(root);
		UpdateBar();
	}

	/// <summary>A new draft: hear the volumes and see the language now, keep the rest for Apply.</summary>
	void Change(Settings.Values draft)
	{
		_draft = draft;
		_note = null;
		Preview();
		UpdateBar();
	}

	static void Preview()
	{
		Prefs.ApplyAudio(Draft);
		Lang.Use(Draft.Language);
	}

	void Apply()
	{
		var windowChanged = Draft.WindowMode != Settings.WindowMode || Draft.WindowSize != Settings.WindowSize || Draft.Screen != Settings.Screen;
		Settings.Save(Draft);
		Prefs.ApplyAudio();
		Lang.Use(Settings.Language);
		if (windowChanged) Prefs.ApplyWindow();
		_note = "applied";
		UpdateBar();
	}

	/// <summary>Back home; unsaved changes are dropped (after asking).</summary>
	void Leave()
	{
		void Go()
		{
			_draft = null;
			_note = null;
			Prefs.ApplyAudio();
			Lang.Use(Settings.Language);
			App.I.GoHome();
		}
		if (Dirty) Dialogs.Confirm(this, "You have changes you haven't applied. Leave without saving them?", "Discard changes", Go);
		else Go();
	}

	void UpdateBar()
	{
		_apply.Disabled = !Dirty;
		_discard.Disabled = !Dirty;
		_reset.Disabled = Draft == Settings.Defaults;
		var (text, color) = _note switch
		{
			"applied" when !Dirty => (Lang.T("✓ Settings saved"), C.Teal),
			"reset" when Dirty => (Lang.T("Defaults filled in — press Apply to keep them."), C.Accent),
			_ when Dirty => (Lang.T("Unsaved changes"), C.Accent),
			_ => ("", C.Muted),
		};
		_status.Text = text;
		_status.AddThemeColorOverride("font_color", color);
	}

	Control BuzzerSection()
	{
		var error = U.Error();
		var own = U.Small("Use my own sound…", () => Dialogs.OpenFile(this, "Pick a sound file", Sfx.FileFilters, path =>
		{
			try
			{
				Sfx.SetCustom(path);
				Sfx.PlayBuzzer(Sfx.Custom);
				_draft = Draft with { Buzzer = Sfx.Custom };
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
			BuzzerPicker.Make(Draft.Buzzer, id => Change(Draft with { Buzzer = id }), true),
			U.Muted("Plays for the host and all players when someone buzzes in. This is the default for new lobbies; the host can still change it in the lobby for one night. If you host with your own sound, players hear their own default buzzer instead.", 12),
			U.H(10, own, U.Muted(Lang.F("MP3, OGG or WAV, up to {0} MB.", Sfx.CustomMaxMb), 12, false).With(l => l.SizeFlagsVertical = SizeFlags.ShrinkCenter)),
			error);
	}

	Control PodiumSection()
	{
		var follow = new CheckBox { Text = Lang.T("As a player, play the one the host picked"), ButtonPressed = Draft.PodiumFollowHost, MouseDefaultCursorShape = CursorShape.PointingHand };
		follow.Toggled += on => Change(Draft with { PodiumFollowHost = on });
		return U.V(6,
			U.Text("Podium sound", 15),
			PodiumPicker.Make(Draft.Podium, id => Change(Draft with { Podium = id }), false),
			follow,
			U.Muted("Plays with the final results. As host, it's the default for your lobbies (you can change it there for one night). As a player, untick the box to always hear this one.", 12));
	}

	static Control Volume(string label, string hint, int value, System.Action<int> changed)
	{
		var shown = U.Mono($"{value}%", 15, C.Muted);
		var slider = new HSlider { MinValue = 0, MaxValue = 100, Step = 1, Value = value, MouseDefaultCursorShape = CursorShape.PointingHand };
		slider.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		slider.ValueChanged += v =>
		{
			shown.Text = $"{(int)v}%";
			changed((int)v);
		};
		// One row: name, slider, value (the explanation is the tooltip).
		var row = U.H(12, U.Text(label, 15).MinSize(150).With(l => l.SizeFlagsVertical = SizeFlags.ShrinkCenter), slider.With(s => s.SizeFlagsVertical = SizeFlags.ShrinkCenter), shown.MinSize(48).With(l => l.HorizontalAlignment = HorizontalAlignment.Right));
		row.TooltipText = Lang.T(hint);
		slider.TooltipText = Lang.T(hint);
		return row;
	}
}
