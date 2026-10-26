// The WoW Forever map (OpenMap's art; the zones and distances are Classic's, which Forever builds on) as one seamless, zoomable world, like the OpenMap addon: zoom out and you see
// Azeroth, zoom in and the continent comes in, then every zone stitched together, then the cities.
// Pins are placed anywhere and stored as a point on the world map image, as before (WowAtlas).
//
// Layers, bottom to top, and the zoom each fades in at (zoom 1 = the whole world fits; OpenMap's):
//   world map           always
//   continent maps      1.3–2.2   soft edges, so their parchment frame never shows (what's left of them shows
//                                 only where the zone art has gaps)
//   zone art, whole     1.1–1.5   all the stitched zones as one map, downsized (assets/maps/wow/overview-2/-4,
//                                 tools/openmap_overview.py): the sharper one once a world-map pixel is over 2 px
//   stitched zones      4–6 px per world-map pixel: OpenMap's zone art baked into tiles (assets/maps/wow/stitched,
//                                 made by tools/openmap_tiles.gd), loaded in the background
//   cities              once the city fills a fifth to two fifths of the view, cut round

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using QuizNight.Shared;
using QuizNight.Storage;

namespace QuizNight.Ui;

/// <summary>The seamless map's art: continents and cities cut to shape, and the stitched zone tiles.</summary>
public static class WowMapArt
{
	const string TileDir = "res://assets/maps/wow/stitched/";
	// OpenMap's bake: 16 px per world-map pixel, in 512-px tiles of which the middle 510 px are the
	// tile's own (the outer pixel is its neighbours' edge, there so tiles meet without seams).
	const double TilePx = 16, TileStep = 510;

	public sealed record Tile(string Path, Rect2 World);

	/// <summary>Every tile, with the part of the world map (0–1) it covers.</summary>
	public static readonly IReadOnlyList<Tile> Tiles = FindTiles();
	public static readonly Rect2 TileSource = new(1, 1, (float)TileStep, (float)TileStep);

	/// <summary>All the tiles as one map, downsized to px pixels per world-map pixel (2 or 4; tools/openmap_overview.py).</summary>
	public static string Overview(int px) => $"res://assets/maps/wow/overview-{px}.webp";

	static List<Tile> FindTiles()
	{
		var list = new List<Tile>();
		var size = TileStep / TilePx; // world-map pixels per tile
		for (var i = 0; i < 40; i++)
		for (var j = 0; j < 30; j++)
		{
			var path = $"{TileDir}{i}_{j}.webp";
			if (!FileAccess.FileExists(Images.BuiltinFile(path))) continue;
			list.Add(new Tile(path, new Rect2(
				(float)(i * size / WowAtlas.ImageWidth), (float)(j * size / WowAtlas.ImageHeight),
				(float)(size / WowAtlas.ImageWidth), (float)(size / WowAtlas.ImageHeight))));
		}
		return list;
	}

	// Decoded tiles, most recently drawn last. Only the newest MaxTiles are kept (each is ~1.4 MB of video
	// memory; a full view at the zoom where zones appear needs about 45).
	const int MaxTiles = 120;
	static readonly Dictionary<string, Texture2D?> Loaded = new();
	static readonly LinkedList<string> Recent = new();
	static readonly Dictionary<string, Task<Image?>> Loading = new();

	/// <summary>A tile's texture, or null while it loads in the background (call again next frame).</summary>
	public static Texture2D? TileTexture(string path)
	{
		if (Loaded.TryGetValue(path, out var tex))
		{
			Recent.Remove(path);
			Recent.AddLast(path);
			return tex;
		}
		if (!Loading.TryGetValue(path, out var task))
		{
			Loading[path] = Task.Run(() => Images.LoadBuiltinImage(path));
			return null;
		}
		if (!task.IsCompleted) return null;
		Loading.Remove(path);
		Loaded[path] = tex = task.IsCompletedSuccessfully && task.Result is { } img ? ImageTexture.CreateFromImage(img) : null;
		Recent.AddLast(path);
		while (Recent.Count > MaxTiles)
		{
			Loaded.Remove(Recent.First!.Value);
			Recent.RemoveFirst();
		}
		return tex;
	}

	static readonly Dictionary<int, Texture2D?> Cut = new();

