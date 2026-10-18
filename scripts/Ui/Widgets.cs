// Reusable pieces: avatars, type icons, countdowns, images, number fields, steppers, dialogs.

using System;
using System.Globalization;
using Godot;
using QuizNight.Net;
using QuizNight.Shared;

namespace QuizNight.Ui;

/// <summary>A coloured circle with the player's initials.</summary>
public partial class Avatar : Control
{
	readonly Color _color;
	readonly string _initials;
	readonly bool _offline;

	public Avatar() : this(new PublicPlayer()) { }

	public Avatar(PublicPlayer player, float size = 30)
	{
		_color = new Color(player.Color);
		_initials = U.Initials(player.Name);
		_offline = !player.Connected;
		CustomMinimumSize = new Vector2(size, size);
		SizeFlagsVertical = SizeFlags.ShrinkCenter;
		TooltipText = player.Name;
		MouseFilter = MouseFilterEnum.Pass;
	}

	public override void _Draw()
	{
		var r = Mathf.Min(Size.X, Size.Y) / 2;
		var center = Size / 2;
		var alpha = _offline ? 0.35f : 1f;
		DrawCircle(center, r, new Color(_color, alpha), true, -1, true);
		var fontSize = (int)(r * 0.8f);
		var font = Fonts.Bold;
		var ascent = font.GetAscent(fontSize);
		var height = font.GetHeight(fontSize);
		DrawString(font, new Vector2(0, center.Y - height / 2 + ascent), _initials, HorizontalAlignment.Center, Size.X, fontSize,
			new Color("#14151b", alpha));
	}
}

/// <summary>The little line drawing for each question type (same shapes as the web version's icons).</summary>
public partial class TypeIcon : Control
{
	readonly string _type;

	public TypeIcon() : this(QuestionType.Classic) { }

	public TypeIcon(string type, float size = 22)
	{
		_type = type;
		CustomMinimumSize = new Vector2(size, size);
		SizeFlagsVertical = SizeFlags.ShrinkCenter;
		MouseFilter = MouseFilterEnum.Ignore;
	}

	public override void _Draw()
	{
		var k = Mathf.Min(Size.X, Size.Y) / 24f;
		var color = C.ForType(_type);
		var w = 1.8f * k;
		Vector2 P(float x, float y) => new(x * k, y * k);
		switch (_type)
		{
			case QuestionType.MapPin:
				DrawPolyline(new[] { P(12, 21), P(7, 15.5f), P(5, 10.5f), P(6, 6.5f), P(9, 3.4f), P(12, 2.5f), P(15, 3.4f), P(18, 6.5f), P(19, 10.5f), P(17, 15.5f), P(12, 21) }, color, w, true);
				DrawArc(P(12, 9.5f), 2.5f * k, 0, Mathf.Tau, 24, color, w, true);
				break;
			case QuestionType.Estimation:
				foreach (var y in new[] { 9f, 15f })
				{
					var pts = new Vector2[25];
					for (var i = 0; i <= 24; i++)
					{
						var x = 3 + i * 0.75f;
						pts[i] = P(x, y - Mathf.Sin((x - 3) / 6 * Mathf.Pi) * 1.6f);
					}
					DrawPolyline(pts, color, w, true);
				}
				break;
			case QuestionType.Classic:
				DrawArc(P(12, 12), 8.5f * k, 0, Mathf.Tau, 40, color, w, true);
				DrawArc(P(12, 12), 4 * k, 0, Mathf.Tau, 24, color, w, true);
				break;
			case QuestionType.Sound:
				DrawPolyline(new[] { P(9, 18), P(9, 5), P(20, 3), P(20, 16) }, color, w, true);
				DrawArc(P(6, 18), 3 * k, 0, Mathf.Tau, 20, color, w, true);
				DrawArc(P(17, 16), 3 * k, 0, Mathf.Tau, 20, color, w, true);
				break;
			case QuestionType.Video:
				DrawRect(new Rect2(P(3, 5), P(18, 14)), color, false, w, true);
				DrawPolyline(new[] { P(10, 9.5f), P(10, 14.5f), P(14.5f, 12), P(10, 9.5f) }, color, w, true);
				break;
			case QuestionType.Image:
				DrawRect(new Rect2(P(3, 4), P(18, 16)), color, false, w, true);
				DrawPolyline(new[] { P(4, 18), P(10, 11), P(14, 15), P(16, 13), P(20, 17) }, color, w, true);
				DrawArc(P(16, 8), 1.8f * k, 0, Mathf.Tau, 16, color, w, true);
				break;
		}
	}
}

public static class Chips
{
	public static string TypeLabel(PublicQuestion q) =>
		q.Type == QuestionType.Classic ? (q.Mode == ClassicMode.Choice ? "A B C D" : q.Mode == ClassicMode.Multi ? "Multiple choice" : "Buzzer") : QuestionType.Infos[q.Type].Label;

