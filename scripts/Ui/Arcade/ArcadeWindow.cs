// The lobby's mini games window (Shared/Arcade.cs): a 4:3 window over the lobby or the waiting room. Its menu has Tetris
// (alone) and challenges for Pong and Billiard (against anyone in the lobby on 0.12 or later, and the host); a match
// opens by itself when someone accepts, and ends back on the menu. The host and the players use the same window: an
// ArcadeLink says who "I" am, what the room looks like and where requests go.

using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using QuizNight.Net;
using QuizNight.Server;
using QuizNight.Shared;

namespace QuizNight.Ui.Arcade;

/// <summary>What the mini games need from the host's or a player's session.</summary>
public sealed class ArcadeLink
{
	public required string MyId;
	public required Func<RoomState?> State;
	public required Action<ArcadeRequest> Send;
	public event Action<ArcadeFrame>? Frame;
	Action? _unhook;

	public void Raise(ArcadeFrame frame) => Frame?.Invoke(frame);

	public void Unhook() => _unhook?.Invoke();

	public static ArcadeLink ForHost(HostSession host)
	{
		var link = new ArcadeLink { MyId = ArcadeGame.HostId, State = () => host.State, Send = host.ArcadeRequest };
		host.ArcadeFrame += link.Raise;
		link._unhook = () => host.ArcadeFrame -= link.Raise;
		return link;
	}

	public static ArcadeLink ForPlayer(ClientSession client)
	{
		var link = new ArcadeLink { MyId = client.MyId ?? "", State = () => client.State, Send = client.Arcade };
		client.ArcadeFrame += link.Raise;
		link._unhook = () => client.ArcadeFrame -= link.Raise;
		return link;
	}

	/// <summary>Someone's name as the mini games show it (the host by the host's name).</summary>
	public string Name(string id) => id == ArcadeGame.HostId ? State()?.Arcade?.HostName ?? Lang.T("Host") : State()?.Player(id).Name ?? id;

	/// <summary>Everyone I could challenge: in the lobby, on a game with the mini games, not me.</summary>
	public List<string> Opponents()
	{
		if (State() is not { } room) return new();
		var ids = room.Players.Where(p => p.Connected && p.Version != "").Select(p => p.Id).ToList();
		if (MyId != ArcadeGame.HostId) ids.Add(ArcadeGame.HostId);
		return ids.Where(id => id != MyId).ToList();
	}
}

public partial class ArcadeWindow : Control
{
	public static readonly Vector2 Size43 = new(720, 540);

	readonly ArcadeLink _link;
	readonly VBoxContainer _body = U.V(10);
	readonly Label _title = U.Heading("Mini games", 24, false);
	/// <summary>What the window shows now ("Tetris", "8-ball" …), for bug reports.</summary>
	public string Title => _title.Text;
	Control? _game;
	string? _matchId;
	string _billiardMode = BilliardMode.Eight;

	public ArcadeWindow() : this(null!) { }

	public ArcadeWindow(ArcadeLink link)
	{
		_link = link;
		SetAnchorsPreset(LayoutPreset.FullRect);
		MouseFilter = MouseFilterEnum.Stop;
		var dim = new ColorRect { Color = new Color(0, 0, 0, 0.55f), MouseFilter = MouseFilterEnum.Ignore };
		dim.SetAnchorsPreset(LayoutPreset.FullRect);
		AddChild(dim);
		var frame = U.Panel(U.V(8, U.H(8, _title.Grow(), U.Small("✕ Close", Close)), _body.GrowV()));
		frame.CustomMinimumSize = Size43;
		var center = new CenterContainer { MouseFilter = MouseFilterEnum.Ignore };
		center.SetAnchorsPreset(LayoutPreset.FullRect);
		center.AddChild(frame);
		AddChild(center);
		link.Frame += OnFrame;
		ShowMenu();
	}

	public override void _ExitTree()
	{
		_link.Frame -= OnFrame;
		// (closing the window leaves a match: it counts for nobody)
		if (_matchId != null) _link.Send(new ArcadeRequest { Kind = ArcadeRequest.Leave });
	}