	/// <summary>A continent or city map with transparent edges (made once, on first use).</summary>
	public static Texture2D? Shaped(WowAtlas.Map map)
	{
		if (Cut.TryGetValue(map.Id, out var tex)) return tex;
		var source = Images.BuiltinTexture(WowAtlas.ImagePath(map.Id))?.GetImage();
		if (source == null) return Cut[map.Id] = null;
		var img = (Image)source.Duplicate();
		if (img.IsCompressed()) img.Decompress();
		img.ClearMipmaps();
		img.Convert(Image.Format.Rgba8);
		int w = img.GetWidth(), h = img.GetHeight();
		var data = img.GetData();
		var rect = WowAtlas.WorldRect(map);
		var place = map.Kind == WowAtlas.Kind.Continent ? WowAtlas.WorldPlacementOf(map.Continent!.Value) : default;
		for (var y = 0; y < h; y++)
		for (var x = 0; x < w; x++)
		{
			double u = (x + 0.5) / w, v = (y + 0.5) / h;
			double alpha;
			if (map.Kind == WowAtlas.Kind.Continent)
			{
				// Only where the world map draws this continent, fading out before the map's own frame.
				double wx = rect.X + u * rect.W, wy = rect.Y + v * rect.H;
				var inside = Math.Min(Math.Min(wx - place.U0, place.U1 - wx), Math.Min(wy - place.V0, place.V1 - wy));
				var edge = Math.Min(Math.Min(u, 1 - u), Math.Min(v, 1 - v));
				alpha = Smooth(-0.015, 0.02, inside) * Smooth(0, 0.05, edge);
			}
			else
			{
				// A city: round, so its map's dull surroundings drop away into the zone around it.
				var d = Math.Sqrt((u - 0.5) * (u - 0.5) + (v - 0.5) * (v - 0.5)) * 2;
				alpha = Smooth(0.95, 0.55, d);
			}
			data[(y * w + x) * 4 + 3] = (byte)(data[(y * w + x) * 4 + 3] * alpha);
		}
		var shaped = Image.CreateFromData(w, h, false, Image.Format.Rgba8, data);
		shaped.GenerateMipmaps();
		return Cut[map.Id] = ImageTexture.CreateFromImage(shaped);
	}

	static double Smooth(double from, double to, double x)
	{
		var t = Math.Clamp((x - from) / (to - from), 0, 1);
		return t * t * (3 - 2 * t);
	}
}

/// <summary>The seamless WoW map canvas: zoom from the whole world down into a city.</summary>
public partial class WowMapCanvas : MapCanvas
{
	static readonly WowAtlas.Map[] Continents = WowAtlas.Maps.Where(m => m.Kind == WowAtlas.Kind.Continent).ToArray();
	static readonly WowAtlas.Map[] Cities = WowAtlas.Maps.Where(m => m.City).ToArray();
	static readonly bool Headless = DisplayServer.GetName() == "headless";

	// The view set by Show, kept while the canvas settles its size (it resizes a few times while the
	// screen lays out) until the user zooms or drags themselves.
	(NormPoint Center, float Zoom)? _shown;

	public WowMapCanvas()
	{
		MaxZoom = 40;
		WheelStep = 0.2f;
		HoverLabel = PlaceName;
	}

	/// <summary>Zooms to a spot, also before the canvas has its final size.</summary>
	public void Show(NormPoint center, float zoom)
	{
		_shown = (center, zoom);
		if (Size.X > 0) ZoomTo(center, zoom);
	}

	/// <summary>Zoom that fits a part of the world map (0–1 coordinates) into the view, with some room round it.</summary>
	public static float ZoomFor(double width, double height) =>
		(float)Math.Min(0.8 / Math.Max(width, 0.004), 0.8 / Math.Max(height, 0.006));

	public override void _Notification(int what)
	{
		base._Notification(what);
		if (what == NotificationResized && _shown is { } v && Size.X > 0) ZoomTo(v.Center, v.Zoom);
	}

	public override void _GuiInput(InputEvent e)
	{
		if (e is InputEventMouseButton { Pressed: true } or InputEventMagnifyGesture or InputEventPanGesture) _shown = null;
		base._GuiInput(e);
	}

	static string? PlaceName(NormPoint p)
	{
		if (WowAtlas.WorldToLocation(p) is not { } loc) return null;
		return WowAtlas.ZoneAt(loc)?.Name ?? WowAtlas.ContinentMap(loc.Continent).Name;
	}

	static float Ramp(float from, float to, float x) => Mathf.SmoothStep(from, to, x);

