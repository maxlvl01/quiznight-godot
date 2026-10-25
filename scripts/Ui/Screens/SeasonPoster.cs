// The Ewige Tabelle's "Save award pack" (a window explains it with examples, then Generate): a ZIP of pictures to share at the end of a season. A 1920×1080 poster of the
// standings (the champion in the spotlight, the podium below them, the full table beside it), and a 1080×1080 card per
// award: the best at each question type and category, the Host's Favourite Puppy, the fastest buzzer, the best speller.
// Drawn off-screen (a SubViewport), saved in Documents/Quiznight/Pictures and shown in the file manager.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Godot;
using QuizNight.Shared;

namespace QuizNight.Ui.Screens;

public static class SeasonPoster
{
	public sealed record Standing(string Id, string Name, int Points, int Wins, int Nights);

	/// <summary>An award: its file name, title, what it's for, and the top three (id, name, their number as text).</summary>
	public sealed record Award(string File, string Title, string For, List<(string Id, string Name, string Value)> Top);

	public static string Folder => Path.Combine(OS.GetSystemDir(OS.SystemDir.Documents), "Quiznight", "Pictures");

	static readonly Vector2I Size = new(1920, 1080);

	static readonly Vector2I CardSize = new(1080, 1080);

	/// <summary>A control drawn off-screen at this size, as an image.</summary>
	static async System.Threading.Tasks.Task<Image> Render(Control owner, Control content, Vector2I size)
	{
		var viewport = new SubViewport { Size = size, TransparentBg = false, RenderTargetUpdateMode = SubViewport.UpdateMode.Always };
		content.Theme = App.I.Theme;
		viewport.AddChild(content);
		owner.AddChild(viewport);
		// (a moment for the layout and the Steam pictures)
		await owner.ToSignal(owner.GetTree().CreateTimer(0.6), SceneTreeTimer.SignalName.Timeout);
		await owner.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
		var image = viewport.GetTexture().GetImage();
		viewport.QueueFree();
		return image;
	}

	static string Safe(string s) => new string(s.Select(c => char.IsLetterOrDigit(c) ? c : '-').ToArray()).Trim('-');

	/// <summary>Everything an award pack shows: the standings poster's lines and the awards.</summary>
	public sealed record Pack(string Title, string Ranking, string PointsLabel, List<Standing> Standings, string Footer, List<Award> Awards);

	/// <summary>
	/// "Save award pack": a window that says what the pack is, with the real standings poster and first award card as
	/// small examples, and a Generate button that makes it. (No tooltip: it was too long to read in one.)
	/// </summary>
	public static void Ask(Control owner, Pack pack)
	{
		var d = new ConfirmationDialog { Title = Lang.T("Award pack"), OkButtonText = Lang.T("Generate"), CancelButtonText = Lang.T("Cancel") };
		d.MinSize = new Vector2I(760, 0);
		var poster = new TextureRect { CustomMinimumSize = new Vector2(416, 234), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered };
		var card = new TextureRect { CustomMinimumSize = new Vector2(234, 234), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered };
		var drawing = U.Muted("Drawing the examples…", 13);
		Control Example(TextureRect picture, string caption) =>
			U.V(6, U.Panel(picture, "Clear").With(p => p.AddThemeStyleboxOverride("panel", UiTheme.Box(C.Bg, 8, C.Border, 2, 4, 4))), U.Muted(caption, 12));
		var names = string.Join(", ", pack.Awards.Select(a => a.Title));
		d.AddChild(U.V(12,
			U.Text("Pictures to share after a season: one ZIP you can post in Discord or WhatsApp.", 15, C.Text, Fonts.Bold, true),
			U.Muted(pack.Awards.Count > 0
				? Lang.F("{0} pictures: the standings (1920 × 1080) with the champion in the spotlight, the podium and the full table, and a square card (1080 × 1080) for every award: {1}.", pack.Awards.Count + 1, names)
				: Lang.T("The standings (1920 × 1080) with the champion in the spotlight, the podium and the full table. Award cards for the best at every question type and category come with nights that have per-question results (recorded from 0.10.0b on)."), 13),
			pack.Awards.Count > 0 ? U.H(12, Example(poster, "Standings"), Example(card, Lang.F("Award: {0}", pack.Awards[0].Title))) : Example(poster, "Standings"),
			drawing,
			U.Muted(Lang.F("Saved in {0}, and the folder opens.", "Documents/Quiznight/Pictures"), 12)).With(v => v.CustomMinimumSize = new Vector2(720, 0)));
		d.Confirmed += async () =>
		{
			d.QueueFree();
			var wait = Dialogs.Wait(owner, "Drawing the award pack…");
			var zip = await Save(owner, pack);
			Dialogs.Close(wait);
			if (zip == null) Dialogs.Info(owner, "The award pack couldn't be saved. Is Documents/Quiznight/Pictures read-only?", "Couldn't save");
		};
		d.Canceled += d.QueueFree;
		owner.AddChild(d);
		d.PopupCentered();
		// (wrapped text first measures as if it were one letter wide: the window shrinks to fit once it's laid out)
		Callable.From(() =>
		{
			d.ResetSize();
			d.MoveToCenter();
		}).CallDeferred();
		_ = ShowExamples();

		async System.Threading.Tasks.Task ShowExamples()
		{
			var (posterImage, cardImage) = await Examples(owner, pack);
			if (!GodotObject.IsInstanceValid(d)) return;
			if (posterImage != null) poster.Texture = ImageTexture.CreateFromImage(posterImage);
			if (cardImage != null) card.Texture = ImageTexture.CreateFromImage(cardImage);
			drawing.Visible = false;
		}
	}

