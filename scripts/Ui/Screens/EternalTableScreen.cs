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

	/// <summary>The beta table is shown (nights before 1.0); until 1.0 has nights of its own, that's the one that opens.</summary>
	static bool? _beta;

	public EternalTableScreen()
	{
		SetAnchorsPreset(LayoutPreset.FullRect);
		var beta = _beta ?? Results.BundledCsvs().Count == 0;
		var table = Results.Table(Results.BundledCsvs(beta));
		var switcher = U.Segmented(new[] { ("main", "Ewige Tabelle"), ("beta", "Beta table") }, beta ? "beta" : "main", id =>
		{
			_beta = id == "beta";
			App.I.GoEternalTable();
		});
		switcher.SizeFlagsHorizontal = SizeFlags.ShrinkEnd;
		var page = U.V(14,
			U.Link("← Home", () => App.I.GoHome()).With(b => b.SizeFlagsHorizontal = SizeFlags.ShrinkBegin),
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
		var grid = new GridContainer { Columns = 9 };
		grid.AddThemeConstantOverride("h_separation", 18);
		grid.AddThemeConstantOverride("v_separation", 8);
		foreach (var h in new[] { "#", "Player", "Nights", "Wins", "Podiums", "Points", "Ø points", "Ø answer time", "Ø km off" })
			grid.AddChild(U.Eyebrow(h));
		for (var i = 0; i < table.Count; i++)
		{
			var c = table[i];
			grid.AddChild(GameParts.RankBadge(i + 1, true));
			grid.AddChild(U.Text(c.Name, 16, i == 0 ? C.Accent : C.Text, i < 3 ? Fonts.Bold : null).Raw().Grow());
			grid.AddChild(U.Mono($"{c.Nights}", 15));
			grid.AddChild(U.Mono($"{c.Wins}", 15));
			grid.AddChild(U.Mono($"{c.Podiums}", 15));
			grid.AddChild(U.Mono($"{c.Points}", 16, C.Accent));
			grid.AddChild(U.Mono(c.AvgPoints.ToString("0.0", Inv), 15, C.Muted));
			grid.AddChild(U.Mono(c.AvgAnswerS is { } s ? s.ToString("0.000", Inv) + " s" : "–", 15, C.Muted));
			grid.AddChild(U.Mono(c.AvgKm is { } km ? km.ToString("0.0", Inv) + " km" : "–", 15, C.Muted));
		}
		page.AddChild(U.Card(0, grid));
		AddChild(U.Page(page, 980, 20));
	}
}
