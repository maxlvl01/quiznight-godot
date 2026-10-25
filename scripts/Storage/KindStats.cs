// Stats from the per-question results (Results.Questions): for one kind of question (a type, a type's subtype, or a
// category), everyone's numbers, as the Ewige Tabelle's filter and the profile pages show them.

using System;
using System.Collections.Generic;
using System.Linq;
using QuizNight.Shared;

namespace QuizNight.Storage;

public static class KindStats
{
	/// <summary>A column: how to show it, and whether more is better (for sorting and places).</summary>
	/// <param name="Format">"n" a count, "avg" one decimal, "s" seconds, "rate" a share in %, "km" a distance, "off" % off.</param>
	public sealed record Col(string Id, string Label, bool HigherIsBetter, string Format, string Tip = "");

	/// <summary>"" all questions, "type:mapPin" / "type:mapPin/skyrim" a type or subtype, "cat:geography" a category.</summary>
	public static bool Matches(string filter, Results.QuestionRow r) =>
		filter == "" || filter.StartsWith("type:") && QuestionKinds.Matches(filter[5..], r.Type, r.Subtype)
		|| filter.StartsWith("cat:") && r.Category != "" && Categories.Key(r.Category) == filter[4..];

	public static string FilterLabel(string filter)
	{
		if (filter.StartsWith("cat:")) return Categories.Label(filter[4..]);
		if (!filter.StartsWith("type:")) return "All questions";
		var (type, sub) = QuestionKinds.Split(filter[5..]);
		var label = Lang.T(QuestionKinds.TypeLabel(type));
		return sub == "" ? label : label + " · " + Lang.T(QuestionKinds.SubtypeLabel(type, sub));
	}

	static double? Avg(IEnumerable<double?> xs)
	{
		var list = xs.Where(x => x != null).Select(x => x!.Value).ToList();
		return list.Count > 0 ? list.Average() : null;
	}

	static double? Min(IEnumerable<double?> xs)
	{
		var list = xs.Where(x => x != null).Select(x => x!.Value).ToList();
		return list.Count > 0 ? list.Min() : null;
	}

	/// <summary>Every number we can work out from someone's rows (the columns pick from these).</summary>
	public static Dictionary<string, double?> Values(IReadOnlyCollection<Results.QuestionRow> rows)
	{
		var played = rows.Count;
		var right = rows.Count(r => r.Right);
		var buzzes = rows.Sum(r => r.Buzzes);
		var placements = rows.Sum(r => r.Placements);
		var hints = rows.Where(r => r.Hint != null).ToList();
		return new()
		{
			["played"] = played,
			["right"] = right,
			["rate"] = played > 0 ? 100.0 * right / played : null,
			["points"] = rows.Sum(r => r.Points),
			["avgPoints"] = played > 0 ? (double)rows.Sum(r => r.Points) / played : null,
			["buzzes"] = buzzes,
			["wrongBuzzes"] = rows.Sum(r => r.WrongBuzzes),
			["buzzRate"] = buzzes > 0 ? 100.0 * rows.Count(r => r.Right && r.Buzzes > 0) / buzzes : null,
			["avgBuzz"] = Avg(rows.Select(r => r.BuzzS)),
			["bestBuzz"] = Min(rows.Select(r => r.BuzzS)),
			["avgAnswer"] = Avg(rows.Select(r => r.AnswerS)),
			["bestAnswer"] = Min(rows.Where(r => r.Right).Select(r => r.AnswerS)),
			["avgKm"] = Avg(rows.Select(r => r.Km)),
			["bestKm"] = Min(rows.Select(r => r.Km)),
			["under1km"] = rows.Count(r => r.Km < 1),
			["avgPct"] = Avg(rows.Select(r => r.PercentOff)),
			["bestPct"] = Min(rows.Select(r => r.PercentOff)),
			["bullseyes"] = rows.Count(r => r.PercentOff < 1),
			["avgHint"] = hints.Count > 0 ? hints.Average(r => r.Hint!.Value) : null,
			["firstHint"] = hints.Count(r => r.Hint == 1),
			["placements"] = placements,
			["placeRate"] = placements > 0 ? 100.0 * rows.Sum(r => r.PlacementsRight) / placements : null,
			["doubles"] = rows.Count(r => r.Joker == Joker.Double),
			["doublePoints"] = rows.Where(r => r.Joker == Joker.Double).Sum(r => r.Points / 2),
			["passed"] = rows.Count(r => !r.Answered),
			["perfect"] = rows.Count(r => r.Right && r.Typos != null) is var spelled and > 0 ? 100.0 * rows.Count(r => r.Right && r.Typos == 0) / spelled : null,
			["avgTypos"] = rows.Where(r => r.Right && r.Typos != null).Select(r => (double?)r.Typos).DefaultIfEmpty(null).Average(),
		};
	}

