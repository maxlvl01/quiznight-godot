// A player's copy of the game's videos: the host sends each one in pieces (VideoChunk), encrypted, from the
// start of the game. They're written to this game's own cache folder as they arrive and decrypted once the
// question is shown (its VideoKey), into an .ogv file Godot can play.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Godot;
using QuizNight.Shared;

namespace QuizNight.Net;

public sealed class ClientVideos : IDisposable
{
	sealed class Incoming
	{
		public required FileStream File;
		public long Total;
		public long Received;
		public readonly HashSet<long> Offsets = new();
	}

	// Each running copy of the game gets its own folder (several can run on one PC while testing).
	readonly string _dir = Path.Combine(OS.GetUserDataDir(), "cache", "videos", OS.GetProcessId().ToString());
	readonly Dictionary<string, Incoming> _incoming = new();
	readonly Dictionary<string, Task<string?>> _unlocking = new();
	readonly Dictionary<string, VideoStream?> _ready = new();

	public ClientVideos()
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
			GD.Print($"Couldn't clean up old videos: {e.Message}");
		}
	}

	public void Receive(byte[] msg)
	{
		if (!VideoChunk.TryDecode(msg, out var id, out var total, out var offset, out var data)) return;
		if (id.Length == 0 || id.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || id.Contains("..")) return;
		if (!_incoming.TryGetValue(id, out var file))
		{
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
	}

	/// <summary>How much of a video is here, 0–1 (0 when nothing arrived yet).</summary>
	public float Progress(string id) =>
		_incoming.TryGetValue(id, out var f) && f.Total > 0 ? Math.Min(1f, (float)f.Received / f.Total) : 0;

	/// <summary>The playable video once it's all here and unlocked; null until then (call again next frame).</summary>
	public VideoStream? Get(string id, string key)
	{
		if (_ready.TryGetValue(id, out var stream)) return stream;
		if (!_incoming.TryGetValue(id, out var file) || file.Received < file.Total) return null;
		if (!_unlocking.TryGetValue(id, out var task))
		{
			file.File.Flush();
			var enc = Path.Combine(_dir, id + ".enc");
			_unlocking[id] = Task.Run(() => Unlock(enc, Path.Combine(_dir, id + ".ogv"), key));
			return null;
		}
		if (!task.IsCompleted) return null;
		_unlocking.Remove(id);
		var path = task.IsCompletedSuccessfully ? task.Result : null;
		return _ready[id] = path != null ? new VideoStreamTheora { File = path } : null;
	}

	static string? Unlock(string encrypted, string ogv, string key)
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
			File.WriteAllBytes(ogv, aes.DecryptCbc(data.AsSpan(16), data.AsSpan(0, 16)));
			return ogv;
		}
		catch (Exception e)
		{
			GD.Print($"Couldn't unlock the video: {e.Message}");
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
			GD.Print($"Couldn't remove the game's videos: {e.Message}");
		}
	}
}
