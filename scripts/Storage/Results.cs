// Results of finished quiz nights, as CSV (one row per player). The host's game uploads each one to Max's results repo
// (ResultsUpload; only if that fails, it keeps the file in Documents/Quiznight/Results). Max ships them with the game
// (assets/results/*.csv, imported as "keep"): the eternal table adds them all up. Nights played during the beta are in
// assets/results/beta: the beta table, its own (the real one starts with 1.0).

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Godot;
using QuizNight.Server;
using QuizNight.Shared;

namespace QuizNight.Storage;

public static class Results
{
	static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

	public static readonly string[] Columns =
	{
		"date", "quiz", "questions", "players", "place", "player_id", "player", "points", "questions_scored",
		"avg_answer_s", "answers", "avg_km_off", "map_answers", "joined_at", "left_at", "avg_pct_off", "estimate_answers",
		"host", "host_id", "avg_buzz_s", "buzzes", "host_points",
		// (from 0.10.0b: after the players' rows, one row per player and question, with "question" set and the night's
		// own columns empty but date, quiz and player)
		"question", "type", "subtype", "category", "q_points", "right", "answered", "q_buzzes", "wrong_buzzes", "buzz_s",
		"answer_s", "km_off", "pct_off", "hint", "joker", "placements", "placements_right", "typos",
	};

	/// <summary>Where the host's game keeps results it couldn't upload.</summary>
	public static string Folder => Path.Combine(OS.GetSystemDir(OS.SystemDir.Documents), "Quiznight", "Results");

	/// <summary>The bundled results (sent in by the host after quiz nights) the eternal table is made from.</summary>
	const string Bundled = "res://assets/results";

	/// <summary>A finished room as CSV: one row per player, in order of their place.</summary>
	/// <param name="host">Who hosted (their name and player id, e.g. "steam:…"), for the match history.</param>
	public static string ToCsv(Room room, DateTime when, (string Name, string Id)? host = null)
	{
		var (order, places) = room.Ranking();
		var sb = new StringBuilder(string.Join(",", Columns) + "\n");
		var date = when.ToString("yyyy-MM-dd HH:mm", Inv);
		static string N(double? v) => v is { } d ? d.ToString("0.000", Inv) : "";
		void Row(Dictionary<string, string> cells) =>
			sb.Append(string.Join(",", Columns.Select(c => Quote(cells.GetValueOrDefault(c, ""))))).Append('\n');
		foreach (var id in order)
		{
			var p = room.GetPlayer(id)!;
			var s = room.StatsOf(id);
			Row(new()
			{
				["date"] = date, ["quiz"] = room.Quiz.Title, ["questions"] = room.Quiz.Questions.Count.ToString(Inv), ["players"] = order.Count.ToString(Inv),
				["place"] = places[id].ToString(Inv), ["player_id"] = p.Id, ["player"] = p.Name, ["points"] = room.Score(id).ToString(Inv),
				["questions_scored"] = s.Scored.ToString(Inv), ["avg_answer_s"] = N(s.AvgAnswerMs / 1000), ["answers"] = s.Answers.ToString(Inv),
				["avg_km_off"] = N(s.AvgKm), ["map_answers"] = s.KmAnswers.ToString(Inv),
				["joined_at"] = p.JoinedAt?.ToString(Inv) ?? "", ["left_at"] = p.LeftAt?.ToString(Inv) ?? "",
				["avg_pct_off"] = N(s.AvgPercentOff), ["estimate_answers"] = s.PercentAnswers.ToString(Inv),
				["host"] = host?.Name ?? "", ["host_id"] = host?.Id ?? "",
				["avg_buzz_s"] = N(s.AvgBuzzMs / 1000), ["buzzes"] = s.Buzzes.ToString(Inv), ["host_points"] = s.HostPoints.ToString(Inv),
			});
		}
		foreach (var ((index, id), q) in room.QuestionStats.OrderBy(x => x.Key.Question).ThenBy(x => order.IndexOf(x.Key.Player)))
		{
			if (room.GetPlayer(id) is not { } p || index >= room.Quiz.Questions.Count) continue;
			var question = room.Quiz.Questions[index];
			Row(new()
			{
				["date"] = date, ["quiz"] = room.Quiz.Title, ["player_id"] = p.Id, ["player"] = p.Name,
				["question"] = (index + 1).ToString(Inv), ["type"] = question.Type, ["subtype"] = QuestionKinds.Subtype(question, room.Quiz),
				["category"] = question.Category, ["q_points"] = q.Points.ToString(Inv), ["right"] = q.Right ? "1" : "0",
				["answered"] = q.Answered ? "1" : "0", ["q_buzzes"] = q.Buzzes.ToString(Inv), ["wrong_buzzes"] = q.WrongBuzzes.ToString(Inv),
				["buzz_s"] = N(q.BuzzMs / 1000.0), ["answer_s"] = N(q.AnswerMs / 1000.0), ["km_off"] = N(q.Km), ["pct_off"] = N(q.PercentOff),
				["hint"] = q.Hint?.ToString(Inv) ?? "", ["joker"] = q.Joker,
				["placements"] = q.Placements.ToString(Inv), ["placements_right"] = q.PlacementsRight.ToString(Inv),
				["typos"] = q.Typos?.ToString(Inv) ?? "",
			});
		}
		return sb.ToString();
	}

