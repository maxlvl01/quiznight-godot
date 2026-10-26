// The seasonal skins' decorations (Halloween in October, Christmas in December, Cologne carnival on 11 November and in
// the carnival week, Valentine's Day in February). Carnival: confetti and streamers, a jester's cap on the logo;
// Valentine's: hearts floating up, hearts on the logo. On the screen, behind the
// cards (ScreenFrame draws them over its background): Christmas snow falling and fairy lights along the top, Halloween
// cobwebs in the corners, bats flapping across and pumpkins in the corner. On the logo (LogoDecor): a snow cap and a
// Santa hat, or pumpkins, a cobweb and a bat. With less motion asked for, nothing moves.

using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace QuizNight.Ui;

public static class Seasonal
{
	/// <summary>Whether the skin in use has decorations.</summary>
	public static bool On => Skins.IsSeasonal(C.Skin);

	static readonly (float X, float Speed, float Phase, float R)[] Flakes = MakeFlakes(80, 11);

	static (float, float, float, float)[] MakeFlakes(int count, int seed)
	{
		var rng = new Random(seed);
		return Enumerable.Range(0, count).Select(_ => ((float)rng.NextDouble(), 0.03f + (float)rng.NextDouble() * 0.05f, (float)rng.NextDouble(), 1.4f + (float)rng.NextDouble() * 2.4f)).ToArray();
	}

	public static Vector2[] Ellipse(Vector2 c, float rx, float ry, int n = 28)
	{
		var pts = new Vector2[n];
		for (var i = 0; i < n; i++) pts[i] = c + new Vector2(Mathf.Cos(i * Mathf.Tau / n) * rx, Mathf.Sin(i * Mathf.Tau / n) * ry);
		return pts;
	}

	/// <summary>The screen's decorations in <paramref name="frame"/> (the 16:9 game area), at time <paramref name="t"/>.</summary>
	public static void DrawScreen(CanvasItem ci, Rect2 frame, double t)
	{
		if (C.Skin == Skins.Christmas) ChristmasScreen(ci, frame, (float)t);
		else if (C.Skin == Skins.Halloween) HalloweenScreen(ci, frame, (float)t);
		else if (C.Skin == Skins.Carnival) CarnivalScreen(ci, frame, (float)t);
		else if (C.Skin == Skins.Valentine) ValentineScreen(ci, frame, (float)t);
	}

	static readonly Color[] Confetti = { new("#ff4757"), new("#ffd23f"), new("#2ed573"), new("#5cc8ff"), new("#ff8ad8"), new("#ffffff"), new("#ff9f43") };

	static void CarnivalScreen(CanvasItem ci, Rect2 frame, float t)
	{
		// confetti tumbling down: small paper bits that spin as they fall
		for (var i = 0; i < Flakes.Length; i++)
		{
			var (x, speed, phase, r) = Flakes[i];
			var y = (phase + t * speed * 1.4f) % 1f;
			var p = new Vector2(Mathf.Clamp(frame.Position.X + x * frame.Size.X + Mathf.Sin(t + phase * 7) * 18, frame.Position.X + 6, frame.End.X - 6), frame.Position.Y + y * frame.Size.Y);
			var spin = t * (2 + phase * 3) + i;
			var w = new Vector2(Mathf.Cos(spin), Mathf.Sin(spin)) * (r + 2.5f);
			var h = new Vector2(-w.Y, w.X) * 0.45f * Mathf.Abs(Mathf.Cos(spin * 1.7f));
			ci.DrawColoredPolygon(new[] { p - w - h, p + w - h, p + w + h, p - w + h }, new Color(Confetti[i % Confetti.Length], 0.75f));
		}
		// streamers hanging from the top edge, curling as they sway
		for (var i = 0; i < 7; i++)
		{
			var x0 = frame.Position.X + frame.Size.X * (0.06f + i * 0.148f);
			var len = 70 + (i * 37) % 60;
			var pts = new Vector2[18];
			for (var k = 0; k < pts.Length; k++)
			{
				var f = k / (float)(pts.Length - 1);
				pts[k] = new Vector2(x0 + Mathf.Sin(f * 9 + t * 1.5f + i) * 7 * f + Mathf.Sin(t * 0.7f + i) * 6 * f, frame.Position.Y + f * len);
			}
			ci.DrawPolyline(pts, Confetti[(i * 3) % Confetti.Length], 3, true);
		}
	}

