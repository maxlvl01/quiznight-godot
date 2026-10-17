// Quizzes live in the user folder (user://quizzes, one .quiznight.json per quiz).
// Export/import copies those files to and from anywhere, e.g. to share with friends.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Godot;
using QuizNight.Shared;

namespace QuizNight.Storage;

public static class QuizStore
{
	const string Dir = "user://quizzes";
	public const string Extension = ".quiznight.json";
	public static readonly string[] FileFilters = { "*.json ; Quiz files" };

	// Parsing files with big images is slow, so loaded quizzes are kept in memory.
	static readonly Dictionary<string, Quiz> Cache = new();
	static bool _scanned;

	/// <summary>Random id (24 hex characters).</summary>
	public static string MakeId() => Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(12)).ToLowerInvariant();

	public static long NowMs() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

	static string FolderPath => ProjectSettings.GlobalizePath(Dir);
	static string PathFor(string id) => Path.Combine(FolderPath, id + Extension);

	/// <summary>Where the files are on disk, for "Open folder".</summary>
	public static string Folder
	{
		get
		{
			Directory.CreateDirectory(FolderPath);
			return FolderPath;
		}
	}

	public static List<Quiz> List()
	{
		if (!_scanned)
		{
			Directory.CreateDirectory(FolderPath);
			foreach (var file in Directory.GetFiles(FolderPath, "*" + Extension))
			{
				try
				{
					var quiz = QuizJson.Parse(File.ReadAllText(file), out _);
					if (quiz != null && quiz.Id != "" && Path.GetFileName(file) == quiz.Id + Extension) Cache[quiz.Id] = quiz;
				}
				catch (IOException e)
				{
					GD.PrintErr($"Couldn't read {file}: {e.Message}");
				}
			}
			_scanned = true;
		}
		return Cache.Values.OrderByDescending(q => q.UpdatedAt).ToList();
	}

	public static Quiz? Get(string id)
	{
		List();
		return Cache.TryGetValue(id, out var q) ? q : null;
	}

	/// <summary>Saves a quiz. Throws UserError when the disk says no.</summary>
	public static Quiz Save(Quiz quiz)
	{
		// Drop maps no question uses anymore, so saved and exported quizzes stay small.
		var used = quiz.Questions.Where(q => q.Type == QuestionType.MapPin && q.MapId != null).Select(q => q.MapId).ToHashSet();
		quiz.Maps = quiz.Maps.Where(m => used.Contains(m.Id)).ToList();
		// Built-in maps ship with the game; their pictures don't need to be in the file.
		foreach (var m in quiz.Maps)
			if (MapPresets.Get(m.PresetId) != null) m.Image.DataUrl = "";
		quiz.UpdatedAt = NowMs();
		Directory.CreateDirectory(FolderPath);
		var path = PathFor(quiz.Id);
		var tmp = path + ".tmp";
		try
		{
			File.WriteAllText(tmp, QuizJson.Write(quiz));
			File.Move(tmp, path, true);
		}
		catch (Exception e) when (e is IOException or UnauthorizedAccessException)
		{
			throw new UserError($"Saving failed: {e.Message}");
		}
		Cache[quiz.Id] = quiz;
		return quiz;
	}

	public static void Delete(string id)
	{
		Cache.Remove(id);
		var path = PathFor(id);
		if (File.Exists(path)) File.Delete(path);
	}

	public static Quiz Create(string title = "New quiz")
	{
		var now = NowMs();
		return Save(new Quiz { Id = MakeId(), Title = title, CreatedAt = now, UpdatedAt = now });
	}

	public static string SuggestedFileName(Quiz quiz)
	{
		var clean = new string(quiz.Title.Where(c => char.IsLetterOrDigit(c) || c is ' ' or '-' or '_').ToArray()).Trim();
		return (clean == "" ? "quiz" : clean) + Extension;
	}

	/// <summary>Writes a copy of the quiz to any path (backups, sharing).</summary>
	public static void Export(Quiz quiz, string path)
	{
		try
		{
			File.WriteAllText(path, QuizJson.Write(quiz, true));
		}
		catch (Exception e) when (e is IOException or UnauthorizedAccessException)
		{
			throw new UserError($"Export failed: {e.Message}");
		}
	}

	/// <summary>Imports a quiz file as a new quiz (a fresh id, so importing twice gives two copies).</summary>
	public static Quiz Import(string path)
	{
		string text;
		try
		{
			text = File.ReadAllText(path);
		}
		catch (Exception e) when (e is IOException or UnauthorizedAccessException)
		{
			throw new UserError($"Couldn't read that file: {e.Message}");
		}
		var quiz = QuizJson.Parse(text, out var error) ?? throw new UserError(error ?? QuizJson.NotAQuiz);
		quiz.Id = MakeId();
		if (quiz.Title.Trim() == "") quiz.Title = Path.GetFileName(path).Replace(Extension, "");
		if (quiz.CreatedAt == 0) quiz.CreatedAt = NowMs();
		return Save(quiz);
	}
}
