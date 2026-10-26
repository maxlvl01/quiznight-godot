// The home screen's logo as a quiz answer field: "Quiznight?" in a text box with a blinking cursor. Every so often it
// deletes itself letter by letter and types an easter egg instead (Klaus? Mond? Autobahn? …), waits, and types
// "Quiznight?" back. Two spotlights sweep over it from above (Spotlights, drawn behind the page).

using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace QuizNight.Ui;

public partial class LogoField : PanelContainer
{
	const string Logo = "Quiznight?";

	/// <summary>What the field types now and then (in a new random order each round).</summary>
	static readonly string[] Eggs =
	{
		"Klaus?", "Mond?", "Autobahn?", "Mars?", "Grownz?", "Tazor?", "Max?", "Phil?", "Lars?", "Rematch?", "Big Brain?", "Patty?", "Olf?", "Fußspuren?",
	};

	readonly Label _text;
	readonly Label _question;
	readonly ColorRect _caret;
	readonly int _size;
	string _shown = Logo;
	readonly Queue<string> _todo = new();
	double _wait;
	double _blink;
	readonly List<string> _deck = new();

	public LogoField() : this(64) { }

	public LogoField(int size)
	{
		_size = size;
		MouseFilter = MouseFilterEnum.Ignore;
		// (a dark field, or a light one in a light skin)
		var box = UiTheme.Box(C.Light ? C.Panel : C.Bg.Darkened(0.35f), 22, C.Accent, 4, size * 0.4f, size * 0.12f);
		box.BorderWidthBottom = 10;
		box.BorderColor = C.Accent;
		AddThemeStyleboxOverride("panel", box);
		_text = U.Text("", size, C.Text, Fonts.Black).Raw();
		_question = U.Text("?", size, C.Accent, Fonts.Black).Raw();
		_caret = new ColorRect { Color = C.Accent, CustomMinimumSize = new Vector2(Mathf.Max(3, size / 16f), size * 0.95f), SizeFlagsVertical = SizeFlags.ShrinkCenter, MouseFilter = MouseFilterEnum.Ignore };
		var row = U.H(2, _text, _question, U.Gap(4), _caret);
		row.Alignment = BoxContainer.AlignmentMode.Begin;
		AddChild(row);
		// (seasonal skins dress the logo up: snow and a Santa hat, pumpkins and a cobweb)
		if (C.Skin is Skins.Christmas or Skins.Halloween) AddChild(new LogoDecor(C.Skin == Skins.Christmas));
		// (as wide as "Quiznight?", so the field doesn't jump while it types something shorter)
		var width = Fonts.Black.GetStringSize(Logo, HorizontalAlignment.Left, -1, size).X + size * 0.9f;
		CustomMinimumSize = new Vector2(width, 0);
		Show(Logo);
		_wait = 4 + Random.Shared.NextDouble() * 3;
		AccessibilityName = "Quiznight?";
		_text.AccessibilityName = _question.AccessibilityName = " ";
	}

	void Show(string text)
	{
		_shown = text;
		var q = text.EndsWith('?');
		_text.Text = q ? text[..^1] : text;
		_question.Visible = q;
	}

	/// <summary>The next easter egg (each once before any comes again).</summary>
	string NextEgg()
	{
		if (_deck.Count == 0) _deck.AddRange(Eggs.OrderBy(_ => Random.Shared.Next()));
		var egg = _deck[0];
		_deck.RemoveAt(0);
		return egg;
	}

	/// <summary>Plans "delete it all, type this": one step per letter.</summary>
	void Plan(string target)
	{
		for (var i = _shown.Length - 1; i >= 0; i--) _todo.Enqueue(_shown[..i]);
		for (var i = 1; i <= target.Length; i++) _todo.Enqueue(target[..i]);
	}

	public override void _Process(double delta)
	{
		if (Announcer.ReduceMotion) return;
		// (the cursor blinks while it waits, stays on while it types)
		_blink += delta;
		_caret.Modulate = new Color(1, 1, 1, _todo.Count > 0 || (_blink % 1.0) < 0.55 ? 1 : 0);
		_wait -= delta;
		if (_wait > 0) return;
		if (_todo.Count > 0)
		{
			var next = _todo.Dequeue();
			var deleting = next.Length < _shown.Length;
			Show(next);
			// (deleting is quick, typing a little uneven, like a person)
			_wait = deleting ? 0.045 : 0.08 + Random.Shared.NextDouble() * 0.09;
			if (_todo.Count == 0) _wait = _shown == Logo ? 7 + Random.Shared.NextDouble() * 6 : 1.8;
			return;
		}
		Plan(_shown == Logo ? NextEgg() : Logo);
		_wait = 0;
	}
}

/// <summary>
/// Two spotlights from above that sweep slowly across the top of the home screen and cross over the logo: soft cones,
/// bright at the lamp, fading out below. Drawn behind the page; the mouse goes through.
/// </summary>
public partial class Spotlights : Control
{
	double _t;
	/// <summary>Where the light lands, from the top (the logo's height on the screen).</summary>
	public float Target = 150;

	public Spotlights()
	{
		MouseFilter = MouseFilterEnum.Ignore;
		SetAnchorsPreset(LayoutPreset.FullRect);
	}

	public override void _Process(double delta)
	{
		// (with less motion asked for, the lights stand still)
		if (!Announcer.ReduceMotion) _t += delta;
		QueueRedraw();
	}

	void Cone(Vector2 lamp, Vector2 aim, float width, Color color)
	{
		var dir = (aim - lamp).Normalized();
		var side = new Vector2(-dir.Y, dir.X);
		var far = lamp + dir * (aim - lamp).Length() * 1.35f;
		var bright = color;
		var faint = new Color(color, 0);
		DrawPolygon(new[] { lamp + side * 6, lamp - side * 6, far - side * width, far + side * width },
			new[] { bright, bright, faint, faint });
		// (the pool of light where it lands)
		for (var i = 0; i < 6; i++)
			DrawCircle(aim, width * 0.55f * (1 - i / 7f), new Color(color, color.A * 0.12f));
	}

	public override void _Draw()
	{
		var w = Size.X;
		// (two lamps at the top corners; they swing at different speeds, so their pools meet and part)
		var a = (float)Math.Sin(_t * 0.45) * 0.22f;
		var b = (float)Math.Sin(_t * 0.37 + 1.7) * 0.22f;
		var warm = new Color(C.Gold, 0.16f);
		var cool = new Color(C.Pink, 0.13f);
		Cone(new Vector2(w * 0.12f, -20), new Vector2(w * (0.5f + a), Target), 150, warm);
		Cone(new Vector2(w * 0.88f, -20), new Vector2(w * (0.5f + b), Target), 150, cool);
	}
}
