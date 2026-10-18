// Image reveal questions: a picture that starts zoomed right in or pixelated and gets clearer over the
// question's reveal time. ImageRevealView draws it at any progress (0 = hardest, 1 = the whole picture);
// HostImageReveal runs the clock on the host screen like HostMedia does for sound: it pauses while
// someone answers and tells the host session when it runs (buzz times are measured on that clock).

using System;
using System.Linq;
using Godot;
using QuizNight.Server;
using QuizNight.Shared;

namespace QuizNight.Ui;

public partial class ImageRevealView : Control
{
	/// <summary>Zoom: how far in it starts (10 = a tenth of the picture's width).</summary>
	const float StartZoom = 10;
	/// <summary>Pixelate: blocks across the picture at the start.</summary>
	const float StartBlocks = 6;

	Texture2D? _texture;
	string _style = RevealStyle.Zoom;
	NormPoint _focus = new(0.5, 0.5);
	float _progress;
	/// <summary>Pixelate: the picture shrunk to fewer and fewer blocks, made when first needed.</summary>
	const int Levels = 30;
	readonly Texture2D?[] _levels = new Texture2D?[Levels];
	Image? _source;
	/// <summary>The part of the picture on screen right now (in picture pixels).</summary>
	Rect2 _shown = new(0, 0, 1, 1);

	float _maxHeight = 460;
	public float MaxHeight
	{
		get => _maxHeight;
		set
		{
			_maxHeight = value;
			UpdateHeight();
		}
	}
	/// <summary>Where the zoom starts, shown as a marker in the editor.</summary>
	public bool ShowFocus;
	/// <summary>Editor: a click on the picture sets the zoom's starting spot.</summary>
	public Action<NormPoint>? OnPick;

	public ImageRevealView()
	{
		SizeFlagsHorizontal = SizeFlags.ExpandFill;
		ClipContents = true;
		TextureFilter = TextureFilterEnum.LinearWithMipmaps;
	}

	public void Set(Texture2D? texture, string style, NormPoint focus)
	{
		_texture = texture;
		_style = style;
		_focus = focus;
		Array.Clear(_levels);
		_source = null;
		UpdateHeight();
		QueueRedraw();
	}

	/// <summary>0 = hardest to see, 1 = the whole picture.</summary>
	public float Progress
	{
		get => _progress;
		set
		{
			_progress = Mathf.Clamp(value, 0, 1);
			QueueRedraw();
		}
	}

	float Aspect => _texture == null ? 0.5625f : (float)_texture.GetHeight() / _texture.GetWidth();

	void UpdateHeight()
	{
		if (_maxHeight <= 0) this.MarkFlexible();
		var h = U.FitHeight(_maxHeight, Size.X * Aspect);
		if (Mathf.Abs(CustomMinimumSize.Y - h) > 0.5f) CustomMinimumSize = new Vector2(0, h);
	}

	public override void _Notification(int what)
	{
		if (what == NotificationResized) UpdateHeight();
	}

	public override void _GuiInput(InputEvent e)
	{
		if (OnPick == null || e is not InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true } click) return;
		var r = PictureRect;
		if (_texture == null || !r.HasPoint(click.Position)) return;
		// (through the current zoom: the preview may be zoomed in when you click)
		var p = (_shown.Position + (click.Position - r.Position) / r.Size * _shown.Size) / new Vector2(_texture.GetWidth(), _texture.GetHeight());
		OnPick(new NormPoint(p.X, p.Y));
		AcceptEvent();
	}

	/// <summary>Where the picture sits in the control (fitted, centred).</summary>
	public Rect2 PictureRect
	{
		get
		{
			var w = Mathf.Min(Size.X, Size.Y / Aspect);
			var size = new Vector2(w, w * Aspect);
			return new Rect2((Size - size) / 2, size);
		}
	}

	/// <summary>The picture at pixel step <paramref name="level"/> (0 = StartBlocks blocks across).</summary>
	Texture2D? Level(int level)
	{
		if (_levels[level] is { } done) return done;
		if (_source == null)
		{
			if (_texture?.GetImage() is not { } img) return null;
			_source = (Image)img.Duplicate();
			if (_source.IsCompressed()) _source.Decompress();
			_source.ClearMipmaps();
		}
		var w = _source.GetWidth();
		var across = Mathf.Max(1, Mathf.RoundToInt(StartBlocks * Mathf.Pow(w / StartBlocks, (float)level / Levels)));
		var small = (Image)_source.Duplicate();
		// (shrinking averages each block, so a block shows its real colour)
		small.Resize(across, Mathf.Max(1, Mathf.RoundToInt(across * Aspect)), Image.Interpolation.Lanczos);
		return _levels[level] = ImageTexture.CreateFromImage(small);
	}

	public override void _Draw()
	{
		var rect = PictureRect;
		DrawRect(new Rect2(Vector2.Zero, Size), C.Bg);
		if (_texture == null)
		{
			DrawString(Fonts.Regular, new Vector2(0, Size.Y / 2), Lang.T("No picture yet"), HorizontalAlignment.Center, Size.X, 15, C.Muted);
			return;
		}
		var tex = new Vector2(_texture.GetWidth(), _texture.GetHeight());
		if (_style == RevealStyle.Pixelate)
		{
			// In steps: blocks get finer exponentially, so it sharpens at a steady pace; the last step is the picture.
			var level = (int)(_progress * Levels);
			var small = level < Levels ? Level(level) : null;
			TextureFilter = small != null ? TextureFilterEnum.Nearest : TextureFilterEnum.LinearWithMipmaps;
			_shown = new Rect2(Vector2.Zero, tex);
			DrawTextureRect(small ?? _texture, rect, false);
		}
		else
		{
			TextureFilter = TextureFilterEnum.LinearWithMipmaps;
			// Zoom out from StartZoom× to 1×, keeping the focus spot in view.
			var zoom = Mathf.Pow(StartZoom, 1 - _progress);
			var part = tex / zoom;
			var center = new Vector2((float)_focus.X, (float)_focus.Y) * tex;
			var from = (center - part / 2).Clamp(Vector2.Zero, tex - part);
			_shown = new Rect2(from, part);
			DrawTextureRectRegion(_texture, rect, _shown);
		}
		if (ShowFocus && _style == RevealStyle.Zoom)
		{
			var at = rect.Position + (new Vector2((float)_focus.X, (float)_focus.Y) * tex - _shown.Position) / _shown.Size * rect.Size;
			DrawArc(at, 16, 0, Mathf.Tau, 32, C.Bg, 6, true);
			DrawArc(at, 16, 0, Mathf.Tau, 32, C.Accent, 3, true);
			DrawCircle(at, 3, C.Accent, true, -1, true);
		}
	}
}

