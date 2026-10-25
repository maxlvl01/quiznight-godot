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
			// (folded: everyone's place and points in a line; open: the full table with their stats; you in bold)
			var players = U.Flow(14);
			foreach (var p in night.Players)
				players.AddChild(GameParts.Row(6,
					GameParts.RankBadge(p.Place, true),
					U.Text(p.Name, 15, C.Text, p.Id == me ? Fonts.Black : null).Raw(),
					U.Mono(Lang.F("{0} pts", p.Points), 13, C.Muted)));
			var table = Table(night, me);
			table.Visible = false;
			var hint = U.Link("Click for the details", null, 12).With(l => l.SizeFlagsHorizontal = SizeFlags.ShrinkBegin);
			var card = U.Card(8, head, players, table, hint);
			card.MouseDefaultCursorShape = CursorShape.PointingHand;
			void Toggle()
			{
				table.Visible = !table.Visible;
				players.Visible = !table.Visible;
				hint.Text = Lang.T(table.Visible ? "Click to fold it up" : "Click for the details");
			}
			hint.Pressed += Toggle;
			card.GuiInput += e =>
			{
				if (e is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left }) Toggle();
			};
			// (clicks on anything inside reach the card)
			foreach (var c in AllControls(card)) if (c != card && c != hint) c.MouseFilter = MouseFilterEnum.Pass;
			page.AddChild(card);
		}
		AddChild(U.Page(page, 980, 20));
	}

	/// <summary>The night in full: place, picture, name, points, questions scored, Ø answer time, Ø map distance, Ø % off.</summary>
	static Control Table(Results.Night night, string? me)
	{
		var grid = new GridContainer { Columns = 7 };
		grid.AddThemeConstantOverride("h_separation", 18);
		grid.AddThemeConstantOverride("v_separation", 6);
		foreach (var h in new[] { "#", "Player", "Points", "Scored", "Ø answer time", "Ø map off", "Ø % off" }) grid.AddChild(U.Eyebrow(h));
		foreach (var p in night.Players)
		{
			var mine = p.Id == me;
			var who = new PublicPlayer { Id = p.Id, Name = p.Name, Connected = true, Color = Limits.PlayerColors[p.Id.Aggregate(0, (h, ch) => (h * 31 + ch) & 0xffffff) % Limits.PlayerColors.Length] };
			grid.AddChild(GameParts.RankBadge(p.Place, true));
			grid.AddChild(GameParts.Row(8, new Avatar(who, 24), U.Text(p.Name, 15, C.Text, mine ? Fonts.Black : null).Raw()));
			grid.AddChild(U.Mono($"{p.Points}", 15, C.Accent));
			grid.AddChild(U.Mono($"{p.Scored}", 14));
			grid.AddChild(U.Mono(p.AvgAnswerS is { } s ? s.ToString("0.00", Inv) + " s" : "–", 14, C.Muted));
			grid.AddChild(U.Mono(p.AvgKm is { } km ? NightStatsView.Distance(km) : "–", 14, C.Muted));
			grid.AddChild(U.Mono(p.AvgPercentOff is { } pct ? NightStatsView.Percent(pct) : "–", 14, C.Muted));
		}
		return U.Pad(grid, 0, 6, 0, 0);
	}

	static System.Collections.Generic.IEnumerable<Control> AllControls(Node n)
	{
		foreach (var child in n.GetChildren())
		{
			if (child is Control c) yield return c;
			foreach (var d in AllControls(child)) yield return d;
		}
	}

	/// <summary>"2026-10-24 21:25" → "24.10.2026 21:25" (the date style used everywhere in the game).</summary>
	static string Date(string date) =>
		System.DateTime.TryParseExact(date, "yyyy-MM-dd HH:mm", Inv, DateTimeStyles.None, out var d) ? d.ToString("dd.MM.yyyy HH:mm", Inv) : date;
}
