// The root of the app: shows one screen at a time, owns the host or player session, and routes
// room state to the lobby, game and podium screens.

using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using QuizNight.Dev;
using QuizNight.Net;
using QuizNight.Server;
using QuizNight.Shared;
using QuizNight.Storage;
using QuizNight.Ui;
using QuizNight.Ui.Screens;

namespace QuizNight;

public partial class App : Control
{
	public static App I { get; private set; } = null!;
	public static string Version => ProjectSettings.GetSetting("application/config/version", "0.0.0").AsString();

	/// <summary>
	/// LAN play is for testing: exported games hide it until the developer code is typed on the home screen
	/// (until the game closes). Runs from the editor always have it.
	/// </summary>
	// (test builds always start in developer mode)
	public static bool LanUnlocked { get; set; } = !OS.HasFeature("template") || Updater.TestChannel;
	public const string DevCode = "61969";

	static readonly string LanClientId = QuizStore.MakeId();

	/// <summary>How to host and join: the saved choice once LAN is unlocked, else Steam.</summary>
	public static NetMode NetMode => !LanUnlocked ? NetMode.Steam : SteamService.Available ? Settings.Mode : NetMode.Lan;

	public HostSession? Host { get; private set; }
	public ClientSession? Client { get; private set; }
	HostAssets? _hostAssets;

	readonly Control _screens = new ScreenFrame();
	readonly VBoxContainer _banners = U.V(0);
	Control? _screen;
	string? _bannerError;
	DevTools? _dev;

	readonly ColorRect _bg = new();

	public override void _Ready()
	{
		I = this;
		// (nothing saved yet: the very first start on this PC, which asks for the accessibility settings first)
		var firstStart = Settings.NothingSaved;
		var volumesReset = Settings.ResetVolumesOnce(firstStart);
		// (sends the last run's log if it crashed or hit an error; exported builds only)
		// (test builds log every step in detail, and send the log after each session: CrashReports)
		Trace.On = Updater.TestChannel;
		CrashReports.Start();
		Trace.Log("start", $"Quiznight {Version} test build · {OS.GetName()} {OS.GetVersion()} · {DisplayServer.ScreenGetSize()} · skin {Settings.Skin} · {Settings.Language} · {Settings.WindowMode} {Settings.WindowSize}");
		// (dev runs: --skin ID, --dyslexic and --colour-vision MODE try them without saving)
		var devArgs = OS.GetCmdlineUserArgs();
		var skinArg = Array.IndexOf(devArgs, "--skin");
		C.Use(skinArg >= 0 && skinArg + 1 < devArgs.Length ? devArgs[skinArg + 1] : Settings.ShownSkin);
		Fonts.UseDyslexic(Settings.DyslexicFont || devArgs.Contains("--dyslexic"));
		Theme = UiTheme.Build();
		SetAnchorsPreset(LayoutPreset.FullRect);
		_bg.Color = C.Bg;
		AddChild(_bg.With(r => r.SetAnchorsPreset(LayoutPreset.FullRect)));
		AddChild(new SpaceBackdrop().With(b => b.SetAnchorsPreset(LayoutPreset.FullRect)));
		_screens.SetAnchorsPreset(LayoutPreset.FullRect);
		AddChild(_screens);
		_banners.SetAnchorsPreset(LayoutPreset.TopWide);
		_banners.MouseFilter = MouseFilterEnum.Ignore;
		AddChild(_banners);
		AddChild(_audio);
		AddChild(new Jukebox());
		AddChild(_colours);
		AddChild(Announcer);
		var cv = Array.IndexOf(devArgs, "--colour-vision");
		_colours.Use(cv >= 0 && cv + 1 < devArgs.Length ? devArgs[cv + 1] : Settings.ColourVision);
		_audio.TrackStarted += path => AddChild(new NowPlaying(path));
		GetTree().AutoAcceptQuit = false;
		// (the name players see; project.godot keeps "QuizNight!", which names the exported data folder that updates rely on)
		GetWindow().Title = "Quiznight?";
		GetWindow().FilesDropped += OnFilesDropped;

		var args = OS.GetCmdlineUserArgs();
		// Saved window size/mode would fight --resolution (screenshots) and means nothing headless.
		var ownWindow = DisplayServer.GetName() != "headless" && !OS.GetCmdlineArgs().Contains("--resolution");
		Prefs.ApplyAll(ownWindow);
		if (args.Contains("--selftest")) Lang.Use("en");
		// (shows the exported game's home screen, LAN hidden, when run from the editor)
		if (args.Contains("--lan-locked")) LanUnlocked = false;
		if (!args.Contains("--no-steam") && !args.Contains("--selftest") && !args.Contains("--bot")) SteamService.Init();
		_dev = DevTools.FromArgs(this, args);
		if (_dev?.TakesOver == true) return;
		var window = DisplayServer.GetName() != "headless";
		if (window && (firstStart && args.Length == 0 || args.Contains("--welcome")) && !Settings.AccessibilityAsked)
		{
			_afterWelcome = () => Start(args.Length == 0 && window);
			ShowWelcomeAgain();
		}
		else Start(args.Length == 0 && window, volumesReset ? VolumesNotice : null);
	}

