// Checks for the game rules, quiz files and networking. Run headless:
//   Godot_console.exe --headless --path . -- --selftest
// Exits with 0 when everything passes.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using QuizNight.Net;
using QuizNight.Server;
using QuizNight.Shared;

namespace QuizNight.Dev;

public static class SelfTest
{
	static int _passed;
	static readonly List<string> Failures = new();

	static void Check(bool ok, string what)
	{
		if (ok) _passed++;
		else Failures.Add(what);
	}

	static void Eq<T>(T actual, T expected, string what) => Check(Equals(actual, expected), $"{what}: expected {expected}, got {actual}");

	sealed class Clock
	{
		public long Now = 1_000_000;
	}

	/// <summary>A room whose questions show and whose buzzers open by themselves (host control is tested on its own).</summary>
	static (Room Room, Clock Clock) NewRoom(Quiz quiz, params string[] players)
	{
		var clock = new Clock();
		var room = new Room("TEST", quiz, () => clock.Now);
		room.UpdateSettings(hostShows: false, hostUnlocks: false);
		foreach (var p in players) room.Join(p, p);
		return (room, clock);
	}

	/// <summary>Put in order as a timeline: turns (fewest points first), wrong guesses pass the item on, time-outs, skips, free placing.</summary>
	static void Timeline(Room room, Clock clock, Game g)
	{
		Check(g.Question.IsTimeline, "timeline question");
		var years = new Dictionary<string, int> { ["Doom"] = 1993, ["Half-Life"] = 1998, ["Portal"] = 2007, ["Elden Ring"] = 2022 };
		TimelineView Tl() => g.View("host").Timeline!;
		int Right() => Tl().Placed.Count(c => years[c.Text] < years[Tl().Current!]);
		int Wrong() => Enumerable.Range(0, Tl().Placed.Count + 1).First(i => i != Right() && !Tl().WrongGaps.Contains(i));
		var start = room.Scores.ToDictionary(p => p.Key, p => p.Value);

		Check(g.View("host").EndsAt == null, "a timeline has no question timer, only turn timers");
		Eq(Tl().Placed.Count, 1, "one item starts on the timeline");
		Check(Tl().Placed[0] is { Text: "Half-Life", Label: "1998", Start: true }, "the start item shows its year");
		var order = Tl().TurnOrder;
		Check(order.Select(id => start[id]).SequenceEqual(order.Select(id => start[id]).OrderBy(s => s)), "fewest points go first");
		Eq(Tl().TurnPlayerId, order[0], "the first player's turn");
		Check(g.View("ann").Timeline?.Current != null && g.View("ann").Reveal == null, "players see the item to place");
		Check(!g.Answer(order[1], 11, AnswerValue.Number(0)).Ok, "only the player whose turn it is can place");

		// Item 1: order[0] is wrong, order[1] gets it and keeps going. Nothing is judged until the host reveals it.
		var wrongGap = Wrong();
		Check(g.Answer(order[0], 11, AnswerValue.Number(wrongGap)).Ok, "a guess is accepted");
		Check(Tl().Phase == TimelinePhase.Locked && Tl().PendingGap == wrongGap && Tl().TurnEndsAt == null, "locked in: waits for the host, timer stopped");
		Check(!g.Answer(order[0], 11, AnswerValue.Number(Right())).Ok, "no changing a locked-in spot");
		clock.Now += 60_000;
		room.Tick();
		Eq(Tl().Phase, TimelinePhase.Locked, "no time-out while waiting for the host");
		Check(!g.NextTurn(), "no next turn before the reveal");
		Check(g.RevealPlacement() && Tl().Phase == TimelinePhase.Result, "the host reveals");
		Check(Tl().WrongGaps.Contains(wrongGap) && Tl().Last!.Kind == TimelineEventKind.Wrong && Tl().TurnPlayerId == order[0], "wrong: marked, the turn stays until the host goes on");
		Check(g.NextTurn() && Tl().TurnPlayerId == order[1] && Tl().Phase == TimelinePhase.Choosing, "next turn: the next player tries the same item");
		Check(!g.Answer(order[1], 11, AnswerValue.Number(wrongGap)).Ok, "a gap already tried can't be picked again");
		Check(g.Answer(order[1], 11, AnswerValue.Number(Right())).Ok, "the right gap");
		Eq(Tl().Placed.Count, 1, "not placed before the reveal");
		g.RevealPlacement();
		Eq(Tl().Placed.Count, 2, "placed at the reveal");
		Check(Tl().WrongGaps.Count == 0, "once placed, the old wrong gaps are gone");
		Eq(room.Scores[order[1]] - start[order[1]], 1, "a placed item scores right away");
		g.NextTurn();
		Eq(Tl().TurnPlayerId, order[1], "whoever is right keeps going");

		// Item 2: time runs out (waits for the host too), the host skips, the last one misses: it's placed for free.
		clock.Now += 31_000;
		room.Tick();
		Check(Tl().Phase == TimelinePhase.Result && Tl().Last!.Kind == TimelineEventKind.TimeUp, "time up waits for the host");
		Check(g.NextTurn() && Tl().TurnPlayerId == order[2], "time up passes the item on");
		Check(g.SkipTurn() && Tl().TurnPlayerId == order[0] && Tl().Phase == TimelinePhase.Choosing, "the host skips a player");
		g.Answer(order[0], 11, AnswerValue.Number(Wrong()));
		g.RevealPlacement();
		g.NextTurn();
		Check(Tl().Placed.Count == 3 && Tl().Last!.Kind == TimelineEventKind.Free, "nobody got it: placed for free");
		Eq(Tl().TurnPlayerId, order[1], "the next player starts the next item");

		// Item 3, the last one: after the reveal, the host's next step shows the results.
		g.Answer(order[1], 11, AnswerValue.Number(Right()));
		g.RevealPlacement();
		Eq(g.Stage, Stage.Open, "the results wait for the host");
		g.NextTurn();
		Eq(g.Stage, Stage.Reveal, "all placed: reveal");
		var reveal = g.View("host").Reveal!;
		Eq(reveal.AnswerText, "Doom (1993) → Half-Life (1998) → Portal (2007) → Elden Ring (2022)", "timeline answer with years");
		Eq(reveal.Entries[0].PlayerId, order[1], "the reveal lists the best first");
		Eq(reveal.Entries[0].Text, "placed 2 items", "how many each placed");
		Eq(room.Scores[order[1]] - start[order[1]], 2, "points aren't given twice at the reveal");
		Eq(room.Scores[order[0]] - start[order[0]], 0, "no points for misses");
	}

