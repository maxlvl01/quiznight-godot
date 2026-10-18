// A quiz with one question of every kind, made in code (generated images and a beep), for
// testing and for trying the game without building a quiz first.

using System;
using System.Collections.Generic;
using System.IO;
using Godot;
using QuizNight.Server;
using QuizNight.Shared;
using QuizNight.Storage;

namespace QuizNight.Dev;

public static class SampleQuiz
{
	public static Quiz Build()
	{
		var skyrim = new MapAsset { Id = "map-skyrim", Name = "Skyrim", PresetId = "skyrim", WidthKm = MapPresets.Get("skyrim")!.WidthKm };
		var tex = Images.BuiltinTexture(MapPresets.Get("skyrim")!.ImagePath);
		skyrim.Image = new ImageAsset { Width = tex?.GetWidth() ?? 4096, Height = tex?.GetHeight() ?? 4096 };
		var wow = new MapAsset { Id = "map-wow", Name = "WoW Classic – Azeroth", PresetId = WowAtlas.PresetId, Image = new ImageAsset { Width = WowAtlas.ImageWidth, Height = WowAtlas.ImageHeight } };
		var custom = new MapAsset { Id = "map-custom", Name = "Test grid", Image = Picture(1600, 1000, new Color("#2b6"), new Color("#135"), "GRID"), WidthKm = 10 };

		var q = new List<Question>
		{
			new()
			{
				Id = "q1", Type = QuestionType.Classic, Mode = ClassicMode.Buzzer, Prompt = "Which planet is known as the Red Planet?",
				AnswerText = "Mars", Points = 1, Image = Picture(1280, 720, new Color("#c1440e"), new Color("#2a0a05"), "?"),
			},
			new()
			{
				Id = "q2", Type = QuestionType.Classic, Mode = ClassicMode.Choice, Prompt = "Which of these is a prime number?",
				Choices = new() { "21", "27", "29", "33" }, CorrectChoice = 2, TimeLimitSec = 20, Points = 2,
			},
			new()
			{
				Id = "q3", Type = QuestionType.Estimation, Prompt = "How many keys does a standard piano have?", AnswerNumber = 88, Unit = "keys",
				TimeLimitSec = 30, Scoring = Scoring.Top3, Points = 1,
			},
			new()
			{
				Id = "q4", Type = QuestionType.MapPin, Prompt = "Where is Whiterun?", Image = Picture(1280, 720, new Color("#8a7a55"), new Color("#2c2a20"), "SCREENSHOT"),
				MapId = skyrim.Id, Target = new NormPoint(0.49, 0.52), TimeLimitSec = 60, Points = 1,
			},
			new()
			{
				Id = "q5", Type = QuestionType.MapPin, Prompt = "Where is Goldshire?", Image = Picture(1280, 720, new Color("#4a7a35"), new Color("#18240f"), "ELWYNN"),
				MapId = wow.Id, Target = WowAtlas.LocationToWorld(new WowAtlas.Location(0, -9465, 64)), TimeLimitSec = 60, Points = 1,
			},
			new()
			{
				Id = "q6", Type = QuestionType.MapPin, Prompt = "Find the yellow square", Image = Picture(800, 450, new Color("#f5b942"), new Color("#3a2a00"), "■"),
				MapId = custom.Id, Target = new NormPoint(0.7, 0.3), TimeLimitSec = 45, Points = 1,
			},
			new()
			{
				Id = "q7", Type = QuestionType.Sound, Prompt = "Name this sound!", AnswerText = "A beep", Points = 1,
				Media = new MediaAsset { Mime = "audio/wav", Name = "beep.wav", DataUrl = DataUrl.Make("audio/wav", Beep(4)), DurationSec = 4 },
			},
			new()
			{
				Id = "q8", Type = QuestionType.Classic, Mode = ClassicMode.Multi, Prompt = "Which of these are planets?",
				Choices = new() { "Mars", "Venus", "Pluto", "The Moon" }, CorrectChoices = new() { 0, 1 }, TimeLimitSec = 20, Points = 2,
			},
			new()
			{
				Id = "q9", Type = QuestionType.Image, Prompt = "Which game's map is this?", AnswerText = "Skyrim", Points = 1,
				Image = tex?.GetImage() is { } map ? Images.Encode((Image)map.Duplicate(), 1024) : Picture(1280, 720, new Color("#456"), new Color("#123"), "?"),
				RevealStyle = RevealStyle.Pixelate, RevealSec = 20, Focus = new NormPoint(0.5, 0.45),
			},
			new()
			{
				Id = "q10", Type = QuestionType.Order, Prompt = "Put these games in the order they came out",
				Items = new() { "Doom", "Half-Life", "Portal", "Elden Ring" }, OrderTop = "Oldest", OrderBottom = "Newest", TimeLimitSec = 30, Points = 2,
			},
			new()
			{
				Id = "q11", Type = QuestionType.Write, Prompt = "What's the capital of Australia?", AnswerText = "Canberra", TimeLimitSec = 30, Points = 1,
			},
		};
		return new Quiz
		{
			Id = "sample-quiz", Title = "Sample quiz night", CreatedAt = QuizStore.NowMs(), UpdatedAt = QuizStore.NowMs(),
			Maps = new() { skyrim, wow, custom }, Questions = q,
		};
	}

	/// <summary>A gradient picture with a big label, as a WebP data URL.</summary>
	public static ImageAsset Picture(int w, int h, Color a, Color b, string label)
	{
		var img = Image.CreateEmpty(w, h, false, Image.Format.Rgba8);
		for (var y = 0; y < h; y += 4)
		for (var x = 0; x < w; x += 4)
		{
			var t = (x / (float)w + y / (float)h) / 2;
			var c = a.Lerp(b, t);
			// grid lines every 100 px so maps have something to see
			if (x % 100 == 0 || y % 100 == 0) c = c.Lightened(0.25f);
			img.FillRect(new Rect2I(x, y, 4, 4), c);
		}
		var asset = Images.Encode(img, Math.Max(w, h));
		return asset;
	}

	/// <summary>A WAV file with a few seconds of a two-tone beep.</summary>
	public static byte[] Beep(double seconds)
	{
		const int rate = 22050;
		var samples = (int)(rate * seconds);
		using var ms = new MemoryStream();
		using var w = new BinaryWriter(ms);
		w.Write("RIFF"u8.ToArray());
		w.Write(36 + samples * 2);
		w.Write("WAVEfmt "u8.ToArray());
		w.Write(16);
		w.Write((short)1);
		w.Write((short)1);
		w.Write(rate);
		w.Write(rate * 2);
		w.Write((short)2);
		w.Write((short)16);
		w.Write("data"u8.ToArray());
		w.Write(samples * 2);
		for (var i = 0; i < samples; i++)
		{
			var t = i / (double)rate;
			var f = (int)(t * 2) % 2 == 0 ? 440 : 660;
			w.Write((short)(Math.Sin(2 * Math.PI * f * t) * 6000));
		}
		return ms.ToArray();
	}
}
