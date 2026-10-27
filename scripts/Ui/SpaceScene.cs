// The space skin's scene, behind the home screen only (not lobbies, editors or games): twinkling stars, a black hole with a
// turning accretion disk that bends the light near it, planets (some inspired by Star Wars: a desert planet with two suns, an
// ice planet, a gas giant with a cloud city, a forest moon, a lava planet, and a ringed one), and now and then a guest:
// X-wings and TIE fighters, a freighter, a shuttle, a Star Destroyer far off, rarely a Death Star (which sometimes blows up a
// planet; it comes back later), and Wheatley and the Space Core from Portal 2 tumbling past (the Space Core in speech
// bubbles). It all moves: the stars drift past, shooting stars streak by, each planet wanders round its spot and turns
// (its spots and storms slide across its face), the forest moon orbits the gas giant, clumps of hot gas race round the
// black hole and specks spiral in. All drawn here in code, no outside art or sound. Everything is a function of the
// time t (no state), so it stays still when motion is off. Sizes are in the 1280×720 base, scaled with the frame (k).

using System;
using Godot;

namespace QuizNight.Ui;

public static class SpaceScene
{
	/// <summary>The space skin is on and the home screen shows.</summary>
	public static bool OnScreen => C.Skin == Skins.Space && App.I?.CurrentScreen is Screens.HomeScreen;

	static float Hash(int n) => Mathf.PosMod(Mathf.Sin(n * 12.9898f + 78.233f) * 43758.547f, 1f);

	static readonly (float X, float Y, float Size, float Speed, float Phase)[] Stars = MakeStars(260, 7);

	static (float, float, float, float, float)[] MakeStars(int count, int seed)
	{
		var rng = new Random(seed);
		var stars = new (float, float, float, float, float)[count];
		for (var i = 0; i < count; i++)
		{
			var size = (float)Math.Pow(rng.NextDouble(), 3) * 2.2f + 0.6f;
			stars[i] = ((float)rng.NextDouble(), (float)rng.NextDouble(), size, 0.6f + (float)rng.NextDouble() * 2.2f, (float)rng.NextDouble() * 6.28f);
		}
		return stars;
	}

	// (where things are, as parts of the frame: the free space around the home screen's cards)
	static readonly Vector2 Hole = new(0.09f, 0.27f);

	enum Kind { Desert, Ice, GasGiant, ForestMoon, Lava, Ringed }

	// (each wanders round its spot in a slow figure of eight, Wander X/Y base px wide, once every Loop seconds, and turns
	// once every Day seconds; the forest moon's spot is unused: it orbits the gas giant)
	static readonly (Kind Kind, Vector2 At, float R, float WanderX, float WanderY, float Loop, float Day)[] Planets =
	{
		(Kind.Desert, new(0.085f, 0.74f), 26, 12, 12, 34, 30),
		(Kind.Ice, new(0.935f, 0.6f), 17, 10, 16, 41, 38),
		(Kind.GasGiant, new(0.9f, 0.32f), 40, 8, 6, 47, 20),
		(Kind.ForestMoon, new(0.83f, 0.2f), 9, 0, 0, 1, 26),
		(Kind.Lava, new(0.05f, 0.5f), 13, 7, 14, 29, 24),
		(Kind.Ringed, new(0.2f, 0.12f), 16, 20, 9, 37, 22),
	};

	const int GasGiant = 2, Moon = 3;

	// (the forest moon's orbit round the gas giant: tilted, seen nearly edge-on, once every MoonOrbit seconds)
	const float MoonOrbit = 32;
	static float MoonAngle(float t) => t * Mathf.Tau / MoonOrbit + 1;

	static Vector2 PlanetAt(int i, Rect2 frame, float k, float t)
	{
		if (i == Moon)
		{
			var a = MoonAngle(t);
			return PlanetAt(GasGiant, frame, k, t) + new Vector2(Mathf.Cos(a) * 64, Mathf.Sin(a) * 15).Rotated(0.3f) * k;
		}
		var (_, at, _, wx, wy, loop, _) = Planets[i];
		var w = t * Mathf.Tau / loop + i * 1.7f;
		return frame.Position + at * frame.Size + new Vector2(Mathf.Sin(w) * wx, Mathf.Sin(w * 2 + i) * wy * 0.5f) * k;
	}

	// The Death Star's visits: every Cycle seconds, there from Arrive to Leave; on some visits it fires at a planet.
	const float Cycle = 120, Arrive = 20, Leave = 62, Fire = 42, Beam = 1.4f, Blast = 7;
	static readonly Vector2 DeathStarAt = new(0.94f, 0.84f);