	/// <summary>A buzzer question played as "everyone types", the host's bonus and score changes, and the file format.</summary>
	static void TypedAndBonus()
	{
		var typed = new Question { Id = "t1", Type = QuestionType.Sound, Prompt = "Name this song!", AnswerText = "Dragonborn", Typed = true, TimeLimitSec = 30, Points = 1,
			BonusPoints = 2, BonusText = "Also name the game", Media = new MediaAsset { DataUrl = "data:audio/ogg;base64,AA==", Mime = "audio/ogg", Name = "x.ogg" } };
		Check(!typed.IsBuzzer && typed.IsWritten && typed.HasTimeLimit, "a typed sound question has no buzzers and a time limit");
		Check(new Question { Type = QuestionType.Classic, Mode = ClassicMode.Choice, Typed = true }.IsBuzzer == false
			&& !new Question { Type = QuestionType.Classic, Mode = ClassicMode.Choice, Typed = true }.IsTyped, "A–D questions can't be typed");
		var quiz = new Quiz { Id = "tq", Title = "Typed", Questions = { typed, new Question { Id = "t2", Type = QuestionType.Classic, Prompt = "Red planet?", AnswerText = "Mars", Points = 1 } } };
		Eq(QuizRules.QuestionIssues(typed, quiz.Maps).Count, 0, "typed question with a bonus is playable");
		var back = QuizJson.Parse(QuizJson.Write(quiz), out _)!.Questions[0];
		Check(back.Typed && back.BonusPoints == 2 && back.BonusText == "Also name the game" && back.TimeLimitSec == 30, "typed, bonus and time limit are saved");
		Check(QuizRules.QuestionIssues(new Question { Type = QuestionType.Write, Prompt = "x", AnswerText = "y", BonusPoints = 1 }, quiz.Maps)
			.Contains("Say what the bonus is for."), "a bonus needs to say what it's for");

		var (room, clock) = NewRoom(quiz, "ann", "bob");
		Check(room.Start().Ok, "start typed quiz");
		var g = room.Game!;
		Check(!g.BuzzIn("ann"), "no buzzing on a typed question");
		Eq(g.View("ann").Question.Written, true, "players get a text field");
		Eq(g.View("ann").Question.BonusText, "Also name the game", "players see the bonus");
		Check(g.View("host").EndsAt == clock.Now + 30_000, "typed question runs on its time limit");
		g.Answer("ann", 0, AnswerValue.Written("dragon born"));
		g.Answer("bob", 0, AnswerValue.Written("Skyrim"));
		Eq(g.Stage, Stage.Reveal, "reveal once both typed");
		Eq(room.Scores["ann"], 1, "the right song scores");
		Eq(room.Scores["bob"], 0, "a wrong one doesn't");
		Check(g.ToggleBonus("ann") && room.Scores["ann"] == 3, "host gives the bonus (+2)");
		Check(g.ToggleWritten("bob") && room.Scores["bob"] == 1, "host ticks bob");
		Check(g.ToggleBonus("ann") && room.Scores["ann"] == 1, "and takes the bonus back");
		Check(g.ToggleBonus("ann") && g.ToggleWritten("ann") && room.Scores["ann"] == 2, "unticking keeps the bonus");
		Check(g.View("host").Reveal!.Entries.First(e => e.PlayerId == "ann").Bonus, "the reveal shows who got the bonus");
		room.Next();
		Check(!g.ToggleBonus("ann"), "no bonus on a question without one");
		Check(g.BuzzIn("ann"), "the next (buzzer) question still buzzes");
	}