	void Close()
	{
		if (_game is TetrisGame tetris) tetris.Finish();
		QueueFree();
	}

	/// <summary>The lobby changed (a challenge, a record): the menu shows it.</summary>
	public void Refresh()
	{
		if (_game == null) ShowMenu();
	}

	public void OnFrame(ArcadeFrame frame)
	{
		if (frame.Winner != null)
		{
			if (frame.Match != _matchId) return;
			_matchId = null;
			ShowResult(frame);
			return;
		}
		if (_matchId != frame.Match)
		{
			_matchId = frame.Match;
			_title.Text = Lang.T(ArcadeGame.Label(frame.Game));
			Play(frame.Game == ArcadeGame.Pong ? new PongView(_link, frame) : new BilliardView(_link, frame));
		}
		switch (_game)
		{
			case PongView pong: pong.Take(frame); break;
			case BilliardView billiard: billiard.Take(frame); break;
		}
	}

	void Play(Control game)
	{
		_body.Clear();
		_game = game;
		_body.AddChild(game.Grow().GrowV());
		var leave = U.Small("← Back to the menu", () =>
		{
			if (_matchId != null) _link.Send(new ArcadeRequest { Kind = ArcadeRequest.Leave });
			if (_game is TetrisGame t) t.Finish();
			_matchId = null;
			ShowMenu();
		});
		_body.AddChild(U.H(8, leave, U.Fill(), U.Muted(game is ReactionGame ? "Click the buzzer or press Space." : game is TetrisGame
			? "← → move · ↑ / X turn · Z turn back · ↓ faster · Space drop"
			: game is PongView ? "Move the mouse (or ↑ ↓) to move your paddle. First to 5."
			: "Your turn: drag back from your ball and let go to shoot.", 12, false)));
		if (game is BilliardView billiard) _body.AddChild(U.Muted(BilliardMode.Rules(billiard.Mode), 12));
		game.GrabFocus();
	}

	void ShowResult(ArcadeFrame frame)
	{
		_game = null;
		_body.Clear();
		var won = frame.Winner == _link.MyId;
		var text = frame.Winner == "" ? Lang.T("The match was cut off: it counts for nobody.")
			: won ? Lang.F("You beat {0}!", _link.Name(frame.A == _link.MyId ? frame.B : frame.A))
			: Lang.F("{0} won this one.", _link.Name(frame.Winner!));
		_body.AddChild(U.V(14, U.Fill(), U.Heading(text, 30).With(l => l.HorizontalAlignment = HorizontalAlignment.Center),
			U.Text($"{frame.ScoreA} – {frame.ScoreB}", 40, C.Accent, Fonts.Black).With(l => l.HorizontalAlignment = HorizontalAlignment.Center),
			U.Small("Back to the menu", ShowMenu).Center(), U.Fill()).GrowV());
	}

	// ---------- the menu ----------