	public static void Draw(CanvasItem ci, Rect2 frame, float t)
	{
		var k = frame.Size.Y / 720f;
		Vector2 P(Vector2 f) => frame.Position + f * frame.Size;

		// --- stars drifting slowly to the left (the bright, nearer ones faster), each twinkling at its own pace
		foreach (var (x, y, size, speed, phase) in Stars)
		{
			var twinkle = 0.35f + 0.65f * (0.5f + 0.5f * Mathf.Sin(t * speed + phase));
			var at = P(new Vector2(Mathf.PosMod(x - t * (0.0015f + size * 0.0024f), 1f), y));
			var colour = new Color(0.85f + 0.15f * Mathf.Sin(phase * 3), 0.9f, 1f, twinkle);
			ci.DrawCircle(at, size * 0.6f * k, colour);
			// (the brightest ones get a little cross of light)
			if (size > 2)
			{
				var len = size * 2.6f * k * twinkle;
				ci.DrawLine(at - new Vector2(len, 0), at + new Vector2(len, 0), new Color(colour, twinkle * 0.5f), 1);
				ci.DrawLine(at - new Vector2(0, len), at + new Vector2(0, len), new Color(colour, twinkle * 0.5f), 1);
			}
		}

		ShootingStars(ci, frame, k, t);

		// --- the Star Destroyer far off, now and then (behind everything else)
		StarDestroyer(ci, frame, k, t);

		// --- planets (a blown-up one is gone until the Death Star's next visit)
		var cycle = Mathf.FloorToInt(t / Cycle);
		var inCycle = t - cycle * Cycle;
		void DrawPlanet(int i)
		{
			var (kind, _, r, _, _, _, day) = Planets[i];
			var alpha = 1f;
			if (Target(cycle) == i && inCycle >= Fire + Beam) alpha = 0;
			else if (Target(cycle - 1) == i) alpha = Mathf.Clamp(inCycle / 4f, 0, 1);
			if (alpha <= 0) return;
			// (the moon looks a little bigger on the near side of its orbit)
			if (i == Moon) r *= 1 + 0.15f * Mathf.Sin(MoonAngle(t));
			Planet(ci, kind, PlanetAt(i, frame, k, t), r * k, t, t * Mathf.Tau / day, alpha);
		}
		// (on the far half of its orbit the moon goes behind the gas giant)
		var moonBehind = Mathf.Sin(MoonAngle(t)) < 0;
		for (var i = 0; i < Planets.Length; i++)
		{
			if (i == Moon && moonBehind) continue;
			if (i == GasGiant && moonBehind) DrawPlanet(Moon);
			DrawPlanet(i);
		}

		BlackHole(ci, P(Hole), 30 * k, t);
		DeathStar(ci, frame, k, t, cycle, inCycle);
		Ships(ci, frame, k, t);
		Cores(ci, frame, k, t);
	}

	/// <summary>The planet the Death Star fires at on this visit, or -1 (most visits it just sits there).</summary>
	static int Target(int cycle) => cycle < 0 || Hash(cycle * 31 + 5) > 0.45f ? -1 : (int)(Hash(cycle * 17 + 3) * Planets.Length) % Planets.Length;

	// ---------- shooting stars ----------

	static void ShootingStars(CanvasItem ci, Rect2 frame, float k, float t)
	{
		// (one every few seconds, at a different moment, place and angle each time: down to the left, a fading tail)
		const float period = 6, life = 0.9f;
		var cycle = Mathf.FloorToInt(t / period);
		var since = t - cycle * period - Hash(cycle * 19 + 4) * (period - life);
		if (cycle < 0 || since < 0 || since > life) return;
		var f = since / life;
		var start = frame.Position + new Vector2(0.15f + Hash(cycle * 23 + 6) * 0.8f, 0.02f + Hash(cycle * 29 + 8) * 0.35f) * frame.Size;
		var dir = Vector2.FromAngle(Mathf.Pi * (0.74f + Hash(cycle * 37 + 1) * 0.12f));
		var head = start + dir * 230 * k * f;
		var tail = head - dir * 70 * k * Mathf.Min(1, f * 3);
		var alpha = Mathf.Sin(f * Mathf.Pi);
		ci.DrawPolylineColors(new[] { tail, head }, new[] { new Color(1, 1, 1, 0), new Color(0.9f, 0.95f, 1, alpha) }, 1.6f * k, true);
		ci.DrawCircle(head, 1.4f * k, new Color(1, 1, 1, alpha));
	}

	// ---------- the black hole ----------

	static void BlackHole(CanvasItem ci, Vector2 c, float r, float t)
	{
		// (a soft glow around it)
		for (var i = 6; i >= 1; i--) ci.DrawCircle(c, r * (1 + i * 0.45f), new Color(1f, 0.55f, 0.2f, 0.025f));
		// (the disk: tilted rings of hot gas, brighter where it comes towards us, turning; back half first, then the hole,
		// then the light bent over its top, then the front half)
		Disk(ci, c, r, t, back: true);
		ci.DrawCircle(c, r, Colors.Black);
		ci.DrawArc(c, r * 1.06f, 0, Mathf.Tau, 64, new Color(1f, 0.85f, 0.6f, 0.9f), 1.6f, true);
		// (the bent light: the far side of the disk seen over the top of the hole, as a thin arch)
		ci.DrawArc(c, r * 1.35f, Mathf.Pi * 1.08f, Mathf.Pi * 1.92f, 40, new Color(1f, 0.7f, 0.35f, 0.55f), 3f * r / 30, true);
		Disk(ci, c, r, t, back: false);
		// (stars caught near it, smeared into short arcs as they go round)
		for (var i = 0; i < 5; i++)
		{
			var a = t * (0.4f + i * 0.13f) + i * 1.3f;
			var rr = r * (1.6f + i * 0.18f);
			ci.DrawArc(c, rr, a, a + 0.35f, 8, new Color(0.9f, 0.95f, 1f, 0.5f), 1, true);
		}
	}

