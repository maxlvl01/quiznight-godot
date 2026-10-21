// Share codes carry a quiz to your other PC or to a friend (e.g. over Discord), without sound and video (too big;
// the one importing adds them again). The quiz is packed (Pack: the quiz file, Brotli-compressed) and either
// uploaded, so a short code finds it again (QN-7K4P-9XZ2: Storage.QuizShare), or, without internet, written out
// whole as a long code (QN1-…: base64url of the packed quiz).

using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;

namespace QuizNight.Shared;

public static class ShareCode
{
	const string LongPrefix = "QN1-";
	const string ShortPrefix = "QN-";
	/// <summary>Short codes: 8 characters of the room-code alphabet (no 0/O or 1/I), shown as QN-XXXX-XXXX.</summary>
	public const int ShortLength = 8;

	/// <summary>The quiz without sound/video data and built-in map pictures, as a compressed quiz file.</summary>
	public static byte[] Pack(Quiz quiz)
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
		return packed.ToArray();
	}

	/// <summary>A packed quiz back, or null with an error.</summary>
	public static Quiz? Unpack(byte[] packed, out string? error)
	{
		string json;
		try
		{
			using var brotli = new BrotliStream(new MemoryStream(packed), CompressionMode.Decompress);
			using var reader = new StreamReader(brotli, Encoding.UTF8);
			json = reader.ReadToEnd();
		}
		catch (Exception e) when (e is InvalidDataException or IOException)
		{
			error = Lang.T("That share code is broken or cut off. Copy the whole code again.");
			return null;
		}
		return QuizJson.Parse(json, out error);
	}

	/// <summary>The whole quiz as one (long) line of text: the fallback when it can't be uploaded.</summary>
	public static string Make(Quiz quiz) =>
		LongPrefix + Convert.ToBase64String(Pack(quiz)).TrimEnd('=').Replace('+', '-').Replace('/', '_');

	/// <summary>A new random short code, e.g. "QN-7K4P-9XZ2".</summary>
	public static string NewShort()
	{
		var chars = Enumerable.Range(0, ShortLength)
			.Select(_ => Limits.CodeAlphabet[System.Security.Cryptography.RandomNumberGenerator.GetInt32(Limits.CodeAlphabet.Length)]).ToArray();
		return $"{ShortPrefix}{new string(chars, 0, 4)}-{new string(chars, 4, 4)}";
	}

	/// <summary>
	/// The 8 characters of a short code typed or pasted any way ("qn-7k4p-9xz2", "7K4P9XZ2", with spaces), or null if it isn't one.
	/// </summary>
	public static string? ShortKey(string text)
	{
		var s = new string(text.Where(c => !char.IsWhiteSpace(c) && c != '-').ToArray()).ToUpperInvariant();
		if (s.StartsWith("QN")) s = s[2..];
		return s.Length == ShortLength && s.All(c => Limits.CodeAlphabet.Contains(c)) ? s : null;
	}

	/// <summary>Does the text look like a share code, short or long (before the work of reading it)?</summary>
	public static bool Looks(string text) => LooksLong(text) || ShortKey(text) != null;

	public static bool LooksLong(string text) => text.TrimStart().StartsWith(LongPrefix);

	/// <summary>The quiz in a long share code, or null with an error (not a code, cut off, from a newer version).</summary>
	public static Quiz? Read(string text, out string? error)
	{
		// (chat apps may wrap a long code over several lines)
		var code = new string(text.Where(c => !char.IsWhiteSpace(c)).ToArray());
		error = null;
		if (!code.StartsWith(LongPrefix))
		{
			error = Lang.T("That isn't a Quiznight share code.");
			return null;
		}
		byte[] packed;
		try
		{
			var b64 = code[LongPrefix.Length..].Replace('-', '+').Replace('_', '/');
			packed = Convert.FromBase64String(b64.PadRight(b64.Length + (4 - b64.Length % 4) % 4, '='));
		}
		catch (FormatException)
		{
			error = Lang.T("That share code is broken or cut off. Copy the whole code again.");
			return null;
		}
		return Unpack(packed, out error);
	}

	/// <summary>How many sound and video files the importer has to add again.</summary>
	public static int MissingMedia(Quiz quiz) => quiz.Questions.Count(q => q.IsMedia);
}
