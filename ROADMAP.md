# Road to 1.0

Where Quiznight? is heading until the first proper quiz night. Add things anywhere below as you find them; tick them
off (`- [x]`) when they're done, with the version they came in.

**How versions go from here**
- **0.9.x b** — polish: bugs people find (editor especially), smaller grievances, quality of life, UI consistency.
- **0.10.0b** — only if something new is big enough to deserve its own minor version (a maybe, not a plan).
- **1.0.0** — the first proper quiz night. The real Ewige Tabelle starts here; nights before count for the Beta table.

---

## 0.12.0d5 — collecting (nothing built yet)

**Before 0.12.0b goes public, check:** a public 0.11.4b (it has no results token, see 0.12.0d3's changelog; its update
check doesn't need one) updates itself to 0.12.0b, and the new build then uploads results and crash logs again.

**Bugs found in 0.12.0d4:**
- [ ] **The buzzer sound plays only once:** pressing the buzzer again (Test lab's free buzzer) stays silent instead of
      playing the sound each time it's pressed.
- [ ] **The Reaction test in the Test lab is broken:** find out what goes wrong there and fix it.

**Ideas:**
- [ ] **Sort the editor's question list** (the list on the left): by question type, or by the order they were created in.
- [ ] **Look at the buzzer up close:** a way to enlarge the buzzer and see it in detail (for example the engraving).
- [ ] **Microphone volume slider** in Settings: the test recording with Max's mic came out very quiet. It starts at 50 %
      (the normal level) and goes louder from there, with an input meter next to it that shows whether it's clipping.
- [ ] **Your own background picture:** a "Pick a picture …" choice in Settings' background list that takes a picture from
      your PC (copied into the game's user folder, so it stays when the original moves; checked by `Guard` like other pictures).
- [ ] **The animated "Pinke Geilheit Spacing Team" background** as its own choice next to the still one. Godot can't play
      GIFs, so it gets converted when it's added (frames, or a looping .ogv), and it stays still when "Background motion" is
      off. Made from the video version, `Downloads/Astonaut_Gigastacy.mp4` (4.7 MB, 1024×1024 like the still one,
      24 fps, a 3.875 s loop, no sound) → a looping .ogv. (The GIF, `Downloads/ezgif-5-2d0da948e0.gif`, is only 600×600
      at 10 fps.)
- [ ] **A space skin** (a new `Ui.Skins` entry: deep space colours, not just a palette swap):
      - **Twinkling stars** behind the screens (drawn in code, a few hundred, each with its own slow twinkle), a **black hole**
        (dark core with a glowing, slowly turning accretion ring that bends the stars near it), and a few **planets**
        drifting very slowly (one ringed).
      - **A rocket launch:** pressing Start on the intro's title screen launches a rocket from the bottom (flame, smoke,
        a little shake) that flies off the top before the home screen comes up.
      - **Wheatley and the Space Core** from Portal 2 floating around the edges now and then, tumbling slowly; the Space
        Core with a speech bubble ("I'm in space!", "Space!", "Gotta go to space."). Drawn by us in code/our own art, not
        Valve's models or sound files (the code and releases are public on GitHub, so no ripped Portal 2 voice lines in
        them). They don't talk out loud: speech bubbles only.
      - **Rare Star Wars guests:** now and then a Star Destroyer glides slowly across the far background, small and
        dark against the stars; even rarer, a subtle Death Star sits half in shadow among the planets for a while. Drawn
        by us, as silhouettes with a few lights, so they're easy to miss.
        - **The Death Star can fire:** while it's there, a small chance it blows up one of the planets (a green beam,
          a flash, the planet breaking into glowing pieces and a ring of debris that slowly fades). The planet comes back
          later.
        - **Star Wars-inspired planets** among the others: a desert planet with two suns, an ice planet, a gas giant
          with a cloud city glinting on it, a forest moon, a lava planet.
        - **Ships and fighters flying past now and then:** X-wings and TIE fighters (sometimes chasing each other,
          with little laser shots), a Millennium Falcon-style freighter, a Star Destroyer's shuttle. Small and quick,
          mostly at the edges, so they don't pull eyes off the quiz.
      - Stays still when "Background motion" is off (stars don't twinkle, nothing floats, the rocket just isn't there).
- [ ] **Test drop-downs in the Test lab:** a few drop-downs (short, long, scrolling) to check how they open and roll in.


---

## 0.12.0d4 — test build (done)

- [x] (0.12.0d4) **Videos at most 720p.** Still sharp on everyone's screen, and much less to send to every player (helps the stress
  test night). Today only the copy-paste ffmpeg command (`Media.ConvertCommand`) caps the width at 1280; AnyConverter's
  `--format ogv` (what `VideoGuide` opens) keeps the full size, and an .ogv added as it is isn't checked. Plan:
  AnyConverter scales to max 1280×720 when Quiznight opens it; the editor notices bigger .ogv files and offers to shrink
  them. (From the 1.0 roadmap chat, 27 October 2026; Max wants it in 0.12.)
