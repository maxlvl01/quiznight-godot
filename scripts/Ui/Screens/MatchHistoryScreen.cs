using System.Globalization;
using System.Linq;
using Godot;
using QuizNight.Net;
using QuizNight.Shared;
using QuizNight.Storage;

namespace QuizNight.Ui.Screens;

/// <summary>
/// Every quiz night that came with the game, newest first: the quiz, who hosted, when, and everyone's place and points.
/// Your own name (by Steam ID) is in bold.
/// </summary>
public partial class MatchHistoryScreen : Control
{
	static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

	/// <summary>You, as the results know you ("steam:…"), or null without Steam.</summary>
	public static string? MyId => SteamService.MySteamId is var id and not 0 ? "steam:" + id : null;

	public MatchHistoryScreen()
	{
		SetAnchorsPreset(LayoutPreset.FullRect);
		var nights = Results.Nights(Results.BundledCsvs()).Concat(Results.Nights(Results.BundledCsvs(true), true))
			.OrderByDescending(n => n.Date).ToList();
		var page = U.V(14,
			U.Link("← Home", () => App.I.GoHome()).With(b => b.SizeFlagsHorizontal = SizeFlags.ShrinkBegin),
			U.Heading("Match history", 34),
			U.Muted("Every quiz night so far, newest first.", 15));
		if (nights.Count == 0)
			page.AddChild(U.Card(8, U.Muted("No quiz nights yet. After each one the results go to Max; with the next update they show up here.", 15, true, HorizontalAlignment.Center)));
		var me = MyId;
		foreach (var night in nights)
		{
			var head = GameParts.Row(10,
				U.Bold(night.Quiz == "" ? Lang.T("Quiz") : night.Quiz, 20).Raw(),
				night.Host != "" ? U.Muted(Lang.F("by {0}", night.Host), 15, false).Raw() : U.Gap(0),
				U.Fill(),
				night.Beta ? U.Pill("Beta", C.Muted) : U.Gap(0),
				U.Mono(Date(night.Date), 14, C.Muted));
			// (everyone's place and points, in a row that wraps; you in bold)
			var players = U.Flow(14);
			foreach (var p in night.Players)
			{
				var mine = p.Id == me;
				players.AddChild(GameParts.Row(6,
					GameParts.RankBadge(p.Place, true),
					U.Text(p.Name, 15, C.Text, mine ? Fonts.Black : null).Raw(),
					U.Mono(Lang.F("{0} pts", p.Points), 13, C.Muted)));
			}
			page.AddChild(U.Card(8, head, players));
		}
		AddChild(U.Page(page, 980, 20));
	}

	/// <summary>"2026-10-24 21:25" → "24.10.2026 21:25" (the date style used everywhere in the game).</summary>
	static string Date(string date) =>
		System.DateTime.TryParseExact(date, "yyyy-MM-dd HH:mm", Inv, DateTimeStyles.None, out var d) ? d.ToString("dd.MM.yyyy HH:mm", Inv) : date;
}
