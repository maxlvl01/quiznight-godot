// The host's side of the lobby's mini games (Shared/Arcade.cs): challenges, the Pong and Billiard matches it runs, and
// this lobby's records. HostSession passes requests in (only while the room is in the lobby), calls Tick every frame and
// sends the frames it returns to the two in each match; the host plays as ArcadeGame.HostId.

using System;
using System.Collections.Generic;
using System.Linq;
using QuizNight.Shared;

namespace QuizNight.Server;

public sealed class ArcadeHost
{
	sealed class Match
	{
		public string Id = "";
		public string Game = "";
		public string A = "";
		public string B = "";
		public int ScoreA, ScoreB;
		public PongState? Pong;
		public BilliardState? Billiard;
		public double SinceFrame;

		public bool Has(string id) => A == id || B == id;
	}

	readonly List<ArcadeInvite> _invites = new();
	readonly List<Match> _matches = new();
	readonly List<ArcadeRecord> _records = new();
	readonly Dictionary<string, int> _tetris = new();
	int _next;

	/// <summary>Who can be challenged: everyone in the room on a game that has the mini games (0.12 on), and the host.</summary>
	public Func<IEnumerable<string>> Players = () => Array.Empty<string>();
	public string HostName = "Host";

	const double FrameSec = 1 / 30.0, StepSec = 1 / 240.0;

	public ArcadeView View() => new()
	{
		Invites = _invites.Select(i => new ArcadeInvite { From = i.From, To = i.To, Game = i.Game }).ToList(),
		Matches = _matches.Select(m => new ArcadeMatchInfo { Id = m.Id, Game = m.Game, A = m.A, B = m.B }).ToList(),
		Records = _records.Select(r => new ArcadeRecord { Game = r.Game, A = r.A, B = r.B, WinsA = r.WinsA, WinsB = r.WinsB, StreakBy = r.StreakBy, Streak = r.Streak }).ToList(),
		TetrisBest = new Dictionary<string, int>(_tetris),
		HostName = HostName,
	};

	bool Busy(string id) => _matches.Any(m => m.Has(id));

	/// <summary>
	/// A request from someone in the lobby; returns whether the lobby's view changed, and frames to send (a match that
	/// started or ended).
	/// </summary>
	public (bool Changed, List<ArcadeFrame> Frames) Request(string from, ArcadeRequest r)
	{
		var frames = new List<ArcadeFrame>();
		switch (r.Kind)
		{
			case ArcadeRequest.Challenge when ArcadeGame.Versus.Contains(r.Game) && r.Who != from && Players().Contains(r.Who) && !Busy(from) && !Busy(r.Who):
				_invites.RemoveAll(i => i.From == from);
				_invites.Add(new ArcadeInvite { From = from, To = r.Who, Game = r.Game });
				return (true, frames);
			case ArcadeRequest.Cancel:
				return (_invites.RemoveAll(i => i.From == from) > 0, frames);
			case ArcadeRequest.Decline:
				return (_invites.RemoveAll(i => i.From == r.Who && i.To == from) > 0, frames);
			case ArcadeRequest.Accept when _invites.FirstOrDefault(i => i.From == r.Who && i.To == from) is { } invite && !Busy(from) && !Busy(r.Who):
			{
				_invites.RemoveAll(i => i.From == from || i.To == from || i.From == r.Who || i.To == r.Who);
				var m = new Match { Id = $"m{++_next}", Game = invite.Game, A = invite.From, B = from };
				if (m.Game == ArcadeGame.Pong) m.Pong = new PongState();
				else m.Billiard = BilliardState.New(m.Game);
				_matches.Add(m);
				frames.Add(Frame(m));
				return (true, frames);
			}
			case ArcadeRequest.Paddle when _matches.FirstOrDefault(m => m.Has(from) && m.Pong != null) is { } m:
				var y = Math.Clamp(r.Value, 0, 1) * PongState.H;
				if (from == m.A) m.Pong!.PaddleA = y;
				else m.Pong!.PaddleB = y;
				return (false, frames);
			// (the lag for the break: each shoots once, whenever they like, even while the other's ball rolls)
			case ArcadeRequest.Shot when _matches.FirstOrDefault(m => m.Has(from) && m.Billiard is { Lag: true }) is { } m
				&& double.IsFinite(r.Angle) && double.IsFinite(r.Power):
				if (BilliardRules.LagShoot(m.Billiard!, from == m.A ? "A" : "B", r.Angle, r.Power)) frames.Add(Frame(m));
				return (false, frames);
			case ArcadeRequest.Shot when _matches.FirstOrDefault(m => m.Has(from) && m.Billiard != null) is { } m
				&& !m.Billiard!.Moving && (m.Billiard.Turn == "A" ? m.A : m.B) == from && double.IsFinite(r.Angle) && double.IsFinite(r.Power):
				BilliardRules.Shoot(m.Billiard, r.Angle, r.Power);
				frames.Add(Frame(m));
				return (false, frames);
			case ArcadeRequest.Leave when _matches.FirstOrDefault(m => m.Has(from)) is { } m:
				frames.Add(End(m, ""));
				return (true, frames);
			case ArcadeRequest.Score when r.Value is > 0 and < 100_000_000:
				var score = (int)r.Value;
				if (score <= _tetris.GetValueOrDefault(from)) return (false, frames);
				_tetris[from] = score;
				return (true, frames);
		}
		return (false, frames);
	}

