// The music controls, drawn (emoji like ⏮ ⏭ ♪ don't render in every font): MediaIcon, buttons with one, and the home
// screen's music player under "Host a quiz night" (the song playing, back, play/pause, next, and the jukebox's list).

using System;
using Godot;
using QuizNight.Shared;
using QuizNight.Storage;

namespace QuizNight.Ui;

public partial class MediaIcon : Control
{
	public enum Kind { Play, Pause, Previous, Next, List, Note }

	Kind _kind;
	public Kind Shape
	{
		get => _kind;
		set
		{
			_kind = value;
			QueueRedraw();
		}
	}

	public Color Color = C.Text;

	public MediaIcon() : this(Kind.Play) { }

	public MediaIcon(Kind kind)
	{
		_kind = kind;
		MouseFilter = MouseFilterEnum.Ignore;
	}

	/// <summary>A small button with just this icon (its tooltip says what it does).</summary>
	public static Button Button(Kind kind, string tooltip, Action onPress, float size = 36)
	{
		var b = U.Small("", onPress).MinSize(size + 6, size);
		b.TooltipText = tooltip;
		b.AccessibilityName = Lang.T(tooltip);
		var icon = new MediaIcon(kind);
		icon.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect, Control.LayoutPresetMode.Minsize, (int)(size * 0.28f));
		// (the key's face is higher than its middle: KeyBox's side is at the bottom)
		icon.OffsetBottom -= 2;
		b.AddChild(icon);
		return b;
	}

	public static MediaIcon Of(Button b) => b.GetChild<MediaIcon>(0);

	public override void _Draw()
	{
		var s = Size;
		var m = Mathf.Min(s.X, s.Y);
		var o = new Vector2((s.X - m) / 2, (s.Y - m) / 2);
		Vector2 P(float x, float y) => o + new Vector2(x, y) * m;
		switch (_kind)
		{
			case Kind.Play:
				DrawColoredPolygon(new[] { P(0.18f, 0.05f), P(0.92f, 0.5f), P(0.18f, 0.95f) }, Color);
				break;
			case Kind.Pause:
				DrawRect(new Rect2(P(0.16f, 0.06f), new Vector2(0.24f, 0.88f) * m), Color);
				DrawRect(new Rect2(P(0.6f, 0.06f), new Vector2(0.24f, 0.88f) * m), Color);
				break;
			case Kind.Previous:
				DrawRect(new Rect2(P(0.06f, 0.1f), new Vector2(0.16f, 0.8f) * m), Color);
				DrawColoredPolygon(new[] { P(0.94f, 0.1f), P(0.24f, 0.5f), P(0.94f, 0.9f) }, Color);
				break;
			case Kind.Next:
				DrawColoredPolygon(new[] { P(0.06f, 0.1f), P(0.76f, 0.5f), P(0.06f, 0.9f) }, Color);
				DrawRect(new Rect2(P(0.78f, 0.1f), new Vector2(0.16f, 0.8f) * m), Color);
				break;
			case Kind.List:
				for (var i = 0; i < 3; i++)
				{
					var y = 0.18f + i * 0.32f;
					DrawCircle(P(0.12f, y), 0.09f * m, Color);
					DrawRect(new Rect2(P(0.32f, y - 0.07f), new Vector2(0.64f, 0.14f) * m), Color);
				}
				break;
			case Kind.Note:
				DrawCircle(P(0.26f, 0.78f), 0.15f * m, Color);
				DrawCircle(P(0.76f, 0.68f), 0.15f * m, Color);
				DrawLine(P(0.39f, 0.78f), P(0.39f, 0.16f), Color, Mathf.Max(1.5f, 0.1f * m));
				DrawLine(P(0.89f, 0.68f), P(0.89f, 0.06f), Color, Mathf.Max(1.5f, 0.1f * m));
				DrawColoredPolygon(new[] { P(0.34f, 0.12f), P(0.94f, 0.02f), P(0.94f, 0.2f), P(0.34f, 0.3f) }, Color);
				break;
		}
	}
}

/// <summary>The home screen's music player: what's playing, back, play/pause (music on/off, saved), next, all songs.</summary>
public partial class MusicPlayer : HBoxContainer
{
	readonly Label _title = U.Text("", 18, C.Text, Fonts.Bold).Raw();
	readonly Label _artist = U.Muted("", 13).Raw();
	readonly Button _toggle;

	static UiAudio Audio => App.I.Audio;

	public MusicPlayer()
	{
		AddThemeConstantOverride("separation", 14);
		var disc = new NoteIcon { CustomMinimumSize = new Vector2(48, 48), SizeFlagsVertical = SizeFlags.ShrinkCenter };
		_title.TextOverrunBehavior = _artist.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
		_title.ClipText = _artist.ClipText = true;
		_toggle = MediaIcon.Button(MediaIcon.Kind.Pause, "Turn music off", () =>
		{
			Audio.SetMusicOn(!Settings.MusicOn);
			Refresh();
		}, 40);
		AddChild(disc);
		AddChild(U.V(0, U.Text(Lang.T("Now playing").ToUpper(), 11, C.Muted, Fonts.Spaced), _title, _artist).Grow().With(v => v.SizeFlagsVertical = SizeFlags.ShrinkCenter));
		var controls = U.H(6,
			MediaIcon.Button(MediaIcon.Kind.Previous, "Previous song", () => Audio.Skip(-1)),
			_toggle,
			MediaIcon.Button(MediaIcon.Kind.Next, "Next song", () => Audio.Skip(1)),
			MediaIcon.Button(MediaIcon.Kind.List, "All songs", () => Jukebox.I?.Open(true)));
		controls.SizeFlagsVertical = SizeFlags.ShrinkCenter;
		AddChild(controls);
		_disc = disc;
	}

	readonly NoteIcon _disc;

	public override void _Ready()
	{
		Audio.TrackStarted += OnTrack;
		Refresh();
	}

	public override void _ExitTree() => Audio.TrackStarted -= OnTrack;

	void OnTrack(string _) => Refresh();

	void Refresh()
	{
		var on = Settings.MusicOn;
		_disc.Off = !on;
		MediaIcon.Of(_toggle).Shape = on ? MediaIcon.Kind.Pause : MediaIcon.Kind.Play;
		_toggle.TooltipText = on ? "Turn music off" : "Turn music on";
		if (!on)
		{
			_title.Text = Lang.T("Music is off");
			_artist.Text = Lang.T("Press play to turn it back on.");
		}
		else if (Audio.CurrentSong is { } song)
		{
			var (title, artist) = UiAudio.Describe(song);
			_title.Text = title;
			_artist.Text = artist;
		}
		else
		{
			_title.Text = Lang.T("No music on this screen.");
			_artist.Text = "";
		}
	}
}