	static void Disk(CanvasItem ci, Vector2 c, float r, float t, bool back)
	{
		const int n = 48;
		for (var ring = 0; ring < 6; ring++)
		{
			var rx = r * (1.5f + ring * 0.32f);
			var ry = rx * 0.26f;
			var pts = new Vector2[n + 1];
			var cols = new Color[n + 1];
			for (var j = 0; j <= n; j++)
			{
				// (back half: the top, angles π…2π; front half: 0…π)
				var a = (back ? Mathf.Pi : 0) + j * Mathf.Pi / n;
				pts[j] = c + new Vector2(Mathf.Cos(a) * rx, Mathf.Sin(a) * ry);
				var swirl = 0.55f + 0.45f * Mathf.Sin(a * 3 - t * (1.6f - ring * 0.15f) + ring);
				// (the left side comes towards us: brighter)
				var doppler = 0.6f + 0.4f * -Mathf.Cos(a);
				var heat = (float)ring / 6;
				cols[j] = new Color(1f, 0.95f - heat * 0.5f, 0.75f - heat * 0.6f, (0.75f - heat * 0.5f) * swirl * doppler);
			}
			ci.DrawPolylineColors(pts, cols, (2.4f - ring * 0.25f) * r / 30, true);
		}
		// (clumps of hot gas racing round, the inner ones faster; each drawn in the half it's in: sin < 0 is the back)
		for (var i = 0; i < 10; i++)
		{
			var ring = i % 6;
			var rx = r * (1.5f + ring * 0.32f);
			var a = Hash(i + 40) * Mathf.Tau + t * (2.4f - ring * 0.3f);
			if ((Mathf.Sin(a) < 0) != back) continue;
			var glow = 0.6f + 0.4f * -Mathf.Cos(a);
			ci.DrawCircle(c + new Vector2(Mathf.Cos(a) * rx, Mathf.Sin(a) * rx * 0.26f), (2.2f - ring * 0.2f) * r / 30, new Color(1f, 0.92f - ring * 0.06f, 0.7f - ring * 0.08f, 0.85f * glow));
		}
		// (specks spiralling in from far out, faster the closer they get, gone at the edge of the hole)
		for (var i = 0; i < 5; i++)
		{
			var p = Mathf.PosMod(t * 0.09f + Hash(i + 60), 1f);
			var fall = p * p;
			var rx = r * (3.6f - 2.5f * fall);
			var a = Hash(i + 70) * Mathf.Tau + fall * 12;
			if ((Mathf.Sin(a) < 0) != back) continue;
			var alpha = Mathf.Min(p * 4, 1) * Mathf.Clamp((rx - r * 1.1f) / (r * 0.4f), 0, 1);
			ci.DrawCircle(c + new Vector2(Mathf.Cos(a) * rx, Mathf.Sin(a) * rx * 0.26f), 1.1f * r / 30, new Color(0.9f, 0.95f, 1f, 0.8f * alpha));
		}
	}

	// ---------- planets ----------

	/// <summary>Draws a planet turned by spin (radians): its spots slide across its face and round the back.</summary>
	static void Planet(CanvasItem ci, Kind kind, Vector2 c, float r, float t, float spin, float alpha)
	{
		Color A(Color col) => new(col, col.A * alpha);
		switch (kind)
		{
			case Kind.Desert:
				// (two suns behind it, Tatooine-like)
				Sun(ci, c + new Vector2(-r * 1.6f, -r * 2.1f), r * 0.32f, new Color("#ffe08a"), alpha);
				Sun(ci, c + new Vector2(-r * 0.9f, -r * 2.5f), r * 0.24f, new Color("#ffb066"), alpha);
				ci.DrawCircle(c, r, A(new Color("#d9a85c")));
				Bands(ci, c, r, new[] { (-0.5f, 0.12f), (0.1f, 0.1f), (0.55f, 0.08f) }, A(new Color("#c99650")));
				Spots(ci, c, r, spin, new[] { (-0.3f, 0.3f, 0.16f), (0.2f, 1.6f, 0.2f), (0.5f, 2.9f, 0.12f), (-0.1f, 4.2f, 0.15f), (0.35f, 5.3f, 0.11f) }, A(new Color("#b07a3a")));
				break;
			case Kind.Ice:
				ci.DrawCircle(c, r, A(new Color("#dff2ff")));
				Bands(ci, c, r, new[] { (-0.3f, 0.1f), (0.35f, 0.14f) }, A(new Color("#c8e4f6")));
				Spots(ci, c, r, spin, new[] { (-0.45f, 0.5f, 0.15f), (0.1f, 2f, 0.2f), (0.45f, 3.6f, 0.13f), (-0.15f, 5f, 0.17f) }, A(new Color("#9cc4e0")));
				break;
			case Kind.GasGiant:
				ci.DrawCircle(c, r, A(new Color("#e0a36b")));
				// (cloud bands flowing, next ones the other way)
				FlowingBands(ci, c, r, new[] { (-0.7f, 0.08f), (-0.42f, 0.12f), (-0.1f, 0.07f), (0.2f, 0.14f), (0.55f, 0.09f), (0.78f, 0.06f) }, A(new Color("#c27a45")), t);
				Spots(ci, c, r, spin, new[] { (0.36f, 1f, 0.17f) }, A(new Color("#a8552e")));
				// (a cloud city glinting in its clouds, going round with them)
				if (OnFace(c, r, -0.2f, 3.6f + spin, out var city, out var depth))
				{
					var glint = (0.5f + 0.5f * Mathf.Sin(t * 2.3f)) * depth;
					ci.DrawCircle(city, r * 0.06f, A(new Color(1, 1, 0.9f, (0.6f + 0.4f * glint) * depth)));
					ci.DrawLine(city - new Vector2(r * 0.14f * glint, 0), city + new Vector2(r * 0.14f * glint, 0), A(new Color(1, 1, 1, 0.6f * depth)), 1);
				}
				break;
			case Kind.ForestMoon:
				ci.DrawCircle(c, r, A(new Color("#4f9a4a")));
				Spots(ci, c, r, spin, new[] { (0.2f, 0.2f, 0.35f), (-0.3f, 1.9f, 0.26f), (0.4f, 3.3f, 0.22f), (-0.1f, 4.6f, 0.3f) }, A(new Color("#2f6e33")));
				break;
			case Kind.Lava:
				ci.DrawCircle(c, r, A(new Color("#2a0f0b")));
				// (lava lakes, glowing brighter and dimmer)
				var hot = new Color(1f, 0.45f, 0.1f, 0.6f + 0.4f * Mathf.Sin(t * 1.7f));
				Spots(ci, c, r, spin, new[] { (-0.2f, 0.4f, 0.15f), (0.3f, 1.8f, 0.11f), (-0.45f, 3f, 0.1f), (0.1f, 4.4f, 0.17f), (0.5f, 5.5f, 0.09f) }, A(hot));
				break;
			case Kind.Ringed:
				var ring = A(new Color("#c9b8ff", 0.8f));
				RingHalf(ci, c, r, ring, t, back: true);
				ci.DrawCircle(c, r, A(new Color("#8f6fd6")));
				Bands(ci, c, r, new[] { (-0.35f, 0.12f), (0.25f, 0.1f) }, A(new Color("#7556b8")));
				Spots(ci, c, r, spin, new[] { (0f, 1f, 0.13f), (0.45f, 3.5f, 0.1f) }, A(new Color("#b39cf0")));
				Shadow(ci, c, r, alpha);
				RingHalf(ci, c, r, ring, t, back: false);
				return;
		}
		Shadow(ci, c, r, alpha);
	}

