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
	/// <summary>The host allows this joker kind this match.</summary>
	bool JokerAllowed(string kind);
	/// <summary>The lobby shows question categories to players.</summary>
	bool ShowCategories();
	/// <summary>For the ranking's tiebreaks: how long a player took to lock in an answer or send a recording (ms)…</summary>
	void NoteAnswerTime(string playerId, long ms);
	/// <summary>…how long they took to buzz in (ms; its own average, a buzz is much quicker than typing)…</summary>
	void NoteBuzzTime(string playerId, long ms);
	/// <summary>…a buzz the host judged wrong, and a timeline placement (right or not), for the per-question results…</summary>
	void NoteWrongBuzz(string playerId);
	void NotePlacement(string playerId, bool right);
	/// <summary>
	/// …that they played the question at its reveal: whether they answered at all, the joker they played on it
	/// (Joker kind or null) and, on a step by step question they got right, at which hint (1 = the first, hardest).
	/// </summary>
	void NoteTookPart(string playerId, bool answered, string? joker, int? hint);
	/// <summary>…the host giving (or taking back) a question's bonus…</summary>
	void NoteHostPoints(string playerId, int points);
	/// <summary>…and a typed answer's typos (letters off from the closest accepted answer), for the Spelling stat.</summary>
	void NoteTypos(string playerId, int typos);
	/// <summary>…how far off a map pin was (km, maps with a scale only), and an estimation (% of the right number)…</summary>
	void NoteDistance(string playerId, double km);
	void NotePercentOff(string playerId, double percent);
	/// <summary>…and that they scored on a question.</summary>
	void NoteScored(string playerId);
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
		public bool FastPass;
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
	/// <summary>Earlier rounds of buzzes on this question (the host let everyone buzz again): kept for the reveal.</summary>
	readonly List<Buzz> _pastBuzzes = new();
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

	// Interactive: everyone's recording (a WAV data URL), in the order they came in; then votes or the host's favourite.
	string _iaPhase = InteractivePhase.Recording;
	readonly Dictionary<string, (string DataUrl, double Sec)> _recordings = new();
	readonly List<string> _clipOrder = new();
	int _clip;
	int _clipPlays;
	/// <summary>Voter → the clip (index into _clipOrder) they voted for.</summary>
	readonly Dictionary<string, int> _votes = new();
	string? _favourite;

	/// <summary>Every joker played this match (each player plays each kind once).</summary>
	readonly List<JokerPlay> _jokers = new();
	/// <summary>Joined during the current question: they play from the next one.</summary>
	readonly HashSet<string> _late = new();
	/// <summary>Paused because these players left; it goes on when they're back or the host continues.</summary>
	readonly HashSet<string> _waitingFor = new();
	long? _pausedAt;

	/// <param name="shared">The files players get ahead of time (Room.SharedFiles, sent from the lobby on); made here if null.</param>
	public Game(Quiz quiz, IGameHooks hooks, Random? rng = null, IReadOnlyList<SharedMedia>? shared = null)
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
		SharedFiles = shared ?? ShareFiles(_quiz);
		Open(0);
	}

	/// <summary>
	/// A file the players' screens need: a sound or video (Asset null), or a picture (Asset: its asset key, like
	/// "q3/image", "q3/slide1", or "map:ID" for a custom map). HostSession sends them all to everyone ahead of time, from
	/// the lobby on, encrypted with Key; the id and key only reach players with the question, once it's shown, so
	/// nobody can look ahead. Index: the (first) question that uses it, so files go out in the order they're needed.
	/// </summary>
	public sealed record SharedMedia(int Index, string Id, byte[] Key, string DataUrl, string Mime, string? Asset = null)
	{
		public bool IsPicture => Asset != null;
	}
	public readonly IReadOnlyList<SharedMedia> SharedFiles;

	/// <summary>Every file of a quiz the players need, each with a random id and key (see SharedMedia).</summary>
	public static List<SharedMedia> ShareFiles(Quiz quiz)
	{
		var files = new List<SharedMedia>();
		SharedMedia Make(int index, string dataUrl, string mime, string? asset) =>
			new(index, Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(8)),
				System.Security.Cryptography.RandomNumberGenerator.GetBytes(32), dataUrl,
				DataUrl.TryParse(dataUrl, out var m, out _) && m != "" ? m : mime, asset);
		var maps = new HashSet<string>();
		for (var i = 0; i < quiz.Questions.Count; i++)
		{
			var q = quiz.Questions[i];
			// (pictures first: they're small and needed the moment a question is shown)
			if (q.HasImageField && q.Image is { DataUrl: not "" } image) files.Add(Make(i, image.DataUrl, "image/webp", ImageKey(i)));
			if (q.Type == QuestionType.Picture)
				for (var s = 0; s < q.Slides.Count; s++)
					if (q.Slides[s].DataUrl != "") files.Add(Make(i, q.Slides[s].DataUrl, "image/webp", SlideKey(i, s)));
			if (q.Type == QuestionType.MapPin && quiz.MapById(q.MapId) is { PresetId: null, Image.DataUrl: not "" } map && maps.Add(map.Id))
				files.Add(Make(i, map.Image.DataUrl, "image/webp", "map:" + map.Id));
			if (q.Media is { DataUrl: not "" } media && (q.Type == QuestionType.Sound || q.Type == QuestionType.Video && Storage.Media.CanPlayVideo(media)))
				files.Add(Make(i, media.DataUrl, media.Mime, null));
		}
		return files;
	}

	/// <summary>The current question's sound or video file, if it has one.</summary>
	public SharedMedia? CurrentMedia => SharedFiles.FirstOrDefault(v => v.Index == Index && !v.IsPicture);

	/// <summary>Every file the current question shows (its pictures, its map, its sound or video).</summary>
	public IEnumerable<SharedMedia> CurrentFiles()
	{
		var q = Question;
		var mapAsset = q.Type == QuestionType.MapPin && _quiz.MapById(q.MapId) is { PresetId: null } m ? "map:" + m.Id : null;
		return SharedFiles.Where(f => f.Index == Index && f.Asset is not { } a || f.Asset is { } asset
			&& (asset.StartsWith($"q{Index}/") || asset == mapAsset));
	}

	public Question Question => _quiz.Questions[Index];

	/// <summary>Who plays the current question: connected, and not joined during it.</summary>
	IReadOnlyList<string> Active() => _hooks.ActivePlayerIds().Where(id => !_late.Contains(id)).ToList();

	static int? TimeLimitSec(Question q) => q.HasTimeLimit ? q.TimeLimitSec : null;

	void Open(int index)
	{
		var now = _hooks.Now();
		Index = index;
		Stage = Shared.Stage.Open;
		Slide = 0;
		_slidesSeen = 1;
		Hint = 0;
		_stepsWorth = null;
		_openedAt = now;
		_answers.Clear();
		_buzzes.Clear();
		_pastBuzzes.Clear();
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
		_iaPhase = InteractivePhase.Recording;
		_recordings.Clear();
		_clipOrder.Clear();
		_clip = 0;
		_clipPlays = 0;
		_votes.Clear();
		_favourite = null;
		_late.Clear();
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
		Trace.Log("game", $"show question {Index + 1} ({Question.Type}{(Question.IsTyped ? ", typed" : "")}) stage={Stage} shown={Shown} paused={Paused}");
		if (Stage != Shared.Stage.Open || Shown || Paused) return false;
		StartShown(_hooks.Now());
		return true;
	}

	/// <summary>The host locks or unlocks the buzzers (buzzer questions, once shown).</summary>
	public bool SetBuzzers(bool open)
	{
		if (Stage != Shared.Stage.Open || !Shown || !Question.IsBuzzer || BuzzersOpen == open || open && Paused) return false;
		BuzzersOpen = open;
		return true;
	}

	/// <summary>
	/// Everyone may buzz again (all who buzzed were wrong, nobody is waiting): this round's buzzes are put aside and the
	/// buzzers open.
	/// </summary>
	public bool ReopenBuzzers()
	{
		if (Stage != Shared.Stage.Open || !Shown || !Question.IsBuzzer || Paused || _buzzes.Count == 0
			|| _buzzes.Any(b => b.Status is BuzzStatus.Answering or BuzzStatus.Waiting)) return false;
		_pastBuzzes.AddRange(_buzzes);
		_buzzes.Clear();
		_answeringSince = null;
		BuzzersOpen = true;
		return true;
	}

	/// <summary>Runs timers. Returns true if something changed (the time ran out).</summary>
	public bool Tick()
	{
		// (paused: every clock stands still)
		if (Paused) return false;
		if (Stage == Shared.Stage.Open && _tlTurnEnds is { } turnEnds && _hooks.Now() >= turnEnds)
		{
			MissTurn(TimelineEventKind.TimeUp);
			return true;
		}
		if (Stage != Shared.Stage.Open || _endsAt is not { } ends || _hooks.Now() < ends) return false;
		if (Question.IsInteractive && _iaPhase == InteractivePhase.Recording) EndRecording();
		else DoReveal();
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
			// Pressing play opens the buzzers (no need to remember Unlock), and once everyone has had a wrong go,
			// resuming gives them all another one.
			if (Shown && Question.IsBuzzer && !Paused && _buzzes.All(b => b.Status is BuzzStatus.Wrong or BuzzStatus.Correct))
			{
				if (_buzzes.Count > 0 && Active().All(id => _buzzes.Any(b => b.PlayerId == id))) ReopenBuzzers();
				BuzzersOpen = true;
			}
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
		Trace.Log("game", $"buzz from {playerId} on question {Index + 1}: open={BuzzersOpen} shown={Shown} paused={Paused} fastPass={Played(playerId, Joker.FastPass, Index)} queue={string.Join(",", _buzzes.Select(b => $"{b.PlayerId}:{b.Status}"))}");
		if (Stage != Shared.Stage.Open || !q.IsBuzzer || !Shown || !BuzzersOpen || Paused || _late.Contains(playerId) || _buzzes.Any(b => b.PlayerId == playerId)) return false;
		var now = _hooks.Now();
		var someoneAnswering = _buzzes.Any(b => b.Status == BuzzStatus.Answering);
		var buzz = new Buzz
		{
			PlayerId = playerId,
			Status = someoneAnswering ? BuzzStatus.Waiting : BuzzStatus.Answering,
			AtMs = q.HasPlayClock ? PlayTimeMs(now) : now - _openedAt,
			FastPass = Played(playerId, Joker.FastPass, Index),
		};
		// A Fast Pass always answers first: it takes over from whoever is answering (they wait first in line again) and
		// goes ahead of everyone waiting. It was played blind, before the question was shown: that's the gamble.
		var answering = _buzzes.FirstOrDefault(b => b.Status == BuzzStatus.Answering);
		if (buzz.FastPass && answering is { FastPass: false })
		{
			answering.Status = BuzzStatus.Waiting;
			buzz.Status = BuzzStatus.Answering;
			_buzzes.Insert(_buzzes.IndexOf(answering), buzz);
			_answeringSince = now;
		}
		else
		{
			var at = buzz.FastPass ? _buzzes.FindIndex(b => b.Status == BuzzStatus.Waiting && !b.FastPass) : -1;
			if (at >= 0) _buzzes.Insert(at, buzz);
			else _buzzes.Add(buzz);
			if (!someoneAnswering) _answeringSince = now;
		}
		_hooks.NoteBuzzTime(playerId, buzz.AtMs);
		return true;
	}

	/// <summary>Mark the player who is answering after a buzz right or wrong.</summary>
	public bool Judge(bool correct)
	{
		var current = _buzzes.FirstOrDefault(b => b.Status == BuzzStatus.Answering);
		Trace.Log("game", $"judge {current?.PlayerId ?? "(nobody answering)"}: {(correct ? "right" : "wrong")}");
		if (Stage != Shared.Stage.Open || current == null) return false;
		if (correct)
		{
			current.Status = BuzzStatus.Correct;
			if (Question.IsSteps) _stepsWorth = StepsWorth;
			DoReveal();
			return true;
		}
		current.Status = BuzzStatus.Wrong;
		_hooks.NoteWrongBuzz(current.PlayerId);
		var nextUp = _buzzes.FirstOrDefault(b => b.Status == BuzzStatus.Waiting);
		if (nextUp != null) nextUp.Status = BuzzStatus.Answering;
		_answeringSince = nextUp != null ? _hooks.Now() : null;
		return true;
	}

	public Result Answer(string playerId, int index, AnswerValue? raw)
	{
		var q = Question;
		Trace.Log("game", $"answer from {playerId} for question {index + 1} (now {Index + 1}, {Stage}): {(raw == null ? "null" : raw.Text ?? raw.Num?.ToString() ?? (raw.Picks != null ? string.Join(",", raw.Picks) : raw.Point?.ToString() ?? "?"))}");
		if (index != Index || Stage != Shared.Stage.Open) return Result.Fail("This question is already closed.");
		if (!Shown) return Result.Fail("The host hasn't shown this question yet.");
		if (Paused) return Result.Fail("The game is paused until everyone is back.");
		if (_late.Contains(playerId)) return Result.Fail("You joined during this question: you play from the next one.");
		if (q.IsBuzzer) return Result.Fail("This is a buzzer question.");
		if (q.IsInteractive) return Vote(playerId, raw?.Num);
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
		else if (q.IsWritten)
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
		else if (q.IsClassicLike)
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
		_hooks.NoteAnswerTime(playerId, _hooks.Now() - _openedAt);
		if (Active().All(id => _answers.Any(a => a.PlayerId == id))) DoReveal();
		return Result.Success();
	}

	/// <summary>Someone left or reconnected: maybe everyone who is still here has answered.</summary>
	public bool CheckAllAnswered()
	{
		// (paused: nothing goes on without the ones who left)
		if (Paused) return false;
		// Timeline: the player whose turn it is left, so the next one goes.
		if (Stage == Shared.Stage.Open && _tlPhase == TimelinePhase.Choosing && _tlTurn is { } turn && !Active().Contains(turn))
		{
			MissTurn(TimelineEventKind.Skipped);
			return true;
		}
		if (Stage == Shared.Stage.Open && Question.IsInteractive && Shown) return CheckInteractiveDone();
		if (Stage != Shared.Stage.Open || Question.IsBuzzer || _answers.Count == 0) return false;
		if (!Active().All(id => _answers.Any(a => a.PlayerId == id))) return false;
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
		Trace.Log("game", $"reveal question {Index + 1}");
		SetMedia(false);
		Stage = Shared.Stage.Reveal;
		_tlTurn = null;
		_tlTurnEnds = null;
		_reveal = ComputeReveal();
		foreach (var e in _reveal.Entries)
		{
			e.Doubled = Multiplier(e.PlayerId) == 2;
			// (timeline points were awarded, doubled, as the items were placed)
			if (Question.IsTimeline) continue;
			e.Points *= Multiplier(e.PlayerId);
			if (e.Points != 0) _hooks.Award(e.PlayerId, e.Points);
		}
		foreach (var e in _reveal.Entries.Where(e => e.Points > 0)) _hooks.NoteScored(e.PlayerId);
		if (Question.IsWritten)
			foreach (var (id, value) in _answers)
				if (value.Text is { } text && QuizRules.Typos(text, Question.AnswerText) is { } typos) _hooks.NoteTypos(id, typos);
		// (everyone who played it, for the per-question results: answered or not, joker, the hint a right buzz came at)
		var hint = Question.IsSteps && _stepsWorth is { } worth ? Question.FilledHints.Count - worth + 1 : (int?)null;
		var buzzes = _buzzes.Concat(_pastBuzzes).ToList();
		foreach (var id in Active().Concat(_reveal.Entries.Select(e => e.PlayerId)).Distinct())
		{
			// (timeline turns count through NotePlacement)
			var answered = _answers.Any(a => a.PlayerId == id) || buzzes.Any(b => b.PlayerId == id) || _recordings.ContainsKey(id);
			var joker = Played(id, Joker.Double, Index) ? Joker.Double : Played(id, Joker.FastPass, Index) ? Joker.FastPass : null;
			var right = buzzes.Any(b => b.PlayerId == id && b.Status == BuzzStatus.Correct);
			_hooks.NoteTookPart(id, answered, joker, right ? hint : null);
		}
	}

	// ---------- interactive ----------

	public static string RecordingKey(int index, int clip) => $"q{index}/rec{clip}";

	/// <summary>A player sends their recording (a WAV file); a new one replaces theirs until recording is over.</summary>
	public Result SubmitRecording(string playerId, int index, byte[] wav)
	{
		var q = Question;
		if (index != Index || Stage != Shared.Stage.Open || !q.IsInteractive) return Result.Fail("This question is already closed.");
		if (!Shown) return Result.Fail("The host hasn't shown this question yet.");
		if (Paused) return Result.Fail("The game is paused until everyone is back.");
		if (_late.Contains(playerId)) return Result.Fail("You joined during this question: you play from the next one.");
		if (_iaPhase != InteractivePhase.Recording) return Result.Fail("Time's up: the recordings are being played.");
		if (WavSeconds(wav) is not { } sec || sec > QuestionLimits.RecordingMaxSec + 1) return Result.Fail("That recording didn't arrive properly.");
		_recordings[playerId] = ("data:audio/wav;base64," + Convert.ToBase64String(wav), sec);
		if (!_clipOrder.Contains(playerId)) _clipOrder.Add(playerId);
		_hooks.NoteAnswerTime(playerId, _hooks.Now() - _openedAt);
		CheckInteractiveDone();
		return Result.Success();
	}

	/// <summary>How long a 16-bit PCM WAV file plays, or null if it isn't one (or is too big).</summary>
	public static double? WavSeconds(byte[] wav)
	{
		if (wav.Length < 44 || wav.Length > 4 * 1024 * 1024 || System.Text.Encoding.ASCII.GetString(wav, 0, 4) != "RIFF"
			|| System.Text.Encoding.ASCII.GetString(wav, 8, 4) != "WAVE") return null;
		var byteRate = BitConverter.ToInt32(wav, 28);
		return byteRate <= 0 ? null : (wav.Length - 44) / (double)byteRate;
	}

	/// <summary>Everyone has sent a recording, or everyone who can has voted: on to the next step.</summary>
	bool CheckInteractiveDone()
	{
		if (Paused || Stage != Shared.Stage.Open) return false;
		var active = Active();
		if (_iaPhase == InteractivePhase.Recording && active.Count > 0 && active.All(_recordings.ContainsKey))
		{
			EndRecording();
			return true;
		}
		if (_iaPhase == InteractivePhase.Voting && Voters().All(_votes.ContainsKey))
		{
			DoReveal();
			return true;
		}
		return false;
	}

	/// <summary>Who can vote: everyone here who has someone else's recording to vote for.</summary>
	List<string> Voters() => Active().Where(id => _clipOrder.Any(c => c != id)).ToList();

	/// <summary>Recording is over (time's up, everyone sent one, or the host ends it): the host plays them one by one.</summary>
	void EndRecording()
	{
		_iaPhase = InteractivePhase.Listening;
		_endsAt = null;
		_clip = 0;
		if (_clipOrder.Count == 0) DoReveal();
	}

	/// <summary>The host ends recording early (who hasn't sent one is left out).</summary>
	public bool EndRecordingNow()
	{
		if (Stage != Shared.Stage.Open || !Question.IsInteractive || !Shown || _iaPhase != InteractivePhase.Recording || Paused) return false;
		EndRecording();
		return true;
	}

	/// <summary>The host goes to another recording.</summary>
	public bool SetClip(int clip)
	{
		if (!Question.IsInteractive || _iaPhase == InteractivePhase.Recording || clip == _clip || clip < 0 || clip >= _clipOrder.Count) return false;
		_clip = clip;
		return true;
	}

	/// <summary>The host plays the current recording: on their screen and on every player's PC.</summary>
	public bool PlayClip()
	{
		if (!Question.IsInteractive || _iaPhase == InteractivePhase.Recording || _clipOrder.Count == 0) return false;
		_clipPlays++;
		return true;
	}

	/// <summary>Everyone has heard them: players vote now.</summary>
	public bool StartVoting()
	{
		if (Stage != Shared.Stage.Open || !Question.IsInteractive || Question.Judging != Judging.Vote || _iaPhase != InteractivePhase.Listening || Paused) return false;
		_iaPhase = InteractivePhase.Voting;
		if (Voters().Count == 0) DoReveal();
		return true;
	}

	/// <summary>The host picks a favourite (judged by the host): they get the question's points.</summary>
	public bool PickFavourite(string playerId)
	{
		if (Stage != Shared.Stage.Open || !Question.IsInteractive || Question.Judging != Judging.Host || _iaPhase != InteractivePhase.Listening
			|| !_clipOrder.Contains(playerId)) return false;
		_favourite = playerId;
		DoReveal();
		return true;
	}

	Result Vote(string playerId, double? clip)
	{
		if (_iaPhase != InteractivePhase.Voting) return Result.Fail("Voting hasn't started yet.");
		if (clip is not { } c || c != Math.Floor(c) || c < 0 || c >= _clipOrder.Count) return Result.Fail("Pick a recording.");
		if (_clipOrder[(int)c] == playerId) return Result.Fail("You can't vote for your own recording.");
		if (_votes.ContainsKey(playerId)) return Result.Fail("You already voted.");
		_votes[playerId] = (int)c;
		CheckInteractiveDone();
		return Result.Success();
	}

	InteractiveView InteractiveView(string viewer) => new()
	{
		Phase = _iaPhase,
		Judging = Question.Judging,
		// (everyone's recordings, once recording is over)
		Clips = _iaPhase == InteractivePhase.Recording && Stage == Shared.Stage.Open ? new()
			: _clipOrder.Select((id, i) => new InteractiveClip { PlayerId = id, Key = RecordingKey(Index, i), Sec = _recordings[id].Sec }).ToList(),
		Clip = _clip,
		Plays = _clipPlays,
		DoneIds = _iaPhase == InteractivePhase.Recording ? _clipOrder.ToList() : _iaPhase == InteractivePhase.Voting ? _votes.Keys.ToList() : new(),
		MyVote = _votes.TryGetValue(viewer, out var mine) ? mine : null,
	};

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
		var active = Active();
		_tlOrder.AddRange(active.Select((id, joined) => (id, joined)).OrderBy(p => _hooks.Score(p.id)).ThenBy(p => p.joined).Select(p => p.id));
		NextItem(_tlOrder.FirstOrDefault(), now);
	}

	/// <summary>The next player after this one in turn order (still connected; with skipMissed, not yet missed the current item).</summary>
	string? NextPlayer(string? after, bool skipMissed)
	{
		var active = Active();
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
		_tlTurn = player != null && Active().Contains(player) ? player : NextPlayer(player, false);
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
		_hooks.NotePlacement(player, _tlResultRight);
		if (_tlResultRight)
		{
			PutDown(item, player);
			_tlPoints[player] = _tlPoints.GetValueOrDefault(player) + Question.Points * Multiplier(player);
			_hooks.Award(player, Question.Points * Multiplier(player));
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
			_hooks.NotePlacement(player, false);
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
	/// <summary>
	/// A number as typed, every digit after the point (0.00000034 stays that), without float noise (0.1 + 0.2 shows
	/// 0.3), in the language's style ("2.5" / German "2,5").
	/// </summary>
	public static string Fmt(double n)
	{
		var clean = double.Parse(n.ToString("G15", CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
		var culture = Godot.TranslationServer.GetLocale() == "de" ? De : En;
		return clean.ToString("#,0.###############", culture);
	}

	static readonly CultureInfo De = CultureInfo.GetCultureInfo("de-DE");

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
		IReadOnlyList<(string PlayerId, double Dist)> dists, int points, string scoring, string tieRule, IReadOnlyList<int>? rankPoints = null)
	{
		// (dists come in the order players locked in: "faster answer wins" keeps that order for the same distance)
		var sorted = dists.Select((d, i) => (d.PlayerId, d.Dist, At: i)).OrderBy(d => d.Dist).ThenBy(d => d.At).ToList();
		var faster = tieRule == TieRule.Faster;
		var result = new Dictionary<string, (int, int)>();
		foreach (var d in sorted)
		{
			var rank = faster ? sorted.IndexOf(d) + 1 : sorted.FindIndex(o => o.Dist == d.Dist) + 1;
			var tied = !faster && sorted.Count(o => o.Dist == d.Dist) > 1;
			var pts = 0;
			// (points per place, e.g. F1's 25 / 18 / 15 …; tied players share the better place's points)
			if (rankPoints is { Count: > 0 }) pts = rank <= rankPoints.Count ? rankPoints[rank - 1] : 0;
			else if (scoring == Scoring.ClosestWins && rank == 1) pts = points;
			else if (scoring == Scoring.Top3 && rank <= 3) pts = (4 - rank) * points;
			if (tied && tieRule == TieRule.NobodyScores) pts = 0;
			result[d.PlayerId] = (rank, pts);
		}
		return result;
	}

	RevealView ComputeReveal()
	{
		var q = Question;
		if (q.IsInteractive)
		{
			var byHost = q.Judging == Judging.Host;
			var votes = _clipOrder.ToDictionary(id => id, id => _votes.Values.Count(c => _clipOrder[c] == id));
			var most = votes.Count > 0 ? votes.Values.Max() : 0;
			return new RevealView
			{
				AnswerText = Lang.T(byHost ? (_favourite != null ? "The host's favourite" : "The host picked no favourite") : most > 0 ? "Most votes" : "No votes"),
				WinnerIds = byHost ? (_favourite != null ? new List<string> { _favourite } : new()) : votes.Where(v => most > 0 && v.Value == most).Select(v => v.Key).ToList(),
				Entries = _clipOrder.Select(id => new RevealEntry
				{
					PlayerId = id,
					Text = byHost ? Lang.T(id == _favourite ? "Favourite" : "Recorded") : votes[id] == 1 ? Lang.T("1 vote") : Lang.F("{0} votes", votes[id]),
					Correct = byHost ? id == _favourite : votes[id] > 0,
					// (a point per vote; the host's favourite gets the question's points)
					Points = byHost ? (id == _favourite ? q.Points : 0) : votes[id],
				}).OrderByDescending(e => e.Points).ToList(),
			};
		}
		if (q.IsBuzzer)
		{
			var winner = _buzzes.FirstOrDefault(b => b.Status == BuzzStatus.Correct);
			return new RevealView
			{
				AnswerText = q.AnswerText,
				WinnerIds = winner != null ? new List<string> { winner.PlayerId } : new List<string>(),
				GuessTimeSec = q.HasPlayClock && winner != null ? winner.AtMs / 1000.0 : null,
				// (one line per player: their last buzz, over every round)
				Entries = _pastBuzzes.Concat(_buzzes).GroupBy(b => b.PlayerId).Select(g => g.Last()).Select(b => new RevealEntry
				{
					PlayerId = b.PlayerId,
					Text = Lang.T(b.Status == BuzzStatus.Correct ? "Correct" : b.Status == BuzzStatus.Wrong ? "Wrong" : "Buzzed, no turn"),
					Correct = b.Status == BuzzStatus.Correct,
					Points = b.Status == BuzzStatus.Correct ? (q.IsSteps ? _stepsWorth ?? 1 : q.Points) : 0,
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
					// (the question's points for every item in its right place; with "wrong places cost points" the same off for each other one)
					Points = right * q.Points - (q.WrongCosts ? (n - right) * q.Points : 0),
				};
			}).OrderByDescending(e => e.Correct).ToList();
			return new RevealView
			{
				AnswerText = OrderText(q),
				WinnerIds = entries.Where(e => e.Correct).Select(e => e.PlayerId).ToList(),
				Entries = entries,
			};
		}

		if (q.IsWritten)
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

		if (q.IsClassicLike)
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
					Points = correct ? q.Points : q.WrongCosts ? -q.Points : 0,
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
			var ranks = ClosenessPoints(_answers.Select(a => (a.PlayerId, Math.Abs(a.Value.Num!.Value - answer))).ToList(), q.Points, q.Scoring, q.TieRule, q.RankPoints);
			var entries = _answers.Select(a =>
			{
				var n = a.Value.Num!.Value;
				var diff = n - answer;
				var r = ranks[a.PlayerId];
				if (answer != 0) _hooks.NotePercentOff(a.PlayerId, Math.Abs(diff / answer) * 100);
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
		foreach (var m in measured)
			if (m.D.Km is { } km) _hooks.NoteDistance(m.PlayerId, km);
		var mapRanks = ClosenessPoints(measured.Select(m => (m.PlayerId, m.D.Km is { } km ? Math.Round(km * 1000) : Math.Round(m.D.Percent, 1))).ToList(), q.Points, q.Scoring, q.TieRule, q.RankPoints);
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

	/// <summary>Typed answers, at the reveal: the host ticks or unticks a player's answer (points follow).</summary>
	public bool ToggleWritten(string playerId)
	{
		if (Stage != Shared.Stage.Reveal || !Question.IsWritten || _reveal?.Entries.FirstOrDefault(e => e.PlayerId == playerId) is not { } entry)
			return false;
		entry.Correct = !entry.Correct;
		SetPoints(entry, ((entry.Correct ? Question.Points : 0) + (entry.Bonus ? Question.BonusPoints : 0)) * Multiplier(playerId));
		_reveal.WinnerIds = _reveal.Entries.Where(e => e.Correct).Select(e => e.PlayerId).ToList();
		return true;
	}

	/// <summary>At the reveal: the host gives a player the question's bonus, or takes it back.</summary>
	public bool ToggleBonus(string playerId)
	{
		if (Stage != Shared.Stage.Reveal || !Question.HasBonus || _reveal?.Entries.FirstOrDefault(e => e.PlayerId == playerId) is not { } entry)
			return false;
		entry.Bonus = !entry.Bonus;
		var bonus = (entry.Bonus ? Question.BonusPoints : -Question.BonusPoints) * Multiplier(playerId);
		SetPoints(entry, entry.Points + bonus);
		_hooks.NoteHostPoints(playerId, bonus);
		return true;
	}

	void SetPoints(RevealEntry entry, int points)
	{
		_hooks.Award(entry.PlayerId, points - entry.Points);
		entry.Points = points;
	}

	/// <summary>"Doom → Half-Life → Portal" (a timeline with its labels: "Doom (1993) → …").</summary>
	static string OrderText(Question q) => string.Join(" → ", q.CorrectOrder.Select(i =>
		q.IsTimeline && q.ItemLabel(i) is { Length: > 0 } label ? $"{q.Items[i]} ({label})" : q.Items[i]));

	static List<int> Picks(AnswerValue v) => v.Picks ?? (v.Num is { } n ? new List<int> { (int)n } : new List<int>());

	/// <summary>"C: 29", or for several "A: Red · C: Blue".</summary>
	static string ChoiceText(Question q, IEnumerable<int> picks) =>
		string.Join(" · ", picks.Select(i => $"{QuizRules.ChoiceLetters[i]}: {q.Choices[i]}"));

	/// <summary>An image reveal's picture while its question is still open.</summary>
	public static string ImageKey(int index) => $"q{index}/image";
	public static string MapKey(int index) => $"q{index}/map";
	public static string SlideKey(int index, int slide) => $"q{index}/slide{slide}";

	/// <summary>Picture questions: the picture showing now (the host flips through them).</summary>
	public int Slide { get; private set; }

	/// <summary>Picture slideshows: how many pictures the host has shown so far (once all, players may flip them themselves).</summary>
	int _slidesSeen = 1;

	/// <summary>Step by step: the hint showing now (0 = the first); flipped by the host.</summary>
	public int Hint { get; private set; }
	/// <summary>Step by step: what the right answer was worth when it was given.</summary>
	int? _stepsWorth;

	/// <summary>Step by step: a right answer now gets as many points as there are hints left (5, 4, … 1 with five).</summary>
	int StepsWorth => Math.Max(1, Question.FilledHints.Count - Hint);

	/// <summary>
	/// The host flips the next hint (or reads on: the next part of the sentence): everyone who isn't in line gets another
	/// go at buzzing. A sentence waits while someone has buzzed: the reading stops until they're judged.
	/// </summary>
	public bool NextHint()
	{
		var q = Question;
		if (Stage != Shared.Stage.Open || !q.IsSteps || !Shown || Paused || Hint + 1 >= q.FilledHints.Count) return false;
		if (q.IsSentence && _buzzes.Any(b => b.Status is BuzzStatus.Answering or BuzzStatus.Waiting)) return false;
		Hint++;
		if (_buzzes.Count > 0 && _buzzes.All(b => b.Status == BuzzStatus.Wrong))
		{
			_pastBuzzes.AddRange(_buzzes);
			_buzzes.Clear();
		}
		return true;
	}

	/// <summary>The host shows another picture of a picture question (any time during the question and its reveal).</summary>
	public bool SetSlide(int slide)
	{
		var q = Question;
		if (q.Type != QuestionType.Picture || Finished || slide == Slide || slide < 0 || slide >= q.Slides.Count) return false;
		Slide = slide;
		_slidesSeen = Math.Max(_slidesSeen, slide + 1);
		return true;
	}

	/// <summary>The category players may see (already on the Get ready screen): only if the lobby shows them.</summary>
	string ShownCategory => _hooks.ShowCategories() && !Question.CategorySecret ? Question.Category : "";

	PublicQuestion PublicQuestion()
	{
		var q = Question;
		var pq = new PublicQuestion
		{
			Type = q.Type,
			Prompt = q.Prompt,
			Category = ShownCategory,
			Points = q.HasRankPoints ? q.MaxPoints : q.Points,
			RankScoring = q.HasRankPoints,
			Buzzer = q.IsBuzzer,
			Written = q.IsWritten,
			BonusPoints = q.BonusPoints,
			BonusText = q.HasBonus ? q.BonusText : null,
			// (players see only what's shown: an image reveal's picture is revealed on their screen in step with the host's)
			ImageKey = q.HasImageField && q.Image != null ? ImageKey(Index) : null,
		};
		if (q.Type == QuestionType.Picture && q.Slides.Count > 0)
		{
			pq.ImageKey = SlideKey(Index, Slide);
			pq.Slide = Slide;
			pq.SlideCount = q.Slides.Count;
			pq.AllSlidesShown = _slidesSeen >= q.Slides.Count || Stage == Shared.Stage.Reveal;
		}
		if (q.IsClassicLike)
		{
			pq.Mode = q.Mode;
			if (q.IsChoice)
			{
				pq.Choices = q.Choices.Select(c => string.IsNullOrWhiteSpace(c) ? null : c).ToList();
				pq.TimeLimitSec = q.TimeLimitSec;
				pq.WrongCosts = q.WrongCosts;
			}
		}
		if (q.Type == QuestionType.Order)
		{
			pq.Items = q.Items;
			pq.OrderTop = q.OrderTop;
			pq.OrderBottom = q.OrderBottom;
			pq.OrderMode = q.OrderMode;
			pq.TimeLimitSec = q.TimeLimitSec;
			pq.WrongCosts = !q.IsTimeline && q.WrongCosts;
		}
		if (q.IsWritten) pq.TimeLimitSec = q.TimeLimitSec;
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
		if (q.IsInteractive) pq.TimeLimitSec = q.TimeLimitSec;
		if (q.IsSteps)
		{
			var hints = q.FilledHints;
			// (players see the flipped ones; all of them at the reveal)
			pq.Hints = Stage == Shared.Stage.Reveal ? hints : hints.Take(Hint + 1).ToList();
			pq.StepsMode = q.StepsMode;
			pq.HintCount = hints.Count;
			pq.WorthNow = StepsWorth;
			pq.Points = hints.Count;
		}
		if (q.Type == QuestionType.Image)
		{
			// Players reveal the picture on their own screen, in step with the host's (the play clock).
			pq.RevealStyle = q.RevealStyle;
			pq.RevealSec = q.RevealSec;
			pq.Focus = q.Focus;
		}
		// (the shown question's pictures: the id and key to unlock the copy players already have)
		var files = new Dictionary<string, SharedFileRef>();
		void Unlock(string? key, string? asset)
		{
			if (key != null && SharedFiles.FirstOrDefault(f => f.Asset == (asset ?? key)) is { } f)
				files[key] = new SharedFileRef { Id = f.Id, Key = Convert.ToBase64String(f.Key), Mime = f.Mime };
		}
		Unlock(pq.ImageKey, null);
		// (every picture the host has shown, so players can flip back through them)
		if (q.Type == QuestionType.Picture)
			for (var s = 0; s < Math.Min(q.Slides.Count, pq.AllSlidesShown ? q.Slides.Count : _slidesSeen); s++) Unlock(SlideKey(Index, s), null);
		if (pq.Map?.AssetKey is { } mapKey && _quiz.MapById(q.MapId) is { } usedMap) Unlock(mapKey, "map:" + usedMap.Id);
		if (files.Count > 0) pq.Files = files;
		if (CurrentMedia is { } video)
		{
			pq.MediaId = video.Id;
			pq.MediaKey = Convert.ToBase64String(video.Key);
			pq.MediaMime = video.Mime;
		}
		return pq;
	}

	string HostAnswer()
	{
		var q = Question;
		if (q.IsClassicLike) return q.IsChoice ? ChoiceText(q, q.CorrectSet) : q.AnswerText;
		if (q.Type == QuestionType.Order) return OrderText(q);
		if (q.Type == QuestionType.Estimation) return $"{Fmt(q.AnswerNumber ?? 0)}{(q.Unit != "" ? " " + q.Unit : "")}";
		if (q.Type == QuestionType.MapPin) return Lang.T("The marked spot on the map (shown at the reveal).");
		if (q.IsSteps) return $"{q.AnswerText}  ·  {Lang.T(q.IsSentence ? "next part" : "next hint")}: {(Hint + 1 < q.FilledHints.Count ? q.FilledHints[Hint + 1] : "–")}";
		if (q.IsInteractive) return Lang.T(q.Judging == Judging.Host ? "You pick your favourite recording." : "The players vote for the best recording.");
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
			Question = Shown || viewer == "host" || !open ? PublicQuestion() : new PublicQuestion { Type = Question.Type, Category = ShownCategory, Points = Question.HasRankPoints ? Question.MaxPoints : Question.Points, RankScoring = Question.HasRankPoints, Buzzer = Question.IsBuzzer, Written = Question.IsWritten, Mode = Question.IsClassicLike ? Question.Mode : null, OrderMode = Question.Type == QuestionType.Order ? Question.OrderMode : null, WrongCosts = (Question.IsChoice || Question.Type == QuestionType.Order && !Question.IsTimeline) && Question.WrongCosts, Judging = Question.IsInteractive ? Question.Judging : null },
			Shown = Shown || !open,
			BuzzersOpen = BuzzersOpen,
			EndsAt = open ? _endsAt : null,
			AnswerEndsAt = open && _answeringSince is { } since ? since + _hooks.AnswerTimeSec() * 1000L : null,
			// (interactive: who has sent a recording, then who has voted)
			AnsweredIds = Question.IsInteractive
				? (_iaPhase == InteractivePhase.Voting ? _votes.Keys.ToList() : _clipOrder.ToList())
				: _answers.Select(a => a.PlayerId).ToList(),
			Buzzes = _buzzes.Select(b => new BuzzEntry { PlayerId = b.PlayerId, Status = b.Status, AtSec = b.AtMs / 1000.0, FastPass = b.FastPass }).ToList(),
			MediaPlaying = _playingSince != null,
			MediaPlayedMs = PlayTimeMs(_hooks.Now()),
			MediaStarted = _playingSince != null || _playedMs > 0,
			Reveal = _reveal,
			// (the timeline only exists once the question is shown)
			Timeline = Question.IsTimeline && Shown ? TimelineView() : null,
			Interactive = Question.IsInteractive && (Shown || !open) ? InteractiveView(viewer) : null,
			Paused = Paused,
			WaitingFor = _waitingFor.ToList(),
			LateIds = _late.ToList(),
			JokerIndex = JokerIndex,
			JokerBuzzer = JokerIndex is { } ji && _quiz.Questions[ji].IsBuzzer,
			// (that someone has it, not who: still a secret)
			FastPassTaken = JokerIndex is { } fi && FastPassTaken(fi, viewer),
			// (a joker for a question not shown yet is a secret: only its player sees it, and can take it back)
			Jokers = _jokers.Where(j => j.PlayerId == viewer || Locked(j)).Select(j => new JokerPlay { PlayerId = j.PlayerId, Kind = j.Kind, Index = j.Index }).ToList(),
		};
		if (viewer == "host") view.HostAnswer = HostAnswer();
		else view.MyAnswer = _answers.FirstOrDefault(a => a.PlayerId == viewer).Value;
		return view;
	}

	// ---------- jokers ----------

	/// <summary>
	/// The question jokers go on right now: the current one while it's still hidden, the next one during a reveal.
	/// Null while a question is running (too late) and after the last one.
	/// </summary>
	public int? JokerIndex =>
		Stage == Shared.Stage.Open && !Shown ? Index
		: Stage == Shared.Stage.Reveal && Index + 1 < _quiz.Questions.Count ? Index + 1
		: null;

	bool Played(string playerId, string kind, int index) => _jokers.Any(j => j.PlayerId == playerId && j.Kind == kind && j.Index == index);

	/// <summary>A joker counts (and everyone sees it) once its question is shown.</summary>
	bool Locked(JokerPlay j) => j.Index < Index || j.Index == Index && (Shown || Stage == Shared.Stage.Reveal);

	/// <summary>2 for a player who played Double points on the current question, else 1.</summary>
	int Multiplier(string playerId) => Played(playerId, Joker.Double, Index) ? 2 : 1;

	/// <summary>A player plays a joker for the next question (on), or takes it back before that question is shown.</summary>
	bool FastPassTaken(int index, string viewer) => _jokers.Any(j => j.Kind == Joker.FastPass && j.Index == index && j.PlayerId != viewer);

	public Result PlayJoker(string playerId, string kind, bool on)
	{
		Trace.Log("game", $"joker {kind} {(on ? "played" : "taken back")} by {playerId} (for question {(JokerIndex is { } j ? j + 1 : 0)})");
		if (!Joker.All.Contains(kind) || !_hooks.JokerAllowed(kind)) return Result.Fail("The host didn't allow that joker tonight.");
		var mine = _jokers.FirstOrDefault(j => j.PlayerId == playerId && j.Kind == kind);
		if (!on)
		{
			if (mine == null || Locked(mine)) return Result.Fail("That joker is already in play.");
			_jokers.Remove(mine);
			return Result.Success();
		}
		if (mine != null) return Result.Fail("You already used that joker.");
		if (JokerIndex is not { } index) return Result.Fail("Jokers are played before a question starts.");
		if (kind == Joker.FastPass && !_quiz.Questions[index].IsBuzzer) return Result.Fail("The Fast Pass is only for buzzer questions.");
		// (Double: anyone; Fast Pass: one player per question, first come)
		if (kind == Joker.FastPass && FastPassTaken(index, playerId)) return Result.Fail("Someone else already has the Fast Pass for this question.");
		_jokers.Add(new JokerPlay { PlayerId = playerId, Kind = kind, Index = index });
		return Result.Success();
	}

	// ---------- someone left or joined ----------

	public bool Paused => _pausedAt != null;

	/// <summary>A player dropped out: everything waits (timers, buzzers, answers, the host's media) until they're back.</summary>
	public void PlayerLeft(string playerId)
	{
		_waitingFor.Add(playerId);
		if (_pausedAt != null) return;
		_pausedAt = _hooks.Now();
		SetMedia(false);
	}

	/// <summary>A player who left is back (or was removed): the game goes on once nobody is missing.</summary>
	public bool PlayerBack(string playerId) => _waitingFor.Remove(playerId) && _waitingFor.Count == 0 && Resume();

	/// <summary>The host goes on without the players who left.</summary>
	public bool Continue()
	{
		if (!Paused) return false;
		_waitingFor.Clear();
		Resume();
		return true;
	}

	bool Resume()
	{
		if (_pausedAt is not { } since) return false;
		_pausedAt = null;
		// The clocks pick up where they stopped.
		var away = _hooks.Now() - since;
		_openedAt += away;
		if (_endsAt != null) _endsAt += away;
		if (_answeringSince != null) _answeringSince += away;
		if (_tlTurnEnds != null) _tlTurnEnds += away;
		CheckAllAnswered();
		return true;
	}

	/// <summary>Someone joined while a question runs: they play from the next one.</summary>
	public void LateJoin(string playerId)
	{
		if (Stage == Shared.Stage.Open && Shown) _late.Add(playerId);
	}

	/// <summary>Image data for an asset key — only for questions already shown.</summary>
	/// <param name="host">The host's own screen: may also have what players can't yet.</param>
	public string? AssetDataUrl(string key, bool host = false)
	{
		var parts = key.Split('/');
		if (parts.Length != 2 || !parts[0].StartsWith('q') || !int.TryParse(parts[0].AsSpan(1), out var index)) return null;
		if (index < 0 || index > Index || index >= _quiz.Questions.Count) return null;
		if (!host && index == Index && !Shown && Stage == Shared.Stage.Open) return null;
		var q = _quiz.Questions[index];
		if (parts[1] == "image" && q.HasImageField && q.Image != null) return q.Image.DataUrl;
		if (parts[1] == "map" && q.Type == QuestionType.MapPin) return _quiz.MapById(q.MapId)?.Image.DataUrl;
		// (recordings: once recording is over, while the question and its reveal are on)
		if (parts[1].StartsWith("rec") && int.TryParse(parts[1].AsSpan(3), out var clip) && q.IsInteractive && index == Index
			&& (host || _iaPhase != InteractivePhase.Recording || Stage != Shared.Stage.Open) && clip >= 0 && clip < _clipOrder.Count)
			return _recordings[_clipOrder[clip]].DataUrl;
		// (players get the pictures shown so far, never the ones the host hasn't flipped to yet)
		if (parts[1].StartsWith("slide") && int.TryParse(parts[1].AsSpan(5), out var slide) && q.Type == QuestionType.Picture
			&& slide >= 0 && slide < q.Slides.Count && (host || index < Index || Stage != Shared.Stage.Open || slide < _slidesSeen))
			return q.Slides[slide].DataUrl;
		return null;
	}
}
