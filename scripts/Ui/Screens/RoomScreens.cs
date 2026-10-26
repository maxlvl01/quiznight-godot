using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using QuizNight.Net;
using QuizNight.Server;
using QuizNight.Shared;

namespace QuizNight.Ui.Screens;

/// <summary>A screen that shows a room and redraws when its state changes.</summary>
public interface IRoomScreen
{
	void Refresh(RoomState state);
}

public partial class MessageScreen : Control
{
	public MessageScreen() : this("") { }

	/// <summary>What Escape does here: the button's job (e.g. Cancel), or nothing.</summary>
	public Action? Escape;

	public MessageScreen(string text, string? button = null, Action? onButton = null)
	{
		SetAnchorsPreset(LayoutPreset.FullRect);
		var box = U.V(16, U.Text(text, 18, C.Muted, null, false, HorizontalAlignment.Center));
		if (button != null) box.AddChild(U.Btn(button, "", onButton).Center());
		Escape = onButton;
		var center = new CenterContainer();
		center.SetAnchorsPreset(LayoutPreset.FullRect);
		center.AddChild(box);
		AddChild(center);
	}
}

public partial class HostLobbyScreen : Control, IRoomScreen
{
	readonly HostSession _host;
	readonly VBoxContainer _content = U.V(12);
	string? _error;
	// (the host was told some players' games are too old and starts anyway)
	bool _versionsOk;

	public HostLobbyScreen() : this(null!) { }

	public HostLobbyScreen(HostSession host)
	{
		_host = host;
		SetAnchorsPreset(LayoutPreset.FullRect);
		AddChild(U.Page(U.Panel(_content).With(Seasonal.SnowOn), 1000, 16));
	}

