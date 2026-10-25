// Units for estimation questions, grouped for the editor's drop-down. The question stores the unit as the text players
// see ("km", "light years"); words are stored in the quiz maker's language, symbols as they are. Anything else can still
// be typed as a custom unit.

using System.Collections.Generic;
using System.Linq;

namespace QuizNight.Shared;

public static class Units
{
	/// <summary>(group, units): a unit is its symbol, or a word that's translated when picked.</summary>
	public static readonly (string Group, string[] Units)[] Groups =
	{
		("Length", new[] { "mm", "cm", "m", "km", "in", "ft", "yd", "mi", "nmi", "AU", "light years", "pc" }),
		("Mass", new[] { "mg", "g", "kg", "t", "oz", "lb" }),
		("Time", new[] { "ms", "s", "min", "h", "days", "weeks", "months", "years" }),
		("Speed", new[] { "m/s", "km/h", "mph", "knots" }),
		("Area", new[] { "m²", "km²", "ha", "acres" }),
		("Volume", new[] { "ml", "l", "m³", "gal" }),
		("Temperature", new[] { "°C", "°F", "K" }),
		("Energy and power", new[] { "J", "kJ", "kcal", "kWh", "W", "kW", "MW", "hp", "PS" }),
		("Data", new[] { "KB", "MB", "GB", "TB" }),
		("Money", new[] { "€", "$", "£" }),
		("Other", new[] { "%", "people", "pieces" }),
	};

	/// <summary>Units that are words (translated into the quiz maker's language when picked).</summary>
	static readonly HashSet<string> Words = new() { "light years", "days", "weeks", "months", "years", "knots", "acres", "people", "pieces" };

	/// <summary>What a picked unit stores: a symbol as it is, a word in the current language.</summary>
	public static string Text(string unit) => Words.Contains(unit) ? Lang.T(unit) : unit;

	/// <summary>The list unit a stored text is (in either language), or null for a custom one.</summary>
	public static string? Find(string text)
	{
		if (text == "") return null;
		foreach (var u in Groups.SelectMany(g => g.Units))
			if (u == text || Words.Contains(u) && (Lang.T(u) == text || LangDe.Strings.TryGetValue(u, out var de) && de == text)) return u;
		return null;
	}
}
