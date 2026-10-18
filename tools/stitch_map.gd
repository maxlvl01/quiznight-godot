extends SceneTree

# Builds one built-in map image (assets/maps/<id>.webp) from downloaded map tiles or one big picture.
# Run headless, arguments after "--":
#   tiles DIR PATTERN COLS ROWS TILE CROP_W CROP_H OUT_W OUT.webp [CROP_X CROP_Y]
#       PATTERN names a tile with {x} and {y} (e.g. "{x}_{y}.png"); the tiles are laid out from the top left,
#       then cut to CROP_W x CROP_H at CROP_X, CROP_Y (default 0, 0: the part of the tile grid that is the map) and scaled to OUT_W wide.
#   image IN OUT_W OUT.webp [X Y W H]
#       scales one picture (or the part X, Y, W, H of it) to OUT_W wide.
# Missing tiles stay transparent and are filled with the colour of the first tile's top-left pixel.

func _init():
	var a := OS.get_cmdline_user_args()
	var img: Image
	var out_w: int
	var out: String
	if a[0] == "tiles":
		var dir: String = a[1]
		var cols := int(a[3]); var rows := int(a[4]); var tile := int(a[5])
		var off := Vector2i(int(a[10]), int(a[11])) if a.size() >= 12 else Vector2i.ZERO
		img = Image.create_empty(int(a[6]), int(a[7]), false, Image.FORMAT_RGB8)
		var filled := false
		for x in cols:
			for y in rows:
				var path := dir.path_join(a[2].replace("{x}", str(x)).replace("{y}", str(y)))
				if not FileAccess.file_exists(path):
					continue
				var t := Image.load_from_file(path)
				if t == null:
					continue
				t.convert(Image.FORMAT_RGB8)
				if not filled:
					img.fill(t.get_pixel(0, 0))
					filled = true
				img.blit_rect(t, Rect2i(0, 0, t.get_width(), t.get_height()), Vector2i(x * tile, y * tile) - off)
		out_w = int(a[8]); out = a[9]
	else:
		img = Image.load_from_file(a[1])
		if img == null:
			printerr("can't read ", a[1])
			quit(1)
			return
		img.convert(Image.FORMAT_RGB8)
		if a.size() >= 8:
			img = img.get_region(Rect2i(int(a[4]), int(a[5]), int(a[6]), int(a[7])))
		out_w = int(a[2]); out = a[3]
	img.resize(out_w, int(round(float(out_w) * img.get_height() / img.get_width())), Image.INTERPOLATE_LANCZOS)
	img.save_webp(out, true, 0.85)
	print("saved ", out, " ", img.get_width(), "x", img.get_height())
	quit()
