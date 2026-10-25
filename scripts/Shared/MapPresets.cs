// Built-in game maps with a known scale. Each WidthKm is the real width of exactly the image in
// res://assets/maps/<id>.webp; swapping the image for another crop breaks it.
//
// How each scale was derived (October 2026):
// - Skyrim / Oblivion: UESP gamemap world bounds (posLeft..posRight in game units), which map
//   onto the full square tile pyramid. 4096 units = 1 cell = 58.5 m, i.e. 64 units per yard.
//   Skyrim: -233600..291000, Oblivion: -262144..262144. Image = UESP zoom-4 tiles stitched.
//   Skyrim's 4096 px square was cropped to the map itself (x 56..3528, y 28..2920; the rest was flat
//   padding), so its width is 3472/4096 of those bounds. Quizzes saved with the square are moved over
//   by SkyrimCrop when they load.
// - GTA V: the common FiveM/Leaflet calibration (center 117.3/172.8, scale 0.02072/0.0205 per
//   256 px tile at zoom 0), game units = metres. x spans 256/0.02072 m; the y scale is ~1%
//   different, so north-south distances read ~1% short.
// - GTA IV: interactive-game-maps calibration lx = (x + 2571) * 0.0321 on a 6000 px wide image
//   (zoom 5 = 32 px per unit), i.e. 6000 / 32 / 0.0321 m. y scale differs by ~0.7%.
// - Zelda BotW / TotK (surface and Depths): zeldamods objmap tiles (zoom 5), 24000 x 20000 px at zoom 7 = 2 px per
//   game unit over x -6000..6000, z -5000..5000; units are metres, so the image is 12 x 10 km.
// - Elden Ring: Elden Ring Compass tiles; its master map is the m60 grid at 1 px per world unit (metre). Zoom 5 =
//   2 m per px. Lands Between = the 19 x 18 tiles that exist (4864 x 4608 px); Land of Shadow = a 2564 x 3072 px cut.
// - RDR2: Rockstar Social Club map tiles; the RDR2 Collectors Map (jeanropke) maps game metres with 0.01552 Leaflet
//   units per metre on a 176 x 144 unit map, i.e. 11340 m wide; we keep the 5420 of its 5632 zoom-5 px that are map.
// - Cyberpunk 2077: NC Zoning Board satellite tiles, whose square spans CET x -6298..5815 (metres); we keep
//   3716 of its 4096 zoom-4 px (the rest is empty).
// - GTA San Andreas: 6000 px satellite map of the -3000..3000 world square, 1 px per metre.
// - Forza Horizon 6: the FH6 Heatmap live map (calibrated to the game's position telemetry in metres) puts
//   3.5131 m in one px of its 6144 px image; we keep a 4900 px wide cut (less sea).
// - PUBG: official no-text maps (pubg/api-assets, 8192 px); the API gives each map's size in cm (Erangel,
//   Miramar, Taego, Vikendi, Deston 816000; Sanhok 408000).
// - WoW Classic: a whole atlas (world → continents → zones) with exact game regions instead of
//   one scale; see WowAtlas. Its WidthKm is null and MapDistance measures in yards.

using System;
using System.Collections.Generic;
using System.Linq;

namespace QuizNight.Shared;

/// <param name="AddedIn">The version that brought the map (e.g. "0.9.0b"): the editor's map grid marks it NEW for that
/// MAJOR.MINOR (all of 0.9.x). Null for the maps that were there before this existed.</param>
public sealed record MapPreset(string Id, string Name, double? WidthKm, string ImagePath, string Credit, string? AddedIn = null);

public static class MapPresets
{
	/// <summary>Dev (--pretend-new-map ID): treat that map as new, to see the badge.</summary>
	public static string? PretendNew;

	/// <summary>Whether the map came with this MAJOR.MINOR version (0.9.0b and 0.9.2b are both "0.9").</summary>
	public static bool IsNew(MapPreset preset, string version) =>
		preset.Id == PretendNew || preset.AddedIn is { } added && MinorOf(added) == MinorOf(version);

	static string MinorOf(string version) => string.Join('.', version.Split('.').Take(2));

	const double YardKm = 0.9144 / 1000;
	const double BethesdaUnitKm = YardKm / 64;

	/// <summary>Rounded to whole metres so the editor's km field stays readable.</summary>
	static double Round(double km) => Math.Round(km * 1000) / 1000;

