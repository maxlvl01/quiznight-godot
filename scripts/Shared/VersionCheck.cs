// What a player on an older game can't play in a quiz: question types and built-in maps that came after their version.
// The host's lobby lists it (players' versions come with their join, JoinRequest.Version), so nothing breaks mid-game.

using System.Collections.Generic;
using System.Linq;

namespace QuizNight.Shared;

public static class VersionCheck
{
	/// <summary>Games before 0.12 don't say their version: they're counted as the last one before it.</summary>
	public const string BeforeVersions = "0.11.4b";

	/// <summary>The version each question type came in (types not listed were there from the start).</summary>
	static readonly Dictionary<string, string> TypeSince = new()
	{
		[QuestionType.Picture] = "0.8.9b",
		[QuestionType.Interactive] = "0.9.0b",
		[QuestionType.Steps] = "0.9.0b",
	};

	/// <summary>A player's version, or the one before 0.12 if their game didn't say.</summary>
	public static AppVersion Of(string? version) => AppVersion.Parse(version) ?? AppVersion.Parse(BeforeVersions)!.Value;

	/// <summary>Whether the player's game is older than the host's.</summary>
	public static bool Older(string? player, string host) => AppVersion.Parse(host) is { } h && Of(player) < h;

	/// <summary>What in the quiz the player's game doesn't have: "question 5 (Interactive, needs 0.9.0b)".</summary>
	public static List<string> Missing(Quiz quiz, string? playerVersion)
	{
		var player = Of(playerVersion);
		var missing = new List<string>();
		for (var i = 0; i < quiz.Questions.Count; i++)
		{
			var q = quiz.Questions[i];
			string? what = null, since = null;
			if (TypeSince.TryGetValue(q.Type, out var typeSince) && player < AppVersion.Parse(typeSince)!.Value)
				(what, since) = (Lang.T(QuestionKinds.TypeLabel(q.Type)), typeSince);
			else if (q.Type == QuestionType.MapPin && quiz.MapById(q.MapId) is { } map && MapPresets.Get(map.PresetId) is { AddedIn: { } added } preset
				&& AppVersion.Parse(added) is { } addedIn && player < addedIn)
				(what, since) = (preset.Name, added);
			if (what != null) missing.Add(Lang.F("question {0} ({1}, needs {2})", i + 1, what, since!));
		}
		return missing;
	}
}
