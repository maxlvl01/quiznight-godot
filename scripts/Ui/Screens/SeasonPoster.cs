// The Ewige Tabelle's "Save as picture": a 1920×1080 poster of the standings to share at the end of a season, with the
// champion in the spotlight, the podium below them and the full table beside it. Drawn off-screen (a SubViewport),
// saved as a PNG in Documents/Quiznight/Pictures and opened.

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

	public static string Folder => Path.Combine(OS.GetSystemDir(OS.SystemDir.Documents), "Quiznight", "Pictures");

	static readonly Vector2I Size = new(1920, 1080);

	/// <summary>Draws the poster, saves it and opens it; returns the file's path (null if it couldn't be saved).</summary>
	/// <param name="to">Dev runs: save it here and don't open it.</param>
	public static async System.Threading.Tasks.Task<string?> Save(Control owner, string title, string ranking, string pointsLabel,
		List<Standing> standings, string footer, string? to = null)
	{
		var viewport = new SubViewport { Size = Size, TransparentBg = false, RenderTargetUpdateMode = SubViewport.UpdateMode.Always };
		var poster = Build(title, ranking, pointsLabel, standings, footer);
		poster.Theme = App.I.Theme;
		viewport.AddChild(poster);
		owner.AddChild(viewport);
		// (a moment for the layout and the Steam pictures)
		await owner.ToSignal(owner.GetTree().CreateTimer(0.8), SceneTreeTimer.SignalName.Timeout);
		await owner.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
		var image = viewport.GetTexture().GetImage();
		viewport.QueueFree();
		try
		{
			var safe = new string(title.Select(c => char.IsLetterOrDigit(c) ? c : '-').ToArray()).Trim('-');
			var path = to ?? Path.Combine(Folder, $"{DateTime.Now:yyyy-MM-dd_HH-mm} {safe}.png");
			Directory.CreateDirectory(Path.GetDirectoryName(path)!);
			if (image.SavePng(path) != Error.Ok) return null;
			if (to == null) OS.ShellOpen(path);
			return path;
		}
		catch (Exception e) when (e is IOException or UnauthorizedAccessException)
		{
			GD.PrintErr($"Couldn't save the picture: {e.Message}");
			return null;
		}
	}

	static Control Build(string title, string ranking, string pointsLabel, List<Standing> standings, string footer)
	{
		var root = new Control { Size = Size, CustomMinimumSize = Size };
		var bg = new TextureRect
		{
			Texture = new GradientTexture2D
			{
				Width = 64, Height = 64, FillFrom = new Vector2(0, 0), FillTo = new Vector2(1, 1),
				Gradient = new Gradient { Colors = new[] { new Color("#1a0b2e"), new Color("#0d0618"), new Color("#2a1035") }, Offsets = new[] { 0f, 0.55f, 1f } },
			},
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.Scale, Size = Size,
		};
		root.AddChild(bg);

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
