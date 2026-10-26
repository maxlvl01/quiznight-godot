// Billiard in the lobby's mini games: the host rolls the balls (ArcadeHost), this draws the table. On your turn, press on
// the table and pull back from the white ball like a slingshot: the line shows where it goes and how hard; let go to shoot.

using System;
using System.Linq;
using Godot;
using QuizNight.Shared;

namespace QuizNight.Ui.Arcade;

public partial class BilliardView : Control
{
	readonly ArcadeLink _link;
	ArcadeFrame _frame;
	double _since;
	Vector2? _drag;

	static readonly Color Cloth = new("#1f7a4d"), CaromCloth = new("#2a5fa8"), Rail = new("#5a3a22");
	/// <summary>Pool balls 1–8 (9–15 are the same colours as stripes): yellow, blue, red, purple, orange, green, maroon, black.</summary>
	static readonly Color[] Pool =
	{
		Colors.White, new("#f5c518"), new("#2a5bd7"), new("#e0302a"), new("#6b3fa0"), new("#f07d1a"), new("#1f8a4c"), new("#8a2232"), new("#14151b"),
	};
	static readonly Color Yellow = new("#f5c518"), Red = new("#e0302a");

	/// <summary>A ball's colour, whether it's a stripe, and its number ("" for cue and carom balls).</summary>
	static (Color Colour, bool Stripe, string Number) Look(string mode, int i) => BilliardMode.Carom(mode)
		? (i == 0 ? Colors.White : i == 1 ? Yellow : Red, false, "")
		: i == 0 ? (Colors.White, false, "")
		: i <= 8 ? (Pool[i], false, i.ToString())
		: (Pool[i - 8], true, i.ToString());

	const float MaxPull = 0.5f;

	public BilliardView() : this(null!, new ArcadeFrame()) { }

	public BilliardView(ArcadeLink link, ArcadeFrame first)
	{
		_link = link;
		_frame = first;
		FocusMode = FocusModeEnum.All;
		CustomMinimumSize = new Vector2(0, 420);
	}

	/// <summary>Which billiard game this is (BilliardMode).</summary>
	public string Mode => _frame.Billiard?.Mode ?? _frame.Game;

	public void Take(ArcadeFrame frame)
	{
		_frame = frame;
		_since = 0;
		QueueRedraw();
	}

	bool MyTurn => _frame.Billiard is { Moving: false } b && (b.Turn == "A" ? _frame.A : _frame.B) == _link.MyId;

	Rect2 Table()
	{
		var scale = Mathf.Min((Size.X - 40) / (float)BilliardState.W, (Size.Y - 90) / (float)BilliardState.H);
		var size = new Vector2((float)BilliardState.W, (float)BilliardState.H) * scale;
		return new Rect2(new Vector2((Size.X - size.X) / 2, 60), size);
	}

	/// <summary>Where the shooter's ball is (pool: the white; carom: their own cue ball).</summary>
	Vector2 White()
	{
		var t = Table();
		var b = _frame.Billiard!;
		var cue = BilliardRules.CueFor(b);
		return t.Position + new Vector2((float)b.X[cue], (float)b.Y[cue]) * (t.Size.X / (float)BilliardState.W);
	}