	/// <summary>
	/// Laid out like the players' waiting room (the code, a seat for everyone plus an open one), with the host's options
	/// in two columns underneath, so it fits without scrolling.
	/// </summary>
	public void Refresh(RoomState room)
	{
		_content.Clear();
		var connected = room.Players.Count(p => p.Connected);
		_content.AddChild(U.H(8, U.Heading("Lobby", 28, false).Grow(), new RoomCodeButton(room.Code, 32),
			U.Pill(U.Plural(room.Players.Count, "player", "players")), MediaIcon.SettingsGear()));
		_content.AddChild(U.Muted($"{room.Quiz.Title} · {U.Plural(room.Quiz.QuestionCount, "question", "questions")}", 15).Raw());

		if (_host.Mode == NetMode.Steam)
			_content.AddChild(U.Muted(Lang.F("Players open Quiznight, choose Steam on the home screen and enter this code. {0} here so far (up to {1}).", room.Players.Count, Limits.MaxPlayers), 13));
		else
		{
			var addresses = LanHostTransport.LanAddresses();
			var where = addresses.Count > 0 ? string.Join(Lang.T("  or  "), addresses) : Lang.T("this PC's network address");
			_content.AddChild(U.Muted(Lang.F("LAN mode: players choose \"LAN / same PC\" and enter {0} (same Wi-Fi), or 127.0.0.1 on this PC. Port {1}. {2} here so far (up to {3}).", where, _host.LanPort, room.Players.Count, Limits.MaxPlayers), 13));
		}

		// Seats: everyone who's in (× removes them), plus one open seat while the room isn't full.
		var grid = new GridContainer { Columns = 6 };
		grid.AddThemeConstantOverride("h_separation", 10);
		grid.AddThemeConstantOverride("v_separation", 10);
		var tags = U.AnyClanTag(room.Players.Select(p => p.Name));
		foreach (var p in room.Players)
		{
			var id = p.Id;
			var seat = U.V(4, new Avatar(p, 44).Center(), U.PlayerName(p.Name, 15, HorizontalAlignment.Center, tags),
				U.H(4, GameParts.SeatStatus(p, room, false).With(s => s.SizeFlagsVertical = SizeFlags.ShrinkCenter),
					U.Link("×", () => _host.Kick(id), 15).Tip(Lang.F("Remove {0}", p.Name)).With(b => b.SizeFlagsVertical = SizeFlags.ShrinkCenter)).Center());
			// (an older game than the host's: which version, so the host can ask them to update)
			if (VersionCheck.Older(p.Version, App.Version))
				seat.AddChild(U.Text(p.Version != "" ? Lang.F("⚠ {0}", p.Version) : Lang.T("⚠ older version"), 12, C.Orange)
					.With(l => { l.HorizontalAlignment = HorizontalAlignment.Center; l.MouseFilter = MouseFilterEnum.Stop; })
					.Tip(Lang.F("An older Quiznight than yours ({0}). Ask them to update.", App.Version)));
			grid.AddChild(U.Panel(seat, "Card2").Grow());
		}
		for (var i = room.Players.Count; i < Math.Min(room.Players.Count + 1, Limits.MaxPlayers); i++)
		{
			var empty = U.V(4, U.Panel(U.Gap(34), "Clear").With(p => p.AddThemeStyleboxOverride("panel", UiTheme.Box(Colors.Transparent, 999, C.Border, 2, 4, 4))).Center(),
				U.Muted(room.Players.Count == 0 ? "Send the code to your friends" : "Open seat", 12, true, HorizontalAlignment.Center));
			grid.AddChild(U.Panel(empty, "Card2").With(p => p.Modulate = new Color(1, 1, 1, 0.5f)).Grow());
		}
		_content.AddChild(grid);
		// (the options sit at the bottom of the screen; the seats grow into the space above them)
		_content.AddChild(new Control { SizeFlagsVertical = SizeFlags.ExpandFill });

		// The host's options: rules on the left, time and sounds on the right (details on hover).
		var left = U.V(2,
			U.Muted("How you run it", 13),
			Rule("I show each question myself", "It stays hidden until you press Show (Space).", room.Settings.HostShowsQuestions,
				on => _host.UpdateSettings(hostShows: on)),
			Rule("I unlock the buzzers myself", "After reading the question out (Space).", room.Settings.HostUnlocksBuzzers,
				on => _host.UpdateSettings(hostUnlocks: on)),
			Rule("Only my Steam friends can join", "Over Steam: players who aren't on your Steam friends list can't come in, even with the code.",
				room.Settings.FriendsOnly, on => _host.UpdateSettings(friendsOnly: on)),
			Rule("Show question categories", "Players see each question's category (e.g. Geography) on the Get ready screen. It makes questions easier; secret ones stay hidden.",
				room.Settings.ShowCategories, on => _host.UpdateSettings(categories: on)),
			U.Muted("Jokers (once per player and match)", 13).With(l => l.TooltipText = Lang.T("Played before a question starts.")),
			Rule("×2 Double points", "Whatever the next question gives counts twice. Anyone can play it.", room.Settings.Jokers.Contains(Joker.Double),
				on => _host.UpdateSettings(joker: (Joker.Double, on))),
			Rule("Fast Pass", "On the next buzzer question, you answer first, even if someone is already answering. One player per question.", room.Settings.Jokers.Contains(Joker.FastPass),
				on => _host.UpdateSettings(joker: (Joker.FastPass, on))).With(b => b.Icon = Bolt.Icon(15)));
		var right = U.V(8,
			Stepper.Make("Answer time per buzz", room.Settings.AnswerTimeSec, Limits.AnswerTimeMin, Limits.AnswerTimeMax,
				v => _host.UpdateSettings(answerTimeSec: v), " s"),
			BuzzerPicker.Make(_host.BuzzerSound, id => _host.BuzzerSound = id, false),
			PodiumPicker.Make(_host.PodiumSound, id => _host.PodiumSound = id, true));
		_content.AddChild(U.Panel(U.V(8, U.Eyebrow("Game rules for tonight"), U.H(24, left.Grow(), right.Grow())), "Inset"));

		// What players on older games can't play in this quiz.
		var cant = room.Players.Where(p => p.Connected)
			.Select(p => (p.Name, Missing: VersionCheck.Missing(_host.Room.Quiz, p.Version))).Where(c => c.Missing.Count > 0).ToList();
		foreach (var (name, missing) in cant)
			_content.AddChild(U.Text(Lang.F("⚠ {0}'s game is too old for {1}. Ask them to update.", name, string.Join(", ", missing)), 13, C.Orange, wrap: true));

		if (_error != null) _content.AddChild(U.Error(_error));
		void StartQuiz()
		{
			if (cant.Count > 0 && !_versionsOk)
			{
				Dialogs.Confirm(this, Lang.F("{0} can't play every question on their version (see the lobby). Start anyway?", string.Join(", ", cant.Select(c => c.Name))),
					"Start anyway", () => { _versionsOk = true; StartQuiz(); });
				return;
			}
			var result = _host.Start();
			_error = result.Ok ? null : result.Error;
			if (!result.Ok) Refresh(_host.State);
		}
		var start = U.Btn("Start quiz", "AccentButton", StartQuiz);
		// Everyone gets the quiz's pictures, sounds and videos first, so nothing has to load mid-question.
		var loading = room.Players.Where(p => p.Connected && room.Downloads.GetValueOrDefault(p.Id, 100) < 100).ToList();
		start.Disabled = connected < Limits.MinPlayersToStart || loading.Count > 0;
		Control needs = connected < Limits.MinPlayersToStart ? U.Muted(Lang.F("Needs at least {0} players.", Limits.MinPlayersToStart), 13, false)
			: loading.Count > 0 ? U.H(10,
				U.Muted(loading.Count == 1 ? Lang.F("{0} is still downloading the quiz…", loading[0].Name) : Lang.F("{0} players are still downloading the quiz…", loading.Count), 13, false),
				U.Link("Start anyway", StartQuiz, 13).Tip("Whoever isn't done gets each question's pictures when it comes up"))
			: U.Fill();
		_content.AddChild(U.H(12, U.Link("Close room", () => Dialogs.Confirm(this, "Close the room? Everyone in it is sent back to the home screen.", "Close room", App.I.CloseRoom)),
			U.Fill(), needs, start));
	}

