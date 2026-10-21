using System.Linq;
using Godot;
using QuizNight.Shared;

namespace QuizNight.Ui;

/// <summary>Copying a quiz as a share code, and what to tell the one importing it.</summary>
public static class ShareInfo
{
	// Longer than this doesn't fit in one Discord message (Discord turns it into a message.txt file).
	const int ChatLimit = 4000;

	public static void Copy(Node owner, Quiz quiz)
	{
		var code = ShareCode.Make(quiz);
		DisplayServer.ClipboardSet(code);
		var text = Lang.F("Share code copied ({0} KB). On the other PC, press Paste share code in My quizzes.", System.Math.Max(1, code.Length / 1024));
		if (code.Length > ChatLimit)
			text += "\n\n" + Lang.T("It's too long for one Discord message: paste it anyway and Discord sends it as a file (message.txt), which Import file reads.");
		var media = ShareCode.MissingMedia(quiz);
		if (media > 0)
			text += "\n\n" + Lang.F("Sound and video aren't in the code: {0} to add again after importing.", U.Plural(media, "file", "files"));
		Dialogs.Info(owner, text, "Share code");
	}

	public static void Imported(Node owner, Quiz quiz)
	{
		var text = Lang.F("Imported \"{0}\".", quiz.Title);
		var missing = quiz.Questions.Where(q => q.IsMedia).Select(q => q.Media?.Name is { Length: > 0 } n ? n : q.Prompt).ToList();
		if (missing.Count > 0)
			text += "\n\n" + Lang.F("Add the sound and video files again: {0}", string.Join(", ", missing));
		Dialogs.Info(owner, text, "Share code");
	}
}
