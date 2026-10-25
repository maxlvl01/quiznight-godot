// Closeness questions (estimation, map pin): how they score, and the unit of an estimation.
// ScoringField: a drop-down of the ways to score (closest wins, top 3, F1 and other presets, your own) with an edit
// button that opens the ScoringEditor: points per place, saved as your own presets on this PC.

using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using QuizNight.Shared;
using QuizNight.Storage;

namespace QuizNight.Ui.Screens;

public static class ScoringField
{
	const string ClosestId = "classic:" + Scoring.ClosestWins;
	const string Top3Id = "classic:" + Scoring.Top3;

	/// <summary>The scoring drop-down and its edit button. pointsField shows only for the two classic ways (they use the question's points).</summary>
	public static Control Make(Question q, Action changed, Control pointsField)
	{
		var select = new OptionButton { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		var edit = U.Btn("Edit", "SmallButton", null, 15).Tip("Points per place, and your own presets (like F1's 25 / 18 / 15 …)");
		var hint = U.Muted("", 12);
		void Fill()
		{
			select.Clear();
			var mine = Settings.ScoringPresets;
			void Add(string label, string id)
			{
				select.AddItem(label);
				select.SetItemMetadata(select.ItemCount - 1, id);
			}
			Add(Lang.T("Closest wins the round"), ClosestId);
			Add(Lang.T("Top 3 score (3 / 2 / 1)"), Top3Id);
			select.AddSeparator(Lang.T("Points per place"));
			foreach (var p in ScoringPresets.BuiltIn) Add(Lang.T(p.Name), "preset:" + p.Name);
			if (mine.Count > 0)
			{
				select.AddSeparator(Lang.T("My presets"));
				foreach (var p in mine) Add(p.Name, "mine:" + p.Name);
			}
			// (points that match no preset: shown as they are)
			var current = q.HasRankPoints ? ScoringPresets.Find(q.RankPoints, mine) : null;
			if (q.HasRankPoints && current == null) Add(Lang.F("Custom: {0}", ScoringPresets.Describe(q.RankPoints)), "custom");
			var selectedId = !q.HasRankPoints ? (q.Scoring == Scoring.Top3 ? Top3Id : ClosestId)
				: current == null ? "custom" : (current.BuiltIn ? "preset:" : "mine:") + current.Name;
			for (var i = 0; i < select.ItemCount; i++)
				if (!select.IsItemSeparator(i) && select.GetItemMetadata(i).AsString() == selectedId) select.Selected = i;
			pointsField.Visible = !q.HasRankPoints;
			hint.Text = q.HasRankPoints ? Lang.F("1st gets {0}, 2nd {1} … ({2} places score)", q.RankPoints[0], q.RankPoints.Count > 1 ? q.RankPoints[1] : 0, q.RankPoints.Count) : "";
			hint.Visible = q.HasRankPoints;
		}
		select.ItemSelected += i =>
		{
			var id = select.GetItemMetadata((int)i).AsString();
			if (id == "custom") return;
			if (id.StartsWith("classic:"))
			{
				q.Scoring = id[8..];
				q.RankPoints.Clear();
			}
			else
			{
				var name = id[(id.IndexOf(':') + 1)..];
				var preset = id.StartsWith("preset:") ? ScoringPresets.BuiltIn.First(p => p.Name == name) : Settings.ScoringPresets.First(p => p.Name == name);
				q.RankPoints = preset.Points.ToList();
			}
			changed();
			Fill();
		};
		edit.Pressed += () => ScoringEditor.Open(edit, q.HasRankPoints ? q.RankPoints : null, points =>
		{
			q.RankPoints = points.ToList();
			changed();
			Fill();
		}, Fill);
		Fill();
		return U.Field("Scoring", U.V(4, GameParts.Row(8, select, edit), hint));
	}
}

/// <summary>Points per place for one question: start from a preset, change any place, save it as your own preset.</summary>
public static class ScoringEditor
{
	public static void Open(Node owner, IReadOnlyList<int>? current, Action<IReadOnlyList<int>> use, Action presetsChanged)
	{
		var points = (current ?? ScoringPresets.BuiltIn[0].Points).ToList();
		var d = new ConfirmationDialog { Title = Lang.T("Scoring"), OkButtonText = Lang.T("Use for this question"), CancelButtonText = Lang.T("Cancel") };
		d.MinSize = new Vector2I(640, 0);
		var places = new GridContainer { Columns = 5 };
		places.AddThemeConstantOverride("h_separation", 10);
		places.AddThemeConstantOverride("v_separation", 8);
		var name = U.Input("", "Name, e.g. Our league", null, 40);
		var status = U.Muted("", 12, false);
		var delete = U.Btn("Delete preset", "SmallButton", null, 14);
		var startFrom = new OptionButton { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };

		void Places()
		{
			places.Clear();
			for (var i = 0; i < points.Count; i++)
			{
				var n = i;
				places.AddChild(U.V(2, U.Muted(Lang.Ordinal(i + 1), 12, false),
					new NumberField(points[i], v =>
					{
						points[n] = (int)Math.Clamp(Math.Round(v ?? 0), 0, ScoringPresets.PointsMax);
					}, 0, ScoringPresets.PointsMax, true).MinSize(90, 0)));
			}
		}
		void Presets()
		{
			startFrom.Clear();
			startFrom.AddItem(Lang.T("Start from a preset…"));
			foreach (var p in ScoringPresets.BuiltIn) startFrom.AddItem(Lang.T(p.Name));
			var mine = Settings.ScoringPresets;
			if (mine.Count > 0) startFrom.AddSeparator(Lang.T("My presets"));
			foreach (var p in mine) startFrom.AddItem(p.Name);
			startFrom.Selected = 0;
			delete.Visible = mine.Any(p => p.Name == name.Text.Trim());
		}
		startFrom.ItemSelected += i =>
		{
			var label = startFrom.GetItemText((int)i);
			var preset = ScoringPresets.BuiltIn.FirstOrDefault(p => Lang.T(p.Name) == label) ?? Settings.ScoringPresets.FirstOrDefault(p => p.Name == label);
			if (preset == null) return;
			points = preset.Points.ToList();
			if (!preset.BuiltIn) name.Text = preset.Name;
			Places();
			Presets();
		};
		name.TextChanged += _ => delete.Visible = Settings.ScoringPresets.Any(p => p.Name == name.Text.Trim());
		var add = U.Btn("+ Place", "SmallButton", () =>
		{
			if (points.Count >= ScoringPresets.PlacesMax) return;
			points.Add(points.Count > 0 ? Math.Max(0, points[^1] - 1) : 1);
			Places();
		}, 14);
		var remove = U.Btn("− Place", "SmallButton", () =>
		{
			if (points.Count <= 1) return;
			points.RemoveAt(points.Count - 1);
			Places();
		}, 14);
		var save = U.Btn("Save as my preset", "AccentButton", () =>
		{
			var n = name.Text.Trim();
			if (n == "")
			{
				status.Text = Lang.T("Give it a name first.");
				return;
			}
			var mine = Settings.ScoringPresets.Where(p => p.Name != n).ToList();
			mine.Add(new ScoringPreset(n, points.ToArray(), false));
			Settings.ScoringPresets = mine;
			status.Text = Lang.F("Saved \"{0}\". It's in the list for every question now.", n);
			Presets();
			presetsChanged();
		}, 14);
		delete.Pressed += () =>
		{
			var n = name.Text.Trim();
			Settings.ScoringPresets = Settings.ScoringPresets.Where(p => p.Name != n).ToList();
			status.Text = Lang.F("Deleted \"{0}\".", n);
			Presets();
			presetsChanged();
		};
		var scroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled, CustomMinimumSize = new Vector2(0, 150), SizeFlagsVertical = Control.SizeFlags.ExpandFill };
		scroll.AddChild(places.Grow());
		d.AddChild(U.V(12,
			startFrom,
			// (a wrapping label needs a width, or the dialog sizes itself as if every word were its own line)
			U.Muted("Points for each place, closest first. Places after the last one get nothing. Tied players share the better place; Double points doubles it.", 13).MinSize(620, 0),
			scroll,
			GameParts.Row(8, add, remove),
			U.Field("Save as your own preset (on this PC)", GameParts.Row(8, name.Grow(), save, delete)),
			status));
		Places();
		Presets();
		d.Confirmed += () => use(points.Count > 0 ? points : new List<int> { 1 });
		d.Confirmed += d.QueueFree;
		d.Canceled += d.QueueFree;
		owner.AddChild(d);
		// (a set size: the dialog would otherwise grow with the window and push its buttons off screen)
		d.PopupCentered(new Vector2I(680, 560));
	}
}

