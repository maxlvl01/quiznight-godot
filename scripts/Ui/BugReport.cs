// "Report a bug or give feedback": a small 🐞 button in the bottom left of every screen (above the jukebox's), sent by hand
// only. It takes a screenshot of the screen as it is, then opens a window: bug or feedback, then one text box. The category comes
// from where it was opened (the screen, a window over it, the question in a game), not picked; the game adds what it
// knows (version, room, the end of the log) from a template (Storage/BugReports).

using System;
using System.Linq;
using System.Text;
using Godot;
using QuizNight.Shared;
using QuizNight.Storage;
using QuizNight.Ui.Arcade;
using QuizNight.Ui.Screens;

namespace QuizNight.Ui;

public partial class BugButton : Control
{
	const float Size_ = 36, Margin = 20, JukeboxButton = 44;

	public BugButton()
	{
		SetAnchorsPreset(LayoutPreset.FullRect);
		MouseFilter = MouseFilterEnum.Ignore;
		var button = MediaIcon.Button(MediaIcon.Kind.Bug, "Report a bug or give feedback", Open, Size_ - 6);
		button.FocusMode = FocusModeEnum.None;
		Tutorial.Mark(button, "bug");
		// (its size first: the corner offsets are worked out from it)
		button.Size = new Vector2(JukeboxButton, Size_);
		button.SetAnchorsAndOffsetsPreset(LayoutPreset.BottomLeft, LayoutPresetMode.KeepSize, (int)Margin);
		// (above the jukebox's note button)
		button.OffsetTop -= JukeboxButton + 10;
		button.OffsetBottom -= JukeboxButton + 10;
		AddChild(button);
	}

	/// <summary>The screenshot first (the screen as it is now, without the window), then the window (dev: straight to "bug"
	/// or "feedback").</summary>
	public static void Open() => Open(null);

	public static void Open(string? start)
	{
		var category = Where();
		var details = Details();
		byte[]? png = null;
		if (App.I.GetViewport().GetTexture().GetImage() is { } shot)
		{
			if (shot.GetWidth() > 1920) shot.Resize(1920, shot.GetHeight() * 1920 / shot.GetWidth(), Image.Interpolation.Bilinear);
			png = shot.SavePngToBuffer();
		}
		BugReportWindow.Show(category, details, png, start);
	}

	/// <summary>The category: the screen, what's open over it, and in a game the question it's on.</summary>
	public static string Where()
	{
		var app = App.I;
		var where = app.Screen switch
		{
			HomeScreen => "Home",
			QuizListScreen => "My quizzes",
			QuizOverviewScreen => "Quiz overview",
			QuestionEditorScreen editor => "Editor › " + Lang.T(editor.TypeLabel),
			HostLobbyScreen => "Lobby (host)",
			WaitingRoomScreen => "Waiting room",
			HostGameScreen => "Game (host)" + Question(app.Host?.State),
			PlayerGameScreen => "Game (player)" + Question(app.Client?.State),
			PodiumScreen => "Podium",
			SettingsScreen => "Settings",
			EternalTableScreen => "Statistics › Eternal table",
			MatchHistoryScreen => "Statistics › Match history",
			ProfileScreen => "Statistics › Profile",
			TestLabScreen => "Test lab",
			WelcomeScreen => "Welcome",
			{ } other => other.GetType().Name.Replace("Screen", ""),
			null => "Start",
		};
		// (a window over the screen: both)
		if (app.Screen?.FindChildren("*", nameof(ArcadeWindow), true, false).OfType<ArcadeWindow>().FirstOrDefault() is { } arcade)
			where += " › Mini games › " + arcade.Title;
		if (SettingsWindow.Current != null) where += " › Settings";
		return where;
	}

	/// <summary>" › Map pin › Reveal": the question a game is on (its type, and the reveal).</summary>
	static string Question(RoomState? room) => room?.Game is { } game
		? " › " + Lang.T(QuestionKinds.TypeLabel(game.Question.Type)) + (game.Stage == Stage.Reveal ? " › Reveal" : "")
		: "";

