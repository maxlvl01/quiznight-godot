// Inputs used by the question editor: image and media pickers, round settings.

using System;
using System.Collections.Generic;
using Godot;
using QuizNight.Shared;
using QuizNight.Storage;

namespace QuizNight.Ui.Screens;

/// <summary>Pick an image from a file, the clipboard (Ctrl+V) or by dropping it on the window.</summary>
public partial class ImageField : VBoxContainer
{
	ImageAsset? _image;
	readonly int _maxSide;
	readonly Action<ImageAsset?> _changed;
	readonly bool _removable;
	readonly string _emptyText;
	readonly Label _error = U.Error();
	readonly VBoxContainer _body = U.V(8);

	public ImageField() : this(null, _ => { }, Images.ScreenshotMax, "") { }

	public ImageField(ImageAsset? image, Action<ImageAsset?> changed, int maxSide, string emptyText, bool removable = true)
	{
		_image = image;
		_changed = changed;
		_maxSide = maxSide;
		_emptyText = emptyText;
		_removable = removable;
		AddThemeConstantOverride("separation", 6);
		SizeFlagsHorizontal = SizeFlags.ExpandFill;
		AddChild(_body);
		AddChild(_error);
		Build();
	}

	void Build()
	{
		_body.Clear();
		Control preview;
		if (_image != null && _image.DataUrl != "")
		{
			preview = new FitImage { MaxHeight = 300, Texture = Images.TextureFromDataUrl(_image.DataUrl) };
		}
		else
		{
			preview = U.V(4,
				U.Muted(_emptyText, 14, true, HorizontalAlignment.Center),
				U.Muted("Drop a file on this window, or copy a screenshot (Win+Shift+S) and press Ctrl+V.", 12, true, HorizontalAlignment.Center))
				.MinSize(0, 120).With(v => v.Alignment = BoxContainer.AlignmentMode.Center);
		}
		var box = U.Panel(preview, "Inset");
		box.AddThemeStyleboxOverride("panel", UiTheme.Box(C.Bg, 12, C.Border, 1, 10, 10).With(s => s.BorderBlend = false));
		_body.AddChild(box);
		var row = U.H(6, U.Small(_image == null ? "Choose file…" : "Replace…", PickFile), U.Small("Paste", () => Paste()).Tip("Paste an image from the clipboard (Ctrl+V)"));
		if (_image != null && _removable) row.AddChild(U.Link("Remove image", () => Set(null), 14));
		_body.AddChild(row);
	}

	void Set(ImageAsset? image)
	{
		_image = image;
		_error.Visible = false;
		_changed(image);
		Build();
	}

	void Fail(string message)
	{
		_error.Text = message;
		_error.Visible = true;
	}

	void PickFile() => Dialogs.OpenFile(this, "Pick an image", Images.FileFilters, LoadFile);

	public void LoadFile(string path)
	{
		try
		{
			Set(Images.ReadFile(path, _maxSide));
		}
		catch (UserError e)
		{
			Fail(e.Message);
		}
	}

	/// <summary>Takes an image from the clipboard. Returns false if there is none.</summary>
	public bool Paste()
	{
		var img = Images.FromClipboard(_maxSide);
		if (img == null)
		{
			Fail("There's no image on the clipboard. Copy a screenshot first (Win+Shift+S).");
			return false;
		}
		Set(img);
		return true;
	}
}

/// <summary>Upload a sound or video file, with a preview player.</summary>
public partial class MediaField : VBoxContainer
{
	MediaAsset? _media;
	readonly string _type;
	readonly Action<MediaAsset?> _changed;
	readonly Label _error = U.Error();
	readonly VBoxContainer _body = U.V(8);
	AudioStreamPlayer? _audio;
	VideoStreamPlayer? _video;
	Button? _play;

	public MediaField() : this(null, QuestionType.Sound, _ => { }) { }

	public MediaField(MediaAsset? media, string type, Action<MediaAsset?> changed)
	{
		_media = media;
		_type = type;
		_changed = changed;
		AddThemeConstantOverride("separation", 6);
		SizeFlagsHorizontal = SizeFlags.ExpandFill;
		AddChild(_body);
		AddChild(_error);
		Build();
	}

	bool IsSound => _type == QuestionType.Sound;

