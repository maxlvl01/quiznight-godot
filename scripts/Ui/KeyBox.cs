using System;
using Godot;

namespace QuizNight.Ui;

/// <summary>
/// A big button drawn like a real key: the ink outline, under the face a side in a darker shade of the button's own
/// colour (what makes it look 3D), and on the face a light bevel along the top and a soft shine over its upper half.
/// Pressed, the face sinks into the side. Content margins as in <see cref="UiTheme.Chunky"/>.
/// </summary>
public partial class KeyBox : StyleBox
{
	Color _bg;
	int _radius, _outline, _drop;
	public Color Ink = C.Ink;

	public KeyBox() : this(C.Panel2, 16, 20, 9, 3, 5, false) { }

	public KeyBox(Color bg, int radius, float padX, float padY, int outline, int depth, bool pressed)
	{
		_bg = bg;
		_radius = radius;
		_outline = outline;
		_drop = pressed ? Math.Max(outline, depth / 3) : depth;
		var half = depth / 2f;
		ContentMarginLeft = ContentMarginRight = padX;
		ContentMarginTop = Math.Max(0, padY - half) + (depth - _drop);
		ContentMarginBottom = Math.Max(0, padY - half) + _drop;
	}

	public static StyleBoxFlat Fill(Color c, int r) => new()
	{
		BgColor = c, AntiAliasing = true,
		CornerRadiusTopLeft = r, CornerRadiusTopRight = r, CornerRadiusBottomLeft = r, CornerRadiusBottomRight = r,
	};

	public override void _Draw(Rid canvasItem, Rect2 rect)
	{
		var o = _outline;
		Fill(Ink, _radius).Draw(canvasItem, rect);
		var inner = rect.Grow(-o);
		var r = Math.Max(2, _radius - o);
		// the side: the button's colour in shadow, a little lighter at its very bottom (light bouncing off the table)
		var side = _bg.Darkened(0.42f);
		Fill(side, r).Draw(canvasItem, inner);
		// the face, with a 2 px lighter rim at the top (the bevel)
		var face = new Rect2(inner.Position, new Vector2(inner.Size.X, Math.Max(0, inner.Size.Y - _drop)));
		var faceBox = Fill(_bg, r);
		faceBox.BorderWidthTop = 2;
		faceBox.BorderColor = new Color(_bg.Lightened(0.45f), _bg.A);
		faceBox.BorderBlend = true;
		faceBox.Draw(canvasItem, face);
		// the shine: a softer, lighter band over the upper part of the face
		if (face.Size.Y > 14 && face.Size.X > 14)
		{
			var shine = new Rect2(face.Position + new Vector2(5, 3), new Vector2(face.Size.X - 10, face.Size.Y * 0.42f));
			Fill(new Color(1, 1, 1, 0.16f * _bg.A), Math.Max(2, r - 3)).Draw(canvasItem, shine);
		}
		// a thin dark line where the face meets the side
		if (_drop > 1)
			Fill(new Color(0, 0, 0, 0.18f * _bg.A), 0).Draw(canvasItem, new Rect2(face.Position.X + r, face.End.Y - 1, Math.Max(0, face.Size.X - 2 * r), 1));
	}
}
