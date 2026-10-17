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
		var select = U.Select(Sfx.BuzzerChoices(), current, id =>
		{
			current = id;
			changed(id);
			Sfx.PlayBuzzer(id);
		}).MinSize(220);
		select.SizeFlagsHorizontal = withLabelAbove ? Control.SizeFlags.ExpandFill : Control.SizeFlags.ShrinkEnd;
		var play = U.Small("▶", () => Sfx.PlayBuzzer(current)).Tip("Play the buzzer sound");
		return withLabelAbove ? U.H(8, select, play) : U.H(8, U.Text("Buzzer sound", 15).Grow(), select, play);
	}
}
