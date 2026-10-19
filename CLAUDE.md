# Quiznight! (Godot) — project notes for Claude

A desktop app for hosting quiz nights with friends (Jackbox style: the host's screen goes on the
TV or into a Discord stream). The **host is the quizmaster and never plays**. Players join with a
4-character room code on their own PC.

This is the Godot 4.7 / C# port of the web version at `C:\Users\Xbox3\Desktop\Quiznight!`
(React + Node, v0.3.2a). That folder is **reference only** — don't edit it. Its CLAUDE.md explains
the original design decisions; most still apply.

## Architecture decisions (and why)

- **Steam networking with the Spacewar app ID (480).** Internal program for friends, never
  published, so Valve's public test app is enough. Consequences: everyone needs Steam running and
  shows as "playing Spacewar"; lobby searches see every Spacewar lobby, so ours are tagged
  (`quiznight`, `quiznight_code`, `quiznight_host` lobby data).
- **The host's app is the server.** `HostSession` runs the `Room`/`Game` in-process; players
  connect to the host's Steam ID through Steam's relay (`SteamNetworkingSockets`). The lobby only
  makes the room code findable. Host leaves = room closed.
- **LAN mode (plain TCP, port 7777)** exists for testing several copies on one PC (Steam can't
  connect you to yourself) and for offline play on the same Wi-Fi.
