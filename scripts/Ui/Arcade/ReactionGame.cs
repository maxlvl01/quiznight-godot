// The reaction test for the lobby's mini games, alone and on your own PC (so the time has no network in it): press Start,
// wait for the buzzer to light up (a random 1.5–4 s), then buzz (click it or press Space) as fast as you can. Five tries;
// buzzing before it lights up is a false start. The best time goes to the host (ArcadeView.ReactionBest).

using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using QuizNight.Shared;
using QuizNight.Storage;
using QuizNight.Ui.Screens;

namespace QuizNight.Ui.Arcade;

public partial class ReactionGame : Control
{
	const int Tries = 5;

	readonly Action<int> _done;
	readonly BuzzButton _buzzer;
	readonly Label _status = U.Text("", 20, C.Text, Fonts.Bold, true, HorizontalAlignment.Center);
	readonly Label _times = U.Muted("", 15, true, HorizontalAlignment.Center);
	readonly Button _start;
	readonly List<int?> _results = new();
	ulong _litAt;
	bool _waiting;
	int _round;

	public ReactionGame() : this(_ => { }) { }

	public ReactionGame(Action<int> done)
	{
		_done = done;
		FocusMode = FocusModeEnum.All;
		CustomMinimumSize = new Vector2(0, 420);
		_buzzer = new BuzzButton(() => { }, 200) { Disabled = true, MouseFilter = MouseFilterEnum.Ignore };
		_start = U.Btn("Start", "AccentButton", Next, 18);
		var page = U.V(14, _status, _buzzer.Center(), _times, _start.Center());
		page.SetAnchorsPreset(LayoutPreset.FullRect);
		page.Alignment = BoxContainer.AlignmentMode.Center;
		page.MouseFilter = MouseFilterEnum.Ignore;
		AddChild(page);
		_status.Text = Lang.F("{0} tries. Buzz as soon as the buzzer lights up — not before!", Tries);
	}

	/// <summary>The next try: the buzzer goes dark, and lights up at a random moment.</summary>
	void Next()
	{
		if (_round >= Tries) Reset();
		_start.Visible = false;
		_buzzer.Disabled = true;
		_waiting = true;
		_litAt = 0;
		_status.Text = Lang.F("Try {0} of {1}: wait for it …", _round + 1, Tries);
		var round = _round;
		GetTree().CreateTimer(1.5 + GD.Randf() * 2.5).Timeout += () =>
		{
			if (!IsInstanceValid(this) || !_waiting || round != _round) return;
			_buzzer.Disabled = false;
			_litAt = Time.GetTicksMsec();
			_status.Text = Lang.T("NOW!");
		};
		GrabFocus();
	}

	void Reset()
	{
		_round = 0;
		_results.Clear();
	}

	/// <summary>A buzz: a time if it was lit, a false start if not.</summary>
	void Buzz()
	{
		if (!_waiting) return;
		_waiting = false;
		int? ms = _litAt > 0 ? (int)(Time.GetTicksMsec() - _litAt) : null;
		_results.Add(ms);
		_round++;
		_buzzer.Disabled = true;
		if (ms != null) Sfx.PlayBuzzer(Settings.BuzzerSound);
		_status.Text = ms is { } t ? Lang.F("{0} ms", t) : Lang.T("False start!");
		_times.Text = string.Join("   ", _results.Select(r => r is { } v ? $"{v} ms" : "✕"));
		var times = _results.OfType<int>().ToList();
		if (_round < Tries)
		{
			_start.Text = Lang.T("Next try");
			_start.Visible = true;
			return;
		}
		// (the best of the five goes to the lobby's list)
		if (times.Count > 0)
		{
			_status.Text = Lang.F("Best {0} ms · average {1} ms", times.Min(), (int)times.Average());
			_done(times.Min());
		}
		else _status.Text = Lang.T("Five false starts: patience!");
		_start.Text = Lang.T("Again");
		_start.Visible = true;
	}

	public override void _GuiInput(InputEvent e)
	{
		// (a click anywhere on the game counts, like a real reaction test: no aiming in it)
		if (e is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
		{
			Buzz();
			AcceptEvent();
		}
		else if (e is InputEventKey { Pressed: true, Echo: false, Keycode: Key.Space })
		{
			Buzz();
			AcceptEvent();
		}
	}
}

/// <summary>
/// A high score list like F1 timing: the leader with their time or score, everyone below with the gap to the leader and
/// the interval to the one just ahead; purple for the best of all time, green for a personal best.
/// </summary>
public static class F1Times
{
	public sealed record Entry(string Id, string Name, int Value, bool PersonalBest);

	/// <param name="lower">Lower is better (times).</param>
	/// <param name="record">The best of all time, to mark it purple.</param>
	public static Control List(IReadOnlyList<Entry> entries, bool lower, Func<int, string> show, Func<int, string> gap, int? record)
	{
		var grid = new GridContainer { Columns = 4 };
		grid.AddThemeConstantOverride("h_separation", 14);
		grid.AddThemeConstantOverride("v_separation", 4);
		if (entries.Count == 0) return U.Muted("Nobody yet.", 13);
		var leader = entries[0].Value;
		for (var i = 0; i < entries.Count; i++)
		{
			var e = entries[i];
			var colour = record == e.Value ? C.Purple : e.PersonalBest ? C.Lime : C.Text;
			grid.AddChild(U.Text($"{i + 1}", 14, C.Muted, Fonts.Bold));
			grid.AddChild(U.Text(e.Name, 14, C.Text, Fonts.Bold).Raw().Grow());
			grid.AddChild(U.Text(i == 0 ? show(e.Value) : "+" + gap(Math.Abs(e.Value - leader)), 14, colour, Fonts.Bold).Raw());
			grid.AddChild(U.Text(i == 0 ? "" : "+" + gap(Math.Abs(e.Value - entries[i - 1].Value)), 13, C.Muted).Raw());
		}
		return grid;
	}

	public static string Seconds(int ms) => (ms / 1000.0).ToString("0.000", System.Globalization.CultureInfo.InvariantCulture);
	public static string Points(int points) => points.ToString("N0");
}
