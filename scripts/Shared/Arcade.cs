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

	/// <summary>The 1 vs 1 games: Pong and every billiard game (BilliardMode; "billiard" itself is quick pool).</summary>
	public static readonly string[] Versus = new[] { Pong }.Concat(BilliardMode.All).ToArray();

	public static string Label(string game) => game switch
	{
		Tetris => "Tetris", Pong => "Pong", _ when BilliardMode.Is(game) => BilliardMode.Label(game), _ => game,
	};

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

/// <summary>The billiard games: pool with pockets (Quick, 8-ball, 9-ball) and carom without (4-ball, three-cushion).</summary>
public static class BilliardMode
{
	/// <summary>Seven balls, a point for each one you pot; most points when they're all in wins.</summary>
	public const string Quick = "billiard";
	/// <summary>Solids or stripes (the first ball you pot decides), then the black; the black too early loses.</summary>
	public const string Eight = "8ball";
	/// <summary>Always hit the lowest ball first; whoever pots the 9 in a fair shot wins.</summary>
	public const string Nine = "9ball";
	/// <summary>Yotsudama: no pockets; your cue ball hitting two reds is 3, a red and the other cue ball 2, all three 5. First to 20.</summary>
	public const string Four = "4ball";
	/// <summary>No pockets; your cue ball hits both others with three cushions before the second: a point. First to 3.</summary>
	public const string Three = "3cushion";

	public static readonly string[] All = { Quick, Eight, Nine, Four, Three };

	public static bool Is(string game) => All.Contains(game);
	public static bool Carom(string mode) => mode is Four or Three;

	public static string Label(string mode) => mode switch
	{
		Quick => "Quick pool",
		Eight => "8-ball",
		Nine => "9-ball",
		Four => "4-ball",
		Three => "Three-cushion",
		_ => mode,
	};

	public static string Rules(string mode) => mode switch
	{
		Quick => "Seven balls. Pot one: a point and you go again. When they're all in, most points wins.",
		Eight => "Pot one: that's your group (solids 1–7 or stripes 9–15). Clear yours, then pot the black. The black too early loses.",
		Nine => "Always hit the lowest ball on the table first. Pot any ball in a fair shot and go again; pot the 9 in a fair shot and win.",
		Four => "No pockets. Your cue ball hits both reds: 3 points, a red and the other cue ball: 2, all three: 5. Score and go again. First to 20.",
		Three => "No pockets. Your cue ball hits both other balls and three cushions before the second one: a point, and you go again. First to 3.",
		_ => "",
	};
}

/// <summary>A 2 × 1 table: where the balls are, whose shot it is, and what the shot under way has done so far.</summary>
public sealed class BilliardState
{
	public const double W = 2, H = 1, R = 0.03, Pocket = 0.065;
	public string Mode = BilliardMode.Quick;
	public List<double> X = new(), Y = new(), VX = new(), VY = new();
	public List<bool> In = new();
	/// <summary>Whose shot it is: "A" or "B".</summary>
	public string Turn = "A";
	public bool Moving;
	/// <summary>8-ball: A's group, "solids" or "stripes" ("" while the table is open).</summary>
	public string GroupA = "";
	/// <summary>The ball being shot (pool: the white, 0; carom: 0 for A, 1 for B).</summary>
	public int Cue;
	/// <summary>This shot so far: the balls the cue ball touched (in order), cushions before the second one, balls potted.</summary>
	public List<int> Touched = new();
	public int CushionsBeforeSecond;
	public List<int> Potted = new();
	/// <summary>9-ball: the lowest ball on the table when the shot was taken.</summary>
	public int Lowest;

	public bool Pockets => !BilliardMode.Carom(Mode);

	public static readonly (double X, double Y)[] PocketSpots =
	{
		(0, 0), (W / 2, -0.01), (W, 0), (0, H), (W / 2, H + 0.01), (W, H),
	};

