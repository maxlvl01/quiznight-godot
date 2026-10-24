// The look (two skins: Pinke Geilheit, dark purple with an orange accent and blue outlines; Classic, the web version's
// grey with yellow and teal) and small helpers for building screens in code.

using System;
using System.Linq;
using Godot;
using QuizNight.Shared;

namespace QuizNight.Ui;

public static class C
{
	// The palette's own colours (Use switches them): purple/orange/blue, or the classic grey/yellow/teal.
	public static Color Bg, Panel, Panel2, Text, Muted, Accent, AccentInk;
	/// <summary>Outlines: the blue, dimmed into the background (focus and highlights use Blue itself).</summary>
	public static Color Border;
	/// <summary>Focus outlines, toggles, sliders, the caret.</summary>
	public static Color Blue;
	/// <summary>Write the answer (yellow in the purple palette, where orange is the accent).</summary>
	public static Color Orange;
	public static readonly Color Teal = new("#3fd0b0");
	public static readonly Color TealInk = new("#062019");
	public static readonly Color Coral = new("#ff6b7a");
	public static readonly Color Purple = new("#9d8cff");
	public static readonly Color Sky = new("#5cc8ff");
	public static readonly Color Pink = new("#ff8ad8");
	public static readonly Color Lime = new("#a8e05f");

	/// <summary>A–D tiles: coral, sky, the accent, teal.</summary>
	public static Color[] Choice = Array.Empty<Color>();

	public static readonly Color Gold = new("#ffc93c");
	public static readonly Color Silver = new("#c7cbe0");
	public static readonly Color Bronze = new("#d39a6a");

	/// <summary>1st, 2nd and 3rd place: gold, silver, bronze.</summary>
	public static Color? Medal(int place) => place switch { 1 => Gold, 2 => Silver, 3 => Bronze, _ => null };

	static C() => Use(Skins.Default);

	/// <summary>Switches the skin's colours. The theme and screens built before keep the old ones: rebuild them (App.UseSkin).</summary>
	public static void Use(string skin)
	{
		if (Skins.Valid(skin) == Skins.Classic)
		{
			(Bg, Panel, Panel2, Border, Blue) = (new("#121318"), new("#1b1d26"), new("#232634"), new("#2e3142"), Teal);
			(Text, Muted, Accent, AccentInk, Orange) = (new("#ecedf3"), new("#8b8fa3"), new("#f5b942"), new("#1a1408"), new("#ff9f43"));
		}
		else
		{
			(Bg, Panel, Panel2, Border, Blue) = (new("#150023"), new("#1f0833"), new("#2a1042"), new("#1a5a85"), new("#00c9f6"));
			(Text, Muted, Accent, AccentInk, Orange) = (new("#efeaf6"), new("#a593bd"), new("#ff8201"), new("#2a1200"), new("#ffd23f"));
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
		_ => Muted,
	};
}

/// <summary>The colour skins to choose from in Settings.</summary>
public static class Skins
{
	public const string Default = "pinke-geilheit";
	public const string Classic = "classic";

	public static readonly (string Id, string Label)[] All =
	{
		(Default, "Pinke Geilheit"),
		(Classic, "Classic"),
	};

	public static string Valid(string? id) => All.Any(s => s.Id == id) ? id! : Default;
}

public static class Fonts
{
	static readonly string[] Sans = { "Sora", "Segoe UI", "Arial" };
	public static readonly Font Regular = new SystemFont { FontNames = Sans };
	public static readonly Font Bold = new SystemFont { FontNames = Sans, FontWeight = 700 };
	public static readonly Font Black = new SystemFont { FontNames = Sans, FontWeight = 800 };
	/// <summary>Small caps labels ("eyebrows") get extra letter spacing.</summary>
	public static readonly Font Spaced = new FontVariation { BaseFont = Bold, SpacingGlyph = 2 };
	public static readonly Font Mono =new SystemFont { FontNames = new[] { "JetBrains Mono", "Consolas", "Courier New" }, FontWeight = 700 };
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

