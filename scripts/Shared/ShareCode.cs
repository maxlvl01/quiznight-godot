// A quiz as one line of text, for copy and paste (e.g. to your other PC or to a friend over Discord).
// The quiz file without sound and video (too big; the one importing adds them again), Brotli-compressed
// and base64url-encoded behind a short prefix.

using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;

namespace QuizNight.Shared;

public static class ShareCode
{
	const string Prefix = "QN1-";

	public static string Make(Quiz quiz)
	{
		var copy = QuizJson.Parse(QuizJson.Write(quiz), out _)!;
		foreach (var q in copy.Questions)
			if (q.IsMedia && q.Media != null) q.Media.DataUrl = "";
		// Built-in maps ship with the game.
		foreach (var m in copy.Maps)
			if (MapPresets.Get(m.PresetId) != null) m.Image.DataUrl = "";
		var raw = Encoding.UTF8.GetBytes(QuizJson.Write(copy));
		using var packed = new MemoryStream();
		using (var brotli = new BrotliStream(packed, CompressionLevel.SmallestSize)) brotli.Write(raw);
		return Prefix + Convert.ToBase64String(packed.ToArray()).TrimEnd('=').Replace('+', '-').Replace('/', '_');
	}

	/// <summary>Does the text look like a share code (before the work of reading it)?</summary>
	public static bool Looks(string text) => text.TrimStart().StartsWith(Prefix);

	/// <summary>The quiz in a share code, or null with an error (not a code, cut off, from a newer version).</summary>
	public static Quiz? Read(string text, out string? error)
	{
		// (chat apps may wrap a long code over several lines)
		var code = new string(text.Where(c => !char.IsWhiteSpace(c)).ToArray());
		error = null;
		if (!code.StartsWith(Prefix))
		{
			error = Lang.T("That isn't a Quiznight share code.");
			return null;
		}
		string json;
		try
		{
			var b64 = code[Prefix.Length..].Replace('-', '+').Replace('_', '/');
			b64 = b64.PadRight(b64.Length + (4 - b64.Length % 4) % 4, '=');
			using var brotli = new BrotliStream(new MemoryStream(Convert.FromBase64String(b64)), CompressionMode.Decompress);
			using var reader = new StreamReader(brotli, Encoding.UTF8);
			json = reader.ReadToEnd();
		}
		catch (Exception e) when (e is FormatException or InvalidDataException or IOException)
		{
			error = Lang.T("That share code is broken or cut off. Copy the whole code again.");
			return null;
		}
		return QuizJson.Parse(json, out error);
	}

	/// <summary>How many sound and video files the importer has to add again.</summary>
	public static int MissingMedia(Quiz quiz) => quiz.Questions.Count(q => q.IsMedia);
}
