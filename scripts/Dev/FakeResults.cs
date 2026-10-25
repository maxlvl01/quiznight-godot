// Dev runs only (--fake-results [N]): N made-up quiz nights, played through a real Room with random but player-specific
// skill, so the Ewige Tabelle, its filters, the seasons and the profile pages can be looked at before real nights with
// per-question results exist. Never written anywhere: Results.BundledCsvs returns them for this run instead.

using System;
using System.Collections.Generic;
using System.Linq;
using QuizNight.Server;
using QuizNight.Shared;

namespace QuizNight.Dev;

public static class FakeResults
{
	static readonly string[] Names = { "Ann", "[OTB] maxlvl01", "Bob", "Cat", "Dan", "Eve", "Finn" };

	public static List<string> Make(int nights, int seed = 7)
	{
		var rng = new Random(seed);
		// (everyone is good at some things: a skill per player and question type, plus a buzz speed)
		var skill = Names.ToDictionary(n => n, _ => QuestionType.All.ToDictionary(t => t, _ => 0.15 + rng.NextDouble() * 0.75));
		var speed = Names.ToDictionary(n => n, _ => 0.6 + rng.NextDouble() * 1.4);
		var csvs = new List<string>();
		var date = new DateTime(2026, 11, 6, 20, 0, 0);
		for (var night = 0; night < nights; night++)
		{
			var quiz = SampleQuiz.Build();
			quiz.Title = new[] { "Gaming Night", "Allgemeinwissen", "Mixed Bag", "Musik & Film", "Geo Special" }[night % 5];
			foreach (var q in quiz.Questions)
			{
				q.Category = Categories.Presets[rng.Next(Categories.Presets.Length)];
				if (q.CanBeTyped && q.Type != QuestionType.Steps) q.Typed = rng.Next(3) == 0;
			}
			var players = Names.Where(_ => rng.Next(5) > 0).Take(6).ToList();
			if (players.Count < 3) players = Names.Take(4).ToList();
			var room = new Room("FAKE", quiz);
			room.UpdateSettings(hostShows: false, hostUnlocks: false);
			foreach (var p in players) room.Join("fake:" + p, p);
			room.Start();
			for (var i = 0; i < quiz.Questions.Count; i++)
			{
				var q = quiz.Questions[i];
				double Skill(string p) => skill[p][q.Type];
				bool Knows(string p) => rng.NextDouble() < Skill(p);
				string Id(string p) => "fake:" + p;
				if (q.IsBuzzer)
				{
					var buzzers = players.Where(p => rng.NextDouble() < Skill(p) + 0.2).OrderBy(p => speed[p] * (1.2 - Skill(p)) * rng.NextDouble()).ToList();
					foreach (var p in buzzers)
					{
						room.NoteBuzzTime(Id(p), (long)(400 + 5000 * speed[p] * rng.NextDouble() * (1.3 - Skill(p))));
						if (Knows(p))
						{
							var worth = q.IsSteps ? Math.Max(1, q.FilledHints.Count - rng.Next(q.FilledHints.Count)) : q.Points;
							room.Award(Id(p), worth);
							room.NoteScored(Id(p));
							if (q.IsSteps) room.QuestionStats[(i, Id(p))].Hint = q.FilledHints.Count - worth + 1;
							break;
						}
						room.NoteWrongBuzz(Id(p));
					}
				}
				else if (q.IsCloseness)
				{
					var off = players.ToDictionary(p => p, p => Math.Pow(rng.NextDouble(), 2) * (1.4 - Skill(p)) * (q.Type == QuestionType.MapPin ? 6 : 60));
					foreach (var p in players)
					{
						room.NoteAnswerTime(Id(p), (long)(3000 + 40000 * rng.NextDouble()));
						if (q.Type == QuestionType.MapPin) room.NoteDistance(Id(p), off[p]);
						else room.NotePercentOff(Id(p), off[p]);
					}
					var best = off.OrderBy(o => o.Value).First().Key;
					room.Award(Id(best), q.Points);
					room.NoteScored(Id(best));
				}
				else if (q.IsTimeline)
					foreach (var p in players)
						for (var t = rng.Next(1, 3); t > 0; t--)
						{
							var right = Knows(p);
							room.NotePlacement(Id(p), right);
							if (right) room.Award(Id(p), q.Points);
						}
				else
					foreach (var p in players)
					{
						room.NoteAnswerTime(Id(p), (long)(2000 + 25000 * speed[p] * rng.NextDouble()));
						if (q.IsWritten) room.NoteTypos(Id(p), rng.NextDouble() < skill[p][QuestionType.Write] ? 0 : rng.Next(1, 3));
						if (!Knows(p)) continue;
						room.Award(Id(p), q.IsInteractive ? rng.Next(1, 3) : q.Points);
						room.NoteScored(Id(p));
					}
				// (now and then the host hands someone a point)
				if (rng.Next(6) == 0) room.AdjustScore(Id(players[rng.Next(players.Count)]), 1);
				room.Next();
				// (a Double now and then, played on this question)
				foreach (var p in players.Where(_ => rng.Next(12) == 0))
					if (room.QuestionStats.TryGetValue((i, Id(p)), out var s)) s.Joker = Joker.Double;
				room.Next();
			}
			var host = Names[rng.Next(Names.Length)];
			csvs.Add(Storage.Results.ToCsv(room, date, (host, "fake:" + host)));
			date = date.AddDays(5 + rng.Next(20));
		}
		return csvs;
	}
}
