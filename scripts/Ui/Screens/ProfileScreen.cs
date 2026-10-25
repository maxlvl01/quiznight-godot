// A player's profile page, in the style of a Football Manager player profile: big picture and name with a role line,
// the key numbers (nights, wins, podiums, "caps" as host), the form of the last nights, attributes rated 1–20 against
// everyone else, a radar and a pitch of their best question types, strengths and weaknesses, records, their rival and
// their career night by night. It counts the table picked in the Ewige Tabelle (all time, a season, or the beta).

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Godot;
using QuizNight.Shared;
using QuizNight.Storage;

namespace QuizNight.Ui.Screens;

public partial class ProfileScreen : Control
{
	static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

	/// <summary>An attribute: its name, 1–20 (null: not enough to rate yet) and what it's based on.</summary>
	public sealed record Attribute(string Name, int? Rating, string Tip);

	/// <summary>Each question type's attribute name (how good someone is at it).</summary>
	public static readonly Dictionary<string, string> TypeAttribute = new()
	{
		[QuestionType.MapPin] = "Map sense", [QuestionType.Estimation] = "Estimating", [QuestionType.Classic] = "Knowledge",
		[QuestionType.Picture] = "Picture eye", [QuestionType.Sound] = "Ear", [QuestionType.Video] = "Film memory",
		[QuestionType.Image] = "Sharp eye", [QuestionType.Order] = "Sense of order", [QuestionType.Write] = "Writing",
		[QuestionType.Steps] = "Deduction", [QuestionType.Interactive] = "Performance",
	};

	/// <summary>The role line under the name, from someone's best type ("Map specialist").</summary>
	public static readonly Dictionary<string, string> TypeRole = new()
	{
		[QuestionType.MapPin] = "Map specialist", [QuestionType.Estimation] = "Estimation sniper", [QuestionType.Classic] = "Know-it-all",
		[QuestionType.Picture] = "Eagle eye", [QuestionType.Sound] = "Golden ear", [QuestionType.Video] = "Film buff",
		[QuestionType.Image] = "Pixel hunter", [QuestionType.Order] = "Timeline keeper", [QuestionType.Write] = "Wordsmith",
		[QuestionType.Steps] = "Detective", [QuestionType.Interactive] = "Entertainer",
	};

	/// <summary>
	/// A rating from 1 to 20 for someone's value against everyone else's (the best is 20, the worst 1, the middle about
	/// 10; equal values share). Null: no value. Alone: 10.
	/// </summary>
	public static int? Rate(IReadOnlyDictionary<string, double?> all, string id, bool higherIsBetter)
	{
		if (all.GetValueOrDefault(id) is not { } mine) return null;
		var others = all.Where(x => x.Key != id && x.Value != null).Select(x => x.Value!.Value).ToList();
		if (others.Count == 0) return 10;
		var worse = others.Count(v => higherIsBetter ? v < mine : v > mine) + 0.5 * others.Count(v => Math.Abs(v - mine) < 1e-9);
		return (int)Math.Round(1 + 19 * worse / others.Count);
	}

	/// <summary>FM's colours for 1–20: low red, then orange, green, and the best ones bright.</summary>
	public static Color RatingColor(int? r) => r switch
	{
		null => C.Muted,
		>= 16 => C.Teal,
		>= 11 => C.Lime,
		>= 6 => new Color("#ffb347"),
		_ => C.Coral,
	};

	/// <summary>Fewer questions of a kind than this: no rating (one lucky guess shouldn't make a 20).</summary>
	const int MinQuestions = 3;

	public ProfileScreen() : this("") { }