	public static async Task<int> Run(Node node)
	{
		Rules();
		TypedAndBonus();
		Files();
		// Load, not just Exists: an Opus .ogg exists but Godot can't import it.
		foreach (var b in Storage.Sfx.Buzzers) Check(ResourceLoader.Load<AudioStream>(b.Path) != null, $"buzzer sound {b.Id} plays");
		Check(ResourceLoader.Load<AudioStream>(Storage.Sfx.PodiumJingle) != null, "podium jingle plays (must be Ogg Vorbis, MP3 or WAV — not Opus)");
		Atlas();
		Protocol();
		await Network(node);

		GD.Print($"Self-test: {_passed} passed, {Failures.Count} failed.");
		foreach (var f in Failures) GD.PrintErr("  FAIL " + f);
		return Failures.Count == 0 ? 0 : 1;
	}

	static void Rules()
	{
		// Closeness ranking: ties share a rank, nobodyScores drops tied winners.
		var d = new List<(string, double)> { ("a", 5), ("b", 1), ("c", 1), ("d", 9) };
		var r = Game.ClosenessPoints(d, 2, Scoring.Top3, TieRule.AllScore);
		Eq(r["b"].Rank, 1, "tie rank b");
		Eq(r["c"].Rank, 1, "tie rank c");
		Eq(r["a"].Rank, 3, "rank after tie");
		Eq(r["b"].Points, 6, "top3 first gets 3x");
		Eq(r["a"].Points, 2, "top3 third gets 1x");
		Eq(r["d"].Points, 0, "fourth gets nothing");
		var none = Game.ClosenessPoints(d, 1, Scoring.ClosestWins, TieRule.NobodyScores);
		Eq(none["b"].Points, 0, "nobodyScores tie");
		Eq(Game.FormatDistance(0.312, 1), "312 m", "metres");
		Eq(Game.FormatDistance(1.234, 1), "1.23 km", "km");
		Eq(Game.FormatDistance(null, 12.34), "12.3 % of the map", "percent");
		Eq(Game.Fmt(1234.5), "1,234.5", "number format");

		var quiz = Dev.SampleQuiz.Build();
		// (the sample plays its sound question as "everyone types"; here it buzzes, to test the play clock)
		quiz.Questions.First(x => x.Type == QuestionType.Sound).Typed = false;
		Eq(QuizRules.QuizIssues(quiz).Count, 0, "sample quiz is ready: " + string.Join(" ", QuizRules.QuizIssues(quiz)));

		// Buzzer question: wrong, then the next in line is right.
		var (room, clock) = NewRoom(quiz, "ann", "bob", "cat");
		Check(room.Start().Ok, "start");
		room.Settings.BuzzerSound = "metal-pipe";
		Eq(room.ToState("ann").Settings.BuzzerSound, "metal-pipe", "players are told the buzzer sound");
		var g = room.Game!;
		clock.Now += 1500;
		Check(g.BuzzIn("bob"), "bob buzzes");
		Check(!g.BuzzIn("bob"), "no double buzz");
		clock.Now += 500;
		g.BuzzIn("ann");
		var v = g.View("cat");
		Eq(v.Buzzes[0].Status, BuzzStatus.Answering, "first buzz answers");
		Eq(v.Buzzes[1].Status, BuzzStatus.Waiting, "second waits");
		Eq(v.Buzzes[0].AtSec, 1.5, "buzz time");
		Check(v.HostAnswer == null, "players never see the host answer");
		Check(room.ToState("host").Game!.HostAnswer == "Mars", "host sees the answer");
		g.Judge(false);
		Eq(g.View("host").Buzzes[1].Status, BuzzStatus.Answering, "next in line answers");
		g.Judge(true);
		Eq(g.Stage, Stage.Reveal, "correct reveals");
		Eq(room.Scores["ann"], 1, "ann scored");
		Eq(room.Scores["bob"], 0, "bob didn't");

		// Choice question: everyone answers → reveal.
		room.Next();
		Eq(g.Index, 1, "next question");
		Check(g.View("host").EndsAt != null, "choice has a timer");
		// Answers are shuffled for the game, so find where "29" ended up.
		var right = g.Question.CorrectChoice;
		Eq(g.Question.Choices[right], "29", "shuffled choice keeps its right answer");
		Eq(quiz.Questions[1].CorrectChoice, 2, "the quiz itself isn't shuffled");
		Check(!g.Answer("ann", 1, AnswerValue.Number(7)).Ok, "invalid choice rejected");
		Check(g.Answer("ann", 1, AnswerValue.Number(right)).Ok, "ann answers");
		Check(!g.Answer("ann", 1, AnswerValue.Number(1)).Ok, "no second answer");
		Eq(g.View("ann").MyAnswer?.Num, (double)right, "my answer visible to me");
		Check(g.View("bob").MyAnswer == null, "not to others");
		g.Answer("bob", 1, AnswerValue.Number((right + 1) % 4));
		Eq(g.Stage, Stage.Open, "still waiting for cat");
		room.Disconnect("cat");
		Eq(g.Stage, Stage.Reveal, "reveals when the last missing player leaves");
		Eq(room.Scores["ann"], 3, "choice points");
		Eq(g.View("host").Reveal!.Counts![right], 1, "counts");

		// Estimation with a timer that runs out.
		room.Join("cat", "cat");
		room.Next();
		g.Answer("ann", 2, AnswerValue.Number(90));
		g.Answer("bob", 2, AnswerValue.Number(80));
		clock.Now += 31_000;
		Check(room.Tick(), "timer fires");
		var rev = g.View("host").Reveal!;
		Eq(rev.Entries[0].PlayerId, "ann", "closest first");
		Eq(rev.Entries[0].Text, "90 keys (+2)", "estimation text");
		Eq(rev.Entries[0].Points, 3, "top3 first");
		Eq(rev.Entries[1].Points, 2, "top3 second");

		// Map pin on Skyrim: exact answer is 0 m off.
		room.Next();
		g.Answer("ann", 3, AnswerValue.At(new NormPoint(0.57, 0.555)));
		g.Answer("bob", 3, AnswerValue.At(new NormPoint(0.575, 0.555)));
		g.Answer("cat", 3, AnswerValue.At(new NormPoint(0.9, 0.9)));
		rev = g.View("host").Reveal!;
		Eq(rev.Entries[0].Text, "0 m off", "exact pin");
		Check(rev.Entries[1].Text.EndsWith("m off"), "near pin in m: " + rev.Entries[1].Text);
		Check(g.AssetDataUrl("q3/image") != null, "current image available");
		Check(g.AssetDataUrl("q5/image") == null, "future image hidden");
		Eq(g.View("bob").Question.Map!.PresetId, "skyrim", "built-in map by preset");

		// Sound: the play clock only counts while playing.
		for (var i = 0; i < 5; i++) room.Next();
		Eq(g.Question.Type, QuestionType.Sound, "sound question");
		g.SetMedia(true);
		clock.Now += 2000;
		g.SetMedia(false);
		clock.Now += 5000;
		g.SetMedia(true);
		clock.Now += 500;
		g.BuzzIn("cat");
		g.Judge(true);
		Eq(g.View("host").Reveal!.GuessTimeSec, 2.5, "guess time ignores pauses");

		// Multiple choice: only exactly the right answers score.
		room.Next();
		Check(g.Question.IsMulti, "multi question");
		var rightSet = g.Question.CorrectSet;
		Eq(string.Join(",", rightSet.Select(i => g.Question.Choices[i]).OrderBy(s => s)), "Mars,Venus", "shuffled multi keeps its right answers");
		Check(!g.Answer("ann", 7, AnswerValue.Picked(new int[0])).Ok, "empty multi answer rejected");
		Check(!g.Answer("ann", 7, AnswerValue.Picked(new[] { 9 })).Ok, "invalid multi answer rejected");
		var before = (Ann: room.Scores["ann"], Bob: room.Scores["bob"], Cat: room.Scores["cat"]);
		Check(g.Answer("ann", 7, AnswerValue.Picked(rightSet.AsEnumerable().Reverse())).Ok, "ann picks both");
		g.Answer("bob", 7, AnswerValue.Picked(rightSet.Take(1)));
		g.Answer("cat", 7, AnswerValue.Picked(new[] { 0, 1, 2, 3 }));
		Eq(g.Stage, Stage.Reveal, "multi reveals when all answered");
		Eq(room.Scores["ann"] - before.Ann, 2, "exact set scores");
		Eq(room.Scores["bob"] - before.Bob, 0, "half the set doesn't");
		Eq(room.Scores["cat"] - before.Cat, 0, "picking everything doesn't");
		Eq(string.Join(",", g.View("host").Reveal!.CorrectIndices!), string.Join(",", rightSet), "reveal marks both");
		Eq(g.View("host").Reveal!.Counts![rightSet[0]], 3, "multi counts");

		// Image reveal: players get the picture once it's shown and reveal it in step with the host; buzz times use its clock.
		room.Next();
		Eq(g.Question.Type, QuestionType.Image, "image reveal question");
		g.ShowQuestion();
		var revealQ = g.View("ann").Question;
		Check(revealQ.ImageKey != null && g.AssetDataUrl(Game.ImageKey(8)) != null, "players get the picture once it's shown");
		Check(revealQ.RevealStyle != null && revealQ.RevealSec > 0 && revealQ.Focus != null, "with how it's revealed");
		g.SetMedia(true);
		clock.Now += 4000;
		g.BuzzIn("bob");
		g.SetMedia(false);
		Eq(g.View("host").Buzzes[0].AtSec, 4.0, "buzz time on the reveal clock");
		g.Judge(true);
		Eq(g.View("host").Reveal!.GuessTimeSec, 4.0, "guessed after 4 s");
		Check(g.View("ann").Question.ImageKey != null && g.AssetDataUrl(Game.ImageKey(8)) != null, "everyone gets the picture at the reveal");

		// Put in order: shown shuffled; only the perfect order scores.
		room.Next();
		Eq(g.Question.Type, QuestionType.Order, "order question");
		var shownItems = g.View("ann").Question.Items!;
		Eq(string.Join(",", g.Question.CorrectOrder.Select(i => shownItems[i])), "Doom,Half-Life,Portal,Elden Ring", "shuffled order keeps the right order");
		Check(!shownItems.SequenceEqual(new[] { "Doom", "Half-Life", "Portal", "Elden Ring" }), "never shown already in order");
		Check(!g.Answer("ann", 9, AnswerValue.Picked(new[] { 0, 0, 1, 2 })).Ok, "an order must use every item once");
		before = (room.Scores["ann"], room.Scores["bob"], room.Scores["cat"]);
		g.Answer("ann", 9, AnswerValue.Picked(g.Question.CorrectOrder));
		g.Answer("bob", 9, AnswerValue.Picked(g.Question.CorrectOrder.AsEnumerable().Reverse()));
		g.Answer("cat", 9, AnswerValue.Picked(new[] { g.Question.CorrectOrder[1], g.Question.CorrectOrder[0], g.Question.CorrectOrder[2], g.Question.CorrectOrder[3] }));
		Eq(g.Stage, Stage.Reveal, "order reveals when all answered");
		Eq(room.Scores["ann"] - before.Ann, 2, "perfect order scores");
		Eq(room.Scores["cat"] - before.Cat, 0, "almost right doesn't");
		Eq(g.View("host").Reveal!.Entries.First(e => e.PlayerId == "cat").Text, "2 of 4 in the right place", "order reveal text");
		Eq(g.View("host").Reveal!.AnswerText, "Doom → Half-Life → Portal → Elden Ring", "order answer");

		// Write the answer: close enough is ticked; the host can tick or untick.
		room.Next();
		Eq(g.Question.Type, QuestionType.Write, "write question");
		Check(!g.Answer("ann", 10, AnswerValue.Written("   ")).Ok, "empty answer rejected");
		before = (room.Scores["ann"], room.Scores["bob"], room.Scores["cat"]);
		g.Answer("ann", 10, AnswerValue.Written("canbera"));
		g.Answer("bob", 10, AnswerValue.Written("Sydney"));
		g.Answer("cat", 10, AnswerValue.Written("The Canberra!"));
		Eq(room.Scores["ann"] - before.Ann, 1, "small typo counts");
		Eq(room.Scores["cat"] - before.Cat, 1, "case, 'the' and punctuation don't matter");
		Eq(room.Scores["bob"] - before.Bob, 0, "wrong answer doesn't");
		Check(g.ToggleWritten("bob") && room.Scores["bob"] - before.Bob == 1, "host ticks an answer");
		Check(g.ToggleWritten("ann") && room.Scores["ann"] - before.Ann == 0, "host unticks one");
		Check(!QuizRules.WrittenMatches("Sydney", "Canberra / Canberra City") && QuizRules.WrittenMatches("canberra city", "Canberra / Canberra City"), "several accepted answers");
		room.Next();
		Timeline(room, clock, g);
		room.Next();
		Eq(room.Phase, Phase.Finished, "podium after the last question");


		// Host control: a question stays hidden until shown; buzzers stay locked until unlocked.
		var (ctl, ctlClock) = NewRoom(quiz, "ann", "bob");
		ctl.UpdateSettings(hostShows: true, hostUnlocks: true);
		Check(ctl.Start().Ok, "start with host control");
		var cg = ctl.Game!;
		Check(!cg.Shown, "question starts hidden");
		Eq(ctl.ToState("ann").Game!.Question.Prompt, "", "players don't see a hidden question");
		Eq(ctl.ToState("host").Game!.Question.Prompt, "Which planet is known as the Red Planet?", "the host does");
		Check(!cg.BuzzIn("ann"), "no buzzing while hidden");
		Check(cg.ShowQuestion() && cg.Shown, "host shows the question");
		Check(!cg.BuzzersOpen && !cg.BuzzIn("ann"), "buzzers still locked");
		Check(cg.SetBuzzers(true) && cg.BuzzIn("ann"), "host unlocks, ann buzzes");
		cg.Judge(true);
		ctl.Next();
		Eq(cg.Index, 1, "next question");
		Check(!cg.Shown && cg.View("host").EndsAt == null, "a timed question's clock waits while hidden");
		Check(!cg.Answer("ann", 1, AnswerValue.Number(0)).Ok, "no answers while hidden");
		ctlClock.Now += 60_000;
		Check(!ctl.Tick(), "no time-out while hidden");
		cg.ShowQuestion();
		Check(cg.View("host").EndsAt == ctlClock.Now + 20_000, "the timer starts when shown");
		// Joining rules
		var (lobby, _) = NewRoom(quiz, "a1");
		Eq(lobby.Join("a2", "  A1 ").Error, "Someone in this room already uses that name.", "duplicate names");
		Eq(lobby.Join("a3", "   ").Error, "Please enter a name.", "empty name");
		Check(lobby.Join("a1", "whatever").Player?.Name == "a1", "same id gets the seat back");
		Check(!lobby.Start().Ok, "needs two players");
	}