	const string VolumesNotice = "All volume sliders are at 50 % now, the new standard. Turn them up again in Settings if you liked it louder.";

	/// <summary>The splash (not in dev and test runs, which pass arguments after --), then the home screen.</summary>
	void Start(bool splash, string? notice = null)
	{
		IntroSequence? intro = null;
		// (the intro's title screen plays the voice line when it's left; the music follows it)
		if (splash && !Announcer.ReduceMotion) AddChild(intro = new IntroSequence(() => { if (_screen is HomeScreen) _audio.Greet(); }));
		// (the music waits for the voice line, which comes once the intro is gone: Popups → Greet)
		_audio.HoldForGreeting();
		GoHome(notice);
		// (What's new and the update prompt are windows of their own: they wait until the splash is gone)
		void Popups()
		{
			if (_screen is HomeScreen) _audio.Greet();
			else _audio.SkipGreeting();
			WhatsNew.ShowIfUpdated(this);
			if (Updater.Enabled) Updater.CleanUp();
			UpdatePrompt.CheckAndInstall(this);
		}
		if (intro != null) intro.TreeExited += Popups;
		else Popups();
	}

	Action? _afterWelcome;

	/// <summary>The first start's accessibility screen (again, after a language or font change rebuilt it).</summary>
	public void ShowWelcomeAgain() => Show(new WelcomeScreen(() => _afterWelcome?.Invoke()));

	/// <summary>Seconds until the next quiet update check (every minute while the game is open).</summary>
	double _updateCheckIn = 60;

	public override void _Process(double delta)
	{
		_updateCheckIn -= delta;
		if (_updateCheckIn <= 0)
		{
			_updateCheckIn = 60;
			UpdatePrompt.CheckQuietly();
		}
		SteamService.RunCallbacks();
		Host?.Poll();
		Client?.Poll();
		_dev?.Process(delta);
	}

	public override void _Notification(int what)
	{
		if (what == NotificationWMCloseRequest) Quit();
	}

	/// <summary>
	/// Closes the room and quits on the next frame: this may be called from inside a Steam callback
	/// (an awaited Steam call), and shutting Steam down there crashes it.
	/// </summary>
	public void Quit(int code = 0) => Callable.From(() => QuitNow(code)).CallDeferred();

	void QuitNow(int code)
	{
		Host?.Close("The host closed Quiznight.");
		Host = null;
		Client?.Leave();
		Client = null;
		SteamService.Shutdown();
		CrashReports.Stop();
		GetTree().Quit(code);
	}

	/// <summary>Menu music and the buttons' hover and click sounds.</summary>
	readonly UiAudio _audio = new();
	public UiAudio Audio => _audio;
	/// <summary>Speaks game events to screen readers (an invisible live region).</summary>
	public readonly Announcer Announcer = new();
	/// <summary>Colour vision help over everything (Settings → Accessibility).</summary>
	readonly ColourFilter _colours = new();

	public void UseColourVision(string mode)
	{
		Settings.ColourVision = mode;
		_colours.Use(mode);
	}

	/// <summary>Switches the dyslexia-friendly font: a new theme, and the settings (or first start) screen built again with it.</summary>
	public void UseDyslexicFont(bool on)
	{
		Settings.DyslexicFont = on;
		Fonts.UseDyslexic(on);
		Theme = UiTheme.Build();
		if (_screen is WelcomeScreen) ShowWelcomeAgain();
		else GoSettings();
	}

	internal void Show(Control screen, Action? again = null)
	{
		// (history for the mouse's back/forward buttons: a screen left by going back can be opened again with forward;
		// any other way to a new screen starts over, like a browser)
		if (_backFrom != null && _backFrom == _screen && _shownAgain != null) _forward.Push(_shownAgain);
		else if (!_forwarding) _forward.Clear();
		_backFrom = null;
		_shownAgain = again;
		Sfx.StopPreview();
		_screens.Clear();
		Trace.Log("screen", screen.GetType().Name);
		_screen = screen;
		screen.SetAnchorsPreset(LayoutPreset.FullRect);
		_screens.AddChild(screen);
		_audio.ScreenChanged(screen);
		// (the home screen has its own music player instead of the corner jukebox; the welcome screen neither)
		if (Jukebox.I != null) Jukebox.I.ButtonShown = screen is not (HomeScreen or WelcomeScreen);
		UpdateBanners();
	}