	public ProfileScreen(string playerId)
	{
		SetAnchorsPreset(LayoutPreset.FullRect);
		var (_, csvs, from, until, tableLabel) = EternalTableScreen.Current();
		bool InRange(string d) => (from == null || string.CompareOrdinal(d, from) >= 0) && (until == null || string.CompareOrdinal(d, until) < 0);
		var nights = Results.Nights(csvs).Where(n => InRange(n.Date)).OrderBy(n => n.Date).ToList();
		var careers = Results.Table(csvs, until, from);
		var rows = Results.Questions(csvs, from, until);
		var me = careers.FirstOrDefault(c => c.Id == playerId);
		var back = U.Link("← Ewige Tabelle", App.I.GoEternalTable).With(b => b.SizeFlagsHorizontal = SizeFlags.ShrinkBegin);
		if (me == null)
		{
			AddChild(U.Page(U.V(14, back, U.Card(8, U.Heading("No quiz nights here", 24), U.Muted(Lang.F("This player has no quiz nights in {0}.", tableLabel), 15))), 1180, 20));
			return;
		}
		var mine = nights.Where(n => n.Players.Any(p => p.Id == playerId)).Select(n => (Night: n, Me: n.Players.First(p => p.Id == playerId))).ToList();
		var myRows = rows.Where(r => r.PlayerId == playerId).ToList();
		var byPlayer = rows.GroupBy(r => r.PlayerId).ToDictionary(g => g.Key, g => g.ToList());

		var attributes = TypeAttributes(byPlayer, playerId);
		var other = OtherAttributes(byPlayer, careers, nights, playerId);
		var page = U.V(16, back,
			Header(me, mine, careers.IndexOf(me) + 1, nights.Count(n => n.HostId == playerId), attributes, other, tableLabel,
				Results.FavouritePuppy(careers) == playerId),
			U.H(16,
				Section("Attributes", AttributeList(other, attributes)).MinSize(330),
				Section("Best positions", U.V(14, new ProfileRadar(attributes) { CustomMinimumSize = new Vector2(380, 300) },
					new ProfilePitch(attributes) { CustomMinimumSize = new Vector2(300, 300) })).Grow(),
				Section("Scouting report", Report(byPlayer, rows, playerId, mine, nights, myRows)).MinSize(360)),
			Section("Points per night", new ProfileBars(mine.Select(m => (m.Me.Points, m.Me.Place, m.Night.Quiz, EternalTableScreen.Date(m.Night.Date))).ToList())
				{ CustomMinimumSize = new Vector2(0, 170), TooltipText = " " }),
			Section("Career", Career(mine)));
		if (myRows.Count == 0)
			page.AddChild(U.Muted("Attributes, positions and the scouting report come from the per-question results, recorded from 0.10.0b on: they fill up with the next quiz nights.", 13));
		AddChild(U.Page(page, 1180, 20));
	}

	static Control Section(string title, Control body) => U.Card(12, U.Eyebrow(title), body);

	/// <summary>Per question type: how often they get it right, rated against everyone with enough of that type.</summary>
	static List<(string Type, Attribute A)> TypeAttributes(Dictionary<string, List<Results.QuestionRow>> byPlayer, string id) =>
		QuestionType.All.Select(type =>
		{
			var rates = byPlayer.ToDictionary(p => p.Key, p =>
			{
				var of = p.Value.Where(r => r.Type == type).ToList();
				return of.Count >= MinQuestions ? (double?)of.Count(r => r.Right) / of.Count : null;
			});
			var n = byPlayer.GetValueOrDefault(id)?.Count(r => r.Type == type) ?? 0;
			var rate = rates.GetValueOrDefault(id);
			var tip = rate is { } v ? Lang.F("{0}: {1} % right of {2} questions", Lang.T(QuestionKinds.TypeLabel(type)), Math.Round(v * 100), n)
				: Lang.F("{0}: fewer than {1} questions so far", Lang.T(QuestionKinds.TypeLabel(type)), MinQuestions);
			return (type, new Attribute(TypeAttribute[type], Rate(rates, id, true), tip));
		}).ToList();

