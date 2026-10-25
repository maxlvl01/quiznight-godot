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
			// (a card per night: a small podium of the top 3's pictures; click it for the night's own screen with the full table)
			var open = U.Link("Details →", () => App.I.GoMatchNight(night), 13).With(l => l.SizeFlagsVertical = SizeFlags.ShrinkEnd);
			var more = night.Players.Count > 3 ? U.Muted(Lang.F("+{0} more", night.Players.Count - 3), 13, false).With(l => l.SizeFlagsVertical = SizeFlags.ShrinkEnd) : U.Gap(0);
			var card = U.Card(8, Head(night, 20), GameParts.Row(14, Podium(night, me), more, U.Fill(), open));
			card.MouseDefaultCursorShape = CursorShape.PointingHand;
			card.GuiInput += e =>
			{
				if (e is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left }) App.I.GoMatchNight(night);
			};
			// (clicks on anything inside reach the card)
			foreach (var c in AllControls(card)) if (c != card && c != open) c.MouseFilter = MouseFilterEnum.Pass;
			page.AddChild(card);
		}
		AddChild(U.Page(page, 980, 20));
	}

	/// <summary>Quiz name, "by host", Beta and the date, in one line (centred on each other).</summary>
	public static Control Head(Results.Night night, int size)
	{
		static T Mid<T>(T l) where T : Label
		{
			l.VerticalAlignment = VerticalAlignment.Center;
			l.SizeFlagsVertical = SizeFlags.ShrinkCenter;
			return l;
		}
		return GameParts.Row(10,
			Mid(U.Bold(night.Quiz == "" ? Lang.T("Quiz") : night.Quiz, size).Raw()),
			night.Host != "" ? Mid(U.Muted(Lang.F("by {0}", night.Host), size - 5, false).Raw()) : U.Gap(0),
			U.Fill(),
			night.Beta ? U.Pill("Beta", C.Muted) : U.Gap(0),
			Mid(U.Mono(Date(night.Date), 14, C.Muted)));
	}

	/// <summary>
	/// The top 3's profile pictures on little podium steps (2nd, 1st, 3rd; gold, silver, bronze), names on hover, you
	/// ringed in your colour's light.
	/// </summary>
	static Control Podium(Results.Night night, string? me)
	{
		var row = U.H(6);
		row.Alignment = BoxContainer.AlignmentMode.Center;
		foreach (var place in new[] { 2, 1, 3 })
		{
			if (night.Players.FirstOrDefault(x => x.Place == place) is not { } p) continue;
			var who = new PublicPlayer { Id = p.Id, Name = p.Name, Connected = true, Color = Limits.PlayerColors[p.Id.Aggregate(0, (h, ch) => (h * 31 + ch) & 0xffffff) % Limits.PlayerColors.Length] };
			var avatar = new Avatar(who, place == 1 ? 40 : 32) { TooltipText = Lang.F("{0}: {1} · {2} pts", Lang.Ordinal(place), p.Name, p.Points) };
			// (steps of clearly different heights: 1st highest, then 2nd, then 3rd)
			var label = U.Text(Lang.Ordinal(place), 11, C.Bg, Fonts.Black, false, HorizontalAlignment.Center).With(l => l.VerticalAlignment = VerticalAlignment.Center);
			var step = U.Panel(label, "Clear")
				.With(x => x.AddThemeStyleboxOverride("panel", UiTheme.Box(C.Medal(place) ?? C.Muted, 5, p.Id == me ? C.Text : null, p.Id == me ? 2 : 0, 2, 0)))
				.MinSize(46, place == 1 ? 38 : place == 2 ? 28 : 19);
			var col = U.V(4, avatar.Center(), step);
			col.Alignment = BoxContainer.AlignmentMode.End;
			col.SizeFlagsVertical = SizeFlags.Fill;
			row.AddChild(col);
		}
		return row;
	}

	/// <summary>
	/// "1st  [LCB] BassGaming  10 pts" as one line of text, so place, name and points share a baseline (three separate
	/// labels in different fonts sat at different heights).
	/// </summary>
	static Control Line(Results.NightPlayer p, bool mine)
	{
		static string Hex(Color c) => c.ToHtml(false);
		var name = p.Name.Replace("[", "[lb]");
		var place = $"[font_size=14][color=#{Hex(C.Medal(p.Place) ?? C.Muted)}][code]{Lang.Ordinal(p.Place)}[/code][/color][/font_size]";
		var points = $"[font_size=13][color=#{Hex(C.Muted)}][code]{Lang.F("{0} pts", p.Points)}[/code][/color][/font_size]";
		var label = new RichTextLabel
		{
			BbcodeEnabled = true, FitContent = true, ScrollActive = false, AutowrapMode = TextServer.AutowrapMode.Off,
			AutoTranslateMode = AutoTranslateModeEnum.Disabled, MouseFilter = MouseFilterEnum.Pass,
			Text = $"{place}  {(mine ? $"[b]{name}[/b]" : name)}  {points}",
		};
		label.AddThemeFontSizeOverride("normal_font_size", 15);
		label.AddThemeFontSizeOverride("bold_font_size", 15);
		label.AddThemeFontOverride("bold_font", Fonts.Black);
		label.AddThemeFontOverride("mono_font", Fonts.Mono);
		label.AddThemeColorOverride("default_color", C.Text);
		return label;
	}

	/// <summary>The night in full: place, picture, name, points, questions scored, Ø answer time, Ø map distance, Ø % off.</summary>
	public static Control Table(Results.Night night, string? me)
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

/// <summary>One quiz night on its own screen: the full table of everyone's stats, and back to the match history.</summary>
public partial class MatchNightScreen : Control
{
	public MatchNightScreen() : this(new Results.Night()) { }

	public MatchNightScreen(Results.Night night)
	{
		SetAnchorsPreset(LayoutPreset.FullRect);
		var page = U.V(14,
			U.Link("← Match history", App.I.GoMatchHistory).With(b => b.SizeFlagsHorizontal = SizeFlags.ShrinkBegin),
			MatchHistoryScreen.Head(night, 30),
			U.Card(10, MatchHistoryScreen.Table(night, MatchHistoryScreen.MyId)),
			U.Muted("Ø map off: how far the pins were on maps with a scale. Ø % off: how far the estimations were, in % of the right number (recorded from 0.9.3b on).", 13));
		AddChild(U.Page(page, 980, 20));
	}
}
