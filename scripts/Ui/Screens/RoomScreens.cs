using System;
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

	public MessageScreen(string text, string? button = null, Action? onButton = null)
	{
		SetAnchorsPreset(LayoutPreset.FullRect);
		var box = U.V(16, U.Text(text, 18, C.Muted, null, false, HorizontalAlignment.Center));
		if (button != null) box.AddChild(U.Btn(button, "", onButton).Center());
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

	public HostLobbyScreen() : this(null!) { }

	public HostLobbyScreen(HostSession host)
	{
		_host = host;
		SetAnchorsPreset(LayoutPreset.FullRect);
		AddChild(U.Page(U.Panel(_content), 900, 16));
	}

	public void Refresh(RoomState room)
	{
		_content.Clear();
		var connected = room.Players.Count(p => p.Connected);
		_content.AddChild(U.Eyebrow("Host view"));
		_content.AddChild(U.H(10, U.Heading(room.Quiz.Title, 28, false).Raw(), U.Muted($"· {U.Plural(room.Quiz.QuestionCount, "question", "questions")}", 16, false)
			.With(l => l.SizeFlagsVertical = SizeFlags.ShrinkEnd)));

		_content.AddChild(U.Muted("Lobby code", 15));
		var code = U.Text(room.Code, 64, C.Accent, Fonts.Mono);
		var copied = U.Small("Copy code", null);
		copied.Pressed += () =>
		{
			DisplayServer.ClipboardSet(room.Code);
			copied.Text = "Copied!";
		};
		_content.AddChild(U.H(16, code, U.Fill(), copied.With(b => b.SizeFlagsVertical = SizeFlags.ShrinkCenter)));

		if (_host.Mode == NetMode.Steam)
			_content.AddChild(U.Muted(Lang.F("Players open Quiznight, choose Steam on the home screen and enter this code. {0} here so far (up to {1}).", room.Players.Count, Limits.MaxPlayers), 15));
		else
		{
			var addresses = LanHostTransport.LanAddresses();
			var where = addresses.Count > 0 ? string.Join(Lang.T("  or  "), addresses) : Lang.T("this PC's network address");
			_content.AddChild(U.Muted(Lang.F("LAN mode: players choose \"LAN / same PC\" and enter {0} (same Wi-Fi), or 127.0.0.1 on this PC. Port {1}. {2} here so far (up to {3}).", where, _host.LanPort, room.Players.Count, Limits.MaxPlayers), 15));
		}

		var chips = U.Flow(8);
		if (room.Players.Count == 0) chips.AddChild(U.Muted("Nobody here yet — send the code to your friends.", 14, false));
		foreach (var p in room.Players)
		{
			var chip = Chips.Player(p, p.Connected ? null : "(offline)");
			var row = (HBoxContainer)chip.GetChild(0);
			var id = p.Id;
			row.AddChild(U.Link("×", () => _host.Kick(id), 16).Tip(Lang.F("Remove {0}", p.Name)));
			chips.AddChild(chip);
		}
		_content.AddChild(chips);

		var rules = U.Panel(U.V(10,
			U.Eyebrow("Game rules for tonight"),
			Stepper.Make("Answer time per buzz", room.Settings.AnswerTimeSec, Limits.AnswerTimeMin, Limits.AnswerTimeMax,
				v => _host.UpdateSettings(answerTimeSec: v), " s"),
			Rule("I show each question myself (it stays hidden until I press Show — Space)", room.Settings.HostShowsQuestions,
				on => _host.UpdateSettings(hostShows: on)),
			Rule("I unlock the buzzers myself (after reading the question out — Space)", room.Settings.HostUnlocksBuzzers,
				on => _host.UpdateSettings(hostUnlocks: on)),
			U.Text("Jokers (each player can play each one once per match, before a question starts)", 15),
			Rule("×2 Double points: whatever the next question gives counts twice", room.Settings.Jokers.Contains(Joker.Double),
				on => _host.UpdateSettings(joker: (Joker.Double, on))),
			Rule("⚡ Fast Pass: on the next buzzer question, your buzz goes ahead of the line", room.Settings.Jokers.Contains(Joker.FastPass),
				on => _host.UpdateSettings(joker: (Joker.FastPass, on))),
			BuzzerPicker.Make(_host.BuzzerSound, id => _host.BuzzerSound = id, false),
			PodiumPicker.Make(_host.PodiumSound, id => _host.PodiumSound = id, true)), "Inset");
		_content.AddChild(rules);

		if (_error != null) _content.AddChild(U.Error(_error));
		var start = U.Btn("Start quiz", "AccentButton", () =>
		{
			var result = _host.Start();
			_error = result.Ok ? null : result.Error;
			if (!result.Ok) Refresh(_host.State);
		});
		start.Disabled = connected < Limits.MinPlayersToStart;
		_content.AddChild(U.H(10, U.Link("Close room", () => Dialogs.Confirm(this, "Close the room? Everyone in it is sent back to the home screen.", "Close room", App.I.CloseRoom)), U.Fill(), start));
		if (connected < Limits.MinPlayersToStart)
			_content.AddChild(U.Muted(Lang.F("Needs at least {0} players.", Limits.MinPlayersToStart), 13, false, HorizontalAlignment.Right).End());
	}

	static CheckBox Rule(string text, bool on, Action<bool> changed)
	{
		var box = new CheckBox { Text = text, ButtonPressed = on, MouseDefaultCursorShape = CursorShape.PointingHand };
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
		AddChild(U.Page(U.Panel(_content), 900));
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
			U.Pill(U.Plural(room.Players.Count, "player", "players"))));
		_content.AddChild(U.Muted(room.Quiz.Title, 15).Raw());

		var grid = new GridContainer { Columns = 4 };
		grid.AddThemeConstantOverride("h_separation", 12);
		grid.AddThemeConstantOverride("v_separation", 12);
		foreach (var p in room.Players)
		{
			var me = p.Id == _client.MyId;
			var seat = U.V(6, new Avatar(p, 56).Center(), me ? MyName(p, room) : U.Bold(p.Name, 16, null, false, HorizontalAlignment.Center).Raw(),
				U.Muted(me ? "You · double-click your name to change it" : p.Connected ? "Joined" : "Reconnecting…", 13, true, HorizontalAlignment.Center));
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
		_content.AddChild(U.H(16,
			U.Muted("Waiting for the host to start the quiz.", 14, false).Grow(),
			U.Muted(Lang.F("Answer time per buzz: {0} s", room.Settings.AnswerTimeSec), 14, false),
			U.Link("Leave", App.I.LeaveRoom)));
	}
}

public partial class WaitingRoomScreen
{
	/// <summary>Your name; double-click it to change it (Enter keeps it, Esc goes back).</summary>
	Control MyName(PublicPlayer me, RoomState room)
	{
		if (_rename != null) return _rename;
		var name = U.Bold(me.Name, 16, null, false, HorizontalAlignment.Center).Raw();
		name.MouseFilter = MouseFilterEnum.Stop;
		name.MouseDefaultCursorShape = CursorShape.Ibeam;
		name.TooltipText = Lang.T("Double-click to change your name");
		name.GuiInput += e =>
		{
			if (e is not InputEventMouseButton { DoubleClick: true, ButtonIndex: MouseButton.Left }) return;
			_rename = U.Input(me.Name, "", null, Limits.NameMaxLength);
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
			var name = U.Bold(r.P.Name, 18, null, false, HorizontalAlignment.Center).Raw().With(l =>
			{
				l.ClipText = true;
				l.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
				l.TooltipText = r.P.Name;
				l.MouseFilter = MouseFilterEnum.Pass;
			});
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