	/// <summary>Speed, nerves and the mental side: buzz and answer times, wrong buzzes, consistency, wins, jokers.</summary>
	static List<Attribute> OtherAttributes(Dictionary<string, List<Results.QuestionRow>> byPlayer, List<Results.Career> careers,
		List<Results.Night> nights, string id)
	{
		Dictionary<string, double?> From(Func<List<Results.QuestionRow>, double?> f) => byPlayer.ToDictionary(p => p.Key, p => f(p.Value));
		var buzz = From(r => r.Count(x => x.BuzzS != null) >= MinQuestions ? r.Where(x => x.BuzzS != null).Average(x => x.BuzzS) : null);
		var answer = From(r => r.Count(x => x.AnswerS != null) >= MinQuestions ? r.Where(x => x.AnswerS != null).Average(x => x.AnswerS) : null);
		var composure = From(r => r.Sum(x => x.Buzzes) is var b and >= MinQuestions ? (double)r.Sum(x => x.WrongBuzzes) / b : null);
		// (spelling: typos in typed answers that counted as right, so a wrong answer isn't a spelling mistake)
		var spelling = From(r => r.Where(x => x.Right && x.Typos != null).ToList() is { Count: >= MinQuestions } t ? t.Average(x => (double)x.Typos!.Value) : null);
		var mySpelled = byPlayer.GetValueOrDefault(id)?.Where(x => x.Right && x.Typos != null).ToList() ?? new();
		var jokers = From(r => r.Where(x => x.Joker == Joker.Double).ToList() is { Count: > 0 } d ? d.Average(x => x.Points / 2.0) : null);
		// (consistency: how little their place moves, as a share of the field, over 2+ nights)
		var spread = careers.ToDictionary(c => c.Id, c =>
		{
			var shares = nights.SelectMany(n => n.Players.Where(p => p.Id == c.Id).Select(p => n.Players.Count > 1 ? (p.Place - 1.0) / (n.Players.Count - 1) : 0)).ToList();
			if (shares.Count < 2) return (double?)null;
			var avg = shares.Average();
			return Math.Sqrt(shares.Average(s => (s - avg) * (s - avg)));
		});
		var wins = careers.ToDictionary(c => c.Id, c => c.Nights > 0 ? (double?)c.Wins / c.Nights : null);
		var podium = careers.ToDictionary(c => c.Id, c => c.Nights > 0 ? (double?)c.Podiums / c.Nights : null);
		string S(double? v, string unit) => v is { } x ? x.ToString("0.00", Inv) + unit : "–";
		return new()
		{
			new("Buzzer speed", Rate(buzz, id, false), Lang.F("Ø buzz time: {0}", S(buzz.GetValueOrDefault(id), " s"))),
			new("Quick answers", Rate(answer, id, false), Lang.F("Ø answer time: {0}", S(answer.GetValueOrDefault(id), " s"))),
			new("Composure", Rate(composure, id, false), composure.GetValueOrDefault(id) is { } c ? Lang.F("{0} % of their buzzes were wrong", Math.Round(c * 100)) : Lang.T("Not enough buzzes yet")),
			new("Spelling", Rate(spelling, id, false), mySpelled.Count >= MinQuestions
				? Lang.F("{0} of {1} right typed answers spelled perfectly", mySpelled.Count(x => x.Typos == 0), mySpelled.Count)
				: Lang.F("Fewer than {0} right typed answers so far", MinQuestions)),
			new("Consistency", Rate(spread, id, false), Lang.T("How little their place changes from night to night")),
			new("Winning habit", Rate(wins, id, true), Lang.F("Won {0} of {1} nights", careers.First(x => x.Id == id).Wins, careers.First(x => x.Id == id).Nights)),
			new("Big-game player", Rate(podium, id, true), Lang.T("How often they make the podium")),
			new("Joker timing", Rate(jokers, id, true), jokers.GetValueOrDefault(id) is { } j ? Lang.F("Ø {0} extra points per Double", j.ToString("0.0", Inv)) : Lang.T("No Double played yet")),
		};
	}

