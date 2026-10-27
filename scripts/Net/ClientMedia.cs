// A player's copy of the game's sound and video files: the host sends each one in pieces (MediaChunk),
// encrypted, from the start of the game. They're written to this game's own cache folder as they arrive and
// decrypted once the question is shown (its MediaKey). The host hears how far each download is (Reported).

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Godot;
using QuizNight.Shared;
using QuizNight.Storage;

namespace QuizNight.Net;

public sealed class ClientMedia : IDisposable
{
	sealed class Incoming
	{
		public required FileStream File;
		public long Total;
		public long Received;
		public readonly HashSet<long> Offsets = new();
		public int Reported = -1;
	}

	/// <summary>A download got further (id, percent in steps of 5): the host shows it before starting the media.</summary>
	public event Action<string, int>? ProgressChanged;

	// Each player in each running copy of the game gets its own folder (several can run on one PC while testing).
	static int _count;
	readonly string _dir = Path.Combine(OS.GetUserDataDir(), "cache", "media", $"{OS.GetProcessId()}-{System.Threading.Interlocked.Increment(ref _count)}");
	readonly Dictionary<string, Incoming> _incoming = new();
	readonly Dictionary<string, Task<string?>> _unlocking = new();
	readonly Dictionary<string, Resource?> _ready = new();
	/// <summary>How much the host has said it will send (Guard.MaxSharedTotalBytes at most: a host can't fill the disk).</summary>
	long _promised;

	public ClientMedia()
	{
		CleanUp(Path.GetDirectoryName(_dir)!);
		Directory.CreateDirectory(_dir);
	}

	/// <summary>Removes folders other copies of the game left behind (older than a day).</summary>
	static void CleanUp(string root)
	{
		try
		{
			if (!Directory.Exists(root)) return;
			foreach (var dir in Directory.GetDirectories(root))
				if (Directory.GetLastWriteTimeUtc(dir) < DateTime.UtcNow.AddDays(-1)) Directory.Delete(dir, true);
		}
		catch (Exception e)
		{
			GD.Print($"Couldn't clean up old media files: {e.Message}");
		}
	}

	public void Receive(byte[] msg)
	{
		if (!MediaChunk.TryDecode(msg, out var id, out var total, out var offset, out var data)) return;
		// (the id becomes a file name: only the plain ids the game makes)
		if (!Guard.SafeId(id)) return;
		if (!_incoming.TryGetValue(id, out var file))
		{
			if (total > Guard.MaxSharedFileBytes || _promised + total > Guard.MaxSharedTotalBytes || _incoming.Count >= 1000) return;
			_promised += total;
			file = new Incoming { File = new FileStream(Path.Combine(_dir, id + ".enc"), FileMode.Create, System.IO.FileAccess.ReadWrite), Total = total };
			file.File.SetLength(total);
			_incoming[id] = file;
		}
		// (after a reconnect the host starts over: pieces we already have are skipped)
		if (file.Total != total || !file.Offsets.Add(offset)) return;
		file.File.Position = offset;
		file.File.Write(data.Span);
		file.Received += data.Length;
		if (file.Received >= file.Total) file.File.Flush();
		var percent = (int)(file.Received * 100 / file.Total) / 5 * 5;
		if (percent != file.Reported)
		{
			file.Reported = percent;
			ProgressChanged?.Invoke(id, percent);
		}
	}

	/// <summary>Every download's progress, to tell the host again after a reconnect.</summary>
	public IEnumerable<(string Id, int Percent)> AllProgress()
	{
		foreach (var (id, f) in _incoming) yield return (id, Math.Max(0, f.Reported));
	}

	/// <summary>How much of a video is here, 0–1 (0 when nothing arrived yet).</summary>
	public float Progress(string id) =>
		_incoming.TryGetValue(id, out var f) && f.Total > 0 ? Math.Min(1f, (float)f.Received / f.Total) : 0;

	/// <summary>The playable video once it's all here and unlocked; null until then (call again next frame).</summary>
	public VideoStream? GetVideo(string id, string key) =>
		Get(id, key, ".ogv", path => new VideoStreamTheora { File = path }) as VideoStream;

	/// <summary>A picture (question image, slide or map), the same way.</summary>
	public Texture2D? GetImage(string id, string key, string mime) =>
		Get(id, key, ".img", path => Images.TextureFromBytes(System.IO.File.ReadAllBytes(path), mime)) as Texture2D;

	/// <summary>The file is all here but couldn't be unlocked or read (then it's fetched another way).</summary>
	public bool Failed(string id) => _ready.TryGetValue(id, out var res) && res == null;

	/// <summary>The sound, the same way (mime: its file type, from the question).</summary>
	public AudioStream? GetSound(string id, string key, string mime) =>
		Get(id, key, ".sound", path => Storage.Media.LoadSound(System.IO.File.ReadAllBytes(path), mime)) as AudioStream;

	Resource? Get(string id, string key, string ext, Func<string, Resource?> load)
	{
		if (_ready.TryGetValue(id, out var res)) return res;
		if (!_incoming.TryGetValue(id, out var file) || file.Received < file.Total) return null;
		if (!_unlocking.TryGetValue(id, out var task))
		{
			file.File.Flush();
			var enc = Path.Combine(_dir, id + ".enc");
			_unlocking[id] = Task.Run(() => Unlock(enc, Path.Combine(_dir, id + ext), key));
			return null;
		}
		if (!task.IsCompleted) return null;
		_unlocking.Remove(id);
		var path = task.IsCompletedSuccessfully ? task.Result : null;
		return _ready[id] = path != null ? load(path) : null;
	}

	static string? Unlock(string encrypted, string target, string key)
	{
		try
		{
			byte[] data;
			using (var f = new FileStream(encrypted, FileMode.Open, System.IO.FileAccess.Read, FileShare.ReadWrite))
			{
				data = new byte[f.Length];
				f.ReadExactly(data);
			}
			using var aes = System.Security.Cryptography.Aes.Create();
			aes.Key = Convert.FromBase64String(key);
			File.WriteAllBytes(target, aes.DecryptCbc(data.AsSpan(16), data.AsSpan(0, 16)));
			return target;
		}
		catch (Exception e)
		{
			GD.Print($"Couldn't unlock the file: {e.Message}");
			return null;
		}
	}

	public void Dispose()
	{
		foreach (var f in _incoming.Values) f.File.Dispose();
		_incoming.Clear();
		try
		{
			if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
		}
		catch (Exception e)
		{
			GD.Print($"Couldn't remove the game's media files: {e.Message}");
		}
	}
}