	public Control? CurrentScreen => _screen;

	// ---------- Escape: one screen back ----------

	/// <summary>
	/// Escape goes back one screen (on the home screen it asks to quit): the screen's "← …" link, or what leaving means in a
	/// room (closing the lobby or ending the quiz ask first). Open dialogs, drop-downs and the picture viewer get the key
	/// first and close themselves instead.
	/// </summary>
	public override void _UnhandledKeyInput(InputEvent e)
	{
		if (e is not InputEventKey { Pressed: true, Echo: false } key || _screen == null) return;
		if (GetChildren().OfType<IntroSequence>().Any()) return;
		// (the credits window: Escape closes it)
		if (CreditsWindow.Current != null)
		{
			if (key.Keycode == Key.Escape) CreditsWindow.Close();
			GetViewport().SetInputAsHandled();
			return;
		}
		// (Settings open over a lobby or a game: Escape closes it, Ctrl+S applies; the room underneath isn't touched)
		if (SettingsWindow.Current is { } window)
		{
			var used = key.Keycode == Key.Escape ? window.Leave() : key.Keycode == Key.S && key.CtrlPressed && window.ApplyKey();
			if (used) GetViewport().SetInputAsHandled();
			return;
		}
		var handled = key.Keycode switch
		{
			Key.Escape => _screen is HomeScreen ? AskQuit() : Back(),
			// Ctrl+S: Apply in Settings, Save in the question editor
			Key.S when key.CtrlPressed => _screen switch
			{
				SettingsScreen settings => settings.ApplyKey(),
				QuestionEditorScreen editor => editor.SaveKey(),
				_ => false,
			},
			_ => false,
		};
		if (handled) GetViewport().SetInputAsHandled();
	}

	// The mouse's thumb buttons go back and forward like in a browser. (_Input, not _UnhandledInput: every panel stops
	// mouse clicks, so they'd never get there. An open dialog gets them first.)
	public override void _Input(InputEvent e)
	{
		if (e is not InputEventMouseButton { Pressed: true } button || _screen == null) return;
		// (a right-click on empty space goes back too, like Escape: not on buttons, fields, sliders or lists)
		var rightClick = button.ButtonIndex == MouseButton.Right && OnEmptySpace();
		if (button.ButtonIndex is not (MouseButton.Xbutton1 or MouseButton.Xbutton2) && !rightClick) return;
		if (GetChildren().OfType<IntroSequence>().Any()) return;
		var back = rightClick || button.ButtonIndex == MouseButton.Xbutton1;
		if (CreditsWindow.Current != null)
		{
			if (back) CreditsWindow.Close();
			GetViewport().SetInputAsHandled();
			return;
		}
		// (the mouse's back button closes Settings open over a game, instead of leaving the room)
		if (SettingsWindow.Current is { } window)
		{
			if (back) window.Leave();
			GetViewport().SetInputAsHandled();
			return;
		}
		if (back ? _screen is not HomeScreen && Back() : Forward()) GetViewport().SetInputAsHandled();
	}

	/// <summary>Whether the mouse is over empty space: the background, a card, a label, a picture — nothing that takes clicks.</summary>
	bool OnEmptySpace()
	{
		for (Node? node = GetViewport().GuiGetHoveredControl(); node != null && node != this; node = node.GetParent())
			if (node is BaseButton or LineEdit or TextEdit or Godot.Range or ItemList or Tree or TabBar || node is RichTextLabel { SelectionEnabled: true })
				return false;
		return true;
	}

	readonly Stack<Action> _forward = new();
	/// <summary>Opens the shown screen again (null for room screens, which can't be).</summary>
	Action? _shownAgain;
	Control? _backFrom;
	bool _forwarding;

	bool Back()
	{
		_backFrom = _screen;
		return GoBack(_screen!);
	}

	bool Forward()
	{
		if (!_forward.TryPop(out var again)) return false;
		_forwarding = true;
		try { again(); }
		finally { _forwarding = false; }
		return true;
	}

	/// <summary>Escape or "Quit" on the home screen: asks first.</summary>
	public bool AskQuit()
	{
		if (_screen != null) Dialogs.Confirm(_screen, "Close Quiznight?", "Quit", () => Quit(), "Quit the game");
		return true;
	}