	static Control Header(Results.Career me, List<(Results.Night Night, Results.NightPlayer Me)> mine, int place, int hosted,
		List<(string Type, Attribute A)> types, List<Attribute> other, string tableLabel, bool puppy)
	{
		var who = EternalTableScreen.Who(me.Id, me.Name);
		var best = types.Where(t => t.A.Rating != null).OrderByDescending(t => t.A.Rating).ToList();
		var roles = new List<string>();
		if (other[0].Rating >= 16) roles.Add(Lang.T("Speed buzzer"));
		else if (other[3].Rating >= 18) roles.Add(Lang.T("Spelling bee"));
		roles.AddRange(best.Take(2 - roles.Count).Where(t => t.A.Rating >= 11).Select(t => Lang.T(TypeRole[t.Type])));
		var role = roles.Count > 0 ? string.Join(" · ", roles) : Lang.T("All-rounder");
		var avg = mine.Count > 0 ? mine.Average(m => m.Me.Points) : 0;
		Control Fact(string label, string value, Color? color = null) =>
			U.V(2, U.Eyebrow(label), U.Text(value, 22, color ?? C.Text, Fonts.Black).Raw());
		var facts = U.Flow(28,
			Fact("Ewige Tabelle", Lang.Ordinal(place), C.Medal(place) ?? C.Text),
			Fact("Nights", $"{me.Nights}"), Fact("Wins", $"{me.Wins}", me.Wins > 0 ? C.Gold : null), Fact("Podiums", $"{me.Podiums}"),
			Fact("Hosted", $"{hosted}").Tip(Lang.T("Quiz nights they were the quizmaster of (like international caps). Recorded from 0.9.4b on.")),
			Fact("Points", $"{me.Points}", C.Accent), Fact("Ø points", avg.ToString("0.0", Inv)),
			Fact("From the host", (me.HostPoints > 0 ? "+" : "") + me.HostPoints, me.HostPoints > 0 ? C.Pink : null)
				.Tip(Lang.T("Points the hosts handed them: bonuses and score changes by hand (recorded from 0.10.0b on).")));
		// (form: the last five nights' places, newest on the right, like FM)
		var form = U.H(6);
		foreach (var (night, p) in mine.TakeLast(5))
		{
			var pill = U.Panel(U.Mono(Lang.Ordinal(p.Place), 14, p.Place <= 3 ? C.Bg : C.Text).With(l => l.HorizontalAlignment = HorizontalAlignment.Center));
			pill.AddThemeStyleboxOverride("panel", UiKitBox(C.Medal(p.Place) ?? C.Panel2));
			pill.CustomMinimumSize = new Vector2(46, 0);
			pill.TooltipText = $"{EternalTableScreen.Date(night.Date)} · {night.Quiz} · {Lang.F("{0} pts", p.Points)}";
			form.AddChild(pill);
		}
		var since = mine.Count > 0 ? Lang.F("First quiz night: {0}", EternalTableScreen.Date(mine[0].Night.Date)) : "";
		var right = U.V(8, U.Eyebrow("Form"), form, U.Muted(since, 13), U.Muted(tableLabel, 12));
		right.SizeFlagsHorizontal = SizeFlags.ShrinkEnd;
		// (wide enough for five form pills, so the lines under them don't wrap after every word)
		right.CustomMinimumSize = new Vector2(5 * 46 + 4 * 6, 0);
		form.SizeFlagsHorizontal = SizeFlags.ShrinkBegin;
		var awards = U.H(8);
		if (puppy) awards.AddChild(PuppyBadge(15));
		var name = U.V(6, U.Text(me.Name, 38, C.Text, Fonts.Black).Raw(), U.H(12, U.Text(role, 18, C.Accent, Fonts.Bold).Raw(), awards), U.Gap(4), facts).Grow();
		return U.Panel(U.Pad(U.H(24, new Avatar(who, 120).With(a => a.SizeFlagsVertical = SizeFlags.ShrinkBegin), name, right), 8));
	}

	/// <summary>The award for whoever the hosts handed the most points to.</summary>
	public static Control PuppyBadge(int size)
	{
		var badge = U.Panel(U.Text(Lang.T("Host's Favourite Puppy"), size, C.Bg, Fonts.Black).Raw());
		badge.AddThemeStyleboxOverride("panel", UiTheme.Box(C.Pink, 999, null, 0, 10, 3));
		badge.SizeFlagsVertical = SizeFlags.ShrinkCenter;
		badge.TooltipText = Lang.T("Award: the hosts handed them more points than anyone else (bonuses and score changes by hand).");
		badge.MouseFilter = MouseFilterEnum.Stop;
		return badge;
	}

	static StyleBoxFlat UiKitBox(Color bg) => UiTheme.Box(bg, 8, null, 0, 8, 4);

	static Control AttributeList(List<Attribute> other, List<(string Type, Attribute A)> types)
	{
		var box = U.V(4);
		void Group(string title, IEnumerable<Attribute> list)
		{
			box.AddChild(U.Gap(4));
			box.AddChild(U.Text(Lang.T(title), 14, C.Muted, Fonts.Bold));
			foreach (var a in list)
			{
				var row = U.H(8, U.Text(Lang.T(a.Name), 15).Grow(), U.Mono(a.Rating?.ToString(Inv) ?? "–", 17, RatingColor(a.Rating)).MinSize(28));
				row.TooltipText = a.Tip;
				row.MouseFilter = MouseFilterEnum.Pass;
				box.AddChild(row);
			}
		}
		Group("Speed & technique", other.Take(4));
		Group("Mental", other.Skip(4));
		Group("Question types", types.Select(t => t.A));
		box.AddChild(U.Gap(4));
		box.AddChild(U.Muted("1–20 against everyone in this table. Hover one to see what it's based on.", 12));
		return box;
	}

