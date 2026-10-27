// The saved quiz nights (Server.SavedNight): one file per quiz in user://saves, written by the host's game after every
// question (HostSession.Autosave) and removed when the night ends.

using System;
using System.IO;
using Godot;
using QuizNight.Server;

namespace QuizNight.Storage;

public static class NightSaves
{
	const string Dir = "user://saves";

	static string PathFor(string quizId) => System.IO.Path.Combine(ProjectSettings.GlobalizePath(Dir), quizId + ".json");

	/// <summary>What was written last (it's only written again when something changed).</summary>
	static string? _lastPath, _lastJson;

	public static void Write(string quizId, string json)
	{
		var path = PathFor(quizId);
		if (path == _lastPath && json == _lastJson) return;
		try
		{
			Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
			// (a new file first, then swapped in: a crash while writing never leaves half a save)
			File.WriteAllText(path + ".tmp", json);
			File.Move(path + ".tmp", path, true);
			(_lastPath, _lastJson) = (path, json);
		}
		catch (Exception e)
		{
			GD.PushWarning($"Couldn't save the quiz night: {e.Message}");
		}
	}

	/// <summary>The quiz's unfinished night, or null.</summary>
	public static SavedNight? Load(string quizId)
	{
		var path = PathFor(quizId);
		try
		{
			if (!File.Exists(path) || SavedNight.FromJson(File.ReadAllText(path)) is not { } save || save.QuizId != quizId) return null;
			save.SavedAt = File.GetLastWriteTime(path);
			return save;
		}
		catch (Exception)
		{
			return null;
		}
	}

	public static void Delete(string quizId)
	{
		var path = PathFor(quizId);
		if (path == _lastPath) _lastPath = _lastJson = null;
		try
		{
			if (File.Exists(path)) File.Delete(path);
		}
		catch (Exception e)
		{
			GD.PushWarning($"Couldn't remove the saved quiz night: {e.Message}");
		}
	}
}
