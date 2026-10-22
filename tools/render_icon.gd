# Renders icon.svg into PNGs for the Windows .ico (tools/make_icon.py packs them):
#   godot --headless -s tools/render_icon.gd -- OUT_DIR
extends SceneTree

func _init() -> void:
	var out: String = OS.get_cmdline_user_args()[0]
	var svg := FileAccess.get_file_as_string("res://icon.svg")
	for size in [16, 24, 32, 48, 64, 128, 256]:
		var image := Image.new()
		image.load_svg_from_string(svg, size / 256.0)
		image.save_png(out.path_join("icon-%d.png" % size))
	quit()
