// The Ewige Tabelle's graphs: everyone's place after each quiz night (like an F1 lap chart: lines that cross when
// someone overtakes), or their points climbing from 0. Click a name (on the chart or in the list beside it) to
// highlight that player; hover the chart for the night.

using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using QuizNight.Shared;

namespace QuizNight.Ui.Screens;

public partial class StandingsChart : Control
{
	public sealed class Series
	{
		public string Id = "";
		public string Name = "";
		public Color Color;
		/// <summary>Per night (index = night): their place (positions) or points so far; null before their first night.</summary>
		public double?[] Values = Array.Empty<double?>();
	}

	readonly List<Series> _series;
	/// <summary>Per night: "3 · 12.12.2026 · Gaming Night" (the hover text).</summary>
	readonly List<string> _nights;
	readonly bool _positions;
	readonly HashSet<string> _highlight = new();
	/// <summary>Where each name label was drawn (to click it).</summary>
	readonly List<(Rect2 Rect, string Id)> _labels = new();
	public event Action? HighlightChanged;

	const float Left = 120, Right = 130, Top = 16, Bottom = 34;

	/// <summary>A line's colour by its place in the list: all different (players' own colours repeat too often).</summary>
	public static Color Colour(int i)
	{
		Color[] palette =
		{
			C.Gold, C.Sky, C.Coral, C.Teal, C.Purple, C.Lime, C.Pink, new("#ff9f43"), C.Silver, new("#4d7cff"),
			new("#e0e070"), new("#ff5ea0"), new("#6fffd2"), C.Bronze, new("#b98cff"), new("#8fd0ff"),
		};
		return palette[i % palette.Length];
	}

	public StandingsChart() : this(new(), new(), true) { }

	public StandingsChart(List<Series> series, List<string> nights, bool positions)
	{
		_series = series;
		_nights = nights;
		_positions = positions;
		CustomMinimumSize = new Vector2(0, positions ? Math.Max(320, 34 * Math.Max(1, series.Count) + Top + Bottom) : 440);
		// (for screen readers: the standings after the last night or question)
		var last = Math.Max(0, (series.FirstOrDefault()?.Values.Length ?? 1) - 1);
		AccessibilityName = Lang.T(positions ? "Positions graph" : "Points graph");
		AccessibilityDescription = string.Join(", ", series.Where(s => s.Values.Length > last && s.Values[last] != null)
			.OrderBy(s => positions ? s.Values[last] : -s.Values[last]).Select(s => $"{s.Name} {s.Values[last]:0}"));
		MouseFilter = MouseFilterEnum.Stop;
		MouseDefaultCursorShape = CursorShape.Arrow;
	}

	public IReadOnlyCollection<string> Highlighted => _highlight;

	public void Toggle(string? id)
	{
		if (id == null) _highlight.Clear();
		else if (!_highlight.Remove(id)) _highlight.Add(id);
		QueueRedraw();
		HighlightChanged?.Invoke();
	}

	/// <summary>Points: the first column is "before the first night", where everyone has 0.</summary>
	int Columns => _nights.Count + (_positions ? 0 : 1);

	double MaxValue => _positions
		? Math.Max(2, _series.Count)
		: Math.Max(1, _series.SelectMany(s => s.Values).Where(v => v != null).Select(v => v!.Value).DefaultIfEmpty(1).Max());

	float X(int column) => Left + (Columns <= 1 ? 0 : (Size.X - Left - Right) * column / (Columns - 1));

	float Y(double value)
	{
		var h = Size.Y - Top - Bottom;
		return _positions
			? Top + (float)((value - 1) / (MaxValue - 1)) * h
			: Top + h - (float)(value / NiceMax()) * h;
	}

	/// <summary>Points: a round top for the scale (10, 25, 50, 100 …).</summary>
	double NiceMax()
	{
		var max = MaxValue;
		var step = Step(max);
		return Math.Ceiling(max / step) * step;
	}

	static double Step(double max)
	{
		var raw = max / 5;
		var mag = Math.Pow(10, Math.Floor(Math.Log10(Math.Max(raw, 1))));
		return new[] { 1, 2, 2.5, 5, 10 }.Select(m => m * mag).First(s => s >= raw);
	}

	/// <summary>The value a series has in a chart column (points: column 0 is 0 for everyone).</summary>
	double? At(Series s, int column) => _positions ? s.Values[column] : column == 0 ? 0 : s.Values[column - 1];