	/// <summary>Someone left the room: their challenges go, and their match ends (counting for nobody).</summary>
	public (bool Changed, List<ArcadeFrame> Frames) Gone(string id)
	{
		var changed = _invites.RemoveAll(i => i.From == id || i.To == id) > 0;
		var frames = _matches.Where(m => m.Has(id)).ToList().Select(m => End(m, "")).ToList();
		return (changed || frames.Count > 0, frames);
	}

	/// <summary>The quiz starts: every match ends at once (counting for nobody), challenges go.</summary>
	public List<ArcadeFrame> StopAll()
	{
		_invites.Clear();
		return _matches.ToList().Select(m => End(m, "")).ToList();
	}

	/// <summary>Moves every match on; returns the frames to send and whether the lobby's view changed (a match ended).</summary>
	public (bool Changed, List<ArcadeFrame> Frames) Tick(double dt)
	{
		var frames = new List<ArcadeFrame>();
		var changed = false;
		dt = Math.Min(dt, 0.1);
		foreach (var m in _matches.ToList())
		{
			var urgent = false;
			string? decided = null;
			for (var t = 0.0; t < dt; t += StepSec)
			{
				var step = Math.Min(StepSec, dt - t);
				if (m.Pong != null)
				{
					var point = PongRules.Step(m.Pong, step);
					if (point == 0) continue;
					if (point > 0) m.ScoreA++;
					else m.ScoreB++;
					urgent = true;
				}
				else if (m.Billiard is { Moving: true } b)
				{
					BilliardRules.Step(b, step);
					if (!b.Moving)
					{
						if (b.Lag)
						{
							BilliardRules.EndLag(b);
							urgent = true;
							break;
						}
						decided = BilliardRules.Settle(b, ref m.ScoreA, ref m.ScoreB);
						urgent = true;
						break;
					}
				}
			}
			var winner = m.Pong != null
				? m.ScoreA >= PongState.Win ? m.A : m.ScoreB >= PongState.Win ? m.B : null
				: decided == "A" ? m.A : decided == "B" ? m.B : null;
			if (winner != null)
			{
				frames.Add(End(m, winner));
				changed = true;
				continue;
			}
			// (~30 frames a second while something moves; a billiard table at rest needs none until the next shot)
			m.SinceFrame += dt;
			if (urgent || m.SinceFrame >= FrameSec && (m.Pong != null || m.Billiard is { Moving: true }))
			{
				m.SinceFrame = 0;
				frames.Add(Frame(m));
			}
		}
		return (changed, frames);
	}

	ArcadeFrame End(Match m, string winner)
	{
		_matches.Remove(m);
		if (winner != "")
		{
			var (a, b) = string.CompareOrdinal(m.A, m.B) < 0 ? (m.A, m.B) : (m.B, m.A);
			var record = _records.FirstOrDefault(r => r.Game == m.Game && r.A == a && r.B == b);
			if (record == null) _records.Add(record = new ArcadeRecord { Game = m.Game, A = a, B = b });
			record.Add(winner);
		}
		var frame = Frame(m);
		frame.Winner = winner;
		return frame;
	}

	static ArcadeFrame Frame(Match m) => new()
	{
		Match = m.Id, Game = m.Game, A = m.A, B = m.B, ScoreA = m.ScoreA, ScoreB = m.ScoreB,
		Pong = m.Pong == null ? null : new PongState
		{
			BallX = m.Pong.BallX, BallY = m.Pong.BallY, VelX = m.Pong.VelX, VelY = m.Pong.VelY,
			PaddleA = m.Pong.PaddleA, PaddleB = m.Pong.PaddleB, Wait = m.Pong.Wait, ServeTo = m.Pong.ServeTo,
		},
		Billiard = m.Billiard == null ? null : new BilliardState
		{
			X = m.Billiard.X.ToList(), Y = m.Billiard.Y.ToList(), VX = m.Billiard.VX.ToList(), VY = m.Billiard.VY.ToList(),
			In = m.Billiard.In.ToList(), Turn = m.Billiard.Turn, Moving = m.Billiard.Moving, Mode = m.Billiard.Mode,
			GroupA = m.Billiard.GroupA, Cue = m.Billiard.Cue, Lag = m.Billiard.Lag, LagShot = m.Billiard.LagShot.ToList(), LagFar = m.Billiard.LagFar.ToList(),
		},
	};
}