	/// <summary>What the game knows about the room (if in one): role, mode, phase, question, players.</summary>
	static string Details()
	{
		var app = App.I;
		var sb = new StringBuilder();
		var room = app.Host?.State ?? app.Client?.State;
		if (room == null) return "- **Room:** none\n";
		sb.AppendLine($"- **Room:** {(app.Host != null ? "host" : "player")} · {(app.Host?.Mode ?? app.Client?.Mode)} · {room.Phase} · {room.Players.Count} players");
		if (room.Game is { } game)
			sb.AppendLine($"- **Question:** {game.Index + 1} of {game.Total} · {game.Question.Type} · {game.Stage}");
		return sb.ToString();
	}
}

/// <summary>The window: first bug or feedback, then one text box and Send. A bug is filed under where it was opened (with
/// the log); feedback under what it's about (a map, a question type, quality of life, something else).</summary>
public static class BugReportWindow
{
	// (what feedback can be about: the folder in the repo (English, for Max), the label, and what the text box asks)
	static readonly (string Folder, string Label, string Ask)[] Kinds =
	{
		("Maps", "A map", "Which map would you like? A game's world, a city, a country … A link to a good picture of it helps."),
		("Question types", "A question type", "What kind of question would you like? How would it play?"),
		("Quality of life", "Quality of life", "What would make the game easier or nicer to use?"),
		("Other", "Something else", "What's your idea?"),
	};

	public static void Show(string category, string details, byte[]? png, string? start = null)
	{
		var overlay = new Screens.VideoGuide.GuideOverlay();
		var page = new VBoxContainer();
		overlay.SetContent(U.Pad(page, 24, 22, 24, 22));
		App.I.AddChild(overlay);

		void Swap(Control content)
		{
			foreach (var old in page.GetChildren())
			{
				page.RemoveChild(old);
				old.QueueFree();
			}
			page.AddChild(content);
		}

		void Choose() => Swap(U.V(14,
			U.Eyebrow("Bug or feedback?"),
			U.Text("What would you like to send Max?", 18, C.Text, Fonts.Bold),
			U.H(12,
				Choice(MediaIcon.Kind.Bug, "Report a bug", "Something doesn't work or looks wrong.", () => Form(null)),
				Choice(MediaIcon.Kind.Idea, "Give feedback", "An idea: a map, a question type, something that would make the game nicer.", () => Form(0))),
			U.H(10, new Control().Grow(), U.Small("Cancel", overlay.QueueFree))));

		// (feedback: kind is the index into Kinds; null = a bug)
		void Form(int? kind)
		{
			var bug = kind == null;
			var text = new TextEdit
			{
				PlaceholderText = Lang.T(bug ? "What happened? What did you do just before, and what did you expect?" : Kinds[kind!.Value].Ask),
				CustomMinimumSize = new Vector2(560, 170), WrapMode = TextEdit.LineWrappingMode.Boundary,
			};
			var status = U.Muted("", 13);
			// (a bug's screenshot is on by default; for an idea it rarely helps, so it's off)
			var shot = new CheckBox { Text = Lang.T("Send a screenshot of the screen too"), ButtonPressed = bug && png != null, Disabled = png == null };
			Button? send = null;
			send = U.Btn("Send", "AccentButton", () =>
			{
				if (text.Text.Trim() == "") return;
				send!.Disabled = true;
				status.Text = Lang.T("Sending …");
				var (folder, report, root) = bug
					? (category, BugReports.Make(category, text.Text, details), "bug-reports")
					: (Kinds[kind!.Value].Folder, BugReports.MakeFeedback(Kinds[kind.Value].Folder, category, text.Text), "feedback");
				BugReports.Send(folder, report, shot.ButtonPressed ? png : null, ok =>
				{
					if (!GodotObject.IsInstanceValid(overlay)) return;
					status.Text = ok ? Lang.T(bug ? "Thanks! Max will look at it." : "Thanks for the idea! Max will read it.")
						: Lang.T("Couldn't send it now: it goes the next time you start the game.");
					overlay.GetTree().CreateTimer(ok ? 1.5 : 3).Timeout += () =>
					{
						if (GodotObject.IsInstanceValid(overlay)) overlay.QueueFree();
					};
				}, root);
			}, 18);
			send.Disabled = true;
			text.TextChanged += () => send.Disabled = text.Text.Trim() == "" || !BugReports.Enabled;
			var box = U.V(12, U.Eyebrow(bug ? "Report a bug" : "Give feedback"));
			if (bug) box.AddChild(U.Text(Lang.F("Bug in: {0}", category), 18, C.Text, Fonts.Bold).Raw());
			else
			{
				box.AddChild(U.Text("What is it about?", 18, C.Text, Fonts.Bold));
				// (switching keeps what's typed: only the question in the empty box changes)
				box.AddChild(U.Segmented(Kinds.Select((k, i) => (i.ToString(), k.Label)).ToArray(), kind!.Value.ToString(), id =>
				{
					kind = int.Parse(id);
					text.PlaceholderText = Lang.T(Kinds[kind.Value].Ask);
				}));
			}
			box.AddChild(text);
			box.AddChild(shot);
			box.AddChild(U.Muted(bug
				? "The game adds your Steam name and ID, its version, where you were and the end of its log (your Windows user name is left out), and sends it to Max."
				: "The game adds your Steam name and ID (so your idea can be credited in the changelog), its version and where you were, and sends it to Max.", 12));
			box.AddChild(U.H(10, U.Small("‹ Back", Choose), status.Grow(), U.Small("Cancel", overlay.QueueFree), send));
			if (!BugReports.Enabled) status.Text = Lang.T("This version can't send reports (it has no key for it).");
			Swap(box);
			text.CallDeferred(Control.MethodName.GrabFocus);
		}

		if (start == "bug") Form(null);
		else if (start == "feedback") Form(0);
		else Choose();
	}

