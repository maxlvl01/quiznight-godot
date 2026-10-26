// Command-line helpers for testing on one PC (arguments after "--"):
//   --selftest                     run the checks and quit (exit code 0 = all passed)
//   --host-sample | --host FILE    host the sample quiz / a quiz file in LAN mode
//   --sound FILE / --video FILE    with --host-sample or --open: the sound question plays that file / a video question follows it
//   --autopilot [--players N]      as host: start with N players, judge buzzes, go to the next question
//   --start-at N                   with --autopilot: skip straight to question N (1 = first)
//   --quit-at-end                  quit a few seconds after the podium
//   --bot NAME [--address IP]      join a LAN room and answer on its own
//   --open TYPE                    open the editor on the sample quiz's question of that type
//   --screen home|quizzes|quiz|settings|eternal|history|delete  open that screen (delete: the delete question, deleting nothing)
//   --lang en|de                   use that language this run (not saved)
//   --video-guide [FILE]           open a new video question with the MP4 → .ogv guide
//   --whats-new                    the home screen with this version's What's new open
//   --settings-window              with --host-sample: Settings open as a window over the lobby
//   --new TYPE                     the question editor on a new question of that type (mapPin: the map grid)
//   --buzzer                       the BUZZ! button alone (unlocked and locked)
//   --shot FILE [--after SEC]      save a screenshot after SEC seconds (default 2) and quit
//   --shots DIR                    save a screenshot of every new game moment into DIR

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using QuizNight.Net;
using QuizNight.Shared;
using QuizNight.Storage;
using QuizNight.Ui.Screens;

namespace QuizNight.Dev;

public sealed class DevTools
{
	readonly App _app;
	readonly string[] _args;
	/// <summary>True when the args decided what to show first (no home screen).</summary>
	public bool TakesOver { get; private set; }

	double _time;
	double? _shotAt;
	bool _mapZoomed;
	bool _rowDragged;
	int _introKeys;
	bool _undoChecked;
	bool _dropdownOpened;
	bool _scrolled;

	async void MakeSplash(string path)
	{
		await Ui.IntroSequence.Save(_app, path);
		_app.Quit(0);
	}

	async void SavePoster(string path)
	{
		var (_, csvs, from, until, label) = EternalTableScreen.Current();
		GD.Print("Poster: " + await EternalTableScreen.SavePicture(_app, csvs, from, until, label, path));
		_app.Quit(0);
	}

	static Ui.WowMapCanvas? FindWowCanvas(Node node) =>
		node as Ui.WowMapCanvas ?? node.GetChildren().Select(FindWowCanvas).FirstOrDefault(c => c != null);
	string? _shotPath;
	string? _shotsDir;
	string? _lastMoment;
	double _momentAt = -1;
	int _shotCount;

	bool _autopilot;
	int _players = 2;
	int _startAt;
	bool _quitAtEnd;
	string? _pilotKey;
	double _pilotSince;
	double? _finishedAt;

	Bot? _bot;

	DevTools(App app, string[] args)
	{
		_app = app;
		_args = args;
	}

	string? Value(string name)
	{
		var i = Array.IndexOf(_args, name);
		return i >= 0 && i + 1 < _args.Length ? _args[i + 1] : null;
	}

	bool Has(string name) => _args.Contains(name);

	/// <summary>The first node of that type under root (C# classes aren't found by Godot's FindChildren).</summary>
	static System.Collections.Generic.IEnumerable<T> FindAll<T>(Node root) where T : Node
	{
		if (root is T t) yield return t;
		foreach (var child in root.GetChildren())
			foreach (var found in FindAll<T>(child)) yield return found;
	}

	static T? Find<T>(Node root) where T : Node
	{
		foreach (var child in root.GetChildren())
			if ((child as T ?? Find<T>(child)) is { } found) return found;
		return null;
	}

	/// <summary>
	/// --sound FILE: the sample quiz's sound question plays that file. --video FILE: a video question with that
	/// file comes right after it. Both are read like the editor reads them.
	/// </summary>
	Quiz WithTestMedia(Quiz sample)
	{
		// (--sentence: the step by step question is one sentence in parts instead of hint cards)
		if (Has("--sentence") && sample.Questions.FirstOrDefault(q => q.IsSteps) is { } steps)
		{
			steps.StepsMode = StepsMode.Sentence;
			steps.Prompt = "Which city are we looking for?";
			steps.AnswerText = "Berlin";
			steps.Hints = new() { "This city, founded in 1237,", "was split by a wall from 1961 to 1989", "and is Germany's capital today.", "", "" };
		}
		var sq = sample.Questions.FirstOrDefault(q => q.Type == QuestionType.Sound);
		if (Value("--sound") is { } sound && sq != null)
		{
			sq.Media = Media.ReadFile(sound, QuestionType.Sound);
			sq.Prompt = "Name this song!";
			sq.AnswerText = System.IO.Path.GetFileNameWithoutExtension(sound);
			sq.BonusText = "Also name the game";
		}
		if (Value("--video") is { } video)
		{
			var vq = QuizRules.NewQuestion(QuestionType.Video, "qv");
			vq.Prompt = "Which game is this?";
			vq.AnswerText = System.IO.Path.GetFileNameWithoutExtension(video).Split(" - ")[0];
			vq.Media = Media.ReadFile(video, QuestionType.Video);
			sample.Questions.Insert(sq != null ? sample.Questions.IndexOf(sq) + 1 : 0, vq);
		}
		return sample;
	}