	bool GoBack(Control screen)
	{
		switch (screen)
		{
			case HomeScreen or WelcomeScreen:
				return false;
			case HostLobbyScreen:
				Dialogs.Confirm(screen, "Close the room? Everyone in it is sent back to the home screen.", "Close room", CloseRoom);
				return true;
			case HostGameScreen game:
				game.AskEnd();
				return true;
			case PlayerGameScreen:
				Dialogs.Confirm(screen, "Leave the game? Your seat and points are kept, and you can come back with the room code.", "Leave", LeaveRoom);
				return true;
			case WaitingRoomScreen:
				LeaveRoom();
				return true;
			case PodiumScreen podium:
				podium.GoBack();
				return true;
			case MessageScreen message:
				if (message.Escape != null) message.Escape();
				else GoHome();
				return true;
		}
		// (every other screen has a "← Home", "← My quizzes" … link: Escape presses it)
		if (BackLink(screen) is { } link)
		{
			link.EmitSignal(BaseButton.SignalName.Pressed);
			return true;
		}
		GoHome();
		return true;
	}

	static BaseButton? BackLink(Node node)
	{
		foreach (var child in node.GetChildren())
		{
			if (child is Button { Text: var text } button && text.StartsWith("←") && button.IsVisibleInTree() && !button.Disabled) return button;
			if (BackLink(child) is { } found) return found;
		}
		return null;
	}

	void OnFilesDropped(string[] files)
	{
		switch (_screen)
		{
			case QuestionEditorScreen e:
				e.OnFilesDropped(files);
				break;
			case QuizListScreen l:
				l.OnFilesDropped(files);
				break;
		}
	}

	// ---------- navigation (outside a room) ----------

	public void GoHome(string? notice = null) => Show(new HomeScreen(notice), () => GoHome());
	public void GoQuizzes() => Show(new QuizListScreen(), GoQuizzes);
	public void GoSettings() => Show(new SettingsScreen(), GoSettings);

	/// <summary>Switches the colour skin now: a new theme, and the settings screen built again in its colours.</summary>
	public void UseSkin(string skin)
	{
		Settings.Skin = skin;
		C.Use(Settings.ShownSkin);
		Theme = UiTheme.Build();
		_bg.Color = C.Bg;
		GoSettings();
	}
	public void GoEternalTable() => Show(new EternalTableScreen(), GoEternalTable);
	public void GoMatchHistory() => Show(new MatchHistoryScreen(), GoMatchHistory);
	/// <summary>A player's profile page (from a name in the Ewige Tabelle or the match history).</summary>
	public void GoProfile(string playerId) => Show(new ProfileScreen(playerId), () => GoProfile(playerId));
	/// <summary>Statistics: the Ewige Tabelle or the match history, whichever was open last.</summary>
	public void GoStatistics()
	{
		if (EternalTableScreen.LastTab == "history") GoMatchHistory();
		else GoEternalTable();
	}
	public void GoMatchNight(Storage.Results.Night night) => Show(new MatchNightScreen(night), () => GoMatchNight(night));

	public void GoQuiz(string id)
	{
		if (QuizStore.Get(id) is { } quiz) Show(new QuizOverviewScreen(quiz), () => GoQuiz(id));
		else GoQuizzes();
	}

	public void GoQuestion(string quizId, string? questionId, string? newType)
	{
		var quiz = QuizStore.Get(quizId);
		if (quiz == null)
		{
			GoQuizzes();
			return;
		}
		var question = questionId != null ? quiz.Questions.FirstOrDefault(q => q.Id == questionId)
			: newType != null && QuestionType.IsKnown(newType) ? QuizRules.NewQuestion(newType, QuizStore.MakeId()) : null;
		if (question == null) GoQuiz(quizId);
		else Show(new QuestionEditorScreen(quiz, question, questionId == null), questionId != null ? () => GoQuestion(quizId, questionId, null) : null);
	}

	// ---------- hosting ----------

