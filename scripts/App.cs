// The root of the app: shows one screen at a time, owns the host or player session, and routes
// room state to the lobby, game and podium screens.

using System;
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
	public static bool LanUnlocked { get; set; } = !OS.HasFeature("template");
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
		// (sends the last run's log if it crashed or hit an error; exported builds only)
		CrashReports.Start();
		C.Use(Settings.Skin);
		// (dev runs: --dyslexic and --colour-vision MODE try them without saving)
		var devArgs = OS.GetCmdlineUserArgs();
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
		AddChild(_colours);
		AddChild(Announcer);
		var cv = Array.IndexOf(devArgs, "--colour-vision");
		_colours.Use(cv >= 0 && cv + 1 < devArgs.Length ? devArgs[cv + 1] : Settings.ColourVision);
		_audio.TrackStarted += path => AddChild(new NowPlaying(path));
		// (the splash plays out on top of the first screen; not in dev and test runs, which pass arguments after --)
		SplashIntro? splash = null;
		if (OS.GetCmdlineUserArgs().Length == 0 && DisplayServer.GetName() != "headless" && !Announcer.ReduceMotion) AddChild(splash = new SplashIntro());

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
		if (_dev?.TakesOver != true)
		{
			GoHome();
			// (What's new and the update prompt are windows of their own: they wait until the splash is gone)
			void Popups()
			{
				if (_screen is HomeScreen) _audio.Greet();
				WhatsNew.ShowIfUpdated(this);
				if (Updater.Enabled) Updater.CleanUp();
				UpdatePrompt.CheckAndInstall(this);
			}
			if (splash != null) splash.TreeExited += Popups;
			else Popups();
		}
	}

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

	/// <summary>Switches the dyslexia-friendly font: a new theme, and the settings screen built again with it.</summary>
	public void UseDyslexicFont(bool on)
	{
		Settings.DyslexicFont = on;
		Fonts.UseDyslexic(on);
		Theme = UiTheme.Build();
		GoSettings();
	}

	void Show(Control screen)
	{
		Sfx.StopPreview();
		_screens.Clear();
		_screen = screen;
		screen.SetAnchorsPreset(LayoutPreset.FullRect);
		_screens.AddChild(screen);
		_audio.ScreenChanged(screen);
		UpdateBanners();
	}

	public Control? CurrentScreen => _screen;

	// ---------- Escape: one screen back ----------

	/// <summary>
	/// Escape goes back one screen everywhere but the home screen: the screen's "← …" link, or what leaving means in a
	/// room (closing the lobby or ending the quiz ask first). Open dialogs, drop-downs and the picture viewer get the key
	/// first and close themselves instead.
	/// </summary>
	public override void _UnhandledKeyInput(InputEvent e)
	{
		if (e is not InputEventKey { Pressed: true, Echo: false, Keycode: Key.Escape } || _screen == null) return;
		if (GetChildren().OfType<SplashIntro>().Any()) return;
		if (GoBack(_screen)) GetViewport().SetInputAsHandled();
	}

	bool GoBack(Control screen)
	{
		switch (screen)
		{
			case HomeScreen:
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

	public void GoHome(string? notice = null) => Show(new HomeScreen(notice));
	public void GoQuizzes() => Show(new QuizListScreen());
	public void GoSettings() => Show(new SettingsScreen());

	/// <summary>Switches the colour skin now: a new theme, and the settings screen built again in its colours.</summary>
	public void UseSkin(string skin)
	{
		Settings.Skin = skin;
		C.Use(skin);
		Theme = UiTheme.Build();
		_bg.Color = C.Bg;
		GoSettings();
	}
	public void GoEternalTable() => Show(new EternalTableScreen());
	public void GoMatchHistory() => Show(new MatchHistoryScreen());
	/// <summary>A player's profile page (from a name in the Ewige Tabelle or the match history).</summary>
	public void GoProfile(string playerId) => Show(new ProfileScreen(playerId));
	/// <summary>Statistics: the Ewige Tabelle or the match history, whichever was open last.</summary>
	public void GoStatistics()
	{
		if (EternalTableScreen.LastTab == "history") GoMatchHistory();
		else GoEternalTable();
	}
	public void GoMatchNight(Storage.Results.Night night) => Show(new MatchNightScreen(night));

	public void GoQuiz(string id)
	{
		if (QuizStore.Get(id) is { } quiz) Show(new QuizOverviewScreen(quiz));
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
		else Show(new QuestionEditorScreen(quiz, question, questionId == null));
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
