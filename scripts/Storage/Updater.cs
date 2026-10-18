// Updates the game from GitHub: the public repo ReleasesRepo has one release per version (tag "v0.5.0a")
// with the Windows build as "Quiznight-<version>-windows.zip". At startup the game asks for the latest
// release; if it's newer, it downloads and unpacks it, then a small PowerShell script waits for the game
// to close, copies the new files over the install folder and starts it again.
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
	public const string ZipSuffix = "-windows.zip";

	public sealed record Release(AppVersion Version, string ZipUrl, long Size);

	/// <summary>Exported Windows builds only (the editor and `godot --path .` runs never update themselves).</summary>
	public static bool Enabled => OS.HasFeature("template") && OS.GetName() == "Windows";

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

		var script = Path.Combine(UpdateDir, "apply.ps1");
		File.WriteAllText(script, ApplyScript);
		var installDir = Path.GetDirectoryName(OS.GetExecutablePath())!;
		OS.CreateProcess("powershell.exe", new[]
		{
			"-NoProfile", "-ExecutionPolicy", "Bypass", "-WindowStyle", "Hidden", "-File", script,
			"-ProcId", OS.GetProcessId().ToString(), "-From", newFiles, "-To", installDir, "-Exe", OS.GetExecutablePath(),
		});
		App.I.Quit();
	}

	/// <summary>Waits for the game to close, copies the new build over the old one (retrying while files are still locked), restarts it.</summary>
	const string ApplyScript = """
		param([int]$ProcId, [string]$From, [string]$To, [string]$Exe)
		Wait-Process -Id $ProcId -Timeout 60 -ErrorAction SilentlyContinue
		Start-Sleep -Milliseconds 500
		for ($i = 0; $i -lt 15; $i++) {
			try { Copy-Item -Path (Join-Path $From '*') -Destination $To -Recurse -Force -ErrorAction Stop; break }
			catch { Start-Sleep -Seconds 1 }
		}
		Start-Process -FilePath $Exe -WorkingDirectory $To
		""";
}
