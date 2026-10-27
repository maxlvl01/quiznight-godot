// Undo and redo for the editors (Ctrl+Z, Ctrl+Y or Ctrl+Shift+Z; App routes the keys): a list of snapshots of what's
// being edited, one per change. Typing merges into one step while it goes on (a pause of a second starts the next), so
// one Ctrl+Z takes back a word or a sentence, not a letter. A text field with the keyboard focus undoes its own typing
// first (Godot's LineEdit/TextEdit take the keys); these take back everything else.

using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using Godot;
using QuizNight.Shared;

namespace QuizNight.Ui;

public sealed class EditHistory<T>
{
	const int MaxSteps = 100;
	const double MergeWithinSec = 1.0;

	readonly List<T> _states = new();
	readonly Func<T> _take;
	readonly Action<T> _restore;
	int _pos;
	double _lastAt;
	bool _lastMerges;
	bool _restoring;

	/// <param name="take">A snapshot of how things are now.</param>
	/// <param name="restore">Puts a snapshot back (and shows it).</param>
	public EditHistory(Func<T> take, Action<T> restore)
	{
		_take = take;
		_restore = restore;
		_states.Add(take());
	}

	public bool CanUndo => _pos > 0;
	public bool CanRedo => _pos < _states.Count - 1;

	/// <summary>Something changed. mergeable: typing, which joins the step before it if that was typing a moment ago.</summary>
	public void Record(bool mergeable = false)
	{
		if (_restoring) return;
		var now = Time.GetTicksMsec() / 1000.0;
		var state = _take();
		if (mergeable && _lastMerges && now - _lastAt < MergeWithinSec && _pos > 0 && _pos == _states.Count - 1) _states[_pos] = state;
		else
		{
			// (a new change after some undos: the undone steps can't be redone any more)
			_states.RemoveRange(_pos + 1, _states.Count - _pos - 1);
			_states.Add(state);
			if (_states.Count > MaxSteps) _states.RemoveAt(0);
			_pos = _states.Count - 1;
		}
		_lastAt = now;
		_lastMerges = mergeable;
	}

	public bool Undo()
	{
		if (!CanUndo) return false;
		_pos--;
		Apply();
		return true;
	}

	public bool Redo()
	{
		if (!CanRedo) return false;
		_pos++;
		Apply();
		return true;
	}

	void Apply()
	{
		_restoring = true;
		try
		{
			_restore(_states[_pos]);
		}
		finally
		{
			_restoring = false;
		}
		_lastMerges = false;
	}
}

/// <summary>
/// A question as an undo step, small however big its pictures and sound are: the question's JSON with every long text
/// (the data URLs) kept aside as the very same strings the question has, not copies.
/// </summary>
public sealed class QuestionSnapshot
{
	readonly string _json;
	readonly List<string> _blobs;
	readonly List<MapAsset> _maps;

	QuestionSnapshot(string json, List<string> blobs, List<MapAsset> maps)
	{
		_json = json;
		_blobs = blobs;
		_maps = maps;
	}

	const string BlobMark = "\u0001blob:";

	public static QuestionSnapshot Take(Question q, List<MapAsset> maps)
	{
		var blobs = new List<string>();
		var json = QuizJson.WriteQuestion(q);
		Walk(json, s =>
		{
			if (s.Length < 512) return null;
			// (the same string again: kept once)
			var at = blobs.FindIndex(b => ReferenceEquals(b, s));
			if (at < 0)
			{
				blobs.Add(s);
				at = blobs.Count - 1;
			}
			return BlobMark + at;
		});
		// (map pictures are shared strings too: MapAsset.Clone keeps the same data URL)
		return new QuestionSnapshot(json.ToJsonString(), blobs, maps.ConvertAll(m => m.Clone()));
	}

	public (Question Question, List<MapAsset> Maps) Restore()
	{
		var json = (JsonObject)JsonNode.Parse(_json)!;
		Walk(json, s => s.StartsWith(BlobMark) && int.TryParse(s.AsSpan(BlobMark.Length), out var i) && i < _blobs.Count ? _blobs[i] : null);
		return (QuizJson.ReadQuestion(json), _maps.ConvertAll(m => m.Clone()));
	}

	/// <summary>Every string value in the JSON: replaced by what change returns, unless it returns null.</summary>
	static void Walk(JsonNode? node, Func<string, string?> change)
	{
		switch (node)
		{
			case JsonObject o:
				foreach (var key in new List<string>(((IDictionary<string, JsonNode?>)o).Keys))
				{
					if (o[key] is JsonValue v && v.TryGetValue<string>(out var s))
					{
						if (change(s) is { } to) o[key] = to;
					}
					else Walk(o[key], change);
				}
				break;
			case JsonArray a:
				for (var i = 0; i < a.Count; i++)
				{
					if (a[i] is JsonValue v && v.TryGetValue<string>(out var s))
					{
						if (change(s) is { } to) a[i] = to;
					}
					else Walk(a[i], change);
				}
				break;
		}
	}
}