	static void ValentineScreen(CanvasItem ci, Rect2 frame, float t)
	{
		// hearts floating up, swaying a little
		for (var i = 0; i < 26; i++)
		{
			var (x, speed, phase, r) = Flakes[i * 3];
			var y = 1 - (phase + t * speed * 0.9f) % 1f;
			var size = 5 + r * 3;
			var p = new Vector2(Mathf.Clamp(frame.Position.X + x * frame.Size.X + Mathf.Sin(t * 0.9f + phase * 9) * 16, frame.Position.X + size, frame.End.X - size), frame.Position.Y + y * frame.Size.Y);
			var color = i % 3 == 0 ? new Color("#ff4f8b") : i % 3 == 1 ? new Color("#ff9ec7") : new Color("#ff2e4d");
			Heart(ci, p, size, new Color(color, 0.35f + 0.2f * (i % 2)));
		}
	}

	/// <summary>A heart around <paramref name="c"/>, about <paramref name="size"/> wide.</summary>
	public static void Heart(CanvasItem ci, Vector2 c, float size, Color color)
	{
		var pts = new Vector2[30];
		for (var i = 0; i < pts.Length; i++)
		{
			var a = i * Mathf.Tau / pts.Length;
			var s = Mathf.Sin(a);
			pts[i] = c + new Vector2(16 * s * s * s, -(13 * Mathf.Cos(a) - 5 * Mathf.Cos(2 * a) - 2 * Mathf.Cos(3 * a) - Mathf.Cos(4 * a))) * (size / 32);
		}
		ci.DrawColoredPolygon(pts, color);
	}

	static void ChristmasScreen(CanvasItem ci, Rect2 frame, float t)
	{
		// snow falling slowly over the whole screen
		foreach (var (x, speed, phase, r) in Flakes)
		{
			var y = (phase + t * speed) % 1f;
			// (inside the 16:9 screen: on ultrawide monitors the space picture beside it stays clear)
			var px = Mathf.Clamp(frame.Position.X + x * frame.Size.X + Mathf.Sin(t * 0.8f + phase * 11) * 14, frame.Position.X + r, frame.End.X - r);
			ci.DrawCircle(new Vector2(px, frame.Position.Y + y * frame.Size.Y), r, new Color("#f4f8ff", 0.45f));
		}
		// fairy lights along the top: a sagging wire, bulbs that twinkle
		var colors = new[] { new Color("#ff4757"), new Color("#ffd23f"), new Color("#2ed573"), new Color("#5cc8ff") };
		// (whole spans that end exactly at the screen's edges)
		var spans = Mathf.Max(1, Mathf.RoundToInt(frame.Size.X / 150));
		var span = frame.Size.X / spans;
		var bulb = 0;
		for (var n = 0; n < spans; n++)
		{
			var x0 = frame.Position.X + n * span;
			var pts = new List<Vector2>();
			for (var i = 0; i <= 12; i++)
			{
				var f = i / 12f;
				pts.Add(new Vector2(x0 + f * span, frame.Position.Y + 4 + Mathf.Sin(f * Mathf.Pi) * 16));
			}
			ci.DrawPolyline(pts.ToArray(), new Color("#1b2a22"), 2, true);
			foreach (var f in new[] { 0.2f, 0.5f, 0.8f })
			{
				var p = new Vector2(x0 + f * span, frame.Position.Y + 4 + Mathf.Sin(f * Mathf.Pi) * 16 + 6);
				var color = colors[bulb++ % colors.Length];
				var twinkle = 0.55f + 0.45f * Mathf.Sin(t * 2.2f + bulb * 1.7f);
				ci.DrawCircle(p, 11, new Color(color, 0.18f * twinkle));
				ci.DrawCircle(p + new Vector2(0, -6), 2.5f, new Color("#1b2a22"));
				ci.DrawColoredPolygon(Ellipse(p, 4.5f, 6.5f, 14), color.Lerp(Colors.White, 0.25f * twinkle));
			}
		}
	}

