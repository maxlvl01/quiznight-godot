# More built-in maps for Map pin — research (October 2026)

Nothing here is implemented yet. A preset needs two things: **one full map image** and **its real width**
(so distances and scoring work, see `MapPresets.cs`). Games whose coordinates are in metres can be
calibrated exactly, like Skyrim and GTA V. For the others we only have estimates.

## Group 1: exact scale possible

| Game | Map | Scale | Image source |
|------|-----|-------|--------------|
| Zelda: Breath of the Wild | Hyrule | Game units are metres; the map spans about 12 × 10 km (x −6000..6000, z −5000..5000; check against objmap) | Game tiles stitched by BOTW-Map-Stitcher (24000 × 20000 px, 2 px per m) |
| Zelda: Tears of the Kingdom | Hyrule surface + Depths (+ Sky) | Same grid as BotW | zeldamods objmap-totk / tiles from the game |
| GTA San Andreas | San Andreas | World −3000..3000 in both directions, units are metres: 6 × 6 km | Community radar/satellite maps |
| PUBG | Erangel, Miramar, Taego (8 × 8 km), Vikendi (6 × 6), Sanhok (4 × 4) | Official sizes | Official/community full maps |
| Fallout 3, New Vegas, Fallout 4, Morrowind | Capital Wasteland, Mojave, Commonwealth, Vvardenfell | Same Bethesda units as Skyrim (64 per yard) | UESP (Morrowind), Fallout wiki maps (cell grids) |
| Red Dead Redemption 2 | The whole map | Units are metres; the open-source jeanropke RDR2 collectors map (Leaflet) is calibrated to game coordinates | Same project's tiles |
| Cyberpunk 2077 | Night City + Badlands | Units are metres; about 7.5 km × 11 km. Needs a two-point calibration | PowerPyx full map, or community maps with coordinates |
| The Witcher 3 | White Orchard, Velen/Novigrad, Skellige, Kaer Morhen, Toussaint | Units are metres, but each region is a separate map | Per region, so several presets or a small atlas like WoW |

## Group 2: popular, but scale is only estimated

- **Forza Horizon 6 (Japan, out since May 2026):** Playground published no size. Estimates are 100–135 km² (some
  sites say up to 246). Could be calibrated with the in-game GPS route length on a long straight road. Same for
  **FH5 (Mexico)** and **FH4 (Britain)**.
- **Elden Ring** (Lands Between + Shadow of the Erdtree), **Genshin Impact** (Teyvat, official HoYoLAB map, grows every
  patch), **Ghost of Tsushima / Ghost of Yōtei**, **Horizon Forbidden West**, **Hogwarts Legacy**, **Assassin's Creed**
  (Valhalla, Odyssey, Shadows), **Death Stranding**, **Crimson Desert** (2026), **Warzone** (Verdansk), **Fortnite**
  (changes every season).
- **Spider-Man 2:** the city is real Manhattan/Queens/Brooklyn, so real-world distances could work.
- **GTA VI** is due on 19 November 2026; community maps appear quickly after a launch. Add later.

## WoW as smooth maps (like the Classic atlas)

- **Retail WoW:** the same approach as the Classic atlas (`WowAtlas`), but with retail data. Zone rectangles in
  world yards come from the UiMap, UiMapAssignment and UiMapArtTile tables (CSV on wago.tools). The zone art comes
  from the game files (wow.export can read them from Blizzard's CDN without an install; BLP → WebP like
  `tools/openmap_tiles.gd`). Many more continents than Classic (Outland, Northrend, Pandaria, Draenor, Broken Isles,
  Kul Tiras/Zandalar, Shadowlands, Dragon Isles, Khaz Algar, Quel'Thalas …), so hundreds of zones. Do it in steps,
  starting with the post-Cataclysm Eastern Kingdoms and Kalimdor.
- **WoW Forever** (Blizzard's "Classic+", launches 4 November 2026; beta until about 21 October): original
  Azeroth plus new zones: the Riverglades, Mount Hyjal and Shen'dralas on Azeroth, and Zephras Isle (Skyborne
  start zone, in Skywall, so its own map). The Classic atlas covers most of it; the new zones' art should come
  from the live client after launch. The MapDumper Forever addon (CurseForge) dumps the UiMap IDs and names.

## Suggested order

1. BotW, TotK (surface + Depths), RDR2, Cyberpunk 2077, GTA San Andreas: exact scale, big audience.
2. Forza Horizon 6, Elden Ring, The Witcher 3, Fallout 4, PUBG Erangel.
3. Retail WoW atlas, then WoW Forever once it's live.

Licensing: same as the current presets (private program for friends, credit line per map).