	/// <summary>The real poster and first award card, shrunk (none in headless runs, which can't draw).</summary>
	static async System.Threading.Tasks.Task<(Image? Poster, Image? Card)> Examples(Control owner, Pack pack)
	{
		if (DisplayServer.GetName() == "headless") return (null, null);
		var poster = await Render(owner, Build(pack.Title, pack.Ranking, pack.PointsLabel, pack.Standings, pack.Footer), Size);
		poster.Resize(Size.X / 3, Size.Y / 3, Image.Interpolation.Lanczos);
		Image? card = null;
		if (pack.Awards.Count > 0)
		{
			card = await Render(owner, Card(pack.Title, pack.Awards[0]), CardSize);
			card.Resize(CardSize.X / 3, CardSize.Y / 3, Image.Interpolation.Lanczos);
		}
		return (poster, card);
	}

	/// <summary>
	/// Draws the poster and every award card, zips them and shows the ZIP in the file manager; returns its path (null if
	/// it couldn't be saved).
	/// </summary>
	/// <param name="to">Dev runs: write the pictures into this folder (and the ZIP next to them), open nothing.</param>
	public static async System.Threading.Tasks.Task<string?> Save(Control owner, Pack pack, string? to = null)
	{
		var (title, awards) = (pack.Title, pack.Awards);
		var pictures = new List<(string Name, byte[] Png)>
		{
			("00 Standings.png", (await Render(owner, Build(title, pack.Ranking, pack.PointsLabel, pack.Standings, pack.Footer), Size)).SavePngToBuffer()),
		};
		for (var i = 0; i < awards.Count; i++)
			pictures.Add(($"{i + 1:00} {Safe(awards[i].File)}.png", (await Render(owner, Card(title, awards[i]), CardSize)).SavePngToBuffer()));
		try
		{
			var folder = to ?? Folder;
			Directory.CreateDirectory(folder);
			var zip = Path.Combine(folder, $"{DateTime.Now:yyyy-MM-dd_HH-mm} {Safe(title)} awards.zip");
			using (var archive = System.IO.Compression.ZipFile.Open(zip, System.IO.Compression.ZipArchiveMode.Create))
				foreach (var (name, png) in pictures)
				{
					using var stream = archive.CreateEntry(name).Open();
					stream.Write(png);
				}
			if (to != null) foreach (var (name, png) in pictures) File.WriteAllBytes(Path.Combine(to, name), png);
			else OS.ShellShowInFileManager(zip);
			return zip;
		}
		catch (Exception e) when (e is IOException or UnauthorizedAccessException)
		{
			GD.PrintErr($"Couldn't save the award pack: {e.Message}");
			return null;
		}
	}

	static TextureRect Background(Vector2I size, Color glow) => new()
	{
		Texture = new GradientTexture2D
		{
			Width = 64, Height = 64, FillFrom = new Vector2(0.5f, 0.3f), FillTo = new Vector2(1.1f, 1.1f), Fill = GradientTexture2D.FillEnum.Radial,
			Gradient = new Gradient { Colors = new[] { glow.Darkened(0.55f), new Color("#120822"), new Color("#0a0512") }, Offsets = new[] { 0f, 0.6f, 1f } },
		},
		ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.Scale, Size = size,
	};

	/// <summary>One award, square: its name, the winner big with their number, and who came 2nd and 3rd.</summary>
	static Control Card(string season, Award award)
	{
		var root = new Control { Size = CardSize, CustomMinimumSize = CardSize };
		root.AddChild(Background(CardSize, C.Gold));
		Label Line(string text, int size, Color color, Font? font = null) =>
			U.Text(text, size, color, font, true).Raw().With(l => l.HorizontalAlignment = HorizontalAlignment.Center);
		var box = U.V(14,
			Line("QUIZNIGHT? · " + season, 24, C.Accent, Fonts.Spaced),
			U.Gap(10),
			Line(award.Title, 60, C.Gold, Fonts.Black),
			Line(award.For, 26, C.Muted));
		if (award.Top.Count > 0)
		{
			var (id, name, value) = award.Top[0];
			box.AddChild(U.Gap(16));
			box.AddChild(new Avatar(EternalTableScreen.Who(id, name), 260).With(a => a.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter));
			box.AddChild(Line(name, 64, C.Text, Fonts.Black));
			box.AddChild(Line(value, 32, C.Gold, Fonts.Mono));
			var rest = U.H(60);
			rest.Alignment = BoxContainer.AlignmentMode.Center;
			foreach (var (r, i) in award.Top.Skip(1).Select((r, i) => (r, i + 2)))
				rest.AddChild(U.V(6, new Avatar(EternalTableScreen.Who(r.Id, r.Name), 64).With(a => a.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter),
					Line($"{Lang.Ordinal(i)}  {r.Name}", 24, C.Medal(i) ?? C.Text, Fonts.Bold), Line(r.Value, 20, C.Muted, Fonts.Mono)).MinSize(380));
			box.AddChild(U.Gap(20));
			box.AddChild(rest);
		}
		var margin = U.Pad(box, 70, 70, 70, 60);
		margin.Size = CardSize;
		root.AddChild(margin);
		return root;
	}

