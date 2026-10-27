// Detailed logging for test builds: what happens, with the time to the millisecond, so the log a test build sends after
// each session (Storage/CrashReports) shows even small things — screens, connections, messages, the game's steps. Off
// in public builds, the editor and the self-test (App switches it on for test builds).

using System;
using Godot;

namespace QuizNight.Shared;

public static class Trace
{
	public static bool On;

	/// <summary>"[20:14:03.512] [area] text" in the log, when tracing is on.</summary>
	public static void Log(string area, string text)
	{
		if (On) GD.Print($"[{DateTime.Now:HH:mm:ss.fff}] [{area}] {text}");
	}
}
