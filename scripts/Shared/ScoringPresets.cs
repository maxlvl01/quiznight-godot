// Points per place for closeness questions (estimation, map pin): the quiz maker picks a preset or makes their own.
// The question stores the points themselves (Question.RankPoints), so a shared quiz scores the same everywhere.

using System.Collections.Generic;
using System.Linq;

namespace QuizNight.Shared;

public sealed record ScoringPreset(string Name, int[] Points, bool BuiltIn = true);

public static class ScoringPresets
{
	static int[] Down(int from, int to) => Enumerable.Range(0, from - to + 1).Select(i => from - i).ToArray();

	public static readonly ScoringPreset[] BuiltIn =
	{
		new("F1 (since 2010)", new[] { 25, 18, 15, 12, 10, 8, 6, 4, 2, 1 }),
		new("F1 (2003–2009)", new[] { 10, 8, 6, 5, 4, 3, 2, 1 }),
		new("F1 (1991–2002)", new[] { 10, 6, 4, 3, 2, 1 }),
		new("F1 (1961–1990)", new[] { 9, 6, 4, 3, 2, 1 }),
		new("F1 (1950–1959)", new[] { 8, 6, 4, 3, 2 }),
		new("MotoGP", new[] { 25, 20, 16, 13, 11, 10, 9, 8, 7, 6, 5, 4, 3, 2, 1 }),
		new("IndyCar", new[] { 50, 40, 35, 32, 30, 28, 26, 24, 22, 20, 19, 18, 17, 16, 15, 14, 13, 12, 11, 10, 9, 8, 7, 6, 5 }),
		new("NASCAR Cup (since 2017)", new[] { 40 }.Concat(Down(35, 1)).ToArray()),
		new("Mario Kart 8", new[] { 15, 12, 10, 9, 8, 7, 6, 5, 4, 3, 2, 1 }),
		new("Podium (3 / 2 / 1)", new[] { 3, 2, 1 }),
	};

	/// <summary>"25 / 18 / 15 / 12 …" (the first few places).</summary>
	public static string Describe(IReadOnlyList<int> points, int shown = 5) =>
		string.Join(" / ", points.Take(shown)) + (points.Count > shown ? " …" : "");

	/// <summary>The preset with exactly these points (built-in or one of the user's), or null.</summary>
	public static ScoringPreset? Find(IReadOnlyList<int> points, IEnumerable<ScoringPreset> mine) =>
		BuiltIn.Concat(mine).FirstOrDefault(p => p.Points.SequenceEqual(points));

	public const int PlacesMax = 40;
	public const int PointsMax = 999;
}
