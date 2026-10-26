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
	/// <summary>Exports: a zip with the quiz file and its sound and video files next to it.</summary>
	public const string ZipExtension = ".quiznight.zip";
	public static readonly string[] ZipFilters = { "*.zip ; Quiz files" };
	/// <summary>For importing: exported quizzes (zip, or the older single .json) and share codes saved as text (Discord sends long messages as message.txt).</summary>
	public static readonly string[] ImportFilters = { "*.zip, *.json, *.txt ; Quiz files and share codes" };

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
			throw new UserError(Lang.F("Saving failed: {0}", e.Message));
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
		return (clean == "" ? "quiz" : clean) + ZipExtension;
	}

	/// <summary>
	/// Writes a copy of the quiz to any path (backups, sharing) as a zip: quiz.json, and each sound or video as a file of its
	/// own in media/ (named by its question), instead of as text inside the quiz file (a third smaller).
	/// </summary>
	public static void Export(Quiz quiz, string path)
	{
		try
		{
			var copy = QuizJson.Parse(QuizJson.Write(quiz), out _)!;
			using var zip = System.IO.Compression.ZipFile.Open(path, System.IO.Compression.ZipArchiveMode.Create);
			foreach (var q in copy.Questions)
			{
				if (!q.IsMedia || q.Media is not { DataUrl.Length: > 0 } media || Server.DataUrl.Bytes(media.DataUrl, out var mime) is not { } bytes) continue;
				// (sound and video are already compressed: stored as they are)
				var entry = zip.CreateEntry($"media/{q.Id}{MediaExtension(mime, media.Name)}", System.IO.Compression.CompressionLevel.NoCompression);
				using (var s = entry.Open()) s.Write(bytes);
				media.Mime = mime;
				media.DataUrl = "";
			}
			var json = zip.CreateEntry("quiz.json", System.IO.Compression.CompressionLevel.Optimal);
			using (var w = new StreamWriter(json.Open())) w.Write(QuizJson.Write(copy, true));
		}
		catch (Exception e) when (e is IOException or UnauthorizedAccessException)
		{
			throw new UserError(Lang.F("Export failed: {0}", e.Message));
		}
	}

	static string MediaExtension(string mime, string name) =>
		Path.GetExtension(name) is { Length: > 1 } ext ? ext.ToLowerInvariant()
		: mime switch { "audio/mpeg" => ".mp3", "audio/wav" or "audio/x-wav" => ".wav", "video/ogg" => ".ogv", _ => ".ogg" };

	/// <summary>An exported zip back: quiz.json, with each media/ file put back into its question.</summary>
	public static Quiz ReadZip(string path)
	{
		try
		{
			using var zip = System.IO.Compression.ZipFile.OpenRead(path);
			var json = zip.GetEntry("quiz.json") ?? throw new UserError(QuizJson.NotAQuiz);
			// (zips can unpack to far more than they are: only as much as a quiz and its files can be)
			string text;
			using (var r = json.Open()) text = System.Text.Encoding.UTF8.GetString(Guard.ReadCapped(r, Guard.MaxQuizJsonBytes));
			var quiz = QuizJson.Parse(text, out var error) ?? throw new UserError(error ?? QuizJson.NotAQuiz);
			foreach (var q in quiz.Questions)
			{
				if (q.Media == null || zip.Entries.FirstOrDefault(e => e.FullName.StartsWith($"media/{q.Id}.")) is not { } file) continue;
				using var s = file.Open();
				q.Media.DataUrl = Server.DataUrl.Make(q.Media.Mime, Guard.ReadCapped(s, Guard.MaxSharedFileBytes));
			}
			return quiz;
		}
		catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidDataException)
		{
			throw new UserError(Lang.F("Couldn't read that file: {0}", e.Message));
		}
	}

	/// <summary>Imports a quiz file (zip or .json) as a new quiz (a fresh id, so importing twice gives two copies).</summary>
	public static Quiz Import(string path)
	{
		if (path.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
		{
			var fromZip = ReadZip(path);
			if (fromZip.Title.Trim() == "") fromZip.Title = Path.GetFileName(path).Replace(ZipExtension, "");
			return SaveImported(fromZip);
		}
		string text;
		try
		{
			text = File.ReadAllText(path);
		}
		catch (Exception e) when (e is IOException or UnauthorizedAccessException)
		{
			throw new UserError(Lang.F("Couldn't read that file: {0}", e.Message));
		}
		if (ShareCode.LooksLong(text)) return ImportCode(text);
		var quiz = QuizJson.Parse(text, out var error) ?? throw new UserError(error ?? QuizJson.NotAQuiz);
		if (quiz.Title.Trim() == "") quiz.Title = Path.GetFileName(path).Replace(Extension, "");
		return SaveImported(quiz);
	}

	/// <summary>Imports a long share code (ShareCode) as a new quiz.</summary>
	public static Quiz ImportCode(string code) =>
		SaveImported(ShareCode.Read(code, out var error) ?? throw new UserError(error ?? QuizJson.NotAQuiz));

	/// <summary>Saves a quiz from elsewhere (a file, a share code) as a new quiz: a fresh id, so importing twice gives two copies.</summary>
	public static Quiz SaveImported(Quiz quiz)
	{
		quiz.Id = MakeId();
		if (quiz.CreatedAt == 0) quiz.CreatedAt = NowMs();
		return Save(quiz);
	}
}
