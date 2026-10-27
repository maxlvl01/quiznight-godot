// World of Warcraft Classic map hierarchy: world map → 2 continents → zones/cities, like the
// in-game map. Data from the Classic Era client (1.15.9, UiMap + UiMapAssignment via wago.tools),
// identical to vanilla 1.12. Images: res://assets/maps/wow/<id>.webp, all 1002×668.
//
// Game coordinates are in yards: X points north, Y points west. Each zone/continent image
// covers its region exactly; the world map places each continent inside a ui rect.
//
// Pins are stored as a point on the world map image (so Question.Target stays a plain NormPoint).
// The map players see is seamless (Ui/WowMap.cs): zones stitched into one zoomable world.

using System;
using System.Collections.Generic;
using System.Linq;

namespace QuizNight.Shared;

public static class WowAtlas
{
	public const string PresetId = "wow-azeroth";
	public const int World = 947;
	public const int ImageWidth = 1002;
	public const int ImageHeight = 668;

	public record struct Region(double XMin, double YMin, double XMax, double YMax);

	public enum Kind { World, Continent, Zone }

	/// <summary>A map in the atlas. Continent 0 = Eastern Kingdoms, 1 = Kalimdor (the game's MapID).</summary>
	public sealed record Map(int Id, string Name, Kind Kind, int? Continent, int? Parent, Region? Region, bool City = false);

	/// <summary>A spot in the game world: X yards north, Y yards west.</summary>
	public record struct Location(int Continent, double X, double Y);

	static readonly (int Id, string Name, int Continent, double XMin, double YMin, double XMax, double YMax)[] Zones =
	{
		(1411, "Durotar", 1, -1716.6666259766, -7249.9995117188, 1808.3332519531, -1962.4998779297),
		(1412, "Mulgore", 1, -3697.9165039062, -3089.5832519531, -272.91665649414, 2047.9166259766),
		(1413, "The Barrens", 1, -5143.75, -7510.4165039062, 1612.4998779297, 2622.9165039062),
		(1416, "Alterac Mountains", 0, -366.66665649414, -2016.6666259766, 1500, 783.33331298828),
		(1417, "Arathi Highlands", 0, -2533.3332519531, -4466.6665039062, -133.33332824707, -866.66662597656),
		(1418, "Badlands", 0, -7547.9165039062, -4566.6665039062, -5889.5830078125, -2079.1665039062),
		(1419, "Blasted Lands", 0, -12800, -4591.6665039062, -10566.666015625, -1241.6666259766),
		(1420, "Tirisfal Glades", 0, 824.99993896484, -1485.4166259766, 3837.4997558594, 3033.3332519531),
		(1421, "Silverpine Forest", 0, -1133.3332519531, -750, 1666.6666259766, 3449.9997558594),
		(1422, "Western Plaguelands", 0, 499.99996948242, -3883.3332519531, 3366.6665039062, 416.66665649414),
		(1423, "Eastern Plaguelands", 0, 1218.75, -6056.25, 3799.9997558594, -2185.4165039062),
		(1424, "Hillsbrad Foothills", 0, -1733.3332519531, -2133.3332519531, 400, 1066.6666259766),
		(1425, "The Hinterlands", 0, -1100, -5425, 1466.6666259766, -1575),
		(1426, "Dun Morogh", 0, -7160.4165039062, -3122.9165039062, -3877.0832519531, 1802.0832519531),
		(1427, "Searing Gorge", 0, -7587.4995117188, -2554.1665039062, -6100, -322.91665649414),
		(1428, "Burning Steppes", 0, -8983.3330078125, -3195.8332519531, -7031.2495117188, -266.66665649414),
		(1429, "Elwynn Forest", 0, -10254.166015625, -1935.4166259766, -7939.5830078125, 1535.4166259766),
		(1430, "Deadwind Pass", 0, -11533.333007812, -3333.3332519531, -9866.666015625, -833.33331298828),
		(1431, "Duskwood", 0, -11516.666015625, -1866.6666259766, -9716.666015625, 833.33331298828),
		(1432, "Loch Modan", 0, -6327.0830078125, -4752.0830078125, -4487.5, -1993.7498779297),
		(1433, "Redridge Mountains", 0, -10022.916015625, -3741.6665039062, -8575, -1570.8332519531),
		(1434, "Stranglethorn Vale", 0, -15422.916015625, -4160.4165039062, -11168.75, 2220.8332519531),
		(1435, "Swamp of Sorrows", 0, -11150, -4516.6665039062, -9620.8330078125, -2222.9165039062),
		(1436, "Westfall", 0, -11733.333007812, -483.33331298828, -9400, 3016.6665039062),
		(1437, "Wetlands", 0, -4904.1665039062, -4525, -2147.9165039062, -389.58331298828),
		(1438, "Teldrassil", 1, 8437.5, -1277.0832519531, 11831.25, 3814.5832519531),
		(1439, "Darkshore", 1, 3966.6665039062, -3608.3332519531, 8333.3330078125, 2941.6665039062),
		(1440, "Ashenvale", 1, 829.16662597656, -4066.6665039062, 4672.9165039062, 1699.9998779297),
		(1441, "Thousand Needles", 1, -6899.9995117188, -4833.3330078125, -3966.6665039062, -433.33331298828),
		(1442, "Stonetalon Mountains", 1, -339.58331298828, -1637.4998779297, 2916.6665039062, 3245.8332519531),
		(1443, "Desolace", 1, -2545.8332519531, -262.5, 452.08331298828, 4233.3330078125),
		(1444, "Feralas", 1, -6999.9995117188, -1508.3332519531, -2366.6665039062, 5441.6665039062),
		(1445, "Dustwallow Marsh", 1, -5533.3330078125, -6225, -2033.3332519531, -974.99993896484),
		(1446, "Tanaris", 1, -10475, -7118.7495117188, -5875, -218.74998474121),
		(1447, "Azshara", 1, 1960.4166259766, -8347.916015625, 5341.6665039062, -3277.0832519531),
		(1448, "Felwood", 1, 3299.9997558594, -4108.3330078125, 7133.3330078125, 1641.6666259766),
		(1449, "Un'Goro Crater", 1, -8433.3330078125, -3166.6665039062, -5966.6665039062, 533.33331298828),
		(1450, "Moonglade", 1, 6952.0830078125, -3689.5832519531, 8491.666015625, -1381.25),
		(1451, "Silithus", 1, -8281.25, -945.833984375, -5958.333984375, 2537.5),
		(1452, "Winterspring", 1, 3799.9997558594, -7416.6665039062, 8533.3330078125, -316.66665649414),
		(1453, "Stormwind City", 0, -9175.205078125, 36.70063018799, -8278.8505859375, 1380.9714355469),
		(1454, "Orgrimmar", 1, 1338.4605712891, -5083.2055664062, 2273.8771972656, -3680.6010742188),
		(1455, "Ironforge", 0, -5096.845703125, -1504.2164306641, -4569.2412109375, -713.59136962891),
		(1456, "Thunder Bluff", 1, -1545.8332519531, -527.08331298828, -849.99993896484, 516.66662597656),
		(1457, "Darnassus", 1, 9532.5869140625, 1880.0295410156, 10238.31640625, 2938.3627929688),
		(1458, "Undercity", 0, 1237.8411865234, -86.18240356445, 1877.9453125, 873.19262695313),
	};

