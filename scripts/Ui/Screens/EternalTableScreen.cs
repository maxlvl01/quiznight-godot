using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Godot;
using QuizNight.Shared;
using QuizNight.Storage;

namespace QuizNight.Ui.Screens;

/// <summary>
/// The eternal table: every quiz night whose results came with the game (Results.BundledCsvs), added up per player,
/// like the Bundesliga's "Ewige Tabelle".
/// </summary>
public partial class EternalTableScreen : Control
{
	static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

	static readonly (string Id, string Label)[] Columns =
	{
		("player", "Player"), ("nights", "Nights"), ("wins", "Wins"), ("podiums", "Podiums"), ("points", "Points"),
		("avgPoints", "Ø points"), ("answer", "Ø answer time"), ("km", "Ø km off"), ("pct", "Ø % off"),
	};

	/// <summary>The column the table is sorted by, and which way (kept while the game runs).</summary>
	static string _sort = "points";
	static bool _descending = true;

	/// <summary>Sorts the table by a column (dev runs: --sort).</summary>
	public static void SortBy(string col, bool ascending) => (_sort, _descending) = (col, !ascending);

	/// <summary>More is better for counts and points; less is better for times and distances (and names go A–Z).</summary>
	static bool HigherIsBetter(string col) => col is "nights" or "wins" or "podiums" or "points" or "avgPoints";

	/// <summary>A player's value in the sorted column (null: none yet, e.g. no estimations; names have none).</summary>
	static double? Key(Results.Career c) => _sort switch
	{
		"nights" => c.Nights, "wins" => c.Wins, "podiums" => c.Podiums, "points" => c.Points, "avgPoints" => c.AvgPoints,
		"answer" => c.AvgAnswerS, "km" => c.AvgKm, "pct" => c.AvgPercentOff, _ => null,
	};

	/// <summary>
	/// Everyone's place in the sorted column: the best value is 1st, whichever way the column is turned, and equal values
	/// share a place. By points (and by name) it's the table's own order, where the faster answer time breaks ties.
	/// </summary>
	static Dictionary<string, int?> Places(List<Results.Career> table)
	{
		if (_sort is "points" or "player") return table.Select((c, n) => (c.Id, (int?)(n + 1))).ToDictionary(x => x.Id, x => x.Item2);
		var better = HigherIsBetter(_sort);
		return table.ToDictionary(c => c.Id, c => Key(c) is { } v
			? 1 + table.Count(o => Key(o) is { } w && (better ? w > v : w < v)) : (int?)null);
	}

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

