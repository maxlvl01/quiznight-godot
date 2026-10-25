// Crash reports: when a run of the exported game crashed (it never got to close normally) or ran into a C# error, the
// next start uploads the end of that run's log to Max's private results repo (maxlvl01/quiznight-results, crashes/),
// with the same token as the results. Godot writes the log itself (%APPDATA%\Quiznight\logs, the last few runs); this
// only notices a bad run and sends it, once. Editor and dev runs never upload.

using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Godot;

namespace QuizNight.Storage;

public static class CrashReports
{
	/// <summary>Written at the start of a run, deleted when it closes normally: still there = that run crashed.</summary>
	const string Marker = "user://running.txt";
	/// <summary>Log files already sent (one name per line), so a log is never sent twice.</summary>
	const string Sent = "user://crash-reports-sent.txt";
	const int TailLines = 400;

	static bool Enabled => OS.HasFeature("template") && ResultsUpload.Token != null && OS.GetCmdlineUserArgs().Length == 0;

	/// <summary>
	/// At the start: send the last run's log if it went wrong, then mark this run as running. Also writes C# errors that
	/// would otherwise vanish (exceptions on other threads, forgotten tasks) into the log.
	/// </summary>
	public static void Start()
	{
		AppDomain.CurrentDomain.UnhandledException += (_, e) => GD.PrintErr($"Unhandled exception: {e.ExceptionObject}");
		TaskScheduler.UnobservedTaskException += (_, e) =>
		{
			GD.PrintErr($"Unhandled exception in a task: {e.Exception}");
			e.SetObserved();
		};
		if (!Enabled) return;
		var crashed = Godot.FileAccess.FileExists(Marker);
		var since = crashed ? Godot.FileAccess.GetFileAsString(Marker).Trim() : "";
		try
		{
			if (LastLog() is { } log) Check(log, crashed, since);
		}
		catch (Exception e)
		{
			GD.PrintErr($"Couldn't check the last run's log: {e.Message}");
		}
		using var f = Godot.FileAccess.Open(Marker, Godot.FileAccess.ModeFlags.Write);
		f?.StoreString(DateTime.UtcNow.ToString("o"));
	}

	/// <summary>Closing normally: this run didn't crash.</summary>
	public static void Stop()
	{
		if (Godot.FileAccess.FileExists(Marker)) DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(Marker));
	}

	/// <summary>The previous run's log: Godot renames the old godot.log to godot&lt;time&gt;.log when a run starts.</summary>
	static FileInfo? LastLog()
	{
		var dir = new DirectoryInfo(ProjectSettings.GlobalizePath("user://logs"));
		if (!dir.Exists) return null;
		return dir.GetFiles("godot*.log").Where(f => f.Name != "godot.log").OrderByDescending(f => f.LastWriteTimeUtc).FirstOrDefault();
	}

	/// <summary>Whether a log shows a C# error (Godot logs exceptions from signal handlers as ERROR lines).</summary>
	public static bool HasError(string log) =>
		log.Contains("Unhandled exception") || log.Split('\n').Any(l => l.StartsWith("ERROR:") && l.Contains("Exception"));

	/// <summary>The Windows (or Linux) user name in paths becomes &lt;user&gt;.</summary>
	public static string Anonymise(string text, string user) =>
		user == "" ? text : text.Replace(user, "<user>", StringComparison.OrdinalIgnoreCase);

	static void Check(FileInfo log, bool crashed, string since)
	{
		var sent = Godot.FileAccess.FileExists(Sent) ? Godot.FileAccess.GetFileAsString(Sent).Split('\n') : Array.Empty<string>();
		if (sent.Contains(log.Name)) return;
		// (the run that left the marker is the one that wrote this log, not an older one or a dev run in between)
		if (crashed && DateTime.TryParse(since, null, System.Globalization.DateTimeStyles.RoundtripKind, out var start) && log.LastWriteTimeUtc < start) crashed = false;
		var text = File.ReadAllText(log.FullName);
		if (!crashed && !HasError(text)) return;
		var lines = text.Split('\n');
		var tail = string.Join('\n', lines.Skip(Math.Max(0, lines.Length - TailLines)));
		var report = new StringBuilder()
			.AppendLine($"Quiznight {App.Version} · {OS.GetName()} {OS.GetVersion()} · {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC")
			.AppendLine(crashed ? "The game didn't close normally (crashed or was killed)." : "The game ran into an error.")
			.AppendLine($"Log: {log.Name} (last {Math.Min(lines.Length, TailLines)} of {lines.Length} lines)")
			.AppendLine()
			.Append(tail)
			.ToString();
		report = Anonymise(report, System.Environment.UserName);
		var name = log.Name;
		_ = Task.Run(async () =>
		{
			try
			{
				await Put($"{DateTime.UtcNow:yyyy-MM-dd HH-mm} {App.Version} {(crashed ? "crash" : "error")} {Guid.NewGuid().ToString("N")[..6]}.log", report);
				Callable.From(() =>
				{
					using var f = Godot.FileAccess.Open(Sent, Godot.FileAccess.FileExists(Sent) ? Godot.FileAccess.ModeFlags.ReadWrite : Godot.FileAccess.ModeFlags.Write);
					if (f == null) return;
					f.SeekEnd();
					f.StoreString(name + "\n");
				}).CallDeferred();
			}
			catch (Exception e)
			{
				GD.PrintErr($"Couldn't send the crash report: {e.Message}");
			}
		});
	}

	static async Task Put(string file, string report)
	{
		using var http = ResultsUpload.Client(ResultsUpload.Token!);
		var body = new JsonObject
		{
			["message"] = $"Crash report: {file}",
			["content"] = Convert.ToBase64String(Encoding.UTF8.GetBytes(report)),
		}.ToJsonString();
		var url = $"https://api.github.com/repos/{ResultsUpload.Repo}/contents/crashes/{Uri.EscapeDataString(file)}";
		using var response = await http.PutAsync(url, new StringContent(body, Encoding.UTF8, "application/json"));
		if (!response.IsSuccessStatusCode) throw new HttpRequestException($"GitHub said {(int)response.StatusCode}");
	}
}
