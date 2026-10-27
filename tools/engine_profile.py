# Quiznight's slim Windows export template: only what the game uses (2D UI, OpenGL compatibility renderer, C#,
# Ogg/MP3/WAV, Theora video, WebP/JPG/PNG/BMP/SVG). Build:
#   (once: py misc/scripts/install_accesskit.py in the Godot source folder, for screen reader support)
#   py -m SCons platform=windows target=template_release arch=x86_64 module_mono_enabled=yes d3d12=no vulkan=no -j 14 #      profile=<path to this file>
#   then copy bin/godot.windows.template_release.x86_64.mono.exe to C:/godot-src/templates/quiznight-windows-release.exe
optimize = "size"
lto = "full"
# (no disable_3d / disable_physics_* / disable_navigation_* / disable_xr: those also take methods out of core classes
# like Viewport, and the stock GodotSharp.dll the game is built against binds all of a class's methods at once, so
# Viewport broke. Leaving out whole modules is fine: their classes are just never touched.)
vulkan = "no"
d3d12 = "no"
for _m in ["astcenc", "basis_universal", "betsy", "camera", "csg", "cvtt", "dds", "enet", "etcpak", "fbx", "gdscript",
           "glslang", "gltf", "godot_physics_2d", "godot_physics_3d", "gridmap", "hdr", "interactive_music", "jolt_physics",
           "jsonrpc", "ktx", "lightmapper_rd", "meshoptimizer", "mobile_vr", "msdfgen", "multiplayer", "navigation_2d",
           "navigation_3d", "noise", "objectdb_profiler", "openxr", "raycast", "regex", "tga", "text_server_fb", "tinyexr",
           "upnp", "vhacd", "webrtc", "websocket", "webxr", "visual_shader", "xatlas_unwrap", "zip"]:
    globals()["module_" + _m + "_enabled"] = "no"
