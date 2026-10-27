// Question categories: the quiz maker can give a question one (a preset or their own), the host decides in the lobby
// whether players see it (it makes questions easier), and the results keep it for stats per category.

using System.Linq;

namespace QuizNight.Shared;

public static class Categories
{
	public const int MaxLength = 32;

	/// <summary>The editor's suggestions (English: the translation keys; the file stores what was picked).</summary>
	public static readonly string[] Presets =
	{
		"General knowledge", "Geography", "History", "Science", "Biology", "Chemistry", "Physics", "Space & astronomy",
		"Human body & medicine", "Maths", "Nature & animals", "Music", "Film & TV", "Games", "Sport", "Food & drink",
		"Art & literature", "Language", "Technology", "Pop culture", "Politics",
	};

	/// <summary>Trimmed, single spaces, at most MaxLength characters.</summary>
	public static string Clean(string raw)
	{
		var s = string.Join(' ', (raw ?? "").Split((char[]?)null, System.StringSplitOptions.RemoveEmptyEntries));
		return s.Length > MaxLength ? s[..MaxLength].TrimEnd() : s;
	}

	/// <summary>The same category however it was written ("geography" = "Geography").</summary>
	public static string Key(string category) => Clean(category).ToLowerInvariant();

	/// <summary>A preset's name in the player's language, or the quiz maker's own text as it is.</summary>
	public static string Label(string category) =>
		Presets.FirstOrDefault(p => Key(p) == Key(category)) is { } preset ? Lang.T(preset) : category;
}
