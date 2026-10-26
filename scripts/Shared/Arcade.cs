// The lobby's mini games while everyone waits for the quiz: Tetris (alone, played on your own PC; only the score goes to
// the host), and Pong and Billiard (1 vs 1, against another player or the host). The host's game runs every match, like
// the quiz: players send their paddle or their shot, the host moves the ball and sends the match ~30 times a second
// (ArcadeFrame) to the two in it. Who beat whom how often, and streaks, are kept for this lobby (ArcadeView, in the room
// state). Starting the quiz ends every match at once; a match cut off counts for nobody.

using System;
using System.Collections.Generic;
using System.Linq;

namespace QuizNight.Shared;

public static class ArcadeGame
{
	public const string Tetris = "tetris";
	public const string Pong = "pong";
	public const string Billiard = "billiard";

	public static readonly string[] Versus = { Pong, Billiard };

	public static string Label(string game) => game switch { Tetris => "Tetris", Pong => "Pong", Billiard => "Billiard", _ => game };

	/// <summary>The host plays as this id (not a player of the quiz).</summary>
	public const string HostId = "host";
}

/// <summary>Player → host: a challenge and its answer, a paddle, a shot, leaving a match, a Tetris score.</summary>
public sealed class ArcadeRequest
{
	public string Kind = "";
	public string Game = "";
	/// <summary>Who's challenged (challenge), or whose challenge it is (accept / decline).</summary>
	public string Who = "";
	/// <summary>Pong: the paddle's middle, 0–1 down the field. Tetris: the score.</summary>
	public double Value;
	/// <summary>Billiard: the shot's direction (radians) and strength (0–1).</summary>
	public double Angle;
	public double Power;

	public const string Challenge = "challenge";
	public const string Accept = "accept";
	public const string Decline = "decline";
	public const string Cancel = "cancel";
	public const string Paddle = "paddle";
	public const string Shot = "shot";
	public const string Leave = "leave";
	public const string Score = "score";
}

/// <summary>The lobby's side of the mini games (in RoomState): open challenges, who's playing, the records.</summary>
public sealed class ArcadeView
{
	public List<ArcadeInvite> Invites = new();
	public List<ArcadeMatchInfo> Matches = new();
	public List<ArcadeRecord> Records = new();
	/// <summary>Tetris: everyone's best score in this lobby.</summary>
	public Dictionary<string, int> TetrisBest = new();
	/// <summary>The host's name, as the mini games show them.</summary>
	public string HostName = "Host";
}

public sealed class ArcadeInvite
{
	public string From = "";
	public string To = "";
	public string Game = "";
}

public sealed class ArcadeMatchInfo
{
	public string Id = "";
	public string Game = "";
	public string A = "";
	public string B = "";
}

/// <summary>Two people's record in one game: wins each, and the current streak.</summary>
public sealed class ArcadeRecord
{
	public string Game = "";
	public string A = "";
	public string B = "";
	public int WinsA;
	public int WinsB;
	/// <summary>Who won the last ones in a row, and how many.</summary>
	public string StreakBy = "";
	public int Streak;

	public void Add(string winner)
	{
		if (winner == A) WinsA++;
		else WinsB++;
		Streak = StreakBy == winner ? Streak + 1 : 1;
		StreakBy = winner;
	}
}

/// <summary>Host → the two in a match: where everything is right now.</summary>
public sealed class ArcadeFrame
{
	public string Match = "";
	public string Game = "";
	public string A = "";
	public string B = "";
	public int ScoreA;
	public int ScoreB;
	/// <summary>Set when it's over: the winner's id ("" = cut off, counts for nobody).</summary>
	public string? Winner;
	public PongState? Pong;
	public BilliardState? Billiard;
}

// ---------- Pong ----------

/// <summary>A 4 × 3 field; A's paddle on the left, B's on the right; first to <see cref="Win"/>.</summary>
public sealed class PongState
{
	public const double W = 4, H = 3, PaddleH = 0.6, PaddleX = 0.15, Ball = 0.06, Win = 5;
	public double BallX = W / 2, BallY = H / 2, VelX, VelY;
	public double PaddleA = H / 2, PaddleB = H / 2;
	/// <summary>Seconds until the ball is served (after a point).</summary>
	public double Wait = 1.2;
	/// <summary>Who the next serve goes towards: +1 B, -1 A.</summary>
	public int ServeTo = 1;
}

