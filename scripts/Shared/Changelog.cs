// Versions (MAJOR.MINOR.HOTFIX with an optional letter: "0.5.0a" = 0.5.0 in development) and the
// changelog, read from CHANGELOG.md (shipped with the game) for the "What's new" screen.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace QuizNight.Shared;

public readonly record struct AppVersion(int Major, int Minor, int Hotfix, string Suffix) : IComparable<AppVersion>
{
	static readonly Regex Pattern = new(@"^v?(\d+)\.(\d+)(?:\.(\d+))?([a-z]*)$", RegexOptions.IgnoreCase);

	public static AppVersion? Parse(string? text)
	{
		var m = Pattern.Match((text ?? "").Trim());
		if (!m.Success) return null;
		return new AppVersion(int.Parse(m.Groups[1].Value), int.Parse(m.Groups[2].Value),
			m.Groups[3].Success && m.Groups[3].Value != "" ? int.Parse(m.Groups[3].Value) : 0, m.Groups[4].Value.ToLowerInvariant());
	}

	/// <summary>Numbers first; for the same numbers a letter ("a", in development) comes before the finished version.</summary>
	public int CompareTo(AppVersion other)
	{
		var c = (Major, Minor, Hotfix).CompareTo((other.Major, other.Minor, other.Hotfix));
		if (c != 0) return c;
		if (Suffix == other.Suffix) return 0;
		if (Suffix == "") return 1;
		if (other.Suffix == "") return -1;
		return string.CompareOrdinal(Suffix, other.Suffix);
	}

	public static bool operator >(AppVersion a, AppVersion b) => a.CompareTo(b) > 0;
	public static bool operator <(AppVersion a, AppVersion b) => a.CompareTo(b) < 0;

	public override string ToString() => $"{Major}.{Minor}.{Hotfix}{Suffix}";
}

public static class Changelog
{
	/// <summary>One "### Added" (Changed, Fixed, Removed …) block: its bullet points.</summary>
	public sealed record Section(string Kind, List<string> Items);

	/// <summary>One "## [0.5.0a] — title" block.</summary>
	public sealed record Release(string VersionText, AppVersion? Version, string Title, List<Section> Sections);

	static readonly Regex ReleaseLine = new(@"^##\s+\[([^\]]+)\]\s*(?:[—–-]\s*(.*))?$");

	public static List<Release> Parse(string markdown)
	{
		var releases = new List<Release>();
		Release? release = null;
		Section? section = null;
		foreach (var raw in markdown.Replace("\r", "").Split('\n'))
		{
			var line = raw.TrimEnd();
			if (ReleaseLine.Match(line) is { Success: true } m)
			{
				release = new Release(m.Groups[1].Value, AppVersion.Parse(m.Groups[1].Value), m.Groups[2].Value.Trim(), new List<Section>());
				releases.Add(release);
				section = null;
				continue;
			}
			if (release == null) continue;
			if (line.StartsWith("### "))
			{
				section = new Section(line[4..].Trim(), new List<string>());
				release.Sections.Add(section);
				continue;
			}
			if (section == null || line.Trim() == "") continue;
			if (line.StartsWith("- ")) section.Items.Add(line[2..].Trim());
			// An indented "- " is a sub-point of the item above; other indented lines continue it.
			else if (section.Items.Count > 0 && line.TrimStart().StartsWith("- ")) section.Items[^1] += "\n• " + line.TrimStart()[2..].Trim();
			else if (section.Items.Count > 0) section.Items[^1] += " " + line.Trim();
		}
		return releases;
	}

	/// <summary>Releases newer than <paramref name="since"/> (all when null) up to <paramref name="upTo"/>, newest first.</summary>
	public static List<Release> Between(List<Release> all, AppVersion? since, AppVersion upTo) =>
		all.Where(r => r.Version is { } v && !(v > upTo) && (since == null || v > since.Value)).OrderByDescending(r => r.Version!.Value).ToList();
}
