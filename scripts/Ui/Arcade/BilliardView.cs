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

	static readonly Color Cloth = new("#1f7a4d"), Rail = new("#5a3a22");
	static readonly Color[] Balls =
	{
		Colors.White, new("#ffc93c"), new("#4a6cff"), new("#ff6b7a"), new("#9d8cff"), new("#ff9f43"), new("#3fd0b0"), new("#14151b"),
	};

	const float MaxPull = 0.5f;

	public BilliardView() : this(null!, new ArcadeFrame()) { }

	public BilliardView(ArcadeLink link, ArcadeFrame first)
	{
		_link = link;
		_frame = first;
		FocusMode = FocusModeEnum.All;
		CustomMinimumSize = new Vector2(0, 420);
	}

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

	Vector2 White()
	{
		var t = Table();
		var b = _frame.Billiard!;
		return t.Position + new Vector2((float)b.X[0], (float)b.Y[0]) * (t.Size.X / (float)BilliardState.W);
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
		DrawRect(t, Cloth);
		foreach (var (px, py) in BilliardState.Pockets) DrawCircle(t.Position + new Vector2((float)px, (float)py) * k, (float)BilliardState.Pocket * k, new Color("#0b0b0f"));
		for (var i = 0; i < s.X.Count; i++)
		{
			if (s.In[i]) continue;
			// (moved on between the host's frames by its speed)
			var dt = s.Moving ? Math.Min(_since, 0.05) : 0;
			var at = t.Position + new Vector2((float)(s.X[i] + s.VX[i] * dt), (float)(s.Y[i] + s.VY[i] * dt)) * k;
			DrawCircle(at + new Vector2(1.5f, 2), (float)BilliardState.R * k, new Color(0, 0, 0, 0.35f));
			DrawCircle(at, (float)BilliardState.R * k, Balls[i % Balls.Length]);
			DrawCircle(at - Vector2.One * (float)BilliardState.R * k * 0.35f, (float)BilliardState.R * k * 0.3f, new Color(1, 1, 1, 0.45f));
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
		var left = s.In.Skip(1).Count(i => !i);
		var line = s.Moving ? "" : MyTurn ? Lang.T("Your shot") : Lang.F("{0}'s shot", _link.Name(s.Turn == "A" ? _frame.A : _frame.B));
		DrawString(Fonts.Bold, new Vector2(t.Position.X, t.End.Y + 38), line + (line != "" ? " · " : "") + Lang.F("{0} balls left", left), HorizontalAlignment.Center, t.Size.X, 15, C.Muted);
	}
}