/// <summary>The image reveal on the host screen: runs the clock, pauses while someone answers.</summary>
public partial class HostImageReveal : VBoxContainer
{
	readonly HostSession? _host;
	readonly ImageRevealView _view = new();
	readonly Button _toggle;
	readonly Label _time;
	readonly ProgressBar _bar = new() { MinValue = 0, MaxValue = 1, Step = 0, ShowPercentage = false, CustomMinimumSize = new Vector2(0, 8) };
	readonly double _duration;
	double _played;
	bool _paused;
	bool _autoPaused;
	bool _open = true;
	bool _reported;

	public HostImageReveal() : this(null, new Question()) { }

	/// <param name="host">Null in the editor's preview: then it only plays, nothing is reported.</param>
	public HostImageReveal(HostSession? host, Question q, float maxHeight = 460)
	{
		_host = host;
		_duration = Math.Max(1, q.RevealSec);
		AddThemeConstantOverride("separation", 10);
		_view.MaxHeight = maxHeight;
		_view.Set(q.Image is { DataUrl: not "" } img ? Storage.Images.TextureFromDataUrl(img.DataUrl) : null, q.RevealStyle, q.Focus);
		var frame = U.Panel(_view, "Clear").With(p => p.AddThemeStyleboxOverride("panel", UiTheme.Box(C.Bg, 12, C.Border, 1, 0, 0)));
		AddChild(frame);
		if (maxHeight <= 0)
		{
			frame.SizeFlagsVertical = SizeFlags.ExpandFill;
			SizeFlagsVertical = SizeFlags.ExpandFill;
		}
		_toggle = U.Small("Pause", () =>
		{
			_autoPaused = false;
			if (_played >= _duration)
			{
				// (the editor preview, played to the end: from the start again)
				_played = 0;
				_paused = false;
			}
			else _paused = !_paused;
		});
		_time = U.Mono("0 s", 15, C.Muted);
		_bar.AddThemeStyleboxOverride("background", UiTheme.Box(C.Panel2, 4, null, 0, 0, 0));
		_bar.AddThemeStyleboxOverride("fill", UiTheme.Box(C.Accent, 4, null, 0, 0, 0));
		AddChild(Screens.GameParts.Row(10, _toggle, _bar.Grow().With(b => b.SizeFlagsVertical = SizeFlags.ShrinkCenter), _time));
	}

	public ImageRevealView View => _view;

	bool Running => _open && !_paused && _played < _duration;

	/// <summary>Follows the game: pause while someone answers, show it all at the reveal.</summary>
	public void Update(GameView game)
	{
		_open = game.Stage == Stage.Open;
		// At the reveal it makes room for the answer below it.
		if (!_open && _view.MaxHeight > 0) _view.MaxHeight = Mathf.Min(_view.MaxHeight, 300);
		var answering = _open && game.Buzzes.Any(b => b.Status == BuzzStatus.Answering);
		if (answering && !_paused)
		{
			_autoPaused = true;
			_paused = true;
		}
		else if (!answering && _autoPaused && _open)
		{
			_autoPaused = false;
			_paused = false;
		}
	}

	public override void _Process(double delta)
	{
		if (Running) _played = Math.Min(_duration, _played + delta);
		var running = Running;
		if (running != _reported)
		{
			_reported = running;
			if (_open) _host?.SetMedia(running);
		}
		var progress = _open ? (float)(_played / _duration) : 1;
		_view.Progress = progress;
		_bar.Value = progress;
		_toggle.Text = Lang.T(_played >= _duration ? "Again" : _paused ? "Play" : "Pause");
		_toggle.Visible = _host == null || _open && _played < _duration;
		_time.Text = $"{Math.Floor(_played)} / {_duration} s";
	}
}
