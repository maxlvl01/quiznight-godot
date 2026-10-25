# Road to 1.0

Where Quiznight? is heading until the first proper quiz night. Add things anywhere below as you find them; tick them
off (`- [x]`) when they're done, with the version they came in.

**How versions go from here**
- **0.9.x b** — polish: bugs people find (editor especially), smaller grievances, quality of life, UI consistency.
- **0.10.0b** — only if something new is big enough to deserve its own minor version (a maybe, not a plan).
- **1.0.0** — the first proper quiz night. The real Ewige Tabelle starts here; nights before count for the Beta table.

---

## For 1.0 — content and bigger things

- [ ] Questions / quizzes for the first proper night
- [ ] Discord Rich Presence (needs a Discord application ID from you: "Playing Quiznight? · Question 4/15")
- [ ] Steam invites: join from the friends list instead of typing the code
- [ ] Map pin "buzz in first, then place your pin" (`Question.BuzzFirst` exists in the file format, not in the game yet)

## 0.9.x — polish

### Bugs
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
- [ ] _(add what you find)_

### Not tested with real people yet
- [x] Interactive questions: recording with a real microphone — works (tested 0.9.1b)
- [x] Steam profile pictures in the avatars — load (tested 0.9.1b)
- [ ] Steam outbox and lobby downloads with big videos over a slow connection

## UI inconsistencies spotted so far

Seen in this session's screenshots; all tidied in the next 0.9.x (not released yet). Add new ones below.

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
