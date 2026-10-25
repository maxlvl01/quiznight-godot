using System.Linq;
using Godot;
using QuizNight.Shared;
using QuizNight.Storage;

namespace QuizNight.Ui.Screens;

/// <summary>One quiz: title, its questions (reorder, delete) and "add a question".</summary>
public partial class QuizOverviewScreen : Control
{
	readonly Quiz _quiz;
	readonly VBoxContainer _questions = U.V(6);
	readonly VBoxContainer _foot = U.V(8);
	readonly Label _error = U.Error();

	public QuizOverviewScreen() : this(new Quiz()) { }

	public QuizOverviewScreen(Quiz quiz)
	{
		_quiz = quiz;
		SetAnchorsPreset(LayoutPreset.FullRect);

		// Saving rewrites the whole file (images included), so the title is saved when you leave the field.
		var title = U.Input(quiz.Title, "Quiz title", t =>
		{
			_quiz.Title = t;
			_titleDirty = true;
			RefreshFoot();
		}, QuestionLimits.TitleMaxLength);
		title.FocusExited += SaveTitle;
		title.TextSubmitted += _ => SaveTitle();
		title.AddThemeFontOverride("font", Fonts.Black);
		title.AddThemeFontSizeOverride("font_size", 22);

		var sidebar = U.V(12,
			U.Link("← My quizzes", App.I.GoQuizzes, 14).With(b => b.SizeFlagsHorizontal = SizeFlags.ShrinkBegin),
			U.Eyebrow("Quiz"),
			title,
			new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled }.With(s => s.AddChild(_questions.Grow())).GrowV(),
			_error,
			_foot);
		var side = U.Panel(sidebar).MinSize(380);
		side.AddThemeStyleboxOverride("panel", UiTheme.Box(C.Panel, 0, null, 0, 18, 18));

		var types = new GridContainer { Columns = 3 };
		types.AddThemeConstantOverride("h_separation", 12);
		types.AddThemeConstantOverride("v_separation", 12);
		foreach (var type in QuestionType.All)
		{
			var info = QuestionType.Infos[type];
			// Layers: the clickable card, the name on top (it sets the height: long names wrap, e.g. in German), and the "?"
			// on the right, level with the name, which tells what the type does (hover) and its variants (click).
			var card = new MarginContainer { MouseFilter = MouseFilterEnum.Ignore }.Grow();
			var button = new Button { ThemeTypeVariation = "", MouseDefaultCursorShape = CursorShape.PointingHand, CustomMinimumSize = new Vector2(0, 76) };
			button.AddThemeStyleboxOverride("normal", UiTheme.Box(C.Panel, 14, C.Border, 1, 16, 14));
			button.AddThemeStyleboxOverride("hover", UiTheme.Box(C.Panel2, 14, C.ForType(type), 1, 16, 14));
			button.AddThemeStyleboxOverride("pressed", UiTheme.Box(C.Panel2, 14, C.ForType(type), 1, 16, 14));
			var t = type;
			button.Pressed += () => App.I.GoQuestion(_quiz.Id, null, t);
			card.AddChild(button);
			var inner = GameParts.Row(12, new TypeIcon(type, 28), U.Bold(info.Label, 17).Grow().With(l => l.AutowrapMode = TextServer.AutowrapMode.Word));
			// (the row and everything in it let clicks through to the card underneath)
			inner.MouseFilter = MouseFilterEnum.Ignore;
			foreach (var c in inner.GetChildren().OfType<Control>()) c.MouseFilter = MouseFilterEnum.Ignore;
			card.AddChild(U.Pad(inner, 16, 14, 44, 14).With(m => m.MouseFilter = MouseFilterEnum.Ignore));
			var corner = new Control { MouseFilter = MouseFilterEnum.Ignore };
			var help = U.Link("?", () => Dialogs.Info(this, TypeHelp(info), info.Label), 20);
			help.AnchorLeft = help.AnchorRight = 1;
			help.AnchorTop = help.AnchorBottom = 0.5f;
			(help.OffsetLeft, help.OffsetTop, help.OffsetRight, help.OffsetBottom) = (-38, -21, -10, 11);
			corner.AddChild(help);
			card.AddChild(corner);
			types.AddChild(card);
		}
		var main = U.V(10, GameParts.Row(12, U.Heading("Add a question", 30).Grow(), new UpdateBadge()), U.Muted("Pick a type, then fill in the blanks. No code needed. The ? on a type tells what it does.", 15), U.Gap(6), types);

