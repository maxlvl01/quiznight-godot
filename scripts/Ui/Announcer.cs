// Screen reader support. Godot reads buttons, labels and inputs on its own (AccessKit, on whenever a screen reader runs);
// this adds what a reader can't see on screen: spoken announcements of what just happened in a game (a "live region":
// an invisible label the reader speaks whenever its text changes), and whether the player asked Windows for less motion.

using System;
using Godot;
using QuizNight.Shared;

namespace QuizNight.Ui;

public partial class Announcer : Label
{
	string _last = "";

	public Announcer()
	{
		// (invisible on screen, but in the accessibility tree; the reader speaks every new text)
		Modulate = new Color(1, 1, 1, 0);
		MouseFilter = MouseFilterEnum.Ignore;
		Size = new Vector2(1, 1);
		AccessibilityLive = AccessibilityServer.AccessibilityLiveMode.Polite;
		AutoTranslateMode = AutoTranslateModeEnum.Disabled;
	}

	/// <summary>Says this (once: the same text twice in a row isn't repeated).</summary>
	public void Say(string text)
	{
		if (text == "" || text == _last) return;
		_last = text;
		Text = text;
		AccessibilityName = text;
	}

	/// <summary>Whether Windows asks apps for less motion (then the splash, spotlights, typing logo and bounces keep still).</summary>
	public static bool ReduceMotion
	{
		get
		{
			try
			{
				return DisplayServer.GetName() != "headless" && DisplayServer.AccessibilityShouldReduceAnimation() != 0;
			}
			catch (Exception)
			{
				return false;
			}
		}
	}

	/// <summary>
	/// A player's game, as words: called on every state update, it announces what changed: a new question (its number,
	/// type and text once shown), buzzers opening, your turn, and the answer and your points at the reveal.
	/// </summary>
	public void Follow(RoomState room, string me)
	{
		if (room.Game is not { } game) return;
		var q = game.Question;
		var n = Lang.F("Question {0} of {1}", game.Index + 1, game.Total);
		if (game.Stage == Stage.Reveal && game.Reveal is { } reveal)
		{
			var mine = reveal.Entries.Find(e => e.PlayerId == me);
			Say($"{n}. {Lang.T("The answer")}: {reveal.AnswerText}. " + (mine == null ? "" : Lang.F("You got {0} points. You have {1}.", mine.Points, room.Score(me))));
			return;
		}
		if (!game.Shown)
		{
			Say($"{n}: {Lang.T(QuestionType.Infos.TryGetValue(q.Type, out var info) ? info.Label : q.Type)}. {Lang.T("Get ready…")}");
			return;
		}
		var answering = game.Buzzes.Find(b => b.Status == BuzzStatus.Answering);
		if (answering?.PlayerId == me) Say(Lang.T("Your turn! Say your answer."));
		else if (q.Buzzer && game.BuzzersOpen && answering == null) Say($"{q.Prompt}. {Lang.T("Buzzers are open: press Space to buzz in.")}");
		else Say($"{n}: {q.Prompt}");
	}
}
