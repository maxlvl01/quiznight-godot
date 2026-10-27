// The look (three skins: Quiznight Purple, dark purple with an orange accent and blue outlines; Pinke Geilheit, pastel
// pink and lavender on dusky rose; Classic, the web version's grey with yellow and teal) and small helpers for building screens in code.

using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using QuizNight.Shared;

namespace QuizNight.Ui;

public static class C
{
	// The palette's own colours (Use switches them): purple/orange/blue, or the Alpha skin's grey/yellow/teal, and so on.
	public static Color Bg, Panel, Panel2, Text, Muted, Accent, AccentInk;
	/// <summary>Outlines: the blue, dimmed into the background (focus and highlights use Blue itself).</summary>
	public static Color Border;
	/// <summary>Focus outlines, toggles, sliders, the caret.</summary>
	public static Color Blue;
	/// <summary>Write the answer (yellow in the purple palette, where orange is the accent).</summary>
	public static Color Orange;
	/// <summary>The party show look's thick outlines and the solid drop under buttons and cards (almost black).</summary>
	public static Color Ink;
	public static readonly Color Teal = new("#3fd0b0");
	public static readonly Color TealInk = new("#062019");
	public static readonly Color Coral = new("#ff6b7a");
	public static readonly Color Purple = new("#9d8cff");
	public static readonly Color Sky = new("#5cc8ff");
	public static readonly Color Pink = new("#ff8ad8");
	public static readonly Color Lime = new("#a8e05f");

	/// <summary>A–D tiles: coral, sky, the accent, teal.</summary>
	public static Color[] Choice = Array.Empty<Color>();

	/// <summary>Dark text on bright patches (medals, badges, coloured pills), the same in every skin (C.Bg is light in Daylight).</summary>
	public static readonly Color OnBright = new("#14151b");
	/// <summary>A light skin (Daylight): the logo's field is light too.</summary>
	public static bool Light;
	/// <summary>Text fields, insets and slider tracks: sunk into the background (near-white in a light skin).</summary>
	public static Color Well => Light ? new Color("#fbf8f3") : Bg.Darkened(0.3f);

	public static readonly Color Gold = new("#ffc93c");
	public static readonly Color Silver = new("#c7cbe0");
	public static readonly Color Bronze = new("#d39a6a");

	/// <summary>1st, 2nd and 3rd place: gold, silver, bronze.</summary>
	public static Color? Medal(int place) => place switch { 1 => Gold, 2 => Silver, 3 => Bronze, _ => null };

	static C() => Use(Skins.Default);

	/// <summary>Switches the skin's colours. The theme and screens built before keep the old ones: rebuild them (App.UseSkin).</summary>
	/// <summary>The skin in use now.</summary>
	public static string Skin = Skins.Default;

	public static void Use(string skin)
	{
		Skin = Skins.Valid(skin);
		// (Bg, Panel, Panel2, Border, Blue), (Text, Muted, Accent, AccentInk, Orange), Ink
		Color H(string hex) => new(hex);
		Light = false;
		switch (Skins.Valid(skin))
		{
			case Skins.Classic:
				(Bg, Panel, Panel2, Border, Blue) = (H("#121318"), H("#20232e"), H("#2b2f40"), H("#2e3142"), Teal);
				(Text, Muted, Accent, AccentInk, Orange) = (H("#ecedf3"), H("#8b8fa3"), H("#f5b942"), H("#1a1408"), H("#ff9f43"));
				Ink = H("#07080c");
				break;
			case Skins.Pink:
				(Bg, Panel, Panel2, Border, Blue) = (H("#26101f"), H("#3f1a37"), H("#532548"), H("#7a4a80"), H("#c9a7ff"));
				(Text, Muted, Accent, AccentInk, Orange) = (H("#fff0f7"), H("#d6abc7"), H("#ff9fd0"), H("#3a0f26"), H("#ffc9a3"));
				Ink = H("#14060f");
				break;
			// (Formula 1: carbon black and grey, F1 red, white, the yellow flag)
			case Skins.F1:
				(Bg, Panel, Panel2, Border, Blue) = (H("#0b0b0e"), H("#18181d"), H("#24242b"), H("#3b3b45"), H("#c8ccd6"));
				(Text, Muted, Accent, AccentInk, Orange) = (H("#f5f5f7"), H("#9b9ba8"), H("#e10600"), H("#ffffff"), H("#ffd400"));
				Ink = H("#000000");
				break;
			// (true black for OLED screens: the pixels are off)
			case Skins.Amoled:
				(Bg, Panel, Panel2, Border, Blue) = (H("#000000"), H("#0b0b0e"), H("#16161b"), H("#2a2a33"), H("#7c9cff"));
				(Text, Muted, Accent, AccentInk, Orange) = (H("#f2f2f5"), H("#8c8c99"), H("#ff8201"), H("#1a0b00"), H("#ffd23f"));
				Ink = H("#000000");
				break;
			// (light, but easy on the eyes: warm off-white, never pure white, dark plum text and outlines)
			case Skins.Daylight:
				(Bg, Panel, Panel2, Border, Blue) = (H("#e6e0d6"), H("#f4efe7"), H("#e9e2d6"), H("#c4b8a8"), H("#2f7fd8"));
				(Text, Muted, Accent, AccentInk, Orange) = (H("#2a2230"), H("#74697a"), H("#e8730c"), H("#ffffff"), H("#c98a00"));
				Ink = H("#3b3142");
				Light = true;
				break;
			// (1. FC Köln: red and white)
			case Skins.Effzeh:
				(Bg, Panel, Panel2, Border, Blue) = (H("#1a0507"), H("#2e0a0e"), H("#3e1015"), H("#7d1f27"), H("#f4f4f4"));
				(Text, Muted, Accent, AccentInk, Orange) = (H("#ffffff"), H("#dcaaae"), H("#ed1c24"), H("#ffffff"), H("#ffd23f"));
				Ink = H("#0d0203");
				break;
			// (October: pumpkin orange, witch purple, slime green)
			case Skins.Halloween:
				(Bg, Panel, Panel2, Border, Blue) = (H("#120a14"), H("#22122a"), H("#2f1839"), H("#5a2d6e"), H("#a06bff"));
				(Text, Muted, Accent, AccentInk, Orange) = (H("#f7efe6"), H("#b49cb9"), H("#ff7a00"), H("#1f0d00"), H("#a8e05f"));
				Ink = H("#07040a");
				break;
			// (Cologne carnival: the city's red and white; the confetti brings the colour)
			case Skins.Carnival:
				(Bg, Panel, Panel2, Border, Blue) = (H("#24060a"), H("#3e0d15"), H("#521320"), H("#9c3546"), H("#ffffff"));
				(Text, Muted, Accent, AccentInk, Orange) = (H("#fff8f2"), H("#e8b4ba"), H("#ffffff"), H("#c8102e"), H("#ffd23f"));
				Ink = H("#0f0204");
				break;
			// (February: rose, pink and red)
			case Skins.Valentine:
				(Bg, Panel, Panel2, Border, Blue) = (H("#26091a"), H("#40102d"), H("#55173d"), H("#a8447a"), H("#ff9ec7"));
				(Text, Muted, Accent, AccentInk, Orange) = (H("#fff0f6"), H("#e3a8c5"), H("#ff4f8b"), H("#ffffff"), H("#ffd1e3"));
				Ink = H("#12030b");
				break;
			// (December: pine green, Christmas red, gold)
			case Skins.Christmas:
				(Bg, Panel, Panel2, Border, Blue) = (H("#0a1912"), H("#12291f"), H("#1a392b"), H("#2f6a4e"), H("#e9f3ff"));
				(Text, Muted, Accent, AccentInk, Orange) = (H("#f5f9f5"), H("#a6c3b2"), H("#d8263c"), H("#ffffff"), H("#ffd23f"));
				Ink = H("#03100a");
				break;
			// (New Year: midnight blue, gold and champagne)
			case Skins.NewYear:
				(Bg, Panel, Panel2, Border, Blue) = (H("#070b1f"), H("#111a3a"), H("#18244d"), H("#3a4f8f"), H("#e9f3ff"));
				(Text, Muted, Accent, AccentInk, Orange) = (H("#f7f3e8"), H("#a9b2d2"), H("#ffc83d"), H("#1d1300"), H("#f3e2b0"));
				Ink = H("#02040d");
				break;
			default:
				(Bg, Panel, Panel2, Border, Blue) = (H("#150023"), H("#2b0b47"), H("#3a1260"), H("#1a5a85"), H("#00c9f6"));
				(Text, Muted, Accent, AccentInk, Orange) = (H("#efeaf6"), H("#a593bd"), H("#ff8201"), H("#2a1200"), H("#ffd23f"));
				Ink = H("#0d0016");
				break;
		}
		Choice = new[] { Coral, Sky, Accent, Teal };
	}

