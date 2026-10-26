// Every dropdown (OptionButton) in the game: a chunky arrow in the party show style instead of Godot's thin one, pointing
// up while the list is open, and the list hangs right under its button and unfolds smoothly (easing out) instead of popping
// in anywhere it fits; long lists scroll inside. App installs it once; it catches every OptionButton as it's made.

using System;
using System.Collections.Generic;
using Godot;

namespace QuizNight.Ui;

public static class DropDowns
{
	public static void Install(SceneTree tree) => tree.NodeAdded += node =>
	{
		if (node is OptionButton o && !o.HasMeta(Hooked)) Hook(o);
	};

	const string Hooked = "dropdown_hooked";
	const double UnfoldSec = 0.26;
	/// <summary>A long list shows this many rows and scrolls for the rest.</summary>
	const int MaxRows = 10;
	/// <summary>Room kept between a list and the window's edge.</summary>
	const float Margin = 8;

	static void Hook(OptionButton o)
	{
		o.SetMeta(Hooked, true);
		o.AddThemeIconOverride("arrow", Arrow(false));
		var popup = o.GetPopup();
		popup.AboutToPopup += () =>
		{
			o.AddThemeIconOverride("arrow", Arrow(true));
			Callable.From(() => Unfold(popup, o)).CallDeferred();
		};
		popup.PopupHide += () =>
		{
			if (GodotObject.IsInstanceValid(o)) o.AddThemeIconOverride("arrow", Arrow(false));
		};
	}

	/// <summary>
	/// Where the list goes: right under its button (above it only if there's no room below), never over it; a long list
	/// (categories, units) is cut to what fits, at most <see cref="MaxRows"/> rows, and scrolls inside itself. Then it
	/// unfolds from the button, easing out. (The list is its own small window: it's see-through round its rounded panel,
	/// so no black box shows while it grows.)
	/// </summary>
	static void Unfold(PopupMenu popup, OptionButton o)
	{
		if (!GodotObject.IsInstanceValid(popup) || !popup.Visible) return;
		popup.TransparentBg = true;
		var button = o.GetGlobalRect();
		var screen = o.GetViewportRect();
		var natural = popup.GetContentsMinimumSize();
		var row = natural.Y / Math.Max(1, popup.ItemCount);
		var wanted = Mathf.Min(natural.Y, row * MaxRows + 12);
		float below = screen.End.Y - button.End.Y - Margin, above = button.Position.Y - screen.Position.Y - Margin;
		var upwards = below < Mathf.Min(wanted, row * 3) && above > below;
		var full = new Vector2I(Mathf.RoundToInt(Mathf.Max(natural.X, button.Size.X)), Mathf.RoundToInt(Mathf.Min(wanted, upwards ? above : below)));
		var x = Mathf.RoundToInt(Mathf.Clamp(button.Position.X, screen.Position.X, screen.End.X - full.X));
		var top = upwards ? Mathf.RoundToInt(button.Position.Y) - full.Y : Mathf.RoundToInt(button.End.Y) + 2;
		void Height(float h)
		{
			if (!GodotObject.IsInstanceValid(popup) || !popup.Visible) return;
			var height = Mathf.RoundToInt(h);
			popup.Size = new Vector2I(full.X, height);
			popup.Position = new Vector2I(x, upwards ? top + full.Y - height : top);
		}
		if (Announcer.ReduceMotion)
		{
			Height(full.Y);
			return;
		}
		Height(full.Y * 0.15f);
		var tween = o.CreateTween();
		tween.TweenMethod(Callable.From<float>(Height), full.Y * 0.15f, (float)full.Y, UnfoldSec).SetTrans(Tween.TransitionType.Quart).SetEase(Tween.EaseType.Out);
	}

	static readonly Dictionary<(bool Up, Color Color), ImageTexture> Arrows = new();

	/// <summary>A thick, rounded chevron in the text colour (drawn once per colour and direction, 4× and shrunk for smooth edges).</summary>
	static ImageTexture Arrow(bool up)
	{
		if (Arrows.TryGetValue((up, C.Text), out var tex)) return tex;
		const int size = 18, big = size * 4;
		var img = Image.CreateEmpty(big, big, false, Image.Format.Rgba8);
		// (a V from (0.18, 0.36) down to (0.5, 0.68) and up to (0.82, 0.36), 0.15 thick, round ends)
		Vector2 a = new(0.18f, 0.36f), m = new(0.5f, 0.68f), b = new(0.82f, 0.36f);
		const float half = 0.075f;
		static float Dist(Vector2 p, Vector2 s, Vector2 e)
		{
			var d = e - s;
			var t = Mathf.Clamp((p - s).Dot(d) / d.LengthSquared(), 0, 1);
			return p.DistanceTo(s + d * t);
		}
		for (var y = 0; y < big; y++)
			for (var x = 0; x < big; x++)
			{
				var p = new Vector2((x + 0.5f) / big, (y + 0.5f) / big);
				if (up) p.Y = 1 - p.Y;
				var dist = Math.Min(Dist(p, a, m), Dist(p, m, b));
				var alpha = Mathf.Clamp((half - dist) * big + 0.5f, 0, 1);
				if (alpha > 0) img.SetPixel(x, y, new Color(C.Text, alpha));
			}
		img.Resize(size, size, Image.Interpolation.Lanczos);
		return Arrows[(up, C.Text)] = ImageTexture.CreateFromImage(img);
	}
}
