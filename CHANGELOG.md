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

## [0.10.3b]

### Added
- **A music player on the home screen,** under "Host a quiz night": the song playing, back, play/pause, next and a button for the jukebox's list of songs. The long text in the host card is gone, so the card is smaller. (The note button in the corner stays on every other screen.)
- **"Fußspuren?"** joins the title screen's easter eggs.

### Changed
- **The first download is much smaller.** The music is now encoded at 128 kbps from the original recordings (was ~150 kbps; it sounds the same), the F1 Podium sound is a compressed Ogg instead of a 23 MB WAV (it was 134 seconds of uncompressed audio), the built-in maps are re-compressed (a little more compression; when you zoom in all the way you can't tell the difference), and the game leaves out the parts of .NET it never uses and .NET's debugging tools (the folder next to the game went from 79 MB to 23 MB). This update itself is bigger than usual, once, because those files all changed.
- **Buttons look like real 3D keys:** under the coloured top there's a side in a darker shade of the button's own colour, with a light edge along the top and a soft shine. Pressing one pushes it down into its side.
- **The buzzer is a big square red key on a silver base,** in the same style, with the Quiznight Q? embossed on it instead of "BUZZ!".
- **The jukebox's buttons are drawn icons** (back, play/pause, next) instead of the ⏮ ⏭ characters, which didn't look right in every font. The song playing is marked with a drawn note.
- **The music waits for the voice intro.** When the game opens, the voice line plays on its own and the first song starts from the beginning once it's done (before, the song already played underneath, turned down).
- **"Add a question" lists the question types alphabetically** (by the name you see, so in German too), and the type cards are keys like the buttons.
- **The BETA stamp is a sticker on the logo's corner,** so "Quiznight?" sits exactly in the middle of the home screen again (the stamp next to it pushed it to the left).

### Removed
- **"Write the answer" as its own question type.** It was the same as a classic question with "Everyone types the answer", so that's what it is now: older quizzes and share codes with "Write the answer" questions open them as classic questions played that way (question, answers, picture, time limit, points and bonus kept), and old nights' stats for it count under Classic → Everyone types.

### Fixed
- **The Fast Pass lost to someone who buzzed at the same moment.** Bob had the Fast Pass, but Ann's buzz arrived a few milliseconds earlier (both showed 2.6 s), so Ann was already answering and the Fast Pass only put Bob first among those still waiting, behind her. A Fast Pass now also takes over from someone who started answering less than a second earlier (they couldn't have said anything yet); they're next in line instead. Someone who has been answering for longer keeps their turn, as before.
- **The text on big buttons sat too low** ("My quizzes", "Join!"). The dark edge under each button counted as part of the button when the text was centred, so the text was centred on the button plus its edge and ended up below the middle of the coloured part. It's now centred on the coloured part.
- **"What's new" showed the notes in a narrow column** on the left half of the window. The window's scroll area didn't give its content the full width, so the wrapped text was only as wide as the heading. It now uses the whole width, and opens at the top instead of sometimes scrolled to the end.

## [0.10.2b]

### Added
- **A jukebox on every screen.** The little note button in the bottom left opens it: switch the music off (it stays off next time you start the game) or on again, skip to the next or previous song, or pick a song from the list of what this screen plays. The "Now playing" card slides in right next to it.
- **The mouse's thumb buttons go back and forward,** like in a browser. Back works on every screen except the home screen, the same as Escape. Forward opens the screen you just came back from again (from Settings back to the home screen, then forward to Settings again). Going somewhere new by clicking starts the history over, like a browser does.
- **Ctrl+S** applies your changes in Settings and saves the question in the question editor (the same as pressing Apply or "Save question").
- **A Quit button on the home screen,** next to Settings. Pressing Escape on the home screen now asks whether you want to quit the game, too. Before, there was no way to close Quiznight from inside the game.
- **A welcome screen on the very first start.** Before the intro and the home screen, a new player can pick their language, colour vision help and the dyslexia-friendly font, so they can read the game from the start. It only shows on a PC that has never run Quiznight before: updating never brings it up.
- **A new skin: Pinke Geilheit,** soft pastel pink and lavender on dusky rose.
- **Sounds for right and wrong answers.** When the host marks a buzz right or wrong, everyone hears it, the same for each timeline placement. At the reveal of questions everyone answers, each player hears whether they got it, and the host's screen plays the right sound if anyone did (the wrong one if nobody did). Questions already judged buzz by buzz stay quiet at their reveal, so nothing plays twice. They follow the Sound effects slider.
- **Three new songs** for the menus, the lobby and quizzes: "Before Chill" (Yomoti), "Drive-Through Dinner" (Magnus Ringblom) and "Sunny San Francisco" (Oakwood Station). They're in the jukebox list too.
- **Olf?** joined the title screen's easter eggs.