	static CheckBox Rule(string text, string tip, bool on, Action<bool> changed)
	{
		var box = new CheckBox { Text = text, TooltipText = tip, ButtonPressed = on, MouseDefaultCursorShape = CursorShape.PointingHand };
		box.Toggled += v => changed(v);
		return box;
	}
}

public partial class WaitingRoomScreen : Control, IRoomScreen
{
	readonly ClientSession _client;
	readonly VBoxContainer _content = U.V(14);

	public WaitingRoomScreen() : this(null!) { }

	public WaitingRoomScreen(ClientSession client)
	{
		_client = client;
		SetAnchorsPreset(LayoutPreset.FullRect);
		AddChild(U.Page(U.Panel(_content).With(Seasonal.SnowOn), 1000, 16));
	}

	/// <summary>While you change your name: the field (kept across updates, so typing isn't lost).</summary>
	LineEdit? _rename;
	string? _renameError;

	public void Refresh(RoomState room)
	{
		_rename?.GetParent()?.RemoveChild(_rename);
		_content.Clear();
		_content.AddChild(U.H(8, U.Heading("Waiting room", 28, false).Grow(),
			_client.Mode == NetMode.Steam ? new RoomCodeButton(room.Code) : U.Pill(Lang.F("Host {0}", _client.Target)),
			U.Pill(U.Plural(room.Players.Count, "player", "players")), MediaIcon.SettingsGear()));
		_content.AddChild(U.Muted(room.Quiz.Title, 15).Raw());

		var grid = new GridContainer { Columns = 4 };
		grid.AddThemeConstantOverride("h_separation", 12);
		grid.AddThemeConstantOverride("v_separation", 12);
		var tags = U.AnyClanTag(room.Players.Select(p => p.Name));
		foreach (var p in room.Players)
		{
			var me = p.Id == _client.MyId;
			var seat = U.V(6, new Avatar(p, 56).Center(), me ? MyName(p, room, tags) : U.PlayerName(p.Name, 16, HorizontalAlignment.Center, tags),
				me && room.Downloads.GetValueOrDefault(p.Id, 100) >= 100 ? U.Muted("You · double-click your name to change it", 13, true, HorizontalAlignment.Center)
					: GameParts.SeatStatus(p, room, true).Center());
			if (me && _renameError != null) seat.AddChild(U.Text(_renameError, 12, C.Coral, null, true, HorizontalAlignment.Center));
			grid.AddChild(U.Panel(seat, me ? "Scored" : "Card2").Grow());
		}
		// Everyone who's in, plus one open seat (while the room isn't full): the grid grows as people join.
		for (var i = room.Players.Count; i < Math.Min(room.Players.Count + 1, Limits.MaxPlayers); i++)
		{
			var empty = U.V(6, U.Panel(U.Gap(44), "Clear").With(p => p.AddThemeStyleboxOverride("panel", UiTheme.Box(Colors.Transparent, 999, C.Border, 2, 4, 4))).Center(),
				U.Muted("Open seat", 13, false, HorizontalAlignment.Center));
			grid.AddChild(U.Panel(empty, "Card2").With(p => p.Modulate = new Color(1, 1, 1, 0.5f)).Grow());
		}
		_content.AddChild(grid);
		_content.AddChild(new Control { SizeFlagsVertical = SizeFlags.ExpandFill });
		_content.AddChild(Rules(room.Settings));
		_content.AddChild(U.H(16,
			U.Muted("Waiting for the host to start the quiz.", 14, false).Grow(),
			U.Link("Leave", App.I.LeaveRoom)));
	}