	public static Color ForType(string type) => type switch
	{
		QuestionType.MapPin => Accent,
		QuestionType.Estimation => Teal,
		QuestionType.Classic => Purple,
		QuestionType.Sound => Coral,
		QuestionType.Video => Sky,
		QuestionType.Image => Pink,
		QuestionType.Order => Lime,
		QuestionType.Write => Orange,
		QuestionType.Picture => Blue,
		QuestionType.Interactive => Gold,
		QuestionType.Steps => Teal,
		_ => Muted,
	};
}

/// <summary>
/// The colour skins. Halloween and Christmas aren't picked: in their season they come on by themselves (unless "Seasonal
/// skins" is off), and afterwards the skin the player chose is back. Developer mode can pick any skin any time.
/// </summary>
public static class Skins
{
	/// <summary>Quiznight Purple (its id is from when it was called Pinke Geilheit, so saved settings keep it).</summary>
	public const string Default = "pinke-geilheit";
	public const string Pink = "pastel-pink";
	public const string Classic = "classic";
	public const string F1 = "f1";
	public const string Amoled = "amoled";
	public const string Daylight = "daylight";
	public const string Effzeh = "effzeh";
	public const string Halloween = "halloween";
	public const string Christmas = "christmas";
	public const string NewYear = "new-year";
	public const string Carnival = "carnival";
	public const string Valentine = "valentine";

	public static readonly (string Id, string Label)[] All =
	{
		(Default, "Quiznight Purple"),
		(Pink, "Pinke Geilheit"),
		(Classic, "Alpha"),
		(F1, "Formula 1"),
		(Amoled, "AMOLED black"),
		(Daylight, "Daylight"),
		(Effzeh, "#effzeh"),
		(Halloween, "Halloween"),
		(Christmas, "Christmas"),
		(NewYear, "Happy New Year"),
		(Carnival, "Kölle Alaaf (Carnival)"),
		(Valentine, "Valentine's Day"),
	};

	/// <summary>Whether a skin can be used today (the seasonal ones only in their season).</summary>
	public static bool InSeason(string id) => id switch
	{
		Halloween => Seasons.IsHalloween(Seasons.Today),
		Christmas => Seasons.IsChristmas(Seasons.Today),
		NewYear => Seasons.IsNewYear(Seasons.Today),
		Carnival => Seasons.IsCarnival(Seasons.Today),
		Valentine => Seasons.IsValentine(Seasons.Today),
		_ => true,
	};

	public static bool IsSeasonal(string id) => id is Halloween or Christmas or NewYear or Carnival or Valentine;

	/// <summary>The skins Settings offers: all of them in developer mode, else the ones that aren't seasonal.</summary>
	public static (string Id, string Label)[] Available => All.Where(s => App.LanUnlocked || !IsSeasonal(s.Id)).ToArray();

