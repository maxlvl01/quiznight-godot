// Built-in game maps with a known scale. Each WidthKm is the real width of exactly the image in
// res://assets/maps/<id>.webp; swapping the image for another crop breaks it.
//
// How each scale was derived (October 2026):
// - Skyrim / Oblivion: UESP gamemap world bounds (posLeft..posRight in game units), which map
//   onto the full square tile pyramid. 4096 units = 1 cell = 58.5 m, i.e. 64 units per yard.
//   Skyrim: -233600..291000, Oblivion: -262144..262144. Image = UESP zoom-4 tiles stitched.
// - GTA V: the common FiveM/Leaflet calibration (center 117.3/172.8, scale 0.02072/0.0205 per
//   256 px tile at zoom 0), game units = metres. x spans 256/0.02072 m; the y scale is ~1%
//   different, so north-south distances read ~1% short.
// - GTA IV: interactive-game-maps calibration lx = (x + 2571) * 0.0321 on a 6000 px wide image
//   (zoom 5 = 32 px per unit), i.e. 6000 / 32 / 0.0321 m. y scale differs by ~0.7%.
// - WoW Classic: a whole atlas (world → continents → zones) with exact game regions instead of
//   one scale; see WowAtlas. Its WidthKm is null and MapDistance measures in yards.

using System;
using System.Collections.Generic;
using System.Linq;

namespace QuizNight.Shared;

public sealed record MapPreset(string Id, string Name, double? WidthKm, string ImagePath, string Credit);

public static class MapPresets
{
	const double YardKm = 0.9144 / 1000;
	const double BethesdaUnitKm = YardKm / 64;

	/// <summary>Rounded to whole metres so the editor's km field stays readable.</summary>
	static double Round(double km) => Math.Round(km * 1000) / 1000;

	public static readonly IReadOnlyList<MapPreset> All = new List<MapPreset>
	{
		new("skyrim", "Skyrim", Round((291000 - -233600) * BethesdaUnitKm), "res://assets/maps/skyrim.webp",
			"Map: UESP.net (CC BY-SA 2.5). Skyrim © Bethesda."),
		new("oblivion", "TES IV: Oblivion – Cyrodiil", Round((262144 - -262144) * BethesdaUnitKm), "res://assets/maps/oblivion.webp",
			"Map: UESP.net (CC BY-SA 2.5). Oblivion © Bethesda."),
		new("gta5", "GTA V – Los Santos & Blaine County", Round(256 / 0.02072 / 1000), "res://assets/maps/gta5.webp",
			"Satellite map © Rockstar Games."),
		new("gta4", "GTA IV – Liberty City", Round(6000 / 32 / 0.0321 / 1000), "res://assets/maps/gta4.webp",
			"In-game map © Rockstar Games, via GTAvision.com."),
		new(WowAtlas.PresetId, "WoW Classic – Azeroth (one seamless map, zoom into any zone)", null, WowAtlas.ImagePath(WowAtlas.World),
			"Maps © Blizzard Entertainment; zones stitched by the OpenMap addon (MIT). Distances in game yards, exact per zone."),
	};

	public static MapPreset? Get(string? id) => id == null ? null : All.FirstOrDefault(p => p.Id == id);
}