	/// <summary>Capital city map → the zone it sits in (where the game shows its name).</summary>
	static readonly Dictionary<int, int> CityZone = new() { [1453] = 1429, [1454] = 1411, [1455] = 1426, [1456] = 1412, [1457] = 1438, [1458] = 1420 };

	public static readonly IReadOnlyList<Map> Maps = BuildMaps();

	static List<Map> BuildMaps()
	{
		var list = new List<Map>
		{
			new(World, "Azeroth", Kind.World, null, null, null),
			new(1415, "Eastern Kingdoms", Kind.Continent, 0, World, new Region(-16000, -19199.900390625, 7466.6000976562, 16000)),
			new(1414, "Kalimdor", Kind.Continent, 1, World, new Region(-11733.299804688, -19733.2109375, 12799.900390625, 17066.599609375)),
		};
		list.AddRange(Zones
			.Select(z => new Map(z.Id, z.Name, Kind.Zone, z.Continent,
				CityZone.TryGetValue(z.Id, out var parent) ? parent : (z.Continent == 0 ? 1415 : 1414),
				new Region(z.XMin, z.YMin, z.XMax, z.YMax), CityZone.ContainsKey(z.Id)))
			.OrderBy(m => m.Name, StringComparer.Ordinal));
		return list;
	}

	/// <summary>Where each continent is drawn on the world map: its ui rect and the region it shows.</summary>
	static readonly Dictionary<int, (double U0, double V0, double U1, double V1, Region R)> WorldPlacement = new()
	{
		[0] = (0.55049997568, 0.09939999878, 0.89660000801, 0.86909997463, new Region(-16000, -7466.7001953125, 6933.2998046875, 8000)),
		[1] = (0.03990000114, 0.08550000191, 0.40830001235, 0.92339998484, new Region(-12800, -9600, 12266.700195312, 6933.2998046875)),
	};

