# Changelog

All notable changes to Quiznight! are listed here, newest first.

Versions follow **MAJOR.MINOR.HOTFIX**:
- **MAJOR**: a big milestone or a breaking change (e.g. old quiz files no longer load).
- **MINOR**: new features that don't break anything (new question type, new screen).
- **HOTFIX**: bug fixes only.

`0.x.y` means the game is still in development. `1.0.0` will be the first complete, playable quiz night.
Versions up to 0.3.2a were the website; the history of those is in the website's CHANGELOG.md.

## [Unreleased]

### Added
- **German translation:** the whole game is now also in German. Pick the language under Settings;
  on a German Windows it starts in German by itself. Quiz questions stay as they were written.
- **Settings screen** (button at the top right of the home screen):
  - Volume for everything, for music (songs and clips in sound/video questions) and for sound effects.
  - Window mode (windowed, fullscreen, borderless) and window size.
  - Language: English or Deutsch.

### Changed
- The game runs at most at 60 FPS, so it no longer heats up your PC for nothing.

## [0.4.0a] — Quiznight is now a Godot app

### Added
- **Quiznight runs as a desktop app** (Godot 4.7, C#) instead of a website. Everything the website could do
  is in: the quiz editor, all five question types, the lobby, buzzers, reveals, scores and the podium.
- **Play over Steam:** the host opens a lobby and gets a 4-letter code; friends enter the code and connect
  through Steam's network — no port forwarding, works over the internet. Uses Valve's free test app
  (Spacewar), so Steam shows everyone as "playing Spacewar".
- **LAN mode:** play on the same Wi-Fi without Steam, or open several windows on one PC to test.
- If a player's connection drops, the game reconnects by itself and they keep their seat.
- A sample quiz with one question of every kind, for trying things out.

### Changed
- Players now play on their own PC with the app instead of a phone browser.
- Quiz files keep the same format: quizzes exported from the website import as they are. Built-in game maps
  are no longer copied into quiz files (they ship with the game), so files with game maps are much smaller.
- Videos have to be `.ogv` files (Godot can't play MP4 or WebM). Sound can be MP3, OGG or WAV.
- When the last player who hadn't answered leaves, the answer is revealed right away instead of waiting
  for the timer.
