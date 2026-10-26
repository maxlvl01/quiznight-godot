using System;
using System.IO;
using Godot;
using QuizNight.Shared;
using QuizNight.Storage;

namespace QuizNight.Ui.Screens;

/// <summary>
/// Step-by-step help for turning an MP4 (or WebM, MOV…) into the .ogv Godot can play, with ffmpeg.
/// Picking the video fills in a ready-to-paste command with full paths; "Use this video" loads the result.
/// </summary>
public static partial class VideoGuide
{
	/// <summary>Opens the guide as an overlay over the whole app (a popup Window can't size wrapped text).</summary>
	public static void Show(Node owner, string? videoPath, Action<string> useOgv)
	{
		var d = new GuideOverlay();
		string? input = null;

		var pickedLabel = U.Muted("", 13).Raw();
		var convertCode = Code("");
		var resultLabel = U.Muted("", 13).Raw();
		var status = U.Error();

		void SetVideo(string? path)
		{
			input = path;
			var shownIn = path != null ? WinPath(path) : Lang.T("your-video.mp4");
			var shownOut = path != null ? WinPath(OgvPath(path)) : Lang.T("your-video.ogv");
			pickedLabel.Text = path != null ? Lang.F("Your video: {0}", Path.GetFileName(path)) : Lang.T("No video picked yet.");
			SetCode(convertCode, Media.ConvertCommand(shownIn, shownOut));
			resultLabel.Text = path != null ? Lang.F("The new file will be: {0}", WinPath(OgvPath(path))) : "";
			resultLabel.Visible = path != null;
			status.Visible = false;
		}

		var pick = U.Small("Pick your video…", () => Dialogs.OpenFile(d, "Pick a video", Media.VideoFilters, SetVideo));
		var use = U.Btn("Use the converted video", "TealButton", () =>
		{
			if (input == null)
			{
				status.Text = Lang.T(AnyConverterPath != null ? "Pick your video in step 1 first." : "Pick your video in step 2 first.");
				status.Visible = true;
				return;
			}
			var ogv = OgvPath(input);
			if (!File.Exists(ogv))
			{
				status.Text = Lang.T(AnyConverterPath != null
					? "The .ogv file isn't there yet. Wait until AnyConverter says Done, then try again."
					: "The .ogv file isn't there yet. Wait until the terminal is finished, then try again.");
				status.Visible = true;
				return;
			}
			d.QueueFree();
			useOgv(ogv);
		}, 15);

		var header = U.H(10, U.Heading("Turn your video into an .ogv file", 24).Grow(), U.Small("Close", d.QueueFree).With(b => b.SizeFlagsVertical = Control.SizeFlags.ShrinkBegin));
		var content = AnyConverterPath is { } converter
			// The Windows and Linux builds ship AnyConverter: two clicks instead of the terminal.
			? U.V(16, header,
				U.Text("Quiznight can only play videos in the .ogv format. AnyConverter, which comes with Quiznight, turns your MP4 (or WebM, MOV, MKV) into one.", 15, null, null, true),
				Step(1, "Convert your video",
					U.H(10, pick, pickedLabel.With(l => l.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter)),
					U.Btn("Open AnyConverter", "AccentButton", () => OpenAnyConverter(converter, input), 15)
						.With(b => b.SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin),
					U.Muted("It opens with your video and .ogv already chosen: press Convert and wait until it says Done. The very first time, it helps you install FFmpeg, the free tool that does the converting.", 13)),
				Step(2, "Use it in your question",
					resultLabel,
					use.With(b => b.SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin),
					status))
			: U.V(16, header,
			U.Text("Quiznight can only play videos in the .ogv format. A free tool called ffmpeg turns your MP4 (or WebM, MOV, MKV) into one. It takes three steps.", 15, null, null, true),
			Step(1, "Install ffmpeg (only the very first time)",
				U.Text("Press the Windows key, type Terminal and press Enter. Then paste this line (Ctrl+V) and press Enter:", 14, null, null, true),
				Code("winget install Gyan.FFmpeg"),
				U.Muted("If it asks you to agree, type Y and press Enter. When it says \"Successfully installed\", close the window.", 13)),
			Step(2, "Convert your video",
				U.H(10, pick, pickedLabel.With(l => l.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter)),
				U.Text("Open the Terminal again (a new window, so it finds ffmpeg), paste this line and press Enter:", 14, null, null, true),
				convertCode,
				U.Muted("Lots of text will scroll by. It's done when it stops and you can type again — a few seconds for a short clip, up to a minute or two for a long trailer.", 13)),
			Step(3, "Use it in your question",
				resultLabel,
				use.With(b => b.SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin),
				status));
		content.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		d.SetContent(content);
		SetVideo(videoPath);
		App.I.AddChild(d);
	}