	/// <summary>Tonight's rules as the host set them (read only): how the host runs it, and the jokers you can play.</summary>
	static Control Rules(RoomSettings settings)
	{
		Control Line(string text) => U.Text(text, 15);
		var general = U.V(6,
			Line(settings.HostShowsQuestions ? "• The host shows each question when ready" : "• Questions show right away"),
			Line(settings.HostUnlocksBuzzers ? "• The host unlocks the buzzers after reading out" : "• Buzzers open as soon as a question shows"),
			Line(settings.ShowCategories ? "• You see each question's category" : "• Categories stay hidden"),
			Line(Lang.F("• Answer time per buzz: {0} s", settings.AnswerTimeSec)));
		var jokers = U.V(6, U.Muted("Jokers (once each per match)", 13));
		// (every joker stays listed, greyed out when the host turns it off, so the panel doesn't jump in height)
		foreach (var kind in Joker.All)
		{
			var on = settings.Jokers.Contains(kind);
			var row = U.H(8, Bolt.JokerPill(kind, on ? C.Accent : C.Muted), U.Muted(!on ? Lang.T("Off tonight.") : kind == Joker.Double
				? Lang.T("The next question counts twice. Anyone can play it.")
				: Lang.T("You answer first, even if someone is already answering. One player per question."), 13).Grow());
			if (!on) row.Modulate = new Color(1, 1, 1, 0.45f);
			jokers.AddChild(row);
		}
		return U.Panel(U.V(8, U.Eyebrow("Tonight's rules"), U.H(24, general.Grow(), jokers.Grow())), "Inset");
	}
}

public partial class WaitingRoomScreen
{
	/// <summary>Your name; double-click it to change it (Enter keeps it, Esc goes back).</summary>
	Control MyName(PublicPlayer me, RoomState room, bool reserveTag)
	{
		if (_rename != null) return _rename;
		var name = U.PlayerName(me.Name, 16, HorizontalAlignment.Center, reserveTag);
		name.MouseFilter = MouseFilterEnum.Stop;
		name.MouseDefaultCursorShape = CursorShape.Ibeam;
		name.TooltipText = Lang.T("Double-click to change your name");
		name.GuiInput += e =>
		{
			if (e is not InputEventMouseButton { DoubleClick: true, ButtonIndex: MouseButton.Left }) return;
			_rename = U.Input(me.Name, "", null, Limits.NameInputMax);
			_rename.Alignment = HorizontalAlignment.Center;
			var field = _rename;
			field.TextSubmitted += _ => Done(true);
			field.FocusExited += () => Done(true);
			field.GuiInput += k =>
			{
				if (k is InputEventKey { Pressed: true, Keycode: Key.Escape }) Done(false);
			};
			Refresh(room);
			field.CallDeferred(Control.MethodName.GrabFocus);
			field.CallDeferred(LineEdit.MethodName.SelectAll);

			void Done(bool keep)
			{
				if (_rename != field) return;
				_rename = null;
				var text = Server.Room.CleanName(field.Text);
				_renameError = null;
				if (keep && text != "" && text != me.Name)
				{
					if (room.Players.Any(p => p.Id != me.Id && string.Equals(p.Name, text, StringComparison.OrdinalIgnoreCase)))
						_renameError = Lang.T("Someone in this room already uses that name.");
					else
					{
						_client.Rename(text);
						Storage.Settings.PlayerName = text;
					}
				}
				// (after the field's own signal is done: rebuilding frees it)
				Callable.From(() => Refresh(_client.State ?? room)).CallDeferred();
			}
		};
		return name;
	}
}

/// <summary>Final results: places 1–3 on the podium, everyone else below. Ties share a place.</summary>
public partial class PodiumScreen : Control, IRoomScreen
{
	readonly bool _isHost;
	readonly string? _myId;
	readonly Action? _backToLobby;
	readonly VBoxContainer _content = U.V(14);

	public PodiumScreen() : this(false, null, null) { }

	// (the podium song stops with the screen: back to the lobby, home, or the game closing)
	public override void _ExitTree() => Storage.Sfx.StopPodium();