	static void Files()
	{
		// A quiz file as the web version exported it (format 1: classic without mode).
		const string old = """
		{"version":1,"id":"abc","title":"Old quiz","createdAt":1,"updatedAt":2,
		 "maps":[{"id":"m1","name":"Skyrim","image":{"dataUrl":"data:image/webp;base64,AAAA","width":4096,"height":4096},"widthKm":467.1,"presetId":"skyrim"}],
		 "questions":[
		  {"id":"q1","points":1,"type":"classic","prompt":"Capital of France?","image":null,"answer":"Paris"},
		  {"id":"q2","points":2,"type":"estimation","prompt":"How tall?","image":null,"answer":330,"unit":"m","timeLimitSec":30,"tieRule":"allScore","scoring":"top3"},
		  {"id":"q3","points":1,"type":"mapPin","prompt":"Where?","image":{"dataUrl":"data:image/webp;base64,AAAA","width":10,"height":10},"mapId":"m1","target":{"x":0.25,"y":0.75},"buzzFirst":false,"timeLimitSec":60,"tieRule":"nobodyScores","scoring":"closestWins"},
		  {"id":"q4","points":1,"type":"sound","prompt":"Song?","media":{"dataUrl":"data:audio/mpeg;base64,AAAA","mime":"audio/mpeg","name":"a.mp3","durationSec":3.5},"startSec":1.5,"answer":"X"}
		 ]}
		""";
		var quiz = QuizJson.Parse(old, out var err);
		Check(quiz != null && err == null, "old file parses");
		if (quiz == null) return;
		Eq(quiz.Questions[0].Mode, ClassicMode.Buzzer, "migrated classic mode");
		Eq(quiz.Questions[0].AnswerText, "Paris", "classic answer");
		Eq(quiz.Questions[1].AnswerNumber, 330.0, "estimation answer");
		Eq(quiz.Questions[1].Scoring, Scoring.Top3, "scoring");
		// (saved with the old 4096 px square Skyrim map: moved onto the cropped one)
		Check(Math.Abs(quiz.Questions[2].Target!.Value.X - (0.25 * 4096 - 56) / 3472) < 1e-9 && quiz.Questions[2].Target!.Value.Y == 1 /* below the crop: kept on the edge */, "old Skyrim pin moved onto the cropped map");
		Check(quiz.Maps[0].Image.Width == 3472 && quiz.Maps[0].Image.Height == 2892 && quiz.Maps[0].WidthKm == MapPresets.Get("skyrim")!.WidthKm, "old Skyrim map gets the cropped size and scale");
		var reloaded = QuizJson.Parse(QuizJson.Write(quiz), out _)!;
		Eq(reloaded.Questions[2].Target, quiz.Questions[2].Target, "a moved pin isn't moved twice");
		Eq(quiz.Questions[2].TieRule, TieRule.NobodyScores, "tie rule");
		Eq(quiz.Questions[3].StartSec, 1.5, "start sec");
		Eq(quiz.Questions[3].Media?.DurationSec, 3.5, "duration");
		Eq(quiz.Maps[0].PresetId, "skyrim", "preset id");
		Eq(QuizRules.QuizIssues(quiz).Count, 0, "old quiz is playable: " + string.Join(" ", QuizRules.QuizIssues(quiz)));

		// Built-in maps: every image ships, ids are unique, and all but the WoW atlas have a real width.
		// (decoded like in the game: the maps ship as plain WebP files, so this also catches a map the export left out)
		var broken = MapPresets.All.Where(p => Storage.Images.LoadBuiltinImage(p.ImagePath) == null).Select(p => p.Id).ToList();
		Check(broken.Count == 0, "every built-in map image loads: " + string.Join(", ", broken));
		Eq(MapPresets.All.Select(p => p.Id).Distinct().Count(), MapPresets.All.Count, "built-in map ids are unique");
		Check(MapPresets.All.All(p => p.Id == WowAtlas.PresetId || p.WidthKm > 0), "built-in maps have a scale");
		Eq(MapPresets.Get("botw")!.WidthKm, 12.0, "Hyrule is 12 km wide");
		Eq(MapPresets.Get("fh6")!.WidthKm, 17.214, "FH6 Japan width");

		var again = QuizJson.Parse(QuizJson.Write(quiz), out _)!;
		Eq(QuizJson.Write(again), QuizJson.Write(quiz), "write/read round trip");
		var multi = Dev.SampleQuiz.Build().Questions.First(x => x.IsMulti).Clone();
		Eq(multi.Mode, ClassicMode.Multi, "multi mode is saved");
		Eq(string.Join(",", multi.CorrectChoices), "0,1", "multi correct answers are saved");
		var timelineQ = Dev.SampleQuiz.Build().Questions.First(x => x.IsTimeline).Clone();
		Check(timelineQ.IsTimeline && timelineQ.TimelineStart == 1 && string.Join(",", timelineQ.ItemLabels) == "1993,1998,2007,2022" && timelineQ.KeepGoing, "timeline settings are saved");
		var shuffledTl = QuizRules.ShuffleOrder(timelineQ, new Random(3));
		Eq(shuffledTl.Items[shuffledTl.TimelineStart], "Half-Life", "the start item survives the shuffle");
		Eq(shuffledTl.ItemLabel(shuffledTl.Items.IndexOf("Portal")), "2007", "labels move with their items");
		var image = Dev.SampleQuiz.Build().Questions.First(x => x.Type == QuestionType.Image).Clone();
		Check(image.RevealStyle == RevealStyle.Pixelate && image.RevealSec == 20 && Math.Abs(image.Focus.Y - 0.45) < 1e-9, "image reveal settings are saved");
		// Shuffling moves the right answer around (over many games it can't always stay first).
		var choiceQ = new Question { Type = QuestionType.Classic, Mode = ClassicMode.Choice, Choices = new() { "a", "", "c", "d" }, CorrectChoice = 0 };
		var rng = new Random(1);
		var spots = Enumerable.Range(0, 40).Select(_ => QuizRules.ShuffleChoices(choiceQ, rng)).ToList();
		Check(spots.All(s => s.Choices.Count == 3 && s.Choices[s.CorrectChoice] == "a"), "shuffle drops empty answers, keeps the right one");
		Check(spots.Select(s => s.CorrectChoice).Distinct().Count() == 3, "shuffle puts the right answer in every spot");
		var clone = quiz.Questions[1].Clone();
		Eq(clone.Points, 2, "clone keeps numbers");
		Eq(clone.AnswerNumber, 330.0, "clone keeps answer");

		Check(QuizJson.Parse("[1,2]", out _) == null, "array is not a quiz");
		Check(QuizJson.Parse("{nope", out _) == null, "garbage is not a quiz");

		// Versions and the changelog (What's new, updater).
		Check(AppVersion.Parse("0.5.0") > AppVersion.Parse("0.5.0a")!.Value && AppVersion.Parse("0.5.1a") > AppVersion.Parse("0.5.0")!.Value, "version order");
		Check(AppVersion.Parse("v0.10.0a") > AppVersion.Parse("0.9.3")!.Value, "tags and two-digit numbers");
		Check(AppVersion.Parse("hello") == null, "not a version");
		var notes = Changelog.Parse(Godot.FileAccess.GetFileAsString("res://CHANGELOG.md"));
		var thisVersion = notes.FirstOrDefault(r => r.Version is { } nv && AppVersion.Parse(App.Version) is { } av && nv.CompareTo(av) == 0);
		Check(thisVersion != null && thisVersion.Sections.Any(s => s.Kind is "Added" or "Changed" or "Fixed" or "Removed" && s.Items.Count > 0), "the changelog has this version's notes");
		// Since 0.4.0a: every release after it up to this one, newest first, without 0.4.0a itself.
		var since = Changelog.Between(notes, AppVersion.Parse("0.4.0a"), AppVersion.Parse(App.Version)!.Value);
		Check(since.Count >= 1 && since[0] == thisVersion && since.All(r => r.VersionText != "0.4.0a"), "what's new since 0.4.0a");
	}