	static ScrollContainer? FindScroll(Node n)
	{
		if (n is ScrollContainer sc) return sc;
		foreach (var c in n.GetChildren())
			if (FindScroll(c) is { } found) return found;
		return null;
	}

	public static DevTools? FromArgs(App app, string[] args) => args.Length == 0 ? null : new DevTools(app, args).Setup();

	DevTools Setup()
	{
		_shotPath = Value("--shot");
		if (_shotPath != null) _shotAt = double.TryParse(Value("--after"), System.Globalization.CultureInfo.InvariantCulture, out var s) ? s : 2;
		_shotsDir = Value("--shots");
		if (_shotsDir != null) System.IO.Directory.CreateDirectory(_shotsDir);
		_autopilot = Has("--autopilot");
		_quitAtEnd = Has("--quit-at-end");
		if (int.TryParse(Value("--players"), out var n)) _players = n;
		if (int.TryParse(Value("--start-at"), out var first)) _startAt = first - 1;
		if (Value("--lang") is { } lang) Lang.Use(lang);
		// (the home screen as if that version were out: the update note and the green button)
		if (Value("--pretend-update") is { } newer) Ui.Screens.UpdatePrompt.Pretend(newer);
		// (the map grid as if that built-in map had come with this update: its NEW badge)
		if (Value("--pretend-new-map") is { } newMap) MapPresets.PretendNew = newMap;
		if (Value("--pretend-changed-map") is { } changedMap) MapPresets.PretendChanged = changedMap;
		if (Value("--pretend-version") is { } version) ClientSession.PretendVersion = version;
		// (made-up quiz nights for the statistics screens, this run only: --fake-results [N])
		if (Has("--fake-results")) Storage.Results.Fake = FakeResults.Make(int.TryParse(Value("--fake-results"), out var fake) ? fake : 30);
		// --overflow: after 3 s, prints how much the screen's scrolling area is taller than the window (0 = fits) and quits
		if (Has("--overflow"))
			_app.GetTree().CreateTimer(3).Timeout += () =>
			{
				var scroll = _app.CurrentScreen is { } screen ? FindScroll(screen) : null;
				var over = scroll == null ? 0 : scroll.GetVScrollBar().MaxValue - scroll.GetVScrollBar().Page;
				GD.Print($"OVERFLOW {Value("--open") ?? "screen"} {Math.Max(0, over):0}");
				_app.GetTree().Quit();
			};
		// --press TEXT[#N]: presses the first (or Nth) button whose text starts with TEXT, once, as soon as there is one
		// (e.g. "⋮" on a scoreboard, "▶#2" for the podium sound in Settings)
		if (Value("--press") is { } pressArg)
		{
			var timer = new Timer { WaitTime = 0.5, Autostart = true };
			timer.Timeout += () =>
			{
				var parts = pressArg.Split('#');
				var press = parts[0];
				var nth = parts.Length > 1 && int.TryParse(parts[1], out var n) ? n : 1;
				if (FindAll<Button>(_app).Where(b => b.IsVisibleInTree() && b.Text.StartsWith(press)).Skip(nth - 1).FirstOrDefault() is not { } button) return;
				button.EmitSignal(BaseButton.SignalName.Pressed);
				timer.QueueFree();
			};
			_app.AddChild(timer);
		}

		// (--make-splash FILE: the intro's first card as the boot splash picture, then quit; --splash: play the intro in this run)
		if (Has("--splash")) _app.AddChild(new Ui.IntroSequence(null) { StartAt = int.TryParse(Value("--intro-from"), out var from) ? from : 0 });
		// (--anti-cheat: the anti-cheat window's content over this run's window, to look at)
		if (Has("--anti-cheat")) _app.AddChild(new Ui.AntiCheatSplash(null));
		if (Value("--make-splash") is { } splashFile)
		{
			TakesOver = true;
			Callable.From(() => MakeSplash(splashFile)).CallDeferred();
		}
		// (--unzoned-mask FILE: the WoW map's land (zone art) that no Classic zone covers, red, the rest grey; to find
		// where new zones are, WowAtlas.ExtraZones)
		else if (Value("--unzoned-mask") is { } maskFile)
		{
			TakesOver = true;
			var art = Images.LoadBuiltinImage(Ui.WowMapArt.Overview(2))!;
			var mask = Image.CreateEmpty(WowAtlas.ImageWidth, WowAtlas.ImageHeight, false, Image.Format.Rgba8);
			for (var y = 0; y < WowAtlas.ImageHeight; y++)
			for (var x = 0; x < WowAtlas.ImageWidth; x++)
			{
				var land = art.GetPixel(x * 2, y * 2).A > 0.6f;
				var p = new NormPoint((x + 0.5) / WowAtlas.ImageWidth, (y + 0.5) / WowAtlas.ImageHeight);
				var zoned = WowAtlas.WorldToLocation(p) is { } loc && WowAtlas.ZoneAt(loc) != null;
				mask.SetPixel(x, y, !land ? new Color(0, 0, 0, 0) : zoned ? new Color(0.5f, 0.5f, 0.5f) : new Color(1, 0, 0));
			}
			mask.SavePng(maskFile);
			_app.GetTree().Quit();
		}
		else if (Has("--selftest"))
		{
			TakesOver = true;
			RunSelfTest();
		}
		else if (Has("--steam-check"))
		{
			TakesOver = true;
			SteamCheck();
		}
		else if (Has("--share-check"))
		{
			TakesOver = true;
			ShareCheck();
		}
		else if (Value("--bot") is { } name)
		{
			TakesOver = true;
			var address = Value("--address") ?? "127.0.0.1";
			_app.JoinRoom(NetMode.Lan, address, name, "bot-" + name.ToLowerInvariant());
			// --bot-drag (with a window): on its timeline turns the bot drags the item with the mouse, moves it once, then locks in.
			_bot = new Bot(_app, Has("--bot-drag"), _shotsDir != null ? n => Screenshot(System.IO.Path.Combine(_shotsDir, $"{++_shotCount:00}-{n}.png")) : null);
		}
		else if (Has("--host-sample") || Value("--host") != null)
		{
			TakesOver = true;
			var sample = WithTestMedia(SampleQuiz.Build());
			var quiz = Value("--host") is { } file ? QuizStore.Import(file) : QuizStore.Save(sample);
			_app.HostQuiz(quiz, NetMode.Lan);
			// --settings-window: Settings open over the lobby (or the game), for screenshots
			if (Has("--settings-window")) _app.GetTree().CreateTimer(1.5).Timeout += Ui.Screens.SettingsWindow.Open;
		}
		else if (Value("--open") is { } type)
		{
			TakesOver = true;
			var sample = WithTestMedia(SampleQuiz.Build());
			// --map-preset ID (with --open mapPin): the map pin question uses that built-in map instead of Skyrim
			if (Value("--map-preset") is { } presetId && MapPresets.Get(presetId) is { } preset && Images.BuiltinTexture(preset.ImagePath) is { } tex)
			{
				var map = new MapAsset { Id = "map-" + preset.Id, Name = preset.Name, PresetId = preset.Id, WidthKm = preset.WidthKm,
					Image = new ImageAsset { Width = tex.GetWidth(), Height = tex.GetHeight() } };
				sample.Maps.Add(map);
				var pin = sample.Questions.First(x => x.Type == QuestionType.MapPin && x.MapId == "map-skyrim");
				pin.MapId = map.Id;
				pin.Target = new NormPoint(0.5, 0.5);
			}
			// --map-grid (with --open mapPin): no map chosen yet, so the editor shows the map grid
			if (Has("--map-grid")) sample.Questions.First(x => x.Type == QuestionType.MapPin).MapId = null;
			var quiz = QuizStore.Save(sample);
			var q = quiz.Questions.FirstOrDefault(x => x.Type == type || (type == "choice" && x.Mode == ClassicMode.Choice) || (type == "multi" && x.IsMulti) || (type == "image" && x.Type == QuestionType.Image) || (type == "timeline" && x.IsTimeline) || (type == "wow" && x.MapId != null && quiz.MapById(x.MapId)?.PresetId == WowAtlas.PresetId)) ?? quiz.Questions[0];
			_app.GoQuestion(quiz.Id, q.Id, null);
		}
		else if (Has("--whats-new"))
		{
			// --whats-new: the home screen with this version's notes open
			TakesOver = true;
			_app.GoHome();
			Callable.From(() => Ui.Screens.WhatsNew.ShowCurrent(_app)).CallDeferred();
		}
		else if (Has("--buzzer"))
		{
			// --buzzer: the BUZZ! button on its own, unlocked and locked, for screenshots
			TakesOver = true;
			var row = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
			row.AddThemeConstantOverride("separation", 60);
			row.AddChild(new Ui.Screens.BuzzButton(() => { }));
			row.AddChild(new Ui.Screens.BuzzButton(() => { }, 160) { Disabled = true });
			var page = new CenterContainer();
			page.SetAnchorsPreset(Control.LayoutPreset.FullRect);
			page.AddChild(row);
			_app.Show(page, null);
		}
		else if (Value("--new") is { } newType)
		{
			// --new TYPE: the editor on a new question of that type (e.g. mapPin: the map grid)
			TakesOver = true;
			var quiz = QuizStore.Save(SampleQuiz.Build());
			_app.GoQuestion(quiz.Id, null, newType);
		}
		else if (Has("--video-guide"))
		{
			TakesOver = true;
			var quiz = QuizStore.Save(SampleQuiz.Build());
			_app.GoQuestion(quiz.Id, null, QuestionType.Video);
			var video = Value("--video-guide") is { } v && !v.StartsWith("--") ? v : null;
			Callable.From(() => Ui.Screens.VideoGuide.Show(_app.CurrentScreen!, video, _ => { })).CallDeferred();
		}
		else if (Value("--screen") is { } screen)
		{
			TakesOver = true;
			switch (screen)
			{
				case "quizzes":
					QuizStore.Save(SampleQuiz.Build());
					_app.GoQuizzes();
					break;
				case "quiz":
					_app.GoQuiz(QuizStore.Save(SampleQuiz.Build()).Id);
					break;
				case "settings":
					_app.GoSettings();
					break;
				case "testlab":
					_app.GoTestLab();
					break;
				case "credits":
					_app.GoHome();
					Ui.CreditsWindow.Open();
					break;
				case "credits-wall":
				{
					_app.GoHome();
					var bg = new ColorRect { Color = Ui.C.Bg };
					bg.SetAnchorsPreset(Control.LayoutPreset.FullRect);
					var center = new CenterContainer();
					center.SetAnchorsPreset(Control.LayoutPreset.FullRect);
					center.AddChild(new Ui.CreditsWall());
					_app.AddChild(bg);
					_app.AddChild(center);
					break;
				}
				case "eternal":
					// (--sort COL [--ascending]: the table sorted by that column)
					if (Value("--sort") is { } col) EternalTableScreen.SortBy(col, Has("--ascending"));
					// (--table all|season:N|beta, --ranking quiz|F1…, --kind type:mapPin[/skyrim]|cat:geography, --graph positions|points)
					EternalTableScreen.View(Value("--table"), Value("--ranking"), Value("--kind"), Value("--graph"));
					// (--poster FOLDER: save the award pack's pictures there, then quit)
					if (Value("--poster") is { } poster)
						Callable.From(() => SavePoster(poster)).CallDeferred();
					_app.GoEternalTable();
					break;
				case "history":
					_app.GoMatchHistory();
					break;
				// (the newest night on its own screen, with its graphs: --graph points for the other one)
				case "night":
					if (Storage.Results.Nights(Storage.Results.BundledCsvs()).FirstOrDefault() is { } newest)
					{
						MatchNightScreen.GraphKind = Value("--graph") ?? "positions";
						_app.GoMatchNight(newest);
					}
					break;
				// (--player ID, e.g. fake:Ann with --fake-results)
				case "profile":
					_app.GoProfile(Value("--player") ?? "fake:Ann");
					break;
				// (My quizzes with the mouse over the first trash can)
				case "trash-hover":
					_app.GoQuizzes();
					Callable.From(() =>
					{
						if (Find<Ui.TrashIcon>(_app)?.GetParent()?.GetParent() is Button trash) trash.EmitSignal(Control.SignalName.MouseEntered);
					}).CallDeferred();
					break;
				// (the delete question over My quizzes, to see it; it deletes nothing)
				case "delete":
					_app.GoQuizzes();
					Callable.From(() => Ui.Dialogs.Confirm(_app, Lang.F("Delete \"{0}\"? This can't be undone — export it first if you want a backup.", "New quiz"),
						"Delete", () => { }, dontAskAgain: () => { })).CallDeferred();
					break;
				// (four scoreboard rows; after 1.3 s the last one overtakes the other three: shoot at 1.5 to see it mid-slide)
				case "slide":
				{
					var list = Ui.U.V(6);
					var rows = new[] { "1st  Ann  5", "2nd  Ben  4", "3rd  Cleo  3", "4th  Dan  2" }.Select(t =>
					{
						var slide = new Ui.SlideRow();
						slide.AddChild(Ui.U.Panel(Ui.U.Text(t, 16), "Card2"));
						list.AddChild(slide);
						return slide;
					}).ToList();
					var page = new Control();
					page.SetAnchorsPreset(Control.LayoutPreset.FullRect);
					var center = new CenterContainer();
					center.SetAnchorsPreset(Control.LayoutPreset.FullRect);
					page.AddChild(center);
					list.CustomMinimumSize = new Vector2(300, 0);
					center.AddChild(list);
					_app.AddChild(page);
					_app.GetTree().CreateTimer(1.3).Timeout += () =>
					{
						list.MoveChild(rows[3], 0);
						// (as the real board: rebuilt with the new places, and how far each one moved)
						var after = new[] { "2nd  Ann  5  ▼1", "3rd  Ben  4  ▼1", "4th  Cleo  3  ▼1", "1st  Dan  6  ▲3" };
						for (var i = 0; i < 4; i++) ((Label)((Control)rows[i].GetChild(0)).GetChild(0)).Text = after[i];
						rows[3].SlideFrom(3);
						for (var i = 0; i < 3; i++) rows[i].SlideFrom(-1);
					};
					break;
				}
				default:
					_app.GoHome();
					break;
			}
		}
		return this;
	}