	void Build()
	{
		_body.Clear();
		_audio = null;
		_video = null;
		_play = null;
		if (_media == null || _media.DataUrl == "")
		{
			var hint = IsSound
				? Lang.F("A song or sound effect: MP3, OGG or WAV, up to {0} MB.", QuizRules.MediaMaxMb(_type))
				: Lang.F("A trailer or clip as .ogv (Ogg Theora), up to {0} MB.", QuizRules.MediaMaxMb(_type));
			_body.AddChild(U.Panel(U.V(4, U.Muted(hint, 14, true, HorizontalAlignment.Center),
					U.Muted("Click below or drop the file on this window.", 12, true, HorizontalAlignment.Center))
				.MinSize(0, 90).With(v => v.Alignment = BoxContainer.AlignmentMode.Center), "Inset"));
			var row = U.H(6, U.Small("Choose file…", PickFile));
			if (!IsSound) row.AddChild(U.Link("Have an MP4? Here's how to convert it", () => ShowGuide(null), 14));
			_body.AddChild(row);
			return;
		}
		var duration = _media.DurationSec is { } d ? $" · {U.Clock(Math.Round(d))}" : "";
		if (IsSound)
		{
			var stream = Media.LoadSound(_media);
			if (stream == null) Fail("Godot can't play this sound format. Use MP3, OGG or WAV.");
			_audio = new AudioStreamPlayer { Stream = stream, Bus = Prefs.MusicBus };
			_body.AddChild(_audio);
		}
		else if (Media.LoadVideo(_media, "editor-preview") is { } video)
		{
			_video = new VideoStreamPlayer { Stream = video, Bus = Prefs.MusicBus, Expand = true, CustomMinimumSize = new Vector2(0, 220) };
			_body.AddChild(U.Panel(_video, "Inset"));
		}
		else Fail(Media.VideoHelp);
		_play = U.Small("▶ Play", TogglePlay);
		_play.Disabled = _audio?.Stream == null && _video == null;
		_body.AddChild(U.H(8, _play, U.Muted(_media.Name + duration, 13, false).Raw().With(l =>
		{
			l.SizeFlagsVertical = SizeFlags.ShrinkCenter;
			l.ClipText = true;
			l.SizeFlagsHorizontal = SizeFlags.ExpandFill;
			l.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
		})));
		_body.AddChild(U.H(6, U.Link("Replace file", PickFile, 14), U.Link("Remove", () => Set(null), 14)));
	}

	void TogglePlay()
	{
		if (_audio != null)
		{
			if (_audio.Playing) _audio.Stop();
			else _audio.Play();
		}
		else if (_video != null)
		{
			if (_video.IsPlaying()) _video.Stop();
			else _video.Play();
		}
	}

	public override void _Process(double delta)
	{
		if (_play == null) return;
		var playing = _audio?.Playing == true || _video?.IsPlaying() == true;
		_play.Text = playing ? "■ Stop" : "▶ Play";
	}

	void Set(MediaAsset? media)
	{
		_media = media;
		_error.Visible = false;
		_changed(media);
		Build();
	}

	void Fail(string message)
	{
		_error.Text = message;
		_error.Visible = true;
	}

	void PickFile() => Dialogs.OpenFile(this, IsSound ? "Pick a sound file" : "Pick a video", IsSound ? Media.SoundFilters : Media.VideoFilters, LoadFile);

	void ShowGuide(string? video) => VideoGuide.Show(this, video, LoadFile);

	public void LoadFile(string path)
	{
		if (!IsSound && Media.IsConvertibleVideo(path))
		{
			ShowGuide(path);
			return;
		}
		try
		{
			Set(Media.ReadFile(path, _type));
		}
		catch (UserError e)
		{
			Fail(e.Message);
		}
		catch (System.IO.IOException e)
		{
			Fail(Lang.F("Couldn't read that file: {0}", e.Message));
		}
	}
}

public static class RoundSettings
{
	public static VBoxContainer Points(Question q, Action changed) =>
		U.Field("Points", new NumberField(q.Points, v =>
		{
			q.Points = (int)Math.Clamp(Math.Round(v ?? QuestionLimits.PointsMin), QuestionLimits.PointsMin, QuestionLimits.PointsMax);
			changed();
		}, QuestionLimits.PointsMin, QuestionLimits.PointsMax, true));

	public static VBoxContainer TimeLimit(Question q, Action changed) =>
		U.Field("Time limit (seconds)", new NumberField(q.TimeLimitSec, v =>
		{
			q.TimeLimitSec = (int)Math.Clamp(Math.Round(v ?? QuestionLimits.TimeLimitMin), QuestionLimits.TimeLimitMin, QuestionLimits.TimeLimitMax);
			changed();
		}, QuestionLimits.TimeLimitMin, QuestionLimits.TimeLimitMax, true));

	public static PanelContainer Panel(params Control[] kids)
	{
		var all = new List<Control> { U.Bold("Round settings", 17) };
		all.AddRange(kids);
		return U.Panel(U.V(12, all.ToArray()), "Card2");
	}

	/// <summary>Round settings shared by estimation and map pin questions.</summary>
	public static PanelContainer Closeness(Question q, Action changed, params Control[] extra)
	{
		var kids = new List<Control> { TimeLimit(q, changed) };
		kids.AddRange(extra);
		kids.Add(U.Field("If there is a tie", U.Select(TieRule.All, q.TieRule, id =>
		{
			q.TieRule = id;
			changed();
		})));
		kids.Add(U.Field("Scoring", U.Select(Scoring.All, q.Scoring, id =>
		{
			q.Scoring = id;
			changed();
		})));
		kids.Add(Points(q, changed));
		return Panel(kids.ToArray());
	}
}