	static void Atlas()
	{
		var elwynn = WowAtlas.Get(1429);
		var loc = WowAtlas.ToLocation(elwynn, new NormPoint(0.4, 0.6));
		var world = WowAtlas.LocationToWorld(loc);
		var back = WowAtlas.WorldToLocation(world)!.Value;
		Check(Math.Abs(back.X - loc.X) < 0.01 && Math.Abs(back.Y - loc.Y) < 0.01, "world/zone round trip");
		Eq(WowAtlas.ZoneAt(loc)?.Id, 1429, "zone lookup");
		var a = WowAtlas.LocationToWorld(new WowAtlas.Location(0, -9000, 0));
		var b = WowAtlas.LocationToWorld(new WowAtlas.Location(0, -9100, 0));
		Check(Math.Abs(WowAtlas.DistanceKm(a, b) - 100 * 0.9144 / 1000) < 0.001, "same-continent distance in yards");
		var kalimdor = WowAtlas.LocationToWorld(new WowAtlas.Location(1, 0, 0));
		Check(WowAtlas.DistanceKm(a, kalimdor) > WowAtlas.DistanceKm(a, b) * 100, "wrong continent is far");
		Eq(Ui.WowMapArt.Tiles.Count, 207, "seamless map tiles are in the game");
		Check(Storage.Images.LoadBuiltinImage(WowAtlas.ImagePath(WowAtlas.World)) != null && Storage.Images.LoadBuiltinImage(Ui.WowMapArt.Tiles[0].Path) != null, "WoW world map and a zone tile load");
		var goldshire = WowAtlas.LocationToWorld(new WowAtlas.Location(0, -9465, 64));
		Check(Ui.WowMapArt.Tiles.Any(t => t.World.HasPoint(new Godot.Vector2((float)goldshire.X, (float)goldshire.Y))), "Goldshire lies on a tile");
	}