	static void HalloweenScreen(CanvasItem ci, Rect2 frame, float t)
	{
		Cobweb(ci, frame.Position + new Vector2(2, 2), 130, false);
		Cobweb(ci, new Vector2(frame.End.X - 2, frame.Position.Y + 2), 110, true);
		// three bats flapping across now and then, each on its own wavy path; only inside the 16:9 screen, fading in and out
		// near its edges
		for (var i = 0; i < 3; i++)
		{
			var lap = frame.Size.X + 500;
			var x = (t * (55 + i * 18) + i * 470) % lap;
			const float edge = 60;
			if (x < edge || x > frame.Size.X - edge) continue;
			var fade = Mathf.Clamp(Mathf.Min(x - edge, frame.Size.X - edge - x) / 90, 0, 1);
			var y = frame.Size.Y * (0.18f + 0.22f * i) + Mathf.Sin(t * 1.4f + i * 2) * 28;
			Bat(ci, frame.Position + new Vector2(x, y), 1.1f - i * 0.2f, t * 11 + i, fade);
		}
		Pumpkin(ci, new Vector2(frame.End.X - 70, frame.End.Y - 6), 34, t);
		Pumpkin(ci, new Vector2(frame.End.X - 140, frame.End.Y - 4), 24, t + 1);
	}

	/// <summary>A cobweb in a corner: spokes out of it and threads sagging between them (mirrored: the right corner).</summary>
	public static void Cobweb(CanvasItem ci, Vector2 corner, float size, bool mirrored)
	{
		var web = new Color("#d9d2e6", 0.5f);
		var dir = mirrored ? -1 : 1;
		Vector2 At(float a, float r) => corner + new Vector2(Mathf.Cos(a) * dir, Mathf.Sin(a)) * r;
		for (var i = 0; i <= 5; i++) ci.DrawLine(corner, At(Mathf.Pi * 0.5f * i / 5, size), web, 1.2f, true);
		for (var ring = 1; ring <= 4; ring++)
			ci.DrawPolyline(Enumerable.Range(0, 6).Select(i => At(Mathf.Pi * 0.5f * i / 5, ring * size / 4.4f * (i % 2 == 1 ? 0.88f : 1f))).ToArray(), web, 1f, true);
	}

	/// <summary>A bat with its wings at <paramref name="flap"/> (a phase: it flaps as it grows).</summary>
	public static void Bat(CanvasItem ci, Vector2 at, float scale, float flap, float alpha = 1)
	{
		var up = Mathf.Sin(flap) * 9 * scale;
		var ink = new Color(C.Ink, alpha);
		foreach (var side in new[] { -1f, 1f })
			ci.DrawColoredPolygon(new[]
			{
				at + new Vector2(3 * side, 0) * scale, at + new Vector2(16 * side, -8) * scale + new Vector2(0, -up), at + new Vector2(32 * side, -3) * scale + new Vector2(0, -up),
				at + new Vector2(25 * side, 3) * scale, at + new Vector2(17 * side, 1) * scale, at + new Vector2(10 * side, 6) * scale, at + new Vector2(5 * side, 7) * scale,
			}, ink);
		ci.DrawCircle(at + new Vector2(0, 2) * scale, 6.5f * scale, ink);
		ci.DrawColoredPolygon(new[] { at + new Vector2(-5, -3) * scale, at + new Vector2(-3, -10) * scale, at + new Vector2(-1, -4) * scale }, ink);
		ci.DrawColoredPolygon(new[] { at + new Vector2(5, -3) * scale, at + new Vector2(3, -10) * scale, at + new Vector2(1, -4) * scale }, ink);
	}

