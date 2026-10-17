// One running quiz: question flow, buzz queue, judging, answers, timers, the media play clock,
// reveal and scoring. Runs on the host. Nothing here talks to the network.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using QuizNight.Shared;

namespace QuizNight.Server;

public interface IGameHooks
{
	/// <summary>Players currently connected (they are the ones we wait for).</summary>
	IReadOnlyList<string> ActivePlayerIds();
	int AnswerTimeSec();
	void Award(string playerId, int points);
	long Now();
}

public sealed class Game
{
	sealed class Buzz
	{
		public string PlayerId = "";
		public string Status = BuzzStatus.Waiting;
		public long AtMs;
	}

	public int Index { get; private set; }
	public string Stage { get; private set; } = Shared.Stage.Open;
	/// <summary>Past the last question: the room shows the podium.</summary>
	public bool Finished { get; private set; }

	readonly Quiz _quiz;
	readonly IGameHooks _hooks;
	long _openedAt;
	long? _endsAt;
	// Insertion order matters: it is the order answers are listed in.
	readonly List<(string PlayerId, AnswerValue Value)> _answers = new();
	readonly List<Buzz> _buzzes = new();
	long? _answeringSince;
	/// <summary>Sound/video play clock: only counts while the host's player is actually playing.</summary>
	long _playedMs;
	long? _playingSince;
	RevealView? _reveal;

	public Game(Quiz quiz, IGameHooks hooks)
	{
		_quiz = quiz;
		_hooks = hooks;
		Open(0);
	}

	public Question Question => _quiz.Questions[Index];

	static int? TimeLimitSec(Question q) => q.IsCloseness || q.IsChoice ? q.TimeLimitSec : null;

	void Open(int index)
	{
		var now = _hooks.Now();
		Index = index;
		Stage = Shared.Stage.Open;
		_openedAt = now;
		_answers.Clear();
		_buzzes.Clear();
		_answeringSince = null;
		_playedMs = 0;
		_playingSince = null;
		_reveal = null;
		var limit = TimeLimitSec(Question);
		_endsAt = limit is { } l ? now + l * 1000L : null;
	}

	/// <summary>Runs timers. Returns true if something changed (the time ran out).</summary>
	public bool Tick()
	{
		if (Stage != Shared.Stage.Open || _endsAt is not { } ends || _hooks.Now() < ends) return false;
		DoReveal();
		return true;
	}

	public void Next()
	{
		if (Index + 1 >= _quiz.Questions.Count) Finished = true;
		else Open(Index + 1);
	}

	long PlayTimeMs(long now) => _playedMs + (_playingSince is { } since ? now - since : 0);

	public bool SetMedia(bool playing)
	{
		var now = _hooks.Now();
		if (playing && _playingSince == null)
		{
			_playingSince = now;
			return true;
		}
		if (!playing && _playingSince is { } since)
		{
			_playedMs += now - since;
			_playingSince = null;
			return true;
		}
		return false;
	}

	public bool BuzzIn(string playerId)
	{
		var q = Question;
		if (Stage != Shared.Stage.Open || !q.IsBuzzer || _buzzes.Any(b => b.PlayerId == playerId)) return false;
		var now = _hooks.Now();
		var someoneAnswering = _buzzes.Any(b => b.Status == BuzzStatus.Answering);
		_buzzes.Add(new Buzz
		{
			PlayerId = playerId,
			Status = someoneAnswering ? BuzzStatus.Waiting : BuzzStatus.Answering,
			AtMs = q.IsMedia ? PlayTimeMs(now) : now - _openedAt,
		});
		if (!someoneAnswering) _answeringSince = now;
		return true;
	}

	/// <summary>Mark the player who is answering after a buzz right or wrong.</summary>
	public bool Judge(bool correct)
	{
		var current = _buzzes.FirstOrDefault(b => b.Status == BuzzStatus.Answering);
		if (Stage != Shared.Stage.Open || current == null) return false;
		if (correct)
		{
			current.Status = BuzzStatus.Correct;
			DoReveal();
			return true;
		}
		current.Status = BuzzStatus.Wrong;
		var nextUp = _buzzes.FirstOrDefault(b => b.Status == BuzzStatus.Waiting);
		if (nextUp != null) nextUp.Status = BuzzStatus.Answering;
		_answeringSince = nextUp != null ? _hooks.Now() : null;
		return true;
	}