	/// <summary>Dimmed backdrop with a centred, scrollable card. Esc or a click outside closes it.</summary>
	/// <summary>A dimmed full-window cover with a centred, scrolling card (also used by What's new and the updater).</summary>
	public partial class GuideOverlay : Control
	{
		readonly ScrollContainer _scroll = new() { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
		/// <summary>Click outside or Esc closes it (off while an update downloads).</summary>
		public bool Closable = true;

		public GuideOverlay()
		{
			SetAnchorsPreset(LayoutPreset.FullRect);
			MouseFilter = MouseFilterEnum.Stop;
			var dim = new ColorRect { Color = new Color(0, 0, 0, 0.6f) };
			dim.SetAnchorsPreset(LayoutPreset.FullRect);
			dim.GuiInput += e =>
			{
				if (Closable && e is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left }) QueueFree();
			};
			AddChild(dim);
			var center = new CenterContainer { MouseFilter = MouseFilterEnum.Ignore };
			center.SetAnchorsPreset(LayoutPreset.FullRect);
			AddChild(center);
			center.AddChild(U.Panel(_scroll));
		}

		public void SetContent(Control content)
		{
			_scroll.AddChild(content);
			// Wrapped text only knows its height once it has a width, so refit when that settles.
			content.MinimumSizeChanged += FitToWindow;
		}

		public override void _Ready()
		{
			FitToWindow();
			// (opens at the top: while the text was still being wrapped, the scroll could end up at the bottom)
			GetTree().CreateTimer(0.1).Timeout += () => { if (IsInstanceValid(_scroll)) _scroll.ScrollVertical = 0; };
		}

		public override void _Notification(int what)
		{
			if (what == NotificationResized) FitToWindow();
		}

		// The card is 740 wide (less on small windows) and as tall as its content, up to the window height.
		void FitToWindow()
		{
			var w = Mathf.Min(740, Size.X - 48);
			// (the content gets the card's width itself, less the scroll bar: left to the scroll container, wrapped text
			// ended up only as wide as the heading)
			if (_scroll.GetChildCount() > 0) _scroll.GetChild<Control>(0).CustomMinimumSize = new Vector2(w - 14, 0);
			var contentH = _scroll.GetChildCount() > 0 ? _scroll.GetChild<Control>(0).GetCombinedMinimumSize().Y : 0;
			_scroll.CustomMinimumSize = new Vector2(w, Mathf.Min(contentH, Size.Y - 120));
		}

		public override void _UnhandledKeyInput(InputEvent e)
		{
			if (Closable && e is InputEventKey { Pressed: true, Keycode: Key.Escape })
			{
				QueueFree();
				GetViewport().SetInputAsHandled();
			}
		}
	}

	/// <summary>AnyConverter, shipped with the Windows and Linux builds in an AnyConverter folder next to the game (null when it isn't there).</summary>
	static string? AnyConverterPath
	{
		get
		{
			var name = OS.GetName() switch { "Windows" => "AnyConverter.exe", "Linux" => "AnyConverter", _ => null };
			if (name == null) return null;
			var path = Path.Combine(Path.GetDirectoryName(OS.GetExecutablePath()) ?? "", "AnyConverter", name);
			return File.Exists(path) ? path : null;
		}
	}

	static void OpenAnyConverter(string converter, string? video)
	{
		// Zips made on Windows lose the executable bit.
		if (OS.GetName() == "Linux") OS.Execute("chmod", new[] { "+x", converter });
		OS.CreateProcess(converter, video != null ? new[] { "--format", "ogv", WinPath(video) } : new[] { "--format", "ogv" });
	}

	static string OgvPath(string video) => Path.ChangeExtension(video, ".ogv");

	/// <summary>Backslashes on Windows; other systems keep the path as it is.</summary>
	static string WinPath(string path) => OS.GetName() == "Windows" ? path.Replace('/', '\\') : path;

	static Control Step(int n, string title, params Control[] body)
	{
		var badge = U.Panel(U.Text($"{n}", 14, C.AccentInk, Fonts.Black, false, HorizontalAlignment.Center), "Clear")
			.With(p => p.AddThemeStyleboxOverride("panel", UiTheme.Box(C.Accent, 999, null, 0, 0, 2))).MinSize(26, 26);
		badge.SizeFlagsVertical = Control.SizeFlags.ShrinkBegin;
		var col = U.V(8, U.Bold(title, 16));
		foreach (var b in body) col.AddChild(b);
		return U.H(12, badge, col.Grow());
	}

	/// <summary>A command in a box with a Copy button.</summary>
	static Control Code(string command)
	{
		var text = U.Mono(command, 13).Raw();
		text.AutowrapMode = TextServer.AutowrapMode.Arbitrary;
		text.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		Button? copy = null;
		copy = U.Small("Copy", () =>
		{
			DisplayServer.ClipboardSet(text.Text);
			copy!.Text = Lang.T("Copied!");
		});
		copy.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
		var box = U.Panel(U.H(10, text, copy), "Inset");
		box.SetMeta("code", text);
		box.SetMeta("copy", copy);
		return box;
	}

	static void SetCode(Control box, string command)
	{
		((Label)box.GetMeta("code").AsGodotObject()).Text = command;
		((Button)box.GetMeta("copy").AsGodotObject()).Text = Lang.T("Copy");
	}
}