	/// <summary>A carved pumpkin standing on <paramref name="bottom"/>, its face glowing.</summary>
	public static void Pumpkin(CanvasItem ci, Vector2 bottom, float r, float t)
	{
		var orange = new Color("#ff7a00");
		var dark = new Color("#c85500");
		var c = bottom - new Vector2(0, r * 0.8f);
		var glow = 0.75f + 0.25f * Mathf.Sin(t * 5 + bottom.X);
		ci.DrawColoredPolygon(Ellipse(c, r * 1.12f + 3, r * 0.86f + 3), C.Ink);
		// (three lobes side by side, the middle one in front)
		ci.DrawColoredPolygon(Ellipse(c - new Vector2(r * 0.45f, 0), r * 0.62f, r * 0.82f), dark);
		ci.DrawColoredPolygon(Ellipse(c + new Vector2(r * 0.45f, 0), r * 0.62f, r * 0.82f), dark);
		ci.DrawColoredPolygon(Ellipse(c, r * 0.68f, r * 0.86f), orange);
		ci.DrawColoredPolygon(new[] { c + new Vector2(-r * 0.08f, -r * 0.78f), c + new Vector2(r * 0.1f, -r * 0.78f), c + new Vector2(r * 0.22f, -r * 1.18f), c + new Vector2(r * 0.06f, -r * 1.2f) }, new Color("#5a8a2c"));
		var light = new Color(1f, 0.85f, 0.25f, glow);
		ci.DrawColoredPolygon(new[] { c + new Vector2(-r * 0.45f, -r * 0.12f), c + new Vector2(-r * 0.12f, -r * 0.12f), c + new Vector2(-r * 0.28f, -r * 0.42f) }, light);
		ci.DrawColoredPolygon(new[] { c + new Vector2(r * 0.12f, -r * 0.12f), c + new Vector2(r * 0.45f, -r * 0.12f), c + new Vector2(r * 0.28f, -r * 0.42f) }, light);
		ci.DrawColoredPolygon(new[]
		{
			c + new Vector2(-r * 0.5f, r * 0.12f), c + new Vector2(-r * 0.3f, r * 0.24f), c + new Vector2(-r * 0.18f, r * 0.14f), c + new Vector2(0, r * 0.28f),
			c + new Vector2(r * 0.18f, r * 0.14f), c + new Vector2(r * 0.3f, r * 0.24f), c + new Vector2(r * 0.5f, r * 0.12f),
			c + new Vector2(r * 0.25f, r * 0.48f), c + new Vector2(-r * 0.25f, r * 0.48f),
		}, light);
	}
}

/// <summary>The logo's seasonal dressing, drawn over the whole field and a little past its edges.</summary>
public partial class LogoDecor : Control
{
	readonly string _skin;
	double _t;
	readonly (float X, float Speed, float Phase, float R)[] _flakes;

	public LogoDecor() : this(Skins.Christmas) { }

	public LogoDecor(string skin)
	{
		_skin = skin;
		MouseFilter = MouseFilterEnum.Ignore;
		var rng = new Random(7);
		_flakes = Enumerable.Range(0, 26).Select(_ => ((float)rng.NextDouble(), 0.12f + (float)rng.NextDouble() * 0.18f, (float)rng.NextDouble(), 1.5f + (float)rng.NextDouble() * 2.2f)).ToArray();
	}

	public override void _Process(double delta)
	{
		if (Announcer.ReduceMotion) return;
		_t += delta;
		QueueRedraw();
	}