- **Quiz files extend the web version's format** (`.quiznight.json`, now format 5: multi choice, image reveal, timeline,
  put in order, write the answer, typed buzzer questions, bonus points), so old exports import as-is.
  Buzzer kinds with `Typed` play like write the answer (`Question.IsWritten`); a bonus is given by the host at the reveal
  (`Game.ToggleBonus`), and the host can change scores any time (`HostSession.AdjustScore`).
  `QuizJson` reads forgivingly (= migration) and writes only each type's fields.
  Built-in map images are not stored in files (they ship with the game, `assets/maps/`). Each one's source and
  scale is noted in `MapPresets.cs`; `tools/stitch_map.gd` builds them from downloaded tiles (run one Godot at a
  time: two at once hang). All map images (also WoW's) are imported as **"keep"** and decoded by
  `Images.LoadBuiltinImage` (WebP + mipmaps): Godot's texture import stored them 2–7× bigger. Exports leave
  `assets/maps/` out of the .pck; `release.sh` copies the WebPs into a `maps/` folder next to the game
  (`Images.BuiltinFile` finds them there). New map images need the same 3-line `importer="keep"` .import file.
- **The host runs the pace.** Questions open hidden (`Game.Shown`) until the host shows them; buzzers stay
  locked (`BuzzersOpen`) until unlocked — both lobby rules. Players get no content of a hidden question, and
  an image reveal's picture only at the reveal. Choice answers and order items are shuffled per game
  (`QuizRules.ShuffleChoices`/`ShuffleOrder`); the stored quiz is never changed.
- **Game state is per viewer** (`Room.ToState(viewer)`): players never get answers before the
  reveal. Question images/custom maps are fetched by asset key (`q3/image`, `q3/map`) only for
  questions already shown, split into ≤300 KB parts (Steam's message limit is 512 KB).
- **Media plays only on the host** and is never sent. Godot plays MP3/OGG/WAV and `.ogv` video
  only (no MP4/WebM) — the editor rejects others with a conversion hint.
- **Timers run on `Room.Tick()`** from `_Process` (no threads); all networking is polled on the
  main thread. Steam callbacks run inside `SteamClient.RunCallbacks()`, so awaited Steam calls
  continue *inside* a callback — never shut Steam down there (`App.Quit` defers for that reason).
- **UI is built in code** (`scripts/Ui`), not in .tscn files: one root scene `scenes/Main.tscn`
  with `App.cs`. Screens that update often use `Keyed` slots so inputs, map zoom and the music
  player survive state updates.

## Layout

| Path | What |
|------|------|
| `scripts/Shared/Quiz.cs` | Quiz/question model, limits, `QuestionIssues`/`QuizIssues`, `MapDistance` |
| `scripts/Shared/QuizJson.cs` | Reading/writing `.quiznight.json` |
| `scripts/Shared/Protocol.cs` | Limits, room-code alphabet, player colours, state DTOs, messages (`Msg`) |
| `scripts/Shared/MapPresets.cs`, `WowAtlas.cs` | Built-in maps with real scale; WoW Classic world → continent → zone |
| `scripts/Server/Game.cs`, `Room.cs` | Question flow, buzz queue, judging, answers, play clock, reveal + scoring; players/settings/phases |
| `scripts/Server/HostSession.cs` | Host side: messages ↔ room, per-viewer broadcast, asset sending |
| `scripts/Net/` | `SteamTransport` (lobbies + relay), `LanTransport` (TCP), `ClientSession` (player side, auto-reconnect), `Assets`, `ServerClock` |
| `scripts/Storage/` | `QuizStore` (user://quizzes), `Images`, `Media`, `Settings` |
| `scripts/Ui/` | `UiKit` (theme, colours, builders), `Widgets`, `MapView` (zoom/pan map), `WowMap` (seamless WoW map: world → continents → stitched zone tiles → cities), `ImageReveal` (zoom/pixel reveal) |
| `assets/maps/wow/stitched/` | OpenMap's baked zone art (207 tiles), converted by `tools/openmap_tiles.gd` from the addon's `Media/Textures/MapZones` (BLP) |
| `scripts/Ui/Screens/` | Home, QuizList, QuizOverview, QuestionEditor, HostLobby, WaitingRoom, HostGame (+ Curtain, HostNotes window), PlayerGame, Podium, Settings |
| `scripts/Dev/` | `SelfTest`, `SampleQuiz`, `DevTools` (bots, autopilot, screenshots) |

User data: `%APPDATA%\Quiznight\` (quizzes, settings.cfg, cache).

## Commands

Godot is not on PATH. Use the console exe:
`G="/c/Users/Xbox3/Desktop/Godot_v4.7.2-stable_mono_win64/Godot_v4.7.2-stable_mono_win64_console.exe"`

```bash
dotnet build "QuizNight!.csproj"                              # compile — run before every commit
"$G" --headless --editor --path . --quit                      # (re)import assets after adding images
"$G" --headless --path . -- --selftest                        # rules, files, atlas, protocol, real TCP round trip; exit 0 = pass
"$G" --headless --path . -- --steam-check                     # needs Steam running: opens a lobby, finds it by code
"$G" --path . -- --no-steam --host-sample --autopilot --quit-at-end --shots DIR   # host the sample quiz, play it alone
"$G" --headless --path . -- --bot Ann                         # a bot player joining LAN 127.0.0.1
"$G" --path . --resolution 1600x900 -- --no-steam --open mapPin --shot out.png    # screenshot a screen
```

`--sound FILE` / `--video FILE` (with `--host-sample` or `--open`: the sound question plays that file / a video question follows it), `--screen home|quizzes|quiz|settings`, `--open classic|choice|multi|estimation|mapPin|wow|sound|image|order|timeline|write` (`--map-preset ID` with mapPin), `--start-at N` (with `--autopilot`), `--bot-drag` (a windowed bot drags the timeline item with real mouse events), `--map-zoom Z [--map-at X,Y]` (WoW map), `--lang en|de` (this run only), `--shot FILE --after SEC`
(see `DevTools.cs`). Full solo test: host with `--autopilot --players 2` plus two `--bot` instances.
Screenshots need a window (no `--headless`); the window opens briefly on the user's screen.

## Gotchas learned so far

- **`default_texture_filter=3` is Nearest Mipmap** (the project setting's order differs from CanvasItem's enum), so
  scaled art looks blocky. Maps set `TextureFilter = LinearWithMipmaps` on their canvas.
- **Headless runs (bots, self-test) have a dummy renderer**: don't build textures there (`WowMapCanvas` skips drawing).
- **Sounds must be Ogg Vorbis, MP3 or WAV.** Ogg *Opus* files (common from browser/Discord downloads) fail to
  import (`valid=false` in the .import file). The self-test loads every built-in sound to catch this.
- **Translations:** the English text is the key (`Shared/Lang.cs`, German in `Shared/LangDe.cs`). Godot
  auto-translates Label/Button/tooltip text that matches a key exactly, so plain strings just need a
  LangDe entry. Text built from parts must use `Lang.F("… {0} …", x)` (never `$"…"` in UI text), and
  player-written text (names, prompts, answers) gets `.Raw()` so it's never translated. Reveal texts are
  made on the host, in the host's language.

- `GetCmdlineUserArgs()` only sees arguments after `--`.
- `RandomNumberGenerator` is ambiguous between Godot and System.Security.Cryptography — qualify it.
- In-memory `JsonValue`s don't convert number types on read; `Question.Clone()` round-trips through text.
- Stretch mode `canvas_items` with a 1280×720 base: sizes in code are base units (×1.25 at 1600×900).
- Godot C# classes need a parameterless constructor (we add `public X() : this(...)`).
- `steam_api64.dll` comes from the Facepunch package's `content/` folder and is copied to the build
  output by the csproj (`None … CopyToOutputDirectory`).
- **Exporting:** the 4.7.2 mono templates are installed (Windows x86_64). C# export needs `QuizNight!.sln`
  next to the csproj. The build lands in `build/Quiznight/`; `steam_api64.dll` ends up in the
  `data_QuizNight!_windows_x86_64` folder and Steam works from there (checked with `--steam-check`).
- **Releases & updates:** `tools/release.sh` exports, zips and publishes a GitHub release on the public
  `maxlvl01/quiznight-releases` repo (code lives in the private `maxlvl01/quiznight-godot`; `maxlvl01/quiznight` is the web version). Exported games check
  it at startup (`Storage/Updater.cs`) and update themselves; "What's new" is read from the shipped
  CHANGELOG.md, so its `## [version]` / `### Added|Changed|Fixed|Removed` layout matters.
  Updates go file by file: each release has `manifest-windows.json` / `manifest-linux.json` (every file's SHA-256 and
  the URL of its gzipped copy, made by `tools/manifest.py`); only files whose hash is new are uploaded, the rest point
  into older releases — so **never delete old releases**. The game downloads only what differs (off the main
  thread: awaiting there throttles reads to one per frame), falling back to the zip. Test against a local server
  with `QUIZNIGHT_UPDATE_FEED=<url of a fake releases/latest JSON>`. `build/` has a `.gdignore` (else the editor
  imports the copied maps).
- **AnyConverter** (the user's separate Python/tkinter converter, `C:\Users\Xbox3\Desktop\AnyConverter`, not in git)
  ships in the Windows zip as `AnyConverter/AnyConverter.exe` and in the Linux zip as `AnyConverter/AnyConverter`;
  `release.sh` copies them from its `dist/` (`AnyConverter.exe`, and `linux/AnyConverter-linux-x86_64.tar.gz` from its
  GitHub Actions build; override with `ANYCONVERTER=…` / `ANYCONVERTER_LINUX=…`) and stops if one is missing. Rebuild it with `py -m PyInstaller --noconfirm AnyConverter.spec`.
  `VideoGuide` opens it with `--format ogv FILE`. FFmpeg is deliberately not bundled (too big): AnyConverter
  prompts players to install it.

## Versioning rules

Same as the web version: **MAJOR.MINOR.HOTFIX** (`0.4.0a` = alpha, `0.7.1b` = beta; the home screen stamp follows the letter), stored in
`project.godot` (`application/config/version`). Add changelog entries under `[Unreleased]` in
CHANGELOG.md while working (plain language, for friends to read). If the quiz file format changes,
bump `QuizRules.FormatVersion` and keep old files loading.

**Never `git push` (or push tags, create releases/PRs) without the user's explicit approval for that
specific push.** Committing locally is fine.

## Next up

1. Install export templates and make a Windows build friends can run; check Steam works from it.
2. Real test with friends on two PCs over Steam (only lobby + lookup are tested so far — not a
   relay connection between two accounts).
3. Map pin "players must buzz in first"; buzzer fairness via on-device reaction time.
4. Optional: Steam invites (join via friends list), avatars from Steam.