	/// <summary>
	/// Where a point at a latitude (part of r, up is minus) and longitude (radians, 0 = facing us) is on the planet's face;
	/// false on the far side. depth is 1 in the middle of the face, 0 at its edge.
	/// </summary>
	static bool OnFace(Vector2 c, float r, float lat, float lon, out Vector2 at, out float depth)
	{
		depth = Mathf.Cos(lon);
		var y = lat * r;
		at = c + new Vector2(Mathf.Sin(lon) * Mathf.Sqrt(Mathf.Max(0, r * r - y * y)), y);
		return depth > 0.05f;
	}

	/// <summary>Patches on a turning planet (latitude, longitude, size as part of r), squeezed thin near its edge.</summary>
	static void Spots(CanvasItem ci, Vector2 c, float r, float spin, (float Lat, float Lon, float Size)[] spots, Color col)
	{
		const int n = 14;
		foreach (var (lat, lon, size) in spots)
		{
			if (!OnFace(c, r, lat, lon + spin, out var at, out var depth)) continue;
			// (kept inside the planet's outline: never wider than the room left to its edge)
			var y = lat * r;
			var room = Mathf.Sqrt(Mathf.Max(0, r * r - y * y)) - Mathf.Abs(at.X - c.X);
			var w = Mathf.Min(size * r * depth, room * 0.9f);
			if (w < 0.3f) continue;
			var pts = new Vector2[n];
			for (var i = 0; i < n; i++) pts[i] = at + new Vector2(Mathf.Cos(i * Mathf.Tau / n) * w, Mathf.Sin(i * Mathf.Tau / n) * size * r * 0.75f);
			Poly(ci, pts, new Color(col, col.A * Mathf.Min(1, depth * 3)));
		}
	}

	/// <summary>Bands that ripple and flow sideways, each the other way to the one above it.</summary>
	static void FlowingBands(CanvasItem ci, Vector2 c, float r, (float Y, float Thick)[] bands, Color col, float t)
	{
		const int n = 18;
		for (var b = 0; b < bands.Length; b++)
		{
			var (y, thick) = bands[b];
			var yy = y * r;
			var half = Mathf.Sqrt(Mathf.Max(0, r * r - yy * yy)) * 0.97f;
			var flow = t * 0.8f * (b % 2 == 0 ? 1 : -1);
			var pts = new Vector2[n + 1];
			for (var j = 0; j <= n; j++)
			{
				var x = -half + 2 * half * j / n;
				pts[j] = c + new Vector2(x, yy + Mathf.Sin(x / r * 6 + flow + b * 1.3f) * thick * r * 0.35f);
			}
			ci.DrawPolyline(pts, col, Mathf.Max(1, thick * r), true);
		}
	}

	static void Sun(CanvasItem ci, Vector2 c, float r, Color col, float alpha)
	{
		for (var i = 4; i >= 1; i--) ci.DrawCircle(c, r * (1 + i * 0.6f), new Color(col, 0.06f * alpha));
		ci.DrawCircle(c, r, new Color(col, alpha));
	}

	/// <summary>Stripes across a planet (y and thickness as parts of its radius), kept inside its circle.</summary>
	static void Bands(CanvasItem ci, Vector2 c, float r, (float Y, float Thick)[] bands, Color col)
	{
		foreach (var (y, thick) in bands)
		{
			var yy = y * r;
			var half = Mathf.Sqrt(Mathf.Max(0, r * r - yy * yy)) * 0.97f;
			ci.DrawLine(c + new Vector2(-half, yy), c + new Vector2(half, yy), col, Mathf.Max(1, thick * r));
		}
	}

	/// <summary>(dev: `--space-check` reports every shape Godot can't fill, with the line that drew it)</summary>
	static readonly bool CheckShapes = System.Array.IndexOf(OS.GetCmdlineUserArgs(), "--space-check") >= 0;

	/// <summary>A filled shape. Godot can't fill one whose outline touches itself (a point twice, say): it then logs an error every frame.</summary>
	static void Poly(CanvasItem ci, Vector2[] pts, Color col, [System.Runtime.CompilerServices.CallerLineNumber] int line = 0)
	{
		if (CheckShapes && Geometry2D.TriangulatePolygon(pts).Length == 0) GD.Print($"Space scene: shape from line {line} can't be filled ({pts.Length} points)");
		ci.DrawColoredPolygon(pts, col);
	}

	/// <summary>The night side: a crescent of shadow on the lower right (the light comes from the upper left).</summary>
	static void Shadow(CanvasItem ci, Vector2 c, float r, float alpha)
	{
		const int n = 24;
		var pts = new Vector2[n * 2 - 2];
		// (towards the lower right, away from the light)
		var dir = Mathf.Pi / 4;
		for (var i = 0; i < n; i++)
		{
			// (the outer edge, half the circle on the far side; then the terminator back, an ellipse bulging into the dark;
			// its two ends are the outer edge's, so they're left out: a point twice and Godot can't fill the shape)
			var a = dir - Mathf.Pi / 2 + i * Mathf.Pi / (n - 1);
			pts[i] = c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
			if (i == 0 || i == n - 1) continue;
			var b = dir + Mathf.Pi / 2 - i * Mathf.Pi / (n - 1);
			var local = new Vector2(Mathf.Cos(b - dir) * r * 0.35f, Mathf.Sin(b - dir) * r);
			pts[n + i - 1] = c + local.Rotated(dir);
		}
		Poly(ci, pts, new Color(0.02f, 0.02f, 0.08f, 0.62f * alpha));
	}

