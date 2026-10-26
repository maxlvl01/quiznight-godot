using System.Collections.Generic;
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
	readonly LineEdit _title;
	/// <summary>Ctrl+Z / Ctrl+Y: moving, deleting and renaming, back and forth (the questions themselves aren't copied).</summary>
	readonly EditHistory<(string Title, List<Question> Questions)> _history;

	public QuizOverviewScreen() : this(new Quiz()) { }

	public QuizOverviewScreen(Quiz quiz)
	{
		_quiz = quiz;
		SetAnchorsPreset(LayoutPreset.FullRect);

		// Saving rewrites the whole file (images included), so the title is saved when you leave the field.
		var title = _title = U.Input(quiz.Title, "Quiz title", t =>
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
		// (alphabetical by the name shown, so also in German)
		foreach (var type in QuestionType.All.OrderBy(t => Lang.T(QuestionType.Infos[t].Label), System.StringComparer.CurrentCultureIgnoreCase))
		{
			var info = QuestionType.Infos[type];
			// Layers: the clickable card, the name on top (it sets the height: long names wrap, e.g. in German), and the "?"
			// on the right, level with the name, which tells what the type does (hover) and its variants (click).
			var card = new MarginContainer { MouseFilter = MouseFilterEnum.Ignore }.Grow();
			var button = new Button { ThemeTypeVariation = "", MouseDefaultCursorShape = CursorShape.PointingHand, CustomMinimumSize = new Vector2(0, 76) };
			button.SetMeta(UiAudio.NoBounce, true);
			// (a key like every button, KeyBox: lighter under the mouse, pushed in when clicked)
			button.AddThemeStyleboxOverride("normal", new KeyBox(C.Panel2, 16, 16, 14, 3, 6, false));
			button.AddThemeStyleboxOverride("hover", new KeyBox(C.Panel2.Lightened(0.1f), 16, 16, 14, 3, 6, false));
			button.AddThemeStyleboxOverride("pressed", new KeyBox(C.Panel2.Darkened(0.05f), 16, 16, 14, 3, 6, true));
			button.AddThemeStyleboxOverride("hover_pressed", new KeyBox(C.Panel2, 16, 16, 14, 3, 6, true));
			button.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
			var t = type;
			button.Pressed += () => App.I.GoQuestion(_quiz.Id, null, t);
			card.AddChild(button);
			var inner = GameParts.Row(12, new TypeIcon(type, 28), U.Bold(info.Label, 17).Grow().With(l => l.AutowrapMode = TextServer.AutowrapMode.Word));
			// (the row and everything in it let clicks through to the card underneath)
			inner.MouseFilter = MouseFilterEnum.Ignore;
			foreach (var c in inner.GetChildren().OfType<Control>()) c.MouseFilter = MouseFilterEnum.Ignore;
			card.AddChild(U.Pad(inner, 18, 12, 44, 18).With(m => m.MouseFilter = MouseFilterEnum.Ignore));
			var corner = new Control { MouseFilter = MouseFilterEnum.Ignore };
			var help = U.Link("?", () => Dialogs.Info(this, TypeHelp(info), info.Label), 20);
			help.AnchorLeft = help.AnchorRight = 1;
			help.AnchorTop = help.AnchorBottom = 0.5f;
			(help.OffsetLeft, help.OffsetTop, help.OffsetRight, help.OffsetBottom) = (-40, -24, -12, 8);
			corner.AddChild(help);
			card.AddChild(corner);
			types.AddChild(card);
		}
		var main = U.V(10, GameParts.Row(12, U.Heading("Add a question", 30).Grow(), new UpdateBadge()), U.Muted("Pick a type, then fill in the blanks. No code needed. The ? on a type tells what it does.", 15), U.Gap(6), types);

		var layout = U.H(0, side, U.Pad(new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled }.With(s => s.AddChild(main.Grow())), 32, 28, 32, 28).Grow().GrowV());
		layout.SetAnchorsPreset(LayoutPreset.FullRect);
		AddChild(layout);
		RefreshList();
		_history = new(() => (_quiz.Title, _quiz.Questions.ToList()), state =>
		{
			_quiz.Title = state.Title;
			_quiz.Questions = state.Questions.ToList();
			_title.Text = state.Title;
			_titleDirty = false;
			Save();
			RefreshList();
		});
	}

	public bool Undo() => _history.Undo();

	public bool Redo() => _history.Redo();

	bool _titleDirty;

	void SaveTitle()
	{
		if (!_titleDirty) return;
		_titleDirty = false;
		Save();
		_history.Record();
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
		// (the number is a field: type another one to move the question there)
		var num = new LineEdit
		{
			Text = $"{i + 1}", Alignment = HorizontalAlignment.Center, MaxLength = 3, CustomMinimumSize = new Vector2(34, 0),
			SizeFlagsVertical = SizeFlags.ShrinkCenter, SelectAllOnFocus = true, TooltipText = Lang.T("Type a number to move the question there"),
		};
		foreach (var style in new[] { "normal", "read_only" }) num.AddThemeStyleboxOverride(style, UiTheme.Box(C.ForType(q.Type), 6, null, 0, 4, 2));
		num.AddThemeStyleboxOverride("focus", UiTheme.Box(C.ForType(q.Type), 6, C.Text, 2, 4, 2));
		num.AddThemeColorOverride("font_color", C.AccentInk);
		num.AddThemeColorOverride("caret_color", C.AccentInk);
		num.AddThemeFontOverride("font", Fonts.Bold);
		num.AddThemeFontSizeOverride("font_size", 13);
		num.AddThemeConstantOverride("minimum_character_width", 2);
		void Renumber()
		{
			if (!int.TryParse(num.Text.Trim(), out var to)) to = i + 1;
			to = Mathf.Clamp(to, 1, _quiz.Questions.Count) - 1;
			if (to != i) Callable.From(() => Move(i, to)).CallDeferred();
			else num.Text = $"{i + 1}";
		}
		num.TextSubmitted += _ => Renumber();
		num.FocusExited += Renumber;
		var prompt = U.Text(q.Prompt == "" ? "No question text" : q.Prompt, 14, q.Prompt == "" ? C.Muted : C.Text, Fonts.Bold);
		prompt.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
		prompt.ClipText = true;
		prompt.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		var sub = U.H(0, U.Muted(QuizRules.Summary(q), 12, false));
		if (QuizRules.MapNeedsCheck(q, _quiz.Maps)) sub.AddChild(U.Text(" · ⚠ map changed: check the spot", 12, C.Orange));
		else if (broken) sub.AddChild(U.Text(" · incomplete", 12, C.Accent));
		var text = U.V(0, prompt, sub).Grow();
		var grip = new GripIcon { CustomMinimumSize = new Vector2(12, 20), SizeFlagsVertical = SizeFlags.ShrinkCenter };
		// (click opens the question; drag it onto another row to move it there)
		var open = new QuestionRowButton(i, Move) { Flat = true, MouseDefaultCursorShape = CursorShape.PointingHand, TooltipText = Lang.T("Click to edit, drag to move") }.Grow();
		open.SetMeta(UiAudio.NoBounce, true);
		open.Pressed += () => App.I.GoQuestion(_quiz.Id, q.Id, null);
		open.Preview = () => U.Panel(U.Text(q.Prompt == "" ? Lang.T("No question text") : q.Prompt, 14, C.Text, Fonts.Bold).Raw(), "Card2")
			.With(p => p.AddThemeStyleboxOverride("panel", UiTheme.Box(C.Panel2, 10, C.Accent, 2, 12, 8)));
		var content = U.H(10, text, grip);
		content.MouseFilter = MouseFilterEnum.Ignore;
		foreach (var c in content.GetChildren().OfType<Control>()) c.MouseFilter = MouseFilterEnum.Ignore;
		foreach (var c in text.GetChildren().OfType<Control>()) c.MouseFilter = MouseFilterEnum.Ignore;
		open.AddChild(content.With(c => c.SetAnchorsPreset(LayoutPreset.FullRect)));
		open.CustomMinimumSize = new Vector2(0, 46);
		var del = U.Small("×", () => Dialogs.Confirm(this, "Delete this question?", "Delete", () =>
		{
			_quiz.Questions.RemoveAt(i);
			Save();
			_history.Record();
			RefreshList();
		})).Tip("Delete");
		return U.Panel(U.H(10, num, open, del), "Card2").With(p => p.AddThemeStyleboxOverride("panel", UiTheme.Box(C.Panel2, 10, C.Border, 1, 8, 4)));
	}

	void Move(int from, int to)
	{
		if (from == to || from < 0 || to < 0 || from >= _quiz.Questions.Count || to >= _quiz.Questions.Count) return;
		var item = _quiz.Questions[from];
		_quiz.Questions.RemoveAt(from);
		_quiz.Questions.Insert(to, item);
		Save();
		_history.Record();
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

/// <summary>
/// A question row in the quiz overview: clicking opens it, dragging it onto another row moves it there (a line shows
/// where it lands: above the row's middle before it, below after it).
/// </summary>
public partial class QuestionRowButton : Button
{
	readonly int _index;
	readonly System.Action<int, int> _move;
	public System.Func<Control>? Preview;
	/// <summary>Where a dragged row would land while one is over this row: -1 above, 1 below, 0 none.</summary>
	int _dropSide;

	public QuestionRowButton() : this(0, (_, _) => { }) { }

	public QuestionRowButton(int index, System.Action<int, int> move)
	{
		_index = index;
		_move = move;
		MouseExited += () => SetDropSide(0);
	}

	const string DragKind = "quiznight_question_row";

	public override Variant _GetDragData(Vector2 atPosition)
	{
		if (Preview?.Invoke() is { } preview)
		{
			preview.Modulate = new Color(1, 1, 1, 0.9f);
			preview.CustomMinimumSize = new Vector2(Mathf.Min(Size.X, 420), 0);
			SetDragPreview(preview);
		}
		return new Godot.Collections.Dictionary { ["kind"] = DragKind, ["index"] = _index };
	}

	public override bool _CanDropData(Vector2 atPosition, Variant data)
	{
		if (data.VariantType != Variant.Type.Dictionary || data.AsGodotDictionary() is not { } d || !d.ContainsKey("kind") || d["kind"].AsString() != DragKind) return false;
		SetDropSide(atPosition.Y < Size.Y / 2 ? -1 : 1);
		return true;
	}

	public override void _DropData(Vector2 atPosition, Variant data)
	{
		var from = data.AsGodotDictionary()["index"].AsInt32();
		var below = atPosition.Y >= Size.Y / 2;
		SetDropSide(0);
		// (the slot between rows: dropping below row 3 lands after it; taking the row out first shifts the later ones up)
		var to = _index + (below ? 1 : 0);
		if (from < to) to--;
		_move(from, to);
	}

	public override void _Notification(int what)
	{
		if (what == NotificationDragEnd) SetDropSide(0);
	}

	void SetDropSide(int side)
	{
		if (_dropSide == side) return;
		_dropSide = side;
		QueueRedraw();
	}

	public override void _Draw()
	{
		if (_dropSide == 0) return;
		var y = _dropSide < 0 ? -5f : Size.Y + 5f;
		DrawLine(new Vector2(-40, y), new Vector2(Size.X + 40, y), C.Accent, 4);
	}
}

/// <summary>Six dots: the handle that says "you can drag this row".</summary>
public partial class GripIcon : Control
{
	public GripIcon() => MouseFilter = MouseFilterEnum.Ignore;

	public override void _Draw()
	{
		for (var row = 0; row < 3; row++)
			for (var col = 0; col < 2; col++)
				DrawCircle(new Vector2(Size.X * (0.25f + col * 0.5f), Size.Y * (0.2f + row * 0.3f)), 1.8f, C.Muted);
	}
}
