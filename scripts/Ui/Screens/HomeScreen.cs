using System;
using Godot;
using QuizNight.Net;
using QuizNight.Shared;
using QuizNight.Storage;

namespace QuizNight.Ui.Screens;

public partial class HomeScreen : Control
{
	NetMode _mode;
	LineEdit _target = null!;
	LineEdit _name = null!;
	Button _join = null!;
	Label _error = null!;
	VBoxContainer _joinFields = null!;

	public HomeScreen() : this(null) { }

	public HomeScreen(string? notice)
	{
		SetAnchorsPreset(LayoutPreset.FullRect);
		_mode = App.NetMode;

		var brand = U.H(18, U.H(0, U.Text("Quiznight", 64, C.Text, Fonts.Black), U.Text("?", 64, C.Accent, Fonts.Black)));
		// The stamp next to the logo follows the version letter: 0.5.0a = alpha, 0.7.1b = beta, none when finished.
		var stage = AppVersion.Parse(App.Version)?.Suffix switch { "a" => "ALPHA", "b" => "BETA", _ => null };
		if (stage != null)
			brand.AddChild(U.Panel(U.Text(stage, 22, C.Coral, Fonts.Mono), "Clear").With(p =>
			{
				p.AddThemeStyleboxOverride("panel", UiTheme.Box(Colors.Transparent, 10, C.Coral, 3, 12, 2));
				p.RotationDegrees = -6;
				p.SizeFlagsVertical = SizeFlags.ShrinkCenter;
			}));
		brand.Center();

		var page = U.V(18, brand,
			U.Muted("Quiz nights with your friends — on Discord or on the couch.", 17, false, HorizontalAlignment.Center).Center());
		// "Update available" under the logo, once a check found a newer version.
		var updateNote = U.Text("", 14, C.Teal, Fonts.Bold, false, HorizontalAlignment.Center).Center();
		page.AddChild(updateNote);
		if (notice != null) page.AddChild(U.Panel(U.Text(notice, 15, null, null, true), "Notice").With(p =>
			p.AddThemeStyleboxOverride("panel", UiTheme.Box(C.Panel2, 10, C.Accent, 1, 14, 10))));

		page.AddChild(NetworkRow());

		var host = U.Card(10,
			U.Eyebrow("Quizmaster"),
			U.Heading("Host a quiz night", 24),
			U.Muted("Build a quiz, open a lobby, share the code and run the show. You control the pace — players answer on their own PC.", 15),
			U.Fill(),
			U.Btn("My quizzes", "AccentButton", App.I.GoQuizzes).With(b => b.SizeFlagsHorizontal = SizeFlags.ShrinkBegin)).Grow();

		_target = U.Input("", "", _ => Validate(), 0, "CodeInput");
		// (Steam names can be longer than names in a room may be: the field starts with the part that fits)
		var name = Settings.PlayerName != "" ? Settings.PlayerName : SteamService.PlayerName;
		_name = U.Input(Server.Room.CleanName(name), "e.g. Max", _ => Validate(), Limits.NameMaxLength);
		_name.TextSubmitted += _ => Join();
		_target.TextSubmitted += _ => Join();
		_target.TextChanged += t =>
		{
			if (_mode != NetMode.Steam) return;
			var norm = Limits.NormalizeCode(t);
			if (norm == t) return;
			_target.Text = norm;
			_target.CaretColumn = norm.Length;
			Validate();
		};
		_joinFields = U.V(10);
		_error = U.Error();
		_join = U.Btn("Join", "TealButton", Join).With(b => b.SizeFlagsHorizontal = SizeFlags.ShrinkBegin);
		var join = U.Card(10, U.Eyebrow("Player"), U.Heading("Join a game", 24), _joinFields, _error, _join).Grow();

		var grid = U.H(16, host, join);
		page.AddChild(grid);
		// Just an arrow: grey (click to check), green when a newer version is out (click to update).
		var updateButton = U.Small("", () =>
		{
			if (UpdatePrompt.Available is { } release) UpdatePrompt.Install(this, release);
			else UpdatePrompt.CheckNow(this);
		}).MinSize(22, 22);
		foreach (var state in new[] { "normal", "hover", "pressed", "focus" }) updateButton.AddThemeStyleboxOverride(state, new StyleBoxEmpty());
		var arrow = new UpdateArrow();
		arrow.SetAnchorsPreset(LayoutPreset.FullRect);
		updateButton.AddChild(arrow);
		updateButton.SizeFlagsVertical = SizeFlags.ShrinkCenter;
		updateButton.MouseEntered += () => arrow.Hovered = true;
		updateButton.MouseExited += () => arrow.Hovered = false;
		page.AddChild(U.H(6, U.Muted($"v{App.Version}", 12, false), updateButton, U.Muted("·", 12, false), U.Link("What's new", () => WhatsNew.ShowCurrent(this), 12),
			U.Muted("·", 12, false), U.Link("🏆 Ewige Tabelle", App.I.GoEternalTable, 12)).Center());
		void ShowUpdate()
		{
			if (!IsInstanceValid(updateButton)) return;
			var release = UpdatePrompt.Available;
			updateNote.Text = release != null ? Lang.F("An update is available: version {0}", release.Version) : "";
			updateNote.Visible = release != null;
			arrow.Available = release != null;
			updateButton.TooltipText = release != null ? Lang.F("Update to {0}", release.Version) : UpdatePrompt.Checking ? Lang.T("Checking…") : Lang.T("Check for updates");
		}
		ShowUpdate();
		UpdatePrompt.Changed += ShowUpdate;
		TreeExiting += () => UpdatePrompt.Changed -= ShowUpdate;

		AddChild(U.Page(page, 960, 56));
		var corner = U.H(8, U.Small("☀ Light mode", LightMode), U.Small("Settings", App.I.GoSettings));
		corner.SetAnchorsAndOffsetsPreset(LayoutPreset.TopRight, LayoutPresetMode.KeepSize, 20);
		corner.GrowHorizontal = GrowDirection.Begin;
		AddChild(corner);
		BuildJoinFields();
	}