	static void RingHalf(CanvasItem ci, Vector2 c, float r, Color col, float t, bool back)
	{
		const int n = 24;
		Vector2 On(float a) => c + new Vector2(Mathf.Cos(a) * r * 1.9f, Mathf.Sin(a) * r * 0.42f).Rotated(-0.25f);
		var pts = new Vector2[n + 1];
		for (var i = 0; i <= n; i++) pts[i] = On((back ? Mathf.Pi : 0) + i * Mathf.Pi / n);
		ci.DrawPolyline(pts, col, Mathf.Max(1.5f, r * 0.16f), true);
		// (bits of ice glinting as the ring turns)
		for (var i = 0; i < 6; i++)
		{
			var a = i * Mathf.Tau / 6 + t * 0.5f;
			if ((Mathf.Sin(a) < 0) != back) continue;
			ci.DrawCircle(On(a), Mathf.Max(0.8f, r * 0.08f), new Color(1, 1, 1, col.A * (0.5f + 0.5f * Mathf.Sin(t * 3 + i))));
		}
	}

	// ---------- the Death Star ----------

	static void DeathStar(CanvasItem ci, Rect2 frame, float k, float t, int cycle, float inCycle)
	{
		if (inCycle < Arrive || inCycle > Leave) return;
		// (fades in and out: half in shadow, easy to miss)
		var come = Mathf.Clamp(Mathf.Min(inCycle - Arrive, Leave - inCycle) / 8f, 0, 1);
		var alpha = Mathf.Clamp(come * 1.6f, 0, 1) * 0.85f;
		// (glides in from the right edge, drifts a little while it's here, glides out the same way)
		var ease = 1 - (1 - come) * (1 - come);
		var c = frame.Position + DeathStarAt * frame.Size + new Vector2((1 - ease) * 90 + Mathf.Sin(inCycle * 0.15f) * 6, Mathf.Cos(inCycle * 0.11f) * 4) * k;
		var r = 20 * k;
		ci.DrawCircle(c, r, new Color("#8a8f99", alpha));
		ci.DrawLine(c + new Vector2(-r, r * 0.05f), c + new Vector2(r, r * 0.05f), new Color("#5a5f69", alpha), Mathf.Max(1, r * 0.08f));
		var dish = c + new Vector2(-r * 0.38f, -r * 0.4f);
		ci.DrawCircle(dish, r * 0.26f, new Color("#6e737d", alpha));
		ci.DrawCircle(dish, r * 0.08f, new Color("#4a4f59", alpha));
		Shadow(ci, c, r, alpha * 1.2f);
		// (a few lights on the dark side)
		for (var i = 0; i < 6; i++)
			ci.DrawCircle(c + new Vector2(r * (0.25f + Hash(i) * 0.6f), r * (-0.3f + Hash(i + 9) * 0.9f)), 0.8f * k, new Color(1, 0.95f, 0.7f, alpha * (0.5f + 0.5f * Mathf.Sin(t * 3 + i))));
		var target = Target(cycle);
		if (target < 0 || inCycle < Fire) return;
		var pr = Planets[target].R;
		// (the blast stays where the planet was when the beam hit, it doesn't wander on)
		var p = PlanetAt(target, frame, k, t - Mathf.Max(0, inCycle - Fire - Beam));
		var since = inCycle - Fire;
		if (since < Beam)
		{
			// (eight beams meet in front of the dish, then one goes to the planet)
			var meet = dish + (p - dish).Normalized() * r * 0.7f;
			for (var i = 0; i < 8; i++)
			{
				var a = i * Mathf.Tau / 8;
				ci.DrawLine(dish + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r * 0.24f, meet, new Color(0.4f, 1f, 0.4f, 0.8f), 1.2f, true);
			}
			if (since > Beam * 0.45f)
			{
				var reach = Mathf.Clamp((since - Beam * 0.45f) / (Beam * 0.4f), 0, 1);
				ci.DrawLine(meet, meet.Lerp(p, reach), new Color(0.55f, 1f, 0.5f, 0.95f), 3 * k, true);
				ci.DrawLine(meet, meet.Lerp(p, reach), new Color(1f, 1f, 1f, 0.8f), 1.2f * k, true);
			}
			return;
		}
		var boom = since - Beam;
		if (boom > Blast) return;
		var fade = 1 - boom / Blast;
		// (the flash, then a ring of debris spreading and glowing pieces flying off)
		if (boom < 0.6f) ci.DrawCircle(p, pr * k * (1 + boom * 4), new Color(1f, 1f, 0.85f, 1 - boom / 0.6f));
		var ringR = pr * k * (1.2f + boom * 0.45f);
		var ring = new Vector2[33];
		for (var i = 0; i <= 32; i++) ring[i] = p + new Vector2(Mathf.Cos(i * Mathf.Tau / 32) * ringR * 1.8f, Mathf.Sin(i * Mathf.Tau / 32) * ringR * 0.35f);
		ci.DrawPolyline(ring, new Color(0.6f, 1f, 0.6f, 0.6f * fade), 2 * k, true);
		for (var i = 0; i < 36; i++)
		{
			var a = Hash(i * 3 + cycle) * Mathf.Tau;
			var speed = (6 + Hash(i * 7 + 1) * 14) * k;
			var d = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
			var piece = p + d * (pr * k * 0.6f + speed * boom);
			ci.DrawCircle(piece, (0.8f + Hash(i + 4) * 1.6f) * k, new Color(1f, 0.55f + Hash(i) * 0.4f, 0.2f, fade));
		}
	}

