// Step by step questions: the hints as cards, the hardest first. Hints still to come show their back; the one the host
// just flipped turns over like a card.

using System;
using Godot;
using QuizNight.Shared;

namespace QuizNight.Ui.Screens;

public partial class HintCard : PanelContainer
{
	readonly Control _front;
	readonly Control _back;
	readonly int _size;

	public HintCard() : this(1, null, 1, false) { }

	/// <param name="text">The hint, or null while it's still face down.</param>
	/// <param name="flip">Turn it over now (it was just flipped).</param>
	public HintCard(int number, string? text, int worth, bool flip, int size = 22)
	{
		_size = size;
		SizeFlagsHorizontal = SizeFlags.ExpandFill;
		MouseFilter = MouseFilterEnum.Ignore;
		var badge = U.Text(number.ToString(), size, C.Bg, Fonts.Black, false, HorizontalAlignment.Center).MinSize(size * 1.8f, 0);
		var badgePanel = U.Panel(badge, "Clear").With(p => p.AddThemeStyleboxOverride("panel", UiTheme.Box(C.Accent, 999, null, 0, 6, 2)));
		badgePanel.SizeFlagsVertical = SizeFlags.ShrinkCenter;
		var points = U.Mono(worth == 1 ? Lang.T("1 pt") : Lang.F("{0} pts", worth), 14, C.Muted);
		points.SizeFlagsVertical = SizeFlags.ShrinkCenter;
		_front = GameParts.Row(14, badgePanel, U.Text(text ?? "", size, C.Text, Fonts.Bold, true).Raw().Grow(), points);
		var backBadge = U.Panel(U.Text(number.ToString(), size, C.Muted, Fonts.Black, false, HorizontalAlignment.Center).MinSize(size * 1.8f, 0), "Clear")
			.With(p => p.AddThemeStyleboxOverride("panel", UiTheme.Box(Colors.Transparent, 999, C.Border, 2, 6, 2)));
		backBadge.SizeFlagsVertical = SizeFlags.ShrinkCenter;
		_back = GameParts.Row(14, backBadge, U.Text("? ? ?", size, new Color(C.Muted, 0.5f), Fonts.Black).Grow(), U.Mono(worth == 1 ? Lang.T("1 pt") : Lang.F("{0} pts", worth), 14, new Color(C.Muted, 0.6f)));
		var stack = new MarginContainer();
		stack.AddChild(_front);
		stack.AddChild(_back);
		AddChild(stack);
		var shown = text != null;
		Face(shown && !flip);
		if (shown && flip) Ready += () => Callable.From(Flip).CallDeferred();
	}

	void Face(bool front)
	{
		_front.Visible = front;
		_back.Visible = !front;
		// (padding follows the text size: the players' cards are smaller, so the buzzer fits below them)
		var padY = Mathf.Max(4, _size * 0.4f);
		AddThemeStyleboxOverride("panel", front
			? UiTheme.Box(C.Panel2, 12, C.Accent, 2, 16, padY)
			: UiTheme.Box(new Color(C.Panel, 0.6f), 12, C.Border, 1, 16, padY));
	}

	/// <summary>Turns over like a card: the back folds away, the hint unfolds.</summary>
	void Flip()
	{
		if (!IsInsideTree()) return;
		PivotOffset = Size / 2;
		var tween = CreateTween();
		tween.TweenProperty(this, "scale:y", 0f, 0.18).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.In);
		tween.TweenCallback(Callable.From(() => Face(true)));
		tween.TweenProperty(this, "scale:y", 1f, 0.28).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
	}
}

public static class StepsParts
{
	/// <summary>All the hint cards: flipped ones face up, the rest face down. flipLast: the newest one turns over now.</summary>
	public static Control Cards(PublicQuestion q, bool flipLast, int size = 22)
	{
		var box = U.V(size >= 20 ? 8 : 5);
		var count = q.HintCount ?? 0;
		var shown = q.Hints?.Count ?? 0;
		for (var i = 0; i < count; i++)
			box.AddChild(new HintCard(i + 1, i < shown ? q.Hints![i] : null, count - i, flipLast && i == shown - 1, size));
		return box;
	}

	/// <summary>"Right now: 4 points" under the cards.</summary>
	public static Control Worth(PublicQuestion q, int size = 17) =>
		U.Text(q.WorthNow == 1 ? Lang.T("Right now: 1 point") : Lang.F("Right now: {0} points", q.WorthNow ?? 0), size, C.Accent, Fonts.Bold).Center();
}

/// <summary>Remembers which hint a screen has shown, so only a newly flipped one turns over.</summary>
public sealed class HintTracker
{
	int _index = -1;
	int _shown;

	/// <summary>True if the newest hint of this question wasn't shown on this screen before.</summary>
	public bool JustFlipped(GameView game)
	{
		var shown = game.Question.Hints?.Count ?? 0;
		var flip = game.Index == _index && shown > _shown && game.Stage == Stage.Open;
		if (game.Index != _index || shown > _shown)
		{
			_index = game.Index;
			_shown = shown;
		}
		return flip;
	}
}
