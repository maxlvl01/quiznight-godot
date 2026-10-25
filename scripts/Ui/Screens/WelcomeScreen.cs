// The very first start on a PC (no settings saved yet, so never after an update): before the splash and the home screen,
// the accessibility settings (colour vision, the dyslexia-friendly font) and the language, so nobody has to find
// Settings first to be able to read the game. Everything applies and saves at once; "Let's go!" carries on.

using System;
using Godot;
using QuizNight.Shared;
using QuizNight.Storage;

namespace QuizNight.Ui.Screens;

public partial class WelcomeScreen : Control
{
	public WelcomeScreen() : this(() => { }) { }

	public WelcomeScreen(Action done)
	{
		SetAnchorsPreset(LayoutPreset.FullRect);
		var dyslexic = new CheckBox
		{
			Text = Lang.T("Dyslexia-friendly font"), ButtonPressed = Settings.DyslexicFont && Fonts.DyslexicAvailable,
			Disabled = !Fonts.DyslexicAvailable, MouseDefaultCursorShape = CursorShape.PointingHand,
		};
		dyslexic.Toggled += on => Callable.From(() => App.I.UseDyslexicFont(on)).CallDeferred();
		var go = U.Btn("Let's go!", "AccentButton", () =>
		{
			Settings.AccessibilityAsked = true;
			done();
		}, 20);
		go.SizeFlagsHorizontal = SizeFlags.ShrinkEnd;

		var page = U.V(18,
			U.Heading("Welcome to Quiznight?", 40),
			U.Text("Before the show starts: does anything make the game easier for you to see or read?", 17, C.Text, Fonts.Bold, true),
			U.Card(14,
				U.Eyebrow("Language"),
				U.Segmented(Lang.All, Settings.Language, code =>
				{
					Settings.Language = code;
					Lang.Use(code);
					// (built again so text made from parts switches too)
					Callable.From(App.I.ShowWelcomeAgain).CallDeferred();
				}),
				U.Gap(4),
				U.Eyebrow("Accessibility"),
				U.Field("Colour vision", U.Select(ColourFilter.Modes, Settings.ColourVision, App.I.UseColourVision),
					"Shifts the colours of the whole game so the ones you can't tell apart become different: right and wrong, the A–D answers, medals and player colours."),
				dyslexic,
				U.Muted(Fonts.DyslexicAvailable ? "All text in OpenDyslexic: letters with heavy bottoms, so they don't flip or swim."
					: "The dyslexia-friendly font isn't installed with this version yet.", 12)),
			U.Muted("You can change all of this any time in Settings. This only shows on the very first start.", 13),
			go);
		AddChild(U.Page(page, 720, 60));
	}
}