	// ---------- the Star Destroyer ----------

	static void StarDestroyer(CanvasItem ci, Rect2 frame, float k, float t)
	{
		const float period = 95, cross = 34;
		var cycle = Mathf.FloorToInt(t / period);
		var inCycle = t - cycle * period;
		if (inCycle > cross) return;
		var f = inCycle / cross;
		// (slow, small and dark, across the top: nose first, to the left)
		var c = frame.Position + new Vector2((1.08f - f * 1.16f) * frame.Size.X, (0.06f + Hash(cycle) * 0.04f) * frame.Size.Y);
		var len = 70 * k;
		var hull = new[] { c + new Vector2(-len * 0.5f, 0), c + new Vector2(len * 0.5f, -len * 0.2f), c + new Vector2(len * 0.5f, len * 0.2f) };
		Poly(ci, hull, new Color("#3a3f4a", 0.9f));
		ci.DrawLine(hull[0], c + new Vector2(len * 0.5f, 0), new Color("#5a606c", 0.9f), 1);
		// (the bridge tower, and the engines' blue glow)
		ci.DrawRect(new Rect2(c + new Vector2(len * 0.3f, -len * 0.08f), new Vector2(len * 0.1f, len * 0.16f)), new Color("#4a505c", 0.95f));
		for (var i = -1; i <= 1; i++) ci.DrawCircle(c + new Vector2(len * 0.52f, i * len * 0.09f), 1.6f * k, new Color(0.5f, 0.8f, 1f, 0.9f));
		for (var i = 0; i < 5; i++) ci.DrawCircle(c + new Vector2(len * (-0.2f + i * 0.13f), len * 0.06f), 0.7f * k, new Color(1, 1, 0.8f, 0.6f));
	}

	// ---------- ships and fighters ----------

	static void Ships(CanvasItem ci, Rect2 frame, float k, float t)
	{
		const float period = 23, fly = 5.5f;
		var cycle = Mathf.FloorToInt(t / period);
		var inCycle = t - cycle * period;
		if (inCycle > fly || cycle < 0) return;
		var f = inCycle / fly;
		var kind = (int)(Hash(cycle * 13 + 2) * 4);
		// (up or down a side of the cards, where it's free, a little wavy)
		var lane = new[] { 0.06f, 0.16f, 0.84f, 0.94f }[(int)(Hash(cycle * 5 + 1) * 4) % 4];
		var up = Hash(cycle * 3 + 9) > 0.5f;
		var y = up ? 1.1f - f * 1.2f : -0.1f + f * 1.2f;
		Vector2 At(float yy, float wobble) => frame.Position + new Vector2((lane + Mathf.Sin(yy * 9 + wobble) * 0.012f) * frame.Size.X, yy * frame.Size.Y);
		var dir = up ? -Mathf.Pi / 2 : Mathf.Pi / 2;
		switch (kind)
		{
			case 0:
				// (an X-wing chased by a TIE fighter, both shooting)
				var ahead = At(y, 0);
				var behind = At(up ? y + 0.1f : y - 0.1f, 1.2f);
				XWing(ci, ahead, dir, k);
				Tie(ci, behind, k);
				if (Mathf.PosMod(inCycle, 0.5f) < 0.25f) Laser(ci, behind, dir, k, new Color(0.4f, 1, 0.4f), Mathf.PosMod(inCycle, 0.5f) / 0.25f, behind.DistanceTo(ahead));
				if (Mathf.PosMod(inCycle + 0.3f, 0.8f) < 0.2f) Laser(ci, ahead, dir, k, new Color(1, 0.3f, 0.3f), Mathf.PosMod(inCycle + 0.3f, 0.8f) / 0.2f, 60 * k);
				break;
			case 1:
				Tie(ci, At(y, 0), k);
				Tie(ci, At(up ? y + 0.06f : y - 0.06f, 2) + new Vector2(18 * k, 0), k);
				break;
			case 2:
				Freighter(ci, At(y, 0), dir, k);
				break;
			default:
				Shuttle(ci, At(y, 0), dir, k);
				break;
		}
	}

	static void Laser(CanvasItem ci, Vector2 from, float dir, float k, Color col, float f, float reach)
	{
		var d = Vector2.FromAngle(dir);
		var start = from + d * (8 * k + reach * f);
		ci.DrawLine(start, start + d * 10 * k, col, 1.6f * k, true);
	}

	/// <summary>Draws in the ship's own space: x along its nose (dir), centred on c.</summary>
	static void Local(CanvasItem ci, Vector2 c, float dir, float k, Action draw)
	{
		ci.DrawSetTransform(c, dir, new Vector2(k, k));
		draw();
		ci.DrawSetTransform(Vector2.Zero, 0, Vector2.One);
	}

	static void XWing(CanvasItem ci, Vector2 c, float dir, float k) => Local(ci, c, dir, k, () =>
	{
		var body = new Color("#d8dce4");
		// (the wings in their X, seen from above: two long wings with cannons at the tips, red stripes)
		ci.DrawLine(new Vector2(-4, -11), new Vector2(-4, 11), body, 3);
		ci.DrawLine(new Vector2(-4, -11), new Vector2(6, -11), body, 1);
		ci.DrawLine(new Vector2(-4, 11), new Vector2(6, 11), body, 1);
		ci.DrawLine(new Vector2(-4, -6), new Vector2(-4, -3), new Color("#d33"), 3);
		ci.DrawLine(new Vector2(-4, 3), new Vector2(-4, 6), new Color("#d33"), 3);
		Poly(ci, new[] { new Vector2(13, 0), new Vector2(-8, -2.2f), new Vector2(-8, 2.2f) }, body);
		ci.DrawCircle(new Vector2(-1, 0), 1.3f, new Color("#2a3a55"));
		// (the engines)
		ci.DrawCircle(new Vector2(-9, -3), 1.4f, new Color(1, 0.55f, 0.4f));
		ci.DrawCircle(new Vector2(-9, 3), 1.4f, new Color(1, 0.55f, 0.4f));
	});

