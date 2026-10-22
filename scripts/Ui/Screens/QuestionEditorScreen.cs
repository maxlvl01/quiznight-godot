using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using QuizNight.Shared;
using QuizNight.Storage;

namespace QuizNight.Ui.Screens;

/// <summary>Edits one question. Changes stay in a draft until "Save question", so backing out discards them.</summary>
public partial class QuestionEditorScreen : Control
{
	readonly Quiz _quiz;
	readonly Question _draft;
	readonly List<MapAsset> _maps;
	readonly bool _isNew;
	bool _dirty;
	readonly Label _issues = U.Text("", 13, C.Muted, null, true);
	readonly VBoxContainer _form = U.V(16);
	ImageField? _primaryImage;
	MediaField? _mediaField;

	public QuestionEditorScreen() : this(new Quiz(), QuizRules.NewQuestion(QuestionType.Classic, "x"), true) { }

	public QuestionEditorScreen(Quiz quiz, Question question, bool isNew)
	{
		_quiz = quiz;
		_draft = question.Clone();
		_maps = quiz.Maps.Select(m => m.Clone()).ToList();
		_isNew = isNew;
		SetAnchorsPreset(LayoutPreset.FullRect);

		var title = QuestionType.Infos[_draft.Type].Label + (_draft.Type is QuestionType.MapPin or QuestionType.Estimation or QuestionType.Sound or QuestionType.Video ? " question" : "");
		var bar = U.Panel(U.H(12,
			U.Link("← Back to quiz", Back, 14),
			U.Heading(title, 22, false).Grow().With(l => l.HorizontalAlignment = HorizontalAlignment.Center),
			U.Btn("Save question", "AccentButton", Save, 15)), "Bar");
		var body = U.V(12, _issues, _form);
		var scroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled }.GrowV();
		scroll.AddChild(U.Pad(body.Grow().GrowV(), 28, 16, 28, 28).Grow().GrowV());
		_form.SizeFlagsVertical = SizeFlags.ExpandFill;
		var layout = U.V(0, bar, scroll);
		layout.SetAnchorsPreset(LayoutPreset.FullRect);
		AddChild(layout);
		BuildForm();
	}

	void Changed(bool rebuild = false)
	{
		_dirty = true;
		if (rebuild) BuildForm();
		else UpdateIssues();
	}

	void UpdateIssues()
	{
		var issues = QuizRules.QuestionIssues(_draft, _maps);
		if (issues.Count == 0)
		{
			_issues.Text = "✓ Ready to play.";
			_issues.AddThemeColorOverride("font_color", C.Teal);
		}
		else
		{
			_issues.Text = Lang.F("Still missing: {0} You can save anyway and finish it later.", string.Join(" ", issues));
			_issues.AddThemeColorOverride("font_color", C.Muted);
		}
	}

	void BuildForm()
	{
		_form.Clear();
		_primaryImage = null;
		_mediaField = null;
		_form.AddChild(_draft.Type switch
		{
			QuestionType.Classic => ClassicForm(),
			QuestionType.Estimation => EstimationForm(),
			QuestionType.MapPin => MapPinForm(),
			QuestionType.Image => ImageRevealForm(),
			QuestionType.Order => OrderForm(),
			QuestionType.Write => WriteForm(),
			_ => MediaForm(),
		});
		// Maps and previews take the height that's left, so the form fits without scrolling.
		U.PropagateGrow(_form);
		UpdateIssues();
	}

	static HBoxContainer Columns(params (Control Col, float Ratio)[] cols)
	{
		var row = U.H(24);
		foreach (var (col, ratio) in cols)
		{
			col.SizeFlagsHorizontal = SizeFlags.ExpandFill;
			col.SizeFlagsStretchRatio = ratio;
			row.AddChild(col);
		}
		return row;
	}

	Control PromptArea(string placeholder) =>
		U.Field("Question", U.Area(_draft.Prompt, placeholder, t =>
		{
			_draft.Prompt = t;
			Changed();
		}, 2, QuestionLimits.PromptMaxLength));

	Control PromptLine() =>
		U.Field("Question text", U.Input(_draft.Prompt, "", t =>
		{
			_draft.Prompt = t;
			Changed();
		}, QuestionLimits.PromptMaxLength));

	ImageField OptionalImage()
	{
		_primaryImage = new ImageField(_draft.Image, img =>
		{
			_draft.Image = img;
			Changed();
		}, Images.ScreenshotMax, "No image (optional)");
		return _primaryImage;
	}

	// ---------- classic ----------

	Control ClassicForm()
	{
		var choice = _draft.IsChoice;
		var left = U.V(14, PromptArea("e.g. How many hours did we play last month?"),
			U.Segmented(new[]
				{
					(ClassicMode.Buzzer, "Buzzer — first to buzz"),
					(Typed, "Everyone types the answer"),
					(ClassicMode.Choice, "Pick one — one is right"),
					(ClassicMode.Multi, "Multiple choice — several right"),
				},
				_draft.IsTyped ? Typed : _draft.Mode, mode =>
				{
					_draft.Typed = mode == Typed;
					if (mode == Typed) mode = ClassicMode.Buzzer;
					// Keep the marked answer when switching between the two choice modes.
					if (mode == ClassicMode.Multi && _draft.CorrectChoices.Count == 0) _draft.CorrectChoices = new() { _draft.CorrectChoice };
					if (mode == ClassicMode.Choice && _draft.CorrectChoices.Count > 0) _draft.CorrectChoice = _draft.CorrectChoices.Min();
					_draft.Mode = mode;
					Changed(true);
				}));
		if (choice)
		{
			var rows = U.V(8, U.Muted(_draft.IsMulti
				? "Answers — tick every correct one. Leave answers empty to use fewer."
				: "Answers — tick the correct one. Leave answers empty to use fewer.", 13));
			for (var i = 0; i < 4; i++) rows.AddChild(ChoiceRow(i));
			rows.AddChild(U.Muted("The order doesn't matter: answers are shuffled every time you host, so players can't learn where the right one usually is.", 13));
			left.AddChild(rows);
		}
		else
		{
			left.AddChild(U.Field("Correct answer (only you see this)", U.Input(_draft.AnswerText, "", t =>
			{
				_draft.AnswerText = t;
				Changed();
			}).With(e => e.AddThemeFontOverride("font", Fonts.Bold))));
		}
		left.AddChild(U.Field("Image (optional)", OptionalImage()));

		var settings = new List<Control>();
		if (choice || _draft.IsTyped) settings.Add(RoundSettings.TimeLimit(_draft, () => Changed()));
		settings.Add(RoundSettings.Points(_draft, () => Changed()));
		settings.Add(RoundSettings.Bonus(_draft, () => Changed()));
		settings.Add(U.Muted(_draft.IsTyped ? TypedHelp : _draft.IsMulti
			? "Everyone picks all the answers they think are right on their own screen. Only players who pick exactly the right ones get the points. The answer is revealed when everyone has answered, the time runs out, or you reveal it."
			: choice
			? "Everyone picks A, B, C or D on their own screen. Everyone who picks the right one gets the points. The answer is revealed when everyone has answered, the time runs out, or you reveal it."
			: "Players buzz in; the fastest answers first. You mark answers Correct or Wrong, and on Wrong the next in line gets a go. A correct answer reveals it; if nobody knows, you reveal it. Answer time per buzz is set in the lobby.", 13));
		return Columns((left, 1.6f), (RoundSettings.Panel(settings.ToArray()), 1));
	}

	const string Typed = "typed";
	const string TypedHelp = "Everyone types their answer on their own screen. Answers that match yours are ticked automatically (case and small typos don't matter; accept several with \"/\"). At the reveal you can tick or untick any answer.";

	Control ChoiceRow(int i)
	{
		var correct = _draft.CorrectSet.Contains(i);
		var mark = U.Btn("✓", correct ? "TealButton" : "", () =>
		{
			if (!_draft.IsMulti) _draft.CorrectChoice = i;
			else if (!_draft.CorrectChoices.Remove(i)) _draft.CorrectChoices.Add(i);
			Changed(true);
		}).MinSize(44).Tip(correct && _draft.IsMulti ? "Mark as wrong" : "Mark as correct");
		if (!correct) mark.AddThemeColorOverride("font_color", C.Border);
		var input = U.Input(_draft.Choices[i], i == 0 ? "An answer" : "Another answer", t =>
		{
			_draft.Choices[i] = t;
			Changed();
		}, 120);
		var row = U.H(8, mark, input);
		if (correct) row.AddChild(U.Pill("Correct", C.Teal));
		return row;
	}

	// ---------- estimation ----------

	Control EstimationForm()
	{
		var left = U.V(14, PromptArea("e.g. How many hours did we play last month?"),
			U.H(12,
				U.Field("Correct number", new NumberField(_draft.AnswerNumber, v =>
				{
					_draft.AnswerNumber = v;
					Changed();
				}, placeholder: "e.g. 42")),
				U.Field("Unit (optional)", U.Input(_draft.Unit, "e.g. hours", t =>
				{
					_draft.Unit = t;
					Changed();
				}, 20))),
			U.Field("Image (optional)", OptionalImage()));
		return Columns((left, 1.6f), (RoundSettings.Closeness(_draft, () => Changed()), 1));
	}

	// ---------- sound / video ----------

	Control MediaForm()
	{
		var isSound = _draft.Type == QuestionType.Sound;
		_mediaField = new MediaField(_draft.Media, _draft.Type, m =>
		{
			_draft.Media = m;
			Changed();
		});
		var left = U.V(14, U.Field("Question", U.Input(_draft.Prompt, "", t =>
			{
				_draft.Prompt = t;
				Changed();
			}, QuestionLimits.PromptMaxLength)),
			U.Field(isSound ? "Song or sound effect" : "Trailer or video", _mediaField),
			U.Field("Correct answer (only you see this)", U.Input(_draft.AnswerText, isSound ? "e.g. Dragonborn – Skyrim" : "e.g. Elden Ring", t =>
			{
				_draft.AnswerText = t;
				Changed();
			}).With(e => e.AddThemeFontOverride("font", Fonts.Bold))));
		var settings = RoundSettings.Panel(
			RoundSettings.AnswerMode(_draft, () => Changed(true)),
			U.Field("Start playing at (seconds)", new NumberField(_draft.StartSec, v =>
			{
				_draft.StartSec = Math.Max(0, v ?? 0);
				Changed();
			}, 0, _draft.Media?.DurationSec), "Skip a silent intro or a title card that gives it away."),
			_draft.Typed ? U.H(12, RoundSettings.TimeLimit(_draft, () => Changed()).Grow(), RoundSettings.Points(_draft, () => Changed()).Grow()) : RoundSettings.Points(_draft, () => Changed()),
			RoundSettings.Bonus(_draft, () => Changed()),
			U.Muted(_draft.Typed ? TypedHelp : isSound
				? "The sound plays on your screen. Players buzz in on their own PC; playback pauses when someone buzzes. After a wrong answer it stays paused until you resume it (or reveal the answer if nobody knows it). Once it's guessed, everyone sees how many seconds of listening it took. Answer time per buzz is set in the lobby."
				: "The video plays on your screen and on the players' screens too (blurred until you start it). Players buzz in on their own PC; playback pauses when someone buzzes. After a wrong answer it stays paused until you resume it (or reveal the answer if nobody knows it). Once it's guessed, everyone sees how many seconds of watching it took. Answer time per buzz is set in the lobby.", 13));
		return Columns((left, 1.6f), (settings, 1));
	}

	// ---------- put in order ----------

	Control OrderForm()
	{
		var timeline = _draft.IsTimeline;
		var max = timeline ? QuestionLimits.TimelineItemsMax : QuestionLimits.OrderItemsMax;
		// Labels follow their items (a timeline shows each item's label, e.g. its year, once it's placed).
		while (_draft.ItemLabels.Count < _draft.Items.Count) _draft.ItemLabels.Add("");
		var rows = U.V(8);
		for (var i = 0; i < _draft.Items.Count; i++)
		{
			var at = i;
			var remove = U.Small("×", () =>
			{
				_draft.Items.RemoveAt(at);
				if (at < _draft.ItemLabels.Count) _draft.ItemLabels.RemoveAt(at);
				Changed(true);
			}).Tip("Remove");
			remove.Disabled = _draft.Items.Count <= QuestionLimits.OrderItemsMin;
			var row = U.H(8, U.Mono($"{at + 1}.", 16, C.Muted).MinSize(28), U.Input(_draft.Items[at], at == 0 ? "e.g. Doom" : "", t =>
			{
				_draft.Items[at] = t;
				Changed();
			}, 80).Grow());
			if (timeline)
			{
				row.AddChild(U.Input(_draft.ItemLabels[at], at == 0 ? "e.g. 1993" : "", t =>
				{
					_draft.ItemLabels[at] = t;
					Changed();
				}, QuestionLimits.TimelineLabelMaxLength).MinSize(120).Tip("Shown once it's placed").With(e => e.SizeFlagsHorizontal = SizeFlags.Fill));
			}
			row.AddChild(remove);
			rows.AddChild(row);
		}
		var add = U.Small("+ Add item", () =>
		{
			_draft.Items.Add("");
			_draft.ItemLabels.Add("");
			Changed(true);
		});
		add.Disabled = _draft.Items.Count >= max;
		var mode = U.Field("How it's played", U.Segmented(OrderMode.Modes, _draft.OrderMode, id =>
		{
			_draft.OrderMode = id;
			// Back to everyone at once: at most its own number of items.
			if (id == OrderMode.All && _draft.Items.Count > QuestionLimits.OrderItemsMax)
			{
				_draft.Items = _draft.Items.Take(QuestionLimits.OrderItemsMax).ToList();
				_draft.ItemLabels = _draft.ItemLabels.Take(QuestionLimits.OrderItemsMax).ToList();
			}
			Changed(true);
		}));
		var left = U.V(14, PromptArea("e.g. Put these games in the order they came out"), mode,
			U.H(12,
				U.Field(timeline ? "Left end means" : "Top of the list means", U.Input(_draft.OrderTop, "e.g. Oldest", t =>
				{
					_draft.OrderTop = t;
					Changed();
				}, 30)).Grow(),
				U.Field(timeline ? "Right end means" : "Bottom of the list means", U.Input(_draft.OrderBottom, "e.g. Newest", t =>
				{
					_draft.OrderBottom = t;
					Changed();
				}, 30)).Grow()),
			U.Field("Items, in the right order", U.V(8, rows, add.With(b => b.SizeFlagsHorizontal = SizeFlags.ShrinkBegin)),
				timeline
					? Lang.F("Type them in order ({0}–{1} items) with what each one shows once it's placed, e.g. its year. A random one is already on the timeline when the question starts.", QuestionLimits.OrderItemsMin, max)
					: Lang.F("Type them top to bottom ({0}–{1} items). Players get them shuffled.", QuestionLimits.OrderItemsMin, max)));

		Control settings;
		if (timeline)
		{
			var keepGoing = new CheckBox { Text = "Whoever is right keeps going", ButtonPressed = _draft.KeepGoing, MouseDefaultCursorShape = CursorShape.PointingHand };
			keepGoing.Toggled += on =>
			{
				_draft.KeepGoing = on;
				Changed();
			};
			settings = RoundSettings.Panel(
				U.Field("Time per turn (seconds)", new NumberField(_draft.TimeLimitSec, v =>
				{
					_draft.TimeLimitSec = (int)Math.Clamp(Math.Round(v ?? QuestionLimits.TimeLimitMin), QuestionLimits.TimeLimitMin, QuestionLimits.TimeLimitMax);
					Changed();
				}, QuestionLimits.TimeLimitMin, QuestionLimits.TimeLimitMax, true)),
				U.Field("Points per item placed", new NumberField(_draft.Points, v =>
				{
					_draft.Points = (int)Math.Clamp(Math.Round(v ?? QuestionLimits.PointsMin), QuestionLimits.PointsMin, QuestionLimits.PointsMax);
					Changed();
				}, QuestionLimits.PointsMin, QuestionLimits.PointsMax, true)),
				U.V(4, keepGoing, U.Muted("Off: the turn passes on after every item.", 12)),
				RoundSettings.Bonus(_draft, () => Changed()),
				U.Muted("Players take turns, fewest points first. On your turn you pick where the next item goes on the timeline. Right: it's placed with its label, you score and carry on. Wrong: the next player tries the same item. If nobody gets it, it's placed for free and the next player starts the next item.", 13));
		}
		else
			settings = RoundSettings.Panel(
				RoundSettings.TimeLimit(_draft, () => Changed()),
				RoundSettings.Points(_draft, () => Changed()),
				RoundSettings.Bonus(_draft, () => Changed()),
				U.Muted("Everyone puts the items in order on their own screen. Only a perfect order gets the points; the reveal shows how many each player had in the right place.", 13));
		return Columns((left, 1.6f), (settings, 1));
	}

	// ---------- write the answer ----------

	Control WriteForm()
	{
		var left = U.V(14, PromptArea("e.g. What's the capital of Australia?"),
			U.Field("Accepted answers", U.Input(_draft.AnswerText, "e.g. Canberra", t =>
			{
				_draft.AnswerText = t;
				Changed();
			}).With(e => e.AddThemeFontOverride("font", Fonts.Bold)), "Separate several with / (e.g. \"Mount Everest / Everest\"). Case, accents and small typos don't matter."),
			U.Field("Image (optional)", OptionalImage()));
		var settings = RoundSettings.Panel(
			RoundSettings.TimeLimit(_draft, () => Changed()),
			RoundSettings.Points(_draft, () => Changed()),
			RoundSettings.Bonus(_draft, () => Changed()),
			U.Muted("Everyone types their answer on their own screen. Answers that match are ticked for you; at the reveal you can tick or untick any answer, and the points follow.", 13));
		return Columns((left, 1.6f), (settings, 1));
	}

	// ---------- image reveal ----------

	Control ImageRevealForm()
	{
		_primaryImage = new ImageField(_draft.Image, img =>
		{
			_draft.Image = img;
			Changed(true);
		}, Images.ScreenshotMax, "The picture players have to recognise.", false);
		var left = U.V(14, PromptLine(),
			U.Field("Picture", _primaryImage),
			U.Field("Correct answer (only you see this)", U.Input(_draft.AnswerText, "e.g. The Eiffel Tower", t =>
			{
				_draft.AnswerText = t;
				Changed();
			}).With(e => e.AddThemeFontOverride("font", Fonts.Bold))),
			// (here, not in the settings: the picture on the left gives up the room)
			U.H(12, RoundSettings.AnswerMode(_draft, () => Changed(true)).Grow(),
				_draft.Typed ? RoundSettings.TimeLimit(_draft, () => Changed()).Grow() : U.Gap(0)),
			RoundSettings.Bonus(_draft, () => Changed()));
		var right = U.V(14);
		if (_draft.Image != null)
		{
			var zoom = _draft.RevealStyle == RevealStyle.Zoom;
			var preview = new HostImageReveal(null, _draft, U.Flexible);
			preview.View.ShowFocus = zoom;
			if (zoom)
				preview.View.OnPick = p =>
				{
					_draft.Focus = p;
					Changed(true);
				};
			right.AddChild(U.Field("Preview", preview, zoom
				? "Click the picture to choose the spot it starts zoomed in on (the yellow ring)."
				: "This is what your screen shows; it gets sharper until the time is up."));
		}

		var settings = RoundSettings.Panel(
			U.Field("How it's hidden", U.Segmented(RevealStyle.All, _draft.RevealStyle, style =>
			{
				_draft.RevealStyle = style;
				Changed(true);
			})),
			// (side by side, so the preview and the settings fit next to the picture)
			U.H(12,
				U.Field("Seconds until it's fully clear", new NumberField(_draft.RevealSec, v =>
				{
					_draft.RevealSec = (int)Math.Clamp(v ?? QuestionLimits.RevealSecDefault, QuestionLimits.RevealSecMin, QuestionLimits.RevealSecMax);
					Changed();
				}, QuestionLimits.RevealSecMin, QuestionLimits.RevealSecMax, true), Lang.F("{0}–{1} seconds.", QuestionLimits.RevealSecMin, QuestionLimits.RevealSecMax)).Grow(1.4f),
				RoundSettings.Points(_draft, () => Changed()).Grow()),
			_draft.Typed ? U.Muted(TypedHelp, 13) : U.Muted("The picture is shown on your screen only and gets clearer over time. Players buzz in on their own PC; it pauses while someone answers and you mark it Correct or Wrong. Once it's guessed, everyone sees the whole picture and how many seconds it took. Answer time per buzz is set in the lobby.", 13));
		right.AddChild(settings);
		return Columns((left, 1.6f), (right, 1));
	}

	// ---------- map pin ----------

	VBoxContainer _mapCol = U.V(10);
	readonly Dictionary<string, Texture2D?> _mapTextures = new();
	bool _addingMap;
	const string NewMap = "__new";
	const string PresetPrefix = "preset:";

	Control MapPinForm()
	{
		_primaryImage = new ImageField(_draft.Image, img =>
		{
			_draft.Image = img;
			Changed();
		}, Images.ScreenshotMax, "The screenshot players have to locate.", false);
		var left = U.V(14, U.Bold("1 · Screenshot", 16), _primaryImage, PromptLine());
		_mapCol = U.V(10);
		BuildMapColumn();
		var check = new CheckBox { Text = "Players must buzz in first", ButtonPressed = _draft.BuzzFirst };
		check.Toggled += on =>
		{
			_draft.BuzzFirst = on;
			Changed();
		};
		var right = RoundSettings.Closeness(_draft, () => Changed(), U.V(4, check,
			U.Muted("Not used in the game yet. Answer time per buzz is a game rule, set in the lobby.", 12)));
		return Columns((left, 1), (_mapCol, 1.5f), (right, 0.8f));
	}

	MapAsset? CurrentMap => _maps.FirstOrDefault(m => m.Id == _draft.MapId);

	Texture2D? MapTexture(MapAsset map)
	{
		if (MapPresets.Get(map.PresetId) is { } preset) return Images.BuiltinTexture(preset.ImagePath);
		if (!_mapTextures.TryGetValue(map.Id, out var tex))
		{
			tex = map.Image.DataUrl != "" ? Images.TextureFromDataUrl(map.Image.DataUrl) : null;
			_mapTextures[map.Id] = tex;
		}
		return tex;
	}

	void BuildMapColumn()
	{
		_mapCol.Clear();
		// (once it's built: the map takes the height that's left)
		Callable.From(() => U.PropagateGrow(_form)).CallDeferred();
		_mapCol.AddChild(U.Bold("2 · Map and correct spot", 16));

		var items = new List<(string, string)> { ("", "Choose a map…") };
		items.AddRange(_maps.Select(m => (m.Id, m.Name)));
		items.AddRange(MapPresets.All.Where(p => _maps.All(m => m.PresetId != p.Id)).OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase).Select(p => (PresetPrefix + p.Id, Lang.F("{0}  (built-in)", p.Name))));
		items.Add((NewMap, "+ Upload a new map"));
		_mapCol.AddChild(U.Select(items.ToArray(), _addingMap ? NewMap : _draft.MapId ?? "", id =>
		{
			_addingMap = id == NewMap;
			if (id.StartsWith(PresetPrefix)) ChoosePreset(id[PresetPrefix.Length..]);
			else if (!_addingMap)
			{
				_draft.MapId = id == "" ? null : id;
				_draft.Target = null;
				Changed();
			}
			BuildMapColumn();
		}));

		var map = CurrentMap;
		if (_addingMap)
		{
			_mapCol.AddChild(MapUploader());
			return;
		}
		if (map == null)
		{
			_mapCol.AddChild(U.Panel(U.Muted("Pick a game map or upload your own above.", 14, true, HorizontalAlignment.Center).MinSize(0, 120), "Inset"));
			return;
		}

		var hint = U.Muted("", 13);
		void UpdateHint(bool wow) => hint.Text = wow
			? (_draft.Target != null ? "Correct spot set. Click again to move it." : "Zoom in and click to set the correct spot.") +
			  " Players see how far off they were in game distance (yards converted to km/m); a guess on the wrong continent always counts as far away."
			: _draft.Target != null ? "Correct spot set. Click again to move it." : "Click the map to set the correct spot. Scroll to zoom, drag to move.";

		var credit = MapPresets.Get(map.PresetId)?.Credit;
		if (map.PresetId == WowAtlas.PresetId)
		{
			_mapCol.AddChild(new WowAtlasView(_draft.Target, p =>
			{
				_draft.Target = p;
				Changed();
				UpdateHint(true);
			}, null, U.Flexible));
			UpdateHint(true);
			_mapCol.AddChild(hint);
		}
		else
		{
			var view = new MapView(map.Name, MapTexture(map), map.Image.Width, map.Image.Height, U.Flexible);
			view.Canvas.Target = _draft.Target;
			view.Canvas.OnPick = p =>
			{
				_draft.Target = p;
				view.Canvas.Target = p;
				view.Canvas.QueueRedraw();
				Changed();
				UpdateHint(false);
			};
			_mapCol.AddChild(view);
			UpdateHint(false);
			_mapCol.AddChild(hint);
			var scale = U.Muted("", 13);
			void UpdateScale() => scale.Text = (map.WidthKm is > 0
				? "Players see how far off they were in km/m."
				: "No scale set — players see how far off they were in % of the map.") + " Applies to every question using this map.";
			UpdateScale();
			_mapCol.AddChild(U.H(12,
				U.Field("Map name", U.Input(map.Name, "", t =>
				{
					map.Name = t;
					Changed();
				})),
				U.Field("Map width in km (optional)", new NumberField(map.WidthKm, v =>
				{
					map.WidthKm = v is > 0 ? v : null;
					Changed();
					UpdateScale();
				}, 0.001, placeholder: "unknown"))));
			_mapCol.AddChild(scale);
		}
		if (credit != null) _mapCol.AddChild(U.Muted(credit, 12));
	}

	void ChoosePreset(string presetId)
	{
		_addingMap = false;
		var existing = _maps.FirstOrDefault(m => m.PresetId == presetId);
		if (existing == null)
		{
			var preset = MapPresets.Get(presetId)!;
			var tex = Images.BuiltinTexture(preset.ImagePath);
			existing = new MapAsset
			{
				Id = QuizStore.MakeId(),
				Name = preset.Name,
				WidthKm = preset.WidthKm,
				PresetId = preset.Id,
				Image = new ImageAsset { Width = tex?.GetWidth() ?? WowAtlas.ImageWidth, Height = tex?.GetHeight() ?? WowAtlas.ImageHeight },
			};
			_maps.Add(existing);
		}
		_draft.MapId = existing.Id;
		_draft.Target = null;
		Changed();
	}

	Control MapUploader()
	{
		ImageAsset? image = null;
		var name = "";
		double? widthKm = null;
		var add = U.Btn("Add map", "TealButton", null, 14);
		add.Disabled = true;
		void Validate() => add.Disabled = image == null || name.Trim() == "";
		add.Pressed += () =>
		{
			if (image == null) return;
			var m = new MapAsset { Id = QuizStore.MakeId(), Name = name.Trim(), Image = image, WidthKm = widthKm };
			_maps.Add(m);
			_draft.MapId = m.Id;
			_draft.Target = null;
			_addingMap = false;
			Changed();
			BuildMapColumn();
		};
		var actions = U.H(8, U.Fill());
		if (_maps.Count > 0)
			actions.AddChild(U.Link("Cancel", () =>
			{
				_addingMap = false;
				BuildMapColumn();
			}, 14));
		actions.AddChild(add);
		return U.Panel(U.V(12,
			new ImageField(null, img =>
			{
				image = img;
				Validate();
			}, Images.MapMax, "A map image (big is fine — it's kept up to 4096 px)."),
			U.Field("Map name", U.Input("", "e.g. Skyrim", t =>
			{
				name = t;
				Validate();
			})),
			U.Field("Map width in km (optional)", new NumberField(null, v => widthKm = v is > 0 ? v : null, 0.001, placeholder: "unknown"),
				"How wide the whole image is in the real/game world. With it, players see distances in km/m; without it, in % of the map."),
			actions), "Card2");
	}

	// ---------- save / back / paste / drop ----------

	void Back()
	{
		if (!_dirty)
		{
			App.I.GoQuiz(_quiz.Id);
			return;
		}
		Dialogs.Confirm(this, "Discard your changes to this question?", "Discard", () => App.I.GoQuiz(_quiz.Id));
	}

	void Save()
	{
		if (_isNew && _quiz.Questions.All(q => q.Id != _draft.Id)) _quiz.Questions.Add(_draft);
		else
		{
			var i = _quiz.Questions.FindIndex(q => q.Id == _draft.Id);
			if (i >= 0) _quiz.Questions[i] = _draft;
			else _quiz.Questions.Add(_draft);
		}
		_quiz.Maps = _maps;
		try
		{
			QuizStore.Save(_quiz);
		}
		catch (UserError e)
		{
			Dialogs.Info(this, e.Message, "Couldn't save");
			return;
		}
		_dirty = false;
		App.I.GoQuiz(_quiz.Id);
	}

	public override void _Input(InputEvent e)
	{
		if (e is InputEventKey { Pressed: true, Echo: false, Keycode: Key.V, CtrlPressed: true } && DisplayServer.ClipboardHasImage() && _primaryImage != null)
		{
			_primaryImage.Paste();
			GetViewport().SetInputAsHandled();
		}
	}

	public void OnFilesDropped(string[] files)
	{
		var file = files.FirstOrDefault();
		if (file == null) return;
		if (_mediaField != null && Media.IsMediaPath(file)) _mediaField.LoadFile(file);
		else if (_mediaField != null && file.GetExtension().ToLowerInvariant() is "mp4" or "webm" or "mov" or "mkv") _mediaField.LoadFile(file);
		else if (_primaryImage != null && Images.IsImagePath(file)) _primaryImage.LoadFile(file);
	}
}
