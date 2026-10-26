// "Now playing": when a menu song starts (UiAudio.TrackStarted), a small card slides in at the bottom left with its title
// and artist (from the song's file name, or assets/music/CREDITS.md), stays a few seconds and slides out.

using System.IO;
using System.Linq;
using Godot;
using QuizNight.Shared;

namespace QuizNight.Ui;

public partial class NowPlaying : PanelContainer
{
	/// <summary>The card showing now: a new song replaces it.</summary>
	static NowPlaying? _current;

	public NowPlaying() : this("") { }

	public NowPlaying(string trackPath)
	{
		MouseFilter = MouseFilterEnum.Ignore;
		var (title, artist) = UiAudio.Describe(trackPath);
		var text = artist != "" ? $"{title} · {artist}" : title;
		AddThemeStyleboxOverride("panel", UiTheme.Box(C.Panel, 14, C.Border, 2, 12, 8));
		AddChild(U.H(12,
			new NoteIcon { CustomMinimumSize = new Vector2(34, 34), SizeFlagsVertical = SizeFlags.ShrinkCenter },
			U.V(0, U.Text(Lang.T("Now playing").ToUpper(), 11, C.Muted, Fonts.Spaced), U.Text(text, 15, C.Text, Fonts.Bold).Raw())));
	}

	public override void _Ready()
	{
		if (_current != null && IsInstanceValid(_current)) _current.QueueFree();
		_current = this;
		// (bottom left, out of the way of the screens' own buttons, beside the jukebox; slides in from the left)
		Callable.From(() =>
		{
			var parent = GetParentControl()?.Size ?? GetViewportRect().Size;
			var y = ClearOfButtons(Jukebox.Right + 10f, parent.Y - Size.Y - 20);
			Position = new Vector2(-Size.X - 10, y);
			var t = CreateTween();
			t.TweenProperty(this, "position:x", Jukebox.Right + 10f, 0.45).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
			t.TweenInterval(5);
			t.TweenProperty(this, "position:x", -Size.X - 10, 0.35).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.In);
			t.TweenCallback(Callable.From(QueueFree));
		}).CallDeferred();
	}

	/// <summary>
	/// The card's height: above any of the screen's buttons it would cover there (the quiz overview's "Copy share code"
	/// sits in the bottom left), else at the bottom.
	/// </summary>
	float ClearOfButtons(float x, float y)
	{
		var buttons = new System.Collections.Generic.List<Rect2>();
		void Collect(Node node)
		{
			foreach (var child in node.GetChildren())
			{
				// (lists scroll under it anyway: only fixed buttons like a screen's bottom row count)
				if (child == this || child is Jukebox or NowPlaying or ScrollContainer) continue;
				if (child is BaseButton b && b.IsVisibleInTree()) buttons.Add(b.GetGlobalRect());
				Collect(child);
			}
		}
		Collect(GetParent());
		for (var tries = 0; tries < 8; tries++)
		{
			var card = new Rect2(x, y, Size);
			var hit = buttons.Where(r => r.Intersects(card)).ToList();
			if (hit.Count == 0) break;
			y = hit.Min(r => r.Position.Y) - Size.Y - 10;
		}
		return y;
	}
}

/// <summary>A rounded square with two music notes, drawn (emoji notes don't render in every font). Off: greyed and struck through.</summary>
public partial class NoteIcon : Control
{
	bool _off;
	public bool Off
	{
		get => _off;
		set
		{
			_off = value;
			QueueRedraw();
		}
	}

	public NoteIcon() => MouseFilter = MouseFilterEnum.Ignore;

	public override void _Draw()
	{
		var s = Size;
		DrawStyleBox(UiTheme.Box(_off ? C.Panel2 : C.Accent, 9, null, 0, 0, 0), new Rect2(Vector2.Zero, s));
		var ink = _off ? C.Muted : C.AccentInk;
		var w = s.X;
		DrawCircle(new Vector2(w * 0.34f, s.Y * 0.7f), w * 0.11f, ink);
		DrawCircle(new Vector2(w * 0.68f, s.Y * 0.62f), w * 0.11f, ink);
		DrawLine(new Vector2(w * 0.43f, s.Y * 0.7f), new Vector2(w * 0.43f, s.Y * 0.28f), ink, 2.5f);
		DrawLine(new Vector2(w * 0.77f, s.Y * 0.62f), new Vector2(w * 0.77f, s.Y * 0.2f), ink, 2.5f);
		DrawLine(new Vector2(w * 0.43f, s.Y * 0.28f), new Vector2(w * 0.77f, s.Y * 0.2f), ink, 3.5f);
		if (_off) DrawLine(new Vector2(w * 0.18f, s.Y * 0.18f), new Vector2(w * 0.82f, s.Y * 0.82f), C.Coral, 3f);
	}
}