	public Result Answer(string playerId, int index, AnswerValue? raw)
	{
		var q = Question;
		if (index != Index || Stage != Shared.Stage.Open) return Result.Fail("This question is already closed.");
		if (q.IsBuzzer) return Result.Fail("This is a buzzer question.");
		if (_answers.Any(a => a.PlayerId == playerId)) return Result.Fail("You already locked in your answer.");

		AnswerValue value;
		if (q.Type == QuestionType.Estimation)
		{
			if (raw?.Num is not { } n || !double.IsFinite(n)) return Result.Fail("Please enter a number.");
			value = AnswerValue.Number(n);
		}
		else if (q.Type == QuestionType.Classic)
		{
			if (raw?.Num is not { } n || n != Math.Floor(n) || n < 0 || n >= q.Choices.Count || string.IsNullOrWhiteSpace(q.Choices[(int)n]))
				return Result.Fail("Pick one of the answers.");
			value = AnswerValue.Number(n);
		}
		else if (q.Type == QuestionType.MapPin)
		{
			if (raw?.Point is not { } p || !double.IsFinite(p.X) || !double.IsFinite(p.Y)) return Result.Fail("Place your pin on the map.");
			value = AnswerValue.At(new NormPoint(Math.Clamp(p.X, 0, 1), Math.Clamp(p.Y, 0, 1)));
		}
		else return Result.Fail("This question takes no answers.");

		_answers.Add((playerId, value));
		if (_hooks.ActivePlayerIds().All(id => _answers.Any(a => a.PlayerId == id))) DoReveal();
		return Result.Success();
	}

	/// <summary>Someone left or reconnected: maybe everyone who is still here has answered.</summary>
	public bool CheckAllAnswered()
	{
		if (Stage != Shared.Stage.Open || Question.IsBuzzer || _answers.Count == 0) return false;
		if (!_hooks.ActivePlayerIds().All(id => _answers.Any(a => a.PlayerId == id))) return false;
		DoReveal();
		return true;
	}

	/// <summary>The host reveals the answer (nobody knows it, or no need to wait for the timer).</summary>
	public bool HostReveal()
	{
		if (Stage != Shared.Stage.Open) return false;
		DoReveal();
		return true;
	}

	void DoReveal()
	{
		if (Stage == Shared.Stage.Reveal) return;
		SetMedia(false);
		Stage = Shared.Stage.Reveal;
		_reveal = ComputeReveal();
		foreach (var e in _reveal.Entries)
			if (e.Points != 0) _hooks.Award(e.PlayerId, e.Points);
	}

	static readonly CultureInfo En = CultureInfo.GetCultureInfo("en-GB");

	/// <summary>Like toLocaleString('en-GB', { maximumFractionDigits: 2 }).</summary>
	public static string Fmt(double n) => n.ToString("#,0.##", En);

	public static string FormatDistance(double? km, double percent)
	{
		if (km is not { } k) return $"{percent.ToString("0.0", En)} % of the map";
		var m = Math.Round(k * 1000);
		return m < 1000 ? $"{m.ToString("0", En)} m" : $"{k.ToString(k < 10 ? "0.00" : "0.0", En)} km";
	}

	/// <summary>
	/// Ranks players by distance (smaller is better) and hands out points.
	/// Ties share a rank ("1, 1, 3"); nobodyScores gives tied players nothing at that rank.
	/// </summary>
	public static Dictionary<string, (int Rank, int Points)> ClosenessPoints(
		IReadOnlyList<(string PlayerId, double Dist)> dists, int points, string scoring, string tieRule)
	{
		var sorted = dists.OrderBy(d => d.Dist).ToList();
		var result = new Dictionary<string, (int, int)>();
		foreach (var d in sorted)
		{
			var rank = sorted.FindIndex(o => o.Dist == d.Dist) + 1;
			var tied = sorted.Count(o => o.Dist == d.Dist) > 1;
			var pts = 0;
			if (scoring == Scoring.ClosestWins && rank == 1) pts = points;
			if (scoring == Scoring.Top3 && rank <= 3) pts = (4 - rank) * points;
			if (tied && tieRule == TieRule.NobodyScores) pts = 0;
			result[d.PlayerId] = (rank, pts);
		}
		return result;
	}

