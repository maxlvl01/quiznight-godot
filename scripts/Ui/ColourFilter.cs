// Colour vision help (Settings → Accessibility): a colour correction over the whole game for red-green (deuteranopia,
// protanopia) or blue-yellow (tritanopia) colour blindness. It's daltonization: work out which colour differences the
// eye can't see, and move them into ones it can, so right and wrong, the A–D tiles, medals and player colours stay apart.

using Godot;

namespace QuizNight.Ui;

public partial class ColourFilter : CanvasLayer
{
	public static readonly (string Id, string Label)[] Modes =
	{
		("off", "Off"),
		("deuteranopia", "Red-green (deuteranopia)"),
		("protanopia", "Red-green (protanopia)"),
		("tritanopia", "Blue-yellow (tritanopia)"),
	};

	static readonly Shader Daltonize = new()
	{
		Code = """
			shader_type canvas_item;
			uniform sampler2D screen_texture : hint_screen_texture, filter_linear;
			// 1 protanopia, 2 deuteranopia, 3 tritanopia
			uniform int mode = 0;
			void fragment() {
				vec3 c = texture(screen_texture, SCREEN_UV).rgb;
				float l = 17.8824 * c.r + 43.5161 * c.g + 4.11935 * c.b;
				float m = 3.45565 * c.r + 27.1554 * c.g + 3.86714 * c.b;
				float s = 0.0299566 * c.r + 0.184309 * c.g + 1.46709 * c.b;
				if (mode == 1) { l = 2.02344 * m - 2.52581 * s; }
				else if (mode == 2) { m = 0.494207 * l + 1.24827 * s; }
				else if (mode == 3) { s = -0.395913 * l + 0.801109 * m; }
				vec3 seen = vec3(
					0.0809444479 * l - 0.130504409 * m + 0.116721066 * s,
					-0.0102485335 * l + 0.0540193266 * m - 0.113614708 * s,
					-0.000365296938 * l - 0.00412161469 * m + 0.693511405 * s);
				vec3 lost = c - seen;
				vec3 shift = vec3(0.0, 0.7 * lost.r + lost.g, 0.7 * lost.r + lost.b);
				COLOR = vec4(clamp(c + shift, 0.0, 1.0), 1.0);
			}
			""",
	};

	readonly ColorRect _rect = new() { MouseFilter = Control.MouseFilterEnum.Ignore };
	readonly ShaderMaterial _material = new() { Shader = Daltonize };

	public ColourFilter()
	{
		// (above everything, pop-ups included)
		Layer = 120;
		_rect.Material = _material;
		_rect.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		AddChild(_rect);
	}

	/// <summary>Switches the correction ("off", "deuteranopia", "protanopia", "tritanopia").</summary>
	public void Use(string mode)
	{
		var n = mode switch { "protanopia" => 1, "deuteranopia" => 2, "tritanopia" => 3, _ => 0 };
		_material.SetShaderParameter("mode", n);
		Visible = n != 0;
	}
}