		var layout = U.H(0, side, U.Pad(new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled }.With(s => s.AddChild(main.Grow())), 32, 28, 32, 28).Grow().GrowV());
		layout.SetAnchorsPreset(LayoutPreset.FullRect);
		AddChild(layout);
		RefreshList();
	}

	bool _titleDirty;

	void SaveTitle()
	{
		if (!_titleDirty) return;
		_titleDirty = false;
		Save();
	}

	public override void _ExitTree() => SaveTitle();

	void Save()
	{
		try
		{
			QuizStore.Save(_quiz);
			_error.Visible = false;
		}
		catch (UserError e)
		{
			_error.Text = e.Message;
			_error.Visible = true;
		}
	}

	void RefreshList()
	{
		_questions.Clear();
		if (_quiz.Questions.Count == 0) _questions.AddChild(U.Muted("No questions yet — pick a type on the right.", 13));
		for (var i = 0; i < _quiz.Questions.Count; i++) _questions.AddChild(Row(i));
		RefreshFoot();
	}

	Control Row(int i)
	{
		var q = _quiz.Questions[i];
		var broken = QuizRules.QuestionIssues(q, _quiz.Maps).Count > 0;
		var num = U.Panel(U.Text($"{i + 1}", 13, C.AccentInk, Fonts.Bold, false, HorizontalAlignment.Center), "Clear")
			.With(p => p.AddThemeStyleboxOverride("panel", UiTheme.Box(C.ForType(q.Type), 6, null, 0, 6, 2))).MinSize(30);
		num.SizeFlagsVertical = SizeFlags.ShrinkCenter;
		var prompt = U.Text(q.Prompt == "" ? "No question text" : q.Prompt, 14, q.Prompt == "" ? C.Muted : C.Text, Fonts.Bold);
		prompt.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
		prompt.ClipText = true;
		prompt.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		var sub = U.H(0, U.Muted(QuizRules.Summary(q), 12, false));
		if (broken) sub.AddChild(U.Text(" · incomplete", 12, C.Accent));
		var text = U.V(0, prompt, sub).Grow();
		var open = new Button { Flat = true, MouseDefaultCursorShape = CursorShape.PointingHand }.Grow();
		open.Pressed += () => App.I.GoQuestion(_quiz.Id, q.Id, null);
		var content = U.H(10, num, text);
		content.MouseFilter = MouseFilterEnum.Ignore;
		foreach (var c in content.GetChildren().OfType<Control>()) c.MouseFilter = MouseFilterEnum.Ignore;
		foreach (var c in text.GetChildren().OfType<Control>()) c.MouseFilter = MouseFilterEnum.Ignore;
		open.AddChild(content.With(c => c.SetAnchorsPreset(LayoutPreset.FullRect)));
		open.CustomMinimumSize = new Vector2(0, 46);
		var up = U.Small("↑", () => Move(i, i - 1)).Tip("Move up");
		up.Disabled = i == 0;
		var down = U.Small("↓", () => Move(i, i + 1)).Tip("Move down");
		down.Disabled = i == _quiz.Questions.Count - 1;
		var del = U.Small("×", () => Dialogs.Confirm(this, "Delete this question?", "Delete", () =>
		{
			_quiz.Questions.RemoveAt(i);
			Save();
			RefreshList();
		})).Tip("Delete");
		return U.Panel(U.H(4, open, up, down, del), "Card2").With(p => p.AddThemeStyleboxOverride("panel", UiTheme.Box(C.Panel2, 10, C.Border, 1, 8, 4)));
	}

	void Move(int from, int to)
	{
		var item = _quiz.Questions[from];
		_quiz.Questions.RemoveAt(from);
		_quiz.Questions.Insert(to, item);
		Save();
		RefreshList();
	}

	void RefreshFoot()
	{
		_foot.Clear();
		var issues = QuizRules.QuizIssues(_quiz);
		if (issues.Count > 0 && _quiz.Questions.Count > 0)
			_foot.AddChild(U.Muted(Lang.F("{0} to fix before hosting.", U.Plural(issues.Count, "thing", "things")), 13));
		var host = U.Btn("Open lobby", "AccentButton", () => App.I.HostQuiz(_quiz)).Grow().Tip(issues.FirstOrDefault());
		host.Disabled = issues.Count > 0;
		_foot.AddChild(U.H(8, U.Btn("Copy share code", "", () => ShareInfo.Copy(this, _quiz)), U.Btn("Export file", "",() => QuizListScreen.ExportQuiz(this, _quiz, _error)), host));
	}

	/// <summary>A type's help, translated: what it does, then its variants as a list.</summary>
	static string TypeHelp(QuestionType.Info info) =>
		Lang.T(info.Intro) + "\n\n" + string.Join("\n\n", (info.Details ?? System.Array.Empty<string>()).Select(d => "• " + Lang.T(d)));
}
