using System.Linq;
using Godot;
using QuizNight.Shared;

namespace QuizNight.Ui.Screens;

/// <summary>
/// The host's cheat sheet in its own window: the current question and its answer, so the host can read
/// a hidden question out loud. If only the game window is shared in Discord, viewers don't see this one.
/// </summary>
public partial class HostNotes : Window
{
	static HostNotes? _open;
	static GameView? _last;
	readonly VBoxContainer _body = U.V(10);

	public HostNotes()
	{
		Title = Lang.T("Quiznight – host notes");
		Size = new Vector2I(560, 420);
		MinSize = new Vector2I(380, 260);
		// Its own window on the desktop, not drawn inside the game's.
		ForceNative = true;
		Theme = App.I.Theme;
		var bg = new Panel();
		bg.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		bg.AddThemeStyleboxOverride("panel", UiTheme.Box(C.Bg, 0, null, 0, 0, 0));
		AddChild(bg);
		var scroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
		scroll.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		scroll.AddChild(U.Pad(_body.Grow(), 20, 18, 20, 18).Grow());
		AddChild(scroll);
		CloseRequested += () =>
		{
			_open = null;
			QueueFree();
		};
	}

	public static void Open()
	{
		if (_open != null && IsInstanceValid(_open))
		{
			_open.GrabFocus();
			return;
		}
		_open = new HostNotes();
		App.I.AddChild(_open);
		_open.PopupCentered();
		_open.Fill();
	}

	/// <summary>Keeps the notes on the current question (called on every state update).</summary>
	public static void Update(GameView game)
	{
		_last = game;
		if (_open != null && IsInstanceValid(_open)) _open.Fill();
	}

	/// <summary>Closes the window when the game ends.</summary>
	public static void Close()
	{
		if (_open != null && IsInstanceValid(_open)) _open.QueueFree();
		_open = null;
	}

	void Fill()
	{
		_body.Clear();
		if (_last is not { } game) return;
		_body.AddChild(U.Eyebrow(Lang.F("Question {0} / {1}", game.Index + 1, game.Total) + " · " + Lang.T(QuestionType.Infos[game.Question.Type].Label)));
		_body.AddChild(U.Text(game.Question.Prompt, 24, C.Text, Fonts.Black, true).Raw());
		var lines = game.Question.Choices?.Select((c, i) => c == null ? null : $"{QuizRules.ChoiceLetters[i]}: {c}").OfType<string>()
			?? game.Question.Items?.AsEnumerable() ?? Enumerable.Empty<string>();
		foreach (var line in lines) _body.AddChild(U.Text("• " + line, 17).Raw());
		_body.AddChild(new HSeparator());
		_body.AddChild(U.Eyebrow("Answer"));
		_body.AddChild(U.Text(game.HostAnswer ?? "", 20, C.Accent, Fonts.Bold, true).Raw());
		_body.AddChild(U.Muted(game.Shown ? "Shown to everyone." : "Players don't see it until you press Show question.", 13));
	}
}
