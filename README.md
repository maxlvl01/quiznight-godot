# Quiznight! (Godot)

Host quiz nights with friends — over Discord or in the same room. The host is the quizmaster
(builds the quiz and runs the show, doesn't play); players join with a 4-character room code on
their own PC. Jackbox style: the host's screen goes on the TV or into a Discord stream.

This is the Godot 4.7 (C#) version of Quiznight. It plays over **Steam** (using Valve's Spacewar
test app, so everyone needs Steam running) or over **LAN** (same Wi-Fi, or several windows on one PC).

## What you can do

- **Build quizzes** in the app — saved in `%APPDATA%\Quiznight\quizzes`, exported as
  `.quiznight.json` files to back up or share. Files exported from the old Quiznight website import as-is.
- **Question types**
  - **Classic** — buzzer (first to buzz says the answer) or A B C D (everyone picks).
  - **Sound** and **Video** — play a song, sound effect or trailer; players buzz in, and everyone sees how
    many seconds it took to guess. Sound: MP3, OGG, WAV. Video: `.ogv` only (convert MP4 with ffmpeg).
  - **Estimation** — closest number wins.
  - **Map pin** — show a screenshot, players pin the spot on a map; scored by real distance on game maps
    (Skyrim, Oblivion, GTA IV/V, WoW Classic with zones).
- **Play** — buzz queue with Correct / Wrong, timers, reveals, scores and a final podium.

## Run it

1. Open the project in Godot 4.7.2 (.NET / mono version) and press Play — or run it from the command line:
   `Godot_v4.7.2-stable_mono_win64.exe --path .`
2. Start Steam first if you want to play over Steam.
3. **Host:** My quizzes → build or import a quiz → **Open lobby**, share the code.
   **Players:** choose Steam (or LAN), enter the code (or the host's address) and a name → Join.

To try it alone, use LAN mode: open the game twice (or more) and join `127.0.0.1` from the extra windows.

## License

Quiznight? is free, and always will be: no purchases, no ads, no donations.

From 0.12 on the code is under the [PolyForm Noncommercial License 1.0.0](LICENSE): anyone may use, change and share
it, but not sell it or use it commercially. (Versions up to 0.11.x were MIT.) **Game map images are not covered** — they belong to their game publishers
(and UESP.net for the Elder Scrolls maps). Game names are trademarks of their owners; Quiznight is a
fan project and not affiliated with them.