	static void ButtonType(Theme t, string type, Color bg, Color ink, Color? border, int radius = 10, float padX = 20, float padY = 10)
	{
		t.SetStylebox("normal", type, Box(bg, radius, border, 1, padX, padY));
		t.SetStylebox("hover", type, Box(bg.A == 0 ? bg : bg.Lightened(0.08f), radius, border, 1, padX, padY));
		t.SetStylebox("pressed", type, Box(bg.A == 0 ? bg : bg.Darkened(0.08f), radius, border, 1, padX, padY));
		t.SetStylebox("hover_pressed", type, Box(bg.A == 0 ? bg : bg.Darkened(0.04f), radius, border, 1, padX, padY));
		var disabled = bg.A == 0 ? bg : new Color(bg, 0.4f);
		t.SetStylebox("disabled", type, Box(disabled, radius, border is { } b ? new Color(b, 0.4f) : null, 1, padX, padY));
		t.SetStylebox("focus", type, Outline(C.Blue, radius));
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

		ButtonType(t, "Button", C.Panel2, C.Text, C.Border);
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
		ButtonType(t, "LinkButton2", Colors.Transparent, C.Muted, null, 8, 4, 6);
		t.SetTypeVariation("SmallButton", "Button");
		ButtonType(t, "SmallButton", C.Panel2, C.Text, C.Border, 8, 12, 5);
		t.SetTypeVariation("ToggleOn", "Button");
		ButtonType(t, "ToggleOn", new Color(C.Blue, 0.18f), C.Blue, C.Blue, 10, 14, 8);
		t.SetTypeVariation("ToggleOff", "Button");
		ButtonType(t, "ToggleOff", C.Bg, C.Muted, C.Border, 10, 14, 8);

		// OptionButton has its own theme type.
		ButtonType(t, "OptionButton", C.Bg, C.Text, C.Border, 10, 12, 8);

		// Cards and panels
		t.SetStylebox("panel", "PanelContainer", Box(C.Panel, 14, C.Border, 1, 24, 22));
		foreach (var (name, box) in new (string, StyleBox)[]
		{
			("Card2", Box(C.Panel2, 12, C.Border, 1, 14, 12)),
			("Inset", Box(C.Bg, 12, C.Border, 1, 14, 12)),
			("Chip", Box(C.Panel2, 999, C.Border, 1, 10, 5)),
			("ChipMe", Box(C.Panel2, 999, C.Blue, 1, 10, 5)),
			("Pill", Box(C.Panel2, 999, C.Border, 1, 12, 5)),
			("Notice", Box(C.Panel2, 10, C.Border, 1, 14, 10)),
			("Bar", Box(C.Panel, 0, null, 0, 20, 12)),
			("Highlight", Box(new Color(C.Accent, 0.1f), 12, C.Accent, 1, 14, 10)),
			("Scored", Box(new Color(C.Teal, 0.1f), 12, C.Teal, 1, 14, 10)),
			("Clear", new StyleBoxEmpty()),
		})
		{
			t.SetTypeVariation(name, "PanelContainer");
			t.SetStylebox("panel", name, box);
		}

		// Text inputs
		foreach (var type in new[] { "LineEdit", "TextEdit" })
		{
			t.SetStylebox("normal", type, Box(C.Bg, 10, C.Border, 1, 12, 9));
			t.SetStylebox("focus", type, Outline(C.Blue, 10, 1));
			t.SetStylebox("read_only", type, Box(C.Panel2, 10, C.Border, 1, 12, 9));
			t.SetColor("font_color", type, C.Text);
			t.SetColor("font_placeholder_color", type, new Color(C.Muted, 0.7f));
			t.SetColor("caret_color", type, C.Blue);
			t.SetColor("selection_color", type, new Color(C.Blue, 0.3f));
		}
		t.SetTypeVariation("CodeInput", "LineEdit");
		t.SetFont("font", "CodeInput", Fonts.Mono);
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
		var popup = Box(C.Panel2, 10, C.Border, 1, 6, 6);
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
			var frame = Box(C.Panel2, 14, C.Border, 1, 0, 0);
			frame.ExpandMarginTop = titleHeight;
			frame.ExpandMarginLeft = frame.ExpandMarginRight = frame.ExpandMarginBottom = 6;
			t.SetStylebox(state, "Window", frame);
		}
		t.SetConstant("title_height", "Window", titleHeight);
		t.SetColor("title_color", "Window", C.Text);
		t.SetFont("title_font", "Window", Fonts.Bold);
		t.SetFontSize("title_font_size", "Window", 16);
		t.SetStylebox("panel", "TooltipPanel", Box(C.Panel2, 8, C.Border, 1, 10, 6));
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
		t.SetStylebox("slider", "HSlider", Box(C.Border, 4, null, 0, 0, 3));
		t.SetStylebox("grabber_area", "HSlider", Box(C.Blue, 4, null, 0, 0, 3));
		t.SetStylebox("grabber_area_highlight", "HSlider", Box(C.Blue.Lightened(0.15f), 4, null, 0, 0, 3));
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
	public static ScrollContainer Page(Control content, float maxWidth = 960, int padTop = 36)
	{
		var scroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
		scroll.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		var col = new Column { MaxWidth = maxWidth, PadTop = padTop, PadBottom = Mathf.Min(36, padTop) };
		col.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		col.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
		col.AddChild(content);
		scroll.AddChild(col);
		return scroll;
	}

	/// <summary>
	/// A player's name for seats and the podium: a clan tag ("[OTB] maxlvl") on its own small line above the name, so
	/// long tags don't crowd the name.
	/// </summary>
	public static Control PlayerName(string name, int size, HorizontalAlignment align = HorizontalAlignment.Center)
	{
		var (tag, rest) = Limits.SplitClanTag(name);
		var label = Bold(rest, size, null, false, align).Raw();
		label.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
		label.ClipText = true;
		label.TooltipText = name;
		label.MouseFilter = Control.MouseFilterEnum.Pass;
		if (tag == null) return label;
		var box = V(0, Text($"[{tag}]", Math.Max(10, size - 4), C.Accent, Fonts.Mono, false, align).Raw(), label);
		box.MouseFilter = Control.MouseFilterEnum.Ignore;
		return box;
	}

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