	static void Protocol()
	{
		var state = new RoomState { Code = "ABCD", Phase = Phase.Playing, Scores = new() { ["x"] = 3 } };
		state.Game = new GameView { Index = 2, Reveal = new RevealView { Target = new NormPoint(0.1, 0.2), Entries = new() { new RevealEntry { PlayerId = "x", Value = AnswerValue.At(new NormPoint(0.3, 0.4)) } } } };
		var msg = Msg.Decode(Msg.Encode(Msg.State, state))!.Value;
		Eq(msg.Type, Msg.State, "message type");
		var back = Msg.Read<RoomState>(msg.Data)!;
		Eq(back.Code, "ABCD", "state code");
		Eq(back.Scores["x"], 3, "state scores");
		Eq(back.Game!.Reveal!.Target!.Value.Y, 0.2, "state target");
		Eq(back.Game.Reveal.Entries[0].Value!.Point!.Value.X, 0.3, "answer point");
		Check(Msg.Decode(new byte[] { 1, 2, 3 }) == null, "garbage message ignored");
		var chunk = MediaChunk.Encode("ab12", 1000, 300, new byte[] { 7, 8, 9 });
		Check(MediaChunk.TryDecode(chunk, out var cid, out var ctotal, out var coffset, out var cdata) && cid == "ab12" && ctotal == 1000 && coffset == 300 && cdata.Span.SequenceEqual(new byte[] { 7, 8, 9 }), "video piece round trip");
		Check(!MediaChunk.TryDecode(MediaChunk.Encode("x", 10, 9, new byte[] { 1, 2 }), out _, out _, out _, out _), "video piece past the end is refused");

		var reader = new FrameReader();
		var frames = FrameReader.Frame(new byte[] { 1, 2, 3 }).Concat(FrameReader.Frame(new byte[] { 4 })).ToArray();
		var got = reader.Feed(frames[..5]);
		Eq(got.Count, 0, "partial frame waits");
		got = reader.Feed(frames[5..]);
		Eq(got.Count, 2, "two frames");
		Eq(got[1][0], (byte)4, "frame content");
		Eq(Limits.NormalizeCode(" ab-c1d9 "), "ABC1", "code normalising");
	}

