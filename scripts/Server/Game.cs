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
	bool HostShowsQuestions();
	bool HostUnlocksBuzzers();
	int AnswerTimeSec();
	int Score(string playerId);
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
	/// <summary>The host has shown the current question (until then players get nothing of it).</summary>
	public bool Shown { get; private set; }
	/// <summary>Buzzer questions: players can buzz in.</summary>
	public bool BuzzersOpen { get; private set; }
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

	// Timeline (put in order, taking turns). Items are indexes into the shown (shuffled) Items.
	readonly List<int> _tlPlaced = new();
	readonly Dictionary<int, string?> _tlPlacedBy = new();
	readonly Queue<int> _tlDeck = new();
	int? _tlCurrent;
	readonly List<string> _tlOrder = new();
	string? _tlTurn;
	long? _tlTurnEnds;
	readonly List<string> _tlMissed = new();
	readonly List<int> _tlWrongGaps = new();
	readonly Dictionary<string, int> _tlPoints = new();
	TimelineEvent? _tlLast;
	string _tlPhase = TimelinePhase.Choosing;
	int? _tlPending;
	/// <summary>Result phase: whether the last turn placed the item (else it passes on).</summary>
	bool _tlResultRight;

	public Game(Quiz quiz, IGameHooks hooks, Random? rng = null)
	{
		// Choice answers get a new random order every game (the quiz itself isn't changed).
		rng ??= new Random();
		_quiz = new Quiz
		{
			Version = quiz.Version, Id = quiz.Id, Title = quiz.Title, CreatedAt = quiz.CreatedAt, UpdatedAt = quiz.UpdatedAt,
			Maps = quiz.Maps,
			Questions = quiz.Questions.Select(q => q.IsChoice ? QuizRules.ShuffleChoices(q, rng) : q.Type == QuestionType.Order ? QuizRules.ShuffleOrder(q, rng) : q).ToList(),
		};
		_hooks = hooks;
		Open(0);
	}

	public Question Question => _quiz.Questions[Index];

	static int? TimeLimitSec(Question q) => q.HasTimeLimit ? q.TimeLimitSec : null;

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
		_endsAt = null;
		_tlPlaced.Clear();
		_tlPlacedBy.Clear();
		_tlDeck.Clear();
		_tlCurrent = null;
		_tlOrder.Clear();
		_tlTurn = null;
		_tlTurnEnds = null;
		_tlMissed.Clear();
		_tlWrongGaps.Clear();
		_tlPoints.Clear();
		_tlLast = null;
		_tlPhase = TimelinePhase.Choosing;
		_tlPending = null;
		_tlResultRight = false;
		Shown = false;
		BuzzersOpen = false;
		if (!_hooks.HostShowsQuestions()) StartShown(now);
	}

	/// <summary>The question appears: its timer starts and (unless the host unlocks them) the buzzers open.</summary>
	void StartShown(long now)
	{
		Shown = true;
		_openedAt = now;
		var limit = TimeLimitSec(Question);
		_endsAt = limit is { } l ? now + l * 1000L : null;
		BuzzersOpen = Question.IsBuzzer && !_hooks.HostUnlocksBuzzers();
		if (Question.IsTimeline) StartTimeline(now);
	}

	/// <summary>The host shows the hidden question to everyone.</summary>
	public bool ShowQuestion()
	{
		if (Stage != Shared.Stage.Open || Shown) return false;
		StartShown(_hooks.Now());
		return true;
	}

	/// <summary>The host locks or unlocks the buzzers (buzzer questions, once shown).</summary>
	public bool SetBuzzers(bool open)
	{
		if (Stage != Shared.Stage.Open || !Shown || !Question.IsBuzzer || BuzzersOpen == open) return false;
		BuzzersOpen = open;
		return true;
	}

	/// <summary>Runs timers. Returns true if something changed (the time ran out).</summary>
	public bool Tick()
	{
		if (Stage == Shared.Stage.Open && _tlTurnEnds is { } turnEnds && _hooks.Now() >= turnEnds)
		{
			MissTurn(TimelineEventKind.TimeUp);
			return true;
		}
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
		if (Stage != Shared.Stage.Open || !q.IsBuzzer || !Shown || !BuzzersOpen || _buzzes.Any(b => b.PlayerId == playerId)) return false;
		var now = _hooks.Now();
		var someoneAnswering = _buzzes.Any(b => b.Status == BuzzStatus.Answering);
		_buzzes.Add(new Buzz
		{
			PlayerId = playerId,
			Status = someoneAnswering ? BuzzStatus.Waiting : BuzzStatus.Answering,
			AtMs = q.HasPlayClock ? PlayTimeMs(now) : now - _openedAt,
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
		if (!Shown) return Result.Fail("The host hasn't shown this question yet.");
		if (q.IsBuzzer) return Result.Fail("This is a buzzer question.");
		if (q.IsTimeline)
		{
			if (raw?.Num is not { } gap || gap != Math.Floor(gap)) return Result.Fail("Pick a spot on the timeline.");
			return Place(playerId, (int)gap);
		}
		if (_answers.Any(a => a.PlayerId == playerId)) return Result.Fail("You already locked in your answer.");

		AnswerValue value;
		if (q.Type == QuestionType.Estimation)
		{
			if (raw?.Num is not { } n || !double.IsFinite(n)) return Result.Fail("Please enter a number.");
			value = AnswerValue.Number(n);
		}
		else if (q.Type == QuestionType.Order)
		{
			// Every shown item exactly once, top first.
			if (raw?.Picks is not { } order || order.Count != q.Items.Count || !order.OrderBy(i => i).SequenceEqual(Enumerable.Range(0, q.Items.Count)))
				return Result.Fail("Put all the items in order.");
			value = AnswerValue.Picked(order);
		}
		else if (q.Type == QuestionType.Write)
		{
			var text = (raw?.Text ?? "").Trim();
			if (text == "") return Result.Fail("Type your answer.");
			value = AnswerValue.Written(text.Length > QuestionLimits.WrittenMaxLength ? text[..QuestionLimits.WrittenMaxLength] : text);
		}
		else if (q.IsMulti)
		{
			if (raw?.Picks is not { Count: > 0 } picks || picks.Any(i => i < 0 || i >= q.Choices.Count || string.IsNullOrWhiteSpace(q.Choices[i])))
				return Result.Fail("Pick at least one of the answers.");
			value = AnswerValue.Picked(picks.Distinct().OrderBy(i => i));
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
		// Timeline: the player whose turn it is left, so the next one goes.
		if (Stage == Shared.Stage.Open && _tlPhase == TimelinePhase.Choosing && _tlTurn is { } turn && !_hooks.ActivePlayerIds().Contains(turn))
		{
			MissTurn(TimelineEventKind.Skipped);
			return true;
		}
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
		_tlTurn = null;
		_tlTurnEnds = null;
		_reveal = ComputeReveal();
		// (timeline points were awarded as the items were placed)
		if (!Question.IsTimeline)
			foreach (var e in _reveal.Entries)
				if (e.Points != 0) _hooks.Award(e.PlayerId, e.Points);
	}

	// ---------- timeline ----------

	/// <summary>Where item i belongs: its index in the right order.</summary>
	int Rank(int item) => Question.CorrectOrder.IndexOf(item);

	/// <summary>The gap item i belongs in on the current timeline.</summary>
	int RightGap(int item) => _tlPlaced.Count(p => Rank(p) < Rank(item));

	void StartTimeline(long now)
	{
		var q = Question;
		_tlPlaced.Add(q.TimelineStart);
		_tlPlacedBy[q.TimelineStart] = null;
		// The rest come in the shown (shuffled) order.
		foreach (var i in Enumerable.Range(0, q.Items.Count).Where(i => i != q.TimelineStart)) _tlDeck.Enqueue(i);
		// Fewest points first; ties in the order people joined.
		var active = _hooks.ActivePlayerIds();
		_tlOrder.AddRange(active.Select((id, joined) => (id, joined)).OrderBy(p => _hooks.Score(p.id)).ThenBy(p => p.joined).Select(p => p.id));
		NextItem(_tlOrder.FirstOrDefault(), now);
	}

	/// <summary>The next player after this one in turn order (still connected; with skipMissed, not yet missed the current item).</summary>
	string? NextPlayer(string? after, bool skipMissed)
	{
		var active = _hooks.ActivePlayerIds();
		// People who joined during the question queue up at the end.
		foreach (var id in active)
			if (!_tlOrder.Contains(id)) _tlOrder.Add(id);
		if (_tlOrder.Count == 0) return null;
		var start = after != null ? _tlOrder.IndexOf(after) : -1;
		for (var k = 1; k <= _tlOrder.Count; k++)
		{
			var id = _tlOrder[((start + k) % _tlOrder.Count + _tlOrder.Count) % _tlOrder.Count];
			if (active.Contains(id) && !(skipMissed && _tlMissed.Contains(id))) return id;
		}
		return null;
	}

	/// <summary>On to the next item (or the reveal once all are placed); the given player places it.</summary>
	void NextItem(string? player, long now)
	{
		_tlMissed.Clear();
		_tlWrongGaps.Clear();
		if (_tlDeck.Count == 0)
		{
			_tlCurrent = null;
			DoReveal();
			return;
		}
		_tlCurrent = _tlDeck.Dequeue();
		_tlTurn = player != null && _hooks.ActivePlayerIds().Contains(player) ? player : NextPlayer(player, false);
		StartTurn(now);
	}

	void StartTurn(long now)
	{
		_tlPhase = TimelinePhase.Choosing;
		_tlPending = null;
		_tlTurnEnds = _tlTurn != null ? now + Question.TimeLimitSec * 1000L : null;
	}

	void PutDown(int item, string? by)
	{
		_tlPlaced.Insert(RightGap(item), item);
		_tlPlacedBy[item] = by;
	}

	/// <summary>A player puts the current item into a gap (0 = before the first card, Placed.Count = after the last).</summary>
	Result Place(string playerId, int gap)
	{
		if (_tlCurrent is not { } item) return Result.Fail("This question is already closed.");
		if (_tlTurn != playerId || _tlPhase != TimelinePhase.Choosing) return Result.Fail("It's not your turn.");
		if (gap < 0 || gap > _tlPlaced.Count || _tlWrongGaps.Contains(gap)) return Result.Fail("Pick a spot on the timeline.");
		// Locked in: the host reveals whether it's right.
		_tlPending = gap;
		_tlPhase = TimelinePhase.Locked;
		_tlTurnEnds = null;
		_tlLast = new TimelineEvent { Kind = TimelineEventKind.Locked, PlayerId = playerId, Item = Question.Items[item] };
		return Result.Success();
	}

	/// <summary>Host: reveal whether the locked-in gap is right. The next turn waits for <see cref="NextTurn"/>.</summary>
	public bool RevealPlacement()
	{
		if (Stage != Shared.Stage.Open || _tlPhase != TimelinePhase.Locked || _tlCurrent is not { } item || _tlPending is not { } gap || _tlTurn is not { } player)
			return false;
		var text = Question.Items[item];
		_tlResultRight = gap == RightGap(item);
		if (_tlResultRight)
		{
			PutDown(item, player);
			_tlPoints[player] = _tlPoints.GetValueOrDefault(player) + Question.Points;
			_hooks.Award(player, Question.Points);
			_tlCurrent = null;
			// (gaps tried for this item no longer mean anything once it's placed)
			_tlWrongGaps.Clear();
			_tlLast = new TimelineEvent { Kind = TimelineEventKind.Right, PlayerId = player, Item = text };
		}
		else
		{
			_tlWrongGaps.Add(gap);
			_tlLast = new TimelineEvent { Kind = TimelineEventKind.Wrong, PlayerId = player, Item = text };
		}
		_tlPending = null;
		_tlPhase = TimelinePhase.Result;
		return true;
	}

	/// <summary>Host: after a result, on to the next turn (the next item, or the next player on the same item).</summary>
	public bool NextTurn()
	{
		if (Stage != Shared.Stage.Open || _tlPhase != TimelinePhase.Result || _tlTurn is not { } player) return false;
		var now = _hooks.Now();
		if (_tlResultRight) NextItem(Question.KeepGoing ? player : NextPlayer(player, false), now);
		else PassOn(player, now);
		return true;
	}

	/// <summary>
	/// The current player misses: the next one tries the same item. Time up waits for the host like a wrong
	/// answer; a skip (by the host, or because the player left) moves on right away.
	/// </summary>
	void MissTurn(string kind)
	{
		if (_tlCurrent is not { } item || _tlTurn is not { } player) return;
		_tlLast = new TimelineEvent { Kind = kind, PlayerId = player, Item = Question.Items[item] };
		if (kind == TimelineEventKind.TimeUp)
		{
			_tlResultRight = false;
			_tlTurnEnds = null;
			_tlPhase = TimelinePhase.Result;
			return;
		}
		PassOn(player, _hooks.Now());
	}

	void PassOn(string player, long now)
	{
		_tlMissed.Add(player);
		if (NextPlayer(player, true) is { } next)
		{
			_tlTurn = next;
			StartTurn(now);
			return;
		}
		// Everyone missed it: it goes in its place for free, and the next player starts the next item.
		PlaceForFree(player, now);
	}

	void PlaceForFree(string? lastPlayer, long now)
	{
		if (_tlCurrent is not { } item) return;
		PutDown(item, null);
		_tlLast = new TimelineEvent { Kind = TimelineEventKind.Free, Item = Question.Items[item] };
		NextItem(NextPlayer(lastPlayer, false), now);
	}

	/// <summary>Host: skip the player whose turn it is.</summary>
	public bool SkipTurn()
	{
		if (Stage != Shared.Stage.Open || _tlTurn == null || _tlPhase != TimelinePhase.Choosing) return false;
		MissTurn(TimelineEventKind.Skipped);
		return true;
	}

	/// <summary>Host: put the current item in its place without points; the next player gets the next item.</summary>
	public bool PlaceCurrentForFree()
	{
		if (Stage != Shared.Stage.Open || _tlCurrent == null || _tlPhase == TimelinePhase.Locked) return false;
		PlaceForFree(_tlTurn, _hooks.Now());
		return true;
	}

	TimelineView TimelineView() => new()
	{
		Phase = _tlPhase,
		PendingGap = _tlPending,
		Placed = _tlPlaced.Select(i => new TimelineCard
		{
			Text = Question.Items[i],
			Label = Question.ItemLabel(i),
			PlacedBy = _tlPlacedBy.GetValueOrDefault(i),
			Start = i == Question.TimelineStart,
		}).ToList(),
		Current = _tlCurrent is { } c ? Question.Items[c] : null,
		Left = _tlDeck.Count,
		TurnPlayerId = _tlTurn,
		TurnEndsAt = Stage == Shared.Stage.Open ? _tlTurnEnds : null,
		WrongGaps = _tlWrongGaps.ToList(),
		TurnOrder = _tlOrder.ToList(),
		Missed = _tlMissed.ToList(),
		Last = _tlLast,
	};

	static readonly CultureInfo En = CultureInfo.GetCultureInfo("en-GB");

	/// <summary>Like toLocaleString('en-GB', { maximumFractionDigits: 2 }).</summary>
	public static string Fmt(double n) => n.ToString("#,0.##", En);

	public static string FormatDistance(double? km, double percent)
	{
		if (km is not { } k) return Lang.F("{0} % of the map", percent.ToString("0.0", En));
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
				GuessTimeSec = q.HasPlayClock && winner != null ? winner.AtMs / 1000.0 : null,
				Entries = _buzzes.Select(b => new RevealEntry
				{
					PlayerId = b.PlayerId,
					Text = Lang.T(b.Status == BuzzStatus.Correct ? "Correct" : b.Status == BuzzStatus.Wrong ? "Wrong" : "Buzzed, no turn"),
					Correct = b.Status == BuzzStatus.Correct,
					Points = b.Status == BuzzStatus.Correct ? q.Points : 0,
				}).ToList(),
			};
		}

		if (q.IsTimeline)
		{
			var entries = _tlOrder.Select(id =>
			{
				var placed = _tlPlacedBy.Count(p => p.Value == id);
				return new RevealEntry
				{
					PlayerId = id,
					Text = placed == 1 ? Lang.T("placed 1 item") : Lang.F("placed {0} items", placed),
					Correct = placed > 0,
					Points = _tlPoints.GetValueOrDefault(id),
				};
			}).OrderByDescending(e => e.Points).ToList();
			return new RevealView
			{
				AnswerText = OrderText(q),
				WinnerIds = entries.Where(e => e.Points > 0).Select(e => e.PlayerId).ToList(),
				Entries = entries,
			};
		}

		if (q.Type == QuestionType.Order)
		{
			var n = q.Items.Count;
			var entries = _answers.Select(a =>
			{
				var order = a.Value.Picks!;
				var right = Enumerable.Range(0, n).Count(i => order[i] == q.CorrectOrder[i]);
				return new RevealEntry
				{
					PlayerId = a.PlayerId,
					Value = a.Value,
					Text = right == n ? Lang.T("All in the right order!") : Lang.F("{0} of {1} in the right place", right, n),
					Correct = right == n,
					Points = right == n ? q.Points : 0,
				};
			}).OrderByDescending(e => e.Correct).ToList();
			return new RevealView
			{
				AnswerText = OrderText(q),
				WinnerIds = entries.Where(e => e.Correct).Select(e => e.PlayerId).ToList(),
				Entries = entries,
			};
		}

		if (q.Type == QuestionType.Write)
		{
			var entries = _answers.Select(a =>
			{
				var correct = QuizRules.WrittenMatches(a.Value.Text!, q.AnswerText);
				return new RevealEntry { PlayerId = a.PlayerId, Value = a.Value, Text = a.Value.Text!, Correct = correct, Points = correct ? q.Points : 0 };
			}).ToList();
			return new RevealView
			{
				AnswerText = q.AnswerText,
				WinnerIds = entries.Where(e => e.Correct).Select(e => e.PlayerId).ToList(),
				Entries = entries,
			};
		}

		if (q.Type == QuestionType.Classic)
		{
			var counts = new int[q.Choices.Count];
			var right = q.CorrectSet;
			var entries = _answers.Select(a =>
			{
				var picks = Picks(a.Value);
				foreach (var i in picks) counts[i]++;
				// Multi: only exactly the right set counts — no points for half of it.
				var correct = picks.SequenceEqual(right);
				return new RevealEntry
				{
					PlayerId = a.PlayerId,
					Value = a.Value,
					Text = ChoiceText(q, picks),
					Correct = correct,
					Points = correct ? q.Points : 0,
				};
			}).ToList();
			return new RevealView
			{
				AnswerText = ChoiceText(q, right),
				WinnerIds = entries.Where(e => e.Correct).Select(e => e.PlayerId).ToList(),
				Entries = entries,
				Counts = counts,
				CorrectIndices = right,
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
					Text = $"{Fmt(n)}{unit}" + (diff == 0 ? Lang.T(" · spot on!") : $" ({(diff > 0 ? "+" : "−")}{Fmt(Math.Abs(diff))})"),
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
				Text = Lang.F("{0} off", FormatDistance(m.D.Km, m.D.Percent)),
				Points = r.Points,
				Rank = r.Rank,
			};
		}).OrderBy(e => e.Rank ?? 99).ToList();
		return new RevealView
		{
			AnswerText = Lang.F("The yellow pin on the map ({0})", map.Name),
			Target = target,
			WinnerIds = pinEntries.Where(e => e.Points > 0).Select(e => e.PlayerId).ToList(),
			Entries = pinEntries,
		};
	}

	/// <summary>Write the answer, at the reveal: the host ticks or unticks a player's answer (points follow).</summary>
	public bool ToggleWritten(string playerId)
	{
		if (Stage != Shared.Stage.Reveal || Question.Type != QuestionType.Write || _reveal?.Entries.FirstOrDefault(e => e.PlayerId == playerId) is not { } entry)
			return false;
		entry.Correct = !entry.Correct;
		var points = entry.Correct ? Question.Points : 0;
		_hooks.Award(playerId, points - entry.Points);
		entry.Points = points;
		_reveal.WinnerIds = _reveal.Entries.Where(e => e.Correct).Select(e => e.PlayerId).ToList();
		return true;
	}

	/// <summary>"Doom → Half-Life → Portal" (a timeline with its labels: "Doom (1993) → …").</summary>
	static string OrderText(Question q) => string.Join(" → ", q.CorrectOrder.Select(i =>
		q.IsTimeline && q.ItemLabel(i) is { Length: > 0 } label ? $"{q.Items[i]} ({label})" : q.Items[i]));

	static List<int> Picks(AnswerValue v) => v.Picks ?? (v.Num is { } n ? new List<int> { (int)n } : new List<int>());

	/// <summary>"C: 29", or for several "A: Red · C: Blue".</summary>
	static string ChoiceText(Question q, IEnumerable<int> picks) =>
		string.Join(" · ", picks.Select(i => $"{QuizRules.ChoiceLetters[i]}: {q.Choices[i]}"));

	/// <summary>An image reveal's picture while its question is still open.</summary>
	bool HiddenPicture(int index) => index == Index && Stage == Shared.Stage.Open && _quiz.Questions[index].Type == QuestionType.Image;

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
			// (an image reveal's picture only plays on the host's screen; players get it at the reveal)
			ImageKey = q.HasImageField && q.Image != null && !HiddenPicture(Index) ? ImageKey(Index) : null,
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
		if (q.Type == QuestionType.Order)
		{
			pq.Items = q.Items;
			pq.OrderTop = q.OrderTop;
			pq.OrderBottom = q.OrderBottom;
			pq.OrderMode = q.OrderMode;
			pq.TimeLimitSec = q.TimeLimitSec;
		}
		if (q.Type == QuestionType.Write) pq.TimeLimitSec = q.TimeLimitSec;
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
		if (q.Type == QuestionType.Classic) return q.IsChoice ? ChoiceText(q, q.CorrectSet) : q.AnswerText;
		if (q.Type == QuestionType.Order) return OrderText(q);
		if (q.Type == QuestionType.Estimation) return $"{Fmt(q.AnswerNumber ?? 0)}{(q.Unit != "" ? " " + q.Unit : "")}";
		if (q.Type == QuestionType.MapPin) return Lang.T("The marked spot on the map (shown at the reveal).");
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
			// A hidden question: players only learn its number, type and points.
			Question = Shown || viewer == "host" || !open ? PublicQuestion() : new PublicQuestion { Type = Question.Type, Points = Question.Points, Buzzer = Question.IsBuzzer, Mode = Question.Type == QuestionType.Classic ? Question.Mode : null, OrderMode = Question.Type == QuestionType.Order ? Question.OrderMode : null },
			Shown = Shown || !open,
			BuzzersOpen = BuzzersOpen,
			EndsAt = open ? _endsAt : null,
			AnswerEndsAt = open && _answeringSince is { } since ? since + _hooks.AnswerTimeSec() * 1000L : null,
			AnsweredIds = _answers.Select(a => a.PlayerId).ToList(),
			Buzzes = _buzzes.Select(b => new BuzzEntry { PlayerId = b.PlayerId, Status = b.Status, AtSec = b.AtMs / 1000.0 }).ToList(),
			MediaPlaying = _playingSince != null,
			Reveal = _reveal,
			// (the timeline only exists once the question is shown)
			Timeline = Question.IsTimeline && Shown ? TimelineView() : null,
		};
		if (viewer == "host") view.HostAnswer = HostAnswer();
		else view.MyAnswer = _answers.FirstOrDefault(a => a.PlayerId == viewer).Value;
		return view;
	}

	/// <summary>Image data for an asset key — only for questions already shown.</summary>
	/// <param name="host">The host's own screen: may also have what players can't yet.</param>
	public string? AssetDataUrl(string key, bool host = false)
	{
		var parts = key.Split('/');
		if (parts.Length != 2 || !parts[0].StartsWith('q') || !int.TryParse(parts[0].AsSpan(1), out var index)) return null;
		if (index < 0 || index > Index || index >= _quiz.Questions.Count) return null;
		if (!host && index == Index && !Shown && Stage == Shared.Stage.Open) return null;
		if (!host && HiddenPicture(index)) return null;
		var q = _quiz.Questions[index];
		if (parts[1] == "image" && q.HasImageField && q.Image != null) return q.Image.DataUrl;
		if (parts[1] == "map" && q.Type == QuestionType.MapPin) return _quiz.MapById(q.MapId)?.Image.DataUrl;
		return null;
	}
}
