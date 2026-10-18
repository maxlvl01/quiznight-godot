// Command-line helpers for testing on one PC (arguments after "--"):
//   --selftest                     run the checks and quit (exit code 0 = all passed)
//   --host-sample | --host FILE    host the sample quiz / a quiz file in LAN mode
//   --autopilot [--players N]      as host: start with N players, judge buzzes, go to the next question
//   --start-at N                   with --autopilot: skip straight to question N (1 = first)
//   --quit-at-end                  quit a few seconds after the podium
//   --bot NAME [--address IP]      join a LAN room and answer on its own
//   --open TYPE                    open the editor on the sample quiz's question of that type
//   --screen home|quizzes|quiz|settings  open that screen
//   --lang en|de                   use that language this run (not saved)
//   --video-guide [FILE]           open a new video question with the MP4 → .ogv guide
//   --shot FILE [--after SEC]      save a screenshot after SEC seconds (default 2) and quit
//   --shots DIR                    save a screenshot of every new game moment into DIR

using System;
using System.Linq;
using Godot;
using QuizNight.Net;
using QuizNight.Shared;
using QuizNight.Storage;

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

		if (Has("--selftest"))
		{
			TakesOver = true;
			RunSelfTest();
		}
		else if (Has("--steam-check"))
		{
			TakesOver = true;
			SteamCheck();
		}
		else if (Value("--bot") is { } name)
		{
			TakesOver = true;
			var address = Value("--address") ?? "127.0.0.1";
			_app.JoinRoom(NetMode.Lan, address, name, "bot-" + name.ToLowerInvariant());
			_bot = new Bot(_app);
		}
		else if (Has("--host-sample") || Value("--host") != null)
		{
			TakesOver = true;
			var quiz = Value("--host") is { } file ? QuizStore.Import(file) : QuizStore.Save(SampleQuiz.Build());
			_app.HostQuiz(quiz, NetMode.Lan);
		}
		else if (Value("--open") is { } type)
		{
			TakesOver = true;
			var sample = SampleQuiz.Build();
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
			var quiz = QuizStore.Save(sample);
			var q = quiz.Questions.FirstOrDefault(x => x.Type == type || (type == "choice" && x.Mode == ClassicMode.Choice) || (type == "multi" && x.IsMulti) || (type == "image" && x.Type == QuestionType.Image) || (type == "timeline" && x.IsTimeline) || (type == "wow" && x.MapId != null && quiz.MapById(x.MapId)?.PresetId == WowAtlas.PresetId)) ?? quiz.Questions[0];
			_app.GoQuestion(quiz.Id, q.Id, null);
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
			: $"{state.Phase}-{g?.Index}-{g?.Stage}{(g is { Shown: false } ? "-hidden" : g is { Question.Buzzer: true, BuzzersOpen: false } ? "-locked" : "")}-{g?.Buzzes.Count}-{g?.Buzzes.Count(b => b.Status == BuzzStatus.Wrong)}-{g?.AnsweredIds.Count}-{state.Players.Count}{(g?.Timeline is { } tl ? $"-tl{tl.Placed.Count}{tl.TurnOrder.IndexOf(tl.TurnPlayerId ?? "")}{tl.WrongGaps.Count}{tl.Phase[0]}" : "")}";
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
		var key = $"{s.Phase}|{g?.Index}|{g?.Stage}|{g?.Shown}|{g?.BuzzersOpen}|{answering?.PlayerId}|{g?.Buzzes.Count}|{s.Players.Count(p => p.Connected)}|{g?.Timeline?.Placed.Count}|{g?.Timeline?.TurnPlayerId}|{g?.Timeline?.WrongGaps.Count}|{g?.Timeline?.Phase}";
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
			case Phase.Playing when g!.Index < _startAt:
				host.Next();
				break;
			case Phase.Playing when g!.Stage == Stage.Open:
				if (!g.Shown)
				{
					if (waited > 1.2) host.ShowQuestion();
					break;
				}
				if (g.Question.Buzzer && !g.BuzzersOpen && answering == null)
				{
					if (waited > 1.2) host.SetBuzzers(true);
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
	sealed class Bot(App app)
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
					_action = () => client.Answer(index, AnswerValue.Number(gap), _ => { });
				}
			}
			else if (client.State?.Phase == Phase.Playing && g is { Stage: Stage.Open, Shown: true, Timeline: null } && (!g.Question.Buzzer || g.BuzzersOpen) && _handled != $"{g.Index}")
			{
				_handled = $"{g.Index}";
				_actAt = now + 0.8 + _rng.NextDouble() * 2.5;
				var q = g.Question;
				var index = g.Index;
				_action = q.Buzzer ? client.Buzz
					: q.Type == QuestionType.Order ? () => client.Answer(index, AnswerValue.Picked(Enumerable.Range(0, q.Items!.Count).OrderBy(_ => _rng.Next())), _ => { })
					: q.Type == QuestionType.Write ? () => client.Answer(index, AnswerValue.Written(new[] { "Canberra", "canbera!", "Sydney" }[_rng.Next(3)]), _ => { })
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

		int RandomChoice(PublicQuestion q)
		{
			var options = Enumerable.Range(0, q.Choices!.Count).Where(i => q.Choices[i] != null).ToList();
			return options[_rng.Next(options.Count)];
		}
	}
}
