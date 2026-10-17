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
		AddChild(U.Page(U.Panel(_content), 900));
	}

	public void Refresh(RoomState room)
	{
		_content.Clear();
		var connected = room.Players.Count(p => p.Connected);
		_content.AddChild(U.Eyebrow("Host view"));
		_content.AddChild(U.H(10, U.Heading(room.Quiz.Title, 28, false).Raw(), U.Muted($"· {U.Plural(room.Quiz.QuestionCount, "question", "questions")}", 16, false)
			.With(l => l.SizeFlagsVertical = SizeFlags.ShrinkEnd)));

		_content.AddChild(U.Muted("Lobby code", 15));
		var code = U.Text(room.Code, 72, C.Accent, Fonts.Mono);
		var copied = U.Small("Copy code", null);
		copied.Pressed += () =>
		{
			DisplayServer.ClipboardSet(room.Code);
			copied.Text = "Copied!";
		};
		_content.AddChild(U.H(16, code, U.Fill(), copied.With(b => b.SizeFlagsVertical = SizeFlags.ShrinkCenter)));

		if (_host.Mode == NetMode.Steam)
			_content.AddChild(U.Muted(Lang.F("Players open Quiznight, choose Steam on the home screen and enter this code. {0} of {1} seats taken.", room.Players.Count, room.Settings.MaxPlayers), 15));
		else
		{
			var addresses = LanHostTransport.LanAddresses();
			var where = addresses.Count > 0 ? string.Join(Lang.T("  or  "), addresses) : Lang.T("this PC's network address");
			_content.AddChild(U.Muted(Lang.F("LAN mode: players choose \"LAN / same PC\" and enter {0} (same Wi-Fi), or 127.0.0.1 on this PC. Port {1}. {2} of {3} seats taken.", where, _host.LanPort, room.Players.Count, room.Settings.MaxPlayers), 15));
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
			Stepper.Make("Max players", room.Settings.MaxPlayers, Math.Max(Limits.MaxPlayersMin, room.Players.Count), Limits.MaxPlayersMax,
				v => _host.UpdateSettings(maxPlayers: v)),
			Stepper.Make("Answer time per buzz", room.Settings.AnswerTimeSec, Limits.AnswerTimeMin, Limits.AnswerTimeMax,
				v => _host.UpdateSettings(answerTimeSec: v), " s"),
			BuzzerPicker.Make(_host.BuzzerSound, id => _host.BuzzerSound = id, false)), "Inset");
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

	public void Refresh(RoomState room)
	{
		_content.Clear();
		_content.AddChild(U.H(8, U.Heading("Waiting room", 28, false).Grow(),
			U.Pill(_client.Mode == NetMode.Steam ? Lang.F("Code {0}", room.Code) : Lang.F("Host {0}", _client.Target)),
			U.Pill(Lang.F("{0} of {1} players", room.Players.Count, room.Settings.MaxPlayers))));
		_content.AddChild(U.Muted(room.Quiz.Title, 15).Raw());

		var grid = new GridContainer { Columns = 4 };
		grid.AddThemeConstantOverride("h_separation", 12);
		grid.AddThemeConstantOverride("v_separation", 12);
		foreach (var p in room.Players)
		{
			var me = p.Id == _client.MyId;
			var seat = U.V(6, new Avatar(p, 56).Center(), U.Bold(p.Name, 16, null, false, HorizontalAlignment.Center).Raw(),
				U.Muted(me ? "You" : p.Connected ? "Joined" : "Reconnecting…", 13, false, HorizontalAlignment.Center));
			grid.AddChild(U.Panel(seat, me ? "Scored" : "Card2").Grow());
		}
		for (var i = room.Players.Count; i < room.Settings.MaxPlayers; i++)
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
		if (isHost) Ready += Storage.Sfx.PlayPodium;
	}

	public void Refresh(RoomState room)
	{
		_content.Clear();
		var sorted = room.Players.Select(p => (P: p, Score: room.Score(p.Id))).OrderByDescending(r => r.Score).ToList();
		var ranked = sorted.Select(r => (r.P, r.Score, Place: sorted.FindIndex(o => o.Score == r.Score) + 1)).ToList();
		var top = ranked.Where(r => r.Place <= 3).ToList();
		var rest = ranked.Where(r => r.Place > 3).ToList();
		var me = ranked.FirstOrDefault(r => r.P.Id == _myId);

		_content.AddChild(U.Eyebrow(Lang.F("{0} · final results", room.Quiz.Title)).Center());
		_content.AddChild(U.H(0, U.Heading(top.Count(r => r.Place == 1) > 1 ? "It's a tie at the top" : "And the winner is", 48, false), U.Text("…", 48, C.Accent, Fonts.Black)).Center());
		if (me.P != null) _content.AddChild(U.H(4, U.Muted("You finished", 16, false), U.Text($"#{me.Place}", 16, C.Accent, Fonts.Bold), U.Muted(Lang.F("with {0} points.", me.Score), 16, false)).Center());

		// Classic podium order: 2nd, 1st, 3rd
		var podium = U.H(14);
		podium.Alignment = BoxContainer.AlignmentMode.Center;
		foreach (var place in new[] { 2, 1, 3 })
		foreach (var r in top.Where(t => t.Place == place))
		{
			var height = place == 1 ? 190 : place == 2 ? 140 : 100;
			var color = place == 1 ? C.Accent : place == 2 ? new Color("#c7cbe0") : new Color("#d39a6a");
			var block = U.Panel(U.Text($"{place}", 44, new Color("#14151b"), Fonts.Black, false, HorizontalAlignment.Center), "Clear")
				.With(p =>
				{
					var box = UiTheme.Box(color, 0, null, 0, 0, 10);
					box.CornerRadiusTopLeft = box.CornerRadiusTopRight = 14;
					p.AddThemeStyleboxOverride("panel", box);
				}).MinSize(170, height);
			var step = U.V(6, new Avatar(r.P, 64).Center(), U.Bold(r.P.Name, 18, null, false, HorizontalAlignment.Center).Raw(), U.Mono(Lang.F("{0} pts", r.Score), 15, C.Muted).Center(), block);
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
				((HBoxContainer)chip.GetChild(0)).AddChild(U.Mono($"#{r.Place}", 13, C.Muted));
				chips.AddChild(chip);
			}
			_content.AddChild(chips);
		}
		_content.AddChild(U.Gap(10));
		if (_isHost) _content.AddChild(U.Btn("Back to lobby — play again", "AccentButton", _backToLobby).Center());
		else _content.AddChild(U.H(16, U.Muted("Thanks for playing! The host can start another round.", 14, false), U.Link("Leave", App.I.LeaveRoom)).Center());
	}
}