	protected override void DrawMap(Rect2 rect)
	{
		// (no renderer without a window, e.g. bots: nothing to draw)
		if (Headless) return;
		var world = Images.BuiltinTexture(WowAtlas.ImagePath(WowAtlas.World));
		if (world != null) DrawTextureRect(world, rect, false);
		var view = VisibleNorm();
		var zoom = Zoom;
		Rect2 OnScreen(Rect2 n) => new(rect.Position + n.Position * rect.Size, n.Size * rect.Size);
		static Rect2 R((double X, double Y, double W, double H) r) => new((float)r.X, (float)r.Y, (float)r.W, (float)r.H);

		var continents = Ramp(1.3f, 2.2f, zoom);
		if (continents > 0)
			foreach (var c in Continents)
			{
				var at = R(WowAtlas.WorldRect(c));
				if (at.Intersects(view) && WowMapArt.Shaped(c) is { } tex) DrawTextureRect(tex, OnScreen(at), false, new Color(1, 1, 1, continents));
			}

		// The zone art put together (tools/openmap_overview.py), the size that's sharp enough for this zoom: from the
		// first zoom steps on, the land looks the same as in the zone tiles, only less detailed.
		var waiting = false;
		var perWorldPx = rect.Size.X * GetViewport().GetFinalTransform().Scale.X / WowAtlas.ImageWidth;
		var overview = Ramp(1.1f, 1.5f, zoom);
		if (overview > 0)
		{
			var sharp = perWorldPx > 2 ? WowMapArt.TileTexture(WowMapArt.Overview(4)) : null;
			var art = sharp ?? WowMapArt.TileTexture(WowMapArt.Overview(2));
			if (art != null) DrawTextureRect(art, rect, false, new Color(1, 1, 1, overview));
			if (art == null || perWorldPx > 2 && sharp == null) waiting = true;
		}

		// The tiles themselves once the overview's pixels would show (OpenMap's 16 px per world-map pixel).
		var zones = Ramp(4f, 6f, perWorldPx);
		if (zones > 0)
			foreach (var tile in WowMapArt.Tiles)
			{
				if (!tile.World.Intersects(view)) continue;
				// (a hair larger, so neighbouring tiles overlap instead of leaving a hairline between them)
				var at = OnScreen(tile.World).Grow(0.5f);
				if (WowMapArt.TileTexture(tile.Path) is { } tex) DrawTextureRectRegion(tex, at, WowMapArt.TileSource, new Color(1, 1, 1, zones));
				else waiting = true;
			}

		foreach (var city in Cities)
		{
			var at = R(WowAtlas.WorldRect(city));
			if (!at.Intersects(view)) continue;
			// Its own map takes over once the city fills a fifth to two fifths of the view.
			var share = Mathf.Max(at.Size.X / view.Size.X, at.Size.Y / view.Size.Y);
			var a = Ramp(0.2f, 0.4f, share) * zones;
			if (a > 0 && WowMapArt.Shaped(city) is { } tex) DrawTextureRect(tex, OnScreen(at), false, new Color(1, 1, 1, a));
		}

		// Tiles still loading: look again next frame.
		if (waiting) Callable.From(QueueRedraw).CallDeferred();
	}
}

/// <summary>
/// The WoW Forever map for map pin questions: one seamless map from the whole world down to the cities
/// (scroll to zoom, drag to move, or jump straight to a zone). Pins are stored as a point on the world
/// map (see WowAtlas).
/// </summary>
public partial class WowAtlasView : VBoxContainer
{
	public readonly WowMapCanvas Canvas = new();

	public WowAtlasView() : this(null, null) { }

	public WowAtlasView(NormPoint? target, Action<NormPoint>? onPick, List<MapPin>? pins = null, float maxHeight = 560)
	{
		AddThemeConstantOverride("separation", 8);
		SizeFlagsHorizontal = SizeFlags.ExpandFill;

		var places = WowAtlas.Maps.Where(m => m.Kind == WowAtlas.Kind.Zone).OrderBy(m => m.Name, StringComparer.Ordinal).ToList();
		var items = new List<(string, string)> { ("", "Jump to zone…") };
		items.AddRange(places.Select(z => (z.Id.ToString(), z.Name)));
		var jump = U.Select(items.ToArray(), "", id =>
		{
			if (!int.TryParse(id, out var zoneId)) return;
			var r = WowAtlas.WorldRect(WowAtlas.Get(zoneId));
			Canvas.Show(new NormPoint(r.X + r.W / 2, r.Y + r.H / 2), WowMapCanvas.ZoomFor(r.W, r.H));
		});
		jump.CustomMinimumSize = new Vector2(220, 0);
		AddChild(U.H(8, U.Fill(), jump));

		Canvas.Target = target;
		Canvas.Pins = pins ?? new List<MapPin>();
		if (onPick != null)
			Canvas.OnPick = p =>
			{
				Canvas.Target = p;
				Canvas.QueueRedraw();
				onPick(p);
			};
		AddChild(new MapView("", null, WowAtlas.ImageWidth, WowAtlas.ImageHeight, maxHeight, Canvas));
		if (maxHeight <= 0) SizeFlagsVertical = SizeFlags.ExpandFill;
		if (onPick != null) AddChild(U.Muted("Zoom in for more detail: first the zones, then the cities. Click to place the pin.", 12));

		// Start on what matters: the pin and everyone's guesses, or the zone of an existing pin.
		var points = Canvas.Pins.Select(p => p.At).ToList();
		if (target is { } t) points.Add(t);
		if (points.Count == 1 && WowAtlas.WorldToLocation(points[0]) is { } loc && WowAtlas.ZoneAt(loc) is { } zone)
		{
			var r = WowAtlas.WorldRect(zone);
			Canvas.Show(points[0], WowMapCanvas.ZoomFor(r.W, r.H));
		}
		else if (points.Count > 1)
		{
			double x0 = points.Min(p => p.X), x1 = points.Max(p => p.X), y0 = points.Min(p => p.Y), y1 = points.Max(p => p.Y);
			Canvas.Show(new NormPoint((x0 + x1) / 2, (y0 + y1) / 2), Math.Min(WowMapCanvas.ZoomFor(x1 - x0, y1 - y0), 16));
		}
	}
}
