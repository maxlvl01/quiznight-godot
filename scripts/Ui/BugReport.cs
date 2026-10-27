// "Report a bug": a small 🐞 button in the bottom left of every screen (above the jukebox's), sent by hand only. It
// takes a screenshot of the screen as it is, then opens a window with one text box (what happened). The category comes
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
		var button = MediaIcon.Button(MediaIcon.Kind.Bug, "Report a bug", Open, Size_ - 6);
		button.FocusMode = FocusModeEnum.None;
		// (its size first: the corner offsets are worked out from it)
		button.Size = new Vector2(JukeboxButton, Size_);
		button.SetAnchorsAndOffsetsPreset(LayoutPreset.BottomLeft, LayoutPresetMode.KeepSize, (int)Margin);
		// (above the jukebox's note button)
		button.OffsetTop -= JukeboxButton + 10;
		button.OffsetBottom -= JukeboxButton + 10;
		AddChild(button);
	}

	/// <summary>The screenshot first (the screen as it is now, without the window), then the window.</summary>
	public static void Open()
	{
		var category = Where();
		var details = Details();
		byte[]? png = null;
		if (App.I.GetViewport().GetTexture().GetImage() is { } shot)
		{
			if (shot.GetWidth() > 1920) shot.Resize(1920, shot.GetHeight() * 1920 / shot.GetWidth(), Image.Interpolation.Bilinear);
			png = shot.SavePngToBuffer();
		}
		BugReportWindow.Show(category, details, png);
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

/// <summary>The window: where it was opened, one text box, Send.</summary>
public static class BugReportWindow
{
	public static void Show(string category, string details, byte[]? png)
	{
		var overlay = new Screens.VideoGuide.GuideOverlay();
		var text = new TextEdit
		{
			PlaceholderText = Lang.T("What happened? What did you do just before, and what did you expect?"),
			CustomMinimumSize = new Vector2(560, 170), WrapMode = TextEdit.LineWrappingMode.Boundary,
		};
		var status = U.Muted("", 13);
		var shot = new CheckBox { Text = Lang.T("Send a screenshot of the screen too"), ButtonPressed = png != null, Disabled = png == null };
		Button? send = null;
		send = U.Btn("Send", "AccentButton", () =>
		{
			if (text.Text.Trim() == "") return;
			send!.Disabled = true;
			status.Text = Lang.T("Sending …");
			BugReports.Send(category, BugReports.Make(category, text.Text, details), shot.ButtonPressed ? png : null, ok =>
			{
				if (!GodotObject.IsInstanceValid(overlay)) return;
				status.Text = ok ? Lang.T("Thanks! Max will look at it.") : Lang.T("Couldn't send it now: it goes the next time you start the game.");
				overlay.GetTree().CreateTimer(ok ? 1.5 : 3).Timeout += () =>
				{
					if (GodotObject.IsInstanceValid(overlay)) overlay.QueueFree();
				};
			});
		}, 18);
		send.Disabled = true;
		text.TextChanged += () => send.Disabled = text.Text.Trim() == "";
		var box = U.V(12,
			U.Eyebrow("Report a bug"),
			U.Text(Lang.F("Bug in: {0}", category), 18, C.Text, Fonts.Bold).Raw(),
			text,
			shot,
			U.Muted("The game adds its version, where you were and the end of its log (your Windows user name is left out), and sends it to Max.", 12),
			U.H(10, status.Grow(), U.Small("Cancel", overlay.QueueFree), send));
		if (!BugReports.Enabled)
		{
			send.Disabled = true;
			status.Text = Lang.T("This version can't send reports (it has no key for it).");
		}
		overlay.SetContent(U.Pad(box, 24, 22, 24, 22));
		App.I.AddChild(overlay);
		text.CallDeferred(Control.MethodName.GrabFocus);
	}
}