	/// <summary>A known skin, else Quiznight Purple.</summary>
	public static string Valid(string? id) => All.Any(s => s.Id == id) ? id! : Default;

	/// <summary>
	/// The skin to show: a seasonal skin picked in developer mode stays (in its season, or while developer mode is on), the
	/// season's skin when it's Halloween or Christmas time (and seasonal skins are on), else the one the player chose.
	/// </summary>
	public static string Effective(string chosen, bool seasonal)
	{
		chosen = Valid(chosen);
		if (IsSeasonal(chosen))
		{
			if (InSeason(chosen) || App.LanUnlocked) return chosen;
			chosen = Default;
		}
		if (!seasonal) return chosen;
		// (the carnival week can fall in February: carnival first)
		var today = Seasons.Today;
		if (Seasons.IsCarnival(today)) return Carnival;
		if (Seasons.IsHalloween(today)) return Halloween;
		if (Seasons.IsChristmas(today)) return Christmas;
		if (Seasons.IsNewYear(today)) return NewYear;
		if (Seasons.IsValentine(today)) return Valentine;
		return chosen;
	}
}

/// <summary>Seasons for the Christmas song and the seasonal skins. Dev runs can pretend another day: --date 2026-12-10.</summary>
public static class Seasons
{
	static DateTime? _today;

	public static DateTime Today => _today ??= OS.GetCmdlineUserArgs() is var args && Array.IndexOf(args, "--date") is var i and >= 0 && i + 1 < args.Length
		&& DateTime.TryParse(args[i + 1], System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var d) ? d : DateTime.Now;

	/// <summary>1 to 26 December.</summary>
	public static bool IsChristmas(DateTime day) => day.Month == 12 && day.Day <= 26;

	/// <summary>27 December to 7 January (right after Christmas).</summary>
	public static bool IsNewYear(DateTime day) => day.Month == 12 && day.Day >= 27 || day.Month == 1 && day.Day <= 7;

	/// <summary>All of October.</summary>
	public static bool IsHalloween(DateTime day) => day.Month == 10;

	/// <summary>All of February.</summary>
	public static bool IsValentine(DateTime day) => day.Month == 2;

	/// <summary>
	/// Cologne carnival: 11 November (the season opens at 11:11) and the street carnival, Weiberfastnacht to Veilchendienstag
	/// (the Thursday 52 days before Easter Sunday to the Tuesday before Ash Wednesday).
	/// </summary>
	public static bool IsCarnival(DateTime day)
	{
		if (day.Month == 11 && day.Day == 11) return true;
		var easter = Easter(day.Year);
		return day.Date >= easter.AddDays(-52) && day.Date <= easter.AddDays(-47);
	}

	/// <summary>Easter Sunday (the Gregorian "anonymous" computus).</summary>
	public static DateTime Easter(int year)
	{
		int a = year % 19, b = year / 100, c = year % 100, d = b / 4, e = b % 4, f = (b + 8) / 25, g = (b - f + 1) / 3;
		int h = (19 * a + b - d - g + 15) % 30, i = c / 4, k = c % 4, l = (32 + 2 * e + 2 * i - h - k) % 7, m = (a + 11 * h + 22 * l) / 451;
		var month = (h + l - 7 * m + 114) / 31;
		var dayOfMonth = (h + l - 7 * m + 114) % 31 + 1;
		return new DateTime(year, month, dayOfMonth);
	}
}

/// <summary>
/// The party show look's type: Fredoka (rounded, a variable font: one file, any weight) for text, Lilita One (chunky
/// display) for headings, names and big buttons. Both ship in assets/fonts (OFL); a system font fills in any glyph they
/// lack (arrows, symbols) and stands in if the files are missing.
/// </summary>
public static class Fonts
{
	static readonly string[] Sans = { "Sora", "Segoe UI", "Arial" };
	static readonly SystemFont System = new() { FontNames = new[] { "Segoe UI", "Segoe UI Symbol", "Arial", "Noto Sans" } };

	static FontFile? Load(string path)
	{
		if (!ResourceLoader.Exists(path) || GD.Load<FontFile>(path) is not { } font) return null;
		font.Fallbacks = new Godot.Collections.Array<Font> { System };
		return font;
	}

	static readonly FontFile? Fredoka = Load("res://assets/fonts/Fredoka.ttf");
	static readonly FontFile? Lilita = Load("res://assets/fonts/LilitaOne-Regular.ttf");

	/// <summary>Fredoka at a weight (300–700), or the system font at that weight.</summary>
	static Font Weight(int weight)
	{
		if (Fredoka == null) return new SystemFont { FontNames = Sans, FontWeight = weight };
		var tag = (long)TextServerManager.GetPrimaryInterface().NameToTag("wght");
		return new FontVariation { BaseFont = Fredoka, VariationOpentype = new Godot.Collections.Dictionary { [tag] = weight } };
	}

	static readonly Font PartyRegular = Weight(450);
	static readonly Font PartyBold = Weight(600);
	static readonly Font PartyBlack = (Font?)Lilita ?? new SystemFont { FontNames = Sans, FontWeight = 800 };

	// Settings → Accessibility: a dyslexia-friendly font for all text (assets/fonts/OpenDyslexic-Regular / -Bold, .otf or .ttf)
	static FontFile? Any(string name) => Load($"res://assets/fonts/{name}.otf") ?? Load($"res://assets/fonts/{name}.ttf");
	/// <summary>
	/// OpenDyslexic runs wide: letters and words a little closer, so screens laid out for Fredoka still fit. (Squashing the glyphs with
	/// a transform made them thinner but kept their full width, so every word looked letter-spaced.)
	/// </summary>
	static Font? Narrow(FontFile? f) => f == null ? null : new FontVariation { BaseFont = f, SpacingGlyph = -2, SpacingSpace = -3 };
	static readonly Font? DyslexicRegular = Narrow(Any("OpenDyslexic-Regular"));
	static readonly Font? DyslexicBold = Narrow(Any("OpenDyslexic-Bold")) ?? DyslexicRegular;
	static bool _dyslexic;