- [x] (0.12.0d4) **"Report a bug" on every screen.** Only ever sent by hand: a small 🐞 button (corner of every screen, also in
  lobbies and games, next to the Settings gear there) opens a small window with **one text box: what happened**, and
  Send. The **category comes from where it was opened**, filled in by the game, not picked: e.g. "Editor › Map pin",
  "Quiz list", "Lobby (host)", "Waiting room", "Game (player) › Buzzer question", "Reveal", "Podium", "Mini games ›
  8-ball", "Settings", "Statistics › Eternal table", "Test lab" (each screen says its own category; a window over a
  screen, like Settings in a game, says both). Shown at the top of the window so the player sees it ("Bug in: Lobby
  (host)").
  Sending uploads one Markdown file to the results repo's `bug-reports/<category>/` (same token and upload as
  `CrashReports.Put`: `<date> <version> <6 chars>.md`), from a template so they all read the same: category, version
  and build (test/public), date, OS, language, skin, Steam or LAN, room details (host or player, phase, question
  number/type) — the player's text — the last ~200 lines of this run's log (anonymised like crash reports) — and a
  screenshot of the screen as it was when 🐞 was pressed (`.png`, same name). "Thanks, Max will look at it"
  afterwards; if the upload fails it's kept and sent on the next start. Max reads them in the repo and hands them to
  Claude. (Settings' "What the game sends" mentions it.)
- [x] (0.12.0d4) **Test lab: the buzzer is free to press.** Today the BUZZ! button in the Test lab is locked until Start's
  reaction test unlocks it (Max: "I just want to press the button and hear my standard sound"). Instead: the button
  is always unlocked, presses down like in a game and plays the buzzer sound picked in Settings, every time. The
  reaction test becomes optional (its own small "Reaction test" button next to it, which locks the buzzer until the
  random moment as now).
- [x] (0.12.0d4) **Drop-downs still run off the screen and don't scroll (big windows).** Max's Category list at his screen size
  hangs past the window's bottom and can't be scrolled. Likely: 0.12.0d2's `DropDowns.Unfold` measures the button and
  the room left in the game's own 1280×720 units but sizes the list window in screen pixels; at 1280×720 they match
  (that's all that was tested), on a bigger window the game is scaled up and the list comes out too long. Fix: one
  coordinate space (convert with the viewport's scale), then check at 1280×720, 1920×1080, 2560×1440 and 3440×1440.
  And the animation Max wants: the list **quickly rolls out from under the drop-down's head** (as if it slides out of
  the button) and **rolls back in under it** when it closes (today it only unfolds, and just vanishes on close).
- [x] (0.12.0d4) **Reaction test as a lobby mini game, with high scores.** Next to Tetris in the mini games window: "Reaction"
  — press Start, wait for the buzzer to light up (a random 1.5–4 s), buzz as fast as you can; 5 tries, your best (and
  average) counts; buzzing too early is a false start. Timed on the player's own PC (no network delay in it, so it's
  fair over Steam), only the result goes to the host. Two lists, lowest time first, **shown like F1 timing**: the
  leader with their time (`0.187`), everyone below with their gap to the leader (`+0.023`) and the interval to the one
  just ahead (`+0.005`), in milliseconds, purple for the best of all time, green for a personal best:
  - **Tonight:** everyone's best in this lobby (like the Tetris bests, `ArcadeView`).
  - **All time:** the best times ever. Kept like the eternal table: each night's lobby bests go to the results repo
    with the night's results (an extra row type in the CSV, `ResultsUpload`), and the all-time list is added up from
    `assets/results/` (same player ids; the same night counted once). Shown in the mini games window and on the
    Statistics screen. **Tetris gets the same:** tonight's bests and an all-time list (highest score first; F1 style
    with the gap to the leader and to the one ahead in points, purple for the all-time best, green for a personal best).
  Uses the Test lab's reaction test (which stays there too, as an option, see the free buzzer item).