	public override void _GuiInput(InputEvent e)
	{
		if (!MyTurn) return;
		switch (e)
		{
			case InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true } press:
				_drag = press.Position;
				break;
			case InputEventMouseMotion motion when _drag != null:
				_drag = motion.Position;
				QueueRedraw();
				break;
			case InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: false } release when _drag != null:
				var (angle, power) = Aim(release.Position);
				_drag = null;
				if (power > 0.03f) _link.Send(new ArcadeRequest { Kind = ArcadeRequest.Shot, Angle = angle, Power = power });
				QueueRedraw();
				break;
		}
	}

	/// <summary>Pulled back from the white ball: it goes the other way, the further the harder.</summary>
	(float Angle, float Power) Aim(Vector2 mouse)
	{
		var pull = White() - mouse;
		var tableUnits = pull.Length() / (Table().Size.X / (float)BilliardState.W);
		return (Mathf.Atan2(pull.Y, pull.X), Mathf.Clamp(tableUnits / MaxPull, 0, 1));
	}

	public override void _Process(double delta)
	{
		_since += delta;
		if (_frame.Billiard is { Moving: true }) QueueRedraw();
	}

	public override void _Draw()
	{
		if (_frame.Billiard is not { } s) return;
		var t = Table();
		var k = t.Size.X / (float)BilliardState.W;
		DrawRect(t.Grow(14), Rail);
		DrawRect(t, s.Pockets ? Cloth : CaromCloth);
		if (s.Pockets)
			foreach (var (px, py) in BilliardState.PocketSpots) DrawCircle(t.Position + new Vector2((float)px, (float)py) * k, (float)BilliardState.Pocket * k * 0.85f, new Color("#0b0b0f"));
		var r = (float)BilliardState.R * k;
		for (var i = 0; i < s.X.Count; i++)
		{
			if (s.In[i]) continue;
			// (moved on between the host's frames by its speed)
			var dt = s.Moving ? Math.Min(_since, 0.05) : 0;
			var at = t.Position + new Vector2((float)(s.X[i] + s.VX[i] * dt), (float)(s.Y[i] + s.VY[i] * dt)) * k;
			var (colour, stripe, number) = Look(s.Mode, i);
			DrawCircle(at + new Vector2(1.5f, 2), r, new Color(0, 0, 0, 0.35f));
			DrawCircle(at, r, stripe ? Colors.White : colour);
			if (stripe) DrawRect(new Rect2(at.X - r * 0.95f, at.Y - r * 0.5f, r * 1.9f, r), colour);
			if (number != "")
			{
				DrawCircle(at, r * 0.5f, Colors.White);
				DrawString(Fonts.Bold, at + new Vector2(-r * 0.5f, r * 0.3f), number, HorizontalAlignment.Center, r, Mathf.Max(7, (int)(r * 0.75f)), new Color("#14151b"));
			}
			DrawCircle(at - Vector2.One * r * 0.4f, r * 0.25f, new Color(1, 1, 1, 0.4f));
		}
		if (_drag is { } mouse && MyTurn)
		{
			var (angle, power) = Aim(mouse);
			var white = White();
			var dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
			DrawLine(white, white + dir * (60 + 360 * power), new Color(1, 1, 1, 0.7f), 2);
			DrawLine(white, mouse, new Color(C.Gold, 0.8f), 4);
		}
		var font = Fonts.Black;
		string Side(string id, int score, bool turn) => $"{(turn ? "▶ " : "")}{_link.Name(id)}  {score}";
		DrawString(font, new Vector2(t.Position.X, 34), Side(_frame.A, _frame.ScoreA, s.Turn == "A"), HorizontalAlignment.Left, t.Size.X / 2 - 10, 20, s.Turn == "A" ? C.Accent : C.Text);
		DrawString(font, new Vector2(t.Position.X + t.Size.X / 2 + 10, 34), Side(_frame.B, _frame.ScoreB, s.Turn == "B"), HorizontalAlignment.Right, t.Size.X / 2 - 10, 20, s.Turn == "B" ? C.Accent : C.Text);
		var line = s.Moving ? "" : MyTurn ? Lang.T("Your shot") : Lang.F("{0}'s shot", _link.Name(s.Turn == "A" ? _frame.A : _frame.B));
		var extra = s.Mode switch
		{
			BilliardMode.Eight => BilliardRules.GroupOf(s, "A") is { Length: > 0 } a
				? Lang.F("{0}: {1} · {2}: {3}", _link.Name(_frame.A), Lang.T(a), _link.Name(_frame.B), Lang.T(BilliardRules.GroupOf(s, "B")))
				: Lang.T("Table open: the first ball you pot is your group"),
			BilliardMode.Nine => BilliardRules.Targets(s).FirstOrDefault() is var low and > 0 ? Lang.F("Hit the {0} first", low) : "",
			BilliardMode.Four => Lang.T("Your ball: white for the left player, yellow for the right. First to 20."),
			BilliardMode.Three => Lang.T("Your ball: white for the left player, yellow for the right. First to 3."),
			_ => Lang.F("{0} balls left", s.In.Skip(1).Count(i => !i)),
		};
		DrawString(Fonts.Bold, new Vector2(t.Position.X, t.End.Y + 38), line + (line != "" && extra != "" ? " · " : "") + extra, HorizontalAlignment.Center, t.Size.X, 15, C.Muted);
	}
}