	async void RunSelfTest()
	{
		int code;
		try
		{
			code = await SelfTest.Run(_app);
		}
		catch (Exception e)
		{
			GD.PrintErr($"Self-test crashed: {e}");
			code = 2;
		}
		_app.Quit(code);
	}

	/// <summary>Uploads the sample quiz as a short share code and downloads it again (real GitHub repo; prints the code to delete).</summary>
	async void ShareCheck()
	{
		var sample = SampleQuiz.Build();
		try
		{
			var code = await QuizShare.Upload(sample);
			var back = await QuizShare.Download(ShareCode.ShortKey(code)!);
			var same = QuizJson.Write(back) == QuizJson.Write(ShareCode.Unpack(ShareCode.Pack(sample), out _)!);
			GD.Print($"Share check: {code} ({ShareCode.Pack(sample).Length / 1024} KB) {(same ? "came back the same" : "CAME BACK DIFFERENT")}");
			_app.Quit(same ? 0 : 1);
		}
		catch (Exception e)
		{
			GD.PrintErr($"Share check failed: {e.Message}");
			_app.Quit(1);
		}
	}

	/// <summary>Opens a Steam lobby, finds it again by its code, closes it. Needs Steam running.</summary>
	async void SteamCheck()
	{
		if (!SteamService.Available)
		{
			GD.PrintErr($"Steam check: not available ({SteamService.InitError})");
			_app.Quit(1);
			return;
		}
		GD.Print($"Steam check: signed in as {SteamService.PlayerName} ({SteamService.MySteamId})");
		var (transport, code, error) = await SteamHostTransport.Create(() => Server.Room.NewCode());
		if (transport == null)
		{
			GD.PrintErr($"Steam check: couldn't open a lobby: {error}");
			_app.Quit(1);
			return;
		}
		GD.Print($"Steam check: lobby open with code {code}");
		ulong? host = null;
		// Lobby search results can lag a moment behind creation.
		for (var i = 0; i < 5 && host == null; i++)
		{
			host = await SteamService.FindHost(code);
			if (host == null) await _app.ToSignal(_app.GetTree().CreateTimer(1), SceneTreeTimer.SignalName.Timeout);
		}
		GD.Print(host == SteamService.MySteamId ? $"Steam check: found the room by its code. OK" : $"Steam check: lookup failed (got {host})");
		transport.Dispose();
		_app.Quit(host == SteamService.MySteamId ? 0 : 1);
	}