- [x] (0.12.0d4) **Buzzer engraving: on every buzzer, and easier to see.** The smaller buzzers (the Test lab's, 120 px) show no
  TRACKGLOW at all: the engraving is left out below a 6 px font (`BuzzButton._Draw`), and on the big one it's barely
  visible. Draw it on every size (a smaller font on small buzzers, never left out) and make the cut deeper: a darker,
  more pronounced shadow edge on top and a stronger light edge underneath, still in the metal's own colours (not
  TrackGlow's). Check on the game's buzzer, the Test lab's and the disabled (greyed) one.

---

## 0.12.0d3 — test build (done)

- [x] (0.12.0d3) Builds carry the results token again (0.11.4b, 0.12.0d1 and d2 had none: no test updates, logs or
  results uploads); `release.sh` stops without it.
- [x] (0.12.0d3) The buzzer's TRACKGLOW engraving moved to the plate's top strip, above the key.

---

## 0.12.0d2 — second test build (done)

- [x] (0.12.0d2) **Hover names for WoW Forever's new zones, rough outlines for now.** Hovering Riverglades (and Gilneas) on the
  WoW Forever map says "Eastern Kingdoms" (or a neighbour like "Silverpine Forest"): `WowAtlas.ZoneAt` only knows
  Classic's zones, by their map rectangles, and new land outside them falls back to the continent. Until Forever's
  zone data is out (after 4 November; then it replaces these), draw rough outlines on the world map (0–1 points) for
  each new zone, checked before `ZoneAt` in `WowMapCanvas.PlaceName` and listed in "Jump to zone…". Finding them: a
  one-off dev mask of land in the zone art (`overview-2`) that no Classic zone covers. Names known so far: Gilneas,
  Riverglades; any other new areas the mask shows need their names from Max. Tooltip/jump only: scoring stays in
  Classic's game yards. Screenshot from Max, 26 October 2026.
- [x] (0.12.0d2) **No more "on the TV / in a stream" in the game's texts.** Quiznight? is played with everyone on their own PC
  (talking on Discord), never from one shared screen. The home screen's tagline "Quiz nights with your friends — on
  Discord or on the couch." (and LangDe) and the README's "over Discord or in the same room … the host's screen goes on
  the TV or into a Discord stream" need new wording; HostNotes' tooltip "not in your stream if you share only the game
  window" too (the notes window may still be handy, just not for that reason).
- [x] (0.12.0d2) **"The TrackGlow Podium"** — poking fun at American sports ads where everything has a sponsor's name. The podium
  screen (`PodiumScreen` in `RoomScreens.cs`: "And the winner is …") gets the title "The TrackGlow Podium" with the
  TrackGlow logo (`TrackGlowLogo`, like the intro's sponsor card and the title screen), for host and players alike.
  More branded names later (Max has none for the other awards yet: the season poster's awards, the eternal table's
  "Fastest buzz" … could each get one, e.g. "the TrackGlow Fastest Buzz of the Night", once he picks them).
- [x] (0.12.0d2) **Audio normalisation: every sound and song equally loud.** Measured 26 October (EBU R128 integrated loudness):
  - Music (`assets/music`, incl. `christmas/`, `halloween/`): from −9.6 LUFS (Before Chill, Sugarsnaps) to −15.8 LUFS
    (Seth, Drive-Through Dinner), 6 dB apart — the quiet ones sound half as loud. The new Halloween song is −13.1.
  - Sound effects (`assets/soundeffects`): Podium −8.0 vs Podium2 −30.5 (22 dB apart!), buzzers −14.3 (metal pipe),
    −15.0 (soft beep), −27.4 (Buzzer 3); Correct −27.1; the voice line −25.6; click and Wrong too short to measure
    this way (use their peak/short-term loudness).
  - Fix: normalise the files themselves, so nothing changes in code and every player hears the same: music to one target
    (e.g. −14 LUFS, true peak −1 dB), sound effects to one level of their own (short ones by short-term loudness), the
    voice line so it sits clearly above the music; a tool (`tools/normalise_audio.py`, ffmpeg `loudnorm` two-pass,
    re-encoding Ogg Vorbis at 128 kbps like the songs) so new songs and sounds Max adds get the same treatment. Then
    check by ear that the menu music, the buzzers and the podium jingles feel balanced against each other. Players' own
    buzzer sounds (uploaded) could be levelled when they're picked, too. Also the sounds and videos in quizzes? (open:
    those are the quiz maker's; maybe just a "level the volume" tick in the editor)
- [x] (0.12.0d2) **Image reveal becomes a Picture subtype.** Both show a picture and are answered the same way, so in the type picker
  "Image reveal" goes away and Picture gets the choice: **still / slideshow** (as now) or **reveal** (zoomed right in or
  pixelated, uncovering over time: the image reveal's modes and timing, `ImageReveal`, `PlayerImageReveal`). Done the
  way "Write the answer" became Classic + typed in 0.10.3b: older quizzes, exported files and share codes with
  `type "image"` open as Picture → reveal with everything kept (picture, mode, time, answers, points, bonus); quiz format
  bump; past nights' stats for Image reveal count under Picture → Reveal (`KindStats`, eternal table per type); the lobby
  version check knows the new subtype needs 0.12 (older games get "can't play question N"). Changelog says where it went.
  ~26 places use `QuestionType.Image` today.
- [ ] (still open; all four kept for now) **Picture questions: which answer modes make sense?** Picture offers the same four as Classic (Buzzer, Everyone
  types, Pick one, Multiple choice). Max doubts Pick one and Multiple choice fit a picture ("a picture can really only
  be one answer"). Cases they'd still cover: Pick one — "Which game is this Steam review about?" with four games to
  pick from (an easier version of the same question); Multiple choice — "Which of these characters are in the
  screenshot?" (several right). Decide: keep all four, or Picture only Buzzer / Everyone types (old quizzes with the
  others then open as … ? Everyone types, keeping the right answer as the answer).
- [x] (0.12.0d2) **No squash when a button is clicked.** Buttons shrink to 94 % while held down (`UiAudio.Bounce` on ButtonDown);
  they already look pressed in (their 3D style), so drop the squash. The slight grow under the mouse stays (Max didn't
  mind that one; check).
- [x] (0.12.0d2) **Drop-downs: smoother, and always right under their button.** Today the list unfolds in 0.16 s from a quarter of
  its height (`DropDowns.Unfold`) and a long list (Category, the estimation unit list with Length/Mass/Time …) opens
  wherever it fits, even over the button and up the whole screen. Instead: a longer, softer unfold (e.g. 0.25 s, ease
  out, fading in); the list always hangs under its button (above only if there's no room below), at most so tall it
  fits (~8–10 rows), and scrolls inside itself (mouse wheel, the scrollbar) for the rest; keeps its place on screen
  while scrolling. Check in every drop-down: units, categories, maps' Jump to zone, settings.
- [x] (0.12.0d2) **Test screens in developer mode ("Test lab").** Test builds start in developer mode (`App.LanUnlocked`; exported
  games after typing 61969); there a "Test lab" button on the home screen (next to DEV / Test update) opens a screen of
  quick checks, without hosting a whole quiz or using command-line flags (`DevTools` has most of them only as flags):
  - **Buzzers:** press the buzz button (or the key), hear every buzzer sound and your own, see the buzz screen as a
    player sees it, with the reaction time it measured.
  - **Sounds & music:** play each song, buzzer, podium jingle, correct/wrong and the voice line at their volume, to
    hear them side by side (handy for the audio normalisation item).
  - **Microphone:** record a few seconds like an Interactive question and play it back (checks the mic works).
  - **Video:** play a test video (blur until it starts, play clock) like a video question.
  - **Question screens:** open any question type as host or player with sample content (the `--open …` previews), and
    the reveal / podium / The TrackGlow Podium, with bots as players (`--bot`, `--autopilot`), all from buttons.
  - **Maps:** the map picker, WoW Forever zoom and hover, `--pretend-changed-map` / `--pretend-new-map` as switches.
  - **Screens with fake states:** lobby with an old-version player (version check), the intro and the first-start
    welcome screen, "What's new", the yellow new dots (and resetting them).
  Players never see it (public builds without the code don't show the button).
- [x] (0.12.0d2) **Mini games in the lobby while waiting.** In the waiting room (and the host's lobby) a "Play something" button
  opens a 4:3 window over the lobby with a few small games:
  - **Tetris** — single player; best scores shown in the lobby.
  - **Pong** — 1 vs 1: challenge another player in the lobby or the host; they get an invite to accept or decline.
  - **Billiard** — 1 vs 1 the same way (turn-based, so the connection matters little).
  - **Versus stats in the lobby:** who beat whom how often, and win streaks ("Ann beat Max 3× in a row in Pong"),
    shown next to the seats.
  - Rules: the host's game runs the matches (like the quiz: players send their inputs, the host sends the state;
    Pong needs it fast, ~30 updates/s over Steam's relay, so test with real lag); when the host starts the quiz
    every mini game closes at once (a match that's cut off counts for nobody); downloads of the quiz's files carry on
    underneath; a player who's in a match still counts as ready. Older games (version check) just don't see the button.
  - Everyone plays on their own PC (nobody watches a shared TV or stream), so the host plays like anyone else.
  - Open: do the versus stats last only for this lobby, or are they kept (per player id, like the eternal table:
    an all-time Pong table)?

- [x] (0.12.0d2) **Billiard modes:** quick pool, 8-ball, 9-ball, 4-ball (Yotsudama), three-cushion, with their rules
  (`BilliardMode`, `BilliardRules.Settle`); 8-ball and 9-ball start with the lag for the break.
- [x] (0.12.0d2) **Buttons don't grow under the mouse** either; just the pressed-in look.
- [x] (0.12.0d2) **TRACKGLOW engraved in the buzzer's silver base** (subtle, cut in, not in TrackGlow's colours).
- [x] (0.12.0d2) **Jukebox:** shuffle and repeat (saved), back restarts the song (twice: previous), a progress bar with
  time played / left that you can click to jump.

---

## 0.12.0d1 — first test build (done)

- [x] (0.12.0d1) **Licence: MIT → PolyForm Noncommercial 1.0.0** (from 0.12 on; nobody may sell it, free forever). Swap `LICENSE` for
  the official text (copyright line: Max and Quiznight contributors). The in-game credits read that file, so they follow.
  README: licence section says PolyForm Noncommercial (map images still belong to their publishers), plus a line
  "Quiznight? is and always will be free: no purchases, no ads, no donations". Changelog entry. Versions up to 0.11.x
  stay MIT for anyone who already has them — that can't be taken back.
- [x] (0.12.0d1) **Changed maps must be re-checked before hosting.** When an update swaps a built-in map's picture (different or
  better image) or its scale, every map pin question on that map gets a small "map changed" icon (quiz list, quiz
  overview, the question in the editor). The host opens it, checks the answer pin still sits on the right spot (moves it
  if not) and ticks "Checked: the pin is right on the new map". Until every such question is ticked the quiz isn't ready,
  so its lobby can't be opened (a `QuizIssues` entry, like other unfinished questions).
  Idea: `MapPreset` gets `ChangedIn: "x.y.zb"` next to `AddedIn` (with a short note on what changed, shown with the
  icon); each map pin question remembers which map version its pin was checked on (placing or moving the pin, or the
  tick, stamps it; quiz format bump). A question with no stamp (older files, imports, share codes) counts as made before
  any change. Also a dev flag like `--pretend-new-map` to see the icon.
- [x] (0.12.0d1) **Lobby checks players' versions.** Today a join sends no version at all (`JoinRequest`: name, id, code), so a
  player on an old build gets in and things break mid-game when the quiz uses something their build doesn't have.
  - Every join sends the player's app version (and a protocol number that goes up when messages change in a way old
    builds can't read). A join without one is "older than 0.12".
  - The host's lobby shows each player's version; anyone older than the host gets a warning icon ("0.11.3b — older
    than yours").
  - The quiz is compared with each player's version: what it uses that their build doesn't have (question types, built-in
    maps via `AddedIn`, later features) → "Ann can't play question 5 (Interactive, needs 0.10)". Start then asks the host
    to confirm (or wait for Ann to update); nothing breaks silently.
  - A protocol mismatch (too old to understand the game at all) refuses the join with a clear message on the player's
    side: "The host has 0.12.0b, you have 0.11.3b — update first" (with the update button, as the updater already can).
  - Also the other way: a player newer than the host is fine unless the protocol differs.
- [x] (0.12.0d1) **Rename "WoW Classic – Azeroth" to "WoW Forever"** in the editor's map pick (`MapPresets.cs`; the subtitle
  "World → continents → zones" stays). Only the shown name changes: the preset id (`WowAtlas.PresetId`) stays, so
  existing quizzes and nights' stats keep working. Questions also store the map's name (`MapAsset.Name`, e.g. the sample
  quiz), so old quizzes should show the new name too (take the name from the preset when there is one). Also the
  "seamless WoW Classic map" in the map pin explanation (`Quiz.cs` + LangDe), the sample quiz, code comments.
- [x] (0.12.0d1, part 2 under Low priority) **WoW map (the "WoW Classic – Azeroth" preset): zooming in falls back to the old Classic art.** The world and
  continent maps look right, but at zoom ~3–4 the stitched zone layer fades in (`WowMap.cs`: "stitched zones 2.2–4", OpenMap's Classic
  zone art from `assets/maps/wow/stitched`) and the map suddenly looks like old Classic again: blurry, different
  colours, the continent's lettering cut off. Areas Classic never had as zones (Gilneas, Riverglades …) show as
  unexplored land and hovering them says "Eastern Kingdoms", because `WowAtlas` doesn't know them.
  What's really showing (`WowMapCanvas` draw): the 1002 px world map is always drawn underneath, blown up, and the
  continent maps are small and cut to the land's shape — so between the world view and the zone tiles (zoom ~1.3–4)
  everything is stretched low-res art, and the sea round the land stays the stretched world map, lettering
  ("…RN …GDOMS") and all, even at zoom 6. OpenMap is built for WoW Forever, so its zone art (our 207 tiles) already
  has Gilneas, Riverglades etc. (checked: Gilneas is drawn at zoom 4–6) — but `WowAtlas` only knows Classic's zones,
  so hovering them says "Eastern Kingdoms".
  Fix (Max): **the big stitched map, downsized, for the zoom steps until the zones come in.** Build from OpenMap's
  tiles one big map (tools script, like `openmap_tiles.gd`) and save it in a few smaller sizes (e.g. world view, ~2×,
  ~4×; WebP, "keep" import, shipped next to the game like the other maps); the map draws whichever size fits the
  current zoom, so every zoom step shows the same Forever art, only sharper — no fade from one look to another, no
  stretched world map with its lettering. Sea included (from the tiles, or the world map's sea texture at a matching
  scale). Plus the zones Classic doesn't have in `WowAtlas` (names and outlines from OpenMap's data, or the
  MapDumper Forever addon after 4 November) so hover and "Jump to zone…" know Gilneas, Riverglades …
  Screenshots from Max, 26 October 2026.
- [x] (0.12.0d1) **First start: ask how much intro to show next time.** The very first time the game is opened (no setting saved
  yet), after the intro a question: "Next time, skip …" with three choices — **show everything** (as now), **skip the
  joke parts** (the anti-cheat window, the Xbox "World Premiere" … — which cards count is open), **skip everything up to
  the start screen** (straight to the title screen, or to the home screen? open). Saved in settings.cfg; **Settings gets
  an "Intro" option with the same three choices** to change it any time (also for people who picked one on the first
  start and changed their mind). Today there's no such setting: only dev/test runs and "less motion" skip the intro
  (`IntroSequence`). Note the anti-cheat window shows while the game loads (`AntiCheatSplash`), so the setting has to be
  read before that.
- [x] (0.11.4b, hotfix so it's out while it's still October) **October intro song: "The Sleepy Sneaky Sway" (Luella Gren)** plays after the voice line when the game opens
  **during October only** (`Seasons.IsHalloween`, already all of October; try it with `--date`); the rest of the year
  "Easy Breezy" stays the first song. It's a Halloween-only song: it goes in a new `assets/music/halloween/` folder whose
  songs only join the shuffle in October (like `christmas/` in December), and the rest of the year it never plays.
  Max put the WAV in the main checkout's `assets/music/` (50 MB): convert it to Ogg Vorbis like the others
  (`ES_The Sleepy Sneaky Sway - Luella Gren.ogg`), make `UiAudio.IntroSong` pick it in October, check its loudness against the others (the per-song volume list), add it to `assets/music/CREDITS.md` with its Epidemic Sound
  link (https://www.epidemicsound.com/music/tracks/40774b7c-6290-4670-9025-49fef59ad2c7/), changelog entry.

---

## 0.10.0b "Season Ticket" — done

- [x] (0.10.0b) **Ewige Tabelle per question type and subtype** (the main point, not categories): a filter with every question
  type (Classic, Buzzer, A B C D, Multiple choice, Picture, Sound, Video, Image reveal, Map pin, Estimation, Timeline,
  Put in order, Write the answer, Step by step, Interactive) and under each its subtypes (e.g. buzzer said out loud /
  typed; map pin per map — real world, Skyrim, WoW …; order one by one / everyone at once; interactive voted / host's
  pick). Picking one swaps the table's columns for the stats that matter for that type, averages included, the more
  detailed and pointlessly specific the better. Examples: buzzer — buzzes, right/wrong buzzes, hit rate,
  Ø buzz time, fastest buzz ever; map pin — Ø km off, best pin ever, pins under 1 km; estimation — Ø % off, closest ever;
  A B C D — % right, Ø answer time; step by step — Ø hint they got it at; timeline/order — % items placed right;
  interactive — votes received, host favourites; jokers — Double points gained.
  - Needs per-question results: today's CSVs have one row per player and night (totals only), so a night would also
    have to save one row per player and question (type, variant, right/points, time, distance …). Older nights can't
    be split by type; the filtered views only fill from that version on.
- [x] (0.10.0b) **Separate Ø answer time and Ø buzz time** (from the 0.9 chat): today `NoteAnswerTime` puts both into one
  average (`PlayerStats.AnswerMs`, CSV `avg_answer_s`): a buzz counts its time to buzz (~1 s), a typed/picked answer
  or recording its time to lock in (~10 s+). So whoever buzzes a lot looks fast, and that mixed number breaks ties
  (night ranking and Ewige Tabelle). Split into Ø answer time (typed/picked answers, recordings) and Ø buzz time
  (buzzes) with their own counts, a column each in the Ewige Tabelle, a night's table and tonight's Stats; ties by
  answer time, then buzz time; CSV gains `avg_buzz_s`/`buzzes`. Older nights can't be split: they stay in Ø answer
  time, buzz time "–". Fits with the per-question results (buzz time per type comes from there).
  - Where it's recorded: `Game.cs` calls `_hooks.NoteAnswerTime` for buzzes (`Buzz`), answers and recordings
    (`SubmitRecording`); `Room.NoteAnswerTime` adds them up.
- [x] (0.10.0b) **Points as the last column of the Ewige Tabelle** (from the 0.9 chat): like a football table, the points sit at
  the right end of each row, with the averages before them. Today the order is Nights, Wins, Podiums, Points,
  Ø points, Ø answer time, Ø km off, Ø % off; Points moves to after Ø % off. It's the `Columns` array plus the row
  cells in `EternalTableScreen.cs` (the per-type views follow the same rule: the type's main number last).
- [x] (0.10.0b) **Player profile pages:** click a name in the Ewige Tabelle or match history (or "My profile") for a detailed,
  nicely designed overview of one player. Big profile picture top left with name, place in the Ewige Tabelle, nights
  played, wins and podiums; below it cards with every average we have (points, answer/buzz time, km off, % off …).
  Rankings: their 3 best and 3 worst question types / subtypes and categories (compared to everyone else, so "best"
  means where they beat the average most). More ideas: personal records (fastest buzz, closest pin), points per night
  over time as a little graph, favourite joker and how much it paid off, their nights in the match history, best
  rival (who they finish just ahead of / behind most).
  - **Look: like a Football Manager player profile.** Attributes rated 1–20 in coloured numbers (Buzzer speed,
    Map sense, Estimating, Music ear, Memory/order, Nerves (Fast Pass/Double payoff), Consistency …, one per question
    type and category, worked out from how they do against everyone else), an attribute polygon / radar chart, a
    form strip of the last 5 nights (places as coloured pills), "Strengths / Weaknesses" lists, a "best positions"
    pitch-style picture of their favourite question types, a career table (one line per night: quiz, place, points),
    "Hosted: 4 nights" where FM puts international caps (from the results' host, recorded since 0.9.4b; maybe
    their hosted nights listed too), and a role line under the name ("Speed buzzer", "Map specialist", "Estimation sniper" …) from their best attributes.
  - Builds on the per-question results above; types and categories only fill from that version on, the totals
    (nights, wins, points, Ø times) work for every night.
- [x] (0.10.0b) **Question categories (optional):** the host can pick a category for a question (Geography, Music, Film, Sport,
  History, Science, Games …) or type their own. Showing it helps players, so it's an option: a lobby rule "Show
  categories" (off by default), plus per question "show category" or "keep it secret". Players see it on the Get ready
  screen / with the question. Extra on top of the type filter: a category filter in the Ewige Tabelle too ("who's
  our geography king"), only for questions that had a category.

- [x] (0.10.0b) **Seasons** (Season 1 = 1.0 to the end of 2027, then one per calendar year) and **championship points**
  per night's place (F1 and the other scoring presets), as the Ewige Tabelle's "Ranked by".
- [x] (0.10.0b) **Position and points graphs** for the table (after each night) and for each night (after each question).
- [x] (0.10.0b) **Points from the host** (bonuses and by hand) and the **"Host's Favourite Puppy"** badge.
- [x] (0.10.0b) **Award pack:** a ZIP with the standings poster and an award card per question type, category, the
  puppy, the fastest buzzer and the best speller.
- [x] (0.10.0b) **Step by step as one sentence in parts:** the rest blurred, a buzz stops the reading, earlier = more points.
- [x] (0.10.0b) **Spelling measured from typos** in typed answers (letters off from the closest accepted answer).
- [x] (0.10.0b) **Party show look:** chunky outlined theme, Fredoka and Lilita One, stage glow, the logo as an answer field
  with easter eggs and spotlights, the arcade dome buzzer, bouncing buttons, a splash screen.
- [x] (0.10.0b) **Music and sounds:** Epidemic Sound songs (Easy Breezy first, pausing for sound, video and interactive
  questions, a Christmas song in December), "Now playing", a voice line at the start, button clicks.
- [x] (0.10.0b) **Accessibility:** colour vision modes, OpenDyslexic, screen reader announcements, less motion.
- [x] (0.10.0b) Metal pipe buzzer cut to start at once; videos without black bars.

## 0.10.1b — done

- [x] Escape goes back a screen; crash reports to the results repo; thin outlines on small buttons; drawn Fast Pass bolt;
  music fades for sound previews; preset dot; "Top 3" folded into Podium; no hover growth on rows; profile Form column.

## 0.10.2b — done

- [x] Jukebox, mouse thumb buttons back/forward, Ctrl+S, Quit button, welcome screen with accessibility on the first start
- [x] Skins: Quiznight Purple (the old one) and a pastel Pinke Geilheit; Olf?; profile bars light up
- [x] Fixed: Settings with the dyslexia-friendly font; award pack window with examples; volumes 50 %

## 0.10.3b — smaller download

From the 1.0 chat (also on its branch claude/1-0-roadmap-review-9685d7 and the public roadmap). 0.10.1b is ~193 MB
zipped (Windows), ~307 MB unpacked; updates already stay small (game packs ~33 MB), this is about the first download.
- [ ] `Quiznight.exe` 104 MB: stock Godot mono export template → a custom template without unused modules (3D, physics, XR, …)
- [x] `data_QuizNight!_windows_x86_64` 78 MB → 23 MB: .NET trimmed (partial) + debug files left out
- [x] `maps/` 63 MB → 46 MB: re-compressed (WebP q78 where it saved 10 %+)
- [x] `Quiznight.pck` 41 MB → 33 MB: music 128 kbps from the WAVs, the F1 podium as Ogg instead of a 23 MB WAV
- [x] `AnyConverter/` 28 MB: stays bundled (decided)

## Later — maps

- [ ] WARDOGS (Bakurani, Ozeti, Zestafona): skipped in 0.10.2b, the only imagery found (the game's terrain texture,
      via wardogstools.org / XGamingServer, also darkened) didn't look good enough. Needs the real in-game map.
- [ ] Race tracks as a Map pin subtype: F1, WEC, DTM and BTCC circuits. Plan: draw each track from its centre line
      (F1 tracks: github.com/bacinger/f1-circuits, MIT, with official lengths; Le Mans, Fuji, DTM and BTCC tracks:
      OpenStreetMap raceways, ODbL), so the scale comes from real coordinates. Norisring has no raceway in OSM.
- [ ] Sekiro, Dark Souls 1, 2 and 3: no top-down map with a scale exists (only illustrated art, sold as prints, or
      schematic diagrams; Fextralife's maps may not be republished)

## For 1.0 — content and bigger things

- [ ] **Stress test night before 1.0:** a proper quiz night with 50+ questions (every type, lots of pictures, sound
  and video), played start to finish with everyone over Steam. Watch: lobby downloads and the Start wait, memory and
  speed late in the night, pauses/rejoins, the scoreboard and stats with that many questions, results upload.
  (From the 1.0 roadmap chat, 27 October 2026.)
- [ ] Questions / quizzes for the first proper night
- [ ] Map pin "buzz in first, then place your pin" (`Question.BuzzFirst` exists in the file format, not in the game yet)
- [ ] More game maps for map pin (which games: _add them here_)
- [ ] Step by step: a sub category (idea, details to come)

### Game design changes
Whatever turns out not to be fun when we play gets changed or removed outright before 1.0. Note what felt off and
what happened instead.
- [ ] _(add what you notice)_

## Low priority — once everything else is done

Probably not for 1.0; they come when there's nothing else to do and the program is finished.

- [x] (0.12.0d2: see-through popup background) **Black box round a drop-down list, now and then.** Opening and closing a drop-down (e.g. Buzzer sound) quickly can
  leave a black rectangle round the list's rounded panel for a moment (Max: "you really have to try to get it";
  screenshot 26 October). Likely the list's popup window showing its own black background: `DropDowns.Unfold` resizes
  the popup window every frame, and a frame where the window is bigger than what's drawn (or its corners aren't see-
  through) shows black. Fix with the drop-down rework (0.12.0d2 item): give the popup a see-through background and
  animate the list inside a window of fixed size (a clip/mask) instead of resizing the window.
- [ ] **WoW Forever map, part 2 (once WoW Forever is out, 4 November 2026).** 0.12.0d1 made the land sharp at every zoom,
  but the old Classic world map still shows beneath it: the sea between and around the land, and through the soft
  see-through edges round each coast (OpenMap's zone art fades out there), so the coasts look blurry and old (Max's
  screenshot at Silverpine, 26 October). Fix with Forever's own map art once it's out (Max didn't keep screenshots of
  the beta): a Forever world map as the base instead of the 1002 px Classic one, or at least the sea drawn from Forever's
  sea texture, and the coast edges solid. Plus hover/"Jump to zone…" names for the new zones (Gilneas, Riverglades …)
  from Forever's zone data (MapDumper Forever addon).
- [ ] Discord Rich Presence (needs a Discord application ID from you: "Playing Quiznight? · Question 4/15")

Dropped: Steam invites (joining from the friends list) — the room code already does the job.

## 0.9.x — polish

### Bugs
- [x] Ewige Tabelle: players on the same points could show 3rd above 2nd (0.9.5b)
- [ ] _(add what you find)_

### Editor
- [x] Estimation units as a drop-down (SI, light years, AU, horsepower …), custom still possible
- [x] Decimals with dot or comma, every digit kept
- [x] Scoring per place with presets (F1 old and new, MotoGP, IndyCar, NASCAR, Mario Kart) and your own
- [ ] _(add what you find)_

### Quality of life
- [x] "How it works" on the players' Get ready screen, with a "Tonight so far" tab
- [x] Stats for the night (Stats link by the scores, host and players)
- [x] Ewige Tabelle: sort by any column, profile pictures, Ø % off for estimations
- [x] Tie rule "Faster answer wins" (default for new map pins)
- [x] Choice questions: optional "Wrong answers cost points"
- [x] Update check every minute while the game is open; "Update to …" in the editor and quiz screens
- [x] Statistics: Ewige Tabelle and Match history (quiz, host, date, places) as two tabs; your name in bold in both
- [x] Ewige Tabelle: places follow the sorted column, ▲/▼ for changes with the newest night, medal boxes and row lines (0.9.5b)
- [ ] _(add what you find)_

### Not tested with real people yet
- [x] Interactive questions: recording with a real microphone — works (tested 0.9.1b)
- [x] Steam profile pictures in the avatars — load (tested 0.9.1b)
- [ ] Steam outbox and lobby downloads with big videos over a slow connection

## UI inconsistencies spotted so far

Seen in this session's screenshots; all tidied in 0.9.2b. Add new ones below.

- [x] **"Time to answer" on the orange "Your turn!" panel** is tiny and orange-on-orange, almost unreadable.
- [x] **Map pin corner says "Map" twice:** its own "Map" header and the map's "Map: Skyrim" line. One is enough.
- [x] **Clan tags in score lists** are cut off ("[LCB] BassGami…") while seats show the tag as its own line. Score
  rows could leave the tag out or shorten it.
- [x] **Step by step bar pill** always says "5 points", though a right answer is worth less after each hint.
- [x] **Interactive sidebar** says "Answers: 2 / 4" for recordings sent; "Recordings" (then "Votes") would read better.
- [x] **Next buttons differ:** ▶ for the next picture, ▶▶ for the next recording, "Next hint (→)" for hints.
- [x] **Emoji glyphs** don't always render in the game font (🎤 in the recorder showed as a pen-like shape). Drawn icons
  (like the type icons and the new pin) are safer.
- [x] **Peek button** is a small button while "Reveal answer" next to it is a normal one; the row looks uneven.
- [x] **Lobby rules:** "Jokers (once per player and match)" is a muted line between checkboxes while the other groups
  have no heading.
- [x] **Ewige Tabelle "Ø km off"** showed 0.0 km for game maps; under 1 km it now shows metres.
- [x] **"?" on the question type cards** showed a long tooltip on hover, though clicking opens the full help anyway.
- [x] **Waiting room jokers** disappeared when the host turned one off (the box jumped); now greyed out, "Off tonight."
