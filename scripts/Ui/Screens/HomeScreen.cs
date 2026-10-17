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
		_mode = SteamService.Available ? Settings.Mode : NetMode.Lan;

		var brand = U.H(18,
			U.H(0, U.Text("Quiznight", 64, C.Text, Fonts.Black), U.Text("!", 64, C.Accent, Fonts.Black)),
			U.Panel(U.Text("ALPHA", 22, C.Coral, Fonts.Mono), "Clear").With(p =>
			{
				p.AddThemeStyleboxOverride("panel", UiTheme.Box(Colors.Transparent, 10, C.Coral, 3, 12, 2));
				p.RotationDegrees = -6;
				p.SizeFlagsVertical = SizeFlags.ShrinkCenter;
			})).Center();

		var page = U.V(18, brand,
			U.Muted("Quiz nights with your friends — on Discord or on the couch.", 17, false, HorizontalAlignment.Center).Center());
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
		_name = U.Input(Settings.PlayerName != "" ? Settings.PlayerName : SteamService.PlayerName, "e.g. Max", _ => Validate(), Limits.NameMaxLength);
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
		page.AddChild(U.Muted($"v{App.Version}", 12, false, HorizontalAlignment.Center).Center());

		AddChild(U.Page(page, 960, 56));
		BuildJoinFields();
	}

	Control NetworkRow()
	{
		var status = SteamService.Available
			? $"Steam: signed in as {SteamService.PlayerName}"
			: SteamService.InitError ?? "Steam isn't available.";
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

	public override void _Ready() => (_mode == NetMode.Steam && _name.Text != "" ? _target : (Control)_name).CallDeferred(Control.MethodName.GrabFocus);
}