	public void Process(double delta)
	{
		_time += delta;
		// --map-zoom Z [--map-at X,Y]: zoom the WoW map (for screenshots of its layers)
		// (--scroll PX: the screen's page scrolled down, to see what's below the window)
		if (_time >= 1 && !_scrolled && int.TryParse(Value("--scroll"), out var scroll) && Find<ScrollContainer>(_app) is { } page)
		{
			_scrolled = true;
			page.ScrollVertical = scroll;
		}
		if (_time >= 0.5 && !_mapZoomed && float.TryParse(Value("--map-zoom"), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var mapZoom))
		{
			_mapZoomed = true;
			if (FindWowCanvas(_app) is { } canvas)
			{
				var mapAt = Value("--map-at")?.Split(',');
				var center = mapAt is { Length: 2 } ? new NormPoint(double.Parse(mapAt[0], System.Globalization.CultureInfo.InvariantCulture), double.Parse(mapAt[1], System.Globalization.CultureInfo.InvariantCulture)) : canvas.Target ?? new NormPoint(0.5, 0.5);
				canvas.ZoomTo(center, mapZoom);
			}
		}
		// --intro-start (with --splash): Escape skips the cards at 1 s, a key starts from the title screen at 2.5 s
		if (Has("--intro-start") && _introKeys < 2 && _time >= (_introKeys == 0 ? 1 : 2.5))
		{
			_introKeys++;
			var key = new InputEventKey { Keycode = _introKeys == 1 ? Key.Escape : Key.Space, Pressed = true };
			_app.GetViewport().PushInput(key, true);
		}
		// --open-dropdown [N]: opens the screen's first (or Nth, from 1) dropdown at 1 s (to see its arrow and how it unfolds)
		if (_time >= 1 && !_dropdownOpened && Has("--open-dropdown"))
		{
			_dropdownOpened = true;
			var nth = int.TryParse(Value("--open-dropdown"), out var n) ? n - 1 : 0;
			Bot.All(_app).OfType<OptionButton>().Where(o => o.IsVisibleInTree()).Skip(nth).FirstOrDefault()?.ShowPopup();
		}
		if (_time >= 1.5 && !_undoChecked && Has("--undo-check"))
		{
			_undoChecked = true;
			UndoCheck();
		}
		// --drag-row (with --screen quiz): drags question 1 below question 3 with real mouse events and prints the new order
		if (_time >= 1 && !_rowDragged && Has("--drag-row"))
		{
			_rowDragged = true;
			DragRow();
		}
		if (_shotAt is { } at && _time >= at)
		{
			_shotAt = null;
			Screenshot(_shotPath!);
			_app.Quit();
			return;
		}
		if (_shotsDir != null) WatchMoments();
		if (_autopilot) Autopilot();
		_bot?.Process(_time);
	}