	/// <summary>Strengths and weaknesses (kinds where they beat or trail the average most), records, jokers and their rival.</summary>
	static Control Report(Dictionary<string, List<Results.QuestionRow>> byPlayer, List<Results.QuestionRow> rows, string id,
		List<(Results.Night Night, Results.NightPlayer Me)> mine, List<Results.Night> nights, List<Results.QuestionRow> myRows)
	{
		var box = U.V(6);
		// (every type, subtype and category they played enough of: their hit rate minus everyone's)
		var kinds = myRows.Select(r => "type:" + r.Type).Concat(myRows.Where(r => r.Subtype != "").Select(r => "type:" + QuestionKinds.Key(r.Type, r.Subtype)))
			.Concat(myRows.Where(r => r.Category != "").Select(r => "cat:" + Categories.Key(r.Category))).Distinct()
			.Select(k =>
			{
				var my = myRows.Where(r => KindStats.Matches(k, r)).ToList();
				var all = rows.Where(r => KindStats.Matches(k, r)).ToList();
				return (Kind: k, N: my.Count, Diff: my.Count == 0 || all.Count == 0 ? 0 : 100.0 * (my.Count(r => r.Right) / (double)my.Count - all.Count(r => r.Right) / (double)all.Count));
			}).Where(x => x.N >= MinQuestions).ToList();
		void List(string title, IEnumerable<(string Kind, int N, double Diff)> list, Color color)
		{
			box.AddChild(U.Text(Lang.T(title), 14, C.Muted, Fonts.Bold));
			var any = false;
			foreach (var (kind, n, diff) in list)
			{
				any = true;
				var row = U.H(8, U.Text(KindStats.FilterLabel(kind), 15).Raw().Grow(), U.Mono((diff >= 0 ? "+" : "") + Math.Round(diff).ToString(Inv) + " %", 14, color));
				row.TooltipText = Lang.F("Hit rate {0} points compared to everyone ({1} questions)", Math.Round(diff), n);
				row.MouseFilter = MouseFilterEnum.Pass;
				box.AddChild(row);
			}
			if (!any) box.AddChild(U.Muted("Not enough questions yet", 13));
			box.AddChild(U.Gap(6));
		}
		// (one line per type at most: "Classic question" and "Classic question · A B C D" say the same twice)
		static IEnumerable<(string Kind, int N, double Diff)> OnePerType(IEnumerable<(string Kind, int N, double Diff)> list)
		{
			var types = new HashSet<string>();
			foreach (var k in list)
				if (!k.Kind.StartsWith("type:") || types.Add(QuestionKinds.Split(k.Kind[5..]).Type)) yield return k;
		}
		List("Strengths", OnePerType(kinds.Where(k => k.Diff > 0).OrderByDescending(k => k.Diff)).Take(3), C.Teal);
		List("Weaknesses", OnePerType(kinds.Where(k => k.Diff < 0).OrderBy(k => k.Diff)).Take(3), C.Coral);

		box.AddChild(U.Text(Lang.T("Records"), 14, C.Muted, Fonts.Bold));
		void Record(string label, string? value, string? note = null)
		{
			if (value == null) return;
			var row = U.H(8, U.Text(Lang.T(label), 15).Grow(), U.Mono(value, 14, C.Accent));
			if (note != null) row.TooltipText = note;
			row.MouseFilter = MouseFilterEnum.Pass;
			box.AddChild(row);
		}
		string Where(Results.QuestionRow r) => $"{EternalTableScreen.Date(r.Date)} · {r.Quiz}";
		var fastest = myRows.Where(r => r.BuzzS != null).OrderBy(r => r.BuzzS).FirstOrDefault();
		Record("Fastest buzz", fastest?.BuzzS is { } b ? b.ToString("0.00", Inv) + " s" : null, fastest != null ? Where(fastest) : null);
		var pin = myRows.Where(r => r.Km != null).OrderBy(r => r.Km).FirstOrDefault();
		Record("Closest pin", pin?.Km is { } km ? NightStatsView.Distance(km) : null, pin != null ? Where(pin) : null);
		var guess = myRows.Where(r => r.PercentOff != null).OrderBy(r => r.PercentOff).FirstOrDefault();
		Record("Closest guess", guess?.PercentOff is { } pct ? NightStatsView.Percent(pct) : null, guess != null ? Where(guess) : null);
		var bestNight = mine.OrderByDescending(m => m.Me.Points).FirstOrDefault();
		Record("Most points in a night", bestNight.Night != null ? $"{bestNight.Me.Points}" : null, bestNight.Night != null ? $"{EternalTableScreen.Date(bestNight.Night.Date)} · {bestNight.Night.Quiz}" : null);
		Record("Most right in a night", mine.Count > 0 ? $"{mine.Max(m => m.Me.Scored)}" : null);
		var streak = 0;
		var run = 0;
		foreach (var m in mine)
		{
			run = m.Me.Place == 1 ? run + 1 : 0;
			streak = Math.Max(streak, run);
		}
		Record("Longest winning streak", streak > 0 ? $"{streak}" : null);
		var doubles = myRows.Where(r => r.Joker == Joker.Double).ToList();
		Record("Double points played", doubles.Count > 0 ? Lang.F("{0} (+{1})", doubles.Count, doubles.Sum(r => r.Points / 2)) : null, Lang.T("How many times, and the extra points they brought"));
		box.AddChild(U.Gap(6));

		// (rival: whoever finished right next to them most often)
		var rival = nights.Where(n => n.Players.Any(p => p.Id == id)).SelectMany(n =>
		{
			var my = n.Players.First(p => p.Id == id).Place;
			return n.Players.Where(p => p.Id != id && Math.Abs(p.Place - my) == 1).Select(p => (p.Id, p.Name, Ahead: my < p.Place));
		}).GroupBy(x => x.Id).OrderByDescending(g => g.Count()).FirstOrDefault();
		box.AddChild(U.Text(Lang.T("Rival"), 14, C.Muted, Fonts.Bold));
		if (rival != null)
		{
			var name = rival.Last().Name;
			var row = U.H(8, new Avatar(EternalTableScreen.Who(rival.Key, name), 24), EternalTableScreen.NameLink(rival.Key, name, 15, C.Text).Grow(),
				U.Mono(Lang.F("{0}× next to each other", rival.Count()), 13, C.Muted));
			box.AddChild(row);
			box.AddChild(U.Muted(Lang.F("You were ahead {0} of those times.", rival.Count(x => x.Ahead)), 13));
		}
		else box.AddChild(U.Muted("No rival yet", 13));
		return box;
	}