	/// <summary>"(icon) Buzzer" pill for the current question.</summary>
	public static Control Question(PublicQuestion q) =>
		U.Panel(U.H(6, new TypeIcon(q.Type, 18), U.Text(TypeLabel(q), 14, C.ForType(q.Type), Fonts.Bold)), "Pill")
			.With(p => p.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter);

	/// <summary>A player name chip with avatar.</summary>
	public static PanelContainer Player(PublicPlayer p, string? suffix = null, bool me = false, bool dim = false)
	{
		var row = U.H(6, new Avatar(p, 24), U.Text(p.Name, 15).Raw());
		if (suffix != null) row.AddChild(U.Muted(suffix, 13, false));
		var chip = U.Panel(row, me ? "ChipMe" : "Chip");
		if (dim || !p.Connected) chip.Modulate = new Color(1, 1, 1, 0.55f);
		return chip;
	}
}

/// <summary>"0:12" countdown to a host-time deadline; turns red in the last 5 seconds.</summary>
public partial class Countdown : HBoxContainer
{
	readonly long? _endsAt;
	readonly Label _time;

	public Countdown() : this(null) { }

	public Countdown(long? endsAt, string? label = null, int size = 26)
	{
		_endsAt = endsAt;
		AddThemeConstantOverride("separation", 8);
		SizeFlagsVertical = SizeFlags.ShrinkCenter;
		if (label != null) AddChild(U.Text(label, 13, C.Muted, Fonts.Bold).With(l => l.SizeFlagsVertical = SizeFlags.ShrinkCenter));
		_time = U.Mono("", size);
		AddChild(_time);
		Visible = endsAt != null;
		Update();
	}

	void Update()
	{
		if (ServerClock.SecondsLeft(_endsAt) is not { } left) return;
		var s = (int)Math.Ceiling(left);
		_time.Text = $"{s / 60}:{s % 60:00}";
		_time.AddThemeColorOverride("font_color", s <= 5 ? C.Coral : C.Text);
	}

	public override void _Process(double delta) => Update();
}

/// <summary>Shows a texture as large as fits (keeping its shape), up to a maximum height.</summary>
public partial class FitImage : Control
{
	Texture2D? _texture;
	public float MaxHeight = 420;
	/// <summary>Aspect (height / width) to reserve space with while the texture is still loading.</summary>
	public float PlaceholderAspect = 0.5625f;

	public FitImage() => SizeFlagsHorizontal = SizeFlags.ExpandFill;

	public Texture2D? Texture
	{
		get => _texture;
		set
		{
			_texture = value;
			UpdateHeight();
			QueueRedraw();
		}
	}

	float Aspect => _texture != null ? (float)_texture.GetHeight() / _texture.GetWidth() : PlaceholderAspect;

	void UpdateHeight()
	{
		var h = Mathf.Min(MaxHeight, Size.X * Aspect);
		if (Mathf.Abs(CustomMinimumSize.Y - h) > 0.5f) CustomMinimumSize = new Vector2(0, h);
	}

	public override void _Notification(int what)
	{
		if (what == NotificationResized) UpdateHeight();
	}

	public override void _Draw()
	{
		var w = Mathf.Min(Size.X, Size.Y / Aspect);
		var h = w * Aspect;
		var rect = new Rect2((Size.X - w) / 2, (Size.Y - h) / 2, w, h);
		if (_texture == null)
		{
			DrawRect(rect, C.Panel2);
			var font = Fonts.Regular;
			DrawString(font, new Vector2(0, Size.Y / 2), Lang.T("Loading image…"), HorizontalAlignment.Center, Size.X, 14, C.Muted);
			return;
		}
		DrawTextureRect(_texture, rect, false);
	}
}

/// <summary>An image that comes from an AssetSource (players load it from the host).</summary>
public partial class AssetImage : FitImage
{
	readonly AssetSource? _source;
	readonly string _key;

	public AssetImage() : this(null, "") { }

	public AssetImage(AssetSource? source, string key, float maxHeight = 420)
	{
		_source = source;
		_key = key;
		MaxHeight = maxHeight;
		if (source == null) return;
		Texture = source.Get(key);
		if (Texture == null) source.Loaded += OnLoaded;
	}

	void OnLoaded(string key)
	{
		if (key != _key || !IsInstanceValid(this)) return;
		Texture = _source!.Get(key);
	}

	public override void _ExitTree()
	{
		if (_source != null) _source.Loaded -= OnLoaded;
	}
}

/// <summary>A box whose content is rebuilt only when its key changes (keeps inputs and zoom alive between updates).</summary>
public partial class Keyed : VBoxContainer
{
	string? _key;

	public void Set(string key, Func<Control?> build)
	{
		if (key == _key) return;
		_key = key;
		this.Clear();
		if (build() is { } c) AddChild(c);
	}

	/// <summary>Forces the next Set to rebuild.</summary>
	public void Reset() => _key = null;
}

