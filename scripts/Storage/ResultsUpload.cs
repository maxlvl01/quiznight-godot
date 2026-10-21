// Uploads a finished quiz night's results to Max's private GitHub repo (maxlvl01/quiznight-results), so he gets them
// even from nights he didn't play in. The game carries a token that can only write to that one repo
// (results-token.txt, not in git, shipped in the build); without it nothing is uploaded.

using System;
using System.Net.Http;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Godot;

namespace QuizNight.Storage;

public static class ResultsUpload
{
	internal const string Repo = "maxlvl01/quiznight-results";
	const string TokenFile = "res://results-token.txt";

	/// <summary>The token that can read and write that repo (also for short share codes: QuizShare).</summary>
	internal static string? Token
	{
		get
		{
			if (!Godot.FileAccess.FileExists(TokenFile)) return null;
			var token = Godot.FileAccess.GetFileAsString(TokenFile).Trim();
			return token == "" ? null : token;
		}
	}

	/// <summary>
	/// Only real games: an exported build (test runs from the editor, with bots, never upload) over Steam, with a token.
	/// </summary>
	public static bool Enabled => OS.HasFeature("template") && Token != null;

	/// <summary>Uploads in the background. If that fails (e.g. no internet), the host's PC keeps them (Results.Folder) so the night isn't lost.</summary>
	public static void Send(string name, string csv)
	{
		if (Token is not { } token) return;
		_ = Task.Run(async () =>
		{
			try
			{
				await Put(token, name, csv);
			}
			catch (Exception e)
			{
				GD.PrintErr($"Couldn't upload the results: {e.Message}");
				Results.Save(name, csv);
			}
		});
	}

	static async Task Put(string token, string name, string csv)
	{
		using var http = Client(token);
		var body = new JsonObject
		{
			["message"] = $"Results: {name}",
			["content"] = Convert.ToBase64String(Encoding.UTF8.GetBytes(csv)),
		}.ToJsonString();
		// (a random tail: two nights saved in the same minute never overwrite each other)
		var file = $"{System.IO.Path.GetFileNameWithoutExtension(name)} {Guid.NewGuid().ToString("N")[..6]}.csv";
		var url = $"https://api.github.com/repos/{Repo}/contents/results/{Uri.EscapeDataString(file)}";
		using var response = await http.PutAsync(url, new StringContent(body, Encoding.UTF8, "application/json"));
		if (!response.IsSuccessStatusCode) throw new HttpRequestException($"GitHub said {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
	}

	/// <summary>A client for GitHub's API, signed in with the token.</summary>
	internal static System.Net.Http.HttpClient Client(string token, string accept = "application/vnd.github+json")
	{
		var http = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(60) };
		http.DefaultRequestHeaders.UserAgent.ParseAdd("Quiznight");
		http.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
		http.DefaultRequestHeaders.Accept.ParseAdd(accept);
		return http;
	}
}