public static class PongRules
{
	const double StartSpeed = 2.4, MaxSpeed = 6, SpeedUp = 1.06;

	/// <summary>Moves the game on; returns +1 when A scored, -1 when B scored, 0 otherwise.</summary>
	public static int Step(PongState s, double dt)
	{
		if (s.Wait > 0)
		{
			s.Wait -= dt;
			if (s.Wait <= 0)
			{
				var angle = (Random.Shared.NextDouble() - 0.5) * 0.9;
				s.VelX = Math.Cos(angle) * StartSpeed * s.ServeTo;
				s.VelY = Math.Sin(angle) * StartSpeed;
			}
			return 0;
		}
		s.BallX += s.VelX * dt;
		s.BallY += s.VelY * dt;
		if (s.BallY < PongState.Ball && s.VelY < 0 || s.BallY > PongState.H - PongState.Ball && s.VelY > 0) s.VelY = -s.VelY;
		Bounce(s, PongState.PaddleX, s.PaddleA, 1);
		Bounce(s, PongState.W - PongState.PaddleX, s.PaddleB, -1);
		if (s.BallX < -0.2) return Serve(s, -1);
		if (s.BallX > PongState.W + 0.2) return Serve(s, 1);
		return 0;
	}

	/// <summary>Off a paddle: the further from its middle, the steeper; a little faster every hit.</summary>
	static void Bounce(PongState s, double x, double paddle, int dir)
	{
		if (Math.Sign(s.VelX) == dir || Math.Abs(s.BallX - x) > PongState.Ball + 0.04) return;
		var off = (s.BallY - paddle) / (PongState.PaddleH / 2 + PongState.Ball);
		if (Math.Abs(off) > 1) return;
		var speed = Math.Min(MaxSpeed, Math.Sqrt(s.VelX * s.VelX + s.VelY * s.VelY) * SpeedUp);
		var angle = off * 1.0;
		s.VelX = Math.Cos(angle) * speed * dir;
		s.VelY = Math.Sin(angle) * speed;
		s.BallX = x + dir * (PongState.Ball + 0.04);
	}

	static int Serve(PongState s, int scorer)
	{
		s.BallX = PongState.W / 2;
		s.BallY = PongState.H / 2;
		s.VelX = s.VelY = 0;
		s.Wait = 1.0;
		s.ServeTo = -scorer;
		return scorer;
	}
}

// ---------- Billiard ----------

/// <summary>
/// A 2 × 1 table, the white ball (index 0) and seven others. Players take turns: potting a ball scores a point and you
/// go again; potting the white is a foul (it comes back, the other player's turn). When all seven are in, most points wins.
/// </summary>
public sealed class BilliardState
{
	public const double W = 2, H = 1, R = 0.03, Pocket = 0.065;
	public List<double> X = new(), Y = new(), VX = new(), VY = new();
	public List<bool> In = new();
	/// <summary>Whose shot it is: "A" or "B".</summary>
	public string Turn = "A";
	public bool Moving;

	public static readonly (double X, double Y)[] Pockets =
	{
		(0, 0), (W / 2, -0.01), (W, 0), (0, H), (W / 2, H + 0.01), (W, H),
	};

	public static BilliardState New()
	{
		var s = new BilliardState();
		void Add(double x, double y)
		{
			s.X.Add(x);
			s.Y.Add(y);
			s.VX.Add(0);
			s.VY.Add(0);
			s.In.Add(false);
		}
		Add(0.5, H / 2);
		// (a triangle of seven: 1, 2, then 4)
		var d = R * 2.05;
		var apex = 1.4;
		Add(apex, H / 2);
		Add(apex + d * 0.87, H / 2 - d / 2);
		Add(apex + d * 0.87, H / 2 + d / 2);
		for (var i = 0; i < 4; i++) Add(apex + d * 1.74, H / 2 + (i - 1.5) * d);
		return s;
	}
}

public static class BilliardRules
{
	const double MaxSpeed = 3.2, Friction = 0.55, Cushion = 0.85;

