// Bug reports, sent by hand (the 🐞 button, Ui/BugReport): one Markdown file per report in Max's private results repo
// (bug-reports/<category>/<date> <version> <id>.md), from the same template every time, with a screenshot next to it
// (.png, same name). If the upload fails (no internet), the report waits in user://bug-reports and goes on the next start.

using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Godot;

namespace QuizNight.Storage;

public static class BugReports
{
	const string Waiting = "user://bug-reports";
	const int LogLines = 200;

	/// <summary>Reports can be sent (the game has the results token).</summary>
	public static bool Enabled => ResultsUpload.Token != null;

	/// <summary>The end of this run's log (Godot's user://logs/godot.log, open while the game runs), user name left out.</summary>
	public static string LogTail()
	{
		try
		{
			var path = ProjectSettings.GlobalizePath("user://logs/godot.log");
			if (!File.Exists(path)) return "(no log file)";
			using var stream = new FileStream(path, FileMode.Open, System.IO.FileAccess.Read, FileShare.ReadWrite);
			using var reader = new StreamReader(stream);
			var lines = reader.ReadToEnd().Split('\n');
			return CrashReports.Anonymise(string.Join('\n', lines.Skip(Math.Max(0, lines.Length - LogLines))), System.Environment.UserName);
		}
		catch (Exception e)
		{
			return $"(couldn't read the log: {e.Message})";
		}
	}

	/// <summary>The report from the template: what the game knows, then the player's text, then the log.</summary>
	public static string Make(string category, string text, string details) => new StringBuilder()
		.AppendLine($"# Bug: {category}")
		.AppendLine()
		.AppendLine($"- **Version:** {App.Version}{(Updater.TestChannel ? " (test build)" : OS.HasFeature("template") ? "" : " (editor)")}")
		.AppendLine($"- **Date:** {DateTime.Now:yyyy-MM-dd HH:mm} ({DateTime.UtcNow:HH:mm} UTC)")
		.AppendLine($"- **System:** {OS.GetName()} {OS.GetVersion()} · window {DisplayServer.WindowGetSize().X}×{DisplayServer.WindowGetSize().Y}")
		.AppendLine($"- **Language / skin:** {Settings.Language} / {Settings.Skin}")
		.Append(details)
		.AppendLine()
		.AppendLine("## What happened")
		.AppendLine()
		.AppendLine(text.Trim())
		.AppendLine()
		.AppendLine($"## Log (last {LogLines} lines)")
		.AppendLine()
		.AppendLine("```")
		.AppendLine(LogTail())
		.AppendLine("```")
		.ToString();

	/// <summary>Sends a report (and its screenshot); if that fails it waits for the next start. Calls back with whether it went.</summary>
	public static void Send(string category, string report, byte[]? png, Action<bool> done)
	{
		var folder = string.Concat(category.Select(c => char.IsLetterOrDigit(c) || c is ' ' or '-' ? c : '-')).Replace("  ", " ").Trim(' ', '-');
		var name = $"bug-reports/{folder}/{DateTime.Now:yyyy-MM-dd HH-mm} {App.Version} {Guid.NewGuid().ToString("N")[..6]}";
		_ = Task.Run(async () =>
		{
			var ok = true;
			try
			{
				await Put(name + ".md", Encoding.UTF8.GetBytes(report));
				if (png != null) await Put(name + ".png", png);
			}
			catch (Exception e)
			{
				GD.PrintErr($"Couldn't send the bug report: {e.Message}");
				ok = false;
				Keep(name, report, png);
			}
			Callable.From(() => done(ok)).CallDeferred();
		});
	}

	/// <summary>At the start: reports that couldn't go last time go now.</summary>
	public static void SendWaiting()
	{
		if (!Enabled || !DirAccess.DirExistsAbsolute(Waiting)) return;
		var dir = ProjectSettings.GlobalizePath(Waiting);
		_ = Task.Run(async () =>
		{
			foreach (var md in Directory.GetFiles(dir, "*.md"))
			{
				try
				{
					// (the repo path is in the file name, "/" as "~")
					var name = Path.GetFileNameWithoutExtension(md).Replace('~', '/');
					await Put(name + ".md", await File.ReadAllBytesAsync(md));
					var png = Path.ChangeExtension(md, ".png");
					if (File.Exists(png)) await Put(name + ".png", await File.ReadAllBytesAsync(png));
					File.Delete(md);
					if (File.Exists(png)) File.Delete(png);
				}
				catch (Exception e)
				{
					GD.PrintErr($"Couldn't send a waiting bug report: {e.Message}");
					return;
				}
			}
		});
	}

	static void Keep(string name, string report, byte[]? png)
	{
		try
		{
			var dir = ProjectSettings.GlobalizePath(Waiting);
			Directory.CreateDirectory(dir);
			var file = Path.Combine(dir, name.Replace('/', '~'));
			File.WriteAllText(file + ".md", report);
			if (png != null) File.WriteAllBytes(file + ".png", png);
		}
		catch (Exception e)
		{
			GD.PrintErr($"Couldn't keep the bug report: {e.Message}");
		}
	}

	static async Task Put(string file, byte[] content)
	{
		using var http = ResultsUpload.Client(ResultsUpload.Token!);
		var body = new JsonObject { ["message"] = $"Bug report: {file}", ["content"] = Convert.ToBase64String(content) }.ToJsonString();
		var url = $"https://api.github.com/repos/{ResultsUpload.Repo}/contents/{string.Join('/', file.Split('/').Select(Uri.EscapeDataString))}";
		using var response = await http.PutAsync(url, new StringContent(body, Encoding.UTF8, "application/json"));
		if (!response.IsSuccessStatusCode) throw new HttpRequestException($"GitHub said {(int)response.StatusCode}");
	}
}