	/// <summary>Whether the dyslexia-friendly font came with the game (Settings only offers it then).</summary>
	public static bool DyslexicAvailable => DyslexicRegular != null;

	/// <summary>Switches all text to the dyslexia-friendly font (or back). Screens and the theme built after this use it.</summary>
	public static void UseDyslexic(bool on)
	{
		_dyslexic = on && DyslexicAvailable;
		_spaced = null;
	}

	public static Font Regular => _dyslexic ? DyslexicRegular! : PartyRegular;
	public static Font Bold => _dyslexic ? DyslexicBold! : PartyBold;
	/// <summary>Headings, names, big numbers and buttons: Lilita One (the dyslexia font's bold when that's on).</summary>
	public static Font Black => _dyslexic ? DyslexicBold! : PartyBlack;
	static Font? _spaced;
	/// <summary>Small caps labels ("eyebrows") get extra letter spacing.</summary>
	public static Font Spaced => _spaced ??= _dyslexic ? Bold : new FontVariation { BaseFont = Bold, SpacingGlyph = 2 };
	/// <summary>
	/// Numbers (scores, places, times, room codes, number inputs): Fredoka bold with tabular figures, so digits are all one
	/// width and columns of numbers line up, in the game's own type (it was the system's Consolas, which looked out of place).
	/// </summary>
	public static Font Mono => _dyslexic ? DyslexicBold! : PartyNumbers;
	static readonly Font PartyNumbers = Numbers();

	static Font Numbers()
	{
		if (Fredoka == null) return new SystemFont { FontNames = new[] { "Consolas", "Courier New" }, FontWeight = 700 };
		var ts = TextServerManager.GetPrimaryInterface();
		return new FontVariation
		{
			BaseFont = Fredoka,
			VariationOpentype = new Godot.Collections.Dictionary { [(long)ts.NameToTag("wght")] = 650 },
			OpentypeFeatures = new Godot.Collections.Dictionary { [(long)ts.NameToTag("tnum")] = 1 },
		};
	}
}

public static class UiTheme
{
	public const int BaseFontSize = 16;

	public static StyleBoxFlat Box(Color bg, int radius, Color? border = null, int borderWidth = 1, float padX = 12, float padY = 8)
	{
		var s = new StyleBoxFlat { BgColor = bg };
		s.SetCornerRadiusAll(radius);
		if (border is { } b)
		{
			s.BorderColor = b;
			s.SetBorderWidthAll(borderWidth);
		}
		s.ContentMarginLeft = s.ContentMarginRight = padX;
		s.ContentMarginTop = s.ContentMarginBottom = padY;
		s.AntiAliasing = true;
		return s;
	}

	static StyleBoxFlat Outline(Color color, int radius, int width = 2)
	{
		var s = Box(Colors.Transparent, radius, color, width);
		s.DrawCenter = false;
		return s;
	}

	/// <summary>
	/// The party show look's box: a thick ink outline and a solid drop under it (a thicker bottom edge), so it looks like
	/// it stands up off the screen. pressed: the drop shrinks and the content moves down, as if pushed in.
	/// </summary>
	public static StyleBoxFlat Chunky(Color bg, int radius, float padX = 12, float padY = 8, int outline = 3, int depth = 5, bool pressed = false)
	{
		var s = Box(bg, radius, C.Ink, outline, padX, padY);
		var drop = pressed ? Math.Max(outline, depth / 3) : depth;
		s.BorderWidthBottom = outline + drop;
		// (the text is centred on the coloured face, not on face + edge; pushed in: it moves down by what the drop lost,
		// so the button's height stays)
		var half = depth / 2f;
		s.ContentMarginTop = Math.Max(0, padY - half) + (depth - drop);
		s.ContentMarginBottom = Math.Max(0, padY - half) + drop;
		return s;
	}

	/// <param name="outline">Ink outline width; <paramref name="depth"/> the solid edge under it. Small buttons get thin ones
	/// (a thick edge on a 30 px button looks crowded), big ones the full party show look.</param>
	static void ButtonType(Theme t, string type, Color bg, Color ink, Color? border, int radius = 16, float padX = 20, float padY = 9, bool chunky = true,
		int outline = 3, int depth = 5)
	{
		if (!chunky || bg.A == 0)
		{
			t.SetStylebox("normal", type, Box(bg, radius, border, 1, padX, padY));
			t.SetStylebox("hover", type, Box(bg, radius, border, 1, padX, padY));
			t.SetStylebox("pressed", type, Box(bg, radius, border, 1, padX, padY));
			t.SetStylebox("hover_pressed", type, Box(bg, radius, border, 1, padX, padY));
			t.SetStylebox("disabled", type, Box(bg, radius, border is { } b0 ? new Color(b0, 0.4f) : null, 1, padX, padY));
		}
		else
		{
			// (every solid button is drawn as a real key: KeyBox)
			StyleBox Face(Color c, bool pressed) => new KeyBox(c, radius, padX, padY, outline, depth, pressed);
			t.SetStylebox("normal", type, Face(bg, false));
			t.SetStylebox("hover", type, Face(bg.Lightened(0.1f), false));
			t.SetStylebox("pressed", type, Face(bg.Darkened(0.08f), true));
			t.SetStylebox("hover_pressed", type, Face(bg.Darkened(0.04f), true));
			var off = Face(new Color(bg, 0.45f), false);
			((KeyBox)off).Ink = new Color(C.Ink, 0.6f);
			t.SetStylebox("disabled", type, off);
		}
		var focus = Outline(C.Blue, radius + 2, 2);
		focus.ExpandMarginLeft = focus.ExpandMarginRight = focus.ExpandMarginTop = focus.ExpandMarginBottom = 3;
		t.SetStylebox("focus", type, focus);
		t.SetColor("font_color", type, ink);
		t.SetColor("font_hover_color", type, bg.A == 0 ? C.Text : ink);
		t.SetColor("font_pressed_color", type, ink);
		t.SetColor("font_hover_pressed_color", type, ink);
		t.SetColor("font_focus_color", type, ink);
		t.SetColor("font_disabled_color", type, new Color(ink, 0.4f));
		t.SetFont("font", type, Fonts.Bold);
	}

