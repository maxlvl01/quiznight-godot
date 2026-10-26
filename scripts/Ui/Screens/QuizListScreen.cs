using System;
using System.Linq;
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
			new UpdateBadge(),
			U.Btn("Open folder", "", () => OS.ShellOpen(QuizStore.Folder)).Tip("Where your quiz files are saved"),
			U.Btn("Import file", "", Import),
			U.Btn("Paste share code", "", PasteCode).Tip("Imports the share code you copied"),
			U.Btn("New quiz", "AccentButton", Create));
		var page = U.V(14, U.Link("← Home", () => App.I.GoHome()).With(b => b.SizeFlagsHorizontal = SizeFlags.ShrinkBegin), head, _error, _list);
		AddChild(U.Page(page, 980, 20));
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
		var edited = DateTimeOffset.FromUnixTimeMilliseconds(quiz.UpdatedAt).LocalDateTime.ToString("dd.MM.yyyy", System.Globalization.CultureInfo.InvariantCulture);
		var title = U.Btn(quiz.Title == "" ? "Untitled quiz" : quiz.Title, "LinkButton2", () => App.I.GoQuiz(quiz.Id), 22);
		title.AddThemeColorOverride("font_color", C.Text);
		title.Alignment = HorizontalAlignment.Left;
		title.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
		title.ClipText = true;
		var info = U.H(8, U.Muted(Lang.F("{0} · edited {1}", U.Plural(quiz.Questions.Count, "question", "questions"), edited), 13, false));
		if (quiz.Questions.Any(q => QuizRules.MapNeedsCheck(q, quiz.Maps))) info.AddChild(U.Pill("⚠ A map changed: check", C.Orange));
		else if (!ready) info.AddChild(U.Pill("Not ready yet", C.Accent));
		var host = U.Btn("Open lobby", "AccentButton", () => App.I.HostQuiz(quiz), 14);
		host.Disabled = !ready;
		var actions = U.H(6,
			U.Fill(),
			U.Small("Copy share code", () => ShareInfo.Copy(this, quiz)),
			U.Small("Export", () => Export(quiz)),
			host);
		// (the trash can in the top-right corner, away from the buttons you use all the time)
		var top = GameParts.Row(10, title.Grow(), TrashButton(quiz));
		return U.Card(6, top, info, U.Gap(4), actions).Grow();
	}

	/// <summary>The trash can: deletes the quiz, after asking (unless "Don't ask me again" was ticked once).</summary>
	Button TrashButton(Quiz quiz)
	{
		void Delete()
		{
			QuizStore.Delete(quiz.Id);
			Refresh();
		}
		var button = U.Small("", () =>
		{
			if (!Settings.AskBeforeDeletingQuiz) Delete();
			else Dialogs.Confirm(this, Lang.F("Delete \"{0}\"? This can't be undone — export it first if you want a backup.", quiz.Title), "Delete", Delete,
				dontAskAgain: () => Settings.AskBeforeDeletingQuiz = false);
		});
		// Just the red can, no box. Under the mouse it grows, lights up and says "Delete?".
		foreach (var state in new[] { "normal", "hover", "pressed", "focus" }) button.AddThemeStyleboxOverride(state, new StyleBoxEmpty());
		var icon = new TrashIcon(new Color(C.Coral, 0.7f), 22);
		var ask = U.Text("Delete?", 15, C.Coral, Fonts.Bold).With(l => l.Visible = false);
		var row = GameParts.Row(6, ask, icon);
		row.Alignment = BoxContainer.AlignmentMode.End;
		row.SetAnchorsPreset(LayoutPreset.FullRect);
		row.MouseFilter = MouseFilterEnum.Ignore;
		foreach (var c in row.GetChildren().OfType<Control>()) c.MouseFilter = MouseFilterEnum.Ignore;
		button.AddChild(row);
		void Fit() => button.CustomMinimumSize = row.GetCombinedMinimumSize().Max(new Vector2(34, 34));
		row.MinimumSizeChanged += Fit;
		Fit();
		Tween? grow = null;
		void Hover(bool on)
		{
			ask.Visible = on;
			icon.Color = on ? C.Coral.Lightened(0.25f) : new Color(C.Coral, 0.7f);
			grow?.Kill();
			grow = button.CreateTween();
			grow.TweenProperty(icon, "custom_minimum_size", new Vector2(on ? 28 : 22, on ? 28 : 22), 0.12);
		}
		button.MouseEntered += () => Hover(true);
		button.MouseExited += () => Hover(false);
		return button;
	}

	void Create()
	{
		var quiz = QuizStore.Create();
		App.I.GoQuiz(quiz.Id);
	}

	async void PasteCode()
	{
		var text = DisplayServer.ClipboardGet();
		if (!ShareCode.Looks(text))
		{
			ShowError(Lang.T("Copy a share code first (it looks like QN-7K4P-9XZ2), then press Paste share code."));
			return;
		}
		try
		{
			Quiz quiz;
			if (ShareCode.ShortKey(text) is { } key)
			{
				var wait = Dialogs.Wait(this, "Looking up the share code…", "Share code");
				try
				{
					quiz = QuizStore.SaveImported(await QuizShare.Download(key));
				}
				finally
				{
					Dialogs.Close(wait);
				}
				if (!IsInstanceValid(this)) return;
			}
			else quiz = QuizStore.ImportCode(text);
			_error.Visible = false;
			Refresh();
			ShareInfo.Imported(this, quiz);
		}
		catch (UserError e)
		{
			ShowError(e.Message);
		}
	}

	void ShowError(string text)
	{
		_error.Text = text;
		_error.Visible = true;
	}

	void Import() => Dialogs.OpenFile(this, "Import a quiz file", QuizStore.ImportFilters, path =>
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
		Dialogs.SaveFile(owner, "Export quiz", QuizStore.SuggestedFileName(quiz), QuizStore.ZipFilters, path =>
		{
			try
			{
				QuizStore.Export(quiz, path.EndsWith(".zip") ? path : path + QuizStore.ZipExtension);
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
			if (!f.EndsWith(".json") && !f.EndsWith(".txt") && !f.EndsWith(".zip")) continue;
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
