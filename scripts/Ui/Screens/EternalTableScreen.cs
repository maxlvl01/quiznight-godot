using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Godot;
using QuizNight.Shared;
using QuizNight.Storage;

namespace QuizNight.Ui.Screens;

/// <summary>
/// The eternal table: every quiz night whose results came with the game (Results.BundledCsvs), added up per player,
/// like the Bundesliga's "Ewige Tabelle". All time or one season; ranked by quiz points or by championship points (each
/// night's place scored like a race: F1, MotoGP …); for all questions or one kind (a type, subtype or category), whose
/// own stats then fill the columns. The main number is always the last column, like in a football table.
/// </summary>
public partial class EternalTableScreen : Control
{
	static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

	/// <summary>One player's line: name and every column's value (null: none yet, e.g. no estimations).</summary>
	sealed class Line
	{
		public string Id = "";
		public string Name = "";
		public readonly Dictionary<string, double?> V = new();
	}

	/// <summary>The column the table is sorted by, and which way (kept while the game runs).</summary>
	static string _sort = "";
	static bool _descending = true;
	/// <summary>"all", "season:N" or "beta"; empty: the beta until 1.0 has nights of its own, then all time.</summary>
	static string _table = "";
	/// <summary>"quiz" (quiz points) or a scoring preset's name (championship points per night's place).</summary>
	static string _system = ScoringPresets.BuiltIn[0].Name;
	/// <summary>Which questions (see KindStats.Matches).</summary>
	static string _kind = "";
	/// <summary>"table", or a graph: "positions" (everyone's place after each night) or "points" (climbing from 0).</summary>
	static string _graph = "table";

	/// <summary>Sorts the table by a column (dev runs: --sort).</summary>
	public static void SortBy(string col, bool ascending) => (_sort, _descending) = (col, !ascending);

	/// <summary>Dev runs: which table, ranking and questions (--table, --ranking, --kind).</summary>
	public static void View(string? table, string? system, string? kind, string? graph = null)
	{
		if (graph != null) _graph = graph;
		if (table != null) _table = table;
		if (system != null) _system = system == "quiz" ? "quiz" : ScoringPresets.BuiltIn.FirstOrDefault(p => p.Name.StartsWith(system, System.StringComparison.OrdinalIgnoreCase))?.Name ?? _system;
		if (kind != null) _kind = kind;
	}

	static ScoringPreset? Ranking => _system == "quiz" ? null : ScoringPresets.BuiltIn.FirstOrDefault(p => p.Name == _system);

	/// <summary>
	/// "▲2" / "▼1" next to a place: how it changed with the newest quiz night ("New" for someone who played their first;
	/// nothing when it stayed, or before there are two nights).
	/// </summary>
	static Control Move(int? place, Dictionary<string, int?>? before, string id)
	{
		var label = U.Mono("", 12, C.Muted).MinSize(32);
		label.SizeFlagsVertical = SizeFlags.ShrinkCenter;
		if (before == null || place is not { } now) return label;
		if (!before.TryGetValue(id, out var was))
		{
			label.Text = Lang.T("New");
			label.AddThemeColorOverride("font_color", C.Sky);
			label.TooltipText = Lang.T("First quiz night in this table");
		}
		else if (was is { } then && then != now)
		{
			label.Text = (then > now ? "▲" : "▼") + System.Math.Abs(then - now);
			label.AddThemeColorOverride("font_color", then > now ? C.Teal : C.Coral);
			label.TooltipText = Lang.F(then > now ? "Up from {0} with the last quiz night" : "Down from {0} with the last quiz night", Lang.Ordinal(then));
		}
		label.MouseFilter = MouseFilterEnum.Stop;
		return label.Raw();
	}

	/// <summary>Which Statistics tab was open last ("table" or "history"), so Statistics opens there again.</summary>
	public static string LastTab = "table";