	public static void Shoot(BilliardState s, double angle, double power)
	{
		var speed = Math.Clamp(power, 0.05, 1) * MaxSpeed;
		s.VX[0] = Math.Cos(angle) * speed;
		s.VY[0] = Math.Sin(angle) * speed;
		s.Moving = true;
	}

	/// <summary>Moves the balls on; returns the balls potted in this step.</summary>
	public static List<int> Step(BilliardState s, double dt)
	{
		var potted = new List<int>();
		var n = s.X.Count;
		for (var i = 0; i < n; i++)
		{
			if (s.In[i]) continue;
			s.X[i] += s.VX[i] * dt;
			s.Y[i] += s.VY[i] * dt;
			var speed = Math.Sqrt(s.VX[i] * s.VX[i] + s.VY[i] * s.VY[i]);
			if (speed > 0)
			{
				var slower = Math.Max(0, speed - Friction * dt) / speed;
				s.VX[i] *= slower;
				s.VY[i] *= slower;
			}
			if (BilliardState.Pockets.Any(p => (s.X[i] - p.X) * (s.X[i] - p.X) + (s.Y[i] - p.Y) * (s.Y[i] - p.Y) < BilliardState.Pocket * BilliardState.Pocket))
			{
				s.In[i] = true;
				s.VX[i] = s.VY[i] = 0;
				potted.Add(i);
				continue;
			}
			if (s.X[i] < BilliardState.R && s.VX[i] < 0 || s.X[i] > BilliardState.W - BilliardState.R && s.VX[i] > 0) s.VX[i] = -s.VX[i] * Cushion;
			if (s.Y[i] < BilliardState.R && s.VY[i] < 0 || s.Y[i] > BilliardState.H - BilliardState.R && s.VY[i] > 0) s.VY[i] = -s.VY[i] * Cushion;
			s.X[i] = Math.Clamp(s.X[i], BilliardState.R, BilliardState.W - BilliardState.R);
			s.Y[i] = Math.Clamp(s.Y[i], BilliardState.R, BilliardState.H - BilliardState.R);
		}
		// (equal balls: an elastic hit swaps their speeds along the line between them)
		for (var i = 0; i < n; i++)
		for (var j = i + 1; j < n; j++)
		{
			if (s.In[i] || s.In[j]) continue;
			double dx = s.X[j] - s.X[i], dy = s.Y[j] - s.Y[i];
			var dist = Math.Sqrt(dx * dx + dy * dy);
			if (dist >= BilliardState.R * 2 || dist == 0) continue;
			double nx = dx / dist, ny = dy / dist;
			var overlap = BilliardState.R * 2 - dist;
			s.X[i] -= nx * overlap / 2;
			s.Y[i] -= ny * overlap / 2;
			s.X[j] += nx * overlap / 2;
			s.Y[j] += ny * overlap / 2;
			var along = (s.VX[i] - s.VX[j]) * nx + (s.VY[i] - s.VY[j]) * ny;
			if (along <= 0) continue;
			s.VX[i] -= along * nx;
			s.VY[i] -= along * ny;
			s.VX[j] += along * nx;
			s.VY[j] += along * ny;
		}
		s.Moving = Enumerable.Range(0, n).Any(i => !s.In[i] && (Math.Abs(s.VX[i]) > 0.002 || Math.Abs(s.VY[i]) > 0.002));
		if (!s.Moving)
			for (var i = 0; i < n; i++) s.VX[i] = s.VY[i] = 0;
		return potted;
	}

	/// <summary>The white ball back on the table after it was potted (where it started, or next to it if that's taken).</summary>
	public static void ReturnWhite(BilliardState s)
	{
		s.In[0] = false;
		s.X[0] = 0.5;
		s.Y[0] = BilliardState.H / 2;
		for (var tries = 0; tries < 20 && Enumerable.Range(1, s.X.Count - 1).Any(i => !s.In[i]
			&& Math.Pow(s.X[i] - s.X[0], 2) + Math.Pow(s.Y[i] - s.Y[0], 2) < Math.Pow(BilliardState.R * 2.2, 2)); tries++)
			s.Y[0] += BilliardState.R * 2.5 * (tries % 2 == 0 ? 1 : -1) * (tries / 2 + 1);
	}
}
