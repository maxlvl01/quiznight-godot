// A question's type and subtype as the saved results store them (one row per player and question), so the Ewige
// Tabelle and the profile pages can show stats per kind of question: "classic" + "typed", "mapPin" + "skyrim" …

using System.Collections.Generic;
using System.Linq;

namespace QuizNight.Shared;

public static class QuestionKinds
{
	public const string Typed = "typed";
	/// <summary>Map pin on a picture someone uploaded (not a built-in map).</summary>
	public const string CustomMap = "custom";
	public const string Places = "places";
	/// <summary>Picture questions shown as a reveal (the image reveal type).</summary>
	public const string Reveal = "reveal";

	/// <summary>The variant a question is played as (empty for types without variants).</summary>
	public static string Subtype(Question q, Quiz quiz) => q.Type switch
	{
		QuestionType.Classic or QuestionType.Picture => q.IsChoice ? q.Mode : q.IsTyped ? Typed : ClassicMode.Buzzer,
		QuestionType.Sound or QuestionType.Video or QuestionType.Image => q.IsTyped ? Typed : ClassicMode.Buzzer,
		QuestionType.MapPin => quiz.MapById(q.MapId)?.PresetId ?? CustomMap,
		QuestionType.Estimation => q.HasRankPoints ? Places : q.Scoring,
		QuestionType.Order => q.OrderMode,
		QuestionType.Interactive => q.Judging,
		QuestionType.Steps => q.StepsMode,
		_ => "",
	};

	/// <summary>The subtypes the filter offers for a type (map pins: the maps that came up in the results).</summary>
	public static IReadOnlyList<string> Subtypes(string type) => type switch
	{
		QuestionType.Classic => new[] { ClassicMode.Buzzer, Typed, ClassicMode.Choice, ClassicMode.Multi },
		QuestionType.Picture => new[] { ClassicMode.Buzzer, Typed, ClassicMode.Choice, ClassicMode.Multi, Reveal },
		QuestionType.Sound or QuestionType.Video or QuestionType.Image => new[] { ClassicMode.Buzzer, Typed },
		QuestionType.Estimation => new[] { Scoring.ClosestWins, Scoring.Top3, Places },
		QuestionType.Order => new[] { OrderMode.All, OrderMode.Timeline },
		QuestionType.Interactive => new[] { Judging.Vote, Judging.Host },
		QuestionType.Steps => new[] { StepsMode.Hints, StepsMode.Sentence },
		_ => System.Array.Empty<string>(),
	};

	/// <summary>The type's name, as the editor calls it ("Map pin").</summary>
	public static string TypeLabel(string type) => QuestionType.Infos.TryGetValue(type, out var info) ? info.Label : type;

	/// <summary>A subtype's name (English: the translation key).</summary>
	public static string SubtypeLabel(string type, string subtype) => subtype switch
	{
		ClassicMode.Buzzer => "Buzzer",
		Typed => "Everyone types",
		ClassicMode.Choice => "A B C D",
		ClassicMode.Multi => "Multiple choice",
		Scoring.ClosestWins => "Closest wins",
		Scoring.Top3 => "Top 3",
		Places => "Points per place",
		OrderMode.All => "Everyone at once",
		OrderMode.Timeline => "Timeline",
		Judging.Vote => "Players vote",
		Judging.Host => "Host's favourite",
		CustomMap => "Own map",
		StepsMode.Hints => "Hints",
		StepsMode.Sentence => "Sentence in parts",
		Reveal => "Reveal",
		_ when type == QuestionType.MapPin && MapPresets.Get(subtype) is { } map => map.Name.Split(" (")[0],
		_ => subtype,
	};

	/// <summary>Whether players buzz on this type and subtype (so buzz times matter, answer times don't).</summary>
	public static bool IsBuzzer(string type, string subtype) =>
		type == QuestionType.Steps || subtype == ClassicMode.Buzzer;

	/// <summary>The types in the editor's order.</summary>
	public static IEnumerable<string> Types => QuestionType.All;

	/// <summary>"mapPin/skyrim" ↔ (type, subtype): how a filter is kept.</summary>
	public static string Key(string type, string subtype = "") => subtype == "" ? type : type + "/" + subtype;

	public static (string Type, string Subtype) Split(string key)
	{
		var at = key.IndexOf('/');
		return at < 0 ? (key, "") : (key[..at], key[(at + 1)..]);
	}

	public static bool Matches(string key, string type, string subtype)
	{
		var (t, s) = Split(key);
		return t == type && (s == "" || s == subtype);
	}

	/// <summary>Distinct keys in a list (for the filter: only kinds that were actually played).</summary>
	public static List<string> Played(IEnumerable<(string Type, string Subtype)> rows) =>
		rows.Distinct().OrderBy(r => System.Array.IndexOf(QuestionType.All, r.Type)).ThenBy(r => r.Subtype).Select(r => Key(r.Type, r.Subtype)).ToList();
}