	/// <summary>There is no light mode. (A prank for friends: it opens Never Gonna Give You Up.)</summary>
	public static void LightMode() => OS.ShellOpen("https://www.youtube.com/watch?v=dQw4w9WgXcQ");

	Control NetworkRow()
	{
		var status = SteamService.Available
			? Lang.F("Steam: signed in as {0}", SteamService.PlayerName)
			: SteamService.InitError ?? "Steam isn't available.";
		// (LAN is hidden until the developer code is typed: just the Steam status then)
		if (!App.LanUnlocked)
			return U.H(8, new ColorRect { Color = SteamService.Available ? C.Teal : C.Coral, CustomMinimumSize = new Vector2(8, 8) }.With(r => r.SizeFlagsVertical = SizeFlags.ShrinkCenter),
				U.Muted(status, 13, false)).Center();
		var row = U.H(10, U.Text("Play over", 14, C.Muted, Fonts.Bold).With(l => l.SizeFlagsVertical = SizeFlags.ShrinkCenter));
		var seg = U.Segmented(new[] { ("steam", "Steam (internet)"), ("lan", "LAN / same PC") }, _mode == NetMode.Steam ? "steam" : "lan", id =>
		{
			_mode = id == "lan" ? NetMode.Lan : NetMode.Steam;
			Settings.Mode = _mode;
			BuildJoinFields();
		});
		seg.CustomMinimumSize = new Vector2(380, 0);
		if (!SteamService.Available) seg.GetChild<Button>(0).Disabled = true;
		row.AddChild(seg);
		row.AddChild(U.Muted(status, 13, false).With(l => l.SizeFlagsVertical = SizeFlags.ShrinkCenter));
		return row.Center();
	}

	void BuildJoinFields()
	{
		_joinFields.Clear();
		if (_mode == NetMode.Steam)
		{
			_target.PlaceholderText = "– – – –";
			_target.ThemeTypeVariation = "CodeInput";
			_target.Text = Limits.NormalizeCode(_target.Text);
			_joinFields.AddChild(U.Field("Lobby code", _target));
		}
		else
		{
			_target.PlaceholderText = "127.0.0.1";
			_target.ThemeTypeVariation = "";
			if (_target.Text == "" || Limits.NormalizeCode(_target.Text) == _target.Text) _target.Text = Settings.LanAddress;
			_joinFields.AddChild(U.Field("Host address", _target, "The address shown in the host's lobby. 127.0.0.1 = this PC."));
		}
		_joinFields.AddChild(U.Field("Your name", _name));
		_error.Visible = false;
		Validate();
	}

	void Validate()
	{
		var targetOk = _mode == NetMode.Steam ? _target.Text.Length == Limits.CodeLength : _target.Text.Trim() != "";
		_join.Disabled = !targetOk || _name.Text.Trim() == "";
	}

	void Join()
	{
		Validate();
		if (_join.Disabled) return;
		Settings.PlayerName = _name.Text.Trim();
		if (_mode == NetMode.Lan) Settings.LanAddress = _target.Text.Trim();
		App.I.JoinRoom(_mode, _target.Text.Trim(), _name.Text.Trim());
	}

	// The last keys typed anywhere on this screen, for the developer code.
	string _typed = "";

	public override void _Input(InputEvent e)
	{
		if (App.LanUnlocked || e is not InputEventKey { Pressed: true, Echo: false } key || key.Unicode == 0) return;
		_typed += (char)key.Unicode;
		if (_typed.Length > App.DevCode.Length) _typed = _typed[^App.DevCode.Length..];
		if (_typed != App.DevCode) return;
		App.LanUnlocked = true;
		Callable.From(() => App.I.GoHome("Developer mode: LAN play is unlocked until Quiznight closes.")).CallDeferred();
	}

	public override void _Ready() => (_mode == NetMode.Steam && _name.Text != "" ? _target : (Control)_name).CallDeferred(Control.MethodName.GrabFocus);
}