	public static Theme Build()
	{
		var t = new Theme { DefaultFont = Fonts.Regular, DefaultFontSize = BaseFontSize };

		ButtonType(t, "Button", C.Panel2, C.Text, null);
		foreach (var (name, bg, ink) in new[]
		{
			("AccentButton", C.Accent, C.AccentInk),
			("TealButton", C.Teal, C.TealInk),
			("CoralButton", C.Coral, new Color("#2a0a0e")),
		})
		{
			t.SetTypeVariation(name, "Button");
			ButtonType(t, name, bg, ink, null);
		}
		t.SetTypeVariation("LinkButton2", "Button");
		ButtonType(t, "LinkButton2", Colors.Transparent, C.Muted, null, 8, 4, 6, false);
		t.SetTypeVariation("SmallButton", "Button");
		ButtonType(t, "SmallButton", C.Panel2, C.Text, null, 10, 10, 4, outline: 2, depth: 3);
		t.SetTypeVariation("ToggleOn", "Button");
		ButtonType(t, "ToggleOn", C.Blue, C.Ink, null, 14, 14, 7, outline: 2, depth: 3);
		t.SetTypeVariation("ToggleOff", "Button");
		ButtonType(t, "ToggleOff", C.Panel2, C.Muted, null, 14, 14, 7, outline: 2, depth: 3);

		// OptionButton has its own theme type.
		ButtonType(t, "OptionButton", C.Panel2, C.Text, null, 14, 12, 7, outline: 2, depth: 3);

		// Cards and panels: outlined in ink, with a drop under the big ones
		t.SetStylebox("panel", "PanelContainer", Chunky(C.Panel, 24, 24, 22, 4, 7));
		foreach (var (name, box) in new (string, StyleBox)[]
		{
			("Card2", Chunky(C.Panel2, 18, 14, 12, 3, 4)),
			("Inset", Box(C.Light ? C.Well : C.Bg.Darkened(0.25f), 16, C.Ink, 3, 14, 12)),
			("Chip", Chunky(C.Panel2, 12, 10, 4, 2, 0)),
			("ChipMe", Chunky(C.Blue.Darkened(0.45f), 12, 10, 4, 2, 0)),
			("Pill", Chunky(C.Panel2, 12, 12, 4, 2, 0)),
			("Notice", Chunky(C.Panel2, 14, 14, 10, 2, 2)),
			("Bar", Box(C.Panel, 0, null, 0, 20, 12)),
			("Highlight", Chunky(C.Accent.Darkened(0.55f), 16, 14, 10, 3, 4)),
			("Scored", Chunky(C.Teal.Darkened(0.6f), 16, 14, 10, 3, 4)),
			("Clear", new StyleBoxEmpty()),
		})
		{
			t.SetTypeVariation(name, "PanelContainer");
			t.SetStylebox("panel", name, box);
		}

		// Text inputs
		foreach (var type in new[] { "LineEdit", "TextEdit" })
		{
			t.SetStylebox("normal", type, Box(C.Well, 14, C.Ink, 3, 14, 9));
			t.SetStylebox("focus", type, Outline(C.Accent, 14, 3));
			t.SetStylebox("read_only", type, Box(C.Panel2, 14, C.Ink, 3, 14, 9));
			t.SetColor("font_color", type, C.Text);
			t.SetColor("font_placeholder_color", type, new Color(C.Muted, 0.7f));
			t.SetColor("caret_color", type, C.Accent);
			t.SetColor("selection_color", type, new Color(C.Blue, 0.3f));
		}
		t.SetTypeVariation("CodeInput", "LineEdit");
		t.SetFont("font", "CodeInput", Fonts.Black);
		t.SetFontSize("font_size", "CodeInput", 30);
		t.SetTypeVariation("BigInput", "LineEdit");
		t.SetFont("font", "BigInput", Fonts.Mono);
		t.SetFontSize("font_size", "BigInput", 26);

		t.SetColor("font_color", "Label", C.Text);
		t.SetColor("font_color", "CheckBox", C.Text);
		t.SetColor("font_hover_color", "CheckBox", C.Text);
		t.SetColor("font_pressed_color", "CheckBox", C.Text);
		t.SetColor("font_hover_pressed_color", "CheckBox", C.Text);
		t.SetColor("font_focus_color", "CheckBox", C.Text);
		// CheckBox would otherwise inherit the button frame.
		foreach (var state in new[] { "normal", "hover", "pressed", "hover_pressed", "disabled", "focus" })
			t.SetStylebox(state, "CheckBox", new StyleBoxEmpty { ContentMarginLeft = 2, ContentMarginRight = 2, ContentMarginTop = 4, ContentMarginBottom = 4 });
		// Tick boxes in the skin's colours (SVG, so they stay sharp at any window size; no textures in headless runs)
		if (DisplayServer.GetName() != "headless")
		{
			static string Hex(Color c) => "#" + c.ToHtml(false);
			string Box(bool on, float alpha) =>
				$"<svg xmlns='http://www.w3.org/2000/svg' width='18' height='18' opacity='{alpha.ToString(System.Globalization.CultureInfo.InvariantCulture)}'>" +
				(on
					? $"<rect x='0.5' y='0.5' width='17' height='17' rx='4' fill='{Hex(C.Blue)}'/><path d='M4.5 9.2 7.7 12.4 13.5 5.8' fill='none' stroke='{Hex(C.Bg)}' stroke-width='2.4' stroke-linecap='round' stroke-linejoin='round'/>"
					: $"<rect x='1' y='1' width='16' height='16' rx='4' fill='{Hex(C.Bg)}' stroke='{Hex(C.Blue.Lerp(C.Bg, 0.35f))}' stroke-width='1.6'/>") +
				"</svg>";
			t.SetIcon("checked", "CheckBox", DpiTexture.CreateFromString(Box(true, 1)));
			t.SetIcon("unchecked", "CheckBox", DpiTexture.CreateFromString(Box(false, 1)));
			t.SetIcon("checked_disabled", "CheckBox", DpiTexture.CreateFromString(Box(true, 0.45f)));
			t.SetIcon("unchecked_disabled", "CheckBox", DpiTexture.CreateFromString(Box(false, 0.45f)));
		}

		// Popups and dialogs
		var popup = Box(C.Panel2, 14, C.Ink, 3, 6, 6);
		t.SetStylebox("panel", "PopupMenu", popup);
		t.SetStylebox("hover", "PopupMenu", Box(new Color(C.Blue, 0.2f), 6, null, 0, 8, 4));
		t.SetColor("font_color", "PopupMenu", C.Text);
		t.SetColor("font_hover_color", "PopupMenu", C.Text);
		t.SetColor("font_disabled_color", "PopupMenu", C.Muted);
		t.SetColor("font_separator_color", "PopupMenu", C.Muted);
		t.SetStylebox("panel", "AcceptDialog", Box(C.Panel, 0, null, 0, 20, 16));
		// Pop-ups' frame reaches up over their title bar, so the title sits on a solid background (else what's behind shows through).
		const int titleHeight = 40;
		foreach (var state in new[] { "embedded_border", "embedded_unfocused_border" })
		{
			var frame = Box(C.Panel2, 18, C.Ink, 4, 0, 0);
			frame.ExpandMarginTop = titleHeight;
			frame.ExpandMarginLeft = frame.ExpandMarginRight = frame.ExpandMarginBottom = 6;
			t.SetStylebox(state, "Window", frame);
		}
		t.SetConstant("title_height", "Window", titleHeight);
		t.SetColor("title_color", "Window", C.Text);
		t.SetFont("title_font", "Window", Fonts.Black);
		t.SetFontSize("title_font_size", "Window", 18);
		t.SetStylebox("panel", "TooltipPanel", Box(C.Panel2, 12, C.Ink, 3, 10, 6));
		t.SetColor("font_color", "TooltipLabel", C.Text);

		// Thin scroll bars
		foreach (var type in new[] { "VScrollBar", "HScrollBar" })
		{
			t.SetStylebox("scroll", type, Box(Colors.Transparent, 6, null, 0, 4, 4));
			t.SetStylebox("grabber", type, Box(C.Border, 6, null, 0, 4, 4));
			t.SetStylebox("grabber_highlight", type, Box(C.Muted, 6, null, 0, 4, 4));
			t.SetStylebox("grabber_pressed", type, Box(C.Muted, 6, null, 0, 4, 4));
		}
		t.SetStylebox("panel", "ScrollContainer", new StyleBoxEmpty());

		// Volume sliders: thin track, teal fill.
		t.SetStylebox("slider", "HSlider", Box(C.Light ? C.Border : C.Well, 8, C.Ink, 2, 0, 5));
		t.SetStylebox("grabber_area", "HSlider", Box(C.Accent, 8, C.Ink, 2, 0, 5));
		t.SetStylebox("grabber_area_highlight", "HSlider", Box(C.Accent.Lightened(0.15f), 8, C.Ink, 2, 0, 5));
		t.SetConstant("separation", "HSeparator", 12);
		t.SetStylebox("separator", "HSeparator", new StyleBoxLine { Color = C.Border, Thickness = 1 });
		return t;
	}
}