	static void Tie(CanvasItem ci, Vector2 c, float k)
	{
		// (the ball cockpit between two hexagonal wings, seen from the front)
		var grey = new Color("#7c8494");
		ci.DrawLine(c + new Vector2(-8, 0) * k, c + new Vector2(8, 0) * k, grey, 1.5f * k);
		foreach (var side in new[] { -1f, 1f })
		{
			var hex = new Vector2[6];
			for (var i = 0; i < 6; i++) hex[i] = c + new Vector2(side * 9 + Mathf.Cos(i * Mathf.Tau / 6) * 2.2f, Mathf.Sin(i * Mathf.Tau / 6) * 9) * k;
			Poly(ci, hex, new Color("#2c313c"));
			ci.DrawPolyline(new[] { hex[0], hex[1], hex[2], hex[3], hex[4], hex[5], hex[0] }, grey, 1);
		}
		ci.DrawCircle(c, 3.6f * k, grey);
		ci.DrawCircle(c, 1.8f * k, new Color("#1b1f27"));
	}

	static void Freighter(CanvasItem ci, Vector2 c, float dir, float k) => Local(ci, c, dir, k, () =>
	{
		// (a round freighter: a disc, two prongs in front, the cockpit on its side; a blue engine glow behind)
		var hull = new Color("#c9ccd2");
		ci.DrawArc(Vector2.Zero, 10, Mathf.Pi * 0.6f, Mathf.Pi * 1.4f, 16, new Color(0.45f, 0.75f, 1f, 0.9f), 2.5f);
		ci.DrawCircle(Vector2.Zero, 10, hull);
		ci.DrawRect(new Rect2(6, -5, 9, 3.2f), hull);
		ci.DrawRect(new Rect2(6, 1.8f, 9, 3.2f), hull);
		ci.DrawRect(new Rect2(-2, 8, 10, 2.6f), hull);
		ci.DrawCircle(new Vector2(8, 9.3f), 2, hull);
		ci.DrawCircle(Vector2.Zero, 3.2f, new Color("#9aa0aa"));
		ci.DrawArc(Vector2.Zero, 6.5f, 0, Mathf.Tau, 24, new Color("#9aa0aa"), 0.8f);
	});

	static void Shuttle(CanvasItem ci, Vector2 c, float dir, float k) => Local(ci, c, dir, k, () =>
	{
		// (a shuttle with its wings folded down and the tall fin, from above: a slim body, wings swept back)
		var hull = new Color("#e6e8ec");
		Poly(ci, new[] { new Vector2(10, 0), new Vector2(-6, -3), new Vector2(-6, 3) }, hull);
		Poly(ci, new[] { new Vector2(-2, -2), new Vector2(-8, -10), new Vector2(-6, -2) }, hull);
		Poly(ci, new[] { new Vector2(-2, 2), new Vector2(-8, 10), new Vector2(-6, 2) }, hull);
		ci.DrawLine(new Vector2(-6, 0), new Vector2(-2, 0), new Color("#9aa0aa"), 1.4f);
		ci.DrawCircle(new Vector2(-7, 0), 1.5f, new Color(0.6f, 0.8f, 1f));
	});

	// ---------- Wheatley and the Space Core ----------

	static readonly string[] SpaceLines = { "I'm in space!", "Space!", "Gotta go to space.", "Spaaaace!", "Wanna go to space." };

	static void Cores(CanvasItem ci, Rect2 frame, float k, float t)
	{
		const float period = 48, drift = 20;
		var cycle = Mathf.FloorToInt(t / period);
		var inCycle = t - cycle * period;
		if (inCycle > drift || cycle < 0) return;
		var f = inCycle / drift;
		var space = cycle % 2 == 1;
		// (drifting up through a side, tumbling slowly)
		var left = Hash(cycle * 11) > 0.5f;
		var x = left ? 0.03f + f * 0.17f : 0.97f - f * 0.17f;
		var c = frame.Position + new Vector2(x, 1.1f - f * 1.2f) * frame.Size;
		var r = 15 * k;
		var spin = t * (space ? 1.1f : 0.6f) * (left ? 1 : -1);
		var eye = space ? new Color("#ffb000") : new Color("#4fc3ff");
		// (the handles over the top and under the bottom, then the shell, its middle seam, and the eye)
		ci.DrawSetTransform(c, spin, Vector2.One);
		var metal = new Color("#d7dbe2");
		var dark = new Color("#5d636e");
		ci.DrawArc(Vector2.Zero, r * 1.25f, Mathf.Pi * 1.2f, Mathf.Pi * 1.8f, 12, dark, 2.5f * k);
		ci.DrawArc(Vector2.Zero, r * 1.25f, Mathf.Pi * 0.2f, Mathf.Pi * 0.8f, 12, dark, 2.5f * k);
		ci.DrawCircle(Vector2.Zero, r, metal);
		ci.DrawArc(Vector2.Zero, r, 0, Mathf.Tau, 32, dark, 1.2f * k);
		ci.DrawLine(new Vector2(-r, 0), new Vector2(-r * 0.55f, 0), dark, 1.5f * k);
		ci.DrawLine(new Vector2(r * 0.55f, 0), new Vector2(r, 0), dark, 1.5f * k);
		ci.DrawCircle(Vector2.Zero, r * 0.5f, dark);
		ci.DrawCircle(Vector2.Zero, r * 0.36f, eye);
		ci.DrawCircle(Vector2.Zero, r * 0.14f, new Color(1, 1, 1, 0.85f));
		ci.DrawSetTransform(Vector2.Zero, 0, Vector2.One);
		if (!space) return;
		// (the Space Core talks: a speech bubble, a new line every few seconds; not turning with it)
		var line = SpaceLines[(int)(Hash(cycle * 7 + (int)(inCycle / 3.2f)) * SpaceLines.Length) % SpaceLines.Length];
		var font = Fonts.Bold;
		var size = Mathf.Max(8, (int)(13 * k));
		var w = font.GetStringSize(line, HorizontalAlignment.Left, -1, size).X;
		var box = new Rect2(c + new Vector2(left ? r * 0.6f : -r * 0.6f - w - 16 * k, -r * 2.6f), new Vector2(w + 16 * k, size + 12 * k));
		ci.DrawStyleBox(KeyBox.Fill(C.Ink, (int)(8 * k)), box.Grow(2 * k));
		ci.DrawStyleBox(KeyBox.Fill(new Color("#f4f6fa"), (int)(7 * k)), box);
		var tip = left ? box.Position + new Vector2(10 * k, box.Size.Y) : box.End - new Vector2(10 * k, 0);
		Poly(ci, new[] { tip, tip + new Vector2(left ? 8 : -8, 0) * k, c + new Vector2(left ? r * 0.3f : -r * 0.3f, -r * 1.1f) }, new Color("#f4f6fa"));
		ci.DrawString(font, box.Position + new Vector2(8 * k, 6 * k + size * 0.85f), line, HorizontalAlignment.Left, -1, size, new Color("#1b1f27"));
	}
}