	/// <summary>Every night, newest first: date, quiz, place, points and their numbers; click one to open it.</summary>
	static Control Career(List<(Results.Night Night, Results.NightPlayer Me)> mine)
	{
		var grid = new LinedGrid { Columns = 8 };
		grid.AddThemeConstantOverride("h_separation", 18);
		grid.AddThemeConstantOverride("v_separation", 10);
		foreach (var h in new[] { "Date", "Quiz", "#", "Players", "Points", "Scored", "Ø answer time", "Ø buzz time" }) grid.AddChild(U.Eyebrow(h));
		foreach (var (night, p) in Enumerable.Reverse(mine))
		{
			grid.AddChild(U.Mono(EternalTableScreen.Date(night.Date), 14, C.Muted));
			var n = night;
			grid.AddChild(U.Link(night.Quiz, () => App.I.GoMatchNight(n), 15).Raw().With(b => b.Alignment = HorizontalAlignment.Left));
			grid.AddChild(GameParts.PlaceBox(p.Place));
			grid.AddChild(U.Mono($"{night.Players.Count}", 14, C.Muted));
			grid.AddChild(U.Mono($"{p.Points}", 15, C.Accent));
			grid.AddChild(U.Mono($"{p.Scored}", 14));
			grid.AddChild(U.Mono(NightStatsView.Seconds(p.AvgAnswerS), 14, C.Muted));
			grid.AddChild(U.Mono(NightStatsView.Seconds(p.AvgBuzzS), 14, C.Muted));
		}
		return grid;
	}
}

/// <summary>The question type attributes as a radar: the further out, the better (20 = the edge).</summary>
public partial class ProfileRadar : Control
{
	readonly List<(string Type, ProfileScreen.Attribute A)> _types;

	public ProfileRadar() : this(new()) { }
	public ProfileRadar(List<(string Type, ProfileScreen.Attribute A)> types)
	{
		_types = types;
		AccessibilityName = Lang.T("Question type attributes");
		AccessibilityDescription = string.Join(", ", types.Select(t => $"{Lang.T(t.A.Name)} {t.A.Rating?.ToString() ?? "–"}"));
	}

