# Converts OpenMap's baked zone tiles (BLP2, DXT5) to WebP for assets/maps/wow/stitched/.
# OpenMap: https://www.curseforge.com/wow/addons/openmap (code MIT; the art is Blizzard's map art).
# Run: Godot --headless --path . --script res://tools/openmap_tiles.gd -- "<OpenMap>/Media/Textures/MapZones" "<project>/assets/maps/wow/stitched"
extends SceneTree
func _init():
	var args = OS.get_cmdline_user_args()
	var src = args[0]; var out = args[1]
	var only = args[2] if args.size() > 2 else ""
	DirAccess.make_dir_recursive_absolute(out)
	var n = 0
	for f in DirAccess.get_files_at(src):
		if not f.ends_with(".blp"): continue
		if only != "" and f != only + ".blp": continue
		var b = FileAccess.get_file_as_bytes(src + "/" + f)
		if b.slice(0, 4).get_string_from_ascii() != "BLP2" or b[8] != 2 or b[10] != 7:
			push_error("unexpected format " + f); continue
		var w = b.decode_u32(12); var h = b.decode_u32(16)
		var off = b.decode_u32(20); var size = b.decode_u32(84)
		var img = Image.create_from_data(w, h, false, Image.FORMAT_DXT5, b.slice(off, off + size))
		img.decompress()
		img.save_webp(out + "/" + f.get_basename() + ".webp", true, 0.85)
		n += 1
	print("converted ", n)
	quit()
