// Checks for anything that comes from outside this game: other players and hosts over the network, share codes, quiz
// files. Nothing from there is trusted to be the size or shape it claims: pictures whose header says they're enormous,
// packed quizzes that unpack to gigabytes, recordings that aren't the plain sound they should be, names that a
// spreadsheet would run as a formula.

using System;
using System.IO;
using System.Text;

namespace QuizNight.Shared;

public static class Guard
{
	/// <summary>The largest picture side the game decodes (a 4K screen is 3840 wide; quiz pictures are scaled to ≤ 2048).</summary>
	public const int MaxImageSide = 8192;
	/// <summary>And at most this many pixels (a 8192 × 8192 picture would be 256 MB in memory).</summary>
	public const long MaxImagePixels = 40_000_000;
	/// <summary>The most a quiz file (its JSON) may unpack to.</summary>
	public const int MaxQuizJsonBytes = 64 * 1024 * 1024;
	/// <summary>One shared file (sound, video, picture) a host may send a player: the editor allows 80 MB, encryption adds a little.</summary>
	public const long MaxSharedFileBytes = 96L * 1024 * 1024;
	/// <summary>All the files a host may send a player in one room.</summary>
	public const long MaxSharedTotalBytes = 3L * 1024 * 1024 * 1024;

	/// <summary>
	/// A picture's size from its header (PNG, JPEG, WebP or BMP), without decoding it. Null if it isn't one of those or the
	/// header is broken.
	/// </summary>
	public static (int Width, int Height)? ImageSize(ReadOnlySpan<byte> b)
	{
		try
		{
			// PNG: the IHDR chunk comes first
			if (b.Length >= 24 && b[0] == 0x89 && b[1] == 'P' && b[2] == 'N' && b[3] == 'G')
				return (BigEndian32(b, 16), BigEndian32(b, 20));
			// WebP: RIFF....WEBP, then VP8 (lossy), VP8L (lossless) or VP8X (extended)
			if (b.Length >= 30 && Ascii(b, 0, 4) == "RIFF" && Ascii(b, 8, 4) == "WEBP")
			{
				var chunk = Ascii(b, 12, 4);
				if (chunk == "VP8 ") return ((b[26] | b[27] << 8) & 0x3fff, (b[28] | b[29] << 8) & 0x3fff);
				if (chunk == "VP8L")
				{
					var bits = (uint)(b[21] | b[22] << 8 | b[23] << 16 | b[24] << 24);
					return ((int)(bits & 0x3fff) + 1, (int)((bits >> 14) & 0x3fff) + 1);
				}
				if (chunk == "VP8X") return ((b[24] | b[25] << 8 | b[26] << 16) + 1, (b[27] | b[28] << 8 | b[29] << 16) + 1);
				return null;
			}
			// BMP: width and height in the info header (height negative = top-down)
			if (b.Length >= 26 && b[0] == 'B' && b[1] == 'M')
				return (Math.Abs(BitConverter.ToInt32(b[18..22])), Math.Abs(BitConverter.ToInt32(b[22..26])));
			// JPEG: walk the markers to the frame header (SOF0–SOF15, not DHT/JPG/DAC)
			if (b.Length >= 4 && b[0] == 0xFF && b[1] == 0xD8)
			{
				var i = 2;
				while (i + 9 < b.Length)
				{
					if (b[i] != 0xFF) return null;
					var marker = b[i + 1];
					if (marker == 0xFF)
					{
						i++;
						continue;
					}
					var length = b[i + 2] << 8 | b[i + 3];
					if (marker is >= 0xC0 and <= 0xCF and not (0xC4 or 0xC8 or 0xCC))
						return (b[i + 7] << 8 | b[i + 8], b[i + 5] << 8 | b[i + 6]);
					if (length < 2) return null;
					i += 2 + length;
				}
			}
		}
		catch (Exception e) when (e is ArgumentException or IndexOutOfRangeException)
		{
		}
		return null;
	}

	/// <summary>A picture this game is willing to decode: a known kind, and not absurdly big.</summary>
	public static bool ImageOk(ReadOnlySpan<byte> bytes) =>
		ImageSize(bytes) is var (w, h) && w > 0 && h > 0 && w <= MaxImageSide && h <= MaxImageSide && (long)w * h <= MaxImagePixels;

	/// <summary>Reads a stream to its end, but no further than max bytes (else throws InvalidDataException).</summary>
	public static byte[] ReadCapped(Stream stream, long max)
	{
		using var into = new MemoryStream();
		var buffer = new byte[81920];
		int read;
		while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
		{
			if (into.Length + read > max) throw new InvalidDataException("It unpacks to more than a quiz can be.");
			into.Write(buffer, 0, read);
		}
		return into.ToArray();
	}

	/// <summary>
	/// A player's recording as the game makes it (Ui/MicRecorder): plain 16-bit PCM, mono, 16 kHz, its data right after the
	/// header. Anything else (other formats, odd chunks, lying sizes) is turned away before it goes to the other players.
	/// </summary>
	public static bool IsPlainWav(byte[] wav)
	{
		if (wav.Length < 44 || Ascii(wav, 0, 4) != "RIFF" || Ascii(wav, 8, 4) != "WAVE" || Ascii(wav, 12, 4) != "fmt " || Ascii(wav, 36, 4) != "data")
			return false;
		var fmtSize = BitConverter.ToInt32(wav, 16);
		var format = BitConverter.ToInt16(wav, 20);
		var channels = BitConverter.ToInt16(wav, 22);
		var rate = BitConverter.ToInt32(wav, 24);
		var byteRate = BitConverter.ToInt32(wav, 28);
		var align = BitConverter.ToInt16(wav, 32);
		var bits = BitConverter.ToInt16(wav, 34);
		var dataSize = BitConverter.ToInt32(wav, 40);
		return fmtSize == 16 && format == 1 && channels == 1 && rate == 16000 && bits == 16 && align == 2 && byteRate == 32000
			&& dataSize >= 0 && dataSize == wav.Length - 44;
	}

	/// <summary>
	/// A cell for a CSV that a spreadsheet won't run as a formula: text starting with = + - @ (or a tab or return) gets a '
	/// in front. CsvValue takes it off again when the game reads the file.
	/// </summary>
	public static string CsvSafe(string s) => s.Length > 0 && s[0] is '=' or '+' or '-' or '@' or '\t' or '\r' ? "'" + s : s;

	public static string CsvValue(string s) => s.Length > 1 && s[0] == '\'' && s[1] is '=' or '+' or '-' or '@' or '\t' or '\r' ? s[1..] : s;

	/// <summary>An id that's safe as a file name: letters, digits, - and _, 1 to 64 of them.</summary>
	public static bool SafeId(string id)
	{
		if (id.Length is 0 or > 64) return false;
		foreach (var c in id)
			if (!(c is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9' or '-' or '_')) return false;
		return true;
	}

	static int BigEndian32(ReadOnlySpan<byte> b, int at) => b[at] << 24 | b[at + 1] << 16 | b[at + 2] << 8 | b[at + 3];

	static string Ascii(ReadOnlySpan<byte> b, int at, int length) => Encoding.ASCII.GetString(b.Slice(at, length));
}