	public static BilliardState New(string mode)
	{
		var s = new BilliardState { Mode = mode };
		void Add(double x, double y)
		{
			s.X.Add(x);
			s.Y.Add(y);
			s.VX.Add(0);
			s.VY.Add(0);
			s.In.Add(false);
		}
		var d = R * 2.05;
		var apex = 1.4;
		// (a rack, row by row from the apex; the numbers in the order they go in)
		void Rack(int[] rows, IList<int> numbers)
		{
			var placed = new (double X, double Y)[numbers.Count + 1];
			var k = 0;
			for (var row = 0; row < rows.Length; row++)
			for (var i = 0; i < rows[row]; i++)
				placed[numbers[k++]] = (apex + row * d * 0.87, H / 2 + (i - (rows[row] - 1) / 2.0) * d);
			for (var n = 1; n <= numbers.Count; n++) Add(placed[n].X, placed[n].Y);
		}
		var rng = Random.Shared;
		switch (mode)
		{
			case BilliardMode.Eight:
			{
				Add(0.5, H / 2);
				// (the black in the middle of the third row; the rest shuffled)
				var others = Enumerable.Range(1, 15).Where(n => n != 8).OrderBy(_ => rng.Next()).ToList();
				others.Insert(4, 8);
				Rack(new[] { 1, 2, 3, 4, 5 }, others);
				break;
			}
			case BilliardMode.Nine:
			{
				Add(0.5, H / 2);
				// (a diamond: the 1 at the front, the 9 in the middle)
				var others = Enumerable.Range(2, 7).OrderBy(_ => rng.Next()).ToList();
				others.Insert(0, 1);
				others.Insert(4, 9);
				Rack(new[] { 1, 2, 3, 2, 1 }, others);
				break;
			}
			case BilliardMode.Four:
				Add(0.5, H / 2 + 0.12); // white (A)
				Add(0.5, H / 2 - 0.12); // yellow (B)
				Add(1.5, H / 2); // red
				Add(1.0, H / 2); // red
				break;
			case BilliardMode.Three:
				Add(0.5, H / 2 + 0.15); // white (A)
				Add(0.5, H / 2); // yellow (B)
				Add(1.5, H / 2); // red
				break;
			default:
				Add(0.5, H / 2);
				Rack(new[] { 1, 2, 4 }, Enumerable.Range(1, 7).ToList());
				break;
		}
		return s;
	}
}

public static class BilliardRules
{
	const double MaxSpeed = 3.2, Friction = 0.55, Cushion = 0.85;

	/// <summary>The ball the shooter plays: the white in pool; in carom A's white or B's yellow.</summary>
	public static int CueFor(BilliardState s) => s.Pockets ? 0 : s.Turn == "A" ? 0 : 1;

	public static void Shoot(BilliardState s, double angle, double power)
	{
		s.Cue = CueFor(s);
		s.Touched.Clear();
		s.Potted.Clear();
		s.CushionsBeforeSecond = 0;
		s.Lowest = Enumerable.Range(1, s.X.Count - 1).Where(i => !s.In[i]).DefaultIfEmpty(0).Min();
		var speed = Math.Clamp(power, 0.05, 1) * MaxSpeed;
		s.VX[s.Cue] = Math.Cos(angle) * speed;
		s.VY[s.Cue] = Math.Sin(angle) * speed;
		s.Moving = true;
	}

	/// <summary>Moves the balls on, noting what the cue ball touches and what drops.</summary>
	public static void Step(BilliardState s, double dt)
	{
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
			if (s.Pockets && BilliardState.PocketSpots.Any(p => (s.X[i] - p.X) * (s.X[i] - p.X) + (s.Y[i] - p.Y) * (s.Y[i] - p.Y) < BilliardState.Pocket * BilliardState.Pocket))
			{
				s.In[i] = true;
				s.VX[i] = s.VY[i] = 0;
				s.Potted.Add(i);
				continue;
			}
			var cushion = false;
			if (s.X[i] < BilliardState.R && s.VX[i] < 0 || s.X[i] > BilliardState.W - BilliardState.R && s.VX[i] > 0)
			{
				s.VX[i] = -s.VX[i] * Cushion;
				cushion = true;
			}
			if (s.Y[i] < BilliardState.R && s.VY[i] < 0 || s.Y[i] > BilliardState.H - BilliardState.R && s.VY[i] > 0)
			{
				s.VY[i] = -s.VY[i] * Cushion;
				cushion = true;
			}
			if (cushion && i == s.Cue && s.Touched.Count < 2) s.CushionsBeforeSecond++;
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
			if (i == s.Cue && !s.Touched.Contains(j)) s.Touched.Add(j);
			if (j == s.Cue && !s.Touched.Contains(i)) s.Touched.Add(i);
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
	}

	public static bool Solid(int ball) => ball is >= 1 and <= 7;
	public static bool Stripe(int ball) => ball is >= 9 and <= 15;

	/// <summary>8-ball: a side's group ("" while the table is open).</summary>
	public static string GroupOf(BilliardState s, string side) =>
		s.GroupA == "" ? "" : side == "A" ? s.GroupA : s.GroupA == "solids" ? "stripes" : "solids";