/// <summary>Builders that keep screen code short.</summary>
public static class U
{
	/// <summary>
	/// As a picture's or map's max height: no fixed height, it takes the space left in its column (down to
	/// FlexMinHeight). Its parents must expand vertically too (GrowV), or there is no space to take.
	/// </summary>
	public const float Flexible = 0;
	public const float FlexMinHeight = 150;

	/// <summary>The height a picture/map asks for: its natural height up to maxHeight, or a small minimum when flexible.</summary>
	public static float FitHeight(float maxHeight, float natural) => Mathf.Min(maxHeight > 0 ? maxHeight : FlexMinHeight, natural);

	/// <summary>Tags a picture, map or video as flexible: it takes the height that's left (see PropagateGrow).</summary>
	public static T MarkFlexible<T>(this T c) where T : Control
	{
		c.SetMeta("flexible", true);
		c.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
		return c;
	}

	/// <summary>
	/// Makes every container above a flexible child (MarkFlexible) expand too, so the flexible child really gets
	/// the height that's left. Returns whether anything inside is flexible.
	/// </summary>
	public static bool PropagateGrow(Control c)
	{
		var grows = c.HasMeta("flexible");
		if (c is Container and not ScrollContainer)
			foreach (var child in c.GetChildren().OfType<Control>())
				if (PropagateGrow(child)) grows = true;
		if (grows && c is Container) c.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
		return grows;
	}

	public static Label Text(string text, int size = 0, Color? color = null, Font? font = null, bool wrap = false,
		HorizontalAlignment align = HorizontalAlignment.Left)
	{
		var l = new Label { Text = text, HorizontalAlignment = align };
		if (size > 0) l.AddThemeFontSizeOverride("font_size", size);
		if (color is { } c) l.AddThemeColorOverride("font_color", c);
		if (font != null) l.AddThemeFontOverride("font", font);
		if (wrap)
		{
			l.AutowrapMode = TextServer.AutowrapMode.WordSmart;
			l.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		}
		return l;
	}

