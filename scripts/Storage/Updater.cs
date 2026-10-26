// Updates the game from GitHub: the public repo ReleasesRepo has one release per version (tag "v0.5.0a")
// with the builds as "Quiznight-<version>-windows.zip" and "…-linux.zip", plus "update-windows.json" /
// "update-linux.json": every file of the build with its SHA-256 and the pack (a zip: game, engine, maps or
// AnyConverter) it's in, maybe in an older release (tools/manifest.py). At startup the game asks for the latest
// release; if it's newer, it downloads only the packs with files that differ from its own (usually just the game
// pack, about a MB) — or the whole zip when a release has no update list. Then a small script (PowerShell on Windows, sh on Linux) waits for the game to
// close, copies the new files over the install folder and starts it again.
// Only in exported builds — never while running from the Godot editor or a dev build.
// Test builds (a test-channel.txt next to the game, put there by `tools/release.sh --test`) update from the private
// TestRepo instead: its releases are pre-releases (GitHub's "latest" skips those, so the newest is picked from the
// list), read with the token the game ships with, and only have the zips (no update lists).
// Every zip and update list is signed with Max's release key (tools/release.sh, ECDSA P-256): the game installs nothing
// whose signature doesn't match the public key below. So even someone who got into the GitHub account, or swapped files
// on the way, can't hand players a game of their own. (Packs are covered by the signed update lists' SHA-256s.)

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
	public const string TestRepo = "maxlvl01/quiznight-test-releases";

	/// <summary>An internal test build: it has test-channel.txt next to the game and updates from TestRepo.</summary>
	public static bool TestChannel => OS.HasFeature("template") && File.Exists(Path.Combine(Path.GetDirectoryName(OS.GetExecutablePath()) ?? "", "test-channel.txt"));

	static string Repo => TestChannel ? TestRepo : ReleasesRepo;
	static bool IsWindows => OS.GetName() == "Windows";
	/// <summary>The release asset for this system: "Quiznight-0.5.0a-windows.zip" or "…-linux.zip".</summary>
	public static string ZipSuffix => IsWindows ? "-windows.zip" : "-linux.zip";

	/// <summary>The update list for this system.</summary>
	static string ManifestName => IsWindows ? "update-windows.json" : "update-linux.json";

	/// <param name="Private">From the private test repo: downloads need the token.</param>
	/// <param name="ZipSigUrl">The zip's signature (zip name + ".sig"); without one the zip isn't installed.</param>
	/// <param name="ManifestSigUrl">The update list's signature; without one the list isn't used.</param>
	public sealed record Release(AppVersion Version, string ZipUrl, long Size, string? ManifestUrl, bool Private = false,
		string? ZipSigUrl = null, string? ManifestSigUrl = null);

	/// <summary>The public half of Max's release key (tools/release-signing-public.pem). The private half never leaves his PC.</summary>
	const string ReleaseKey = """
		-----BEGIN PUBLIC KEY-----
		MFkwEwYHKoZIzj0CAQYIKoZIzj0DAQcDQgAEGoZCVqxhgqBL1ZguwN2L0qrkjqX3
		ctjziNufZgVQWFyznvK2R+KK7tvHPMa2OrU1YSJwDs+glmSTOItGL+Wgnw==
		-----END PUBLIC KEY-----
		""";

	/// <summary>Whether sig is the release key's signature of data with this SHA-256 (as openssl dgst -sha256 -sign makes it).</summary>
	public static bool SignedByMax(byte[] sha256, byte[] sig)
	{
		try
		{
			using var key = ECDsa.Create();
			key.ImportFromPem(ReleaseKey);
			return key.VerifyHash(sha256, sig, DSASignatureFormat.Rfc3279DerSequence);
		}
		catch (CryptographicException)
		{
			return false;
		}
	}

	static string NotSigned => Lang.T("It isn't signed with Max's release key, so it wasn't installed.");

	/// <summary>A release's asset by name: its download address (public) or its API address (private test repo).</summary>
	static string? AssetUrl(System.Collections.Generic.List<JsonObject> assets, string name, bool api) =>
		assets.FirstOrDefault(a => a["name"]?.GetValue<string>() == name)?[api ? "url" : "browser_download_url"]?.GetValue<string>();
	sealed record FileEntry(string Path, long Size, string Sha256, string Pack);
	sealed record Pack(string Url, long Size, string Sha256);

	/// <summary>Exported Windows and Linux builds only (the editor and `godot --path .` runs never update themselves).</summary>
	public static bool Enabled => OS.HasFeature("template") && (IsWindows || OS.GetName() == "Linux");

	static HttpClient NewClient(TimeSpan timeout)
	{
		var http = new HttpClient { Timeout = timeout };
		http.DefaultRequestHeaders.UserAgent.ParseAdd("Quiznight-Updater");
		return http;
	}

	/// <summary>
	/// The newest version's number, from the releases page (github.com/…/releases/latest sends you on to the newest one's
	/// tag). Unlike GitHub's API (60 checks an hour per internet connection) it can be asked every minute. Null if offline.
	/// </summary>
	public static async Task<AppVersion?> LatestVersionAsync()
	{
		// (test builds: the private repo has no public page to look at, the API with the token it is; 5000 checks an hour)
		if (TestChannel) return (await CheckTestAsync(false))?.Version;
		try
		{
			using var http = NewClient(TimeSpan.FromSeconds(10));
			using var response = await http.SendAsync(new HttpRequestMessage(HttpMethod.Head, $"https://github.com/{ReleasesRepo}/releases/latest"));
			var path = response.RequestMessage?.RequestUri?.AbsolutePath ?? "";
			return AppVersion.Parse(path[(path.LastIndexOf('/') + 1)..]);
		}
		catch (Exception)
		{
			return null;
		}
	}

	/// <summary>The newest release if it's newer than this game, else null (also when offline).</summary>
	public static async Task<Release?> CheckAsync()
	{
		if (TestChannel) return await CheckTestAsync(true);
		try
		{
			using var http = NewClient(TimeSpan.FromSeconds(10));
			// (QUIZNIGHT_UPDATE_FEED: a stand-in for GitHub's answer, to test updates against a local server; debug
			// exports only, so nothing on a player's PC can point their game at another update)
			var feed = OS.IsDebugBuild() ? OS.GetEnvironment("QUIZNIGHT_UPDATE_FEED") : "";
			var json = await http.GetStringAsync(feed.Length > 0 ? feed : $"https://api.github.com/repos/{ReleasesRepo}/releases/latest");
			if (JsonNode.Parse(json) is not JsonObject release) return null;
			var latest = AppVersion.Parse(release["tag_name"]?.GetValue<string>());
			var current = AppVersion.Parse(App.Version);
			if (latest is not { } v || current is not { } c || !(v > c)) return null;
			var assets = (release["assets"] as JsonArray)?.OfType<JsonObject>().ToList() ?? new();
			var zip = assets.FirstOrDefault(a => a["name"]?.GetValue<string>().EndsWith(ZipSuffix, StringComparison.OrdinalIgnoreCase) == true);
			if (zip?["browser_download_url"]?.GetValue<string>() is not { } url) return null;
			var manifest = assets.FirstOrDefault(a => a["name"]?.GetValue<string>() == ManifestName);
			var zipName = zip["name"]!.GetValue<string>();
			return new Release(v, url, zip["size"]?.GetValue<long>() ?? 0, manifest?["browser_download_url"]?.GetValue<string>(), false,
				AssetUrl(assets, zipName + ".sig", false), AssetUrl(assets, ManifestName + ".sig", false));
		}
		catch (Exception e)
		{
			GD.Print($"Update check skipped: {e.Message}");
			return null;
		}
	}

	/// <summary>
	/// Test builds: the newest release in the private TestRepo (pre-releases included, drafts not), if newer than this game.
	/// </summary>
	/// <param name="newerOnly">False: the newest version whatever it is (for the every-minute check).</param>
	static async Task<Release?> CheckTestAsync(bool newerOnly)
	{
		try
		{
			if (ResultsUpload.Token is not { } token) return null;
			using var http = NewClient(TimeSpan.FromSeconds(10));
			http.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
			var json = await http.GetStringAsync($"https://api.github.com/repos/{TestRepo}/releases?per_page=20");
			var newest = (JsonNode.Parse(json) as JsonArray)?.OfType<JsonObject>()
				.Where(r => r["draft"]?.GetValue<bool>() != true)
				.Select(r => (Release: r, Version: AppVersion.Parse(r["tag_name"]?.GetValue<string>())))
				.Where(r => r.Version != null)
				.OrderByDescending(r => r.Version!.Value)
				.FirstOrDefault();
			Trace.Log("update", $"test repo: newest {newest?.Version?.ToString() ?? "none"}, this is {App.Version}");
			if (newest?.Version is not { } v) return null;
			if (newerOnly && (AppVersion.Parse(App.Version) is not { } c || !(v > c))) return null;
			var assets = (newest.Value.Release["assets"] as JsonArray)?.OfType<JsonObject>().ToList() ?? new();
			var zip = assets.FirstOrDefault(a => a["name"]?.GetValue<string>().EndsWith(ZipSuffix, StringComparison.OrdinalIgnoreCase) == true);
			// (the asset's API address: with the token and "octet-stream" it sends you on to the file)
			if (zip?["url"]?.GetValue<string>() is not { } url) return null;
			return new Release(v, url, zip["size"]?.GetValue<long>() ?? 0, null, true, AssetUrl(assets, zip["name"]!.GetValue<string>() + ".sig", true));
		}
		catch (Exception e)
		{
			GD.Print($"Test update check skipped: {e.Message}");
			Trace.Log("update", $"test repo check failed: {e.Message}");
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
					await DownloadChangedFiles(release, installDir, newFiles, exeName, Report);
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
	/// Finds the files this install doesn't have exactly (same size and SHA-256), downloads the packs they're in
	/// (a few at a time, checked against their SHA-256) and unpacks those files into newFiles (keeping the folders).
	/// </summary>
	static async Task DownloadChangedFiles(Release release, string installDir, string newFiles, string exeName, Action<float> report)
	{
		using var http = NewClient(TimeSpan.FromMinutes(30));
		// (the list says which files and packs, with their SHA-256: only a signed one is used)
		var listBytes = await http.GetByteArrayAsync(release.ManifestUrl);
		if (release.ManifestSigUrl == null || !SignedByMax(SHA256.HashData(listBytes), await http.GetByteArrayAsync(release.ManifestSigUrl)))
			throw new InvalidOperationException(NotSigned);
		var list = JsonNode.Parse(listBytes) ?? throw new InvalidOperationException("The update list is empty.");
		var files = (list["files"] as JsonArray)?.OfType<JsonObject>()
			.Select(f => new FileEntry(f["path"]!.GetValue<string>(), f["size"]!.GetValue<long>(), f["sha256"]!.GetValue<string>(), f["pack"]!.GetValue<string>()))
			.ToList() ?? throw new InvalidOperationException("The update's file list is empty.");
		var packs = (list["packs"] as JsonObject)?.ToDictionary(p => p.Key, p => new Pack(p.Value!["url"]!.GetValue<string>(),
			p.Value["size"]!.GetValue<long>(), p.Value["sha256"]!.GetValue<string>())) ?? new();
		if (files.All(f => f.Path != exeName)) throw new InvalidOperationException($"The update doesn't contain {exeName}.");

		var root = Path.GetFullPath(newFiles) + Path.DirectorySeparatorChar;
		var needed = files.Where(f => !SameFile(Path.Combine(installDir, f.Path), f)).ToList();
		var byPack = needed.GroupBy(f => f.Pack).ToList();
		if (byPack.Any(g => !packs.ContainsKey(g.Key))) throw new InvalidOperationException("The update list names a pack it doesn't have.");
		var total = Math.Max(1, byPack.Sum(g => packs[g.Key].Size));
		var done = new long[1];
		GD.Print($"Updating {needed.Count} of {files.Count} files from {byPack.Count} packs ({total / 1e6:0.0} MB).");
		await Parallel.ForEachAsync(byPack, new ParallelOptions { MaxDegreeOfParallelism = 3 }, async (group, ct) =>
		{
			var pack = packs[group.Key];
			var zipPath = Path.Combine(UpdateDir, Path.GetFileName(group.Key));
			using (var response = await http.GetAsync(pack.Url, HttpCompletionOption.ResponseHeadersRead, ct))
			{
				response.EnsureSuccessStatusCode();
				await using var from = await response.Content.ReadAsStreamAsync(ct);
				await using var to = File.Create(zipPath);
				var buffer = new byte[1 << 16];
				int read;
				while ((read = await from.ReadAsync(buffer, ct)) > 0)
				{
					await to.WriteAsync(buffer.AsMemory(0, read), ct);
					report((float)Interlocked.Add(ref done[0], read) / total * 0.95f);
				}
			}
			await using (var check = File.OpenRead(zipPath))
				if (!Convert.ToHexString(SHA256.HashData(check)).Equals(pack.Sha256, StringComparison.OrdinalIgnoreCase))
					throw new InvalidOperationException($"{group.Key} arrived damaged.");
			using (var zip = ZipFile.OpenRead(zipPath))
				foreach (var file in group)
				{
					var target = Path.GetFullPath(Path.Combine(newFiles, file.Path));
					if (!target.StartsWith(root, StringComparison.Ordinal)) throw new InvalidOperationException($"Bad file name in the update: {file.Path}");
					var entry = zip.GetEntry(file.Path) ?? throw new InvalidOperationException($"{group.Key} doesn't contain {file.Path}.");
					Directory.CreateDirectory(Path.GetDirectoryName(target)!);
					entry.ExtractToFile(target, true);
					if (!SameFile(target, file)) throw new InvalidOperationException($"{file.Path} arrived damaged.");
				}
			File.Delete(zipPath);
		});
	}

	static bool SameFile(string path, FileEntry file)
	{
		if (!File.Exists(path) || new FileInfo(path).Length != file.Size) return false;
		using var stream = File.OpenRead(path);
		return Convert.ToHexString(SHA256.HashData(stream)).Equals(file.Sha256, StringComparison.OrdinalIgnoreCase);
	}

	/// <summary>The whole build as a zip (for releases without an update list), unpacked into newFiles.</summary>
	static async Task DownloadZip(Release release, string newFiles, Action<float> report)
	{
		var zipPath = Path.Combine(UpdateDir, "update.zip");
		if (release.ZipSigUrl == null) throw new InvalidOperationException(NotSigned);
		// (the private test repo: the token, and the file itself rather than its description; GitHub sends the download on
		// to its file storage, and the token isn't passed on there)
		HttpRequestMessage Request(string url)
		{
			var request = new HttpRequestMessage(HttpMethod.Get, url);
			if (release.Private && ResultsUpload.Token is { } token)
			{
				request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
				request.Headers.Accept.ParseAdd("application/octet-stream");
			}
			return request;
		}
		byte[] sig;
		using (var http = NewClient(TimeSpan.FromMinutes(1)))
		using (var request = Request(release.ZipSigUrl))
		using (var response = await http.SendAsync(request))
		{
			response.EnsureSuccessStatusCode();
			sig = await response.Content.ReadAsByteArrayAsync();
		}
		using (var http = NewClient(TimeSpan.FromMinutes(30)))
		using (var request = Request(release.ZipUrl))
		{
			using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
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
		// (checked before a single file is unpacked)
		await using (var check = File.OpenRead(zipPath))
			if (!SignedByMax(SHA256.HashData(check), sig)) throw new InvalidOperationException(NotSigned);
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