	public async void HostQuiz(Quiz quiz, NetMode? forceMode = null)
	{
		var issues = QuizRules.QuizIssues(quiz);
		if (issues.Count > 0)
		{
			ShowError(Lang.F("Couldn't open the lobby: {0}", issues[0]));
			return;
		}
		var mode = forceMode ?? NetMode;
		Show(new MessageScreen(mode == NetMode.Steam ? "Opening the lobby on Steam…" : "Opening the lobby…"));
		ServerClock.Reset();
		IHostTransport transport;
		string code;
		var lanPort = 0;
		if (mode == NetMode.Steam)
		{
			var (steam, steamCode, error) = await SteamHostTransport.Create(() => Room.NewCode());
			if (steam == null)
			{
				GoQuizzes();
				ShowError(Lang.F("Couldn't open the lobby: {0}", Lang.T(error ?? "")));
				return;
			}
			transport = steam;
			code = steamCode;
		}
		else
		{
			var lan = new LanHostTransport();
			if (lan.Listen() is { } error)
			{
				GoQuizzes();
				ShowError(error);
				return;
			}
			transport = lan;
			code = Room.NewCode();
			lanPort = lan.Port;
		}
		Host = new HostSession(new Room(code, quiz), transport, mode, lanPort) { BuzzerSound = Settings.BuzzerSound, PodiumSound = Settings.PodiumSound };
		_hostAssets = new HostAssets(() => Host?.Room);
		Host.Changed += OnRoomChanged;
		OnRoomChanged();
	}

	/// <summary>The host closes the room and goes back to the quiz.</summary>
	public void CloseRoom()
	{
		var quizId = Host?.Room.Quiz.Id;
		Host?.Close();
		Host = null;
		_hostAssets = null;
		if (quizId != null) GoQuiz(quizId);
		else GoQuizzes();
	}

	// ---------- joining ----------

	public void JoinRoom(NetMode mode, string target, string name, string? clientId = null)
	{
		Client?.Dispose();
		// (the same id all session: on LAN that's who you are, so joining again gets your seat and score back)
		clientId ??= LanClientId;
		var client = mode == NetMode.Steam
			? ClientSession.Steam(Limits.NormalizeCode(target), name)
			: ClientSession.Lan(target, LanHostTransport.DefaultPort, name, clientId);
		Client = client;
		client.Changed += OnRoomChanged;
		client.Ended += reason =>
		{
			if (Client != client) return;
			Client = null;
			GoHome(reason);
		};
		Show(new MessageScreen(mode == NetMode.Steam ? Lang.F("Looking for room {0}…", Limits.NormalizeCode(target)) : Lang.F("Connecting to {0}…", target),
			"Cancel", () => client.Leave()));
		client.Start();
	}

	public void LeaveRoom() => Client?.Leave();

	// ---------- room screens ----------

	void Route<T>(RoomState state, Func<T> make) where T : Control, IRoomScreen
	{
		if (_screen is not T screen)
		{
			screen = make();
			Show(screen);
		}
		screen.Refresh(state);
	}

	void OnRoomChanged()
	{
		if (Host != null)
		{
			var host = Host;
			var state = host.State;
			switch (state.Phase)
			{
				case Phase.Lobby:
					Route(state, () => new HostLobbyScreen(host));
					break;
				case Phase.Playing:
					Route(state, () => new HostGameScreen(host, _hostAssets!));
					break;
				default:
					Route(state, () => new PodiumScreen(true, null, host.BackToLobby));
					break;
			}
		}
		else if (Client is { } client)
		{
			UpdateBanners();
			if (client.State is not { } state || client.MyId == null) return;
			switch (state.Phase)
			{
				case Phase.Lobby:
					Route(state, () => new WaitingRoomScreen(client));
					break;
				case Phase.Playing:
					Route(state, () => new PlayerGameScreen(client));
					break;
				default:
					Route(state, () => new PodiumScreen(false, client.MyId, null));
					break;
			}
		}
	}

	// ---------- banners ----------

	public void ShowError(string message)
	{
		_bannerError = message;
		UpdateBanners();
	}

	void UpdateBanners()
	{
		_banners.Clear();
		// (Leave works while it tries: nobody is stuck waiting for a host that's gone)
		if (Client is { Reconnecting: true })
			_banners.AddChild(Banner("Connection lost — reconnecting…", C.Accent, null, ("Leave", LeaveRoom)));
		if (_bannerError != null)
			_banners.AddChild(Banner(_bannerError, C.Coral, () =>
			{
				_bannerError = null;
				UpdateBanners();
			}));
	}

	static Control Banner(string text, Color color, Action? onClose, (string Label, Action Press)? button = null)
	{
		var row = U.H(12, U.Text(text, 15, C.Bg, Fonts.Bold, true).Grow());
		if (button is { } b)
			row.AddChild(U.Btn(b.Label, "SmallButton", b.Press, 14).Tip("Back to the home screen"));
		if (onClose != null) row.AddChild(U.Btn("×", "LinkButton2", onClose, 18).With(b => b.AddThemeColorOverride("font_color", C.Bg)));
		var p = U.Panel(row, "Clear");
		p.AddThemeStyleboxOverride("panel", UiTheme.Box(color, 0, null, 0, 20, 8));
		return p;
	}
}
