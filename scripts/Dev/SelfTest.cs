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

	public static async Task<int> Run(Node node)
	{
		Rules();
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
		g.Answer("ann", 3, AnswerValue.At(new NormPoint(0.49, 0.52)));
		g.Answer("bob", 3, AnswerValue.At(new NormPoint(0.5, 0.52)));
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

		// Image reveal: the picture stays on the host's screen until the reveal; buzz times use its clock.
		room.Next();
		Eq(g.Question.Type, QuestionType.Image, "image reveal question");
		Check(g.View("ann").Question.ImageKey == null, "players don't get the picture while it's open");
		Check(g.AssetDataUrl(Game.ImageKey(8)) == null, "nor can they fetch it");
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
		Eq(quiz.Questions[2].Target?.X, 0.25, "target");
		Eq(quiz.Questions[2].TieRule, TieRule.NobodyScores, "tie rule");
		Eq(quiz.Questions[3].StartSec, 1.5, "start sec");
		Eq(quiz.Questions[3].Media?.DurationSec, 3.5, "duration");
		Eq(quiz.Maps[0].PresetId, "skyrim", "preset id");
		Eq(QuizRules.QuizIssues(quiz).Count, 0, "old quiz is playable: " + string.Join(" ", QuizRules.QuizIssues(quiz)));

		var again = QuizJson.Parse(QuizJson.Write(quiz), out _)!;
		Eq(QuizJson.Write(again), QuizJson.Write(quiz), "write/read round trip");
		var multi = Dev.SampleQuiz.Build().Questions.First(x => x.IsMulti).Clone();
		Eq(multi.Mode, ClassicMode.Multi, "multi mode is saved");
		Eq(string.Join(",", multi.CorrectChoices), "0,1", "multi correct answers are saved");
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
		Check(thisVersion != null && thisVersion.Sections.Any(s => s.Kind == "Added" && s.Items.Count > 3), "the changelog has this version's notes");
		Eq(Changelog.Between(notes, AppVersion.Parse("0.4.0a"), AppVersion.Parse(App.Version)!.Value).Count, 1, "what's new since 0.4.0a");
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