	static List<Results.Career> Sorted(List<Results.Career> table)
	{
		// (players without a value for a column, e.g. no estimations yet, go last either way)
		if (_sort == "player") return (_descending ? table.OrderByDescending(c => c.Name) : table.OrderBy(c => c.Name)).ToList();
		var has = table.Where(c => Key(c) != null);
		var sorted = _descending ? has.OrderByDescending(c => Key(c)) : has.OrderBy(c => Key(c));
		// (OrderBy keeps the table's order for equal values, so ties sit like their places: the faster answer time first)
		return sorted.Concat(table.Where(c => Key(c) == null)).ToList();
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

	/// <summary>The beta table is shown (nights before 1.0); until 1.0 has nights of its own, that's the one that opens.</summary>
	static bool? _beta;

	public EternalTableScreen()
	{
		SetAnchorsPreset(LayoutPreset.FullRect);
		var beta = _beta ?? Results.BundledCsvs().Count == 0;
		var csvs = Results.BundledCsvs(beta);
		var table = Results.Table(csvs);
		// (the table before the newest night, for the arrows: who moved up or down with it)
		var before = Results.LatestDate(csvs) is { } latest ? Results.Table(csvs, latest) : new();
		var switcher = U.Segmented(new[] { ("main", "From 1.0"), ("beta", "Beta") }, beta ? "beta" : "main", id =>
		{
			_beta = id == "beta";
			App.I.GoEternalTable();
		});
		switcher.SizeFlagsHorizontal = SizeFlags.ShrinkEnd;
		var page = U.V(14,
			TopBar("table"),
			U.H(12, U.Heading(beta ? "🏆 Ewige Tabelle · Beta" : "🏆 Ewige Tabelle", 34).Grow(), switcher),
			U.Muted(beta ? "Every quiz night of the beta added up (before 1.0): most points first, then the faster average answer time."
				: "Every quiz night added up: most points first, then the faster average answer time.", 15));
		if (table.Count == 0)
		{
			page.AddChild(U.Card(8,
				U.Heading("No quiz nights yet", 22).With(l => l.HorizontalAlignment = HorizontalAlignment.Center),
				U.Muted(beta ? "After each quiz night the results go to Max. With the next update they show up here."
					: "The Ewige Tabelle starts with version 1.0. Until then, the nights count for the beta table.", 15, true, HorizontalAlignment.Center)));
			AddChild(U.Page(page, 980, 20));
			return;
		}
		// (a heading sorts the table by its column, again for the other way round; # is the place in that column)
		var places = Places(table);
		var placesBefore = before.Count > 0 ? Places(before) : null;
		var sorted = Sorted(table);
		var grid = new LinedGrid { Columns = 10 };
		grid.AddThemeConstantOverride("h_separation", 24);
		grid.AddThemeConstantOverride("v_separation", 12);
		grid.AddChild(U.Eyebrow("#"));
		foreach (var (id, label) in Columns)
		{
			var col = id;
			var head = U.Link(Lang.T(label) + (_sort == id ? (_descending ? " ▼" : " ▲") : ""), () =>
			{
				if (_sort == col) _descending = !_descending;
				else
				{
					_sort = col;
					_descending = HigherIsBetter(col);
				}
				App.I.GoEternalTable();
			}, 12).Tip("Sort by this");
			head.AddThemeColorOverride("font_color", _sort == id ? C.Accent : C.Muted);
			// (no link padding, and the number columns centred like their numbers, so each heading sits over its column)
			foreach (var state in new[] { "normal", "hover", "pressed", "focus" }) head.AddThemeStyleboxOverride(state, new StyleBoxEmpty());
			head.Alignment = id == "player" ? HorizontalAlignment.Left : HorizontalAlignment.Center;
			grid.AddChild(head);
		}
		static Label Num(string text, int size, Color? color = null) =>
			U.Mono(text, size, color).With(l => l.HorizontalAlignment = HorizontalAlignment.Center);
		foreach (var c in sorted)
		{
			var place = places[c.Id];
			grid.AddChild(GameParts.Row(6, GameParts.PlaceBox(place), Move(place, placesBefore, c.Id)));
			// (their Steam picture in front of the name; initials in a colour of their own otherwise)
			var who = new PublicPlayer { Id = c.Id, Name = c.Name, Connected = true, Color = Limits.PlayerColors[(c.Id.Aggregate(0, (h, ch) => (h * 31 + ch) & 0xffffff)) % Limits.PlayerColors.Length] };
			// (you in bold, everyone else plain, so you find yourself at a glance)
			grid.AddChild(GameParts.Row(8, new Avatar(who, 26), U.Text(c.Name, 16, place == 1 ? C.Accent : C.Text, c.Id == MatchHistoryScreen.MyId ? Fonts.Black : null).Raw()).Grow());
			grid.AddChild(Num($"{c.Nights}", 15));
			grid.AddChild(Num($"{c.Wins}", 15));
			grid.AddChild(Num($"{c.Podiums}", 15));
			grid.AddChild(Num($"{c.Points}", 16, C.Accent));
			grid.AddChild(Num(c.AvgPoints.ToString("0.0", Inv), 15, C.Muted));
			grid.AddChild(Num(c.AvgAnswerS is { } s ? s.ToString("0.000", Inv) + " s" : "–", 15, C.Muted));
			grid.AddChild(Num(c.AvgKm is { } km ? NightStatsView.Distance(km) : "–", 15, C.Muted));
			grid.AddChild(Num(c.AvgPercentOff is { } pct ? NightStatsView.Percent(pct) : "–", 15, C.Muted));
		}
		page.AddChild(U.Card(0, grid));
		AddChild(U.Page(page, 980, 20));
	}
}
