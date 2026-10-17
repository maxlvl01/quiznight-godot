// Sound and video files: reading them into a quiz, and turning them back into something Godot plays.
// Godot plays MP3, Ogg Vorbis and WAV sound, and Ogg Theora (.ogv) video — not MP4 or WebM.

using System;
using System.IO;
using Godot;
using QuizNight.Server;
using QuizNight.Shared;

namespace QuizNight.Storage;

public static class Media
{
	public static readonly string[] SoundFilters = { "*.mp3, *.ogg, *.wav ; Sound files" };
	// MP4 & co. are listed so they can be picked: picking one opens the conversion guide (VideoGuide).
	public static readonly string[] VideoFilters = { "*.ogv, *.mp4, *.webm, *.mov, *.mkv, *.avi, *.m4v ; Videos" };

	public const string VideoHelp =
		"Godot can only play .ogv (Ogg Theora) videos. Convert MP4/WebM files first, e.g. with ffmpeg: " +
		"ffmpeg -i trailer.mp4 -q:v 7 -q:a 5 trailer.ogv";

	/// <summary>A video Godot can't play but ffmpeg can convert to .ogv.</summary>
	public static bool IsConvertibleVideo(string path) =>
		path.GetExtension().ToLowerInvariant() is "mp4" or "webm" or "mov" or "mkv" or "avi" or "m4v";

	/// <summary>
	/// ffmpeg command for an .ogv Godot plays well: Theora video at most 1280 px wide (smaller videos keep
	/// their size), Vorbis audio, good quality. -y overwrites an older conversion with the same name.
	/// </summary>
	public static string ConvertCommand(string input, string output) =>
		$"ffmpeg -y -i \"{input}\" -vf \"scale='min(1280,iw)':-2\" -c:v libtheora -q:v 7 -c:a libvorbis -q:a 5 \"{output}\"";

	static string MimeFor(string ext) => ext switch
	{
		"mp3" => "audio/mpeg",
		"ogg" or "oga" => "audio/ogg",
		"wav" => "audio/wav",
		"ogv" => "video/ogg",
		_ => "",
	};

	/// <summary>Reads a sound or video file as-is and measures its length. Throws UserError.</summary>
	public static MediaAsset ReadFile(string path, string questionType)
	{
		var ext = path.GetExtension().ToLowerInvariant();
		var mime = MimeFor(ext);
		var isSound = questionType == QuestionType.Sound;
		if (isSound && !mime.StartsWith("audio/")) throw new UserError("Please pick a sound file (mp3, ogg or wav).");
		if (!isSound && mime != "video/ogg")
			throw new UserError(ext is "mp4" or "webm" or "mov" or "mkv" ? VideoHelp : "Please pick an .ogv video file.");
		var size = new FileInfo(path).Length;
		var maxMb = QuizRules.MediaMaxMb(questionType);
		if (size > maxMb * 1024L * 1024L)
			throw new UserError(Lang.F("That file is {0} MB. The limit is {1} MB — cut or compress it first.", (size / 1048576.0).ToString("0"), maxMb));
		var bytes = File.ReadAllBytes(path);
		var asset = new MediaAsset { DataUrl = DataUrl.Make(mime, bytes), Mime = mime, Name = Path.GetFileName(path) };
		if (isSound)
		{
			var stream = LoadSound(asset) ?? throw new UserError("Godot couldn't read that sound file. Try converting it to MP3 or OGG.");
			var len = stream.GetLength();
			asset.DurationSec = len > 0 ? len : null;
		}
		return asset;
	}

	/// <summary>An AudioStream for a sound question, or null if Godot can't play that format.</summary>
	public static AudioStream? LoadSound(MediaAsset media)
	{
		var bytes = DataUrl.Bytes(media.DataUrl, out var mime);
		if (bytes == null) return null;
		try
		{
			return (mime == "" ? media.Mime : mime) switch
			{
				"audio/mpeg" or "audio/mp3" => new AudioStreamMP3 { Data = bytes },
				"audio/ogg" or "audio/vorbis" => AudioStreamOggVorbis.LoadFromBuffer(bytes),
				"audio/wav" or "audio/x-wav" or "audio/wave" => AudioStreamWav.LoadFromBuffer(bytes),
				_ => null,
			};
		}
		catch (Exception e)
		{
			GD.PrintErr($"Couldn't load sound: {e.Message}");
			return null;
		}
	}

	public static bool CanPlayVideo(MediaAsset media) => media.Mime == "video/ogg" || DataUrl.TryParse(media.DataUrl, out var m, out _) && m == "video/ogg";

	/// <summary>Theora streams need a file, so the video is written to the cache folder first.</summary>
	public static VideoStream? LoadVideo(MediaAsset media, string cacheName)
	{
		if (!CanPlayVideo(media)) return null;
		var bytes = DataUrl.Bytes(media.DataUrl, out _);
		if (bytes == null) return null;
		DirAccess.MakeDirRecursiveAbsolute("user://cache");
		var path = $"user://cache/{cacheName}.ogv";
		using (var f = Godot.FileAccess.Open(path, Godot.FileAccess.ModeFlags.Write))
		{
			if (f == null) return null;
			f.StoreBuffer(bytes);
		}
		return new VideoStreamTheora { File = path };
	}

	public static bool IsMediaPath(string path) => MimeFor(path.GetExtension().ToLowerInvariant()) != "";
}