	RevealView ComputeReveal()
	{
		var q = Question;
		if (q.IsBuzzer)
		{
			var winner = _buzzes.FirstOrDefault(b => b.Status == BuzzStatus.Correct);
			return new RevealView
			{
				AnswerText = q.AnswerText,
				WinnerIds = winner != null ? new List<string> { winner.PlayerId } : new List<string>(),
				GuessTimeSec = q.IsMedia && winner != null ? winner.AtMs / 1000.0 : null,
				Entries = _buzzes.Select(b => new RevealEntry
				{
					PlayerId = b.PlayerId,
					Text = b.Status == BuzzStatus.Correct ? "Correct" : b.Status == BuzzStatus.Wrong ? "Wrong" : "Buzzed, no turn",
					Correct = b.Status == BuzzStatus.Correct,
					Points = b.Status == BuzzStatus.Correct ? q.Points : 0,
				}).ToList(),
			};
		}

		if (q.Type == QuestionType.Classic)
		{
			var counts = new int[q.Choices.Count];
			var entries = _answers.Select(a =>
			{
				var i = (int)a.Value.Num!.Value;
				counts[i]++;
				var correct = i == q.CorrectChoice;
				return new RevealEntry
				{
					PlayerId = a.PlayerId,
					Value = a.Value,
					Text = $"{QuizRules.ChoiceLetters[i]}: {q.Choices[i]}",
					Correct = correct,
					Points = correct ? q.Points : 0,
				};
			}).ToList();
			return new RevealView
			{
				AnswerText = ChoiceText(q),
				WinnerIds = entries.Where(e => e.Correct).Select(e => e.PlayerId).ToList(),
				Entries = entries,
				Counts = counts,
				CorrectIndex = q.CorrectChoice,
			};
		}

		if (q.Type == QuestionType.Estimation)
		{
			var answer = q.AnswerNumber ?? 0;
			var unit = q.Unit != "" ? " " + q.Unit : "";
			var ranks = ClosenessPoints(_answers.Select(a => (a.PlayerId, Math.Abs(a.Value.Num!.Value - answer))).ToList(), q.Points, q.Scoring, q.TieRule);
			var entries = _answers.Select(a =>
			{
				var n = a.Value.Num!.Value;
				var diff = n - answer;
				var r = ranks[a.PlayerId];
				return new RevealEntry
				{
					PlayerId = a.PlayerId,
					Value = a.Value,
					Text = $"{Fmt(n)}{unit}" + (diff == 0 ? " · spot on!" : $" ({(diff > 0 ? "+" : "−")}{Fmt(Math.Abs(diff))})"),
					Correct = diff == 0,
					Points = r.Points,
					Rank = r.Rank,
				};
			}).OrderBy(e => e.Rank ?? 99).ToList();
			return new RevealView
			{
				AnswerText = $"{Fmt(answer)}{unit}",
				WinnerIds = entries.Where(e => e.Points > 0).Select(e => e.PlayerId).ToList(),
				Entries = entries,
			};
		}

		if (q.Type != QuestionType.MapPin) throw new InvalidOperationException($"No reveal for {q.Type}");
		var map = _quiz.MapById(q.MapId)!;
		var target = q.Target!.Value;
		var measured = _answers.Select(a => (a.PlayerId, a.Value, D: QuizRules.MapDistance(map, a.Value.Point!.Value, target))).ToList();
		var mapRanks = ClosenessPoints(measured.Select(m => (m.PlayerId, m.D.Km ?? m.D.Percent)).ToList(), q.Points, q.Scoring, q.TieRule);
		var pinEntries = measured.Select(m =>
		{
			var r = mapRanks[m.PlayerId];
			return new RevealEntry
			{
				PlayerId = m.PlayerId,
				Value = m.Value,
				Text = $"{FormatDistance(m.D.Km, m.D.Percent)} off",
				Points = r.Points,
				Rank = r.Rank,
			};
		}).OrderBy(e => e.Rank ?? 99).ToList();
		return new RevealView
		{
			AnswerText = $"The yellow pin on the map ({map.Name})",
			Target = target,
			WinnerIds = pinEntries.Where(e => e.Points > 0).Select(e => e.PlayerId).ToList(),
			Entries = pinEntries,
		};
	}