	public static string ImagePath(int id) => $"res://assets/maps/wow/{id}.webp";
	public static Map Get(int id) => Maps.First(m => m.Id == id);
	public static Map ContinentMap(int continent) => Maps.First(m => m.Kind == Kind.Continent && m.Continent == continent);

	/// <summary>A point on a zone or continent image → game location.</summary>
	public static Location ToLocation(Map map, NormPoint p) => ToLocation(map.Continent!.Value, map.Region!.Value, p);

	static Location ToLocation(int continent, Region r, NormPoint p) =>
		new(continent, r.XMax - p.Y * (r.XMax - r.XMin), r.YMax - p.X * (r.YMax - r.YMin));

	/// <summary>Game location → point on a zone or continent image (may lie outside 0..1).</summary>
	public static NormPoint FromLocation(Map map, Location l) => FromLocation(map.Region!.Value, l);

	static NormPoint FromLocation(Region r, Location l) =>
		new((r.YMax - l.Y) / (r.YMax - r.YMin), (r.XMax - l.X) / (r.XMax - r.XMin));

	/// <summary>Game location → point on the world map image (how pins are stored).</summary>
	public static NormPoint LocationToWorld(Location l)
	{
		var w = WorldPlacement[l.Continent];
		var f = FromLocation(w.R, l);
		return new NormPoint(w.U0 + f.X * (w.U1 - w.U0), w.V0 + f.Y * (w.V1 - w.V0));
	}

	/// <summary>Where a continent, zone or city image lies on the world map image (0–1): left, top, width, height.</summary>
	public static (double X, double Y, double W, double H) WorldRect(Map map)
	{
		var r = map.Region!.Value;
		var c = map.Continent!.Value;
		var topLeft = LocationToWorld(new Location(c, r.XMax, r.YMax));
		var bottomRight = LocationToWorld(new Location(c, r.XMin, r.YMin));
		return (topLeft.X, topLeft.Y, bottomRight.X - topLeft.X, bottomRight.Y - topLeft.Y);
	}

	/// <summary>Where a continent is drawn on the world map image: left, top, right, bottom (0–1).</summary>
	public static (double U0, double V0, double U1, double V1) WorldPlacementOf(int continent)
	{
		var w = WorldPlacement[continent];
		return (w.U0, w.V0, w.U1, w.V1);
	}

	/// <summary>Which continent is drawn at this point of the world map, if any.</summary>
	public static int? ContinentAtWorld(NormPoint p)
	{
		foreach (var c in new[] { 0, 1 })
		{
			var w = WorldPlacement[c];
			if (p.X >= w.U0 && p.X <= w.U1 && p.Y >= w.V0 && p.Y <= w.V1) return c;
		}
		return null;
	}

	/// <summary>Point on the world map image → game location (null over open sea between continents).</summary>
	public static Location? WorldToLocation(NormPoint p)
	{
		if (ContinentAtWorld(p) is not { } c) return null;
		var w = WorldPlacement[c];
		var f = new NormPoint((p.X - w.U0) / (w.U1 - w.U0), (p.Y - w.V0) / (w.V1 - w.V0));
		return ToLocation(c, w.R, f);
	}

	/// <summary>
	/// The zone a location belongs to. Zone images overlap at their edges, so pick the zone whose
	/// centre the point is relatively closest to; cities must be well inside their small map.
	/// </summary>
	public static Map? ZoneAt(Location l)
	{
		Map? best = null;
		var bestScore = 1.0;
		foreach (var m in Maps)
		{
			if (m.Kind != Kind.Zone || m.Continent != l.Continent) continue;
			var p = FromLocation(m, l);
			var score = Math.Max(Math.Abs(p.X - 0.5), Math.Abs(p.Y - 0.5)) * 2 * (m.City ? 1.6 : 1);
			if (score < bestScore)
			{
				best = m;
				bestScore = score;
			}
		}
		return best;
	}

	/// <summary>
	/// WoW Forever's zones that Classic doesn't have (its zone art from OpenMap shows them, the zone list above doesn't):
	/// rough outlines on the world map (in its pixels, 1002 × 668), found with DevTools' --unzoned-mask, so hovering and
	/// "Jump to zone…" name them. Only names: distances stay in Classic's yards. Replace with Forever's own zone data once
	/// it's out (after 4 November 2026).
	/// </summary>
	public static readonly IReadOnlyList<(string Name, (double X, double Y)[] Outline)> ExtraZones = new[]
	{
		("Gilneas", new (double, double)[] { (665, 250), (705, 247), (718, 260), (713, 278), (680, 281), (664, 268) }),
		("Riverglades", new (double, double)[] { (832, 372), (871, 372), (877, 395), (875, 434), (805, 435), (802, 402), (832, 399) }),
	};

