using System.Linq;
using Godot;
using QuizNight.Shared;
using QuizNight.Storage;

namespace QuizNight.Ui;

/// <summary>Copying a quiz as a share code, and what to tell the one importing it.</summary>
public static class ShareInfo
{
	// Longer than this doesn't fit in one Discord message (Discord turns it into a message.txt file).
	const int ChatLimit = 4000;

	/// <summary>
	/// Uploads the quiz and copies its short code (QN-7K4P-9XZ2). Without internet: the long code that holds the whole quiz.
	/// </summary>
	public static async void Copy(Node owner, Quiz quiz)
	{
		var media = ShareCode.MissingMedia(quiz);
		var mediaNote = media > 0 ? "\n\n" + Lang.F("Sound and video aren't in the code: {0} to add again after importing.", U.Plural(media, "file", "files")) : "";
		var uploadFailed = false;
		if (QuizShare.Available)
		{
			var wait = Dialogs.Wait(owner, "Making a share code…", "Share code");
			try
			{
				var shortCode = await QuizShare.Upload(quiz);
				Dialogs.Close(wait);
				DisplayServer.ClipboardSet(shortCode);
				if (GodotObject.IsInstanceValid(owner))
					Dialogs.Info(owner, Lang.F("Share code copied: {0}\n\nSend it to a friend or use it on your other PC: My quizzes → Paste share code.", shortCode) + mediaNote, "Share code");
				return;
			}
			catch (System.Exception e)
			{
				GD.PrintErr($"Couldn't upload the quiz: {e.Message}");
				Dialogs.Close(wait);
				uploadFailed = true;
			}
			if (!GodotObject.IsInstanceValid(owner)) return;
		}
		var code = ShareCode.Make(quiz);
		DisplayServer.ClipboardSet(code);
		var text = (uploadFailed ? Lang.T("Couldn't upload the quiz (no internet?), so this is the long code with the whole quiz in it.") + "\n\n" : "")
			+ Lang.F("Share code copied ({0} KB). On the other PC, press Paste share code in My quizzes.", System.Math.Max(1, code.Length / 1024));
		if (code.Length > ChatLimit)
			text += "\n\n" + Lang.T("It's too long for one Discord message: paste it anyway and Discord sends it as a file (message.txt), which Import file reads.");
		Dialogs.Info(owner, text + mediaNote, "Share code");
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
