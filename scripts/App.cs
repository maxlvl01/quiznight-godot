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

	public HostSession? Host { get; private set; }
	public ClientSession? Client { get; private set; }
	HostAssets? _hostAssets;

	readonly Control _screens = new();
	readonly VBoxContainer _banners = U.V(0);
	Control? _screen;
	string? _bannerError;
	DevTools? _dev;

	public override void _Ready()
	{
		I = this;
		Theme = UiTheme.Build();
		SetAnchorsPreset(LayoutPreset.FullRect);
		AddChild(new ColorRect { Color = C.Bg }.With(r => r.SetAnchorsPreset(LayoutPreset.FullRect)));
		_screens.SetAnchorsPreset(LayoutPreset.FullRect);
		AddChild(_screens);
		_banners.SetAnchorsPreset(LayoutPreset.TopWide);
		_banners.MouseFilter = MouseFilterEnum.Ignore;
		AddChild(_banners);

		GetTree().AutoAcceptQuit = false;
		GetWindow().FilesDropped += OnFilesDropped;

		var args = OS.GetCmdlineUserArgs();
		// Saved window size/mode would fight --resolution (screenshots) and means nothing headless.
		var ownWindow = DisplayServer.GetName() != "headless" && !OS.GetCmdlineArgs().Contains("--resolution");
		Prefs.ApplyAll(ownWindow);
		if (args.Contains("--selftest")) Lang.Use("en");
		if (!args.Contains("--no-steam") && !args.Contains("--selftest") && !args.Contains("--bot")) SteamService.Init();
		_dev = DevTools.FromArgs(this, args);
		if (_dev?.TakesOver != true)
		{
			GoHome();
			WhatsNew.ShowIfUpdated(this);
			UpdatePrompt.CheckAndInstall(this);
		}
	}

	public override void _Process(double delta)
	{
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
		GetTree().Quit(code);
	}

	void Show(Control screen)
	{
		_screens.Clear();
		_screen = screen;
		screen.SetAnchorsPreset(LayoutPreset.FullRect);
		_screens.AddChild(screen);
		UpdateBanners();
	}

	public Control? CurrentScreen => _screen;

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
		var mode = forceMode ?? (SteamService.Available ? Settings.Mode : NetMode.Lan);
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
		Host = new HostSession(new Room(code, quiz), transport, mode, lanPort) { BuzzerSound = Settings.BuzzerSound };
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
		clientId ??= QuizStore.MakeId();
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
		if (Client is { Reconnecting: true })
			_banners.AddChild(Banner("Connection lost — reconnecting…", C.Accent, null));
		if (_bannerError != null)
			_banners.AddChild(Banner(_bannerError, C.Coral, () =>
			{
				_bannerError = null;
				UpdateBanners();
			}));
	}

	static Control Banner(string text, Color color, Action? onClose)
	{
		var row = U.H(12, U.Text(text, 15, C.Bg, Fonts.Bold, true));
		if (onClose != null) row.AddChild(U.Btn("×", "LinkButton2", onClose, 18).With(b => b.AddThemeColorOverride("font_color", C.Bg)));
		var p = U.Panel(row, "Clear");
		p.AddThemeStyleboxOverride("panel", UiTheme.Box(color, 0, null, 0, 20, 8));
		return p;
	}
}