	public PodiumScreen(bool isHost, string? myId, Action? backToLobby)
	{
		_isHost = isHost;
		_myId = myId;
		_backToLobby = backToLobby;
		SetAnchorsPreset(LayoutPreset.FullRect);
		_content.Alignment = BoxContainer.AlignmentMode.Center;
		AddChild(U.Page(_content, 860, 48));
		Ready += () => AddChild(new Confetti());
	}

	/// <summary>Escape: the host goes back to the lobby, a player leaves.</summary>
	public void GoBack()
	{
		if (_isHost) _backToLobby?.Invoke();
		else App.I.LeaveRoom();
	}

	bool _played;

	public void Refresh(RoomState room)
	{
		// The host's podium sound, once (a player may have picked their own in Settings).
		if (!_played)
		{
			_played = true;
			Storage.Sfx.PlayRoomPodium(room.Settings.PodiumSound, _isHost);
		}
		_content.Clear();
		// (places by points, then the tiebreaks: only fully equal players share one)
		var ranked = room.Ranked().Select(p => (P: p, Score: room.Score(p.Id), Place: room.Place(p.Id))).ToList();
		var top = ranked.Where(r => r.Place <= 3).ToList();
		var rest = ranked.Where(r => r.Place > 3).ToList();
		var me = ranked.FirstOrDefault(r => r.P.Id == _myId);

		_content.AddChild(U.Eyebrow(Lang.F("{0} · final results", room.Quiz.Title)).Center());
		_content.AddChild(U.H(0, U.Heading(top.Count(r => r.Place == 1) > 1 ? "It's a tie at the top" : "And the winner is", 48, false), U.Text("…", 48, C.Accent, Fonts.Black)).Center());
		if (me.P != null) _content.AddChild(U.H(4, U.Muted("You finished", 16, false), U.Text(Lang.Ordinal(me.Place), 16, C.Accent, Fonts.Bold).Raw(), U.Muted(Lang.F("with {0} points.", me.Score), 16, false)).Center());

		// Classic podium order: 2nd, 1st, 3rd
		var podium = U.H(14);
		podium.Alignment = BoxContainer.AlignmentMode.Center;
		foreach (var place in new[] { 2, 1, 3 })
		foreach (var r in top.Where(t => t.Place == place))
		{
			var height = place == 1 ? 190 : place == 2 ? 140 : 100;
			var color = place == 1 ? C.Accent : place == 2 ? new Color("#c7cbe0") : new Color("#d39a6a");
			var block = U.Panel(U.Text(Lang.Ordinal(place), 44, new Color("#14151b"), Fonts.Black, false, HorizontalAlignment.Center).Raw(), "Clear")
				.With(p =>
				{
					var box = UiTheme.Box(color, 0, null, 0, 0, 10);
					box.CornerRadiusTopLeft = box.CornerRadiusTopRight = 14;
					p.AddThemeStyleboxOverride("panel", box);
				}).MinSize(170, height);
			// (a wide name is cut off with "…" instead of widening its step)
			var name = U.PlayerName(r.P.Name, 18, HorizontalAlignment.Center, U.AnyClanTag(top.Select(t => t.P.Name)));
			var step = U.V(6, new Avatar(r.P, 64).Center(), name, U.Mono(Lang.F("{0} pts", r.Score), 15, C.Muted).Center());
			if (GameParts.SeatNote(r.P, room) is { } note) step.AddChild(note.Center());
			step.AddChild(block);
			step.Alignment = BoxContainer.AlignmentMode.End;
			podium.AddChild(step);
		}
		_content.AddChild(podium.Center());

		if (rest.Count > 0)
		{
			var chips = U.Flow(8);
			chips.Alignment = FlowContainer.AlignmentMode.Center;
			foreach (var r in rest)
			{
				var chip = Chips.Player(r.P, $"· {r.Score}", r.P.Id == _myId);
				var inside = (HBoxContainer)chip.GetChild(0);
				// The place first, like the podium: "4th  Ann · 2"
				var place = U.Mono(Lang.Ordinal(r.Place), 13, C.Muted).Raw();
				inside.AddChild(place);
				inside.MoveChild(place, 0);
				if (GameParts.SeatNote(r.P, room) is { } note) inside.AddChild(note);
				chips.AddChild(chip);
			}
			_content.AddChild(chips);
		}
		_content.AddChild(U.Gap(10));
		if (_isHost) _content.AddChild(U.Btn("Back to lobby — play again", "AccentButton", _backToLobby).Center());
		else _content.AddChild(U.H(16, U.Muted("Thanks for playing! The host can start another round.", 14, false), U.Link("Leave", App.I.LeaveRoom)).Center());
	}
}
