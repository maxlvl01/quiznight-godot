// Short share codes: "Copy share code" uploads the packed quiz (ShareCode.Pack) to Max's private results repo
// (shares/<code>.qn, with the game's token), and the code (QN-7K4P-9XZ2) is all a friend needs: pasting it downloads
// the quiz again. Without internet (or a token) the game falls back to the long code that holds the whole quiz.

using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using QuizNight.Shared;

namespace QuizNight.Storage;

public static class QuizShare
{
	public static bool Available => ResultsUpload.Token != null;

	static string Url(string key) => $"https://api.github.com/repos/{ResultsUpload.Repo}/contents/shares/{key}.qn";

	/// <summary>Uploads the quiz; returns its short code. Throws when it can't (no internet, no token, GitHub said no).</summary>
	public static Task<string> Upload(Quiz quiz)
	{
		var packed = ShareCode.Pack(quiz);
		// (off the main thread: awaiting the network there would crawl, one step per frame)
		return Task.Run(async () =>
		{
			var token = ResultsUpload.Token ?? throw new InvalidOperationException("No token");
			using var http = ResultsUpload.Client(token);
			// A new code until one is free (with 32^8 codes, practically always the first).
			for (var attempt = 0; attempt < 5; attempt++)
			{
				var code = ShareCode.NewShort();
				var body = new JsonObject { ["message"] = $"Share code {code}", ["content"] = Convert.ToBase64String(packed) }.ToJsonString();
				using var response = await http.PutAsync(Url(ShareCode.ShortKey(code)!), new StringContent(body, Encoding.UTF8, "application/json"));
				if (response.IsSuccessStatusCode) return code;
				if (response.StatusCode != HttpStatusCode.UnprocessableEntity) // (422: that code is taken)
					throw new HttpRequestException($"GitHub said {(int)response.StatusCode}");
			}
			throw new HttpRequestException("No free share code");
		});
	}

	/// <summary>The quiz behind a short code. Throws UserError with what went wrong.</summary>
	public static Task<Quiz> Download(string key) => Task.Run(async () =>
	{
		var token = ResultsUpload.Token ?? throw new UserError(Lang.T("This game can't look up share codes. Ask for the long code instead."));
		byte[] packed;
		try
		{
			using var http = ResultsUpload.Client(token, "application/vnd.github.raw");
			using var response = await http.GetAsync(Url(key));
			if (response.StatusCode == HttpStatusCode.NotFound) throw new UserError(Lang.T("There's no quiz with that share code. Check it and try again."));
			if (!response.IsSuccessStatusCode) throw new HttpRequestException($"GitHub said {(int)response.StatusCode}");
			packed = await response.Content.ReadAsByteArrayAsync();
		}
		catch (Exception e) when (e is HttpRequestException or TaskCanceledException)
		{
			throw new UserError(Lang.T("Couldn't look up the share code. Check your internet connection and try again."));
		}
		return ShareCode.Unpack(packed, out var error) ?? throw new UserError(error ?? QuizJson.NotAQuiz);
	});
}
