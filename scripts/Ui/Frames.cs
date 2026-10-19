// Containers that keep things in shape on any screen: the screens at most 16:9 wide (ultrawide monitors
// get the background colour at the sides instead of stretched-out screens), and videos at their own
// aspect ratio (letterboxed in black) instead of filling whatever box they're in.

using Godot;

namespace QuizNight.Ui;

/// <summary>Lays its children out in the middle, at most 16:9 wide (full height; narrower windows use all of it).</summary>
public partial class ScreenFrame : Container
{
	public const float MaxAspect = 16f / 9f;

	public override void _Notification(int what)
	{
		if (what != NotificationSortChildren) return;
		var width = Mathf.Min(Size.X, Size.Y * MaxAspect);
		var rect = new Rect2((Size.X - width) / 2, 0, width, Size.Y);
		foreach (var child in GetChildren())
			if (child is Control c) FitChildInRect(c, rect);
	}
}

/// <summary>A video player kept at the video's own aspect ratio, centred on black.</summary>
public partial class VideoFrame : Container
{
	readonly VideoStreamPlayer? _video;
	Vector2 _videoSize = new(16, 9);

	public VideoFrame() : this(null) { }

	public VideoFrame(VideoStreamPlayer? video)
	{
		_video = video;
		ClipContents = true;
		if (video == null) return;
		video.Expand = true;
		video.CustomMinimumSize = Vector2.Zero;
		AddChild(video);
	}

	public override void _Process(double delta)
	{
		// (the video's size is only known once Godot has decoded a frame)
		if (_video?.GetVideoTexture() is not { } tex) return;
		var size = tex.GetSize();
		if (size.X < 1 || size.Y < 1 || size == _videoSize) return;
		_videoSize = size;
		QueueSort();
	}

	public override void _Draw() => DrawRect(new Rect2(Vector2.Zero, Size), Colors.Black);

	public override void _Notification(int what)
	{
		if (what != NotificationSortChildren || _video == null) return;
		var scale = Mathf.Min(Size.X / _videoSize.X, Size.Y / _videoSize.Y);
		var fit = _videoSize * scale;
		FitChildInRect(_video, new Rect2((Size - fit) / 2, fit));
		QueueRedraw();
	}
}
