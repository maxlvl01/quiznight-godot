# Changelog

All notable changes to Quiznight! are listed here, newest first.

Versions follow **MAJOR.MINOR.HOTFIX**:
- **MAJOR**: a big milestone or a breaking change (e.g. old quiz files no longer load).
- **MINOR**: new features that don't break anything (new question type, new screen).
- **HOTFIX**: bug fixes only.

`0.x.y` means the game is still in development: a letter `a` is an alpha, `b` a beta (from 0.7.1b on).
`1.0.0` will be the first complete, playable quiz night.
Versions up to 0.3.2a were the website; the history of those is in the website's CHANGELOG.md.

## [Unreleased]

### Added
- **Short share codes**: "Copy share code" now gives a code like **QN-7K4P-9XZ2** that fits in any Discord message.
  The quiz is stored with Max's results online; pasting the code (My quizzes → Paste share code) fetches it again.
  Without internet you get the long code with the whole quiz in it, like before.
- **Check for updates** next to the version number on the home screen. When a new version is out it turns green
  ("Update to …") and a note under the logo says so.

### Changed
- **Export** makes a zip: the quiz plus its sound and video files as real files, about a third smaller than before.
  Import file reads these zips and the older .json files.
- The trash can sits in a quiz's top-right corner, just a red can. Under the mouse it grows and asks "Delete?".
- The **?** on the question types sits level with the name, without a box.

### Fixed
- The "Don't ask me again" box in the delete question sat on top of the text.

## [0.8.1b] — tidier quiz screens

### Changed
- **Deleting a quiz**: the trash can on a quiz in My quizzes. It asks first, unless you tick "Don't ask me again"
  (Settings → Video → "Ask before deleting a quiz" turns the question back on).
- **Picking a question type**: the cards show just the name. The **?** on each one tells what it does (hover) and
  explains its variants and options (click), e.g. buzzer, A B C D and multiple choice for a classic question.
- German: "Put in order" is now called **Einordnen**.

## [0.8.0b] — "Jokers Wild": jokers, big rooms, fair places and the eternal table

### Added
- **Jokers**: each player can play each joker once per match, before a question starts (during the reveal before it,
  or while the host still hides it). Until the question is shown you can take it back; then everyone sees it.
  The host picks which ones are allowed in the lobby.
  - **×2 Double points**: whatever the question gives you counts twice.
  - **⚡ Fast Pass** (buzzer questions only): when you buzz, you go ahead of everyone waiting in line.
  - During a reveal the top bar says whether the **next question is a buzzer one**, so you know when a Fast Pass works.
- **Leaving and joining during a game**:
  - If someone drops out, **the game pauses** (timers, buzzers, answers and the video or song) until they're back,
    so they lose nothing. The host can also **continue without them**. Leaving and coming back with the code keeps
    your seat and points.
  - **New players can join a running game** with the code. They watch the question that's running and play from
    the next one.
  - The scores and the podium show **who left (and at which question) and who joined later**.
- **Download rings**: on sound and video questions, a ring next to each name in the scores fills up while that
  player gets the file, and turns into a tick when they have it.
- **The room code** is in the top bar for the host and the players. Click it to copy it.
- **Change your name in the waiting room**: double-click it, type, Enter. The waiting room shows everyone who's in
  plus one open seat, instead of every seat the room has.
- **Confetti** on the final results.
- **Fairer places**: equal points are decided by who got there first, then by the faster average buzz/answer time
  (to the thousandth of a second), then by how close your map pins were on average. Hover a name in the scores to see
  their average answer time.
- **Shorter scores for players**: the top 3, then "⋮" and the players right around you (the one ahead, you, the one
  behind), so you see your closest competition even in a big room. The host still sees everyone.
- **Results go to Max**: after a quiz night the host's game sends everyone's results to Max for the eternal table
  (only if that fails, e.g. without internet, it keeps them in Documents/Quiznight/Results).
- **🏆 Eternal table** (link on the home screen): every quiz night whose results came with the game, added up per
  player: nights, wins, podiums, points, average points, average answer time and average km off.
- **Podium sounds**: the new **F1 Podium** (now the default) and the first one, **#SoBack**. The host picks one in the lobby, and everyone
  hears it with the final results. In Settings you can pick your own and untick "As a player, play the one the host
  picked" to always hear yours.
- **A new background picture** for wide monitors, the **Pinke Geilheit Spacing Team** (now the default): the astronaut
  beside the screens on the left, a corner of her ship on the right. Settings → Video → Background picture switches back to the
  Cosmic Cliffs.