/// <summary>An estimation's unit: a drop-down of common units by kind, or your own.</summary>
public static class UnitField
{
	public static Control Make(string unit, Action<string> changed)
	{
		var select = new OptionButton { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		var custom = U.Input(unit, "Your unit, e.g. goals", t => changed(t.Trim()), 20);
		select.AddItem(Lang.T("No unit"));
		select.SetItemMetadata(0, "");
		foreach (var (group, units) in Units.Groups)
		{
			select.AddSeparator(Lang.T(group));
			foreach (var u in units)
			{
				select.AddItem(Units.Text(u));
				select.SetItemMetadata(select.ItemCount - 1, u);
			}
		}
		select.AddSeparator();
		select.AddItem(Lang.T("Custom…"));
		select.SetItemMetadata(select.ItemCount - 1, "custom");
		// (a unit not in the list, e.g. typed in an older version, is a custom one)
		var found = Units.Find(unit);
		var isCustom = unit != "" && found == null;
		for (var i = 0; i < select.ItemCount; i++)
		{
			if (select.IsItemSeparator(i)) continue;
			var id = select.GetItemMetadata(i).AsString();
			if (isCustom ? id == "custom" : id == (found ?? "")) select.Selected = i;
		}
		custom.Visible = isCustom;
		select.ItemSelected += i =>
		{
			var id = select.GetItemMetadata((int)i).AsString();
			custom.Visible = id == "custom";
			if (id == "custom")
			{
				custom.GrabFocus();
				changed(custom.Text.Trim());
			}
			else changed(Units.Text(id));
		};
		return U.V(6, select, custom);
	}
}