	public override void _Draw()
	{
		var n = _types.Count;
		if (n < 3) return;
		var center = Size / 2;
		var radius = Mathf.Min(Size.X - 200, Size.Y - 50) / 2;
		Vector2 At(int i, float r) => center + new Vector2(Mathf.Sin(Mathf.Tau * i / n), -Mathf.Cos(Mathf.Tau * i / n)) * r;
		var grid = C.Border with { A = 0.4f };
		foreach (var ring in new[] { 0.25f, 0.5f, 0.75f, 1f })
			DrawPolyline(Enumerable.Range(0, n + 1).Select(i => At(i % n, radius * ring)).ToArray(), grid, 1, true);
		for (var i = 0; i < n; i++) DrawLine(center, At(i, radius), grid, 1);
		var shape = Enumerable.Range(0, n).Select(i => At(i, radius * (_types[i].A.Rating ?? 1) / 20f)).ToArray();
		DrawColoredPolygon(shape, C.Accent with { A = 0.28f });
		DrawPolyline(shape.Append(shape[0]).ToArray(), C.Accent, 2, true);
		for (var i = 0; i < n; i++)
		{
			// (labels lean away from the middle: right of the right half, left of the left half)
			var p = At(i, radius + 12);
			var label = Lang.T(_types[i].A.Name);
			var dx = p.X - center.X;
			var (x, align) = dx > 8 ? (p.X, HorizontalAlignment.Left) : dx < -8 ? (p.X - 100, HorizontalAlignment.Right) : (p.X - 50, HorizontalAlignment.Center);
			var y = p.Y + 4 + (p.Y > center.Y + 8 ? 6 : p.Y < center.Y - 8 ? -2 : 0);
			DrawString(Fonts.Mono, new Vector2(x, y), label, align, 100, 11, _types[i].A.Rating == null ? C.Muted : C.Text);
		}
	}
}

/// <summary>
/// FM's "positions" pitch, with the eleven question types as the eleven positions: green circles where they're a
/// natural (16+), lighter where accomplished, orange and red where it's not their game, grey without enough data.
/// </summary>
public partial class ProfilePitch : Control
{
	readonly List<(string Type, ProfileScreen.Attribute A)> _types;

	/// <summary>Where each type plays on the pitch (0–1 across, 0 = our goal at the bottom … 1 = attack at the top).</summary>
	static readonly Dictionary<string, Vector2> Spots = new()
	{
		[QuestionType.MapPin] = new(0.5f, 0.92f),
		[QuestionType.Order] = new(0.12f, 0.7f), [QuestionType.Estimation] = new(0.37f, 0.74f), [QuestionType.Write] = new(0.63f, 0.74f), [QuestionType.Classic] = new(0.88f, 0.7f),
		[QuestionType.Picture] = new(0.22f, 0.48f), [QuestionType.Steps] = new(0.5f, 0.52f), [QuestionType.Image] = new(0.78f, 0.48f),
		[QuestionType.Sound] = new(0.2f, 0.2f), [QuestionType.Interactive] = new(0.5f, 0.14f), [QuestionType.Video] = new(0.8f, 0.2f),
	};

	public ProfilePitch() : this(new()) { }

	public ProfilePitch(List<(string Type, ProfileScreen.Attribute A)> types)
	{
		_types = types;
		MouseFilter = MouseFilterEnum.Stop;
		TooltipText = " ";
	}

	Rect2 Field => new(new Vector2(20, 6), new Vector2(Size.X - 40, Size.Y - 12));

	Vector2 Pos(string type) => Field.Position + Spots[type] * Field.Size;

	public override void _Draw()
	{
		var f = Field;
		DrawRect(f, new Color("#1d3b2a"));
		var line = new Color(1, 1, 1, 0.25f);
		DrawRect(f, line, false, 2);
		DrawLine(new Vector2(f.Position.X, f.GetCenter().Y), new Vector2(f.End.X, f.GetCenter().Y), line, 2);
		DrawArc(f.GetCenter(), f.Size.X * 0.14f, 0, Mathf.Tau, 48, line, 2);
		var box = new Vector2(f.Size.X * 0.5f, f.Size.Y * 0.14f);
		DrawRect(new Rect2(new Vector2(f.GetCenter().X - box.X / 2, f.Position.Y), box), line, false, 2);
		DrawRect(new Rect2(new Vector2(f.GetCenter().X - box.X / 2, f.End.Y - box.Y), box), line, false, 2);
		foreach (var (type, a) in _types.Where(t => Spots.ContainsKey(t.Type)))
		{
			var p = Pos(type);
			var color = ProfileScreen.RatingColor(a.Rating);
			if (a.Rating == null) DrawArc(p, 11, 0, Mathf.Tau, 24, C.Muted, 2);
			else DrawCircle(p, 11, color);
			// (the attribute's name, short enough for its spot; the keeper's above, out of the box)
			if (Spots[type].Y < 0.9f) DrawString(Fonts.Mono, p + new Vector2(-55, 26), Lang.T(a.Name), HorizontalAlignment.Center, 110, 11, new Color(1, 1, 1, 0.85f));
			else DrawString(Fonts.Mono, p + new Vector2(16, 5), Lang.T(a.Name), HorizontalAlignment.Left, 110, 11, new Color(1, 1, 1, 0.85f));
			if (a.Rating is { } r) DrawString(Fonts.Mono, p + new Vector2(-15, 5), $"{r}", HorizontalAlignment.Center, 30, 11, C.Bg);
		}
	}

