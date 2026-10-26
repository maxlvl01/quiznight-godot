# Road to 1.0

Where Quiznight? is heading until the first proper quiz night. Add things anywhere below as you find them; tick them
off (`- [x]`) when they're done, with the version they came in.

**How versions go from here**
- **0.9.x b** — polish: bugs people find (editor especially), smaller grievances, quality of life, UI consistency.
- **0.10.0b** — only if something new is big enough to deserve its own minor version (a maybe, not a plan).
- **1.0.0** — the first proper quiz night. The real Ewige Tabelle starts here; nights before count for the Beta table.

---

## 0.12 — collecting (nothing built yet)

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
- [x] (0.12.0d1, except hover names for Forever's new zones: need its zone data after 4 November) **WoW map (the "WoW Classic – Azeroth" preset): zooming in falls back to the old Classic art.** The world and
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
