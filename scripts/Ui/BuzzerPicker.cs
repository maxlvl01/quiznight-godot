using System;
using Godot;
using QuizNight.Shared;
using QuizNight.Storage;

namespace QuizNight.Ui;

/// <summary>"Buzzer sound  [Soft beep ▾] [▶]" — used in Settings (the default) and the host lobby (tonight's).</summary>
public static class BuzzerPicker
{
	public static HBoxContainer Make(string selected, Action<string> changed, bool withLabelAbove)
	{
		var current = Sfx.Valid(selected);
		var play = new PreviewButton("buzzer", false, () => Sfx.PlayBuzzer(current), "Play the buzzer sound");
		var select = U.Select(Sfx.BuzzerChoices(), current, id =>
		{
			current = id;
			changed(id);
			play.Start();
		}).MinSize(220);
		select.SizeFlagsHorizontal = withLabelAbove ? Control.SizeFlags.ExpandFill : Control.SizeFlags.ShrinkEnd;
		return withLabelAbove ? U.H(8, select, play) : U.H(8, U.Text("Buzzer sound", 15).Grow(), select, play);
	}
}

/// <summary>The podium sound: a dropdown and ▶ to hear it.</summary>
public static class PodiumPicker
{
	public static HBoxContainer Make(string selected, Action<string> changed, bool withLabel)
	{
		var current = Sfx.ValidPodium(selected);
		var select = U.Select(Sfx.PodiumChoices(), current, id =>
		{
			current = id;
			changed(id);
		}).MinSize(220);
		select.SizeFlagsHorizontal = withLabel ? Control.SizeFlags.ShrinkEnd : Control.SizeFlags.ExpandFill;
		var play = new PreviewButton("podium", true, () => Sfx.PlayPodium(current), "Play the podium sound");
		return withLabel ? U.H(8, U.Text("Podium sound", 15).Grow(), select, play) : U.H(8, select, play);
	}
}

/// <summary>
/// ▶ to hear a sound, ■ while it plays to stop it. Only one preview plays at a time: starting one stops the other, and
/// leaving the screen stops it (Sfx.StopPreview). A rebuilt picker (the host lobby redraws often) picks up its ■.
/// </summary>
public partial class PreviewButton : Button
{
	static string? _activeKind;
	readonly string _kind;
	readonly bool _music;
	readonly Action _play;

	public PreviewButton() : this("", false, () => { }, "") { }

	/// <param name="kind">Which sound ("buzzer", "podium"), so a rebuilt button knows it's the one playing.</param>
	/// <param name="music">Plays on the podium (music) player, not the buzzer one.</param>
	public PreviewButton(string kind, bool music, Action play, string tip)
	{
		_kind = kind;
		_music = music;
		_play = play;
		ThemeTypeVariation = "SmallButton";
		AddThemeFontSizeOverride("font_size", 14);
		MouseDefaultCursorShape = CursorShape.PointingHand;
		TooltipText = tip;
		CustomMinimumSize = new Vector2(40, 0);
		Pressed += () =>
		{
			if (Playing) Stop();
			else Start();
		};
		SetPlaying(_activeKind == kind && Sfx.Previewing && Sfx.IsPlaying(music));
	}

	bool Playing => _activeKind == _kind && Sfx.Previewing && Sfx.IsPlaying(_music);

	public void Start()
	{
		Sfx.StopAll();
		_play();
		Sfx.Previewing = true;
		_activeKind = _kind;
		SetPlaying(true);
	}

	void Stop()
	{
		Sfx.StopAll();
		SetPlaying(false);
	}

	void SetPlaying(bool playing)
	{
		Text = playing ? "■" : "▶";
		SetProcess(playing);
	}

	// (back to ▶ once it has finished, or another preview took over)
	public override void _Process(double delta)
	{
		if (!Playing) SetPlaying(false);
	}
}