- **Click a question's picture** (e.g. the screenshot of a map question) to see it as big as your screen allows.
- **Share codes**: "Copy share code" (on a quiz, or in My quizzes) copies the whole quiz as one line of text. Paste it
  to a friend or to your other PC, where **My quizzes → Paste share code** imports it. Pictures are in the code;
  sound and video files aren't (too big), so the game tells you which ones to add again. A long code that Discord
  turns into a message.txt file can be imported with **Import file**.

### Changed
- **No more "Max players" setting**: every room has room for 24 players.
- **The host sees a hidden question right away** (no more frosted glass), so you can read it out before pressing
  Show question. Players still see "Get ready…" until then.
- **Names are at most 12 characters** (longer Steam names are shortened), so the scores stay tidy. Initials skip
  symbols ("[OTB] maxlvl" is OM).
- **LAN play is hidden** on the home screen: it was for testing on one PC. Games are played over Steam.
- **The space background on wide monitors moves much less** and follows the mouse more slowly (it could cause motion
  sickness). Settings → Video → Background movement still turns it off completely.

## [0.7.4b] — small fixes

### Changed
- Small fixes and polish.

## [0.7.3b] — everyone sees and hears it all, faster updates, smaller download

### Added
- **Players see and hear everything the host's screen shows** (except the answers), so nobody needs to watch a
  Discord stream:
  - **Videos and sounds play on the players' PCs too**, in step with the host's: they play, pause and jump along
    with it. Players can turn the sound off. The game sends them to everyone in the background from the start of
    the quiz (locked, so nobody can watch or listen before its question).
  - **The host starts them**: next to the Start button you see who's still downloading, or "Everyone has it".
    Until then, players see the video blurred, so nobody can sit on the buzzer.
  - **Image reveal**: players see the picture getting clearer on their own screen, in step with the host's.
  - **At the reveal** players see everyone's answers, like on the host's screen.
- **Space background on wide monitors**: the Cosmic Cliffs (James Webb Space Telescope) fill the space beside the
  screens, slightly out of focus, sharp where the mouse is. **Settings → Video → Background movement** turns its
  movement off.

### Changed
- **Everyone needs this version:** the host and all players should update to 0.7.3b before playing together.
- **Sound, video and image reveal questions stay paused after a wrong answer** (when nobody else is in line): the host decides
  whether to resume it or to reveal the answer if nobody knows it.
- **Much faster updates.** The game now downloads only the parts that changed (usually about 1 MB instead of the
  whole game), several at once, and no longer slows the download down while it shows the progress bar. The update to this
  version still comes the old way (the whole zip), after that it's quick.