	public override void _Draw()
	{
		_labels.Clear();
		var font = Fonts.Mono;
		var grid = C.Border with { A = 0.35f };
		var plotRight = Size.X - Right;
		// (grid: a line per place or per step of points, and one per night)
		if (_positions)
			for (var p = 1; p <= (int)MaxValue; p++)
				DrawLine(new Vector2(Left, Y(p)), new Vector2(plotRight, Y(p)), grid, 1);
		else
		{
			var step = Step(MaxValue);
			for (var v = 0.0; v <= NiceMax() + 0.001; v += step)
			{
				DrawLine(new Vector2(Left, Y(v)), new Vector2(plotRight, Y(v)), grid, 1);
				DrawString(font, new Vector2(Left - 46, Y(v) + 5), v.ToString("0", System.Globalization.CultureInfo.InvariantCulture), HorizontalAlignment.Right, 40, 12, C.Muted);
			}
		}
		for (var c = 0; c < Columns; c++)
		{
			DrawLine(new Vector2(X(c), Top), new Vector2(X(c), Size.Y - Bottom), grid with { A = 0.18f }, 1);
			// (night numbers along the bottom; every few when there are many)
			var every = Math.Max(1, Columns / 16);
			var night = _positions ? c + 1 : c;
			if (night > 0 && (night % every == 0 || night == 1))
				DrawString(font, new Vector2(X(c) - 20, Size.Y - Bottom + 20), $"{night}", HorizontalAlignment.Center, 40, 12, C.Muted);
		}
		var dim = _highlight.Count > 0;
		// (highlighted lines last, so they're on top)
		foreach (var s in _series.OrderBy(s => _highlight.Contains(s.Id)))
		{
			var on = _highlight.Contains(s.Id);
			var color = dim && !on ? s.Color with { A = 0.18f } : s.Color;
			var width = on ? 4f : 2.5f;
			var points = new List<Vector2>();
			for (var c = 0; c < Columns; c++)
			{
				if (At(s, c) is not { } v)
				{
					DrawRun(points, color, width);
					points.Clear();
					continue;
				}
				var p = new Vector2(X(c), Y(v));
				if (_positions && points.Count > 0)
				{
					// (lap chart style: level, a slope where the places change, level again)
					var prev = points[^1];
					var dx = p.X - prev.X;
					points.Add(new Vector2(prev.X + dx * 0.3f, prev.Y));
					points.Add(new Vector2(prev.X + dx * 0.7f, p.Y));
				}
				points.Add(p);
			}
			DrawRun(points, color, width);
			// (a dot on the last night)
			if (Enumerable.Range(0, Columns).Reverse().FirstOrDefault(c => At(s, c) != null, -1) is var last and >= 0)
				DrawCircle(new Vector2(X(last), Y(At(s, last)!.Value)), on ? 5 : 3.5f, color);
		}
		DrawNames(font, dim);
	}

	void DrawRun(List<Vector2> points, Color color, float width)
	{
		if (points.Count >= 2) DrawPolyline(points.ToArray(), color, width, true);
	}

	/// <summary>Names on the right at their last value (pushed apart so they don't overlap), and on the left at their first.</summary>
	void DrawNames(Font font, bool dim)
	{
		const float gap = 17;
		foreach (var right in new[] { true, false })
		{
			if (!right && !_positions) break;
			var placed = _series.Select(s =>
			{
				var cols = Enumerable.Range(0, Columns).Where(c => At(s, c) != null).ToList();
				return cols.Count == 0 ? default : (S: s, Y: Y(At(s, right ? cols[^1] : cols[0])!.Value));
			}).Where(x => x.S != null).OrderBy(x => x.Y).ToList();
			var ys = placed.Select(x => x.Y).ToArray();
			for (var i = 1; i < ys.Length; i++) ys[i] = Math.Max(ys[i], ys[i - 1] + gap);
			var overflow = ys.Length > 0 ? ys[^1] - (Size.Y - Bottom) : 0;
			if (overflow > 0)
				for (var i = ys.Length - 1; i >= 0; i--) ys[i] = Math.Min(ys[i] - overflow, i < ys.Length - 1 ? ys[i + 1] - gap : float.MaxValue);
			for (var i = 0; i < placed.Count; i++)
			{
				var s = placed[i].S;
				var color = dim && !_highlight.Contains(s.Id) ? s.Color with { A = 0.3f } : s.Color;
				var name = s.Name.Length > 14 ? s.Name[..13] + "…" : s.Name;
				var x = right ? Size.X - Right + 10 : 4;
				var w = Right - 14;
				var pos = new Vector2(x, ys[i] + 5);
				DrawString(font, pos, name, right ? HorizontalAlignment.Left : HorizontalAlignment.Right, right ? w : Left - 12, 13, color);
				_labels.Add((new Rect2(x, ys[i] - 9, right ? w : Left - 12, 18), s.Id));
			}
		}
	}

	public override void _GuiInput(InputEvent e)
	{
		if (e is not InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } click) return;
		var hit = _labels.FirstOrDefault(l => l.Rect.HasPoint(click.Position));
		if (hit.Id != null) Toggle(hit.Id);
		else if (_highlight.Count > 0) Toggle(null);
		AcceptEvent();
	}

	public override string _GetTooltip(Vector2 at)
	{
		if (_labels.FirstOrDefault(l => l.Rect.HasPoint(at)).Id is { } id) return Lang.T("Click to highlight");
		if (at.X < Left - 10 || at.X > Size.X - Right + 10 || Columns == 0) return "";
		var column = Enumerable.Range(0, Columns).OrderBy(c => Math.Abs(X(c) - at.X)).First();
		var night = _positions ? column : column - 1;
		if (night < 0) return Lang.T("Before the first night: everyone on 0");
		var lines = _series.Where(s => s.Values[night] != null).OrderBy(s => _positions ? s.Values[night] : -s.Values[night])
			.Select(s => _positions ? $"{Lang.Ordinal((int)s.Values[night]!.Value)}  {s.Name}" : $"{s.Values[night]:0}  {s.Name}");
		return _nights[night] + "\n" + string.Join("\n", lines);
	}

	public override void _Notification(int what)
	{
		if (what == NotificationResized) QueueRedraw();
	}
}