	public static Label Bold(string text, int size = 0, Color? color = null, bool wrap = false, HorizontalAlignment align = HorizontalAlignment.Left) =>
		Text(text, size, color, Fonts.Bold, wrap, align);

	public static Label Heading(string text, int size = 30, bool wrap = true) => Text(text, size, C.Text, Fonts.Black, wrap);

	public static Label Muted(string text, int size = 14, bool wrap = true, HorizontalAlignment align = HorizontalAlignment.Left) =>
		Text(text, size, C.Muted, null, wrap, align);

	/// <summary>Small caps label. Translated before upper-casing (the upper-case text is no translation key).</summary>
	public static Label Eyebrow(string text) => Text(Lang.T(text).ToUpper(), 12, C.Muted, Fonts.Spaced);

	/// <summary>Text the players or quiz author wrote: never run it through the translation.</summary>
	public static T Raw<T>(this T c) where T : Control
	{
		c.AutoTranslateMode = Node.AutoTranslateModeEnum.Disabled;
		return c;
	}

	public static Label Error(string text = "") =>
		Text(text, 15, C.Coral, null, true).With(l => l.Visible = text != "");

	public static Label Mono(string text, int size = 16, Color? color = null) => Text(text, size, color ?? C.Text, Fonts.Mono);

	public static Button Btn(string text, string variant = "", Action? onPress = null, int size = 0)
	{
		var b = new Button { Text = text, ThemeTypeVariation = variant, MouseDefaultCursorShape = Control.CursorShape.PointingHand };
		if (size > 0) b.AddThemeFontSizeOverride("font_size", size);
		if (onPress != null) b.Pressed += onPress;
		return b;
	}

	public static Button Small(string text, Action? onPress = null) => Btn(text, "SmallButton", onPress, 14);
	public static Button Link(string text, Action? onPress = null, int size = 15) => Btn(text, "LinkButton2", onPress, size);

	public static VBoxContainer V(int sep = 8, params Control[] kids)
	{
		var box = new VBoxContainer();
		box.AddThemeConstantOverride("separation", sep);
		foreach (var k in kids) box.AddChild(k);
		return box;
	}

	public static HBoxContainer H(int sep = 8, params Control[] kids)
	{
		var box = new HBoxContainer();
		box.AddThemeConstantOverride("separation", sep);
		foreach (var k in kids) box.AddChild(k);
		return box;
	}

	public static HFlowContainer Flow(int sep = 8, params Control[] kids)
	{
		var box = new HFlowContainer();
		box.AddThemeConstantOverride("h_separation", sep);
		box.AddThemeConstantOverride("v_separation", sep);
		foreach (var k in kids) box.AddChild(k);
		return box;
	}

	public static PanelContainer Panel(Control child, string variant = "")
	{
		var p = new PanelContainer { ThemeTypeVariation = variant };
		p.AddChild(child);
		return p;
	}

	public static PanelContainer Card(int sep, params Control[] kids) => Panel(V(sep, kids));

	public static Control Pill(string text, Color? color = null, Font? font = null) =>
		Panel(Text(text, 14, color ?? C.Text, font ?? Fonts.Bold), "Pill").With(p => p.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter);

	public static MarginContainer Pad(Control child, int left, int top, int right, int bottom)
	{
		var m = new MarginContainer();
		m.AddThemeConstantOverride("margin_left", left);
		m.AddThemeConstantOverride("margin_top", top);
		m.AddThemeConstantOverride("margin_right", right);
		m.AddThemeConstantOverride("margin_bottom", bottom);
		m.AddChild(child);
		return m;
	}

	public static MarginContainer Pad(Control child, int all) => Pad(child, all, all, all, all);

	public static Control Gap(int px) => new Control { CustomMinimumSize = new Vector2(px, px), MouseFilter = Control.MouseFilterEnum.Ignore };

	/// <summary>Takes all free space in a box (pushes the following items to the end).</summary>
	public static Control Fill() => new Control
	{
		SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
		SizeFlagsVertical = Control.SizeFlags.ExpandFill,
		MouseFilter = Control.MouseFilterEnum.Ignore,
	};

	public static T With<T>(this T c, Action<T> f)
	{
		f(c);
		return c;
	}

	public static T Grow<T>(this T c, float ratio = 1) where T : Control
	{
		c.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		c.SizeFlagsStretchRatio = ratio;
		return c;
	}

	public static T GrowV<T>(this T c) where T : Control
	{
		c.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
		return c;
	}

	public static T Center<T>(this T c) where T : Control
	{
		c.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
		return c;
	}

	public static T End<T>(this T c) where T : Control
	{
		c.SizeFlagsHorizontal = Control.SizeFlags.ShrinkEnd;
		return c;
	}

	public static T MinSize<T>(this T c, float w, float h = 0) where T : Control
	{
		c.CustomMinimumSize = new Vector2(w, h);
		return c;
	}

	public static T Tip<T>(this T c, string? tip) where T : Control
	{
		c.TooltipText = tip ?? "";
		return c;
	}

	public static void Clear(this Node n)
	{
		foreach (var child in n.GetChildren())
		{
			n.RemoveChild(child);
			child.QueueFree();
		}
	}

	public static LineEdit Input(string text = "", string placeholder = "", Action<string>? changed = null, int maxLength = 0, string variant = "")
	{
		var e = new LineEdit { Text = text, PlaceholderText = placeholder, MaxLength = maxLength, ThemeTypeVariation = variant };
		e.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		if (changed != null) e.TextChanged += s => changed(s);
		return e;
	}