/// <summary>Number input that lets you type freely ("-", "1.") and reports a number or null.</summary>
public partial class NumberField : LineEdit
{
	readonly double? _min;
	readonly double? _max;
	readonly bool _integer;
	readonly Action<double?> _changed;
	public double? Value { get; private set; }

	public NumberField() : this(null, _ => { }) { }

	public NumberField(double? value, Action<double?> changed, double? min = null, double? max = null, bool integer = false, string placeholder = "")
	{
		Value = value;
		_changed = changed;
		_min = min;
		_max = max;
		_integer = integer;
		Text = Format(value);
		PlaceholderText = placeholder;
		SizeFlagsHorizontal = SizeFlags.ExpandFill;
		VirtualKeyboardType = integer ? VirtualKeyboardTypeEnum.Number : VirtualKeyboardTypeEnum.NumberDecimal;
		TextChanged += OnText;
		FocusExited += Clamp;
		TextSubmitted += _ => Clamp();
	}

	static string Format(double? v) => v is { } n ? n.ToString("0.#########", CultureInfo.InvariantCulture) : "";

	public static double? Parse(string raw)
	{
		var t = raw.Trim().Replace(',', '.').Replace(" ", "");
		return t != "" && double.TryParse(t, NumberStyles.Float, CultureInfo.InvariantCulture, out var n) && double.IsFinite(n) ? n : null;
	}

	void OnText(string raw)
	{
		Value = Parse(raw);
		_changed(Value);
	}

	void Clamp()
	{
		if (Value is not { } v) return;
		var n = _integer ? Math.Round(v) : v;
		if (_min is { } min) n = Math.Max(min, n);
		if (_max is { } max) n = Math.Min(max, n);
		if (n != v)
		{
			Value = n;
			_changed(n);
		}
		var caret = CaretColumn;
		Text = Format(n);
		CaretColumn = Math.Min(caret, Text.Length);
	}
}

/// <summary>"Label  [−] 8 [+]".</summary>
public static class Stepper
{
	public static HBoxContainer Make(string label, int value, int min, int max, Action<int> changed, string suffix = "")
	{
		var minus = U.Small("−", () => changed(value - 1));
		minus.Disabled = value <= min;
		var plus = U.Small("+", () => changed(value + 1));
		plus.Disabled = value >= max;
		var shown = U.Mono($"{value}{suffix}", 18).With(l => l.HorizontalAlignment = HorizontalAlignment.Center).MinSize(64);
		return U.H(8, U.Text(label, 15).Grow(), minus, shown, plus);
	}
}

public static class Dialogs
{
	/// <summary>Asks a yes/no question; runs onYes only on yes.</summary>
	public static void Confirm(Node owner, string text, string yes, Action onYes, string title = "Are you sure?")
	{
		var d = new ConfirmationDialog { Title = Lang.T(title), DialogText = Lang.T(text), OkButtonText = Lang.T(yes), CancelButtonText = Lang.T("Cancel"), DialogAutowrap = true };
		d.MinSize = new Vector2I(460, 0);
		d.Confirmed += () => onYes();
		d.Confirmed += d.QueueFree;
		d.Canceled += d.QueueFree;
		owner.AddChild(d);
		d.PopupCentered();
	}

	public static void Info(Node owner, string text, string title = "Quiznight!")
	{
		var d = new AcceptDialog { Title = Lang.T(title), DialogText = Lang.T(text), DialogAutowrap = true };
		d.MinSize = new Vector2I(460, 0);
		d.Confirmed += d.QueueFree;
		d.Canceled += d.QueueFree;
		owner.AddChild(d);
		d.PopupCentered();
	}

	/// <summary>The system's file dialog for opening a file.</summary>
	public static void OpenFile(Node owner, string title, string[] filters, Action<string> picked)
	{
		var d = new FileDialog
		{
			Title = Lang.T(title),
			FileMode = FileDialog.FileModeEnum.OpenFile,
			Access = FileDialog.AccessEnum.Filesystem,
			Filters = filters,
			UseNativeDialog = true,
		};
		d.FileSelected += path => picked(path);
		d.FileSelected += _ => d.QueueFree();
		d.Canceled += d.QueueFree;
		owner.AddChild(d);
		d.PopupCentered(new Vector2I(900, 600));
	}

	public static void SaveFile(Node owner, string title, string fileName, string[] filters, Action<string> picked)
	{
		var d = new FileDialog
		{
			Title = Lang.T(title),
			FileMode = FileDialog.FileModeEnum.SaveFile,
			Access = FileDialog.AccessEnum.Filesystem,
			Filters = filters,
			CurrentFile = fileName,
			UseNativeDialog = true,
		};
		d.FileSelected += path => picked(path);
		d.FileSelected += _ => d.QueueFree();
		d.Canceled += d.QueueFree;
		owner.AddChild(d);
		d.PopupCentered(new Vector2I(900, 600));
	}
}
