using System.Linq;
using Godot;
using QuizNight.Shared;
using QuizNight.Storage;

namespace QuizNight.Ui.Screens;

/// <summary>
/// Audio (master, music, video/sound questions, sound effects, buzzer), video (window mode, size) and language.
/// Changes are a draft until Apply: volumes and the language preview right away, the window changes on
/// Apply. Discard goes back to what's saved, Reset fills in the defaults (still to be applied).
/// </summary>
public partial class SettingsScreen : Control
{
	// Kept across rebuilds of this screen (a language change rebuilds it so all text switches).
	static Settings.Values? _draft;
	static string? _note;

	static Settings.Values Draft => _draft ??= Fitted(Settings.Current);
	// (compared with the saved settings as the screen shows them, so a window size swapped for one that fits this monitor
	// doesn't count as a change nobody made)
	static bool Dirty => Draft != Fitted(Settings.Current);

	/// <summary>Only sizes that fit on the chosen monitor (a 1440p screen can't pick 4K): else the biggest one that does.</summary>
	static Settings.Values Fitted(Settings.Values d)
	{
		var fitting = Prefs.SizesFor(d.Screen);
		if (fitting.Contains(d.WindowSize)) return d;
		return d with { WindowSize = fitting.Where(s => s.X <= d.WindowSize.X && s.Y <= d.WindowSize.Y).DefaultIfEmpty(fitting[0]).MaxBy(s => s.X * s.Y) };
	}

	readonly Label _status = U.Muted("", 13, false, HorizontalAlignment.Right);
	/// <summary>Opened from a lobby or a game (SettingsWindow): closing goes back there, not home.</summary>
	readonly bool _inWindow;
	readonly Button _apply;
	readonly Button _discard;
	readonly Button _reset;

	public SettingsScreen() : this(false) { }

	public SettingsScreen(bool inWindow)
	{
		_inWindow = inWindow;
		SetAnchorsPreset(LayoutPreset.FullRect);
		var d = Draft;

		var audio = U.Card(14,
			U.Eyebrow("Audio"),
			Volume("Master volume", "Everything Quiznight plays.", d.Master, v => Change(Draft with { Master = v })),
			Volume("Music", "Menu music and the podium jingle.", d.GameMusic, v => Change(Draft with { GameMusic = v })),
			Volume("Video", "Videos, songs and clips in sound and video questions.", d.Music, v => Change(Draft with { Music = v })),
			Volume("Sound effects", "Buzzer and other sound effects.", d.Sfx, v => Change(Draft with { Sfx = v })),
			new HSeparator(),
			BuzzerSection(),
			new HSeparator(),
			PodiumSection()).Grow();

		_draft = d = Fitted(d);
		var fitting = Prefs.SizesFor(d.Screen);
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
			Callable.From(Rebuild).CallDeferred();
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
			// (the skin and the font build every screen again: from the home screen only, not in the middle of a game)
			U.Field("Skin", U.Select(Skins.Available, Settings.Skin, id => Callable.From(() => App.I.UseSkin(id)).CallDeferred()).With(o => o.Disabled = inWindow),
				inWindow ? "Change this on the home screen's Settings." : null),
			new CheckBox
			{
				Text = Lang.T("Seasonal skins (Halloween in October, Christmas in December)"), ButtonPressed = Settings.SeasonalSkins,
				Disabled = inWindow, MouseDefaultCursorShape = CursorShape.PointingHand,
				TooltipText = Lang.T("They come on by themselves in their season, and your own skin is back afterwards."),
			}.With(b => b.Toggled += on =>
			{
				Settings.SeasonalSkins = on;
				Callable.From(() => App.I.UseSkin(Settings.Skin)).CallDeferred();
			}),
			U.Field("Background picture", U.Select(Backgrounds.All, d.Background, id => Change(Draft with { Background = id }))),
			U.Field("Background movement", U.Segmented(new[] { ("on", "On"), ("off", "Off") }, d.BackgroundMotion ? "on" : "off", id => Change(Draft with { BackgroundMotion = id == "on" }))),
			U.Muted("On wide monitors, the space picture beside the game sways with the mouse. Off keeps it still.", 12),
			U.Field("Theme", U.Small("☀ Switch to light mode", HomeScreen.LightMode).With(b => b.SizeFlagsHorizontal = SizeFlags.ShrinkBegin)),
			U.Muted("Frame rate is capped at 60 FPS.", 12),
			new CheckBox { Text = Lang.T("Ask before deleting a quiz"), ButtonPressed = Settings.AskBeforeDeletingQuiz, MouseDefaultCursorShape = CursorShape.PointingHand }
				.With(b => b.Toggled += on => Settings.AskBeforeDeletingQuiz = on));

