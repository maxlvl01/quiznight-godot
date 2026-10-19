// Updates the game from GitHub: the public repo ReleasesRepo has one release per version (tag "v0.5.0a")
// with the builds as "Quiznight-<version>-windows.zip" and "…-linux.zip", plus "manifest-windows.json" /
// "manifest-linux.json": every file of the build with its SHA-256 and where to download it gzipped
// (tools/manifest.py). At startup the game asks for the latest release; if it's newer, it downloads only the
// files that differ from its own (usually the .pck and the game's DLL, a few MB) — or the whole zip when a
// release has no manifest. Then a small script (PowerShell on Windows, sh on Linux) waits for the game to
// close, copies the new files over the install folder and starts it again.
// Only in exported builds — never while running from the Godot editor or a dev build.

using System;
using System.IO;
using System.Linq;
using System.IO.Compression;
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading;
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

	/// <summary>The update manifest for this system.</summary>
	static string ManifestName => IsWindows ? "manifest-windows.json" : "manifest-linux.json";

	public sealed record Release(AppVersion Version, string ZipUrl, long Size, string? ManifestUrl);
	sealed record FileEntry(string Path, long Size, string Sha256, string Url, long Gz);

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
			// (QUIZNIGHT_UPDATE_FEED: a stand-in for GitHub's answer, to test updates against a local server)
			var feed = OS.GetEnvironment("QUIZNIGHT_UPDATE_FEED");
			var json = await http.GetStringAsync(feed.Length > 0 ? feed : $"https://api.github.com/repos/{ReleasesRepo}/releases/latest");
			if (JsonNode.Parse(json) is not JsonObject release) return null;
			var latest = AppVersion.Parse(release["tag_name"]?.GetValue<string>());
			var current = AppVersion.Parse(App.Version);
			if (latest is not { } v || current is not { } c || !(v > c)) return null;
			var assets = (release["assets"] as JsonArray)?.OfType<JsonObject>().ToList() ?? new();
			var zip = assets.FirstOrDefault(a => a["name"]?.GetValue<string>().EndsWith(ZipSuffix, StringComparison.OrdinalIgnoreCase) == true);
			if (zip?["browser_download_url"]?.GetValue<string>() is not { } url) return null;
			var manifest = assets.FirstOrDefault(a => a["name"]?.GetValue<string>() == ManifestName);
			return new Release(v, url, zip["size"]?.GetValue<long>() ?? 0, manifest?["browser_download_url"]?.GetValue<string>());
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

	/// <summary>Downloads the release's new files, starts the helper that swaps them in, and quits the game.</summary>
	public static async Task InstallAsync(Release release, Action<float> progress)
	{
		var newFiles = Path.Combine(UpdateDir, "new");
		var installDir = Path.GetDirectoryName(OS.GetExecutablePath())!;
		var exeName = Path.GetFileName(OS.GetExecutablePath());
		var shown = -1f;
		var gate = new object();
		void Report(float p)
		{
			lock (gate)
			{
				if (p - shown < 0.005f) return;
				shown = p;
			}
			Callable.From(() => progress(p)).CallDeferred();
		}

		// All off the main thread: awaited there, every network read waited for the next frame, so
		// downloads crawled along at a few MB per second however fast the internet was.
		await Task.Run(async () =>
		{
			if (Directory.Exists(UpdateDir)) Directory.Delete(UpdateDir, true);
			Directory.CreateDirectory(newFiles);
			if (release.ManifestUrl != null)
			{
				try
				{
					await DownloadChangedFiles(release.ManifestUrl, installDir, newFiles, exeName, Report);
					return;
				}
				catch (Exception e)
				{
					GD.Print($"Updating file by file didn't work ({e.Message}), downloading the whole build.");
					Directory.Delete(newFiles, true);
					Directory.CreateDirectory(newFiles);
				}
			}
			await DownloadZip(release, newFiles, Report);
			if (!File.Exists(Path.Combine(newFiles, exeName))) throw new InvalidOperationException($"The update doesn't contain {exeName}.");
		});
		progress(1);

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

	/// <summary>
	/// Downloads every file of the manifest that this install doesn't have exactly (same size and SHA-256),
	/// four at a time, into newFiles (keeping the build's folders).
	/// </summary>
	static async Task DownloadChangedFiles(string manifestUrl, string installDir, string newFiles, string exeName, Action<float> report)
	{
		using var http = NewClient(TimeSpan.FromMinutes(30));
		var files = (JsonNode.Parse(await http.GetStringAsync(manifestUrl))?["files"] as JsonArray)?.OfType<JsonObject>()
			.Select(f => new FileEntry(f["path"]!.GetValue<string>(), f["size"]!.GetValue<long>(), f["sha256"]!.GetValue<string>(),
				f["url"]!.GetValue<string>(), f["gz"]!.GetValue<long>()))
			.ToList() ?? throw new InvalidOperationException("The update's file list is empty.");
		if (files.All(f => f.Path != exeName)) throw new InvalidOperationException($"The update doesn't contain {exeName}.");

		var root = Path.GetFullPath(newFiles) + Path.DirectorySeparatorChar;
		var needed = files.Where(f => !SameFile(Path.Combine(installDir, f.Path), f)).ToList();
		var total = Math.Max(1, needed.Sum(f => f.Gz));
		var done = new long[1];
		GD.Print($"Updating {needed.Count} of {files.Count} files ({total / 1e6:0.0} MB).");
		await Parallel.ForEachAsync(needed, new ParallelOptions { MaxDegreeOfParallelism = 4 }, async (file, ct) =>
		{
			var target = Path.GetFullPath(Path.Combine(newFiles, file.Path));
			if (!target.StartsWith(root, StringComparison.Ordinal)) throw new InvalidOperationException($"Bad file name in the update: {file.Path}");
			Directory.CreateDirectory(Path.GetDirectoryName(target)!);
			var gz = target + ".download.gz";
			using (var response = await http.GetAsync(file.Url, HttpCompletionOption.ResponseHeadersRead, ct))
			{
				response.EnsureSuccessStatusCode();
				await using var from = await response.Content.ReadAsStreamAsync(ct);
				await using var to = File.Create(gz);
				var buffer = new byte[1 << 16];
				int read;
				while ((read = await from.ReadAsync(buffer, ct)) > 0)
				{
					await to.WriteAsync(buffer.AsMemory(0, read), ct);
					report((float)Interlocked.Add(ref done[0], read) / total * 0.95f);
				}
			}
			await using (var packed = new GZipStream(File.OpenRead(gz), CompressionMode.Decompress))
			await using (var to = File.Create(target))
				await packed.CopyToAsync(to, ct);
			File.Delete(gz);
			if (!SameFile(target, file)) throw new InvalidOperationException($"{file.Path} arrived damaged.");
		});
	}

	static bool SameFile(string path, FileEntry file)
	{
		if (!File.Exists(path) || new FileInfo(path).Length != file.Size) return false;
		using var stream = File.OpenRead(path);
		return Convert.ToHexString(SHA256.HashData(stream)).Equals(file.Sha256, StringComparison.OrdinalIgnoreCase);
	}

	/// <summary>The whole build as a zip (for releases without a manifest), unpacked into newFiles.</summary>
	static async Task DownloadZip(Release release, string newFiles, Action<float> report)
	{
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
				if (total > 0) report((float)done / total * 0.9f);
			}
		}
		// (System.IO.Compression keeps memory low for the big files)
		ZipFile.ExtractToDirectory(zipPath, newFiles, true);
		File.Delete(zipPath);
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