	public override void _Draw()
	{
		// (the field's whole box: this control only gets the space inside its padding)
		var box = new Rect2(-Position, GetParent<Control>().Size);
		var t = (float)_t;
		if (_skin == Skins.Carnival)
		{
			Carnival(box, t);
			return;
		}
		if (_skin == Skins.Valentine)
		{
			// hearts around the field, beating gently
			var beat = 1 + 0.08f * Mathf.Max(0, Mathf.Sin(t * 5));
			Seasonal.Heart(this, box.Position + new Vector2(4, 4), 38 * beat, C.Ink);
			Seasonal.Heart(this, box.Position + new Vector2(4, 4), 32 * beat, new Color("#ff2e4d"));
			Seasonal.Heart(this, new Vector2(box.End.X - 30, box.End.Y - 2), 30 * beat, C.Ink);
			Seasonal.Heart(this, new Vector2(box.End.X - 30, box.End.Y - 2), 25 * beat, new Color("#ff9ec7"));
			Seasonal.Heart(this, new Vector2(box.Position.X + box.Size.X * 0.55f, box.Position.Y - 4), 20 * beat, new Color("#ff4f8b"));
			return;
		}
		if (_skin == Skins.Halloween)
		{
			Seasonal.Cobweb(this, box.Position + new Vector2(6, 6), 62, false);
			Seasonal.Bat(this, box.Position + new Vector2(box.Size.X * (0.6f + 0.05f * Mathf.Sin(t * 0.7f)), -18 + 5 * Mathf.Sin(t * 1.9f)), 0.9f, t * 12);
			Seasonal.Pumpkin(this, new Vector2(box.Position.X + 8, box.End.Y + 2), 30, t);
			Seasonal.Pumpkin(this, new Vector2(box.End.X - 4, box.End.Y + 4), 22, t + 1);
			return;
		}
		var snow = new Color("#f4f8ff");
		var shade = new Color("#c9d6ea");
		// falling snow, around and in front of the field
		foreach (var (x, speed, phase, r) in _flakes)
		{
			var y = (phase + t * speed) % 1f;
			var px = box.Position.X - 30 + x * (box.Size.X + 60) + Mathf.Sin(t * 1.3f + phase * 9) * 6;
			DrawCircle(new Vector2(px, box.Position.Y - 40 + y * (box.Size.Y + 70)), r, new Color(snow, 0.8f));
		}
		// a snow cap along the top edge: one lumpy band, a few drips over the edge
		var top = box.Position.Y + 2;
		var left = box.Position.X + 10;
		var right = box.End.X - 10;
		float Lump(float x) => 7 + 3 * Mathf.Sin(x * 0.11f) + 1.5f * Mathf.Sin(x * 0.37f);
		for (var x = left; x <= right; x += 5) DrawCircle(new Vector2(x, top), Lump(x) + 1.5f, shade);
		for (var x = left; x <= right; x += 5) DrawCircle(new Vector2(x, top - 1.5f), Lump(x), snow);
		foreach (var f in new[] { 0.22f, 0.47f, 0.71f, 0.88f })
		{
			var x = left + (right - left) * f;
			DrawCircle(new Vector2(x, top + 8), 3.5f, snow);
			DrawCircle(new Vector2(x, top + 12), 2.4f, snow);
		}
		// a Santa hat on the top left corner, tilted
		DrawSetTransformMatrix(Transform2D.Identity.Rotated(-0.35f).Translated(box.Position + new Vector2(18, 2)));
		DrawColoredPolygon(new[] { new Vector2(-30, 0), new Vector2(30, 0), new Vector2(14, -34), new Vector2(-6, -48), new Vector2(-24, -30) }, C.Ink);
		DrawColoredPolygon(new[] { new Vector2(-27, -2), new Vector2(27, -2), new Vector2(12, -32), new Vector2(-6, -44), new Vector2(-21, -28) }, new Color("#d8263c"));
		DrawColoredPolygon(new[] { new Vector2(-6, -44), new Vector2(4, -40), new Vector2(-10, -30) }, new Color("#a81a2c"));
		DrawCircle(new Vector2(-8, -48), 8.5f, C.Ink);
		DrawCircle(new Vector2(-8, -48), 7, snow);
		DrawColoredPolygon(Seasonal.Ellipse(Vector2.Zero, 34, 9.5f), C.Ink);
		DrawColoredPolygon(Seasonal.Ellipse(Vector2.Zero, 32, 8), snow);
		DrawSetTransformMatrix(Transform2D.Identity);
	}

	/// <summary>Carnival: a red and white jester's cap with bells on the top left corner, a little confetti on the field.</summary>
	void Carnival(Rect2 box, float t)
	{
		var red = new Color("#c8102e");
		var white = new Color("#f8f4ef");
		var gold = new Color("#ffd23f");
		var rng = new Random(3);
		for (var i = 0; i < 14; i++)
		{
			var p = box.Position + new Vector2((float)rng.NextDouble() * box.Size.X, (float)rng.NextDouble() * 10 - 6);
			var colors = new[] { new Color("#ff4757"), new Color("#ffd23f"), new Color("#2ed573"), new Color("#5cc8ff"), new Color("#ff8ad8") };
			DrawRect(new Rect2(p, new Vector2(5, 3)), colors[i % colors.Length]);
		}
		DrawSetTransformMatrix(Transform2D.Identity.Rotated(-0.3f).Translated(box.Position + new Vector2(22, 4)));
		var swing = Mathf.Sin(t * 3) * 3;
		// (three points flopping out to the sides and up, alternating red and white, a bell on each)
		Vector2[] tips = { new(-38, -16 + swing), new(0, -50), new(38, -16 - swing) };
		for (var i = 0; i < 3; i++)
		{
			var left = new Vector2(-24 + i * 16, 0);
			var right = left + new Vector2(16, 0);
			DrawColoredPolygon(new[] { left + new Vector2(-2, 2), tips[i] + new Vector2(0, -3), right + new Vector2(2, 2) }, C.Ink);
			DrawColoredPolygon(new[] { left, tips[i], right }, i == 1 ? white : red);
			DrawCircle(tips[i], 6.5f, C.Ink);
			DrawCircle(tips[i], 5, gold);
		}
		// (the band)
		DrawRect(new Rect2(-30, -3, 60, 12), C.Ink);
		DrawRect(new Rect2(-28, -1, 56, 8), white);
		for (var x = -26; x < 26; x += 8) DrawRect(new Rect2(x, -1, 4, 8), red);
		DrawSetTransformMatrix(Transform2D.Identity);
	}
}