### Changed
- **The old Pinke Geilheit skin is now called Quiznight Purple.** Nothing about it changed, and if you had it picked, you still have it; the name now goes to the new pink skin.
- **Volume sliders renamed.** "Game music" (menus, lobby and the podium sound) is now just **Music**, and the slider that was called "Music" (songs and clips in sound and video questions) is now **Video**, so it's clear which is which.
- **All volume sliders start at 50 %.** That is the new standard for everyone: if you already played, your volumes are set to 50 % once with this update, and the home screen tells you so (turn them back up in Settings if you liked it louder). New players start at 50 % too.
- **The F1 Podium sound is 10 dB louder.** It was much quieter than the menu music around it.
- **The award pack explains itself.** "Save award pack" used to have a long tooltip nobody read. Pressing it now opens a window that says what's in the pack, with two real examples drawn from the table you're looking at (the standings poster and the first award card), and a Generate button that makes it.
- **The bars under "Points per night" on a profile light up under the mouse** and show that night's place and points above the bar (the tooltip still adds the date and the quiz).

### Fixed
- **Settings (and every other screen) looked broken with the dyslexia-friendly font.** The text was spread out with big gaps between all the letters, so the video settings ran off the right edge of their card and got cut off. To make OpenDyslexic fit, the game squashed its letters a little sideways, but Godot kept each letter's full width, so the squashed letters just stood further apart. The letters now keep their real shape and sit a little closer instead, and the small headings in capitals no longer get extra spacing on top of that.
- **Settings said "Unsaved changes" as soon as it opened** when the saved window size didn't fit the monitor (it picks one that fits). That swap no longer counts as a change you made.

## [0.10.1b]

### Added
- **Escape goes back.** On every screen except the home screen, Escape takes you one screen back, like the "← …" link at the top: from a question to the quiz, from the quiz to My quizzes, from Settings or the Ewige Tabelle to the home screen. In a room it does what leaving means there: a player in the waiting room leaves. For things you can't undo, it asks first: closing the lobby, leaving a running game, or (as the host) ending the quiz early. Open windows, drop-downs and the big picture view close first, as before.
- **Crash reports.** If Quiznight crashes, or runs into an error in its code, the next start sends the end of that run's log (the last 400 lines, plus version, system and date) to Max, so he can see what went wrong and fix it. Your Windows user name is cut out of the file paths in it, and each log is sent only once. Only the real game does this, never test runs. Settings says so under Language. (Quiznight has always written these logs to %APPDATA%\Quiznight\logs; until now nobody ever saw them.)

### Changed
- **Thinner outlines on small buttons.** The party show look gave every button a thick ink outline and a big solid edge under it. On big buttons that looks great; on small ones like the host's − and + next to the scores, ↑ ↓ × in the question list, or the pills and chips, it looked crowded and heavy. Small buttons, drop-downs and toggles now have a thin outline and only a hint of an edge; pills and chips have no edge at all. Big buttons and cards stay chunky.
- **A drawn lightning bolt for the Fast Pass.** The Fast Pass joker used the ⚡ emoji, which looked like it came from another app next to the drawn icons. It's now a drawn yellow bolt with an ink outline, on the joker buttons, the lobby rule, the scoreboard and the buzz order.
- **The music steps back for previews.** When you press ▶ to hear a buzzer or podium sound (in Settings or the lobby), the menu music now fades out, so you actually hear the sound, and fades back in once it's over or you stop it. The same happens while the podium sound plays.
- **"Top 3 score (3 / 2 / 1)" is gone from the scoring list,** because "Podium (3 / 2 / 1)" under Points per place does the same. Questions that already use it keep it (it still shows for them).

### Fixed
- **The profile page's "Form" column was squashed.** With fewer than five nights played, the column on the right got only as wide as its few pills, so "First quiz night: 24.10.2026" and "Beta (before 1.0)" broke after every word into a tall, messy stack. The column now always has room for five pills, and those lines fit.
- **The preset list's dot never moved.** In the Scoring window, "Start from a preset…" always kept the selection dot, whichever preset you picked, because the list jumped back to it after every choice. The dot now stays on the preset your points match (and goes back to "Start from a preset…" once you change them).
- **Big buttons grew out of their space.** The party show look makes buttons grow a little under the mouse. On wide ones, the question rows in a quiz and the cards in "Add a question", that made them poke out of their list, so their left edge got cut off. Rows and cards that big now keep still (their colour still changes under the mouse).