	static Control Build(string title, string ranking, string pointsLabel, List<Standing> standings, string footer)
	{
		var root = new Control { Size = Size, CustomMinimumSize = Size };
		root.AddChild(Background(Size, C.Purple));

		var champ = standings.FirstOrDefault();
		Control Centered(Control c)
		{
			c.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
			return c;
		}
		Label Line(string text, int size, Color color, Font? font = null) =>
			U.Text(text, size, color, font).Raw().With(l => l.HorizontalAlignment = HorizontalAlignment.Center);

		// (left: the champion in the spotlight, the podium under them)
		var left = U.V(10);
		if (champ != null)
		{
			left.AddChild(Centered(U.Text(Lang.T("Champion").ToUpper(), 26, C.Gold, Fonts.Spaced)));
			left.AddChild(Centered(new Avatar(EternalTableScreen.Who(champ.Id, champ.Name), 170)));
			left.AddChild(Line(champ.Name, 76, C.Gold, Fonts.Black));
			left.AddChild(Line(Lang.F("{0} {1} · {2} wins · {3} nights", champ.Points, Lang.T(pointsLabel), champ.Wins, champ.Nights), 26, C.Text));
		}
		left.AddChild(U.Gap(18));
		var podium = U.H(18);
		podium.Alignment = BoxContainer.AlignmentMode.Center;
		foreach (var place in new[] { 2, 1, 3 })
		{
			if (standings.Count < place) continue;
			var s = standings[place - 1];
			var color = C.Medal(place)!.Value;
			var block = U.Panel(Line(Lang.Ordinal(place), 40, C.Bg, Fonts.Black));
			block.AddThemeStyleboxOverride("panel", UiTheme.Box(color, 14, null, 0, 10, 10));
			block.CustomMinimumSize = new Vector2(250, place switch { 1 => 170, 2 => 125, _ => 90 });
			var col = U.V(8, Centered(new Avatar(EternalTableScreen.Who(s.Id, s.Name), 84)), Line(s.Name, 28, C.Text, Fonts.Black), Line($"{s.Points}", 26, color, Fonts.Mono), block);
			col.Alignment = BoxContainer.AlignmentMode.End;
			col.SizeFlagsVertical = Control.SizeFlags.ShrinkEnd;
			podium.AddChild(col);
		}
		left.AddChild(podium);
		left.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		left.SizeFlagsStretchRatio = 1.35f;

		// (right: everyone, in order)
		var list = U.V(6, U.Text(Lang.T("Standings").ToUpper(), 22, C.Muted, Fonts.Spaced), U.Gap(6));
		foreach (var (s, i) in standings.Take(13).Select((s, i) => (s, i)))
		{
			var place = U.Mono(Lang.Ordinal(i + 1), 24, C.Medal(i + 1) ?? C.Muted).MinSize(70);
			var row = U.H(14, place, new Avatar(EternalTableScreen.Who(s.Id, s.Name), 40), U.Text(s.Name, 26, i == 0 ? C.Gold : C.Text, i < 3 ? Fonts.Black : null).Raw().Grow(),
				U.Mono(Lang.F("{0} W", s.Wins), 20, C.Muted).MinSize(70), U.Mono($"{s.Points}", 28, i == 0 ? C.Gold : C.Accent).MinSize(80).With(l => l.HorizontalAlignment = HorizontalAlignment.Right));
			row.CustomMinimumSize = new Vector2(0, 52);
			list.AddChild(row);
		}
		if (standings.Count > 13) list.AddChild(U.Muted(Lang.F("… and {0} more", standings.Count - 13), 20));
		var card = U.Panel(U.Pad(list, 28, 24, 28, 24));
		card.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		card.SizeFlagsVertical = Control.SizeFlags.ShrinkBegin;

		var head = U.V(4,
			U.Text("QUIZNIGHT?", 24, C.Accent, Fonts.Spaced),
			U.Text(title, 64, C.Text, Fonts.Black).Raw(),
			U.Text(ranking, 26, C.Muted).Raw());
		var right = U.V(16, card, U.Text(footer, 22, C.Muted).Raw());
		right.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		var page = U.V(30, head, U.H(50, left, right).GrowV());
		var margin = U.Pad(page, 80, 60, 80, 50);
		margin.Size = Size;
		root.AddChild(margin);
		return root;
	}
}
