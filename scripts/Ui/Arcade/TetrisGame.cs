// Tetris for the lobby's mini games, alone and on your own PC: a 10 × 20 well, the seven pieces in random bags, line
// clears for points (more at once, and at higher levels, for more), a little faster every 10 lines. When it's over the
// score goes to the host (best per player in this lobby, ArcadeView.TetrisBest).

using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using QuizNight.Shared;

namespace QuizNight.Ui.Arcade;

public partial class TetrisGame : Control
{
	const int Cols = 10, Rows = 20;

	static readonly Vector2I[][] Shapes =
	{
		new Vector2I[] { new(0, 1), new(1, 1), new(2, 1), new(3, 1) }, // I
		new Vector2I[] { new(0, 0), new(0, 1), new(1, 1), new(2, 1) }, // J
		new Vector2I[] { new(2, 0), new(0, 1), new(1, 1), new(2, 1) }, // L
		new Vector2I[] { new(1, 0), new(2, 0), new(1, 1), new(2, 1) }, // O
		new Vector2I[] { new(1, 0), new(2, 0), new(0, 1), new(1, 1) }, // S
		new Vector2I[] { new(1, 0), new(0, 1), new(1, 1), new(2, 1) }, // T
		new Vector2I[] { new(0, 0), new(1, 0), new(1, 1), new(2, 1) }, // Z
	};

	static readonly Color[] Colours =
	{
		new("#5cc8ff"), new("#4a6cff"), new("#ff9f43"), new("#ffc93c"), new("#a8e05f"), new("#9d8cff"), new("#ff6b7a"),
	};

	readonly int[,] _well = new int[Cols, Rows];
	readonly Action<int> _over;
	readonly List<int> _bag = new();
	Vector2I[] _piece = Array.Empty<Vector2I>();
	int _kind, _next;
	Vector2I _at;
	double _fall;
	int _score, _lines;
	bool _done, _sent;

	int Level => _lines / 10 + 1;
	double FallSec => Math.Max(0.06, 0.8 * Math.Pow(0.85, Level - 1));

	public TetrisGame() : this(_ => { }) { }

	public TetrisGame(Action<int> over)
	{
		_over = over;
		FocusMode = FocusModeEnum.All;
		CustomMinimumSize = new Vector2(0, 420);
		_next = FromBag();
		Spawn();
	}

	int FromBag()
	{
		if (_bag.Count == 0) _bag.AddRange(Enumerable.Range(0, 7).OrderBy(_ => Random.Shared.Next()));
		var k = _bag[0];
		_bag.RemoveAt(0);
		return k;
	}

	void Spawn()
	{
		_kind = _next;
		_next = FromBag();
		_piece = Shapes[_kind].ToArray();
		_at = new Vector2I(3, 0);
		if (!Fits(_piece, _at)) End();
	}

	/// <summary>Leaving early still counts the score so far.</summary>
	public void Finish()
	{
		if (!_done) End();
	}

	void End()
	{
		_done = true;
		if (!_sent && _score > 0) _over(_score);
		_sent = true;
		QueueRedraw();
	}

	bool Fits(Vector2I[] cells, Vector2I at) => cells.All(c =>
	{
		var p = c + at;
		return p.X >= 0 && p.X < Cols && p.Y < Rows && (p.Y < 0 || _well[p.X, p.Y] == 0);
	});

	void Move(int dx, int dy)
	{
		var to = _at + new Vector2I(dx, dy);
		if (Fits(_piece, to)) _at = to;
		else if (dy > 0) Lock();
	}

	void Turn(int dir)
	{
		if (_kind == 3) return; // (the O doesn't turn)
		var size = _kind == 0 ? 3 : 2;
		var turned = _piece.Select(c => dir > 0 ? new Vector2I(size - c.Y, c.X) : new Vector2I(c.Y, size - c.X)).ToArray();
		foreach (var kick in new[] { 0, -1, 1, -2, 2 })
			if (Fits(turned, _at + new Vector2I(kick, 0)))
			{
				_piece = turned;
				_at += new Vector2I(kick, 0);
				return;
			}
	}

