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
		Check(Tl().Placed[0] is { Start: true } first && first.Label == years[first.Text].ToString(), "the start item shows its year");
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
	static void Sentence()
	{
		var quiz = new Quiz { Title = "S", Questions = { new Question { Id = "s1", Type = QuestionType.Steps, StepsMode = StepsMode.Sentence, AnswerText = "Berlin",
			Hints = new() { "This city, founded in 1237,", "was split by a wall", "and is Germany's capital.", "", "" } } } };
		var back = QuizJson.Parse(QuizJson.Write(quiz), out _)!.Questions[0];
		// (parsed from text, like a file: in-memory JSON numbers don't convert on read)
		var old = QuizJson.ReadQuestion((System.Text.Json.Nodes.JsonObject)System.Text.Json.Nodes.JsonNode.Parse(
			"{\"id\":\"w\",\"type\":\"write\",\"prompt\":\"Capital of Australia?\",\"answer\":\"Canberra\",\"timeLimitSec\":25}")!);
		Check(old.Type == QuestionType.Classic && old.IsTyped && old.AnswerText == "Canberra" && old.TimeLimitSec == 25,
			"an old \"Write the answer\" question opens as classic, everyone types (answer and time limit kept)");
		Check(!QuestionType.All.Contains(QuestionType.Write), "\"Write the answer\" isn't offered as a type any more");
		Check(back.IsSentence && back.FilledHints.Count == 3, "a sentence in parts survives saving");
		var (room, _) = NewRoom(quiz, "ann", "bob");
		room.Start();
		var g = room.Game!;
		Check(g.View("bob").Question.Hints!.Count == 1 && g.View("bob").Question.StepsMode == StepsMode.Sentence, "players only get the part read so far");
		g.BuzzIn("ann");
		Check(!g.NextHint(), "a buzz stops the reading");
		g.Judge(false);
		Check(g.NextHint() && g.View("bob").Question.WorthNow == 2, "after a wrong answer the host reads on: 3 parts, now worth 2");
		g.BuzzIn("bob");
		g.Judge(true);
		Eq(room.Score("bob"), 2, "a right buzz after the second of three parts gets 2 points");
		Check(Storage.Results.Questions(new[] { Storage.Results.Make(room).Csv }).Any(r => r.Subtype == StepsMode.Sentence && r.Hint == 2), "results: the sentence subtype and the part it was got at");
	}

	static void Categories()
	{
		var quiz = Dev.SampleQuiz.Build();
		quiz.Questions[0].Category = "Geography";
		quiz.Questions[1].Category = "Music";
		quiz.Questions[1].CategorySecret = true;
		var back = QuizJson.Parse(QuizJson.Write(quiz), out _)!;
		Check(back.Questions[0].Category == "Geography" && back.Questions[1].CategorySecret && !back.Questions[0].CategorySecret, "categories survive saving (secret ones stay secret)");
		var (room, _) = NewRoom(quiz, "ann", "bob");
		room.UpdateSettings(hostShows: true);
		room.Start();
		Eq(room.Game!.View("ann").Question.Category, "", "categories stay hidden unless the lobby shows them");
		room.BackToLobby();
		room.UpdateSettings(categories: true);
		room.Start();
		Eq(room.Game!.View("ann").Question.Category, "Geography", "with Show categories, players see it on Get ready");
		room.Next();
		room.Next();
		Eq(room.Game!.View("ann").Question.Category, "", "a secret category is never shown");
		Check(Storage.Results.Parse(Storage.Results.Make(room).Csv).Any(r => r["question"] == "1" && r["category"] == "Geography"), "the results keep the category");
	}

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
		Check(QuizRules.QuestionIssues(new Question { Type = QuestionType.Classic, Typed = true, Prompt = "x", AnswerText = "y", BonusPoints = 1 }, quiz.Maps)
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

	/// <summary>A Fast Pass buzz right after someone else's (the same moment, as far as anyone can tell) answers first.</summary>
	static void FastPassTie()
	{
		var (room, clock) = NewRoom(Dev.SampleQuiz.Build(), "ann", "bob");
		room.UpdateSettings(hostShows: true);
		room.Start();
		var g = room.Game!;
		Check(g.PlayJoker("bob", Joker.FastPass, true).Ok, "bob plays the Fast Pass");
		g.ShowQuestion();
		clock.Now += 2600;
		g.BuzzIn("ann");
		clock.Now += 40;
		g.BuzzIn("bob");
		var buzzes = g.View("host").Buzzes;
		Eq(string.Join(",", buzzes.Select(b => $"{b.PlayerId}:{b.Status}")), $"bob:{BuzzStatus.Answering},ann:{BuzzStatus.Waiting}", "a Fast Pass 40 ms after someone else's buzz answers first, they wait");
		g.Judge(false);
		Eq(g.View("host").Buzzes.First(b => b.Status == BuzzStatus.Answering).PlayerId, "ann", "then it's their turn");
	}

	/// <summary>Jokers, joining during a game, and the pause when someone leaves.</summary>
	static void JokersAndSeats()
	{
		var quiz = Dev.SampleQuiz.Build();
		var (room, clock) = NewRoom(quiz, "ann", "bob", "cat");
		room.UpdateSettings(hostShows: true);
		Check(room.Rename("ann", "  Annie  ").Ok && room.GetPlayer("ann")!.Name == "Annie", "a player changes their name in the waiting room");
		Check(!room.Rename("ann", "BOB").Ok && !room.Rename("ann", " ").Ok, "not to someone else's name, or none");
		Check(room.Rename("ann", "Ann").Ok, "and back");
		room.Start();
		Check(!room.Rename("ann", "Zed").Ok, "no renaming during a game");
		var g = room.Game!;
		Eq(g.JokerIndex, 0, "jokers go on the hidden question");
		Check(g.PlayJoker("ann", Joker.FastPass, true).Ok && g.PlayJoker("bob", Joker.Double, true).Ok, "jokers played");
		Check(g.View("cat").Jokers.Count == 0 && g.View("ann").Jokers.Count == 1, "a joker stays secret until its question is shown");
		Check(g.PlayJoker("ann", Joker.FastPass, false).Ok && g.PlayJoker("ann", Joker.FastPass, true).Ok, "taken back and played again");
		Check(!g.PlayJoker("cat", Joker.FastPass, true).Ok && g.View("cat").FastPassTaken && !g.View("ann").FastPassTaken, "one Fast Pass per question (others see it's taken, not by whom)");
		Check(g.PlayJoker("cat", Joker.Double, true).Ok && g.PlayJoker("cat", Joker.Double, false).Ok, "Double: anyone can play it on the same question");
		g.ShowQuestion();
		Check(!g.PlayJoker("ann", Joker.FastPass, false).Ok && g.JokerIndex == null, "once shown: locked in, no new ones");
		Eq(g.View("cat").Jokers.Count, 2, "everyone sees them now");
		clock.Now += 1000;
		g.BuzzIn("cat");
		clock.Now += 5000;
		g.BuzzIn("bob");
		g.BuzzIn("ann");
		Eq(string.Join(",", g.View("host").Buzzes.Select(b => b.PlayerId)), "ann,cat,bob", "the Fast Pass always answers first, even after someone answered for a while");
		Check(g.View("host").Buzzes[0].FastPass && g.View("host").Buzzes[0].Status == BuzzStatus.Answering, "the buzz shows its Fast Pass");
		g.Judge(true);
		Eq(room.Scores["ann"], 1, "fast pass scores normally");
		Eq(g.JokerIndex, 1, "during the reveal: jokers for the next question");
		Check(!g.PlayJoker("cat", Joker.FastPass, true).Ok, "no Fast Pass for a question that isn't a buzzer one");
		Check(!g.PlayJoker("ann", Joker.FastPass, true).Ok, "each joker once per match");
		Check(g.PlayJoker("cat", Joker.Double, true).Ok, "double for the next question");
		room.Next();
		g.ShowQuestion();
		// Someone joins mid-question: they play from the next one.
		var (dan, _) = room.Join("dan", "dan");
		Check(dan != null && dan.JoinedAt == 2 && g.View("dan").LateIds.Contains("dan"), "joining during a game: they come in at this question");
		Check(!g.Answer("dan", 1, AnswerValue.Number(g.Question.CorrectChoice)).Ok, "but can't answer it");
		foreach (var p in new[] { "ann", "bob", "cat" }) g.Answer(p, 1, AnswerValue.Number(g.Question.CorrectChoice));
		Eq(g.Stage, Stage.Reveal, "the reveal doesn't wait for them");
		Eq(room.Scores["cat"], g.Question.Points * 2, "double points");
		Check(g.View("host").Reveal!.Entries.First(e => e.PlayerId == "cat").Doubled, "the reveal shows the double");
		// Someone leaves: the clock stops until they're back.
		room.Next();
		g.ShowQuestion();
		var ends = g.View("host").EndsAt;
		room.Disconnect("bob");
		clock.Now += 5000;
		room.Join("bob", "bob");
		Check(!g.Paused && room.GetPlayer("bob")!.LeftAt == null, "back: the game goes on");
		Eq(g.View("host").EndsAt, ends + 5000, "the timer lost no time while they were away");
		room.Leave("cat");
		Check(room.GetPlayer("cat") is { Connected: false } && room.Scores["cat"] > 0 && g.Paused, "leaving during a game keeps the seat and score, and waits");
		room.Next();
		Eq(g.Index, 2, "paused: Next waits for the host to continue");
	}

	/// <summary>The ranking's tiebreaks, the players' short scoreboard and the saved results.</summary>
	static void RankingAndResults()
	{
		var (room, clock) = NewRoom(Dev.SampleQuiz.Build(), "ann", "bob", "cat", "dan");
		room.Start();
		Check(room.Ranking().Places.Values.All(p => p == 1), "nobody has done anything: everyone shares first place");
		room.Award("bob", 3);
		clock.Now += 5000;
		room.Award("ann", 3);
		Eq(string.Join(",", room.Ranking().Order.Take(2)), "bob,ann", "same points: whoever got there first is ahead");
		room.Award("cat", 2);
		room.Award("dan", 2);
		room.NoteAnswerTime("cat", 2500);
		room.NoteAnswerTime("dan", 1200);
		var (order, places) = room.Ranking();
		Eq(string.Join(",", order), "bob,ann,dan,cat", "same points at the same moment: the faster average answer time is ahead");
		Eq(places["cat"], 4, "and nobody shares a place then");
		Eq(string.Join(",", Ui.Screens.GameParts.ShownPositions(10, 6)), "0,1,2,5,6,7", "7th of 10 sees the top 3 and 6th–8th");
		Eq(string.Join(",", Ui.Screens.GameParts.ShownPositions(10, 9)), "0,1,2,8,9", "the last one sees who's just ahead");
		Eq(string.Join(",", Ui.Screens.GameParts.ShownPositions(4, 1)), "0,1,2", "small rooms: everyone near the top");

		room.GetPlayer("ann")!.Name = "Ann, \"the\" Great";
		room.NoteDistance("ann", 12.5);
		var (_, csv) = Storage.Results.Make(room);
		var all = Storage.Results.Parse(csv);
		var rows = all.Where(r => r["question"] == "").ToList();
		Check(rows.Count == 4 && rows[1]["player"] == "Ann, \"the\" Great" && rows[1]["avg_km_off"] == "12.500" && rows[0]["place"] == "1", "results CSV: one row per player, in place order, names with commas and quotes survive");
		var table = Storage.Results.Table(new[] { csv, csv.Replace("2026", "2027"), csv });
		Check(table.Count == 4 && table.First(c => c.Id == "bob") is { Nights: 2, Wins: 2, Podiums: 2, Points: 6 } && table.Last().Points == 4, "eternal table: nights added up per player (the same night twice counts once), most points first");
		Eq(table.First(c => c.Id == "bob").ChampionshipPoints(ScoringPresets.BuiltIn[0].Points), 50, "championship points: two F1 wins are 50");
		Eq(Storage.Results.Table(new[] { csv, csv.Replace("2026", "2028") }, from: "2028-01-01").First().Nights, 1, "a season only counts its own nights");

		// Per-question rows: points from the question, the type and subtype, and the host's score changes left out.
		var q1 = all.Where(r => r["question"] == "1").ToList();
		Check(q1.Count == 4 && q1.All(r => r["type"] == room.Quiz.Questions[0].Type) && q1.First(r => r["player_id"] == "bob")["q_points"] == "3",
			"results CSV: one row per player and question, with its type and the points from it");
		room.AdjustScore("cat", 5);
		Check(room.StatsOf("cat").HostPoints == 5 && Storage.Results.FavouritePuppy(Storage.Results.Table(new[] { Storage.Results.Make(room).Csv })) == "cat",
			"score changes by hand count as the host's points: the Host's Favourite Puppy");
		Check(Storage.Results.Questions(new[] { Storage.Results.Make(room).Csv }).First(r => r.PlayerId == "cat").Points == 2, "the host's score changes don't count for a question");

		Check(QuizRules.Typos("canbera", "Canberra / ACT") == 1 && QuizRules.Typos("The Beatles", "beatles") == 0 && QuizRules.Typos("Zürich", "Zurich") == 0,
			"typos: letters off from the closest accepted answer (case, accents and \"the\" don't count)");

		// Answer and buzz times are separate: a quick buzz doesn't make someone look fast at typing.
		var (r2, _) = NewRoom(Dev.SampleQuiz.Build(), "ann", "bob");
		r2.Start();
		r2.NoteBuzzTime("ann", 800);
		r2.NoteAnswerTime("bob", 9000);
		r2.NoteAnswerTime("ann", 12000);
		Check(r2.StatsOf("ann") is { Buzzes: 1, Answers: 1, AvgAnswerMs: 12000 } && r2.Ranking().Order[0] == "bob",
			"buzz time has its own average; ties go by answer time first");
	}

	/// <summary>A host transport without a network: who each connection is (Steam ID), and what the host sent.</summary>
	sealed class FakeHost : IHostTransport
	{
		public readonly Dictionary<string, string?> Steam = new();
		public readonly List<(string Peer, string Type, System.Text.Json.Nodes.JsonNode? Data)> Sent = new();
		public readonly HashSet<string> Dropped = new();
		public event Action<string>? PeerConnected;
		public event Action<string>? PeerDisconnected;
		public event Action<string, byte[]>? Received;
		public string? PeerSteamId(string peer) => Steam.GetValueOrDefault(peer);
		public void Send(string peer, byte[] data)
		{
			if (Msg.Decode(data) is { } m) Sent.Add((peer, m.Type, m.Data));
		}
		public bool TrySend(string peer, byte[] data) => true;
		public void Disconnect(string peer)
		{
			Dropped.Add(peer);
			PeerDisconnected?.Invoke(peer);
		}
		public void Poll() { }
		public int MaxMessageBytes => 300 * 1024;
		public void Dispose() { }
		public void Deliver(string peer, string type, object payload) => Received?.Invoke(peer, Msg.Encode(type, payload));
		public JoinReply? Reply(string peer) => Sent.LastOrDefault(x => x.Peer == peer && x.Type == Msg.Joined).Data is { } d ? Msg.Read<JoinReply>(d) : null;
		void Unused() => PeerConnected?.Invoke("");
	}

	/// <summary>What comes from outside: joins over Steam, pictures, packed quizzes, recordings, CSV cells, frames.</summary>
	static void Safety()
	{
		var room = new Room("ABCD", Dev.SampleQuiz.Build());
		var net = new FakeHost();
		var host = new HostSession(room, net, NetMode.Steam) { IsFriend = id => id == 111 };
		net.Steam["p1"] = "111";
		net.Steam["p2"] = "222";
		net.Steam["p3"] = null;
		net.Deliver("p1", Msg.Join, new JoinRequest { Name = "Ann", Code = "ZZZZ" });
		Check(net.Reply("p1")?.Ok == false && net.Dropped.Contains("p1"), "Steam: no seat without the right room code");
		net.Deliver("p3", Msg.Join, new JoinRequest { Name = "Cat", ClientId = "steam-111", Code = "abcd" });
		Check(net.Reply("p3")?.Ok == false, "Steam: a game that only claims a Steam ID gets no seat");
		net.Deliver("p1", Msg.Join, new JoinRequest { Name = "Ann", Code = "abcd" });
		Check(net.Reply("p1")?.Ok == true && room.GetPlayer("steam:111") != null, "Steam: the right code (any case) gets in, as the Steam account Steam names");
		host.UpdateSettings(friendsOnly: true);
		net.Deliver("p2", Msg.Join, new JoinRequest { Name = "Bob", Code = "ABCD" });
		Check(net.Reply("p2")?.Ok == false, "friends only: someone who isn't the host's Steam friend stays out");
		host.UpdateSettings(friendsOnly: false);
		net.Deliver("p2", Msg.Join, new JoinRequest { Name = "Bob", Code = "ABCD" });
		Check(net.Reply("p2")?.Ok == true, "and gets in once it's off");
		host.Kick("steam:222");
		net.Deliver("p4", Msg.Join, new JoinRequest { Name = "Bob again", Code = "ABCD" });
		net.Steam["p4"] = "222";
		net.Deliver("p4", Msg.Join, new JoinRequest { Name = "Bob again", Code = "ABCD" });
		Check(net.Reply("p4")?.Ok == false && room.GetPlayer("steam:222") == null, "a kicked player can't come back into the room");
		Check(SteamService.CodeKey("ABCD") == SteamService.CodeKey("ABCD") && SteamService.CodeKey("ABCD") != SteamService.CodeKey("ABCE")
			&& !SteamService.CodeKey("ABCD").Contains("ABCD"), "the lobby list only has a hash of the room code");

		// pictures: a header claiming 60 000 × 60 000 pixels is never decoded
		byte[] Png(int w, int h)
		{
			var b = new byte[33];
			new byte[] { 0x89, (byte)'P', (byte)'N', (byte)'G', 13, 10, 26, 10, 0, 0, 0, 13, (byte)'I', (byte)'H', (byte)'D', (byte)'R' }.CopyTo(b, 0);
			b[16] = (byte)(w >> 24); b[17] = (byte)(w >> 16); b[18] = (byte)(w >> 8); b[19] = (byte)w;
			b[20] = (byte)(h >> 24); b[21] = (byte)(h >> 16); b[22] = (byte)(h >> 8); b[23] = (byte)h;
			return b;
		}
		Check(Guard.ImageSize(Png(640, 480)) == (640, 480) && Guard.ImageOk(Png(2048, 2048)), "picture size read from its header");
		Check(!Guard.ImageOk(Png(60000, 60000)) && !Guard.ImageOk(Png(8000, 8000)) && Storage.Images.Decode(Png(60000, 60000), "image/png") == null,
			"pictures claiming to be enormous aren't decoded");
		Check(!Guard.ImageOk(new byte[] { 1, 2, 3, 4, 5 }), "unknown kinds of picture aren't decoded");
		var webp = Storage.Images.Encode(Image.CreateEmpty(300, 200, false, Image.Format.Rgba8), 2048);
		Check(Guard.ImageSize(Server.DataUrl.Bytes(webp.DataUrl, out _)!) == (300, 200), "a WebP picture's size from its header");

		// a share code that unpacks to more than a quiz can be
		var bomb = new System.IO.MemoryStream();
		using (var br = new System.IO.Compression.BrotliStream(bomb, System.IO.Compression.CompressionLevel.SmallestSize, true))
			for (var i = 0; i < 80; i++) br.Write(new byte[1024 * 1024]);
		Check(ShareCode.Unpack(bomb.ToArray(), out var bombError) == null && bombError != null && bomb.Length < 200_000,
			"a share code that unpacks to gigabytes is turned away (not unpacked)");

		// recordings: only plain 16 kHz mono sound
		byte[] Wav(int rate, short channels, int dataBytes)
		{
			using var ms = new System.IO.MemoryStream();
			using var w = new System.IO.BinaryWriter(ms);
			w.Write("RIFF"u8); w.Write(36 + dataBytes); w.Write("WAVE"u8); w.Write("fmt "u8); w.Write(16); w.Write((short)1); w.Write(channels);
			w.Write(rate); w.Write(rate * 2 * channels); w.Write((short)(2 * channels)); w.Write((short)16); w.Write("data"u8); w.Write(dataBytes);
			w.Write(new byte[dataBytes]);
			return ms.ToArray();
		}
		Check(Guard.IsPlainWav(Wav(16000, 1, 32000)) && !Guard.IsPlainWav(Wav(44100, 2, 32000)), "recordings: the game's own format only");
		var lying = Wav(16000, 1, 32000);
		BitConverter.TryWriteBytes(lying.AsSpan(40), 999_999);
		Check(!Guard.IsPlainWav(lying), "recordings: a header that lies about its size is turned away");

		// CSV: names that would run as formulas in a spreadsheet
		Check(Guard.CsvSafe("=HYPERLINK(\"x\")") == "'=HYPERLINK(\"x\")" && Guard.CsvValue(Guard.CsvSafe("=1+1")) == "=1+1"
			&& Guard.CsvSafe("Max") == "Max" && Guard.CsvValue("'Tis") == "'Tis", "CSV cells can't run as formulas, and read back as they were");
		Check(Guard.SafeId("A1B2C3D4E5F6A7B8") && !Guard.SafeId("../x") && !Guard.SafeId("CON:") && !Guard.SafeId(""), "file ids: plain ones only");

		// frames: a player's game can't announce a 200 MB message
		var reader = new FrameReader(1024 * 1024);
		var threw = false;
		try
		{
			reader.Feed(BitConverter.GetBytes(200 * 1024 * 1024));
		}
		catch (InvalidOperationException)
		{
			threw = true;
		}
		Check(threw, "LAN: a message bigger than a player ever sends is refused");
	}

	public static async Task<int> Run(Node node)
	{
		RankingAndResults();
		Categories();
		Sentence();
		Rules();
		TypedAndBonus();
		JokersAndSeats();
		FastPassTie();
		Safety();
		Files();
		// Load, not just Exists: an Opus .ogg exists but Godot can't import it.
		foreach (var b in Storage.Sfx.Buzzers) Check(ResourceLoader.Load<AudioStream>(b.Path) != null, $"buzzer sound {b.Id} plays");
		foreach (var f in new[] { "Correct Answer", "Wrong Answer" })
			Check(ResourceLoader.Load<AudioStream>($"res://assets/soundeffects/{f}.ogg") != null, $"answer sound {f} plays");
		foreach (var p in Storage.Sfx.Podiums) Check(ResourceLoader.Load<AudioStream>(p.Path) != null, $"podium sound {p.Id} plays (must be Ogg Vorbis, MP3 or WAV — not Opus)");
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
		Check(g.Paused && g.Stage == Stage.Open && g.View("ann").WaitingFor.SequenceEqual(new[] { "cat" }), "someone leaves: the game waits for them");
		Eq(room.ToState("ann").Players.First(p => p.Id == "cat").LeftAt, 2, "everyone sees when they left");
		Check(!g.Answer("cat", 1, AnswerValue.Number(0)).Ok && !room.Tick(), "paused: no answers, the clock stands still");
		Check(g.Continue(), "the host goes on without them");
		Eq(g.Stage, Stage.Reveal, "reveals when the last missing player is gone");
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

		// Put in order: shown shuffled; every item in its right place scores.
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
		Eq(room.Scores["ann"] - before.Ann, 8, "perfect order: the points for each of the 4 items");
		Eq(room.Scores["cat"] - before.Cat, 4, "2 of 4 right still score for those 2");
		Eq(room.Scores["bob"] - before.Bob, 0, "none right: nothing (no minus unless it's switched on)");
		Eq(g.View("host").Reveal!.Entries.First(e => e.PlayerId == "cat").Text, "2 of 4 in the right place", "order reveal text");
		Eq(g.View("host").Reveal!.AnswerText, "Doom → Half-Life → Portal → Elden Ring", "order answer");

		// Everyone types: close enough is ticked; the host can tick or untick.
		room.Next();
		Check(g.Question.Type == QuestionType.Classic && g.Question.IsWritten, "everyone types question");
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

		// Picture: a slideshow the host flips through; players only get the pictures shown so far.
		room.Next();
		Eq(g.Question.Type, QuestionType.Picture, "picture question");
		var pic = g.View("ann").Question;
		Check(pic.Slide == 0 && pic.SlideCount == 3 && pic.ImageKey == Game.SlideKey(g.Index, 0), "the slideshow starts on the first picture");
		Check(g.AssetDataUrl(Game.SlideKey(g.Index, 1)) == null && g.AssetDataUrl(Game.SlideKey(g.Index, 1), host: true) != null, "players can't get a picture before the host shows it");
		Check(g.SetSlide(1) && g.View("ann").Question.ImageKey == Game.SlideKey(g.Index, 1) && g.AssetDataUrl(Game.SlideKey(g.Index, 1)) != null, "the host flips; everyone sees the next picture");
		Check(!g.SetSlide(3) && !g.SetSlide(-1) && g.Slide == 1, "no flipping past the ends");
		Check(room.SharedFiles.Count(f => f.Asset is { } a && a.StartsWith($"q{g.Index}/slide")) == 3, "a slideshow's pictures are sent to players ahead of time");
		Check(g.View("ann").Question.Files is { } shownFiles && shownFiles.ContainsKey(Game.SlideKey(g.Index, 1)) && shownFiles.Count == 2,
			"shown pictures come with the keys to their downloaded copies (only the ones shown so far)");
		Check(!g.View("ann").Question.AllSlidesShown && g.SetSlide(2) && g.SetSlide(0) && g.View("ann").Question is { AllSlidesShown: true, Files.Count: 3 }
			&& g.AssetDataUrl(Game.SlideKey(g.Index, 2)) != null, "once all are shown, players may flip through them all themselves");
		g.SetSlide(1);
		Check(g.CurrentFiles().Count() == 3, "the current question's files are its three pictures");
		g.SetBuzzers(true);
		Check(g.BuzzIn("ann") && g.Judge(false) && !g.BuzzIn("ann"), "wrong: no second buzz");
		Check(g.ReopenBuzzers() && g.BuzzIn("ann") && g.View("host").Buzzes.Count == 1, "the host lets everyone buzz again");
		room.Next(); // (open: reveals first)
		Check(g.SetSlide(2), "the host can still flip at the reveal");

		// Step by step: hints flip one by one; the earlier someone is right, the more points.
		room.Next();
		Eq(g.Question.Type, QuestionType.Steps, "step by step question");
		g.SetBuzzers(true);
		var sv = g.View("ann").Question;
		Check(sv.Buzzer && sv.Hints!.Count == 1 && sv.HintCount == 5 && sv.WorthNow == 5, "only the first hint shows, worth 5");
		Check(g.BuzzIn("ann") && g.Judge(false) && !g.BuzzIn("ann"), "wrong: no second buzz on the same hint");
		Check(g.NextHint() && g.View("ann").Question is { Hints.Count: 2, WorthNow: 4 }, "the host flips the next hint: worth one less");
		Check(g.BuzzIn("ann"), "a new hint is a new chance");
		var stepsBefore = room.Scores["ann"];
		g.Judge(true);
		Eq(room.Scores["ann"] - stepsBefore, 4, "right on the second of five hints: 4 points");
		Eq(g.View("bob").Question.Hints!.Count, 5, "the reveal shows every hint");

		// Interactive: everyone records, the host plays them, everyone votes (not for themselves).
		room.Next();
		Eq(g.Question.Type, QuestionType.Interactive, "interactive question");
		// (a second of sound as the game records it: 16 kHz mono)
		var wav = Ui.MicRecorder.Wav(new float[16000], 16000);
		Check(!g.SubmitRecording("ann", g.Index, new byte[100]).Ok, "something that isn't a WAV isn't a recording");
		Check(g.SubmitRecording("ann", g.Index, wav).Ok && g.SubmitRecording("bob", g.Index, wav).Ok, "players send recordings");
		Check(g.View("ann").Interactive is { Phase: InteractivePhase.Recording, Clips.Count: 0 } && g.AssetDataUrl(Game.RecordingKey(g.Index, 0)) == null,
			"nobody hears the recordings while recording");
		Check(g.SubmitRecording("cat", g.Index, wav).Ok && g.View("ann").Interactive is { Phase: InteractivePhase.Listening, Clips.Count: 3 },
			"everyone has sent one: the host plays them");
		Check(g.AssetDataUrl(Game.RecordingKey(g.Index, 2)) is { } rec && rec.StartsWith("data:audio/wav"), "players can fetch the recordings now");
		Check(g.PlayClip() && g.View("cat").Interactive!.Plays == 1 && g.SetClip(1) && !g.SetClip(3), "the host plays one and flips to the next");
		Check(!g.Answer("ann", g.Index, AnswerValue.Number(1)).Ok && g.StartVoting(), "no votes before voting starts");
		Check(!g.Answer("ann", g.Index, AnswerValue.Number(0)).Ok, "nobody votes for their own recording");
		var votesBefore = (Ann: room.Scores["ann"], Bob: room.Scores["bob"]);
		g.Answer("ann", g.Index, AnswerValue.Number(1));
		g.Answer("bob", g.Index, AnswerValue.Number(0));
		g.Answer("cat", g.Index, AnswerValue.Number(0));
		Eq(g.Stage, Stage.Reveal, "everyone voted: reveal");
		Check(room.Scores["ann"] - votesBefore.Ann == 2 && room.Scores["bob"] - votesBefore.Bob == 1, "a point per vote");
		Check(g.View("host").Reveal!.WinnerIds.SequenceEqual(new[] { "ann" }), "most votes wins");
		Check(Game.WavSeconds(wav) is > 0.9 and < 1.1, "a recording's length comes from its WAV header");

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
		var code = ShareCode.Make(quiz);
		var shared = ShareCode.Read("  " + code[..40] + "\n" + code[40..] + " ", out var shareError);
		Check(shared != null, "a share code reads back (also wrapped over lines): " + shareError);
		var noMedia = QuizJson.Parse(QuizJson.Write(quiz), out _)!;
		foreach (var q in noMedia.Questions.Where(q => q.IsMedia)) q.Media!.DataUrl = "";
		foreach (var m in noMedia.Maps.Where(m => MapPresets.Get(m.PresetId) != null)) m.Image.DataUrl = "";
		Eq(QuizJson.Write(shared!), QuizJson.Write(noMedia), "a share code is the quiz without its sound, video and built-in map pictures");
		Check(ShareCode.Read(code[..^30], out _) == null && ShareCode.Read("hello", out _) == null, "cut-off and non-codes are refused");
		// Export: a zip with the sound as its own file, and back.
		var withSound = QuizJson.Parse(QuizJson.Write(quiz), out _)!;
		var soundQ = withSound.Questions.First(q => q.IsMedia);
		soundQ.Media = new MediaAsset { DataUrl = Server.DataUrl.Make("audio/mpeg", Enumerable.Range(0, 5000).Select(i => (byte)(i * 7)).ToArray()), Mime = "audio/mpeg", Name = "song.mp3" };
		var zipPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"quiznight-selftest-{Guid.NewGuid():N}.quiznight.zip");
		Storage.QuizStore.Export(withSound, zipPath);
		using (var z = System.IO.Compression.ZipFile.OpenRead(zipPath))
			Check(z.GetEntry($"media/{soundQ.Id}.mp3") is { Length: 5000 } && z.GetEntry("quiz.json") != null, "export zip: quiz.json and the sound as a file of its own");
		var fromZip = Storage.QuizStore.ReadZip(zipPath);
		System.IO.File.Delete(zipPath);
		Eq(QuizJson.Write(fromZip), QuizJson.Write(withSound), "export zip reads back the same quiz, sound included");
		var shortCode = ShareCode.NewShort();
		Check(shortCode.Length == 12 && ShareCode.ShortKey(shortCode) is { Length: 8 }, "short codes look like QN-XXXX-XXXX");
		Check(ShareCode.ShortKey(" qn-7k4p-9xz2 ") == "7K4P9XZ2" && ShareCode.ShortKey("7K4P 9XZ2") == "7K4P9XZ2", "short codes read in any case, with or without QN- and dashes");
		Check(ShareCode.ShortKey("QN-7K4P-9XZ") == null && ShareCode.ShortKey("QN-0K4P-9XZ2") == null && ShareCode.ShortKey(code) == null, "not a short code: too short, a 0, or a long code");
		Check(ShareCode.Looks("QN-7K4P-9XZ2") && ShareCode.Looks(code) && !ShareCode.LooksLong("QN-7K4P-9XZ2"), "both kinds are recognised");
		Check(shared!.Questions.Where(q => q.IsMedia).All(q => q.Media is { DataUrl: "", Name.Length: > 0 }), "shared media keep their file names");
		var multi = Dev.SampleQuiz.Build().Questions.First(x => x.IsMulti).Clone();
		Eq(multi.Mode, ClassicMode.Multi, "multi mode is saved");
		Eq(string.Join(",", multi.CorrectChoices), "0,1", "multi correct answers are saved");
		var timelineQ = Dev.SampleQuiz.Build().Questions.First(x => x.IsTimeline).Clone();
		Check(timelineQ.IsTimeline && string.Join(",", timelineQ.ItemLabels) == "1993,1998,2007,2022" && timelineQ.KeepGoing, "timeline settings are saved");
		var shuffledTl = QuizRules.ShuffleOrder(timelineQ, new Random(3));
		var starts = Enumerable.Range(0, 40).Select(seed => QuizRules.ShuffleOrder(timelineQ, new Random(seed))).Select(s => s.Items[s.TimelineStart]).Distinct().Count();
		Eq(starts, 4, "the timeline starts with a random item");
		Eq(shuffledTl.ItemLabel(shuffledTl.Items.IndexOf("Portal")), "2007", "labels move with their items");
		Eq(Room.CleanName("  [OTB]   maxlvl0123456789 "), "[OTB] maxlvl012345", "a clan tag doesn't count toward the name's length");
		Eq(Room.CleanName("[TOOLONGTAG] Ann"), "[TOOLON] Ann", "a clan tag is cut to its own length");
		Check(Limits.SplitClanTag("[OTB] maxlvl") == ("OTB", "maxlvl") && Limits.SplitClanTag("Ann [x]") == (null, "Ann [x]"), "clan tags are found at the front only");
		Eq(Ui.U.Initials("[OTB] maxlvl"), "MA", "initials skip the clan tag");

		// Scoring by place (F1 and friends), exact numbers, units.
		var f1 = ScoringPresets.BuiltIn.First(p => p.Name.StartsWith("F1 (since"));
		var byPlace = Game.ClosenessPoints(new List<(string, double)> { ("a", 1), ("b", 2), ("c", 2), ("d", 9) }, 1, Scoring.ClosestWins, TieRule.AllScore, f1.Points);
		Check(byPlace["a"].Points == 25 && byPlace["b"].Points == 18 && byPlace["c"].Points == 18 && byPlace["d"].Points == 12,
			"points per place: 25, tied 2nd both 18, then 4th 12");
		Check(Game.ClosenessPoints(new List<(string, double)> { ("a", 1), ("b", 2) }, 1, Scoring.ClosestWins, TieRule.AllScore, new[] { 5 })["b"].Points == 0,
			"places after the last one get nothing");
		var ranked = QuizRules.NewQuestion(QuestionType.Estimation, "r");
		ranked.RankPoints = f1.Points.ToList();
		Check(QuizJson.ReadQuestion(QuizJson.WriteQuestion(ranked)).RankPoints.SequenceEqual(f1.Points) && ranked.MaxPoints == 25, "points per place are saved with the question");
		Check(QuizJson.ReadQuestion(QuizJson.WriteQuestion(QuizRules.NewQuestion(QuestionType.Estimation, "c"))).RankPoints.Count == 0, "no points per place: the classic way");
		var fasterTie = Game.ClosenessPoints(new List<(string, double)> { ("late", 5), ("early", 5) }, 1, Scoring.Top3, TieRule.Faster);
		Check(fasterTie["late"].Points == 3 && fasterTie["early"].Points == 2, "faster answer wins a tie (the one who locked in first is ahead)");
		Eq(QuizRules.NewQuestion(QuestionType.MapPin, "m").TieRule, TieRule.Faster, "new map pins break ties by who was faster");
		var costly = QuizRules.NewQuestion(QuestionType.Classic, "w");
		costly.Mode = ClassicMode.Choice;
		costly.WrongCosts = true;
		Check(QuizJson.ReadQuestion(System.Text.Json.Nodes.JsonNode.Parse(QuizJson.WriteQuestion(costly).ToJsonString())!.AsObject()).WrongCosts, "'wrong answers cost points' is saved");
		Check(Storage.Results.Columns.Contains("avg_pct_off"), "results record the estimation % off");
		var nightCsv = "date,quiz,place,player_id,player,points,host,host_id\n2026-11-01 20:00,Quiz A,2,steam:2,Bob,3,Max,steam:9\n2026-11-01 20:00,Quiz A,1,steam:1,Ann,5,Max,steam:9\n2026-10-01 20:00,Quiz B,1,steam:1,Ann,4,,\n";
		var nights = Storage.Results.Nights(new[] { nightCsv, nightCsv });
		Check(nights.Count == 2 && nights[0].Quiz == "Quiz A" && nights[0].Host == "Max" && nights[0].Players.Count == 2 && nights[0].Players[0].Name == "Ann" && nights[1].Host == "",
			"match history: nights newest first, with the host and places (the same night twice counts once)");
		var started = Storage.Results.Nights(new[] { "date,quiz,place,player_id,player,points,started\n2026-11-02 22:41,Quiz C,1,steam:1,Ann,5,2026-11-02 20:03\n" })[0];
		Check(Ui.Screens.MatchHistoryScreen.When(started) == "02.11.2026 20:03" && Ui.Screens.MatchHistoryScreen.When(nights[1]) == "01.10.2026",
			"the match history shows when a night started (older nights without it: just the day)");
		Check(Storage.Results.Columns.Contains("started"), "results record when the quiz started");
		Eq(Game.Fmt(0.00000034), "0.00000034", "small numbers keep every digit");
		Eq(Game.Fmt(0.1 + 0.2), "0.3", "no float noise");
		Eq(Ui.NumberField.Parse("2,5"), 2.5, "a comma reads as the decimal point");
		Check(Units.Find("km") == "km" && Units.Find("goals") == null && Units.Find("") == null, "units in the list are found, others are custom");
		var pictureQ = Dev.SampleQuiz.Build().Questions.First(x => x.Type == QuestionType.Picture).Clone();
		Check(pictureQ.Slides.Count == 3 && pictureQ.Slides.All(i => i.DataUrl.StartsWith("data:image")) && pictureQ.IsBuzzer && pictureQ.IsClassicLike, "picture questions keep their pictures and play like classic");
		Check(QuizRules.QuestionIssues(new Question { Type = QuestionType.Picture, Prompt = "x", AnswerText = "y" }, new List<MapAsset>()).Contains(Lang.T("Add at least one picture.")), "a picture question needs a picture");
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
		Check(AppVersion.Parse("0.10.0b") > AppVersion.Parse("0.9.6b")!.Value, "0.10.0b is an update for 0.9.6b");
		Check(Ui.UiAudio.IsChristmas(new DateTime(2026, 12, 24)) && !Ui.UiAudio.IsChristmas(new DateTime(2026, 12, 27)) && !Ui.UiAudio.IsChristmas(new DateTime(2026, 11, 30)),
			"Christmas songs: 1 to 26 December");
		Check(Storage.CrashReports.HasError("ok\nERROR: System.NullReferenceException: Object reference not set\n   at X")
			&& !Storage.CrashReports.HasError("ERROR: Couldn't load a font\nWARNING: x"), "crash reports: C# errors found in a log, plain Godot errors not");
		Check(Storage.CrashReports.Anonymise(@"C:\Users\Max\AppData\Roaming\Quiznight", "max") == @"C:\Users\<user>\AppData\Roaming\Quiznight",
			"crash reports: the user name is left out of paths");
		Check(Ui.UiAudio.Describe("res://assets/music/ES_Easy Breezy - Megan Wofford.ogg") == ("Easy Breezy", "Megan Wofford"), "a song's title and artist come from its Epidemic Sound name");
		Check(Ui.Skins.Valid("pinke-geilheit") == Ui.Skins.Default && Ui.Skins.All.First(s => s.Id == Ui.Skins.Default).Label == "Quiznight Purple"
			&& Ui.Skins.Valid(Ui.Skins.Pink) == Ui.Skins.Pink, "skins: the old Pinke Geilheit id is Quiznight Purple now, the pastel Pinke Geilheit is its own");
		Check(Ui.Seasons.IsHalloween(new DateTime(2026, 10, 31)) && !Ui.Seasons.IsHalloween(new DateTime(2026, 11, 1))
			&& Ui.Seasons.IsChristmas(new DateTime(2026, 12, 1)) && !Ui.Seasons.IsChristmas(new DateTime(2027, 1, 2)),
			"seasonal skins: Halloween all October, Christmas 1 to 26 December");
		Check(Ui.Seasons.IsNewYear(new DateTime(2026, 12, 27)) && Ui.Seasons.IsNewYear(new DateTime(2027, 1, 7)) && !Ui.Seasons.IsNewYear(new DateTime(2026, 12, 26))
			&& !Ui.Seasons.IsNewYear(new DateTime(2027, 1, 8)), "New Year skin: 27 December to 7 January");
		Check(Ui.Seasons.Easter(2026) == new DateTime(2026, 4, 5) && Ui.Seasons.Easter(2027) == new DateTime(2027, 3, 28),
			"Easter Sunday 2026 and 2027");
		Check(Ui.Seasons.IsCarnival(new DateTime(2026, 2, 12)) && Ui.Seasons.IsCarnival(new DateTime(2026, 2, 16)) && Ui.Seasons.IsCarnival(new DateTime(2026, 2, 17))
			&& !Ui.Seasons.IsCarnival(new DateTime(2026, 2, 11)) && !Ui.Seasons.IsCarnival(new DateTime(2026, 2, 18))
			&& Ui.Seasons.IsCarnival(new DateTime(2026, 11, 11)) && !Ui.Seasons.IsCarnival(new DateTime(2026, 11, 12)) && Ui.Seasons.IsCarnival(new DateTime(2027, 2, 4)),
			"carnival: Weiberfastnacht to Veilchendienstag (not Ash Wednesday), and 11 November");
		Check(Ui.Seasons.IsValentine(new DateTime(2027, 2, 14)) && !Ui.Seasons.IsValentine(new DateTime(2027, 3, 1)), "Valentine's Day skin: all of February");
		Check(Ui.Skins.Valid(Ui.Skins.Effzeh) == Ui.Skins.Effzeh && Ui.Skins.Valid("nonsense") == Ui.Skins.Default,
			"skins: the new ones are valid, unknown ones fall back to Quiznight Purple");
		var seasonNow = Ui.Seasons.IsCarnival(Ui.Seasons.Today) ? Ui.Skins.Carnival : Ui.Seasons.IsHalloween(Ui.Seasons.Today) ? Ui.Skins.Halloween : Ui.Seasons.IsChristmas(Ui.Seasons.Today) ? Ui.Skins.Christmas
			: Ui.Seasons.IsNewYear(Ui.Seasons.Today) ? Ui.Skins.NewYear : Ui.Seasons.IsValentine(Ui.Seasons.Today) ? Ui.Skins.Valentine : null;
		Check(Ui.Skins.Effective(Ui.Skins.F1, false) == Ui.Skins.F1 && Ui.Skins.Effective(Ui.Skins.F1, true) == (seasonNow ?? Ui.Skins.F1),
			"skins: the season's skin comes on by itself (unless seasonal skins are off), else the chosen one shows");
		Check(Ui.Skins.Available.Any(a => Ui.Skins.IsSeasonal(a.Id)) == App.LanUnlocked, "skins: the seasonal ones can only be picked in developer mode");
		Check(Storage.Sfx.Podiums.First(p => p.Id == "f1-podium").Db == 10, "the F1 podium sound plays 10 dB louder");
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
