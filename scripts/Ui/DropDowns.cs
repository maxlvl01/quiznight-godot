// Every dropdown (OptionButton) in the game: a chunky arrow in the party show style instead of Godot's thin one, pointing
// up while the list is open, and the list unfolds smoothly (from a quarter of its height, easing out) instead of popping
// in. App installs it once; it catches every OptionButton as it's made.

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
	const double UnfoldSec = 0.16;

	static void Hook(OptionButton o)
	{
		o.SetMeta(Hooked, true);
		o.AddThemeIconOverride("arrow", Arrow(false));
		var popup = o.GetPopup();
		popup.AboutToPopup += () =>
		{
			o.AddThemeIconOverride("arrow", Arrow(true));
			if (!Announcer.ReduceMotion) Callable.From(() => Unfold(popup, o)).CallDeferred();
		};
		popup.PopupHide += () =>
		{
			if (GodotObject.IsInstanceValid(o)) o.AddThemeIconOverride("arrow", Arrow(false));
		};
	}

	/// <summary>The list grows from a quarter of its height to all of it; opened upwards (no room below), it grows upwards.</summary>
	static void Unfold(PopupMenu popup, OptionButton o)
	{
		if (!GodotObject.IsInstanceValid(popup) || !popup.Visible) return;
		var full = popup.Size;
		var at = popup.Position;
		var upwards = at.Y + full.Y <= o.GetScreenPosition().Y + 2;
		void Height(float h)
		{
			if (!GodotObject.IsInstanceValid(popup) || !popup.Visible) return;
			var height = Mathf.RoundToInt(h);
			popup.Size = new Vector2I(full.X, height);
			if (upwards) popup.Position = new Vector2I(at.X, at.Y + full.Y - height);
		}
		Height(full.Y * 0.25f);
		var tween = o.CreateTween();
		tween.TweenMethod(Callable.From<float>(Height), full.Y * 0.25f, (float)full.Y, UnfoldSec).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
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