	void ShowMenu()
	{
		_game = null;
		_title.Text = Lang.T("Mini games");
		_body.Clear();
		var room = _link.State();
		var view = room?.Arcade;
		var me = _link.MyId;

		var tetris = U.Card(8, U.Eyebrow("Alone"),
			U.Btn("▶ Tetris", "AccentButton", () =>
			{
				_title.Text = "Tetris";
				Play(new TetrisGame(score => _link.Send(new ArcadeRequest { Kind = ArcadeRequest.Score, Value = score })));
			}),
			U.Btn("▶ Reaction", "AccentButton", PlayReaction),
			U.Small("🏆 High scores", () => ShowScores(ArcadeGame.Reaction)));

		var versus = U.V(6, U.Eyebrow("1 vs 1"));
		var incoming = view?.Invites.Where(i => i.To == me).ToList() ?? new();
		foreach (var invite in incoming)
		{
			var from = invite.From;
			versus.AddChild(U.H(6, U.Text(Lang.F("{0} challenges you to {1}!", _link.Name(from), Lang.T(ArcadeGame.Label(invite.Game))), 14, C.Gold, null, true).Grow(),
				U.Small("Accept", () => _link.Send(new ArcadeRequest { Kind = ArcadeRequest.Accept, Who = from })),
				U.Small("No thanks", () => _link.Send(new ArcadeRequest { Kind = ArcadeRequest.Decline, Who = from }))));
		}
		var mine = view?.Invites.FirstOrDefault(i => i.From == me);
		if (mine != null)
			versus.AddChild(U.H(6, U.Muted(Lang.F("Waiting for {0} to accept {1} …", _link.Name(mine.To), Lang.T(ArcadeGame.Label(mine.Game))), 13).Grow(),
				U.Small("Cancel", () => _link.Send(new ArcadeRequest { Kind = ArcadeRequest.Cancel }))));
		var busy = view?.Matches.SelectMany(m => new[] { m.A, m.B }).ToHashSet() ?? new HashSet<string>();
		// (which billiard game the Billiard buttons challenge to; kept while the window is open)
		versus.AddChild(U.H(8, U.Muted("Billiard game:", 13, false).With(l => l.SizeFlagsVertical = SizeFlags.ShrinkCenter),
			U.Select(BilliardMode.All.Select(m => (m, BilliardMode.Label(m))).ToArray(), _billiardMode, m =>
			{
				_billiardMode = m;
				Callable.From(ShowMenu).CallDeferred();
			}).Grow()));
		versus.AddChild(U.Muted(BilliardMode.Rules(_billiardMode), 12));
		var opponents = _link.Opponents();
		if (opponents.Count == 0) versus.AddChild(U.Muted("Nobody to play against yet (players on older versions don't have the mini games).", 13));
		foreach (var id in opponents)
		{
			var who = id;
			var row = U.H(6, U.Text(_link.Name(id), 15, C.Text).Raw().Grow());
			if (busy.Contains(id)) row.AddChild(U.Muted("playing", 12, false));
			else
				foreach (var game in new[] { ArcadeGame.Pong, _billiardMode })
				{
					var g = game;
					row.AddChild(U.Small(Lang.T(ArcadeGame.Label(g)), () => _link.Send(new ArcadeRequest { Kind = ArcadeRequest.Challenge, Game = g, Who = who }))
						.With(b => b.Disabled = mine != null || busy.Contains(me)));
				}
			versus.AddChild(row);
		}

		var records = U.V(4, U.Eyebrow("Rivalries"));
		foreach (var line in Rivalries(view, _link.Name)) records.AddChild(U.Text(line, 13, C.Text, null, true).Raw());
		if (records.GetChildCount() == 1) records.AddChild(U.Muted("Who beat whom how often shows here.", 13));

		var scroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled }.GrowV();
		scroll.AddChild(U.Panel(versus, "Inset").Grow());
		_body.AddChild(U.H(10, U.V(10, tetris, U.Panel(records, "Inset").GrowV()).With(v => v.CustomMinimumSize = new Vector2(240, 0)), scroll.Grow()).GrowV());
	}

	public void PlayReaction()
	{
		_title.Text = Lang.T("Reaction");
		Play(new ReactionGame(ms => _link.Send(new ArcadeRequest { Kind = ArcadeRequest.ReactionTime, Value = ms })));
	}

	// ---------- high scores ----------

	/// <summary>The bests of all time, from the results the game ships with (eternal table's data), added up once.</summary>
	static List<string>? _allResults;

	/// <summary>Tonight's and all-time bests for Tetris or the reaction test, like F1 timing.</summary>
	public void ShowScores(string game)
	{
		_game = null;
		_title.Text = Lang.T("High scores");
		_body.Clear();
		var reaction = game == ArcadeGame.Reaction;
		_allResults ??= Storage.Results.BundledCsvs().Concat(Storage.Results.BundledCsvs(true)).ToList();
		var before = Storage.Results.ArcadeBests(_allResults, game);
		var tonightRaw = (reaction ? _link.State()?.Arcade?.ReactionBest : _link.State()?.Arcade?.TetrisBest) ?? new Dictionary<string, int>();
		bool Better(int a, int b) => reaction ? a < b : a > b;
		var tonight = tonightRaw.Select(kv =>
		{
			var had = before.FirstOrDefault(b => b.PlayerId == kv.Key);
			// (green: better than their best before tonight)
			return new F1Times.Entry(kv.Key, _link.Name(kv.Key), kv.Value, had.PlayerId == null || Better(kv.Value, had.Value));
		}).OrderBy(e => reaction ? e.Value : -e.Value).ToList();
		// (all time: the shipped results, with tonight's bests counted in)
		var all = before.ToDictionary(b => b.PlayerId, b => (b.Player, b.Value));
		foreach (var e in tonight)
			if (!all.TryGetValue(e.Id, out var had) || Better(e.Value, had.Value)) all[e.Id] = (e.Name, e.Value);
		var allTime = all.Select(a => new F1Times.Entry(a.Key, a.Value.Player, a.Value.Value, false))
			.OrderBy(e => reaction ? e.Value : -e.Value).Take(15).ToList();
		int? record = allTime.Count > 0 ? allTime[0].Value : null;
		Func<int, string> show = reaction ? F1Times.Seconds : F1Times.Points;
		var tabs = U.Segmented(new[] { (ArcadeGame.Reaction, "Reaction"), (ArcadeGame.Tetris, "Tetris") }, game, g => Callable.From(() => ShowScores(g)).CallDeferred());
		var scroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled }.GrowV();
		scroll.AddChild(U.H(16,
			U.V(8, U.Eyebrow("Tonight"), F1Times.List(tonight, reaction, show, show, record)).Grow(),
			U.V(8, U.Eyebrow("All time"), F1Times.List(allTime, reaction, show, show, record)).Grow()).Grow());
		_body.AddChild(tabs);
		_body.AddChild(scroll);
		_body.AddChild(U.H(8, U.Small("← Back to the menu", ShowMenu), U.Fill(),
			U.Muted(reaction ? "Best of 5 tries, in seconds. Purple: best of all time · green: a personal best." : "Highest score. Purple: best of all time · green: a personal best.", 12, false)));
	}

	/// <summary>
	/// For the lobby screens: a challenge waiting for me (Accept opens the window), the "Mini games" button and the top
	/// rivalries.
	/// </summary>
	public static Control LobbyStrip(ArcadeLink link, Action open)
	{
		var strip = U.V(6);
		var view = link.State()?.Arcade;
		foreach (var invite in view?.Invites.Where(i => i.To == link.MyId) ?? Enumerable.Empty<ArcadeInvite>())
		{
			var from = invite.From;
			strip.AddChild(U.Panel(U.H(8, U.Text(Lang.F("🎮 {0} challenges you to {1}!", link.Name(from), Lang.T(ArcadeGame.Label(invite.Game))), 15, C.Gold, Fonts.Bold, true).Grow(),
				U.Small("Accept", () =>
				{
					link.Send(new ArcadeRequest { Kind = ArcadeRequest.Accept, Who = from });
					open();
				}),
				U.Small("No thanks", () => link.Send(new ArcadeRequest { Kind = ArcadeRequest.Decline, Who = from }))), "Inset"));
		}
		var rivals = Rivalries(view, link.Name).Take(2).ToList();
		strip.AddChild(U.H(10, U.Small("🎮 Mini games", open).Tip("Tetris, Pong and Billiard while you wait"),
			U.Muted(rivals.Count > 0 ? string.Join("   ·   ", rivals) : Lang.T("Tetris, Pong or Billiard while you wait."), 13, false).Raw()
				.With(l => { l.SizeFlagsVertical = SizeFlags.ShrinkCenter; l.ClipText = true; }).Grow()));
		return strip;
	}

	/// <summary>"Ann 3 – 1 Ben · Pong · Ann ×2 in a row", the closest rivalries first.</summary>
	public static IEnumerable<string> Rivalries(ArcadeView? view, Func<string, string> name) =>
		view?.Records.OrderByDescending(r => r.WinsA + r.WinsB).Take(6).Select(r =>
			Lang.F("{0} {1} – {2} {3} · {4}", name(r.A), r.WinsA, r.WinsB, name(r.B), Lang.T(ArcadeGame.Label(r.Game)))
			+ (r.Streak >= 2 ? " · " + Lang.F("{0} ×{1} in a row", name(r.StreakBy), r.Streak) : "")) ?? Enumerable.Empty<string>();
}