	void Lock()
	{
		foreach (var c in _piece)
		{
			var p = c + _at;
			if (p.Y >= 0) _well[p.X, p.Y] = _kind + 1;
		}
		var cleared = 0;
		for (var y = Rows - 1; y >= 0; y--)
		{
			if (Enumerable.Range(0, Cols).Any(x => _well[x, y] == 0)) continue;
			cleared++;
			for (var yy = y; yy > 0; yy--)
			for (var x = 0; x < Cols; x++) _well[x, yy] = _well[x, yy - 1];
			for (var x = 0; x < Cols; x++) _well[x, 0] = 0;
			y++;
		}
		_score += new[] { 0, 100, 300, 500, 800 }[cleared] * Level;
		_lines += cleared;
		Spawn();
	}

	public override void _Process(double delta)
	{
		// (paused while it doesn't have the keyboard: "Click to play")
		if (_done || !IsVisibleInTree() || !HasFocus()) return;
		_fall += delta;
		if (_fall >= FallSec)
		{
			_fall = 0;
			Move(0, 1);
		}
		QueueRedraw();
	}

	public override void _GuiInput(InputEvent e)
	{
		if (e is InputEventMouseButton { Pressed: true }) GrabFocus();
		if (_done || e is not InputEventKey { Pressed: true } key) return;
		switch (key.Keycode)
		{
			case Key.Left: Move(-1, 0); break;
			case Key.Right: Move(1, 0); break;
			case Key.Down:
				Move(0, 1);
				_score++;
				break;
			case Key.Up or Key.X: Turn(1); break;
			case Key.Z: Turn(-1); break;
			case Key.Space:
				while (Fits(_piece, _at + new Vector2I(0, 1)))
				{
					_at += new Vector2I(0, 1);
					_score += 2;
				}
				Lock();
				break;
			default: return;
		}
		AcceptEvent();
		QueueRedraw();
	}

	public override void _Draw()
	{
		var cell = Mathf.Floor(Mathf.Min(Size.Y / Rows, (Size.X - 180) / Cols));
		var well = new Vector2(cell * Cols, cell * Rows);
		var origin = new Vector2((Size.X - well.X - 160) / 2, (Size.Y - well.Y) / 2);
		DrawRect(new Rect2(origin - Vector2.One * 3, well + Vector2.One * 6), C.Border);
		DrawRect(new Rect2(origin, well), C.Well);
		void Cell(Vector2 at, Color colour)
		{
			DrawRect(new Rect2(at + Vector2.One, Vector2.One * (cell - 2)), colour);
			DrawRect(new Rect2(at + Vector2.One, new Vector2(cell - 2, 3)), colour.Lightened(0.3f));
		}
		for (var x = 0; x < Cols; x++)
		for (var y = 0; y < Rows; y++)
			if (_well[x, y] > 0) Cell(origin + new Vector2(x, y) * cell, Colours[_well[x, y] - 1]);
		if (!_done)
		{
			// (where it would land, faintly)
			var ghost = _at;
			while (Fits(_piece, ghost + new Vector2I(0, 1))) ghost += new Vector2I(0, 1);
			foreach (var c in _piece)
				if ((c + ghost).Y >= 0) DrawRect(new Rect2(origin + (Vector2)(c + ghost) * cell + Vector2.One * 2, Vector2.One * (cell - 4)), new Color(Colours[_kind], 0.25f));
			foreach (var c in _piece)
				if ((c + _at).Y >= 0) Cell(origin + (Vector2)(c + _at) * cell, Colours[_kind]);
		}
		var side = origin + new Vector2(well.X + 24, 0);
		var font = Fonts.Bold;
		DrawString(font, side + new Vector2(0, 20), Lang.T("Next"), HorizontalAlignment.Left, -1, 14, C.Muted);
		foreach (var c in Shapes[_next]) Cell(side + new Vector2(0, 32) + (Vector2)c * cell * 0.8f, Colours[_next]);
		DrawString(font, side + new Vector2(0, 130), Lang.T("Score"), HorizontalAlignment.Left, -1, 14, C.Muted);
		DrawString(Fonts.Black, side + new Vector2(0, 158), $"{_score:N0}", HorizontalAlignment.Left, -1, 24, C.Text);
		DrawString(font, side + new Vector2(0, 196), Lang.F("Lines {0} · Level {1}", _lines, Level), HorizontalAlignment.Left, -1, 14, C.Muted);
		if (_done) DrawString(Fonts.Black, origin + new Vector2(0, well.Y / 2), Lang.T("Game over"), HorizontalAlignment.Center, well.X, 30, C.Accent);
		else if (!HasFocus()) DrawString(font, origin + new Vector2(0, well.Y / 2), Lang.T("Click to play"), HorizontalAlignment.Center, well.X, 18, C.Text);
	}
}
