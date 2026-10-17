using System;
using Godot;
using QuizNight.Shared;
using QuizNight.Storage;

namespace QuizNight.Ui.Screens;

public partial class QuizListScreen : Control
{
	readonly VBoxContainer _list = U.V(12);
	readonly Label _error = U.Error();

	public QuizListScreen()
	{
		SetAnchorsPreset(LayoutPreset.FullRect);
		var head = U.H(10,
			U.Heading("My quizzes", 34).Grow(),
			U.Btn("Open folder", "", () => OS.ShellOpen(QuizStore.Folder)).Tip("Where your quiz files are saved"),
			U.Btn("Import file", "", Import),
			U.Btn("New quiz", "AccentButton", Create));
		var page = U.V(14, U.Link("← Home", () => App.I.GoHome()).With(b => b.SizeFlagsHorizontal = SizeFlags.ShrinkBegin), head, _error, _list);
		AddChild(U.Page(page, 980));
		Refresh();
	}

	void Refresh()
	{
		_list.Clear();
		var quizzes = QuizStore.List();
		if (quizzes.Count == 0)
		{
			_list.AddChild(U.Card(8,
				U.Heading("No quizzes yet", 22).With(l => l.HorizontalAlignment = HorizontalAlignment.Center),
				U.Muted("Create your first quiz night, or import a .quiznight.json file — exported from the old Quiznight website or sent by a friend.", 15, true, HorizontalAlignment.Center)));
			return;
		}
		var grid = new GridContainer { Columns = 2 };
		grid.AddThemeConstantOverride("h_separation", 14);
		grid.AddThemeConstantOverride("v_separation", 14);
		foreach (var quiz in quizzes) grid.AddChild(Card(quiz));
		_list.AddChild(grid);
	}

	Control Card(Quiz quiz)
	{
		var ready = QuizRules.QuizIssues(quiz).Count == 0;
		var edited = DateTimeOffset.FromUnixTimeMilliseconds(quiz.UpdatedAt).LocalDateTime.ToShortDateString();
		var title = U.Btn(quiz.Title == "" ? "Untitled quiz" : quiz.Title, "LinkButton2", () => App.I.GoQuiz(quiz.Id), 22);
		title.AddThemeColorOverride("font_color", C.Text);
		title.Alignment = HorizontalAlignment.Left;
		title.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
		title.ClipText = true;
		var info = U.H(8, U.Muted(Lang.F("{0} · edited {1}", U.Plural(quiz.Questions.Count, "question", "questions"), edited), 13, false));
		if (!ready) info.AddChild(U.Pill("Not ready yet", C.Accent));
		var host = U.Btn("Open lobby", "AccentButton", () => App.I.HostQuiz(quiz), 14);
		host.Disabled = !ready;
		var actions = U.H(6,
			U.Link("Delete", () => Dialogs.Confirm(this, Lang.F("Delete \"{0}\"? This can't be undone — export it first if you want a backup.", quiz.Title), "Delete", () =>
			{
				QuizStore.Delete(quiz.Id);
				Refresh();
			}), 14),
			U.Fill(),
			U.Small("Export", () => Export(quiz)),
			host);
		return U.Card(6, title, info, U.Gap(4), actions).Grow();
	}

	void Create()
	{
		var quiz = QuizStore.Create();
		App.I.GoQuiz(quiz.Id);
	}

	void Import() => Dialogs.OpenFile(this, "Import a quiz file", QuizStore.FileFilters, path =>
	{
		try
		{
			QuizStore.Import(path);
			_error.Visible = false;
			Refresh();
		}
		catch (UserError e)
		{
			_error.Text = e.Message;
			_error.Visible = true;
		}
	});

	public void Export(Quiz quiz) => ExportQuiz(this, quiz, _error);

	public static void ExportQuiz(Node owner, Quiz quiz, Label? error) =>
		Dialogs.SaveFile(owner, "Export quiz", QuizStore.SuggestedFileName(quiz), QuizStore.FileFilters, path =>
		{
			try
			{
				QuizStore.Export(quiz, path.EndsWith(".json") ? path : path + QuizStore.Extension);
			}
			catch (UserError e)
			{
				if (error == null) return;
				error.Text = e.Message;
				error.Visible = true;
			}
		});

	public void OnFilesDropped(string[] files)
	{
		foreach (var f in files)
		{
			if (!f.EndsWith(".json")) continue;
			try
			{
				QuizStore.Import(f);
			}
			catch (UserError e)
			{
				_error.Text = e.Message;
				_error.Visible = true;
			}
		}
		Refresh();
	}
}