	/// <summary>One of the two big choices: a key like the quiz overview's type cards, with an icon, a title and a line.</summary>
	static Control Choice(MediaIcon.Kind icon, string title, string line, Action pick)
	{
		var card = new MarginContainer { MouseFilter = Control.MouseFilterEnum.Ignore, CustomMinimumSize = new Vector2(274, 0) }.Grow();
		var button = new Button { MouseDefaultCursorShape = Control.CursorShape.PointingHand, CustomMinimumSize = new Vector2(0, 130) };
		button.AddThemeStyleboxOverride("normal", new KeyBox(C.Panel2, 16, 16, 14, 3, 6, false));
		button.AddThemeStyleboxOverride("hover", new KeyBox(C.Panel2.Lightened(0.1f), 16, 16, 14, 3, 6, false));
		button.AddThemeStyleboxOverride("pressed", new KeyBox(C.Panel2.Darkened(0.05f), 16, 16, 14, 3, 6, true));
		button.AddThemeStyleboxOverride("hover_pressed", new KeyBox(C.Panel2, 16, 16, 14, 3, 6, true));
		button.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
		button.Pressed += pick;
		card.AddChild(button);
		var inner = U.V(6,
			new MediaIcon(icon) { CustomMinimumSize = new Vector2(30, 30), Color = C.Accent, SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin },
			U.Bold(title, 18),
			U.Muted(line, 13));
		// (everything on the card lets clicks through to the key underneath)
		inner.MouseFilter = Control.MouseFilterEnum.Ignore;
		foreach (var c in inner.GetChildren().OfType<Control>()) c.MouseFilter = Control.MouseFilterEnum.Ignore;
		card.AddChild(U.Pad(inner, 18, 14, 18, 20).With(m => m.MouseFilter = Control.MouseFilterEnum.Ignore));
		return card;
	}
}
