// The right/wrong sounds during a quiz (assets/soundeffects/Correct Answer.ogg and Wrong Answer.ogg), on the host's and
// every player's screen: when the host judges a buzz, when a timeline placement turns out right or wrong, and at the
// reveal of questions everyone answers (players hear their own result, the host's screen whether anyone got it). A
// question already judged buzz by buzz or placement by placement stays quiet at its reveal.

using System.Linq;
using QuizNight.Shared;
using QuizNight.Storage;

namespace QuizNight.Ui;

public sealed class AnswerSounds
{
	int _index = -1;
	int _judged;
	string _timeline = "";
	bool _revealed;
	/// <summary>Something in this question already played a sound (a judged buzz or placement).</summary>
	bool _heardLive;
	/// <summary>The first state after (re)joining only catches up, it plays nothing.</summary>
	bool _started;

	/// <param name="myId">The player whose result the reveal plays; null on the host's screen.</param>
	public void Update(GameView game, string? myId)
	{
		var judged = game.Buzzes.Where(b => b.Status is BuzzStatus.Correct or BuzzStatus.Wrong).ToList();
		var last = game.Timeline?.Last;
		var timeline = last is { Kind: TimelineEventKind.Right or TimelineEventKind.Wrong } ? $"{game.Timeline!.Placed.Count}|{last.Kind}|{last.PlayerId}|{last.Item}" : "";
		var reveal = game.Stage == Stage.Reveal ? game.Reveal : null;
		if (game.Index != _index)
		{
			_index = game.Index;
			_judged = _started ? 0 : judged.Count;
			_timeline = _started ? "" : timeline;
			_revealed = !_started && reveal != null;
			_heardLive = false;
		}
		_started = true;

		if (judged.Count > _judged)
		{
			Sfx.PlayAnswer(judged[^1].Status == BuzzStatus.Correct);
			_heardLive = true;
		}
		_judged = judged.Count;
		if (timeline != "" && timeline != _timeline)
		{
			Sfx.PlayAnswer(last!.Kind == TimelineEventKind.Right);
			_heardLive = true;
		}
		_timeline = timeline;

		if (reveal == null || _revealed) return;
		_revealed = true;
		if (_heardLive || judged.Count > 0) return;
		if (myId == null)
		{
			if (reveal.Entries.Count > 0) Sfx.PlayAnswer(reveal.Entries.Any(e => e.Correct || e.Points > 0));
		}
		else if (reveal.Entries.FirstOrDefault(e => e.PlayerId == myId) is { } mine && mine.Value != null)
			Sfx.PlayAnswer(mine.Correct || mine.Points > 0);
	}
}
