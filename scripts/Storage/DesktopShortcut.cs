// A desktop shortcut on the very first start of an exported game, if the desktop has none for Quiznight yet (the zip
// unpacks somewhere in Downloads, and people lose it there). Windows: a .lnk made by PowerShell's WScript.Shell; Linux: a
// .desktop file. Only once: later starts never add one again, so a deleted shortcut stays deleted.

using System;
using System.IO;
using System.Linq;
using Godot;
using QuizNight.Shared;
using Environment = System.Environment;

namespace QuizNight.Storage;

public static class DesktopShortcut
{
	const string Name = "Quiznight";

	/// <summary>The first start: make the shortcut unless one is there (exported games only; never in editor runs).</summary>
	public static void MakeOnFirstStart(bool firstStart)
	{
		if (!firstStart || !OS.HasFeature("template") || OS.GetCmdlineUserArgs().Length > 0) return;
		try
		{
			if (OS.GetName() == "Windows") Windows();
			else if (OS.GetName() == "Linux") Linux();
		}
		catch (Exception e)
		{
			GD.PrintErr($"Couldn't make the desktop shortcut: {e.Message}");
		}
	}

	/// <summary>Any shortcut with Quiznight in its name on this desktop (or everyone's)?</summary>
	static bool Has(string extension, params string[] desktops) =>
		desktops.Where(d => d != "" && Directory.Exists(d))
			.Any(d => Directory.EnumerateFiles(d, "*" + extension).Any(f => Path.GetFileName(f).Contains(Name, StringComparison.OrdinalIgnoreCase)));

	static void Windows()
	{
		var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
		if (desktop == "" || Has(".lnk", desktop, Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory))) return;
		var exe = OS.GetExecutablePath().Replace('/', '\\');
		string Quote(string s) => "'" + s.Replace("'", "''") + "'";
		var script = $"$s = (New-Object -ComObject WScript.Shell).CreateShortcut({Quote(Path.Combine(desktop, Name + ".lnk"))}); "
			+ $"$s.TargetPath = {Quote(exe)}; $s.WorkingDirectory = {Quote(Path.GetDirectoryName(exe)!)}; $s.IconLocation = {Quote(exe + ",0")}; "
			+ "$s.Description = 'Quiznight?'; $s.Save()";
		OS.CreateProcess("powershell.exe", new[] { "-NoProfile", "-NonInteractive", "-WindowStyle", "Hidden", "-Command", script });
		Trace.Log("start", "desktop shortcut made");
	}

	static void Linux()
	{
		var desktop = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Desktop");
		if (!Directory.Exists(desktop) || Has(".desktop", desktop)) return;
		var exe = OS.GetExecutablePath();
		var file = Path.Combine(desktop, Name + ".desktop");
		File.WriteAllText(file, $"[Desktop Entry]\nType=Application\nName=Quiznight?\nExec=\"{exe}\"\nPath={Path.GetDirectoryName(exe)}\nTerminal=false\nCategories=Game;\n");
		OS.Execute("chmod", new[] { "+x", file });
		Trace.Log("start", "desktop shortcut made");
	}
}