	public static readonly IReadOnlyList<MapPreset> All = new List<MapPreset>
	{
		new("skyrim", "Skyrim", Round((291000 - -233600) * BethesdaUnitKm * SkyrimCrop.Width / SkyrimCrop.OldSize), "res://assets/maps/skyrim.webp",
			"Map: UESP.net (CC BY-SA 2.5). Skyrim © Bethesda."),
		new("oblivion", "TES IV: Oblivion – Cyrodiil", Round((262144 - -262144) * BethesdaUnitKm), "res://assets/maps/oblivion.webp",
			"Map: UESP.net (CC BY-SA 2.5). Oblivion © Bethesda."),
		new("gta5", "GTA V – Los Santos & Blaine County", Round(256 / 0.02072 / 1000), "res://assets/maps/gta5.webp",
			"Satellite map © Rockstar Games."),
		new("gta4", "GTA IV – Liberty City", Round(6000 / 32 / 0.0321 / 1000), "res://assets/maps/gta4.webp",
			"In-game map © Rockstar Games, via GTAvision.com."),
		new("botw", "Zelda: Breath of the Wild – Hyrule", 12, "res://assets/maps/botw.webp",
			"Map: zeldamods objmap (from the game's map). Zelda © Nintendo."),
		new("totk", "Zelda: Tears of the Kingdom – Hyrule", 12, "res://assets/maps/totk.webp",
			"Map: zeldamods objmap (from the game's map). Zelda © Nintendo."),
		new("totk-depths", "Zelda: Tears of the Kingdom – the Depths", 12, "res://assets/maps/totk-depths.webp",
			"Map: zeldamods objmap (from the game's map). Zelda © Nintendo."),
		new("eldenring", "Elden Ring – the Lands Between", Round(4864 * 2 / 1000.0), "res://assets/maps/eldenring.webp",
			"Map: Elden Ring Compass (from the game's map). Elden Ring © FromSoftware / Bandai Namco."),
		new("eldenring-dlc", "Elden Ring – Land of Shadow (DLC)", Round(2564 * 2 / 1000.0), "res://assets/maps/eldenring-dlc.webp",
			"Map: Elden Ring Compass (from the game's map). Elden Ring © FromSoftware / Bandai Namco."),
		new("rdr2", "Red Dead Redemption 2", Round(5420 / 5632.0 * 176 / 0.01552 / 1000), "res://assets/maps/rdr2.webp",
			"In-game map © Rockstar Games, via Rockstar Social Club; scale from the RDR2 Collectors Map."),
		new("cyberpunk", "Cyberpunk 2077 – Night City", Round(3716 / 4096.0 * (5815 - -6298) / 1000), "res://assets/maps/cyberpunk.webp",
			"Satellite map: NC Zoning Board. Unofficial fan work, not approved/endorsed by CD PROJEKT RED."),
		new("gtasa", "GTA San Andreas", 6, "res://assets/maps/gtasa.webp",
			"Satellite map via SAMAP. GTA San Andreas © Rockstar Games."),
		new("fh6", "Forza Horizon 6 – Japan", Round(4900 * 3.5131 / 1000), "res://assets/maps/fh6.webp",
			"Map: FH6 Heatmap (kthwang3). Forza Horizon 6 © Microsoft / Playground Games."),
		new("pubg-erangel", "PUBG – Erangel", 8.16, "res://assets/maps/pubg-erangel.webp", "Official map © KRAFTON (PUBG API assets)."),
		new("pubg-miramar", "PUBG – Miramar", 8.16, "res://assets/maps/pubg-miramar.webp", "Official map © KRAFTON (PUBG API assets)."),
		new("pubg-taego", "PUBG – Taego", 8.16, "res://assets/maps/pubg-taego.webp", "Official map © KRAFTON (PUBG API assets)."),
		new("pubg-vikendi", "PUBG – Vikendi", 8.16, "res://assets/maps/pubg-vikendi.webp", "Official map © KRAFTON (PUBG API assets)."),
		new("pubg-deston", "PUBG – Deston", 8.16, "res://assets/maps/pubg-deston.webp", "Official map © KRAFTON (PUBG API assets)."),
		new("pubg-sanhok", "PUBG – Sanhok", 4.08, "res://assets/maps/pubg-sanhok.webp", "Official map © KRAFTON (PUBG API assets)."),
		// WARDOGS: the game's terrain texture, darkened like its tactical map. Scale: 1 px = 1 m on the 16384 px (Bakurani) and
		// 32768 px (Ozeti, Zestafona) renders (XGamingServer's measuring tool); Bakurani's 16.4 km matches the 256 km² given
		// before launch.
		new("wardogs-bakurani", "WARDOGS – Bakurani", 16.384, "res://assets/maps/wardogs-bakurani.webp",
			"Map: game textures via WARDOGS Tools / XGamingServer, darkened. WARDOGS © Bulkhead / Team17.", "0.10.2b"),
		new("wardogs-ozeti", "WARDOGS – Ozeti", 32.768, "res://assets/maps/wardogs-ozeti.webp",
			"Map: game textures via WARDOGS Tools / XGamingServer, darkened. WARDOGS © Bulkhead / Team17.", "0.10.2b"),
		new("wardogs-zestafona", "WARDOGS – Zestafona", 32.768, "res://assets/maps/wardogs-zestafona.webp",
			"Map: game textures via WARDOGS Tools / XGamingServer, darkened. WARDOGS © Bulkhead / Team17.", "0.10.2b"),
		new(WowAtlas.PresetId, "WoW Classic – Azeroth (one seamless map, zoom into any zone)", null, WowAtlas.ImagePath(WowAtlas.World),
			"Maps © Blizzard Entertainment; zones stitched by the OpenMap addon (MIT). Distances in game yards, exact per zone."),
	};

	public static MapPreset? Get(string? id) => id == null ? null : All.FirstOrDefault(p => p.Id == id);
}

/// <summary>Where the cropped Skyrim map sits in the old 4096 px square (for quizzes saved before the crop).</summary>
public static class SkyrimCrop
{
	public const int OldSize = 4096;
	public const int Left = 56, Top = 28, Width = 3472, Height = 2892;

	/// <summary>A spot on the old square image, on the cropped one.</summary>
	public static NormPoint FromOld(NormPoint p) =>
		new(Math.Clamp((p.X * OldSize - Left) / Width, 0, 1), Math.Clamp((p.Y * OldSize - Top) / Height, 0, 1));

	/// <summary>Moves a quiz saved with the old square Skyrim map onto the cropped one (pins, size and scale).</summary>
	public static void Migrate(Quiz quiz)
	{
		foreach (var map in quiz.Maps.Where(m => m.PresetId == "skyrim" && m.Image.Width == OldSize && m.Image.Height == OldSize))
		{
			map.Image.Width = Width;
			map.Image.Height = Height;
			map.WidthKm = MapPresets.Get("skyrim")!.WidthKm;
			foreach (var q in quiz.Questions.Where(q => q.MapId == map.Id && q.Target != null))
				q.Target = FromOld(q.Target!.Value);
		}
	}
}
