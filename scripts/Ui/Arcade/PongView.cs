// Pong in the lobby's mini games: the host runs the ball (ArcadeHost), this draws it. Your own paddle follows your mouse
// (or ↑ ↓) straight away and goes to the host ~30 times a second; the ball is moved on between the host's frames by its
// speed, so it glides even over Steam.

using System;
using Godot;
using QuizNight.Shared;

namespace QuizNight.Ui.Arcade;

public partial class PongView : Control
{
	readonly ArcadeLink _link;
	ArcadeFrame _frame;
	double _since;
	double _mine = PongState.H / 2;
	double _sentMine = -1, _sendIn;
	readonly bool _left;

	public PongView() : this(null!, new ArcadeFrame()) { }

	public PongView(ArcadeLink link, ArcadeFrame first)
	{
		_link = link;
		_frame = first;
		_left = first.A == link.MyId;
		FocusMode = FocusModeEnum.All;
		CustomMinimumSize = new Vector2(0, 420);
	}

	public void Take(ArcadeFrame frame)
	{
		_frame = frame;
		_since = 0;
		QueueRedraw();
	}

	Rect2 Field()
	{
		var scale = Mathf.Min(Size.X / (float)PongState.W, (Size.Y - 40) / (float)PongState.H);
		var size = new Vector2((float)PongState.W, (float)PongState.H) * scale;
		return new Rect2(new Vector2((Size.X - size.X) / 2, 40), size);
	}

	public override void _GuiInput(InputEvent e)
	{
		if (e is InputEventMouseMotion motion)
		{
			var f = Field();
			_mine = Math.Clamp((motion.Position.Y - f.Position.Y) / f.Size.Y * PongState.H, 0, PongState.H);
		}
	}

	public override void _Process(double delta)
	{
		_since += delta;
		if (Input.IsKeyPressed(Key.Up)) _mine = Math.Max(0, _mine - 3.5 * delta);
		if (Input.IsKeyPressed(Key.Down)) _mine = Math.Min(PongState.H, _mine + 3.5 * delta);
		_sendIn -= delta;
		if (_sendIn <= 0 && Math.Abs(_mine - _sentMine) > 0.001)
		{
			_sendIn = 1 / 30.0;
			_sentMine = _mine;
			_link.Send(new ArcadeRequest { Kind = ArcadeRequest.Paddle, Value = _mine / PongState.H });
		}
		QueueRedraw();
	}

	public override void _Draw()
	{
		if (_frame.Pong is not { } s) return;
		var f = Field();
		var k = f.Size.X / (float)PongState.W;
		Vector2 At(double x, double y) => f.Position + new Vector2((float)x, (float)y) * k;
		DrawRect(f, C.Well);
		for (var y = 0f; y < f.Size.Y; y += 18) DrawRect(new Rect2(f.Position.X + f.Size.X / 2 - 2, f.Position.Y + y, 4, 9), new Color(C.Muted, 0.4f));
		// (mine where my mouse is, the other's where the host says)
		var a = _left ? _mine : s.PaddleA;
		var b = _left ? s.PaddleB : _mine;
		var paddle = new Vector2(0.08f * k, (float)PongState.PaddleH * k);
		DrawRect(new Rect2(At(PongState.PaddleX, a) - new Vector2(paddle.X, paddle.Y / 2), paddle), _left ? C.Accent : C.Text);
		DrawRect(new Rect2(At(PongState.W - PongState.PaddleX, b) - new Vector2(0, paddle.Y / 2), paddle), _left ? C.Text : C.Accent);
		if (s.Wait <= 0)
		{
			var x = s.BallX + s.VelX * _since;
			var y = Math.Clamp(s.BallY + s.VelY * _since, PongState.Ball, PongState.H - PongState.Ball);
			DrawCircle(At(x, y), (float)PongState.Ball * k, C.Gold);
		}
		var font = Fonts.Black;
		DrawString(font, new Vector2(f.Position.X, 30), $"{_link.Name(_frame.A)}  {_frame.ScoreA}", HorizontalAlignment.Left, f.Size.X / 2 - 20, 22, C.Text);
		DrawString(font, new Vector2(f.Position.X + f.Size.X / 2 + 20, 30), $"{_frame.ScoreB}  {_link.Name(_frame.B)}", HorizontalAlignment.Right, f.Size.X / 2 - 20, 22, C.Text);
	}
}
