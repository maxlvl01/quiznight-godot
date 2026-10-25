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

namespace QuizNight.Storage;

public static class Results
{
	static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

	public static readonly string[] Columns =
	{
		"date", "quiz", "questions", "players", "place", "player_id", "player", "points", "questions_scored",
		"avg_answer_s", "answers", "avg_km_off", "map_answers", "joined_at", "left_at", "avg_pct_off", "estimate_answers",
	};

	/// <summary>Where the host's game keeps results it couldn't upload.</summary>
	public static string Folder => Path.Combine(OS.GetSystemDir(OS.SystemDir.Documents), "Quiznight", "Results");

	/// <summary>The bundled results (sent in by the host after quiz nights) the eternal table is made from.</summary>
	const string Bundled = "res://assets/results";

	/// <summary>A finished room as CSV: one row per player, in order of their place.</summary>
	public static string ToCsv(Room room, DateTime when)
	{
		var (order, places) = room.Ranking();
		var sb = new StringBuilder(string.Join(",", Columns) + "\n");
		foreach (var id in order)
		{
			var p = room.GetPlayer(id)!;
			var s = room.StatsOf(id);
			sb.Append(string.Join(",", new[]
			{
				when.ToString("yyyy-MM-dd HH:mm", Inv), Quote(room.Quiz.Title), room.Quiz.Questions.Count.ToString(Inv), order.Count.ToString(Inv),
				places[id].ToString(Inv), Quote(p.Id), Quote(p.Name), room.Score(id).ToString(Inv), s.Scored.ToString(Inv),
				s.AvgAnswerMs is { } ms ? (ms / 1000).ToString("0.000", Inv) : "", s.Answers.ToString(Inv),
				s.AvgKm is { } km ? km.ToString("0.000", Inv) : "", s.KmAnswers.ToString(Inv),
				p.JoinedAt?.ToString(Inv) ?? "", p.LeftAt?.ToString(Inv) ?? "",
				s.AvgPercentOff is { } pct ? pct.ToString("0.000", Inv) : "", s.PercentAnswers.ToString(Inv),
			})).Append('\n');
		}
		return sb.ToString();
	}

	/// <summary>A finished room's results: a file name and the CSV.</summary>
	public static (string Name, string Csv) Make(Room room)
	{
		var now = DateTime.Now;
		var title = new string(room.Quiz.Title.Select(c => char.IsLetterOrDigit(c) ? c : '-').ToArray()).Trim('-');
		return ($"{now:yyyy-MM-dd_HH-mm} {(title == "" ? "quiz" : title)}.csv", ToCsv(room, now));
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

	/// <summary>One player across all quiz nights.</summary>
	public sealed class Career
	{
		public string Id = "";
		public string Name = "";
		public int Nights, Wins, Podiums, Points, Scored, Answers, MapAnswers, EstimateAnswers;
		public double AnswerSeconds, Km, PercentOff;
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
	public static List<Career> Table(IEnumerable<string> csvs)
	{
		var careers = new Dictionary<string, Career>();
		// (the same night can come in twice: from the host's upload and from a collector's copy)
		var seen = new HashSet<string>();
		foreach (var row in csvs.SelectMany(Parse).OrderBy(r => r.GetValueOrDefault("date")))
		{
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
			c.Points += I("points");
			c.Scored += I("questions_scored");
			if (D("avg_answer_s") is { } s && I("answers") is var n and > 0)
			{
				c.AnswerSeconds += s * n;
				c.Answers += n;
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
		return careers.Values.OrderByDescending(c => c.Points).ThenBy(c => c.AvgAnswerS ?? double.MaxValue).ThenBy(c => c.Name).ToList();
	}

	/// <summary>
	/// The results shipped with the game: from 1.0 on (assets/results), or the beta's (beta: assets/results/beta, its own
	/// table, kept to look back at).
	/// </summary>
	public static List<string> BundledCsvs(bool beta = false)
	{
		var dir = beta ? Bundled + "/beta" : Bundled;
		if (!DirAccess.DirExistsAbsolute(dir)) return new();
		var files = DirAccess.GetFilesAt(dir);
		return files.Where(f => f.EndsWith(".csv")).Select(f => Godot.FileAccess.GetFileAsString($"{dir}/{f}")).ToList();
	}
}