	/// <summary>A real host and two players over TCP on this PC, through a whole buzzer question.</summary>
	static async Task Network(Node node)
	{
		var quiz = SampleQuiz.Build();
		var lan = new LanHostTransport();
		var port = 47000 + (int)(Time.GetTicksMsec() % 1000);
		if (lan.Listen(port) is { } error)
		{
			Failures.Add("listen: " + error);
			return;
		}
		var host = new HostSession(new Room("NETT", quiz), lan, NetMode.Lan, port);
		host.Room.UpdateSettings(hostShows: false, hostUnlocks: false);
		var ann = ClientSession.Lan("127.0.0.1", port, "Ann", "ann-id");
		var bob = ClientSession.Lan("127.0.0.1", port, "Bob", "bob-id");
		string? ended = null;
		ann.Ended += r => ended = r;
		ann.Start();
		bob.Start();

		async Task<bool> Until(Func<bool> cond, double seconds = 5)
		{
			var start = Time.GetTicksMsec();
			while (!cond())
			{
				if (Time.GetTicksMsec() - start > seconds * 1000) return false;
				host.Poll();
				ann.Poll();
				bob.Poll();
				await node.ToSignal(node.GetTree(), SceneTree.SignalName.ProcessFrame);
			}
			return true;
		}

		Check(await Until(() => host.Room.Players.Count == 2 && ann.State?.Players.Count == 2 && bob.State?.Players.Count == 2), "both players joined");
		Eq(ann.MyId, "lan:ann-id", "player id from client id");
		Check(host.Start().Ok, "network start");
		Check(await Until(() => bob.State?.Phase == Phase.Playing), "players see the game");
		Check(bob.State!.Game!.HostAnswer == null, "no answer leaks to players");

		// The question image comes over in parts.
		var key = bob.State.Game.Question.ImageKey!;
		bob.Assets.Get(key);
		var loaded = false;
		bob.Assets.Loaded += k => loaded |= k == key;
		Check(await Until(() => loaded), "image arrives");
		Check(bob.Assets.Get(key) != null, "image decoded");

		bob.Buzz();
		Check(await Until(() => ann.State?.Game?.Buzzes.Count == 1), "buzz reaches everyone");
		host.Judge(true);
		Check(await Until(() => ann.State?.Game?.Stage == Stage.Reveal), "reveal reaches players");
		Eq(ann.State!.Score("lan:bob-id"), 1, "score sent");

		host.Next();
		Check(await Until(() => ann.State?.Game is { Index: 1, Stage: Stage.Open }), "next question");
		Result? res = null;
		ann.Answer(1, AnswerValue.Number(2), r => res = r);
		Check(await Until(() => res != null) && res!.Ok, "answer accepted");

		host.Close("Test over.");
		Check(await Until(() => ended != null), "players learn the room closed");
		Eq(ended, "Test over.", "close reason");
		bob.Dispose();
	}
}
