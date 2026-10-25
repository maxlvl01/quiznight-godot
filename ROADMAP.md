# Road to 1.0

Where Quiznight? is heading until the first proper quiz night. Add things anywhere below as you find them; tick them
off (`- [x]`) when they're done, with the version they came in.

**How versions go from here**
- **0.9.x b** — polish: bugs people find (editor especially), smaller grievances, quality of life, UI consistency.
- **0.10.0b** — only if something new is big enough to deserve its own minor version (a maybe, not a plan).
- **1.0.0** — the first proper quiz night. The real Ewige Tabelle starts here; nights before count for the Beta table.

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

## For 1.0 — content and bigger things

- [ ] Questions / quizzes for the first proper night
- [ ] Discord Rich Presence (needs a Discord application ID from you: "Playing Quiznight? · Question 4/15")
- [ ] Steam invites: join from the friends list instead of typing the code
- [ ] Map pin "buzz in first, then place your pin" (`Question.BuzzFirst` exists in the file format, not in the game yet)

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