	public static TextEdit Area(string text, string placeholder, Action<string> changed, int lines = 3, int maxLength = 0)
	{
		var e = new TextEdit
		{
			Text = text,
			PlaceholderText = placeholder,
			WrapMode = TextEdit.LineWrappingMode.Boundary,
			ScrollFitContentHeight = false,
		};
		e.CustomMinimumSize = new Vector2(0, 14 + lines * 26);
		e.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		e.TextChanged += () =>
		{
			if (maxLength > 0 && e.Text.Length > maxLength)
			{
				var caret = e.GetCaretColumn();
				e.Text = e.Text[..maxLength];
				e.SetCaretColumn(Math.Min(caret, maxLength));
			}
			changed(e.Text);
		};
		return e;
	}

	/// <summary>A label above an input, with an optional hint below.</summary>
	public static VBoxContainer Field(string label, Control input, string? hint = null)
	{
		var box = V(5, Text(label, 13, C.Muted), input);
		if (hint != null) box.AddChild(Muted(hint, 12));
		box.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		return box;
	}

	public static OptionButton Select((string Id, string Label)[] items, string selected, Action<string> changed)
	{
		var o = new OptionButton();
		for (var i = 0; i < items.Length; i++)
		{
			o.AddItem(items[i].Label, i);
			o.SetItemMetadata(i, items[i].Id);
			if (items[i].Id == selected) o.Selected = i;
		}
		o.ItemSelected += i => changed(o.GetItemMetadata((int)i).AsString());
		o.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		return o;
	}

	/// <summary>Two or more exclusive choices shown as buttons side by side.</summary>
	public static HBoxContainer Segmented((string Id, string Label)[] items, string selected, Action<string> changed)
	{
		var box = H(6);
		foreach (var (id, label) in items)
		{
			var b = Btn(label, id == selected ? "ToggleOn" : "ToggleOff", null, 14).Grow();
			b.ClipText = false;
			b.Pressed += () =>
			{
				foreach (var child in box.GetChildren().OfType<Button>()) child.ThemeTypeVariation = child == b ? "ToggleOn" : "ToggleOff";
				changed(id);
			};
			box.AddChild(b);
		}
		return box;
	}

	/// <summary>A page that scrolls, with its content centred at a readable width.</summary>
	public static ScrollContainer Page(Control content, float maxWidth = 960, int padTop = 36, int padBottom = -1)
	{
		var scroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
		scroll.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		var col = new Column { MaxWidth = maxWidth, PadTop = padTop, PadBottom = padBottom >= 0 ? padBottom : Mathf.Min(36, padTop) };
		col.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		col.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
		col.AddChild(content);
		scroll.AddChild(col);
		return scroll;
	}

	/// <summary>
	/// A player's name for seats and the podium: a clan tag ("[OTB] maxlvl") on its own small line above the name, so
	/// long tags don't crowd the name. reserveTag: keep that line's room even without a tag, so names (and what's under
	/// them) line up with seats that have one.
	/// </summary>
	public static Control PlayerName(string name, int size, HorizontalAlignment align = HorizontalAlignment.Center, bool reserveTag = false)
	{
		var (tag, rest) = Limits.SplitClanTag(name);
		var label = Bold(rest, size, null, false, align).Raw();
		label.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
		label.ClipText = true;
		label.TooltipText = name;
		label.MouseFilter = Control.MouseFilterEnum.Pass;
		if (tag == null && !reserveTag) return label;
		var tagLine = Text($"[{tag}]", Math.Max(10, size - 4), C.Accent, Fonts.Mono, false, align).Raw();
		if (tag == null) tagLine.Modulate = Colors.Transparent;
		var box = V(0, tagLine, label);
		box.MouseFilter = Control.MouseFilterEnum.Ignore;
		return box;
	}

	/// <summary>True if any of these names has a clan tag (then every seat keeps room for one: PlayerName's reserveTag).</summary>
	public static bool AnyClanTag(IEnumerable<string> names) => names.Any(n => Limits.SplitClanTag(n).Tag != null);

	public static string Plural(int n, string one, string many) => $"{n} {Lang.T(n == 1 ? one : many)}";

	public static string Initials(string name)
	{
		// A clan tag doesn't count ("[OTB] maxlvl" is "MA"); letters and digits only.
		name = Limits.SplitClanTag(name).Name;
		var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries)
			.Select(w => new string(w.Where(char.IsLetterOrDigit).ToArray())).Where(w => w != "").ToArray();
		var s = parts.Length > 1 ? $"{parts[0][0]}{parts[1][0]}" : parts.Length == 1 ? parts[0][..Math.Min(2, parts[0].Length)] : name.Trim()[..Math.Min(2, name.Trim().Length)];
		return s.ToUpperInvariant();
	}

	public static string FormatSeconds(double s) => $"{s.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture).Replace(".0", "")} s";

	public static string Clock(double seconds)
	{
		var s = (int)Math.Floor(seconds);
		return $"{s / 60}:{s % 60:00}";
	}
}

/// <summary>Centres its child at a maximum width (like a web page column).</summary>
public partial class Column : Container
{
	public float MaxWidth = 960;
	public float PadX = 16;
	public float PadTop = 36;
	public float PadBottom = 36;

	public override Vector2 _GetMinimumSize()
	{
		var min = Vector2.Zero;
		foreach (var c in GetChildren().OfType<Control>())
			if (c.Visible) min = min.Max(c.GetCombinedMinimumSize());
		return new Vector2(min.X + PadX * 2, min.Y + PadTop + PadBottom);
	}

	public override void _Notification(int what)
	{
		if (what != NotificationSortChildren) return;
		var w = Mathf.Min(Size.X - PadX * 2, MaxWidth);
		var x = (Size.X - w) / 2;
		foreach (var c in GetChildren().OfType<Control>()) FitChildInRect(c, new Rect2(x, PadTop, w, Size.Y - PadTop - PadBottom));
	}
}
