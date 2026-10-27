// Bug reports, sent by hand (the 🐞 button, Ui/BugReport): one Markdown file per report in Max's private results repo
// (bug-reports/<category>/<date> <version> <id>.md), from the same template every time, with a screenshot next to it
// (.png, same name). Feedback (ideas: maps, question types, quality of life …) goes the same way to feedback/<kind>/, without
// the log. If the upload fails (no internet), the report waits in user://bug-reports and goes on the next start.
// Every report says who sent it (Steam name and ID, so ideas can be credited in the changelog), and once it's uploaded a
// push goes to Max's phone and PC through ntfy.sh (Push).

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
	/// <summary>The ntfy.sh topic Max's phone and PC listen to. Anyone who knows it can read and send pushes, so it's long and
	/// random and only here.</summary>
	const string NtfyTopic = "quiznight-reports-ns3jtx1tqjkaqt7s2c4skgeu";
	/// <summary>How much of the player's text the push shows (the whole report is a tap away).</summary>
	const int PushTextMax = 600;

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

	/// <summary>Who sent it: the Steam name and ID with a link to the profile (to credit ideas), or the game's name without Steam.</summary>
	public static string Sender()
	{
		if (Net.SteamService.MySteamId is var id && id != 0)
			return $"{Net.SteamService.PlayerName} · Steam ID {id} (https://steamcommunity.com/profiles/{id})";
		var name = Settings.PlayerName.Trim();
		return $"{(name == "" ? "(no name)" : name)} · no Steam ID (Steam wasn't running)";
	}

	/// <summary>The report from the template: what the game knows, then the player's text, then the log.</summary>
	public static string Make(string category, string text, string details) => new StringBuilder()
		.AppendLine($"# Bug: {category}")
		.AppendLine()
		.AppendLine($"- **Sent by:** {Sender()}")
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

	/// <summary>Feedback from the template: what it's about, where it was sent from, then the idea (no log: it's not a bug).</summary>
	public static string MakeFeedback(string kind, string where, string text) => new StringBuilder()
		.AppendLine($"# Feedback: {kind}")
		.AppendLine()
		.AppendLine($"- **Sent by:** {Sender()}")
		.AppendLine($"- **Version:** {App.Version}{(Updater.TestChannel ? " (test build)" : OS.HasFeature("template") ? "" : " (editor)")}")
		.AppendLine($"- **Date:** {DateTime.Now:yyyy-MM-dd HH:mm} ({DateTime.UtcNow:HH:mm} UTC)")
		.AppendLine($"- **Language / skin:** {Settings.Language} / {Settings.Skin}")
		.AppendLine($"- **Sent from:** {where}")
		.AppendLine()
		.AppendLine("## Idea")
		.AppendLine()
		.AppendLine(text.Trim())
		.ToString();

	/// <summary>Sends a report (and its screenshot) to root/category/; if that fails it waits for the next start. Calls back
	/// with whether it went.</summary>
	public static void Send(string category, string report, byte[]? png, Action<bool> done, string root = "bug-reports")
	{
		var folder = string.Concat(category.Select(c => char.IsLetterOrDigit(c) || c is ' ' or '-' ? c : '-')).Replace("  ", " ").Trim(' ', '-');
		var name = $"{root}/{folder}/{DateTime.Now:yyyy-MM-dd HH-mm} {App.Version} {Guid.NewGuid().ToString("N")[..6]}";
		_ = Task.Run(async () =>
		{
			var ok = true;
			try
			{
				await Put(name + ".md", Encoding.UTF8.GetBytes(report));
				if (png != null) await Put(name + ".png", png);
				await Push(name + ".md", report, false);
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
					var report = await File.ReadAllBytesAsync(md);
					await Put(name + ".md", report);
					var png = Path.ChangeExtension(md, ".png");
					if (File.Exists(png)) await Put(name + ".png", await File.ReadAllBytesAsync(png));
					File.Delete(md);
					if (File.Exists(png)) File.Delete(png);
					await Push(name + ".md", Encoding.UTF8.GetString(report), true);
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

	/// <summary>
	/// The push to Max's phone and PC (ntfy.sh), read from the report itself so late ones look the same: "Bug" or "Feature
	/// request" and where, who sent it, the start of the text; tapping it opens the report in the repo. A push that fails
	/// is only logged: the report is already in.
	/// </summary>
	static async Task Push(string file, string report, bool late)
	{
		try
		{
			var lines = report.Replace("\r", "").Split('\n');
			var head = lines.FirstOrDefault()?.TrimStart('#', ' ') ?? "";
			var bug = head.StartsWith("Bug:");
			var title = (bug ? "Bug: " + head[4..].Trim() : "Feature request: " + head.Replace("Feedback:", "").Trim()) + (late ? " (sent late)" : "");
			string Field(string key) => lines.FirstOrDefault(l => l.StartsWith($"- **{key}:** "))?[($"- **{key}:** ").Length..] ?? "";
			// (the player's text: the section after "What happened" / "Idea", up to the next one)
			var text = string.Join('\n', lines.SkipWhile(l => l is not ("## What happened" or "## Idea")).Skip(1).TakeWhile(l => !l.StartsWith("## "))).Trim();
			if (text.Length > PushTextMax) text = text[..PushTextMax] + " …";
			var message = $"From {Field("Sent by")}\n{Field("Version")}{(bug ? "" : " · " + Field("Sent from"))}\n\n{text}";
			var body = new JsonObject
			{
				["topic"] = NtfyTopic,
				["title"] = title,
				["message"] = message,
				["tags"] = new JsonArray(bug ? "lady_beetle" : "bulb"),
				["priority"] = bug ? 4 : 3,
				["click"] = $"https://github.com/{ResultsUpload.Repo}/blob/HEAD/{string.Join('/', file.Split('/').Select(Uri.EscapeDataString))}",
			}.ToJsonString();
			using var http = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(15) };
			using var response = await http.PostAsync("https://ntfy.sh/", new StringContent(body, Encoding.UTF8, "application/json"));
			if (!response.IsSuccessStatusCode) GD.PrintErr($"Couldn't send the push for the report: ntfy said {(int)response.StatusCode}");
		}
		catch (Exception e)
		{
			GD.PrintErr($"Couldn't send the push for the report: {e.Message}");
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
