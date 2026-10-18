// Updates the game from GitHub: the public repo ReleasesRepo has one release per version (tag "v0.5.0a")
// with the builds as "Quiznight-<version>-windows.zip" and "…-linux.zip". At startup the game asks for the
// latest release; if it's newer, it downloads and unpacks it, then a small script (PowerShell on Windows,
// sh on Linux) waits for the game to close, copies the new files over the install folder and starts it again.
// Only in exported builds — never while running from the Godot editor or a dev build.

using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Godot;
using QuizNight.Shared;
using HttpClient = System.Net.Http.HttpClient;

namespace QuizNight.Storage;

public static class Updater
{
	public const string ReleasesRepo = "maxlvl01/quiznight-releases";
	static bool IsWindows => OS.GetName() == "Windows";
	/// <summary>The release asset for this system: "Quiznight-0.5.0a-windows.zip" or "…-linux.zip".</summary>
	public static string ZipSuffix => IsWindows ? "-windows.zip" : "-linux.zip";

	public sealed record Release(AppVersion Version, string ZipUrl, long Size);

	/// <summary>Exported Windows and Linux builds only (the editor and `godot --path .` runs never update themselves).</summary>
	public static bool Enabled => OS.HasFeature("template") && (IsWindows || OS.GetName() == "Linux");

	static HttpClient NewClient(TimeSpan timeout)
	{
		var http = new HttpClient { Timeout = timeout };
		http.DefaultRequestHeaders.UserAgent.ParseAdd("Quiznight-Updater");
		return http;
	}

	/// <summary>The newest release if it's newer than this game, else null (also when offline).</summary>
	public static async Task<Release?> CheckAsync()
	{
		try
		{
			using var http = NewClient(TimeSpan.FromSeconds(10));
			var json = await http.GetStringAsync($"https://api.github.com/repos/{ReleasesRepo}/releases/latest");
			if (JsonNode.Parse(json) is not JsonObject release) return null;
			var latest = AppVersion.Parse(release["tag_name"]?.GetValue<string>());
			var current = AppVersion.Parse(App.Version);
			if (latest is not { } v || current is not { } c || !(v > c)) return null;
			var zip = (release["assets"] as JsonArray)?.OfType<JsonObject>()
				.FirstOrDefault(a => a["name"]?.GetValue<string>().EndsWith(ZipSuffix, StringComparison.OrdinalIgnoreCase) == true);
			if (zip?["browser_download_url"]?.GetValue<string>() is not { } url) return null;
			return new Release(v, url, zip["size"]?.GetValue<long>() ?? 0);
		}
		catch (Exception e)
		{
			GD.Print($"Update check skipped: {e.Message}");
			return null;
		}
	}

	static string UpdateDir => ProjectSettings.GlobalizePath("user://update");

	/// <summary>At startup: removes what the last update left behind (the download and the unpacked files).</summary>
	public static void CleanUp()
	{
		try
		{
			if (Directory.Exists(UpdateDir)) Directory.Delete(UpdateDir, true);
		}
		catch (Exception e)
		{
			GD.Print($"Couldn't clean up the last update: {e.Message}");
		}
	}

	/// <summary>Downloads and unpacks the release, starts the helper that swaps the files, and quits the game.</summary>
	public static async Task InstallAsync(Release release, Action<float> progress)
	{
		if (Directory.Exists(UpdateDir)) Directory.Delete(UpdateDir, true);
		Directory.CreateDirectory(UpdateDir);
		var zipPath = Path.Combine(UpdateDir, "update.zip");

		using (var http = NewClient(TimeSpan.FromMinutes(30)))
		using (var response = await http.GetAsync(release.ZipUrl, HttpCompletionOption.ResponseHeadersRead))
		{
			response.EnsureSuccessStatusCode();
			var total = response.Content.Headers.ContentLength ?? release.Size;
			await using var from = await response.Content.ReadAsStreamAsync();
			await using var to = File.Create(zipPath);
			var buffer = new byte[1 << 16];
			long done = 0;
			int read;
			while ((read = await from.ReadAsync(buffer)) > 0)
			{
				await to.WriteAsync(buffer.AsMemory(0, read));
				done += read;
				if (total > 0) progress((float)done / total * 0.9f);
			}
		}

		// Unpack next to it (System.IO.Compression keeps memory low for the big files).
		var newFiles = Path.Combine(UpdateDir, "new");
		await Task.Run(() => System.IO.Compression.ZipFile.ExtractToDirectory(zipPath, newFiles, true));
		progress(1);
		var exeName = Path.GetFileName(OS.GetExecutablePath());
		if (!File.Exists(Path.Combine(newFiles, exeName))) throw new InvalidOperationException($"The update doesn't contain {exeName}.");

		var installDir = Path.GetDirectoryName(OS.GetExecutablePath())!;
		var pid = OS.GetProcessId().ToString();
		if (IsWindows)
		{
			var script = Path.Combine(UpdateDir, "apply.ps1");
			File.WriteAllText(script, WindowsScript);
			OS.CreateProcess("powershell.exe", new[]
			{
				"-NoProfile", "-ExecutionPolicy", "Bypass", "-WindowStyle", "Hidden", "-File", script,
				"-ProcId", pid, "-From", newFiles, "-To", installDir, "-Exe", OS.GetExecutablePath(),
			});
		}
		else
		{
			var script = Path.Combine(UpdateDir, "apply.sh");
			File.WriteAllText(script, LinuxScript.Replace("\r", ""));
			OS.CreateProcess("/bin/sh", new[] { script, pid, newFiles, installDir, OS.GetExecutablePath() });
		}
		App.I.Quit();
	}

	/// <summary>Waits for the game to close, copies the new build over the old one (retrying while files are still locked), restarts it.</summary>
	const string WindowsScript = """
		param([int]$ProcId, [string]$From, [string]$To, [string]$Exe)
		Wait-Process -Id $ProcId -Timeout 60 -ErrorAction SilentlyContinue
		Start-Sleep -Milliseconds 500
		for ($i = 0; $i -lt 15; $i++) {
			try { Copy-Item -Path (Join-Path $From '*') -Destination $To -Recurse -Force -ErrorAction Stop; break }
			catch { Start-Sleep -Seconds 1 }
		}
		Start-Process -FilePath $Exe -WorkingDirectory $To
		""";

	/// <summary>The same for Linux (zips made on Windows lose the executable bit, so it's set again).</summary>
	const string LinuxScript = """
		#!/bin/sh
		PID=$1; FROM=$2; TO=$3; EXE=$4
		i=0
		while kill -0 "$PID" 2>/dev/null && [ $i -lt 120 ]; do sleep 0.5; i=$((i+1)); done
		cp -rf "$FROM"/. "$TO"/
		chmod +x "$EXE"
		[ -f "$TO/AnyConverter/AnyConverter" ] && chmod +x "$TO/AnyConverter/AnyConverter"
		cd "$TO" && nohup "$EXE" >/dev/null 2>&1 &
		""";
}