	/// <summary>A finished room's results: a file name and the CSV.</summary>
	public static (string Name, string Csv) Make(Room room, (string Name, string Id)? host = null)
	{
		var now = DateTime.Now;
		var title = new string(room.Quiz.Title.Select(c => char.IsLetterOrDigit(c) ? c : '-').ToArray()).Trim('-');
		return ($"{now:yyyy-MM-dd_HH-mm} {(title == "" ? "quiz" : title)}.csv", ToCsv(room, now, host));
	}

	/// <summary>Keeps a results file in the Results folder; returns its path (null if the disk said no).</summary>
	public static string? Save(string name, string csv)
	{
		try
		{
			Directory.CreateDirectory(Folder);
			var path = Path.Combine(Folder, Path.GetFileName(name));
			File.WriteAllText(path, csv);
			return path;
		}
		catch (Exception e) when (e is IOException or UnauthorizedAccessException)
		{
			GD.PrintErr($"Couldn't save the results: {e.Message}");
			return null;
		}
	}

	static string Quote(string s) => s.IndexOfAny(new[] { ',', '"', '\n' }) >= 0 ? $"\"{s.Replace("\"", "\"\"")}\"" : s;

	/// <summary>A player's line for the whole night (not one of the per-question rows).</summary>
	static bool IsNightRow(Dictionary<string, string> row) => row.GetValueOrDefault("question", "") == "";

	/// <summary>The rows of a results CSV as column → value.</summary>
	public static List<Dictionary<string, string>> Parse(string csv)
	{
		var lines = SplitRows(csv);
		if (lines.Count == 0) return new();
		var header = lines[0];
		return lines.Skip(1).Where(r => r.Count == header.Count)
			.Select(r => header.Zip(r).ToDictionary(x => x.First.Trim(), x => x.Second)).ToList();
	}

	static List<List<string>> SplitRows(string csv)
	{
		var rows = new List<List<string>>();
		var row = new List<string>();
		var cell = new StringBuilder();
		var quoted = false;
		for (var i = 0; i < csv.Length; i++)
		{
			var c = csv[i];
			if (quoted)
			{
				if (c == '"' && i + 1 < csv.Length && csv[i + 1] == '"') { cell.Append('"'); i++; }
				else if (c == '"') quoted = false;
				else cell.Append(c);
			}
			else if (c == '"') quoted = true;
			else if (c == ',') { row.Add(cell.ToString()); cell.Clear(); }
			else if (c == '\n' || c == '\r')
			{
				if (c == '\r' && i + 1 < csv.Length && csv[i + 1] == '\n') i++;
				row.Add(cell.ToString());
				cell.Clear();
				if (row.Count > 1 || row[0] != "") rows.Add(row);
				row = new List<string>();
			}
			else cell.Append(c);
		}
		if (cell.Length > 0 || row.Count > 0)
		{
			row.Add(cell.ToString());
			rows.Add(row);
		}
		return rows;
	}

	/// <summary>One quiz night, for the match history: when, which quiz, who hosted, everyone's place and points.</summary>
	public sealed class Night
	{
		public string Date = "";
		public string Quiz = "";
		/// <summary>Empty for nights before 0.9.4b (they didn't record the host).</summary>
		public string Host = "";
		public string HostId = "";
		public bool Beta;
		public readonly List<NightPlayer> Players = new();
	}

	/// <summary>One player's line of a night: place, points and their stats (null where the night didn't record it).</summary>
	public sealed record NightPlayer(int Place, string Id, string Name, int Points, int Scored, double? AvgAnswerS, double? AvgKm, double? AvgPercentOff,
		double? AvgBuzzS = null);

	/// <summary>Every night in these results, newest first (the same night twice counts once).</summary>
	public static List<Night> Nights(IEnumerable<string> csvs, bool beta = false)
	{
		var nights = new Dictionary<string, Night>();
		var seen = new HashSet<string>();
		foreach (var row in csvs.SelectMany(Parse).Where(IsNightRow))
		{
			var date = row.GetValueOrDefault("date", "");
			var quiz = row.GetValueOrDefault("quiz", "");
			var id = row.GetValueOrDefault("player_id", "");
			if (date == "" || !seen.Add($"{date}|{quiz}|{id}")) continue;
			var key = $"{date}|{quiz}";
			if (!nights.TryGetValue(key, out var night)) nights[key] = night = new Night { Date = date, Quiz = quiz.Trim(), Beta = beta };
			if (row.GetValueOrDefault("host", "") is { Length: > 0 } host)
			{
				night.Host = host;
				night.HostId = row.GetValueOrDefault("host_id", "");
			}
			int I(string k) => int.TryParse(row.GetValueOrDefault(k), NumberStyles.Integer, Inv, out var v) ? v : 0;
			double? D(string k) => double.TryParse(row.GetValueOrDefault(k), NumberStyles.Float, Inv, out var v) ? v : null;
			night.Players.Add(new NightPlayer(I("place"), id, row.GetValueOrDefault("player", ""), I("points"), I("questions_scored"),
				D("avg_answer_s"), D("avg_km_off"), D("avg_pct_off"), D("avg_buzz_s")));
		}
		foreach (var n in nights.Values) n.Players.Sort((a, b) => a.Place.CompareTo(b.Place));
		return nights.Values.OrderByDescending(n => n.Date).ToList();
	}

	/// <summary>One player across all quiz nights.</summary>
	public sealed class Career
	{
		public string Id = "";
		public string Name = "";
		public int Nights, Wins, Podiums, Points, Scored, Answers, MapAnswers, EstimateAnswers, Buzzes;
		/// <summary>Points the host handed them (bonuses and score changes by hand; from 0.10.0b on).</summary>
		public int HostPoints;
		public double AnswerSeconds, Km, PercentOff, BuzzSeconds;
		/// <summary>Every night's place, oldest first (for championship points and the form).</summary>
		public readonly List<int> Places = new();
		public double? AvgBuzzS => Buzzes > 0 ? BuzzSeconds / Buzzes : null;
		/// <summary>Championship points: each night's place scored like a race (e.g. F1: 25 for a win, 18 for 2nd …).</summary>
		public int ChampionshipPoints(IReadOnlyList<int> perPlace) => Places.Sum(p => p >= 1 && p <= perPlace.Count ? perPlace[p - 1] : 0);
		/// <summary>Estimations: average % off (only nights from 0.9.3b on recorded it).</summary>
		public double? AvgPercentOff => EstimateAnswers > 0 ? PercentOff / EstimateAnswers : null;
		public double? AvgAnswerS => Answers > 0 ? AnswerSeconds / Answers : null;
		public double? AvgKm => MapAnswers > 0 ? Km / MapAnswers : null;
		public double AvgPoints => Nights > 0 ? (double)Points / Nights : 0;
	}

	/// <summary>
	/// The eternal table from result CSVs: everyone's nights added up (by player id, so a renamed player stays one
	/// player; the newest name shows). Most points first, then the faster average answer time.
	/// </summary>
	/// <param name="before">Only nights before this date ("yyyy-MM-dd HH:mm"): the table as it was then.</param>
	/// <param name="from">Only nights from this date on (a season).</param>
	public static List<Career> Table(IEnumerable<string> csvs, string? before = null, string? from = null)
	{
		var careers = new Dictionary<string, Career>();
		// (the same night can come in twice: from the host's upload and from a collector's copy)
		var seen = new HashSet<string>();
		foreach (var row in csvs.SelectMany(Parse).Where(IsNightRow).OrderBy(r => r.GetValueOrDefault("date")))
		{
			if (!InRange(row.GetValueOrDefault("date", ""), from, before)) continue;
			var id = row.GetValueOrDefault("player_id", "");
			if (id == "" || !seen.Add($"{row.GetValueOrDefault("date")}|{row.GetValueOrDefault("quiz")}|{id}")) continue;
			if (!careers.TryGetValue(id, out var c)) careers[id] = c = new Career { Id = id };
			int I(string k) => int.TryParse(row.GetValueOrDefault(k), NumberStyles.Integer, Inv, out var v) ? v : 0;
			double? D(string k) => double.TryParse(row.GetValueOrDefault(k), NumberStyles.Float, Inv, out var v) ? v : null;
			c.Name = row.GetValueOrDefault("player", c.Name);
			c.Nights++;
			var place = I("place");
			if (place == 1) c.Wins++;
			if (place is >= 1 and <= 3) c.Podiums++;
			c.Places.Add(place);
			c.Points += I("points");
			c.HostPoints += I("host_points");
			c.Scored += I("questions_scored");
			if (D("avg_answer_s") is { } s && I("answers") is var n and > 0)
			{
				c.AnswerSeconds += s * n;
				c.Answers += n;
			}
			if (D("avg_buzz_s") is { } b && I("buzzes") is var nb and > 0)
			{
				c.BuzzSeconds += b * nb;
				c.Buzzes += nb;
			}
			if (D("avg_km_off") is { } km && I("map_answers") is var m and > 0)
			{
				c.Km += km * m;
				c.MapAnswers += m;
			}
			if (D("avg_pct_off") is { } pct && I("estimate_answers") is var e and > 0)
			{
				c.PercentOff += pct * e;
				c.EstimateAnswers += e;
			}
		}
		return careers.Values.OrderByDescending(c => c.Points).ThenBy(c => c.AvgAnswerS ?? double.MaxValue)
			.ThenBy(c => c.AvgBuzzS ?? double.MaxValue).ThenBy(c => c.Name).ToList();
	}

	/// <summary>
	/// The "Host's Favourite Puppy": whoever the hosts handed the most points to (bonuses and score changes by hand).
	/// Null when nobody got any yet; a tie goes to whoever is higher in the table.
	/// </summary>
	public static string? FavouritePuppy(IEnumerable<Career> table) =>
		table.Where(c => c.HostPoints > 0).OrderByDescending(c => c.HostPoints).FirstOrDefault()?.Id;

	static bool InRange(string date, string? from, string? before) =>
		(from == null || string.CompareOrdinal(date, from) >= 0) && (before == null || string.CompareOrdinal(date, before) < 0);

	/// <summary>
	/// One player's go at one question (from 0.10.0b on): its type and subtype, category, points, and what they did.
	/// </summary>
	public sealed record QuestionRow(string Date, string Quiz, int Question, string PlayerId, string Player, string Type, string Subtype,
		string Category, int Points, bool Right, bool Answered, int Buzzes, int WrongBuzzes, double? BuzzS, double? AnswerS, double? Km,
		double? PercentOff, int? Hint, string Joker, int Placements, int PlacementsRight, int? Typos = null);

	/// <summary>Every per-question row in these results (the same night twice counts once), oldest first.</summary>
	public static List<QuestionRow> Questions(IEnumerable<string> csvs, string? from = null, string? before = null)
	{
		var seen = new HashSet<string>();
		var rows = new List<QuestionRow>();
		foreach (var row in csvs.SelectMany(Parse).Where(r => !IsNightRow(r)))
		{
			var date = row.GetValueOrDefault("date", "");
			var id = row.GetValueOrDefault("player_id", "");
			int I(string k) => int.TryParse(row.GetValueOrDefault(k), NumberStyles.Integer, Inv, out var v) ? v : 0;
			double? D(string k) => double.TryParse(row.GetValueOrDefault(k), NumberStyles.Float, Inv, out var v) ? v : null;
			var quiz = row.GetValueOrDefault("quiz", "");
			if (id == "" || !InRange(date, from, before) || !seen.Add($"{date}|{quiz}|{id}|{I("question")}")) continue;
			// (nights before 0.10.3b had "Write the answer" as its own type: it counts as classic, everyone types)
			var (type, subtype) = (row.GetValueOrDefault("type", ""), row.GetValueOrDefault("subtype", ""));
			if (type == QuestionType.Write) (type, subtype) = (QuestionType.Classic, QuestionKinds.Typed);
			rows.Add(new QuestionRow(date, quiz.Trim(), I("question"), id, row.GetValueOrDefault("player", ""), type,
				subtype, row.GetValueOrDefault("category", ""), I("q_points"), I("right") == 1, I("answered") == 1,
				I("q_buzzes"), I("wrong_buzzes"), D("buzz_s"), D("answer_s"), D("km_off"), D("pct_off"),
				int.TryParse(row.GetValueOrDefault("hint"), NumberStyles.Integer, Inv, out var h) ? h : null, row.GetValueOrDefault("joker", ""),
				I("placements"), I("placements_right"),
				int.TryParse(row.GetValueOrDefault("typos"), NumberStyles.Integer, Inv, out var t) ? t : null));
		}
		return rows.OrderBy(r => r.Date).ThenBy(r => r.Question).ToList();
	}

	/// <summary>
	/// The seasons of the table from 1.0 on: the first runs from 1.0 to the end of 2027, then one per calendar year.
	/// (From, Before) dates as the CSVs write them; null = open.
	/// </summary>
	public static List<(string Id, string Label, string? From, string? Before)> Seasons(IEnumerable<string> csvs)
	{
		var years = csvs.SelectMany(Parse).Where(IsNightRow).Select(r => r.GetValueOrDefault("date", ""))
			.Where(d => d.Length >= 4 && int.TryParse(d[..4], out _)).Select(d => int.Parse(d[..4])).ToList();
		var seasons = new List<(string, string, string?, string?)> { ("1", Lang.F("Season {0}", 1) + " (1.0 – 2027)", null, "2028-01-01") };
		var last = years.Count > 0 ? Math.Max(years.Max(), DateTime.Now.Year) : DateTime.Now.Year;
		for (int y = 2028, n = 2; y <= last; y++, n++) seasons.Add(($"{n}", Lang.F("Season {0}", n) + $" ({y})", $"{y}-01-01", $"{y + 1}-01-01"));
		return seasons;
	}

	/// <summary>
	/// The results shipped with the game: from 1.0 on (assets/results), or the beta's (beta: assets/results/beta, its own
	/// table, kept to look back at).
	/// </summary>
	/// <summary>The date of the newest night in these CSVs (null: none).</summary>
	public static string? LatestDate(IEnumerable<string> csvs) =>
		csvs.SelectMany(Parse).Where(IsNightRow).Select(r => r.GetValueOrDefault("date", "")).Where(d => d != "").DefaultIfEmpty().Max(StringComparer.Ordinal);

	/// <summary>Dev runs (--fake-results): made-up nights shown as the tables from 1.0 on, instead of the real ones.</summary>
	public static List<string>? Fake;

	public static List<string> BundledCsvs(bool beta = false)
	{
		if (Fake != null) return beta ? new() : Fake;
		var dir = beta ? Bundled + "/beta" : Bundled;
		if (!DirAccess.DirExistsAbsolute(dir)) return new();
		var files = DirAccess.GetFilesAt(dir);
		return files.Where(f => f.EndsWith(".csv")).Select(f => Godot.FileAccess.GetFileAsString($"{dir}/{f}")).ToList();
	}
}