- **Smaller download: about 160 MB instead of 240 MB.** The built-in maps now ship as their original image files
  (they're also next to the game in a `maps` folder) instead of being stored again bigger inside the game file.
  They look the same or slightly sharper, nothing was made smaller or blurrier.
- **Wide screens:** on ultrawide monitors (21:9, 32:9) every screen now stays 16:9 in the middle instead of
  stretching across the whole monitor.

### Fixed
- Videos keep their shape: in the editor preview and in the game they're no longer squashed or stretched to fit the
  box (black bars fill the rest).

### Removed
- Leftovers of an old sound pack that was never used in the game.

## [0.7.2b] — everyone types, bonus points, change scores

### Added
- **"Everyone types" instead of buzzers.** Classic buzzer, sound, video and image reveal questions can now be played
  with everyone typing their answer instead of buzzing in: pick **How players answer → Everyone types** in the editor
  (for classic questions it's **Everyone types the answer** next to Buzzer). Answers that match yours are ticked
  automatically like in "Write the answer", everyone who's right gets the points, and at the reveal you can tick or
  untick any answer. These questions have a time limit.
- **Bonus points.** Every question can have an optional bonus, e.g. "+2: Also name the game". Players see it under the
  question. At the reveal you press **Give bonus** next to anyone who earned it (press again to take it back).
- **Change scores at will.** The scoreboard on your screen has **−** and **+** next to every player, so you can add or
  take away points any time during the game.

### Changed
- Quiz files are now format 5 (for typed questions and bonuses). Older files still load.
- The sample quiz's sound question is played with everyone typing and has a bonus; question 1 has a bonus too.

### Fixed
- When you tick or untick a typed answer at the reveal, the player's own screen now updates too.

## [0.7.1b] — beta, with 15 new game maps

### Added
- **15 new built-in maps for Map pin questions**, all with their real size, so "how far off" is right:
  - **Zelda:** Breath of the Wild (Hyrule), Tears of the Kingdom (Hyrule and the Depths).
  - **Elden Ring:** the Lands Between and the Land of Shadow (DLC).
  - **Red Dead Redemption 2**, **Cyberpunk 2077** (Night City), **GTA San Andreas** and **Forza Horizon 6** (Japan).
  - **PUBG:** Erangel, Miramar, Taego, Vikendi, Deston and Sanhok.

  Pick them under "Choose a map…" in a map pin question (the list is now sorted A–Z). Most have no place names on
  them; the Red Dead and Elden Ring maps show some, like in the game.
- **AnyConverter on Linux:** the Linux download now comes with AnyConverter too, and the video guide's
  **Open AnyConverter** button works there. AnyConverter itself is a newer, faster version.

### Changed
- **Timeline: drag and drop.** On your turn the game to place sits above the timeline as a card: drag it into the gap
  where you think it goes. The gaps light up while you drag. Changed your mind? Drag it to another gap, as often as
  you like, then press **Lock in**. Clicking a gap still works too.
- **The players' screen now looks like the host's:** a bar on top with the question number, type and points
  (and your name and score), the question and your answer in a card, and a column on the right with the buzz order,
  the timeline's turn order or who has answered, and the scores.
- **Quiznight is in beta now.** The stamp next to the logo says BETA instead of ALPHA, and versions end in "b".
- **No more scrolling in the game screens and the question editor.** Pictures and maps now take the space that's
  left on the screen instead of a fixed size, so everything fits in the window — on the host's screen, on the
  players' screens and in the editor. On the map pin reveal, players see the answer and the scores next to a big map.
- **Settings is more compact:** each volume is one row (hover it for what it does), so the whole screen fits.
- The empty "Image (optional)" box in the editor is smaller, and the question box is two lines high.

### Fixed
- On the timeline, "◀ Oldest" and "Newest ▶" now sit right at the ends of the timeline instead of at the edges of
  the screen, for the host and the players.
- **The Skyrim map no longer has a big empty border** on the right and at the bottom. Pins in quizzes you already
  made move along with it, and distances stay correct.
- The sample quiz's "Where is Whiterun?" pin is now actually on Whiterun.

## [0.6.1a] — the timeline goes at the host's pace

### Changed
- **The timeline now goes at your pace.** When a player locks in a spot, nothing is shown as right or wrong until
  you press **Reveal** (or Space). After the result, the next turn only starts when you press **Next turn** (Space),
  and after the last item **Show results**. A turn that runs out of time also waits for you. Players see "Locked
  in!" and then "Right! +1" or "Not quite." on their own screen.

### Fixed
- After an item was placed, the ✗ from an earlier wrong guess no longer stays on the timeline in the wrong spot.

## [0.6.0a] — Timeline: take turns putting things in order

### Added
- **Put in order: new "Timeline" way to play.** In the editor, switch "How it's played" to **Timeline — take turns**.
  Give each item what it shows once it's placed (e.g. its year), and mark the one that **starts** on the timeline (it
  can be anywhere: oldest, newest or in between). Up to 10 items.
  - Players take turns, fewest points first. On your turn you click the gap where the next item goes and lock in.
  - Right: it's placed with its year, you score right away and place the next one too (or switch off "Whoever is
    right keeps going" to pass the turn every time).
  - Wrong: the gap is crossed out and the next player tries the same item. If nobody gets it, it's placed for free.
  - Each turn has its own timer. The host can skip a player or place the item for free.
  - The sample quiz has a new question 12 to try it.

### Changed
- Quiz files are now format 4 (for the timeline). Older files still load.

## [0.5.2a] — AnyConverter included

### Added
- **AnyConverter comes with Quiznight (Windows).** It's in the new AnyConverter folder next to the game. When you add a
  video question with an MP4 (or WebM, MOV, MKV), the guide now has an **Open AnyConverter** button: it opens with your
  video and .ogv already picked, you press Convert, then "Use the converted video". No more typing commands into a
  terminal. The first time, AnyConverter helps you install FFmpeg, the free tool that does the converting.

## [0.5.1a] — lobby fix

### Fixed
- **The lobby now has the two host-control checkboxes** ("I show each question myself" and "I unlock the buzzers
  myself"). Both were always on before and couldn't be switched off. Untick them for a faster game where questions
  appear right away and the buzzers are open from the start.

## [0.5.0a] — more question types, host control, seamless WoW map

### Added
- **The game updates itself.** When a new version is out, Quiznight downloads it at start, restarts and
  then shows you what's new (also under "What's new" at the bottom of the home screen).
- **Linux version** (64-bit, with Steam): download the `-linux.zip`, unzip it and start `Quiznight.x86_64`
  (if it won't start, make it runnable first: `chmod +x Quiznight.x86_64`).
- **German translation:** the whole game is now also in German. Pick the language under Settings;
  on a German Windows it starts in German by itself. Quiz questions stay as they were written.
- **Settings screen** (button at the top right of the home screen):
  - Volume for everything, for music in questions (songs and clips in sound/video questions), for game music
    and for sound effects.
  - Which screen the game opens on, window mode (windowed, fullscreen, borderless) and window size. Sizes come
    in 16:9, 16:10, 21:9, 32:9 and 4:3, and only the ones that fit your screen are offered (plus its own
    resolution), so a 1440p screen can't pick 4K.
  - Language: English or Deutsch.
  - Changes only count once you press **Apply** (volumes and language preview right away). **Discard** goes
    back, **Reset to defaults** fills in the standard values, and leaving with unsaved changes asks first.
- **Video questions explain how to use an MP4:** pick or drop an MP4 (or WebM, MOV, MKV) and a short
  3-step guide opens. It shows exactly what to paste to turn it into the .ogv format the game can play
  (with the free tool ffmpeg), then adds the converted video with one click. Also there as a link
  under "Choose file…".
- **Buzzer sounds:** a sound plays when someone buzzes in — on the host's screen and for every player. Pick "Soft beep", "Metal pipe",
  your own sound file or no sound under Settings (the default), or in the lobby for one night. Follows the
  Sound effects volume. If the host uses their own sound file, players hear their own default buzzer instead.
- **Podium jingle:** a short tune plays on the host's screen when the final results appear. It has its own
  "Game music" volume slider in Settings (separate from the music in quiz questions).
- **Seamless WoW map:** WoW map pin questions now use one big map, like the OpenMap addon. Scroll from the
  whole world into a continent, then into every zone stitched together, and on into the cities; drag to move,
  or jump straight to a zone. No more clicking through world → continent → zone. Pins can go anywhere. At
  the reveal, the map zooms to the answer and everyone's guesses. Zone art: the OpenMap addon (thanks!).
- **New question type: Image reveal.** Upload a picture and choose how it's hidden: **zoomed right in** (click
  the spot it starts on) and slowly zooming out, or **pixelated** and slowly getting sharper, over as many
  seconds as you like (5–120). It shows on the host's screen only; players buzz in, it pauses while someone
  answers, and at the reveal everyone sees the whole picture and how many seconds it took. The editor has a
  live preview.
- **New question type: Put in order.** Type 3–6 things in the right order (e.g. games by release date) and
  what the top and bottom mean ("Oldest" / "Newest"). Players get them shuffled and sort them with ▲ ▼. A
  perfect order scores; the reveal shows how many each player had in the right place.
- **New question type: Write the answer.** Everyone types their answer. Answers matching yours are ticked
  automatically — case, accents, "the" and small typos don't matter, and you can accept several answers
  ("Mount Everest / Everest"). At the reveal you can tick or untick any answer and the points follow.
- **You decide when each question appears.** A new question starts hidden: on your screen it's frosted over
  and players only see "Get ready…". Read it out first (the new **Host notes** window shows the question and
  answer — if you share only the game window in Discord, nobody else sees it), then press **Show question**
  or Space. Timers, music, videos and image reveals only start then.
- **You unlock the buzzers.** On buzzer questions the buzzers stay locked (grey) until you press **Unlock
  buzzers** or Space, and you can lock them again any time. Both are lobby rules you can switch off.
- **Multiple choice questions:** a third kind of classic question where several answers can be right. Tick every
  correct answer in the editor; players pick all they think are right and then lock in. Only players who pick
  exactly the right ones get the points.

### Changed
- The game runs at most at 60 FPS, so it no longer heats up your PC for nothing.
- Zoomed-in maps (Skyrim, GTA …) are drawn smoothly instead of blocky.
- **A B C D answers are shuffled every game.** In the editor you just type the answers and tick the right one —
  no letters anymore. When you host, the order is mixed up, so the right answer isn't always B just because
  that's where you typed it. Each new game gets a new order.
- Quiz files are now format 3 (for multiple choice). Older files still load; files with multiple choice
  questions show up as buzzer questions in older versions of the game.

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