	public override string _GetTooltip(Vector2 at)
	{
		var hit = _types.Where(t => Spots.ContainsKey(t.Type)).FirstOrDefault(t => Pos(t.Type).DistanceTo(at) < 16);
		return hit.A != null ? $"{Lang.T(hit.A.Name)}: {hit.A.Rating?.ToString() ?? "–"}\n{hit.A.Tip}" : "";
	}
}

/// <summary>A bar per night: the points they got, coloured by their place (gold, silver, bronze, the rest plain).</summary>
public partial class ProfileBars : Control
{
	readonly List<(int Points, int Place, string Quiz, string Date)> _nights;

	public ProfileBars() : this(new()) { }

	public ProfileBars(List<(int Points, int Place, string Quiz, string Date)> nights)
	{
		_nights = nights;
		AccessibilityName = Lang.T("Points per night");
		AccessibilityDescription = string.Join(", ", nights.Select(n => $"{n.Date}: {Lang.Ordinal(n.Place)}, {n.Points}"));
		MouseFilter = MouseFilterEnum.Stop;
	}

	Rect2 Bar(int i)
	{
		var max = Math.Max(1, _nights.Max(n => n.Points));
		var w = (Size.X - 40) / Math.Max(1, _nights.Count);
		var h = (Size.Y - 24) * Math.Max(0, _nights[i].Points) / max;
		return new Rect2(40 + i * w + w * 0.15f, Size.Y - 20 - h, w * 0.7f, h);
	}

	public override void _Draw()
	{
		if (_nights.Count == 0) return;
		var max = Math.Max(1, _nights.Max(n => n.Points));
		DrawLine(new Vector2(40, Size.Y - 20), new Vector2(Size.X, Size.Y - 20), C.Border, 1);
		DrawString(Fonts.Mono, new Vector2(0, 14), $"{max}", HorizontalAlignment.Right, 34, 11, C.Muted);
		DrawString(Fonts.Mono, new Vector2(0, Size.Y - 16), "0", HorizontalAlignment.Right, 34, 11, C.Muted);
		for (var i = 0; i < _nights.Count; i++)
		{
			var color = C.Medal(_nights[i].Place) ?? C.Muted with { A = 0.45f };
			DrawRect(Bar(i), i == _hover ? color.Lightened(0.35f) with { A = Math.Max(color.A, 0.8f) } : color);
		}
		// (the hovered night's place and points above its bar; the tooltip adds the date and quiz)
		if (_hover is int h && h < _nights.Count)
		{
			var r = Bar(h);
			var label = $"{Lang.Ordinal(_nights[h].Place)} · {Lang.F("{0} pts", _nights[h].Points)}";
			var x = Math.Clamp(r.GetCenter().X - 60, 0, Math.Max(0, Size.X - 120));
			DrawString(Fonts.Bold, new Vector2(x, Math.Max(13, r.Position.Y - 6)), label, HorizontalAlignment.Center, 120, 13, C.Text);
		}
	}

	int? _hover;

	int? At(Vector2 at)
	{
		for (var i = 0; i < _nights.Count; i++)
		{
			var r = Bar(i);
			if (at.X >= r.Position.X - 3 && at.X <= r.End.X + 3) return i;
		}
		return null;
	}

	public override void _GuiInput(InputEvent e)
	{
		if (e is InputEventMouseMotion m && At(m.Position) != _hover)
		{
			_hover = At(m.Position);
			QueueRedraw();
		}
	}

	public override void _Notification(int what)
	{
		if (what == NotificationMouseExit && _hover != null)
		{
			_hover = null;
			QueueRedraw();
		}
	}

	public override string _GetTooltip(Vector2 at) =>
		At(at) is int i ? $"{_nights[i].Date} · {_nights[i].Quiz}\n{Lang.Ordinal(_nights[i].Place)} · {Lang.F("{0} pts", _nights[i].Points)}" : "";
}