	/// <summary>
	/// The top line of both Statistics screens: back home on the left, and on the right the switch between the
	/// Ewige Tabelle and the match history.
	/// </summary>
	public static Control TopBar(string tab)
	{
		LastTab = tab;
		var tabs = U.Segmented(new[] { ("table", "🏆 Ewige Tabelle"), ("history", "Match history") }, tab, id =>
		{
			if (id == "history") App.I.GoMatchHistory();
			else App.I.GoEternalTable();
		});
		tabs.SizeFlagsHorizontal = SizeFlags.ShrinkEnd;
		var home = U.Link("← Home", () => App.I.GoHome());
		home.SizeFlagsHorizontal = SizeFlags.ShrinkBegin | SizeFlags.Expand;
		home.SizeFlagsVertical = SizeFlags.ShrinkCenter;
		return U.H(12, home, tabs);
	}

	/// <summary>Someone as an avatar can draw them: their Steam picture, or initials in a colour of their own.</summary>
	public static PublicPlayer Who(string id, string name) => new()
	{
		Id = id, Name = name, Connected = true,
		Color = Limits.PlayerColors[id.Aggregate(0, (h, ch) => (h * 31 + ch) & 0xffffff) % Limits.PlayerColors.Length],
	};

	/// <summary>A name that opens the player's profile page (you in bold, everyone else plain).</summary>
	public static Control NameLink(string id, string name, int size, Color color, Font? font = null)
	{
		var label = U.Text(name, size, color, font ?? (id == MatchHistoryScreen.MyId ? Fonts.Black : null)).Raw();
		label.MouseFilter = MouseFilterEnum.Stop;
		label.MouseDefaultCursorShape = CursorShape.PointingHand;
		label.TooltipText = Lang.T("Open the profile");
		label.GuiInput += e =>
		{
			if (e is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left }) App.I.GoProfile(id);
		};
		return label;
	}

	/// <summary>The overall columns (all questions): nights, wins, podiums, the averages, then the points.</summary>
	static List<KindStats.Col> OverallColumns()
	{
		var cols = new List<KindStats.Col>
		{
			new("nights", "Nights", true, "n"), new("wins", "Wins", true, "n"), new("podiums", "Podiums", true, "n"),
			new("avgPoints", "Ø points", true, "avg", "Quiz points per night"),
			new("answer", "Ø answer time", false, "s", "Until they locked in an answer or sent a recording (before 0.10.0b buzzes counted in it too)"),
			new("buzz", "Ø buzz time", false, "s", "Until they buzzed in (recorded from 0.10.0b on)"),
			new("km", "Ø km off", false, "km"), new("pct", "Ø % off", false, "off", "Estimations: how far off, in % of the right number"),
			new("points", "Points", true, "n", "Quiz points of all nights added up"),
		};
		if (Ranking is { } s) cols.Add(new("champ", "Champ. points", true, "n", Lang.F("Each night's place scored like {0}: {1}", s.Name, ScoringPresets.Describe(s.Points))));
		return cols;
	}

	/// <summary>Everyone's lines in the table's own order (best first), for the nights in range.</summary>
	static List<Line> Lines(List<string> csvs, string? from, string? before)
	{
		if (_kind == "")
		{
			var table = Results.Table(csvs, before, from);
			var lines = table.Select(c =>
			{
				var l = new Line { Id = c.Id, Name = c.Name };
				l.V["nights"] = c.Nights;
				l.V["wins"] = c.Wins;
				l.V["podiums"] = c.Podiums;
				l.V["avgPoints"] = c.AvgPoints;
				l.V["answer"] = c.AvgAnswerS;
				l.V["buzz"] = c.AvgBuzzS;
				l.V["km"] = c.AvgKm;
				l.V["pct"] = c.AvgPercentOff;
				l.V["points"] = c.Points;
				if (Ranking is { } s) l.V["champ"] = c.ChampionshipPoints(s.Points);
				return l;
			}).ToList();
			// (championship: most of those points first; equal ones keep the quiz points' order)
			return Ranking != null ? lines.OrderByDescending(l => l.V["champ"]).ToList() : lines;
		}
		return Results.Questions(csvs, from, before).Where(r => KindStats.Matches(_kind, r)).GroupBy(r => r.PlayerId)
			.Select(g =>
			{
				var l = new Line { Id = g.Key, Name = g.Last().Player };
				foreach (var (k, v) in KindStats.Values(g.ToList())) l.V[k] = v;
				return l;
			})
			.OrderByDescending(l => l.V["points"]).ThenByDescending(l => l.V["rate"]).ThenBy(l => l.Name).ToList();
	}

	static string MainColumn => _kind == "" && Ranking != null ? "champ" : "points";

	static double? Key(Line l) => l.V.GetValueOrDefault(_sort);

	/// <summary>
	/// Everyone's place in the sorted column: the best value is 1st, whichever way the column is turned, and equal values
	/// share a place. By the main column (and by name) it's the table's own order, where the tiebreaks decide.
	/// </summary>
	static Dictionary<string, int?> Places(List<Line> table, List<KindStats.Col> cols)
	{
		if (_sort == MainColumn || _sort == "player") return table.Select((c, n) => (c.Id, (int?)(n + 1))).ToDictionary(x => x.Id, x => x.Item2);
		var better = cols.FirstOrDefault(c => c.Id == _sort)?.HigherIsBetter ?? true;
		return table.ToDictionary(c => c.Id, c => Key(c) is { } v
			? 1 + table.Count(o => Key(o) is { } w && (better ? w > v : w < v)) : (int?)null);
	}

	static List<Line> Sorted(List<Line> table)
	{
		// (players without a value for a column, e.g. no estimations yet, go last either way)
		if (_sort == "player") return (_descending ? table.OrderByDescending(c => c.Name) : table.OrderBy(c => c.Name)).ToList();
		var has = table.Where(c => Key(c) != null);
		var sorted = _descending ? has.OrderByDescending(c => Key(c)) : has.OrderBy(c => Key(c));
		// (OrderBy keeps the table's order for equal values, so ties sit like their places)
		return sorted.Concat(table.Where(c => Key(c) == null)).ToList();
	}

	/// <summary>
	/// The graph of the shown table: after each night of it, everyone's place (in the table's own order) or main number
	/// (points, championship points, or the kind's points), with the players listed beside it to highlight.
	/// </summary>
	static Control Graph(List<string> csvs, string? from, string? until, List<Line> final)
	{
		var nights = Results.Nights(csvs).Where(n => (from == null || string.CompareOrdinal(n.Date, from) >= 0)
			&& (until == null || string.CompareOrdinal(n.Date, until) < 0)).Reverse().ToList();
		var positions = _graph == "positions";
		var series = final.Select((l, i) => new StandingsChart.Series
		{
			Id = l.Id, Name = l.Name, Color = StandingsChart.Colour(i), Values = new double?[nights.Count],
		}).ToList();
		for (var k = 0; k < nights.Count; k++)
		{
			var after = Lines(csvs, from, k + 1 < nights.Count ? nights[k + 1].Date : until);
			for (var i = 0; i < after.Count; i++)
				if (series.Find(s => s.Id == after[i].Id) is { } s)
					s.Values[k] = positions ? i + 1 : after[i].V.GetValueOrDefault(MainColumn) ?? 0;
		}
		var labels = nights.Select((n, k) => Lang.F("Night {0} · {1} · {2}", k + 1, Date(n.Date), n.Quiz)).ToList();
		var chart = new StandingsChart(series, labels, positions) { TooltipText = " " };
		chart.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		// (beside it: everyone in the table's order, click to highlight; "All" clears)
		var list = U.V(4, U.Eyebrow("Players"));
		var buttons = new List<(Button B, string Id)>();
		var clear = U.Link("Show everyone", () => chart.Toggle(null), 13);
		list.AddChild(clear);
		for (var i = 0; i < final.Count; i++)
		{
			var l = final[i];
			var id = l.Id;
			var b = U.Btn(Lang.F("{0}  {1}", Lang.Ordinal(i + 1), l.Name), "LinkButton2", () => chart.Toggle(id), 14);
			b.Alignment = HorizontalAlignment.Left;
			b.AddThemeColorOverride("font_color", StandingsChart.Colour(i));
			b.AutoTranslateMode = AutoTranslateModeEnum.Disabled;
			buttons.Add((b, id));
			list.AddChild(b);
		}
		chart.HighlightChanged += () =>
		{
			foreach (var (b, id) in buttons) b.Modulate = chart.Highlighted.Count == 0 || chart.Highlighted.Contains(id) ? Colors.White : new Color(1, 1, 1, 0.35f);
		};
		list.CustomMinimumSize = new Vector2(190, 0);
		var hint = positions
			? "Everyone's place after each quiz night (by the table's ranking). Click a name to highlight them, hover for the night."
			: "Everyone's points after each quiz night, from 0. Click a name to highlight them, hover for the night.";
		return U.V(8, U.Muted(hint, 13), U.Card(0, U.H(16, chart, list)));
	}

	/// <summary>
	/// The table picked at the top (also what the profile pages count): its id, results, date range and name.
	/// </summary>
	public static (string Id, List<string> Csvs, string? From, string? Until, string Label) Current()
	{
		var main = Results.BundledCsvs();
		var table = _table != "" ? _table : main.Count == 0 ? "beta" : "all";
		if (table == "beta") return (table, Results.BundledCsvs(true), null, null, Lang.T("Beta (before 1.0)"));
		var season = table.StartsWith("season:") ? Results.Seasons(main).FirstOrDefault(s => "season:" + s.Id == table) : default;
		return season.Id != null ? (table, main, season.From, season.Before, season.Label) : ("all", main, null, null, Lang.T("All time (from 1.0)"));
	}

	/// <summary>The shown standings as a picture (SeasonPoster): champion, podium, everyone's points and wins.</summary>
	public static System.Threading.Tasks.Task<string?> SavePicture(Control owner, List<string> csvs, string? from, string? until, string title, string? to = null) =>
		SeasonPoster.Save(owner, AwardPack(csvs, from, until, title), to);

	static bool _devAwardPack;

	static SeasonPoster.Pack AwardPack(List<string> csvs, string? from, string? until, string title)
	{
		var lines = Lines(csvs, from, until);
		var main = MainColumn;
		var standings = lines.Select(l => new SeasonPoster.Standing(l.Id, l.Name, (int)(l.V.GetValueOrDefault(main) ?? 0), (int)(l.V.GetValueOrDefault("wins") ?? 0),
			(int)(l.V.GetValueOrDefault("nights") ?? 0))).ToList();
		var nights = Results.Nights(csvs).Where(n => (from == null || string.CompareOrdinal(n.Date, from) >= 0) && (until == null || string.CompareOrdinal(n.Date, until) < 0)).ToList();
		var footer = nights.Count > 0 ? Lang.F("{0} quiz nights · {1} – {2}", nights.Count, Date(nights[^1].Date), Date(nights[0].Date)) : "";
		var ranking = Ranking is { } s ? Lang.F("Championship points like {0}: {1}", Lang.T(s.Name), ScoringPresets.Describe(s.Points)) : Lang.T("Quiz points added up");
		return new(title, ranking, Ranking != null ? "championship points" : "points", standings, footer, Awards(csvs, from, until));
	}

	/// <summary>
	/// The award pack's awards: the best at each question type and category (most points from it, then the better hit
	/// rate), the Host's Favourite Puppy, the fastest buzzer and the best speller.
	/// </summary>
	static List<SeasonPoster.Award> Awards(List<string> csvs, string? from, string? until)
	{
		var rows = Results.Questions(csvs, from, until);
		var awards = new List<SeasonPoster.Award>();
		List<(string, string, string)> Top(IEnumerable<Results.QuestionRow> of, System.Func<Dictionary<string, double?>, double?> key, bool high,
			System.Func<Dictionary<string, double?>, string> text, System.Func<List<Results.QuestionRow>, bool>? enough = null) =>
			of.GroupBy(r => r.PlayerId).Where(g => enough?.Invoke(g.ToList()) ?? true)
				.Select(g => (Id: g.Key, Name: g.Last().Player, V: KindStats.Values(g.ToList())))
				.Where(x => key(x.V) != null).OrderBy(x => high ? -key(x.V)!.Value : key(x.V)!.Value).ThenByDescending(x => x.V["rate"])
				.Take(3).Select(x => (x.Id, x.Name, text(x.V))).ToList();
		string Pts(Dictionary<string, double?> v) => Lang.F("{0} points · {1} % right", v["points"] ?? 0, System.Math.Round(v["rate"] ?? 0));
		foreach (var type in QuestionType.All.Where(t => rows.Any(r => r.Type == t)))
			awards.Add(new(QuestionKinds.TypeLabel(type), Lang.T(ProfileScreen.TypeRole[type]),
				Lang.F("Most points from {0} questions", Lang.T(QuestionKinds.TypeLabel(type))), Top(rows.Where(r => r.Type == type), v => v["points"], true, Pts)));
		foreach (var cat in rows.Where(r => r.Category != "").GroupBy(r => Categories.Key(r.Category)))
		{
			var label = Categories.Label(cat.First().Category);
			awards.Add(new("Category " + label, Lang.F("{0} champion", label), Lang.F("Most points in {0}", label), Top(cat, v => v["points"], true, Pts)));
		}
		var puppy = Results.Table(csvs, until, from).Where(c => c.HostPoints > 0).OrderByDescending(c => c.HostPoints).Take(3)
			.Select(c => (c.Id, c.Name, Lang.F("+{0} points from the host", c.HostPoints))).ToList();
		if (puppy.Count > 0) awards.Add(new("Hosts Favourite Puppy", Lang.T("Host's Favourite Puppy"), Lang.T("The hosts handed them the most points (bonuses and by hand)"), puppy));
		var buzz = Top(rows, v => v["avgBuzz"], false, v => Lang.F("Ø {0} s per buzz", (v["avgBuzz"] ?? 0).ToString("0.00", Inv)), r => r.Sum(x => x.Buzzes) >= 5);
		if (buzz.Count > 0) awards.Add(new("Fastest buzzer", Lang.T("Fastest buzzer"), Lang.T("The quickest average buzz (5 buzzes or more)"), buzz));
		var spell = Top(rows, v => v["perfect"], true, v => Lang.F("{0} % spelled perfectly", System.Math.Round(v["perfect"] ?? 0)), r => r.Count(x => x.Right && x.Typos != null) >= 3);
		if (spell.Count > 0) awards.Add(new("Spelling bee", Lang.T("Spelling bee"), Lang.T("The most typed answers without a single typo"), spell));
		return awards;
	}

	/// <summary>"2026-10-24 21:25" → "24.10.2026".</summary>
	public static string Date(string date) =>
		System.DateTime.TryParseExact(date, "yyyy-MM-dd HH:mm", Inv, DateTimeStyles.None, out var d) ? d.ToString("dd.MM.yyyy", Inv) : date;

	public EternalTableScreen()
	{
		SetAnchorsPreset(LayoutPreset.FullRect);
		var mainCsvs = Results.BundledCsvs();
		var (table, csvs, from, until, _) = Current();
		var beta = table == "beta";
		var seasons = Results.Seasons(mainCsvs);
		var season = table.StartsWith("season:") ? seasons.FirstOrDefault(s => "season:" + s.Id == table) : default;

		var tables = new List<(string, string)> { ("all", Lang.T("All time (from 1.0)")) };
		tables.AddRange(seasons.Select(s => ("season:" + s.Id, s.Label)));
		tables.Add(("beta", Lang.T("Beta (before 1.0)")));
		var tableSelect = U.Select(tables.ToArray(), table, id =>
		{
			_table = id;
			App.I.GoEternalTable();
		});
		var systems = new List<(string, string)> { ("quiz", Lang.T("Quiz points")) };
		systems.AddRange(ScoringPresets.BuiltIn.Select(p => (p.Name, Lang.F("{0} ({1})", Lang.T(p.Name), ScoringPresets.Describe(p.Points, 3)))));
		var systemSelect = U.Select(systems.ToArray(), _system, id =>
		{
			_system = id;
			if (_sort is "champ" or "points") _sort = "";
			App.I.GoEternalTable();
		});
		var allRows = Results.Questions(csvs, from, until);
		var filters = KindStats.Filters(allRows);
		if (filters.All(f => f.Id != _kind)) filters.Add((_kind, KindStats.FilterLabel(_kind)));
		var kindSelect = U.Select(filters.ToArray(), _kind, id =>
		{
			_kind = id;
			_sort = "";
			App.I.GoEternalTable();
		});
		var graphs = U.Segmented(new[] { ("table", "Table"), ("positions", "Positions"), ("points", "Points") }, _graph, id =>
		{
			_graph = id;
			App.I.GoEternalTable();
		});
		var pickers = U.H(12, U.Field("Table", tableSelect), U.Field("Ranked by", systemSelect), U.Field("Questions", kindSelect), U.Field("Show", graphs));
		systemSelect.Disabled = _kind != "";
		if (_kind != "") systemSelect.TooltipText = Lang.T("Championship points go by each night's place, so they're for all questions only.");

		var title = beta ? "🏆 Ewige Tabelle · Beta" : season.Id != null ? Lang.F("🏆 Ewige Tabelle · {0}", season.Label) : "🏆 Ewige Tabelle";
		var intro = _kind != ""
			? Lang.F("Only {0} (recorded from 0.10.0b on): most points from them first. Click a heading to sort, a name for the profile.", KindStats.FilterLabel(_kind))
			: Ranking is { } sys
				? Lang.F("Every quiz night scores like a race ({0}: {1}): most championship points first. Click a heading to sort, a name for the profile.", Lang.T(sys.Name), ScoringPresets.Describe(sys.Points))
				: Lang.T("Every quiz night added up: most points first, then the faster average answer time. Click a heading to sort, a name for the profile.");
		var save = U.Btn("Save award pack", "", null, 14);
		save.SizeFlagsVertical = SizeFlags.ShrinkCenter;
		save.Visible = _kind == "";
		save.Pressed += () => SeasonPoster.Ask(this, AwardPack(csvs, from, until, beta ? Lang.T("Beta (before 1.0)") : season.Id != null ? season.Label : Lang.T("Ewige Tabelle · all time")));
		// (dev runs: --award-pack opens the window once, for screenshots)
		if (!_devAwardPack && save.Visible && OS.GetCmdlineUserArgs().Contains("--award-pack"))
		{
			_devAwardPack = true;
			Callable.From(() => save.EmitSignal(BaseButton.SignalName.Pressed)).CallDeferred();
		}
		var page = U.V(14, TopBar("table"), U.H(12, U.Heading(title, 34).Grow(), save), pickers, U.Muted(intro, 15));

		var lines = Lines(csvs, from, until);
		var cols = _kind == "" ? OverallColumns() : KindStats.ColumnsFor(_kind, allRows.Where(r => KindStats.Matches(_kind, r)).ToList());
		if (lines.Count == 0)
		{
			var (head, text) = _kind != ""
				? ("No questions of this kind yet", "Stats per kind of question are recorded from 0.10.0b on: they fill up with the next quiz nights.")
				: beta ? ("No quiz nights yet", "After each quiz night the results go to Max. With the next update they show up here.")
				: season.Id != null ? ("No quiz nights this season yet", "The season fills up with the quiz nights played in it.")
				: ("No quiz nights yet", "The Ewige Tabelle starts with version 1.0. Until then, the nights count for the beta table.");
			page.AddChild(U.Card(8,
				U.Heading(head, 22).With(l => l.HorizontalAlignment = HorizontalAlignment.Center),
				U.Muted(text, 15, true, HorizontalAlignment.Center)));
			AddChild(U.Page(page, 1180, 20));
			return;
		}
		if (_sort == "" || _sort != "player" && cols.All(c => c.Id != _sort)) (_sort, _descending) = (MainColumn, true);

		// (the table before the newest night, for the arrows: who moved up or down with it)
		var latest = Results.LatestDate(csvs.Select(c => c));
		var before = latest != null && (until == null || string.CompareOrdinal(latest, until) < 0) ? Lines(csvs, from, latest) : new();
		if (_graph != "table")
		{
			page.AddChild(Graph(csvs, from, until, lines));
			AddChild(U.Page(page, 1180, 20));
			return;
		}
		var places = Places(lines, cols);
		var placesBefore = before.Count > 0 ? Places(before, cols) : null;

		var grid = new LinedGrid { Columns = cols.Count + 2 };
		grid.AddThemeConstantOverride("h_separation", cols.Count > 10 ? 16 : 22);
		grid.AddThemeConstantOverride("v_separation", 12);
		grid.AddChild(U.Eyebrow("#"));
		foreach (var (id, label, tip) in cols.Select(c => (c.Id, c.Label, c.Tip)).Prepend(("player", "Player", "")))
		{
			var col = id;
			var head = U.Link(Lang.T(label) + (_sort == id ? (_descending ? " ▼" : " ▲") : ""), () =>
			{
				if (_sort == col) _descending = !_descending;
				else
				{
					_sort = col;
					_descending = cols.FirstOrDefault(c => c.Id == col)?.HigherIsBetter ?? false;
				}
				App.I.GoEternalTable();
			}, 12);
			head.TooltipText = tip != "" ? Lang.T(tip) + "\n" + Lang.T("Sort by this") : Lang.T("Sort by this");
			head.AddThemeColorOverride("font_color", _sort == id ? C.Accent : C.Muted);
			// (no link padding, and the number columns centred like their numbers, so each heading sits over its column)
			foreach (var state in new[] { "normal", "hover", "pressed", "focus" }) head.AddThemeStyleboxOverride(state, new StyleBoxEmpty());
			head.Alignment = id == "player" ? HorizontalAlignment.Left : HorizontalAlignment.Center;
			head.AutowrapMode = TextServer.AutowrapMode.Off;
			grid.AddChild(head);
		}
		static Label Num(string text, int size, Color? color = null) =>
			U.Mono(text, size, color).With(l => l.HorizontalAlignment = HorizontalAlignment.Center);
		var main = MainColumn;
		var puppy = _kind == "" ? Results.FavouritePuppy(Results.Table(csvs, until, from)) : null;
		foreach (var line in Sorted(lines))
		{
			var place = places[line.Id];
			grid.AddChild(GameParts.Row(6, GameParts.PlaceBox(place), Move(place, placesBefore, line.Id)));
			var who = GameParts.Row(8, new Avatar(Who(line.Id, line.Name), 26), NameLink(line.Id, line.Name, 16, place == 1 ? C.Accent : C.Text)).Grow();
			if (line.Id == puppy) who.AddChild(ProfileScreen.PuppyBadge(11));
			grid.AddChild(who);
			foreach (var c in cols)
			{
				var text = line.V.GetValueOrDefault(c.Id) is { } v ? KindStats.Format(c.Format, v) : "–";
				// (the main number last and bright, quiz points beside championship points a little quieter)
				grid.AddChild(c.Id == main ? Num(text, 16, C.Accent) : c.Id == "points" ? Num(text, 15) : Num(text, 15, C.Muted));
			}
		}
		page.AddChild(U.Card(0, grid));
		AddChild(U.Page(page, 1180, 20));
	}
}