	/// <summary>A new (WoW Forever) zone at this point of the world map, if it's in one.</summary>
	public static string? ExtraZoneAt(NormPoint p)
	{
		double x = p.X * ImageWidth, y = p.Y * ImageHeight;
		foreach (var (name, outline) in ExtraZones)
		{
			// (point in polygon: count the edges a ray to the right crosses)
			var inside = false;
			for (int i = 0, j = outline.Length - 1; i < outline.Length; j = i++)
			{
				var (xi, yi) = outline[i];
				var (xj, yj) = outline[j];
				if (yi > y != yj > y && x < (xj - xi) * (y - yi) / (yj - yi) + xi) inside = !inside;
			}
			if (inside) return name;
		}
		return null;
	}

	/// <summary>The middle and size of a new zone's outline on the world map (0–1), to zoom to it.</summary>
	public static (double X, double Y, double W, double H) ExtraZoneRect(string name)
	{
		var o = ExtraZones.First(z => z.Name == name).Outline;
		double x0 = o.Min(p => p.X), x1 = o.Max(p => p.X), y0 = o.Min(p => p.Y), y1 = o.Max(p => p.Y);
		return (x0 / ImageWidth, y0 / ImageHeight, (x1 - x0) / ImageWidth, (y1 - y0) / ImageHeight);
	}

	const double YardKm = 0.9144 / 1000;

	/// <summary>
	/// Distance between two stored pins. Same continent: exact, in game yards. Different continents
	/// (no real path between them): the largest continent's diagonal plus the gap on the world map
	/// at its average scale, so a wrong-continent guess always ranks behind every right-continent one
	/// but nearer wrong guesses still beat farther ones.
	/// </summary>
	public static double DistanceKm(NormPoint a, NormPoint b)
	{
		var la = WorldToLocation(a);
		var lb = WorldToLocation(b);
		if (la is { } x && lb is { } y && x.Continent == y.Continent) return Hypot(x.X - y.X, x.Y - y.Y) * YardKm;
		var yardsPerWorldWidth = new[] { 0, 1 }.Average(c =>
		{
			var w = WorldPlacement[c];
			return (w.R.YMax - w.R.YMin) / (w.U1 - w.U0);
		});
		var aspect = (double)ImageHeight / ImageWidth;
		var maxContinentDiagonal = Maps.Where(m => m.Kind == Kind.Continent)
			.Max(m => Hypot(m.Region!.Value.XMax - m.Region.Value.XMin, m.Region.Value.YMax - m.Region.Value.YMin));
		return (maxContinentDiagonal + Hypot(a.X - b.X, (a.Y - b.Y) * aspect) * yardsPerWorldWidth) * YardKm;
	}

	/// <summary>The capital city in this zone, with where to draw its label (its centre, kept inside the map).</summary>
	public static IEnumerable<(Map City, NormPoint At)> CitiesOn(Map map) =>
		Maps.Where(m => m.City && CityZone[m.Id] == map.Id).Select(city =>
		{
			var r = city.Region!.Value;
			var at = FromLocation(map, new Location(city.Continent!.Value, (r.XMin + r.XMax) / 2, (r.YMin + r.YMax) / 2));
			static double Clamp(double v) => Math.Min(0.94, Math.Max(0.06, v));
			return (city, new NormPoint(Clamp(at.X), Clamp(at.Y)));
		});

	/// <summary>Where a click on a world or continent map leads, or null if nowhere.</summary>
	public static Map? ChildAt(Map map, NormPoint p)
	{
		if (map.Kind == Kind.World) return ContinentAtWorld(p) is { } c ? ContinentMap(c) : null;
		if (map.Kind == Kind.Continent) return ZoneAt(ToLocation(map, p));
		return null;
	}

	/// <summary>A stored world-map point on this map, if it lies on it.</summary>
	public static NormPoint? OnMap(Map map, NormPoint world)
	{
		if (map.Kind == Kind.World) return world;
		if (WorldToLocation(world) is not { } loc || loc.Continent != map.Continent) return null;
		var p = FromLocation(map, loc);
		return p.X >= 0 && p.X <= 1 && p.Y >= 0 && p.Y <= 1 ? p : null;
	}

	static double Hypot(double a, double b) => Math.Sqrt(a * a + b * b);
}