		// (accessibility: both apply and save right away, like the skin)
		var dyslexic = new CheckBox
		{
			Text = Lang.T("Dyslexia-friendly font"), ButtonPressed = Settings.DyslexicFont && Fonts.DyslexicAvailable,
			Disabled = !Fonts.DyslexicAvailable || inWindow, MouseDefaultCursorShape = CursorShape.PointingHand,
			TooltipText = inWindow ? Lang.T("Change this on the home screen's Settings.") : "",
		};
		dyslexic.Toggled += on => Callable.From(() => App.I.UseDyslexicFont(on)).CallDeferred();
		var accessibility = U.Card(12,
			U.Eyebrow("Accessibility"),
			U.Field("Colour vision", U.Select(ColourFilter.Modes, Settings.ColourVision, App.I.UseColourVision),
				"Shifts the colours of the whole game so the ones you can't tell apart become different: right and wrong, the A–D answers, medals and player colours."),
			dyslexic,
			U.Muted(Fonts.DyslexicAvailable ? "All text in OpenDyslexic: letters with heavy bottoms, so they don't flip or swim."
				: "The dyslexia-friendly font isn't installed with this version yet.", 12));

		var language = U.Card(12,
			U.Eyebrow("Language"),
			U.Segmented(Lang.All, d.Language, code =>
			{
				Change(Draft with { Language = code });
				// Rebuild so text made from parts (Lang.F) switches too.
				Callable.From(Rebuild).CallDeferred();
			}),
			U.Muted("Changes all menus and game screens. Quiz questions stay in the language they were written in.", 12),
			U.Gap(4),
			U.Eyebrow("What the game sends"),
			U.Muted("If Quiznight crashes or runs into an error, the next start sends the end of its log to Max, so he can fix it. Your Windows user name is left out.", 12),
			U.Muted("When a quiz night over Steam ends, the host's game sends its results (everyone's name, Steam ID and points) to Max's private results list, for the eternal table. \"Copy share code\" uploads that quiz (without its sounds and videos), and the game asks GitHub if there's an update. Nothing else leaves your PC.", 12),
			U.Gap(4),
			U.Eyebrow("Anti-cheat"),
			U.Muted("Quiznight? is protected by the Pinke Geilheit Neural-Level Anti-Cheat. Neural level means it reads your thoughts and bans you if you even think about cheating.", 12));

		_reset = U.Btn("↺ Reset to defaults", "", () =>
		{
			_draft = Settings.Defaults;
			_note = "reset";
			Preview();
			Rebuild();
		});
		_discard = U.Btn("Discard", "", () =>
		{
			_draft = Settings.Current;
			_note = null;
			Preview();
			Rebuild();
		});
		_apply = U.Btn("Apply", "AccentButton", Apply).With(b => b.TooltipText = "Ctrl+S");
		var bar = U.H(10, _reset, _status.Grow(), _discard, _apply);
		_status.SizeFlagsVertical = SizeFlags.ShrinkCenter;

		var top = inWindow
			? U.H(16, U.Heading("Settings", 34).Grow(), U.Small("Close", () => Leave()).With(b => b.SizeFlagsVertical = SizeFlags.ShrinkCenter))
			: U.H(16, U.Link("← Home", () => Leave()).With(b => b.SizeFlagsVertical = SizeFlags.ShrinkCenter), U.Heading("Settings", 34));
		var page = U.V(14, top,
			U.H(14, U.V(14, audio, accessibility).Grow(), U.V(14, video, language).Grow()));
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

	/// <summary>Ctrl+S: Apply (when there's something to apply).</summary>
	public bool ApplyKey()
	{
		if (Dirty) Apply();
		return true;
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

	/// <summary>This screen again (a language or screen change shows new text and sizes), here or in the window.</summary>
	void Rebuild()
	{
		if (_inWindow) SettingsWindow.Rebuild();
		else App.I.GoSettings();
	}

	/// <summary>Back home, or closing the window over a game; unsaved changes are dropped (after asking).</summary>
	public bool Leave()
	{
		void Go()
		{
			_draft = null;
			_note = null;
			Prefs.ApplyAudio();
			Lang.Use(Settings.Language);
			if (_inWindow) SettingsWindow.Close();
			else App.I.GoHome();
		}
		if (Dirty) Dialogs.Confirm(this, "You have changes you haven't applied. Leave without saving them?", "Discard changes", Go);
		else Go();
		return true;
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
				Rebuild();
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
