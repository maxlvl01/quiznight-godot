// Languages. The English text is the translation key; German lives in LangDe.cs.
// Godot translates Label/Button/tooltip/placeholder text by itself when it matches a key exactly,
// so plain strings need nothing. Text built from parts goes through Lang.F with a "{0}" template
// (and the template is the key).

using Godot;

namespace QuizNight.Shared;

public static class Lang
{
	public static readonly (string Code, string Name)[] All = { ("en", "English"), ("de", "Deutsch") };

	static bool _registered;

	/// <summary>Switches the whole UI. Screens built with Lang.F need rebuilding to pick it up.</summary>
	public static void Use(string code)
	{
		if (!_registered)
		{
			var de = new Translation { Locale = "de" };
			foreach (var (en, text) in LangDe.Strings) de.AddMessage(en, text);
			TranslationServer.AddTranslation(de);
			_registered = true;
		}
		var locale = code == "de" ? "de" : "en";
		if (TranslationServer.GetLocale() != locale) TranslationServer.SetLocale(locale);
	}

	/// <summary>"de" on a German Windows, else "en" (used until the player picks one).</summary>
	public static string SystemDefault => OS.GetLocaleLanguage() == "de" ? "de" : "en";

	public static string T(string en) => TranslationServer.Translate(en).ToString();

	public static string F(string en, params object?[] args) => string.Format(T(en), args);

	/// <summary>A place: "1st", "2nd", "3rd", "4th", "11th", "22nd" (German "1.", "2.").</summary>
	public static string Ordinal(int n)
	{
		if (TranslationServer.GetLocale() == "de") return $"{n}.";
		var suffix = (n % 100) is 11 or 12 or 13 ? "th" : (n % 10) switch { 1 => "st", 2 => "nd", 3 => "rd", _ => "th" };
		return $"{n}{suffix}";
	}
}
