// Turning image files, the clipboard and data URLs into ImageAssets and textures.

using System;
using System.Collections.Generic;
using Godot;
using QuizNight.Server;
using QuizNight.Shared;

namespace QuizNight.Storage;

public static class Images
{
	/// <summary>Max longest side in pixels. Maps stay sharper because players zoom into them.</summary>
	public const int ScreenshotMax = 1920;
	public const int MapMax = 4096;

	public static readonly string[] FileFilters = { "*.png, *.jpg, *.jpeg, *.webp, *.bmp, *.tga ; Images" };

	/// <summary>Decodes image bytes by mime type (falls back to trying every format).</summary>
	public static Image? Decode(byte[] bytes, string mime)
	{
		var img = new Image();
		Error Try(string kind) => kind switch
		{
			"webp" => img.LoadWebpFromBuffer(bytes),
			"png" => img.LoadPngFromBuffer(bytes),
			"jpg" => img.LoadJpgFromBuffer(bytes),
			"bmp" => img.LoadBmpFromBuffer(bytes),
			_ => Error.Unavailable,
		};
		var first = mime switch
		{
			"image/webp" => "webp",
			"image/png" => "png",
			"image/jpeg" or "image/jpg" => "jpg",
			"image/bmp" => "bmp",
			_ => "",
		};
		if (first != "" && Try(first) == Error.Ok) return img;
		foreach (var kind in new[] { "webp", "png", "jpg", "bmp" })
			if (kind != first && Try(kind) == Error.Ok) return img;
		return null;
	}

	public static Texture2D? TextureFromDataUrl(string dataUrl)
	{
		var bytes = DataUrl.Bytes(dataUrl, out var mime);
		var img = bytes == null ? null : Decode(bytes, mime);
		if (img == null) return null;
		img.GenerateMipmaps();
		return ImageTexture.CreateFromImage(img);
	}

	/// <summary>Scales an image down to maxSide and re-encodes it as WebP so quizzes stay small.</summary>
	public static ImageAsset Encode(Image img, int maxSide)
	{
		if (img.IsCompressed()) img.Decompress();
		if (img.GetFormat() != Image.Format.Rgba8 && img.GetFormat() != Image.Format.Rgb8) img.Convert(Image.Format.Rgba8);
		var scale = Math.Min(1.0, (double)maxSide / Math.Max(img.GetWidth(), img.GetHeight()));
		if (scale < 1) img.Resize((int)Math.Round(img.GetWidth() * scale), (int)Math.Round(img.GetHeight() * scale), Image.Interpolation.Lanczos);
		var bytes = img.SaveWebpToBuffer(true, 0.85f);
		return new ImageAsset { DataUrl = DataUrl.Make("image/webp", bytes), Width = img.GetWidth(), Height = img.GetHeight() };
	}

	/// <summary>Reads an image file from disk. Throws with a message the user can read.</summary>
	public static ImageAsset ReadFile(string path, int maxSide)
	{
		var img = Image.LoadFromFile(path);
		if (img == null || img.IsEmpty()) throw new UserError("Could not read that image. Try a PNG, JPG or WebP file.");
		return Encode(img, maxSide);
	}

	/// <summary>An image on the clipboard (e.g. a Win+Shift+S screenshot), or null.</summary>
	public static ImageAsset? FromClipboard(int maxSide)
	{
		if (!DisplayServer.ClipboardHasImage()) return null;
		var img = DisplayServer.ClipboardGetImage();
		return img == null || img.IsEmpty() ? null : Encode(img, maxSide);
	}

	static readonly Dictionary<string, Texture2D?> Builtin = new();

	/// <summary>An image that ships with the game (built-in maps). Cached.</summary>
	public static Texture2D? BuiltinTexture(string path)
	{
		if (!Builtin.TryGetValue(path, out var tex))
		{
			tex = ResourceLoader.Exists(path) ? GD.Load<Texture2D>(path) : null;
			Builtin[path] = tex;
		}
		return tex;
	}

	public static bool IsImagePath(string path) =>
		path.GetExtension().ToLowerInvariant() is "png" or "jpg" or "jpeg" or "webp" or "bmp" or "tga";
}

/// <summary>An error whose message is meant for the user.</summary>
public sealed class UserError(string message) : Exception(message);