/// <summary>
/// The space skin's start: pressing Start on the intro's title screen launches a rocket from the bottom (flame, smoke, a
/// little shake) that flies off the top while the home screen comes up. Not with motion turned down.
/// </summary>
public partial class RocketLaunch : Control
{
	const float Duration = 2.2f;
	float _t;

	public RocketLaunch()
	{
		SetAnchorsPreset(LayoutPreset.FullRect);
		MouseFilter = MouseFilterEnum.Ignore;
		ZIndex = 50;
	}

	public override void _Process(double delta)
	{
		_t += (float)delta;
		if (_t > Duration) QueueFree();
		QueueRedraw();
	}

	public override void _Draw()
	{
		var k = Size.Y / 720f;
		// (slow off the pad, then faster and faster; it shakes while the engines are loud)
		var rise = Mathf.Pow(Mathf.Clamp(_t / Duration, 0, 1), 2.2f);
		var shake = _t < 0.9f ? new Vector2(Mathf.Sin(_t * 70) * 2.2f, Mathf.Cos(_t * 53) * 1.6f) * k * (1 - _t / 0.9f) : Vector2.Zero;
		var baseY = Size.Y + 40 * k;
		var c = new Vector2(Size.X * 0.5f, baseY - rise * (Size.Y + 260 * k)) + shake;
		// (smoke: puffs left behind where it was, spreading and fading)
		for (var i = 0; i < 26; i++)
		{
			var born = i * 0.07f;
			if (_t < born) break;
			var age = _t - born;
			var at = Mathf.Pow(Mathf.Clamp(born / Duration, 0, 1), 2.2f);
			var p = new Vector2(Size.X * 0.5f + Mathf.Sin(i * 2.3f) * age * 30 * k, baseY - at * (Size.Y + 260 * k) + 50 * k + age * 20 * k);
			Puff(p, (14 + age * 40) * k, Mathf.Clamp(0.55f - age * 0.3f, 0, 1));
		}
		// (the flame, flickering)
		var flicker = 0.8f + 0.2f * Mathf.Sin(_t * 60);
		DrawColoredPolygon(new[] { c + new Vector2(-9, 34) * k, c + new Vector2(9, 34) * k, c + new Vector2(0, 34 + 46 * flicker) * k }, new Color(1f, 0.55f, 0.1f, 0.9f));
		DrawColoredPolygon(new[] { c + new Vector2(-5, 34) * k, c + new Vector2(5, 34) * k, c + new Vector2(0, 34 + 26 * flicker) * k }, new Color(1f, 0.95f, 0.6f));
		// (the rocket: white body, red nose and fins, a round window, ink outlines like the rest of the game)
		var body = new[] { c + new Vector2(0, -44) * k, c + new Vector2(13, -22) * k, c + new Vector2(13, 30) * k, c + new Vector2(-13, 30) * k, c + new Vector2(-13, -22) * k };
		var outline = new Vector2[body.Length + 1];
		body.CopyTo(outline, 0);
		outline[^1] = body[0];
		DrawColoredPolygon(new[] { c + new Vector2(-13, 10) * k, c + new Vector2(-24, 36) * k, c + new Vector2(-13, 30) * k }, C.Accent);
		DrawColoredPolygon(new[] { c + new Vector2(13, 10) * k, c + new Vector2(24, 36) * k, c + new Vector2(13, 30) * k }, C.Accent);
		DrawColoredPolygon(body, new Color("#f2f2f5"));
		DrawColoredPolygon(new[] { c + new Vector2(0, -44) * k, c + new Vector2(13, -22) * k, c + new Vector2(-13, -22) * k }, C.Accent);
		DrawPolyline(outline, C.Ink, 2.5f * k, true);
		DrawCircle(c + new Vector2(0, -6) * k, 6.5f * k, C.Ink);
		DrawCircle(c + new Vector2(0, -6) * k, 5 * k, new Color("#7fd0ff"));
		DrawRect(new Rect2(c + new Vector2(-13, 30) * k, new Vector2(26, 4) * k), C.Ink);
	}

	void Puff(Vector2 p, float r, float alpha)
	{
		if (alpha <= 0) return;
		DrawCircle(p, r, new Color(0.85f, 0.85f, 0.9f, alpha * 0.5f));
		DrawCircle(p + new Vector2(r * 0.4f, -r * 0.2f), r * 0.7f, new Color(0.95f, 0.95f, 1f, alpha * 0.4f));
	}
}