	/// <summary>
	/// The columns for a filter: what every kind has (played, right, hit rate, Ø points), what this kind's rows actually
	/// have (buzzes, answer times, pins, guesses, hints, placements), and the points from it last.
	/// </summary>
	public static List<Col> ColumnsFor(string filter, IReadOnlyCollection<Results.QuestionRow> rows)
	{
		var cols = new List<Col>
		{
			new("played", "Played", true, "n", "Questions of this kind they played"),
			new("right", "Right", true, "n", "Questions they scored on"),
			new("rate", "Hit rate", true, "rate", "Right, in % of the questions played"),
		};
		if (rows.Any(r => r.Buzzes > 0))
			cols.AddRange(new Col[]
			{
				new("buzzes", "Buzzes", true, "n"),
				new("wrongBuzzes", "Wrong buzzes", false, "n", "Buzzed in and got it wrong"),
				new("buzzRate", "Buzz hit rate", true, "rate", "Right buzzes, in % of all their buzzes"),
				new("avgBuzz", "Ø buzz time", false, "s"),
				new("bestBuzz", "Fastest buzz", false, "s", "Their quickest buzz ever"),
			});
		if (rows.Any(r => r.AnswerS != null))
			cols.AddRange(new Col[]
			{
				new("avgAnswer", "Ø answer time", false, "s", "Until they locked in or sent their recording"),
				new("bestAnswer", "Fastest right", false, "s", "Their quickest right answer"),
			});
		if (rows.Any(r => r.Typos != null))
			cols.AddRange(new Col[]
			{
				new("perfect", "Spelled right", true, "rate", "Right typed answers without a single typo"),
				new("avgTypos", "Ø typos", false, "avg", "Letters off per right typed answer (case and accents don't count)"),
			});
		if (rows.Any(r => r.Km != null))
			cols.AddRange(new Col[]
			{
				new("avgKm", "Ø km off", false, "km"),
				new("bestKm", "Best pin", false, "km", "Their closest pin ever"),
				new("under1km", "Pins < 1 km", true, "n", "Pins less than a kilometre off"),
			});
		if (rows.Any(r => r.PercentOff != null))
			cols.AddRange(new Col[]
			{
				new("avgPct", "Ø % off", false, "off"),
				new("bestPct", "Closest guess", false, "off", "Their closest estimation ever, in % of the right number"),
				new("bullseyes", "Bullseyes", true, "n", "Guesses less than 1 % off"),
			});
		if (rows.Any(r => r.Hint != null))
			cols.AddRange(new Col[]
			{
				new("avgHint", "Ø hint", false, "avg", "The hint they got it at on average (1 = the first, hardest)"),
				new("firstHint", "1st-hint wins", true, "n", "Got it right at the very first hint"),
			});
		if (rows.Any(r => r.Placements > 0))
			cols.AddRange(new Col[]
			{
				new("placements", "Placements", true, "n", "Timeline turns they took"),
				new("placeRate", "Placed right", true, "rate", "Turns that put the item in its right place"),
			});
		cols.Add(new("avgPoints", "Ø points", true, "avg", "Points per question played"));
		cols.Add(new("points", "Points", true, "n", "Points from these questions"));
		return cols;
	}

	public static string Format(string format, double v) => format switch
	{
		"n" => Math.Round(v).ToString("0", System.Globalization.CultureInfo.InvariantCulture),
		"avg" => v.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture),
		"s" => v.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + " s",
		"rate" => v.ToString("0", System.Globalization.CultureInfo.InvariantCulture) + " %",
		"km" => Ui.Screens.NightStatsView.Distance(v),
		"off" => Ui.Screens.NightStatsView.Percent(v),
		_ => v.ToString(System.Globalization.CultureInfo.InvariantCulture),
	};

	/// <summary>The filters there's data for: every type and subtype played, and every category used.</summary>
	public static List<(string Id, string Label)> Filters(IReadOnlyCollection<Results.QuestionRow> rows)
	{
		var list = new List<(string, string)> { ("", Lang.T("All questions")) };
		foreach (var type in QuestionType.All.Where(t => rows.Any(r => r.Type == t)))
		{
			list.Add(("type:" + type, Lang.T(QuestionKinds.TypeLabel(type))));
			var subs = rows.Where(r => r.Type == type && r.Subtype != "").Select(r => r.Subtype).Distinct()
				.OrderBy(s => QuestionKinds.Subtypes(type) is var known && known.Contains(s) ? known.ToList().IndexOf(s) : 99).ThenBy(s => s).ToList();
			if (subs.Count > 1 || subs.Count == 1 && type == QuestionType.MapPin)
				foreach (var s in subs) list.Add(("type:" + QuestionKinds.Key(type, s), "    " + Lang.T(QuestionKinds.SubtypeLabel(type, s))));
		}
		var cats = rows.Where(r => r.Category != "").GroupBy(r => Categories.Key(r.Category)).OrderBy(g => g.Key).ToList();
		foreach (var g in cats) list.Add(("cat:" + g.Key, Lang.F("Category: {0}", Categories.Label(g.First().Category))));
		return list;
	}
}