	async void DragRow()
	{
		var rows = Bot.All(_app).OfType<Ui.Screens.QuestionRowButton>().ToList();
		if (rows.Count < 3) return;
		var vp = _app.GetViewport();
		var tree = _app.GetTree();
		var a = rows[0].GetGlobalRect().GetCenter();
		var r = rows[2].GetGlobalRect();
		var b = new Vector2(r.GetCenter().X, r.Position.Y + r.Size.Y * 0.8f);
		vp.PushInput(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = true, Position = a, GlobalPosition = a, ButtonMask = MouseButtonMask.Left }, true);
		await _app.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
		for (var i = 1; i <= 15; i++)
		{
			var p = a.Lerp(b, i / 15f);
			vp.PushInput(new InputEventMouseMotion { Position = p, GlobalPosition = p, Relative = (b - a) / 15, ButtonMask = MouseButtonMask.Left }, true);
			await _app.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
		}
		await _app.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
		if (_shotPath != null) Screenshot(_shotPath.Replace(".png", "-drag.png"));
		vp.PushInput(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = false, Position = b, GlobalPosition = b }, true);
		for (var i = 0; i < 5; i++) await _app.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
		string Order() => string.Join(" | ", Bot.All(_app).OfType<Ui.Screens.QuestionRowButton>().Take(4).Select(row => Bot.All(row).OfType<Label>().First().Text));
		GD.Print("ORDER " + Order());
		// (and Ctrl+Z puts it back, Ctrl+Y moves it again)
		await Keys(Key.Z, false);
		GD.Print("UNDO " + Order());
		await Keys(Key.Y, false);
		GD.Print("REDO " + Order());
	}

	/// <summary>Ctrl + a key (with Shift if asked), as real key presses, then a few frames.</summary>
	async Task Keys(Key key, bool shift)
	{
		var vp = _app.GetViewport();
		vp.PushInput(new InputEventKey { Keycode = key, CtrlPressed = true, ShiftPressed = shift, Pressed = true }, true);
		vp.PushInput(new InputEventKey { Keycode = key, CtrlPressed = true, ShiftPressed = shift, Pressed = false }, true);
		for (var i = 0; i < 5; i++) await _app.ToSignal(_app.GetTree(), SceneTree.SignalName.ProcessFrame);
	}

	/// <summary>--undo-check (with --open classic): presses a + button twice, then Ctrl+Z, Ctrl+Y, Ctrl+Z, Ctrl+Shift+Z.</summary>
	async void UndoCheck()
	{
		if (_app.CurrentScreen is not Ui.Screens.QuestionEditorScreen editor) return;
		string Now() => QuizJson.WriteQuestion(((Ui.Screens.QuestionEditorScreen)_app.CurrentScreen!).Draft).ToJsonString();
		var before = Now();
		if (Bot.All(editor).OfType<Button>().FirstOrDefault(x => x.Text == "+" && x.IsVisibleInTree() && !x.Disabled) is not { } plus) return;
		plus.EmitSignal(BaseButton.SignalName.Pressed);
		for (var i = 0; i < 3; i++) await _app.ToSignal(_app.GetTree(), SceneTree.SignalName.ProcessFrame);
		plus.EmitSignal(BaseButton.SignalName.Pressed);
		var changed = Now();
		await Keys(Key.Z, false);
		var undone = Now();
		await Keys(Key.Y, false);
		var redone = Now();
		await Keys(Key.Z, false);
		await Keys(Key.Z, true);
		GD.Print($"UNDOCHECK changed={changed != before} undo-back={undone == before} redo-again={redone == changed} shiftZ-redo={Now() == changed}");
	}

	void Screenshot(string path)
	{
		var img = _app.GetViewport().GetTexture().GetImage();
		var err = img.SavePng(path);
		GD.Print(err == Error.Ok ? $"Screenshot: {path}" : $"Screenshot failed: {err}");
		ReportScrolling(_app, System.IO.Path.GetFileName(path));
	}

	/// <summary>Prints every visible scroll area whose content doesn't fit (the screen needs scrolling there).</summary>
	static void ReportScrolling(Node node, string where)
	{
		foreach (var child in node.GetChildren())
		{
			if (child is ScrollContainer { Visible: true } sc && sc.IsVisibleInTree() && sc.GetChildCount() > 0 && sc.GetChild(0) is Control content)
			{
				var need = content.GetCombinedMinimumSize().Y;
				if (need > sc.Size.Y + 2 && sc.VerticalScrollMode != ScrollContainer.ScrollMode.Disabled)
					GD.Print($"SCROLLS {where}: {sc.GetParent()?.GetType().Name}/{sc.Name} needs {need:0} of {sc.Size.Y:0} (+{need - sc.Size.Y:0})");
			}
			ReportScrolling(child, where);
		}
	}

	/// <summary>A screenshot whenever the screen shows something new (after it settles).</summary>
	void WatchMoments()
	{
		var state = _app.Host?.State ?? _app.Client?.State;
		var g = state?.Game;
		var moment = state == null
			? _app.CurrentScreen?.GetType().Name ?? ""
			: $"{state.Phase}-{g?.Index}-{g?.Stage}{(g is { Shown: false } ? "-hidden" : g is { Question.Buzzer: true, BuzzersOpen: false } ? "-locked" : "")}-{g?.Buzzes.Count}-{g?.Buzzes.Count(b => b.Status == BuzzStatus.Wrong)}-{g?.AnsweredIds.Count}-{state.Players.Count}{(g?.Timeline is { } tl ? $"-tl{tl.Placed.Count}{tl.TurnOrder.IndexOf(tl.TurnPlayerId ?? "")}{tl.WrongGaps.Count}{tl.Phase[0]}" : "")}{(g?.Reveal?.Entries.Count(e => e.Bonus) is > 0 and var bonuses ? $"-bonus{bonuses}" : "")}{(g is { Paused: true } ? "-paused" : "")}-on{state.Players.Count(p => p.Connected)}";
		if (moment != _lastMoment)
		{
			_lastMoment = moment;
			_momentAt = _time + 0.9;
		}
		if (_momentAt > 0 && _time >= _momentAt)
		{
			_momentAt = -1;
			Screenshot(System.IO.Path.Combine(_shotsDir!, $"{++_shotCount:00}-{moment}.png"));
		}
	}

	void Autopilot()
	{
		var host = _app.Host;
		if (host == null) return;
		var s = host.State;
		var g = s.Game;
		var answering = g?.Buzzes.FirstOrDefault(b => b.Status == BuzzStatus.Answering);
		var key = $"{s.Phase}|{g?.Index}|{g?.Stage}|{g?.Shown}|{g?.BuzzersOpen}|{answering?.PlayerId}|{g?.Buzzes.Count}|{s.Players.Count(p => p.Connected)}|{g?.Timeline?.Placed.Count}|{g?.Timeline?.TurnPlayerId}|{g?.Timeline?.WrongGaps.Count}|{g?.Timeline?.Phase}|{g?.Question.Slide}|{g?.Question.Hints?.Count}|{g?.Interactive?.Phase}|{g?.Interactive?.Clip}|{g?.Interactive?.Plays}|{g?.Interactive?.DoneIds.Count}";
		if (key != _pilotKey)
		{
			_pilotKey = key;
			_pilotSince = _time;
		}
		var waited = _time - _pilotSince;
		switch (s.Phase)
		{
			case Phase.Lobby when s.Players.Count(p => p.Connected) >= _players && waited > 1.5:
				host.Start();
				break;
			// Someone left: wait a little for them, then go on without them.
			case Phase.Playing when g!.Paused:
				if (waited > 8) host.Continue();
				break;
			case Phase.Playing when g!.Index < _startAt:
				host.Next();
				break;
			case Phase.Playing when g!.Stage == Stage.Open:
				if (!g.Shown)
				{
					if (waited > 1.2) host.ShowQuestion();
					break;
				}
				// Sound and video: start it once every player has the file (or after a while), like a host would.
				if (g.Question.IsMedia && !g.MediaStarted && Find<Ui.Screens.HostMedia>(_app) is { } media)
				{
					var everyone = g.Question.MediaId is not { } mid || host.Room.Players.Where(p => p.Connected).All(p => host.MediaPercent(p.Id, mid) >= 100);
					if (waited > 1.5 && (everyone || waited > 30)) media.StartNow();
					break;
				}
				// A picture slideshow: flip to the next picture now and then, like a host showing them one by one.
				if (g.Question is { SlideCount: > 1, Slide: { } slide } && slide < g.Question.SlideCount - 1 && answering == null)
				{
					if (waited > 1.5) host.SetSlide(slide + 1);
					break;
				}
				// Interactive: wait for the recordings, play each one, then let everyone vote (or pick the first as favourite).
				if (g.Interactive is { } ia)
				{
					if (ia.Phase == InteractivePhase.Listening && waited > 2)
					{
						if (ia.Plays <= ia.Clip) host.PlayClip();
						else if (ia.Clip < ia.Clips.Count - 1) host.SetClip(ia.Clip + 1);
						else if (ia.Judging == Judging.Host) host.PickFavourite(ia.Clips[0].PlayerId);
						else host.StartVoting();
					}
					else if (ia.Phase == InteractivePhase.Voting && waited > 12) host.Reveal();
					break;
				}
				if (g.Question.Buzzer && !g.BuzzersOpen && answering == null)
				{
					if (waited > 1.2) host.SetBuzzers(true);
					break;
				}
				// Step by step: flip the next hint when nobody is answering for a while.
				if (g.Question is { HintCount: { } hints } && (g.Question.Hints?.Count ?? 0) < hints && answering == null)
				{
					if (waited > 3) host.NextHint();
					break;
				}
				// Timeline: reveal the locked-in spot, then go on to the next turn.
				if (g.Timeline?.Phase == TimelinePhase.Locked)
				{
					if (waited > 1.5) host.RevealPlacement();
					break;
				}
				if (g.Timeline?.Phase == TimelinePhase.Result)
				{
					if (waited > 1.5) host.NextTurn();
					break;
				}
				if (answering != null && waited > 1.5)
				{
					// The first buzz of a question is wrong when someone else is waiting, to show the queue.
					var first = g.Buzzes[0].PlayerId == answering.PlayerId && g.Buzzes.Count > 1;
					host.Judge(!first);
				}
				else if (answering == null && waited > 9) host.Reveal();
				break;
			// A question with a bonus: give it to the winner (or the first answer), to show it.
			case Phase.Playing when g!.Stage == Stage.Reveal && g.Question.BonusPoints > 0 && g.Reveal is { Entries.Count: > 0 } rv && !rv.Entries.Any(e => e.Bonus):
				if (waited > 1) host.ToggleBonus(rv.WinnerIds.FirstOrDefault() ?? rv.Entries[0].PlayerId);
				break;
			case Phase.Playing when waited > 3:
				host.Next();
				break;
			case Phase.Finished:
				_finishedAt ??= _time;
				if (_quitAtEnd && _time - _finishedAt > 3) _app.Quit();
				break;
		}
	}

	/// <summary>A pretend player: buzzes and answers after a short random wait.</summary>
	sealed class Bot(App app, bool drag = false, Action<string>? shot = null)
	{
		readonly Random _rng = new();
		string? _handled;
		double _actAt = -1;
		Action? _action;
		bool _quitting;

		public void Process(double now)
		{
			var client = app.Client;
			if (client == null)
			{
				// Kicked or the room closed: a bot has nothing else to do.
				if (!_quitting && now > 3)
				{
					_quitting = true;
					app.Quit();
				}
				return;
			}
			var g = client.State?.Game;
			// Jokers: a bot plays a Fast Pass on the first buzzer question it can, and Double points on the third question.
			if (client.State?.Phase == Phase.Playing && g?.JokerIndex is { } jokerAt && client.State.Settings.Jokers.Count > 0)
			{
				bool Has(string kind) => g.Jokers.Any(j => j.PlayerId == client.MyId && j.Kind == kind);
				if (g.JokerBuzzer && !g.FastPassTaken && !Has(Joker.FastPass) && client.State.Settings.Jokers.Contains(Joker.FastPass)) client.PlayJoker(Joker.FastPass, true);
				if (jokerAt == 2 && !Has(Joker.Double) && client.State.Settings.Jokers.Contains(Joker.Double)) client.PlayJoker(Joker.Double, true);
			}
			// Timeline: on its turn, a bot picks a gap nobody has tried yet.
			if (client.State?.Phase == Phase.Playing && g is { Stage: Stage.Open, Timeline: { } tl } && tl.TurnPlayerId == client.MyId && tl.Current != null && tl.Phase == TimelinePhase.Choosing)
			{
				var turnKey = $"{g.Index}|tl|{tl.Placed.Count}|{tl.WrongGaps.Count}";
				if (_handled != turnKey)
				{
					_handled = turnKey;
					_actAt = now + 1.2 + _rng.NextDouble() * 2;
					var gaps = Enumerable.Range(0, tl.Placed.Count + 1).Where(i => !tl.WrongGaps.Contains(i)).ToList();
					var gap = gaps[_rng.Next(gaps.Count)];
					var index = g.Index;
					_action = drag ? () => DragAndLockIn(gaps[0], gap) : () => client.Answer(index, AnswerValue.Number(gap), _ => { });
				}
			}
			// Interactive: a bot sends a beep as its recording, then votes for someone else's.
			else if (client.State?.Phase == Phase.Playing && g is { Stage: Stage.Open, Shown: true, Interactive: { } ia })
			{
				var iaKey = $"{g.Index}|ia|{ia.Phase}";
				if (_handled != iaKey)
				{
					_handled = iaKey;
					_actAt = now + 1 + _rng.NextDouble() * 2;
					var index = g.Index;
					var others = Enumerable.Range(0, ia.Clips.Count).Where(i => ia.Clips[i].PlayerId != client.MyId).ToList();
					_action = ia.Phase == InteractivePhase.Recording ? () => client.SendRecording(index, BotRecording(1 + _rng.NextDouble() * 2), _ => { })
						: ia.Phase == InteractivePhase.Voting && others.Count > 0 ? () => client.Answer(index, AnswerValue.Number(others[_rng.Next(others.Count)]), _ => { })
						: null;
				}
			}
			// (step by step: every new hint is another chance to buzz)
			else if (client.State?.Phase == Phase.Playing && g is { Stage: Stage.Open, Shown: true, Timeline: null } && (!g.Question.Buzzer || g.BuzzersOpen) && _handled != $"{g.Index}|{g.Question.Hints?.Count}")
			{
				_handled = $"{g.Index}|{g.Question.Hints?.Count}";
				_actAt = now + 0.8 + _rng.NextDouble() * 2.5;
				var q = g.Question;
				var index = g.Index;
				_action = q.Buzzer ? client.Buzz
					: q.Type == QuestionType.Order ? () => client.Answer(index, AnswerValue.Picked(Enumerable.Range(0, q.Items!.Count).OrderBy(_ => _rng.Next())), _ => { })
					: q.Written ? () => client.Answer(index, AnswerValue.Written(new[] { "Canberra", "canbera!", "Sydney", "a beep", "Mars", "Dragonborn", "dragon born" }[_rng.Next(7)]), _ => { })
					: q.Mode == ClassicMode.Multi ? () => client.Answer(index, AnswerValue.Picked(new[] { RandomChoice(q), RandomChoice(q) }), _ => { })
					: q.Choices != null ? () => client.Answer(index, AnswerValue.Number(RandomChoice(q)), _ => { })
					: q.Type == QuestionType.Estimation ? () => client.Answer(index, AnswerValue.Number(_rng.Next(60, 120)), _ => { })
					: () => client.Answer(index, AnswerValue.At(new NormPoint(0.3 + _rng.NextDouble() * 0.4, 0.3 + _rng.NextDouble() * 0.4)), _ => { });
			}
			if (_action != null && _actAt >= 0 && now >= _actAt)
			{
				_actAt = -1;
				var a = _action;
				_action = null;
				a();
			}
		}

		/// <summary>Drags the item from the tray into gap <paramref name="first"/>, then on to <paramref name="last"/>, and locks in — with real mouse events.</summary>
		async void DragAndLockIn(int first, int last)
		{
			var tree = app.GetTree();
			async Task Frames(int n)
			{
				for (var i = 0; i < n; i++) await app.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
			}
			// The gaps in timeline order; the gap holding the dropped item is that card.
			List<Control> Gaps() => All(app).Where(c => c is Ui.Screens.TimelineGap || c is Ui.Screens.TimelineDragCard { Placed: true }).ToList();
			async Task Drag(Control from, Control to, string name)
			{
				var vp = app.GetViewport();
				Vector2 a = from.GetGlobalRect().GetCenter(), b = to.GetGlobalRect().GetCenter();
				vp.PushInput(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = true, Position = a, GlobalPosition = a, ButtonMask = MouseButtonMask.Left }, true);
				await Frames(2);
				for (var i = 1; i <= 15; i++)
				{
					var p = a.Lerp(b, i / 15f);
					vp.PushInput(new InputEventMouseMotion { Position = p, GlobalPosition = p, Relative = (b - a) / 15, ButtonMask = MouseButtonMask.Left }, true);
					await Frames(1);
				}
				await Frames(3);
				shot?.Invoke(name);
				vp.PushInput(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = false, Position = b, GlobalPosition = b }, true);
				await Frames(5);
			}
			await Frames(2);
			if (All(app).OfType<Ui.Screens.TimelineDragCard>().FirstOrDefault(c => !c.Placed) is not { } tray) return;
			await Drag(tray, Gaps()[first], "drag-into-timeline");
			shot?.Invoke("dropped");
			if (last != first && All(app).OfType<Ui.Screens.TimelineDragCard>().FirstOrDefault(c => c.Placed) is { } placed)
			{
				await Drag(placed, Gaps()[last], "drag-to-another-gap");
				shot?.Invoke("moved");
			}
			if (All(app).OfType<Button>().FirstOrDefault(b => b.Text == Lang.T("Lock in") && !b.Disabled) is { } lockIn)
				lockIn.EmitSignal(BaseButton.SignalName.Pressed);
		}

		/// <summary>A beep as the game records sound (16 kHz mono, Ui.MicRecorder): the host takes no other kind.</summary>
		static byte[] BotRecording(double seconds) =>
			Ui.MicRecorder.Wav(Enumerable.Range(0, (int)(16000 * seconds)).Select(i => 0.3f * MathF.Sin(i * 0.17f)).ToArray(), 16000);

		internal static IEnumerable<Control> All(Node n)
		{
			foreach (var child in n.GetChildren())
			{
				if (child is Control { Visible: true } c) yield return c;
				foreach (var d in All(child)) yield return d;
			}
		}

		int RandomChoice(PublicQuestion q)
		{
			var options = Enumerable.Range(0, q.Choices!.Count).Where(i => q.Choices[i] != null).ToList();
			return options[_rng.Next(options.Count)];
		}
	}
}