## [0.10.0b] — "Season Ticket": a party show look, music, seasons, profiles, awards and sentences in parts

### Added
- **A whole new look: the party show.** Quiznight? now looks like a TV game show. Buttons and cards are chunky, with thick outlines and a solid edge under them, so they look like you can press them (and they squash when you do, and grow a little under the mouse). The screens stand on a stage with a warm spotlight glow. There are two new fonts: Fredoka for text (round and friendly) and Lilita One for headings, names and big buttons. The colours are still Quiznight's purple, orange and teal.
- **The logo is an answer field.** On the home screen, "Quiznight?" sits in a text box with a blinking cursor, and two spotlights sweep over it. Every now and then it deletes itself and types something else… you'll see.
- **A real buzzer.** The BUZZ! button is now an arcade button: a red dome with a shine on a chrome ring. It squashes down when you press it and turns grey while the buzzers are locked. The icon for buzzer questions is a little dome too.
- **Music.** Five songs from Epidemic Sound play in the menus, the lobby and during quizzes, shuffled. "Easy Breezy" by Megan Wofford always opens the game. During sound, video and interactive questions the music pauses, so it never covers the clip or your microphone, and it carries on afterwards. When a new song starts, a small "Now playing" card shows its title and artist in the bottom left. In December (1st to 26th) a Christmas song joins in. The music has its own volume: "Game music" in Settings.
- **A voice welcomes you** when you open the game (with a bit of reverb), and the music steps back while it talks.
- **Button sounds.** Buttons click when you press them.
- **A splash screen.** While the game loads you see the Quiznight? logo instead of Godot's, and when it's ready the "?" hops and the logo fades into the home screen. A click or any key skips it. "What's new" and the update question now wait until it's gone.
- **Accessibility** (a new card in Settings):
  - **Colour vision:** for red-green (deuteranopia or protanopia) or blue-yellow (tritanopia) colour blindness. The game shifts its colours so the ones you can't tell apart become different: right and wrong, the A–D answers, medals and player colours.
  - **Dyslexia-friendly font:** every text in OpenDyslexic, letters with heavy bottoms so they don't flip or swim.
  - **Screen readers** (like NVDA or Narrator) can read the game: buttons, texts and fields as always, and now also the buzzer, profile pictures, question type icons and the graphs (as a list of who's where). During a game, the reader announces what happens: the next question and its type, the question once it's shown, when the buzzers open, when it's your turn, and the answer with your points at the reveal.
  - **Less motion:** if you turned off animations in Windows (Settings → Accessibility → Visual effects), the splash, the spotlights, the typing logo and the bouncing buttons keep still.
- **A new way to play step by step: one sentence in parts.** In the editor, pick "One sentence in parts" and type the question in 2 to 5 pieces, in reading order (for example "This city, founded in 1237," / "was split by a wall from 1961 to 1989" / "and is Germany's capital today."). Players see the first part; the rest of the sentence stays blurred. The host reads on with "Read on (→)". Buzz in as soon as you know it: the earlier, the more points. With 5 parts, a right buzz after the first one gets 5 points, after the second 4, and so on. Double points doubles that, and a Fast Pass jumps the queue like always. As soon as someone buzzes, the reading stops: the host can only read on once that answer is judged, and after a wrong answer everyone can buzz again on the next part. Players never get the hidden parts (their screens only get the parts read so far), so nobody can peek.
- **Seasons.** The Ewige Tabelle has a "Table" picker: all time, one season, or the beta. Season 1 runs from 1.0 until the end of 2027; after that every calendar year is its own season (Season 2 is 2028, and so on).
- **Championship points like in racing.** "Ranked by" lets you rank the table the way F1 would: every quiz night, your place gets points (F1 since 2010: 25 for a win, 18 for second, 15 for third …). The other systems from the scoring presets are there too (older F1 systems, MotoGP, IndyCar, NASCAR, Mario Kart, 3/2/1), and "Quiz points" ranks by the points from the questions like before. The championship points are the last column, with the quiz points next to them.
- **The Ewige Tabelle per question type, subtype and category.** The "Questions" picker shows the table for one kind of question, with the columns that matter for it. Pick "Map pin" for pins, Ø km off, best pin ever and pins under 1 km; "Estimation" for Ø % off, closest guess and bullseyes (under 1 % off); a buzzer type for buzzes, wrong buzzes, buzz hit rate, Ø buzz time and fastest buzz ever; "Step by step" for the hint people get it at on average and how often they get it at the very first one; "Timeline" for placements and how many were right; typed answers for how many were spelled perfectly. Every type can be narrowed to its subtype (buzzer or everyone types, A B C D or multiple choice, each map, timeline or everyone at once, hints or sentence …) and to each category. These stats count from this version on: the nights before didn't save what happened in each question, so they can't be split up.
- **Position and points graphs.** "Show: Positions" draws everyone's place after each quiz night, like an F1 lap chart, with the names at both ends. "Show: Points" draws everyone's points climbing from 0. Click a name (on the chart or in the list beside it) to highlight that player, and hover the chart to see the night and everyone's place or points then. A night in the match history also gets both graphs, question by question (for nights from 0.10.0b on).
- **Profile pages, Football Manager style.** Click a name in the Ewige Tabelle, the match history or a rival line to open that player's profile:
  - **The top:** a big profile picture, a role line ("Speed buzzer · Map specialist"), their place in the table, nights, wins, podiums, "Hosted" (how many nights they were the quizmaster, where FM shows international caps), points and the form of their last five nights.
  - **Attributes rated 1 to 20** against everyone in the table: buzzer speed, quick answers, composure (how rarely their buzzes are wrong), spelling, consistency, winning habit, big-game player, joker timing, and one per question type (Map sense, Estimating, Knowledge, Ear, Deduction …).
  - **A radar chart** of the question type attributes, and a **football pitch** with the eleven question types as the eleven positions: green where they're a natural, red where it's not their game.
  - **A scouting report:** their three strengths and three weaknesses (the types, subtypes and categories where they beat or trail everyone's hit rate the most), records (fastest buzz, closest pin, closest guess, most points and most right answers in a night, longest winning streak, Double points played and what they brought) and their rival: the player they finished right next to most often.
  - **Below that:** their points per night as bars (gold, silver and bronze for podiums) and their career, night by night (click a night to open it).
- **Spelling is measured for real.** When you type an answer, the game already forgave small typos. Now it also notes how many letters were off from the closest accepted answer (capitals and accents don't count). The Spelling attribute and the "Spelled right" column use that: the share of right typed answers without a single typo.
- **Points from the host, and the "Host's Favourite Puppy".** Bonuses the host hands out at the reveal and score changes by hand (+ / − by the scores) now count as "points from the host". The profile shows them, and whoever got the most wears the "Host's Favourite Puppy" badge, on their profile and next to their name in the Ewige Tabelle.
- **Award pack.** "Save award pack" in the Ewige Tabelle makes a ZIP of pictures to share at the end of a season (Documents/Quiznight/Pictures; it opens the folder when it's done). There's a poster of the standings, with the champion in the spotlight, the podium and the full table. Then there's an award card for the best at every question type (Map specialist, Golden ear, Detective …) and every category, the Host's Favourite Puppy, the fastest buzzer and the spelling bee, each with the winner and who came second and third.
- **Question categories (optional).** Every question can get a category in its round settings: one of the suggestions (Geography, History, Music, Film & TV, Games, Sport …) or your own. Players only see it if the host turns on "Show question categories" in the lobby, because it makes questions easier. Then it shows on the "Get ready" screen and next to the question. Tick "Keep it secret" on a question whose category would give the answer away. Categories always count for the stats per category, shown or not.

### Fixed
- **The metal pipe buzzer was late.** Its sound file started with 1.37 seconds of silence before the clang, so the buzz sounded long after someone had buzzed. (An earlier fix was meant to cut it but didn't.) The silence is gone: the clang now comes right away.
- **Videos had big black bars.** A video question's player filled the whole width of the question and painted everything around the video black, so a normal 16:9 clip sat between two wide black bars and looked like a broken player. Now the video is as big as it fits at its own shape, with an outline around just the video and nothing painted around it.

### Changed
- **Answer time and buzz time are two separate numbers.** Until now one "Ø answer time" mixed both: a buzz counted its time to buzz (about a second), a typed or picked answer its time to lock in (often 10 seconds or more). So whoever buzzed a lot looked fast, and that mixed number decided ties. Now "Ø answer time" only counts answers you locked in and recordings you sent, and "Ø buzz time" counts buzzes, each with its own column in the Ewige Tabelle, a night's table and tonight's Stats. Ties go by answer time first, then buzz time. Nights before this version only have the old mixed number (shown as answer time, buzz time "–").
- **Points are the last column of the Ewige Tabelle**, like in a football table: the averages come first, the points (or championship points) at the right end. The per-type views end with the points from that type.
- **The results of a night save every question.** Besides everyone's totals, a night's results now have one line per player and question (its type, subtype and category, the points, buzzes, times, distances, typos, hints and jokers). All the stats above come from those lines.
- **Quiz files are format 9 now** (they can save categories and the sentence mode). Quizzes from older versions load as they are.

## [0.9.6b] — aligned headings, a peek at 0.10.0b

### Fixed
- **The Ewige Tabelle's column headings line up with their numbers.** The headings are buttons (click one to sort), and buttons have a little space on their left, so every heading sat a few pixels right of the numbers below it ("Nights" started after the "1"). The headings no longer have that space, and the number columns are now centred under their heading, so heading and numbers sit on one line down the column. "Player" stays left, above the pictures.

### Coming next
- **0.10.0b is on its way, and the Ewige Tabelle is about to get very nerdy.** Who's the fastest buzzer of all time? Whose map pin landed closest ever? Who always gets step by step on the very first hint? Soon you can look at the table one question type at a time, with all the stats nobody asked for. Then click a name: every player gets a profile page like a Football Manager scout report, with attributes rated 1 to 20, a form guide of the last nights, and a verdict on what kind of quizzer they are. Speed buzzer? Map specialist? Estimation sniper? We'll see. And quizmasters might get a way to tell you what a question is about before it starts… if they feel generous. One catch: those stats only start counting with 0.10.0b, so the nights before won't show up there.

## [0.9.5b] — Ewige Tabelle arrows, places by column

### Added
- **Arrows in the Ewige Tabelle.** Next to each place you now see how it changed with the newest quiz night: a green ▲2 if you went up two places, a red ▼1 if you dropped one, and a blue "New" if it was your first night. Hover an arrow to see your place before. It works for any column you sort by.

### Changed
- **The places follow the column you sort by.** In the Ewige Tabelle, # always showed everyone's place by points, so when you sorted by answer time the fastest player could be at the top marked "3rd". Now the best in the sorted column is 1st (whichever way round you sort it), equal values share a place, and only points use the faster answer time to break ties.
- **Places sit in coloured boxes, with lines between the rows.** In the Ewige Tabelle, a night's full table in the match history and tonight's Stats, the places were plain coloured text that sat higher than the profile pictures next to them. Now each place is a small box: gold, silver and bronze for the top 3, plain after that, centred with the picture. A thin line between the rows makes it easy to read a row across.

### Fixed
- **Players on the same points were in the wrong order in the Ewige Tabelle.** Two players on 11 points could show "3rd" above "2nd". The places break ties by the faster average answer time, but the rows were put in order by name, so the two didn't match. The rows now follow the places.

## [0.9.4b] — statistics with a match history, update check while open

### Added
- **The game looks for updates while it's open.** Until now it only checked once, when you started it, so if a new version came out while you were building a quiz you didn't find out until the next restart. Now it checks every minute in the background. When there's a new version, the home screen shows it like before, and "Update to 0.9.4b" appears in the top bar of "My quizzes", the quiz overview and the question editor. Clicking it first warns you that the game restarts to update, so you can save the question you're working on. It never pops up by itself, and not during a game.
  - Why it doesn't run out of checks: GitHub only allows 60 checks an hour from one internet connection, which two PCs checking every minute would use up. So the minute check only looks at the address of the newest release page (which has no such limit) and asks GitHub's update service only once that shows a newer version.
- **Statistics, with a match history.** The "Ewige Tabelle" link on the home screen is now "🏆 Statistics". It opens the Ewige Tabelle like before, and a switch at the top right goes to the new match history and back (Statistics opens on whichever you looked at last). The match history shows every quiz night so far, newest first, with the quiz's name, who hosted it ("by Max"), the date and a little podium of the top 3's profile pictures (hover one for the name and points). Click a night (or "Details →") to open it on its own screen with the full table: everyone's profile picture, points, how many questions they scored on, average answer time, and how far off their map pins and estimations were. "← Match history" takes you back. Nights before 1.0 carry a "Beta" tag. Who hosted is only known from this version on (older results didn't save it), so the first nights show without a host. In the Ewige Tabelle, the switch between the real table and the beta table now says "From 1.0" and "Beta", so it isn't named "Ewige Tabelle" twice.
- **Your name in bold.** In the Ewige Tabelle and the match history your own name (found by your Steam account) is in bold, so you find yourself at a glance. The other names are no longer bold; the top 3 still have their gold, silver and bronze places.

## [0.9.3b] — how it works, tonight's stats, sortable Ewige Tabelle

### Added
- **"How it works" while you wait.** The "Get ready…" screen players see before the host shows a question used to be mostly empty space. It now says what kind of question is coming and how it works in one sentence, and it knows the variant: a buzzer question tells you to buzz and say it out loud, A B C D to pick one before the time runs out, multiple choice that several can be right, a timeline that you take turns, and so on. It also warns you when wrong answers cost points.
- **Tonight's stats.** Next to "Scores" on the side of the host's and the players' screens there's now a "Stats" link. It opens a table of everyone's night so far: place, points, how many questions they scored on, their average answer time, how far off their map pins were on average and how far off their estimations were (in % of the right number). Players can also switch the "Get ready…" screen between "How it works" and "Tonight so far" to see the same table there.
- **Ewige Tabelle: sort by any column.** Click a column's heading (Wins, Ø points, Ø answer time …) to sort the table by it; click again for the other way round. The # column keeps everyone's real place by points, so sorting never changes who's first. Players without a value in that column (say, no estimations yet) go to the bottom.
- **Ewige Tabelle: profile pictures** in front of every name (Steam pictures; players from LAN games get their initials in a colour of their own).
- **Ewige Tabelle: Ø % off** for estimations: how far off someone's guesses were on average, in % of the right number, so a guess of 110 when the answer is 100 counts as 10 % off. The results of a night now record this too. Nights played before this version didn't, so the column only fills from now on.
- **New tie rule "Faster answer wins"** for map pin and estimation questions. Two players with the same distance used to share a place (or both get nothing, depending on the rule). Now you can choose that whoever locked in first gets the better place. New map pin questions use it by default; for existing ones you can pick it under "If there is a tie".
- **Wrong answers can cost points** in A B C D and multiple choice questions: tick "Wrong answers cost points" in the editor and a wrong answer loses as many points as a right one gets (doubled with Double points, like the win would be). Not answering costs nothing. It's off unless you tick it, and players see a warning on the question when it's on.

### Changed
- **Put in order scores every item in its right place.** An "everyone at once" order question only gave points for a perfect order, so someone with 3 of 4 right got the same as someone with none right, which felt harsh. Now every item in its right place gets the question's points (4 items, 1 point each: 3 right = 3 points). The editor calls it "Points per item in the right place". Existing order questions score this way too, so one that was worth 2 for a perfect order now gives up to 2 per item.
- **Optional minus points for wrong places.** Tick "Wrong places cost points" in an order question and every item in a wrong place takes the question's points off again. It's off unless you tick it; players see a warning on the question when it's on. (Timeline questions are unchanged: they already score per item.)
- **Map pin ties go by the distance you see.** Two pins were only a tie if their distance was exactly the same down to the last decimal, which basically never happens, while the reveal showed both as, say, "312 m". Now pins at the same shown distance (to the metre, or 0.1 % on maps without a scale) count as a tie, and the tie rule decides.

## [0.9.2b] — scoring per place, units, UI polish

### Added
- **Scoring per place for estimation and map pin questions.** Until now these questions had two ways to score: only the closest player wins, or the top 3 get 3, 2 and 1 times the question's points. Now there's an "Edit" button next to "Scoring" in the editor. It opens a window where you set the points for every place yourself: 1st, 2nd, 3rd and as many places as you like ("+ Place", "− Place").
  - Start from a built-in preset: F1 since 2010 (25-18-15-12-10-8-6-4-2-1), the older F1 systems (2003–2009, 1991–2002, 1961–1990, 1950–1959), MotoGP, IndyCar, NASCAR Cup, Mario Kart 8, or a simple podium 3 / 2 / 1.
  - Change any place, give it a name and "Save as my preset": it's kept on your PC and shows up in the Scoring list of every question from then on. Your own presets can be deleted again in the same window.
  - The points are saved inside the question itself, so a quiz you share scores exactly the same on someone else's PC, even if they don't have your preset.
  - Places after the last one get nothing. Players who are exactly as close as each other share the better place's points (unless the question says "Nobody gets the point" for ties). Double points doubles whatever a place gets.
  - During the game the top bar says "up to 25 points" instead of a fixed number, since what you get depends on your place.
- **Units are a drop-down.** The unit field of an estimation used to be a free text box with "e.g. hours" in it, which made it easy to type the same unit three different ways across a quiz. Now it's a list grouped by kind: length (mm to light years, AU and parsecs), mass, time, speed, area, volume, temperature, energy and power (including horsepower and PS), data, money and a few others. "Custom…" at the bottom still lets you type any unit, and units from older quizzes that aren't in the list (like "keys") show up there, so nothing changes for existing questions.

### Changed
- **Decimal numbers keep every digit.** Estimation answers were shown with at most two digits after the point, so an answer of 0.00000034 AU showed as "0" at the reveal and a guess that was 0.00000002 off looked like "spot on". Now every digit you type is kept and shown, both in the editor and at the reveal. You can type 2.5 or 2,5 (both work, for the editor and for players' guesses), and the game shows a comma when it's in German. Tiny rounding errors computers make (0.1 + 0.2 = 0.30000000000000004) are hidden, so differences read cleanly.
- **The answer timer on "Your turn!" is readable.** The small "Time to answer" label was orange text on the orange panel, so you could barely see it. It's now dark like the rest of the panel's text, and a bit bigger.
- **Score lists don't cut off clan tags any more.** In the narrow score rows a name like "[LCB] BassGaming" became "[LCB] BassGami…", so the actual name was the part that got lost. Score rows now show the name without the tag; hover over it to see the whole name. Seats in the lobby and the podium still show the tag on its own line above the name.
- **Step by step says "up to 5 points".** The top bar always said "5 points", although a right answer is worth one point less after each hint. It now says "up to", and "Right now: 3 points" under the cards still shows the current value.
- **Interactive questions count recordings, then votes.** The side panel said "Answers: 2 / 4" while players were recording, which was confusing. It now says "Recordings: 2 / 4", and "Votes: 2 / 4" while voting.
- **The same arrows everywhere.** Slideshows flipped with ◀ ▶, recordings with ◀ ▶▶. Recordings now use ◀ ▶ too, and the play button between them says "Play it", so it can't be mistaken for "next".
- **The recorder's microphone is a drawn icon.** It was the 🎤 emoji, which the game's font doesn't have, so some PCs showed a strange pen-like shape instead. It's now drawn like the question type icons.
- **"Hold to peek at the answer" matches the buttons next to it.** It was a smaller button than "Reveal answer" beside it, so the row looked uneven.
- **Lobby rules have a heading for each group.** "Jokers" had a little heading, but the two checkboxes above it didn't, so the list looked like it was missing something. They're now under "How you run it".
- **Ewige Tabelle shows metres for small distances.** The "Ø km off" column showed "0.0 km" for map pin guesses on game maps, because those are only a few hundred metres off. Anything under 1 km now shows in metres, like "29 m".
- **No tooltip on the "?" of the question type cards.** Hovering it showed a long tooltip with the same text you get when you click it, which covered the cards below. Clicking the "?" still shows the full help.

### Fixed
- **The waiting room's jokers no longer jump around.** When the host turned a joker off in the lobby, it disappeared from the players' "Tonight's rules" box, which made the box shrink and grow every time. Every joker now stays listed; one that's turned off is greyed out with "Off tonight."

## [0.9.1b] — map corner, peek and step by step fixes

### Changed
- Map pin: while your mouse isn't on it, the map in the corner is see-through, so the screenshot shows through. The pin button keeps it big, and "Hide" folds it away into a small "Map" button to look at the whole screenshot.
- The host's "Hold to peek at the answer" shows the answer in a box above the button instead of stretching the row, and is gone on timeline questions (the order is right there on the cards).

### Fixed
- Step by step: the hint cards on players' screens are smaller, so the buzzer fits without scrolling.
- Host lobby: "Ready" and the × to remove someone are lined up again.

## [0.9.0b] — "Mic Drop": two new question types and everything from the first real round

### Added
- New question type **Interactive**: give everyone a task like "Imitate the Super Mario jump sound". Players record themselves with their microphone (up to 15 seconds), listen back and send it. Then the host plays every recording one by one, on the host's screen and on every player's PC. Players vote for the best (a point per vote, not for yourself), or the host picks a favourite, who gets the question's points. Double points doubles it.
- New question type **Step by step**: up to 5 hints, the hardest first, that the host flips over like cards. Players buzz in when they know it. Right on the first hint is worth 5 points (as many as there are hints), each hint later one less. Every new hint gives everyone another go.
- Steam profile pictures: avatars in the lobby, the game and on the podium now show everyone's Steam picture.
- Map pin questions look like GeoGuessr for players: the screenshot is big, and the map sits in the bottom-right corner. It grows while your mouse is over it, ⤢ keeps it big, "Full" puts it over the whole picture.
- Picture slideshows: once the host has shown every picture, players can flip through them on their own screen.
- Ewige Tabelle: our first quiz night is in! Nights before 1.0 count for the **Beta table**; the real Ewige Tabelle starts with 1.0, and you can always switch to the Beta table there.
- "Let everyone buzz again" for the host when everyone who buzzed was wrong.

### Changed
- Pressing play on a sound or video, and the start of an image reveal, unlock the buzzers by themselves. Resuming after everyone guessed wrong gives everyone another go.
- New map pin questions score the top 3 (3, 2 and 1 points, doubled with Double points) instead of only the closest. Existing ones can switch under "Round settings" in the editor.
- Leave the name field empty to join with your Steam name.

### Fixed
- Clan tags pushed the "Ready" line of that seat lower than the others.

## [0.8.12b] — download percent fix

### Fixed
- The lobby's download percent went negative (around 30–40 %) on quizzes with big videos, so Start never unlocked. The download itself was fine.

## [0.8.11b] — downloads in the lobby

### Added
- Players get the whole quiz (pictures, maps, sounds and videos) while they wait in the lobby. Each seat shows "Downloading 45 %" or "Ready", and Start waits until everyone is ready ("Start anyway" if someone's download is stuck). Nobody can peek: each file stays locked until its question is shown.
- The rings next to the names during a question now count its pictures too, not just sound and video.
- Clan tags: a tag in square brackets before your name ("[OTB] maxlvl01") doesn't count toward the name's length and shows as its own small line above the name.
- "Leave" on the "Connection lost — reconnecting…" bar, so nobody is stuck when the host is gone.

### Fixed
- Over Steam, pictures could stay on "Loading image…" while the timer ran, and "End quiz early" could leave players stuck in the question. The game's own messages got lost while sound and video files were being sent; now nothing is lost and game messages always go first.
- The podium song stops when you leave the podium screen.

## [0.8.10b] — joining fix

### Fixed
- Over Steam, only one player could join a room: the next one ended up in the same seat (they kept swapping instead of each getting their own). Now everyone gets a seat of their own.

## [0.8.9b] — Quiznight? and picture questions

### Added
- New question type **Picture**: show one picture (e.g. a Steam review to read) or several as a slideshow. You flip through them with ◀ ▶ or the arrow keys, and every player's screen follows; players only ever see the pictures you've shown so far. Answered like a classic question: buzzer, everyone types, A B C D or multiple choice, and a bonus works too. Drop several screenshots on the editor at once, or paste them one after another, to build a slideshow.

### Changed
- Quiznight is now **Quiznight?** — like an invitation to a quiz night. The logo, the window title and a new icon say so.

## [0.8.8b] — icon and sound previews

### Changed
- Quiznight has its own icon: a Q and the orange "!" in the logo's font, on purple (the game file, the taskbar and the window).
- Sound previews (buzzer and podium, in Settings and the lobby): ▶ turns into ■ while it plays, so you can stop it. Only one plays at a time, and leaving the screen stops it.

## [0.8.7b] — hotfix 1

### Changed
- The Eternal table is called "Ewige Tabelle" now, in English too.

## [0.8.6b] — map grid

### Changed
- Map pin editor: you pick the map from a grid of pictures (name and width under each) instead of a drop-down. "Change map" brings the grid back; your own uploaded maps are in it too.
- Maps that come with an update get an orange NEW badge in the map grid for that update.

## [0.8.5b] — scores and lobby

### Changed
- Tick boxes are drawn in the skin's colours: blue in Pinke Geilheit, teal in Classic.
- Places read 1st, 2nd, 3rd … (German 1., 2., 3.) on the podium, scoreboards and in the buzzer line, instead of plain numbers or #4.
- In the score tables, 1st, 2nd and 3rd are gold, silver and bronze, and when someone overtakes someone the rows slide past each other into their new places. At the reveal, a green ▲ or red ▼ shows how many places each player moved with that question.
- Only one player per question can use the ⚡ Fast Pass: whoever plays it first. Everyone else's button says it's taken (not by whom). ×2 Double points can still be used by anyone.
- Players: click "⋮ 3 more" in the scores to see everyone (it scrolls). It folds back a few seconds after your mouse leaves it.
- The host's lobby looks like the players' waiting room: big room code, a seat for everyone (× removes a player), and the game rules in two compact columns at the bottom. It fits without scrolling.
- The waiting room shows tonight's rules at the bottom: how the host runs the questions and buzzers, the answer time and which jokers are allowed. It updates as soon as the host changes something.
- Dates in My quizzes read 22.10.2026 (day.month.year).

## [0.8.4b] — new colours

### Changed
- New look, the "Pinke Geilheit" skin: a deep purple background, orange instead of yellow for buttons and highlights, and blue outlines. Prefer the old colours? Settings → Skin → Classic.
- The update button next to the version is just an arrow now: grey (click it to check for updates), green when there is one (click it to install).
- Timelines start with a random item each game, instead of one you pick in the editor.

## [0.8.3b] — fixes

### Fixed
- Pop-up titles sit on a solid bar now, instead of on whatever was behind the pop-up.
- Clicking a question type's name or icon (not just the space around it) opens it again.

## [0.8.2b] — short share codes and update check

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