	static bool InGroup(int ball, string group) => group == "solids" ? Solid(ball) : group == "stripes" && Stripe(ball);

	/// <summary>
	/// The balls the shooter should aim at: 9-ball the lowest, 8-ball their group (then the black), carom and quick pool
	/// any ball but their own cue ball.
	/// </summary>
	public static IEnumerable<int> Targets(BilliardState s)
	{
		var cue = CueFor(s);
		var on = Enumerable.Range(0, s.X.Count).Where(i => i != cue && !s.In[i] && !(s.Pockets && i == 0)).ToList();
		switch (s.Mode)
		{
			case BilliardMode.Nine:
				return on.Count > 0 ? new[] { on.Min() } : on;
			case BilliardMode.Eight:
				var group = GroupOf(s, s.Turn);
				var mine = on.Where(i => group == "" ? i != 8 : InGroup(i, group)).ToList();
				return mine.Count > 0 ? mine : on.Where(i => i == 8);
			default:
				return on;
		}
	}

	/// <summary>
	/// A shot has come to rest: points, fouls, whose turn it is next. Returns the winner ("A" / "B") if that decided it.
	/// </summary>
	public static string? Settle(BilliardState s, ref int scoreA, ref int scoreB)
	{
		var shooter = s.Turn;
		var other = shooter == "A" ? "B" : "A";
		var points = 0;
		var keep = false;
		string? winner = null;
		var cueIn = s.Pockets && s.Potted.Contains(0);
		switch (s.Mode)
		{
			case BilliardMode.Eight:
			{
				var group = GroupOf(s, shooter);
				var clearedBefore = group != "" && Enumerable.Range(1, 15).Where(i => InGroup(i, group)).All(i => s.In[i] && !s.Potted.Contains(i));
				var first = s.Touched.Count > 0 ? s.Touched[0] : -1;
				var foul = cueIn || first < 0 || first == 8 && !clearedBefore || group != "" && first != 8 && !InGroup(first, group);
				if (s.Potted.Contains(8))
				{
					winner = !foul && clearedBefore ? shooter : other;
					break;
				}
				var firstIn = s.Potted.FirstOrDefault(i => i != 0);
				if (group == "" && !foul && firstIn > 0)
				{
					var mineNow = Solid(firstIn) ? "solids" : "stripes";
					s.GroupA = shooter == "A" ? mineNow : mineNow == "solids" ? "stripes" : "solids";
					group = mineNow;
				}
				points = s.Potted.Count(i => group != "" && InGroup(i, group));
				keep = !foul && points > 0;
				break;
			}
			case BilliardMode.Nine:
			{
				var foul = cueIn || s.Touched.Count == 0 || s.Touched[0] != s.Lowest;
				points = foul ? 0 : s.Potted.Count(i => i != 0);
				if (s.Potted.Contains(9))
				{
					if (!foul)
					{
						winner = shooter;
						break;
					}
					// (the 9 potted in a foul comes back on its spot)
					s.In[9] = false;
					s.X[9] = 1.5;
					s.Y[9] = BilliardState.H / 2;
				}
				keep = !foul && points > 0;
				break;
			}
			case BilliardMode.Four:
			{
				var otherCue = s.Cue == 0 ? 1 : 0;
				var reds = s.Touched.Count(i => i >= 2);
				var hitCue = s.Touched.Contains(otherCue);
				points = reds == 2 && hitCue ? 5 : reds == 2 ? 3 : reds == 1 && hitCue ? 2 : 0;
				keep = points > 0;
				break;
			}
			case BilliardMode.Three:
				points = s.Touched.Count >= 2 && s.CushionsBeforeSecond >= 3 ? 1 : 0;
				keep = points > 0;
				break;
			default:
				points = s.Potted.Count(i => i != 0);
				keep = !cueIn && points > 0;
				break;
		}
		if (shooter == "A") scoreA += points;
		else scoreB += points;
		if (cueIn) ReturnWhite(s);
		if (winner != null) return winner;
		var target = s.Mode == BilliardMode.Four ? 20 : s.Mode == BilliardMode.Three ? 3 : int.MaxValue;
		if (scoreA >= target) return "A";
		if (scoreB >= target) return "B";
		if (s.Mode == BilliardMode.Quick && s.In.Skip(1).All(i => i)) return scoreA > scoreB ? "A" : "B";
		if (!keep) s.Turn = other;
		return null;
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