	static string ChoiceText(Question q) => $"{QuizRules.ChoiceLetters[q.CorrectChoice]}: {q.Choices[q.CorrectChoice]}";

	public static string ImageKey(int index) => $"q{index}/image";
	public static string MapKey(int index) => $"q{index}/map";

	PublicQuestion PublicQuestion()
	{
		var q = Question;
		var pq = new PublicQuestion
		{
			Type = q.Type,
			Prompt = q.Prompt,
			Points = q.Points,
			Buzzer = q.IsBuzzer,
			ImageKey = q.HasImageField && q.Image != null ? ImageKey(Index) : null,
		};
		if (q.Type == QuestionType.Classic)
		{
			pq.Mode = q.Mode;
			if (q.IsChoice)
			{
				pq.Choices = q.Choices.Select(c => string.IsNullOrWhiteSpace(c) ? null : c).ToList();
				pq.TimeLimitSec = q.TimeLimitSec;
			}
		}
		if (q.Type == QuestionType.Estimation)
		{
			pq.Unit = q.Unit;
			pq.TimeLimitSec = q.TimeLimitSec;
		}
		if (q.Type == QuestionType.MapPin)
		{
			var map = _quiz.MapById(q.MapId)!;
			var builtIn = map.PresetId != null && MapPresets.Get(map.PresetId) != null;
			pq.Map = new PublicMap
			{
				Name = map.Name,
				Width = map.Image.Width,
				Height = map.Image.Height,
				WidthKm = map.WidthKm,
				PresetId = builtIn ? map.PresetId : null,
				AssetKey = builtIn ? null : MapKey(Index),
			};
			pq.TimeLimitSec = q.TimeLimitSec;
		}
		if (q.IsMedia) pq.StartSec = q.StartSec;
		return pq;
	}

	string HostAnswer()
	{
		var q = Question;
		if (q.Type == QuestionType.Classic) return q.IsChoice ? ChoiceText(q) : q.AnswerText;
		if (q.Type == QuestionType.Estimation) return $"{Fmt(q.AnswerNumber ?? 0)}{(q.Unit != "" ? " " + q.Unit : "")}";
		if (q.Type == QuestionType.MapPin) return "The marked spot on the map (shown at the reveal).";
		return q.AnswerText;
	}

	/// <summary>The game as one viewer may see it: "host", or a player id.</summary>
	public GameView View(string viewer)
	{
		var open = Stage == Shared.Stage.Open;
		var view = new GameView
		{
			Index = Index,
			Total = _quiz.Questions.Count,
			Stage = Stage,
			Question = PublicQuestion(),
			EndsAt = open ? _endsAt : null,
			AnswerEndsAt = open && _answeringSince is { } since ? since + _hooks.AnswerTimeSec() * 1000L : null,
			AnsweredIds = _answers.Select(a => a.PlayerId).ToList(),
			Buzzes = _buzzes.Select(b => new BuzzEntry { PlayerId = b.PlayerId, Status = b.Status, AtSec = b.AtMs / 1000.0 }).ToList(),
			MediaPlaying = _playingSince != null,
			Reveal = _reveal,
		};
		if (viewer == "host") view.HostAnswer = HostAnswer();
		else view.MyAnswer = _answers.FirstOrDefault(a => a.PlayerId == viewer).Value;
		return view;
	}

	/// <summary>Image data for an asset key — only for questions already shown.</summary>
	public string? AssetDataUrl(string key)
	{
		var parts = key.Split('/');
		if (parts.Length != 2 || !parts[0].StartsWith('q') || !int.TryParse(parts[0].AsSpan(1), out var index)) return null;
		if (index < 0 || index > Index || index >= _quiz.Questions.Count) return null;
		var q = _quiz.Questions[index];
		if (parts[1] == "image" && q.HasImageField && q.Image != null) return q.Image.DataUrl;
		if (parts[1] == "map" && q.Type == QuestionType.MapPin) return _quiz.MapById(q.MapId)?.Image.DataUrl;
		return null;
	}
}
