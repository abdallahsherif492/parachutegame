# Sumo Noodles — Voice Lines for Gemini TTS

## قبل ما تبدأ (مهم)

- **اللغة:** كل الجمل بالإنجليزي. أغلب لاعيبة CrazyGames من أمريكا وأوروبا والبرازيل، والجمل القصيرة دي مفهومة لأي حد.
- **الطول:** كل جملة لازم تكون **أقصر من ثانية ونص** (ما عدا اللي مكتوب جنبها غير كدة). اللعبة سريعة، والجملة الطويلة هتتقطع أو هتدخل في اللي بعدها.
- **التنويعات:** الجمل اللي بتتكرر كتير (الضربة، الرينج أوت، البداية) عملتلها 2–3 نسخ بكلام مختلف، واللعبة هتختار واحدة عشوائي عشان الصوت ميبقاش ممل. لو حبيت، اعمل لكل نسخة take تاني بأداء مختلف شوية وسمّيه `_b`.
- **الصيغة:** ‎WAV أو MP3، ‏mono، ‏44.1kHz أو 24kHz. **قُص السكوت** من أول الملف وآخره (دي أهم حاجة، عشان الصوت يطلع مع الحدث بالظبط).
- **أسماء الملفات:** سمّي كل ملف بالاسم المكتوب في الجدول بالظبط (مثلاً `ann_ringout_1.wav`)، وحطهم كلهم في zip وابعتهولي. أنا هربطهم باللعبة وهظبط مستوى الصوت.
- **الطريقة:** لكل شخصية فيه **Voice Card**. الصقه كـ style instructions في Gemini، وبعدين ولّد كل جملة لوحدها، والـ direction اللي جنب الجملة ضيفه في أول الـ prompt. مثال:
  ```
  [Voice card text]
  Direction: explosive, surprised, rising pitch.
  Say: "SUPER SLAP!"
  ```
- **اقتراح أصوات** (لو موجودة عندك في النسخة اللي بتستخدمها؛ لو لأ اختار أقرب صوت): المعلق `Puck` أو `Fenrir`، الـ Rookie ‏`Zephyr` أو `Laomedeia`، الـ Yokozuna ‏`Algenib` أو `Charon`، الـ Yeti ‏`Algenib` بـ pitch واطي، والصغيرين `Leda`.

---

## 1) المعلق — THE ANNOUNCER (أهم جزء)

### Voice Card
```
You are the announcer of "Sumo Noodles", a goofy cartoon sumo game with wobbly noodle arms.
Voice: a warm, playful, over-the-top sports commentator, like a Saturday-morning cartoon
host crossed with a Japanese game-show MC. Big smile you can hear. Punchy and short.
Never mean or sarcastic toward the player; you are always on their side.
Clean studio recording, no music, no reverb, no breathing before the line.
Deliver ONLY the line, nothing else.
```

### A. بداية الجولة (Bout start)
| File | Line | Direction |
|---|---|---|
| `ann_start_1` | "Hakkeyoi!" | The real sumo referee call. Sharp, loud, ceremonial, stretched: "Hak-KEH-yoi!" |
| `ann_start_2` | "Hakkeyoi! Nokotta!" | Ceremonial and energetic. The second word is quicker. |
| `ann_start_3` | "Wrestlers ready... GO!" | Build a short tension pause, then an explosive "GO!" |
| `ann_nokotta` | "Nokotta, nokotta!" | Fast, rhythmic chant like a referee egging them on (plays mid-fight). |

### B. الضربات (Hits)
| File | Line | Direction |
|---|---|---|
| `ann_slap_1` | "Slap!" | Quick, bright, impact word. |
| `ann_slap_2` | "Ooh, right in the belly!" | Wincing but delighted. |
| `ann_slap_3` | "That's gotta sting!" | Playful sympathy with a little laugh. |
| `ann_super_1` | "SUPER SLAP!" | Explosive, the most hyped line in the game, rising pitch. |
| `ann_super_2` | "Noodle POWER!" | Hyped, heroic. Punch the word "power". |
| `ann_super_3` | "What a smack!" | Amazed, almost falling out of the chair. |
| `ann_boing` | "Boing!" | Cartoonish and bouncy, pitch goes up and down like a spring. |
| `ann_headbutt` | "Headbutt!" | Surprised and punchy. |
| `ann_counter` | "Counter slap!" | Impressed, quick. |

### C. خروج من الحلبة (Points)
| File | Line | Direction |
|---|---|---|
| `ann_ringout_1` | "Ring out!" | Big, celebratory, stretched "ouuut!" |
| `ann_ringout_2` | "Out he goes!" | Laughing, joyful. |
| `ann_ringout_3` | "Bye-bye!" | Cheeky, sing-song, waving goodbye. |
| `ann_splash` | "Splash!" | Wet, funny, a little cartoon gasp before it. |
| `ann_hotspring_1` | "Into the hot spring!" | Delighted surprise. |
| `ann_hotspring_2` | "Bath time!" | Cheeky, sing-song. |
| `ann_iceout` | "Brrr! Into the ice water!" | Shivering on "Brrr", then laughing. |
| `ann_flop` | "He flopped!" | Cracking up at a silly fall. |
| `ann_youlose_1` | "Oh no!" | Genuine, sympathetic, not mocking. |
| `ann_youlose_2` | "Ouch! Shake it off!" | Encouraging coach tone. |
| `ann_matchpoint` | "Match point!" | Tense and exciting, lower and slower. |
| `ann_edge` | "On the edge... on the edge!" | Nervous, whispery-excited, speeding up. (up to 2 s) |
| `ann_close` | "Ooh, so close!" | Relieved gasp. |

### D. أحداث في المرحلة (Level events)
| File | Line | Direction |
|---|---|---|
| `ann_banana` | "Banana!" | Warning-funny, like spotting a peel. |
| `ann_slip` | "Whoops! Slippery!" | Laughing, the "whoops" slides down in pitch. |
| `ann_snowball` | "Snowball incoming!" | Alarmed warning, quick. |
| `ann_wind` | "Here comes the wind!" | Dramatic, a little whooshy. |
| `ann_shrink` | "The ring is shrinking!" | Urgent, dramatic. |
| `ann_melt` | "The ice is melting!" | Urgent, a bit worried. |
| `ann_tilt` | "Whoa, it's tipping!" | Wobbly, off-balance voice. |
| `ann_dumpling_1` | "Yum!" | Happy, quick, a small smack. |
| `ann_dumpling_2` | "Dumpling!" | Excited, like finding treasure. |
| `ann_allcollected` | "All the dumplings!" | Proud, celebratory. |
| `ann_survive_5` | "Five seconds left!" | Countdown urgency. |
| `ann_survive_done` | "You held the line!" | Proud, triumphant. |
| `ann_mob` | "Three at once?!" | Disbelief, comedic. |
| `ann_penguins` | "Penguins?! So many penguins!" | Comedic panic. (up to 2 s) |

### E. الباور أبس (Power-ups)
| File | Line | Direction |
|---|---|---|
| `ann_power` | "Power-up!" | Bright, arcade style. |
| `ann_mega` | "MEGA!" | Huge, deep and booming, like the wrestler just grew. |
| `ann_chili` | "Chili time! It's spicy!" | Hot, fanning-mouth delivery. |
| `ann_fish` | "A fish?! Slap him with it!" | Absurd, laughing. (up to 2 s) |

### F. البوسات (Bosses)
| File | Line | Direction |
|---|---|---|
| `ann_boss_intro` | "Boss fight!" | Epic, deep, slow, movie-trailer style. |
| `ann_boss_yokozuna` | "Here comes the Big Yokozuna!" | Awe and fear, big build-up. (up to 2 s) |
| `ann_boss_yeti` | "The Yeti awakens!" | Spooky-epic, slow. |
| `ann_stomp` | "Stomp! Hop!" | Urgent warning. The second word is a clear command. |
| `ann_hop` | "Hop!" | Short, urgent command. |
| `ann_boss_down` | "The boss is DOWN!" | Stadium-level celebration. |

### G. نهاية المرحلة والنجوم (Results)
| File | Line | Direction |
|---|---|---|
| `ann_win_1` | "Victory!" | Big, proud. |
| `ann_win_2` | "Winner winner, noodle dinner!" | Playful, sing-song. (up to 2 s) |
| `ann_win_3` | "Yokozuna material!" | Impressed, grand. |
| `ann_lose_1` | "So close! Try again!" | Warm and encouraging. Must make them want to retry. |
| `ann_lose_2` | "Almost had him!" | Encouraging, upbeat. |
| `ann_star_1` | "One star!" | Pleased. |
| `ann_star_2` | "Two stars!" | More excited. |
| `ann_star_3` | "Three stars! Perfect!" | Maximum joy. |
| `ann_flawless` | "Flawless!" | Stunned admiration. |
| `ann_unlock` | "New fighter unlocked!" | Reward fanfare voice. |
| `ann_newworld` | "A new world awaits!" | Adventurous, wonder. |
| `ann_newthing` | "Something new!" | Curious teaser, plays on the level intro card. |

### H. Survival / Daily Cup / Menu
| File | Line | Direction |
|---|---|---|
| `ann_streak_3` | "Three in a row!" | Building hype. |
| `ann_streak_5` | "Five! Unstoppable!" | More hype. |
| `ann_streak_10` | "TEN! Legendary!" | Maximum hype. |
| `ann_newbest` | "New record!" | Celebratory. |
| `ann_cup_final` | "The final bout!" | Dramatic. |
| `ann_cup_win` | "Champion of the day!" | Triumphant. |
| `ann_gift` | "Daily gift!" | Cheerful, like opening a present. |
| `ann_title` | "Sumo... Noodles!" | Game title callout for the menu. Dramatic pause, then goofy and bouncy on "Noodles". |

### I. Multiplayer (للمرحلة الجاية)
| File | Line | Direction |
|---|---|---|
| `ann_mp_searching` | "Looking for a challenger..." | Calm, a little mysterious. (up to 2 s) |
| `ann_mp_found` | "Challenger found!" | Hyped. |
| `ann_mp_friend` | "Your friend is here!" | Warm, excited. |
| `ann_mp_rematch` | "Rematch!" | Fired-up. |
| `ann_mp_youwin` | "You beat a real player!" | Proud, big. |

---

## 2) المصارع بتاعك — THE ROOKIE (أصوات مجهود مش كلام)

### Voice Card
```
You are a chubby, lovable cartoon sumo rookie with wobbly noodle arms.
Voice: young adult, round and friendly, a bit silly, like an animated movie sidekick.
These are short EFFORT SOUNDS and reactions, not sentences. Exaggerated and cartoony.
Clean studio recording, no music, no reverb. Deliver only the sound.
```
| File | Line | Direction |
|---|---|---|
| `rk_charge` | "Hnnnnnnngh..." | Straining, building up power. About 1.2 s, rising pitch. |
| `rk_lunge_1` | "Hup!" | Short burst of effort. |
| `rk_lunge_2` | "Hyah!" | Martial-arts shout, short. |
| `rk_super` | "HAAAA!" | Big battle cry, 0.8 s. |
| `rk_hit_1` | "Oof!" | Belly got hit, air knocked out. |
| `rk_hit_2` | "Ow!" | Quick, surprised pain, comedic. |
| `rk_fall` | "Waaaaah!" | Falling off a stage, fading as if falling away. 1.2 s. |
| `rk_hot` | "Hot hot hot!" | Jumped into a hot bath, fast and high. |
| `rk_cold` | "Brrrr!" | Freezing, teeth chattering. |
| `rk_slip` | "Whoa-whoa-whoa!" | Losing balance, wobbly. |
| `rk_hop` | "Hop!" | Light, bouncy. |
| `rk_win_1` | "Yosh!" | Japanese "alright!", pumped fist. |
| `rk_win_2` | "Yatta!" | Japanese "I did it!", pure joy. |
| `rk_eat` | "Mmm!" | Happy munching, eating a dumpling. |
| `rk_grow` | "Whoa, I'm HUGE!" | Amazed, voice drops deep on "HUGE". |

---

## 3) الأعداء

### Big Yokozuna (Boss 1)
**Voice Card:** `A giant, proud, jolly champion sumo boss. Very deep, booming, slow belly laugh. Cartoon villain, not scary for kids. Clean studio, no reverb. Only the line.`

| File | Line | Direction |
|---|---|---|
| `yk_laugh` | "Ho ho ho!" | Deep belly laugh, proud. |
| `yk_stomp` | "HRAAH!" | Heavy effort as he jumps to stomp. |
| `yk_taunt` | "Too small!" | Smug, slow. |
| `yk_hit` | "Hmph!" | Annoyed grunt. |
| `yk_lose` | "Impossible!" | Dramatic disbelief, fading. |

### The Yeti (Boss 2)
**Voice Card:** `A huge, fluffy snow monster who is secretly goofy. Deep growly voice, rumbly, cartoon. Not scary. Clean studio, no reverb. Only the sound.`

| File | Line | Direction |
|---|---|---|
| `yt_roar` | "RAAAWR!" | Big roar, 1 s. |
| `yt_throw` | "Hrrngh!" | Throwing a heavy snowball. |
| `yt_stomp` | "HUUUH!" | Heavy jump. |
| `yt_hit` | "Grumph!" | Offended grunt. |
| `yt_lose` | "Nooo... brrr..." | Sad, falling into icy water. |

### Small enemies
**Voice Card:** `Tiny cartoon creatures. High-pitched, squeaky, fast, adorable. Clean studio. Only the sound.`

| File | Line | Direction |
|---|---|---|
| `mini_hup` | "Hup!" | Squeaky, tiny effort. |
| `mini_fall` | "Eeeee!" | Tiny squeal fading away. |
| `peng_1` | "Wenk!" | Penguin honk, cute. |
| `peng_2` | "Wenk wenk!" | Two quick honks, excited. |

### Normal rivals (one voice for all, I'll change the pitch per character)
**Voice Card:** `A cartoon sumo rival, cocky but lovable. Medium-deep voice. Short effort sounds. Clean studio. Only the sound.`

| File | Line | Direction |
|---|---|---|
| `rv_lunge` | "Hah!" | Short effort. |
| `rv_hit` | "Ugh!" | Hit in the belly. |
| `rv_fall` | "Nooooo!" | Falling away, 1.2 s. |
| `rv_taunt` | "Come on!" | Cocky, challenging. |

---

## الأولويات لو الوقت ضيق

لو هتعمل جزء بس دلوقتي، ابدأ بدول (**24 ملف**) لأنهم أكتر حاجات بتتكرر في كل ماتش:

```
ann_start_1, ann_start_2, ann_slap_1, ann_slap_2, ann_super_1, ann_super_2,
ann_boing, ann_ringout_1, ann_ringout_2, ann_splash, ann_youlose_1,
ann_matchpoint, ann_win_1, ann_lose_1, ann_star_3, ann_hop,
rk_charge, rk_lunge_1, rk_hit_1, rk_fall, rk_win_1,
rv_hit, rv_fall, ann_hotspring_1
```

## إزاي هستخدمهم في اللعبة

- المعلق بيتكلم **جملة واحدة كحد أقصى كل ~2 ثانية**. لو حصلت حاجتين ورا بعض بيقول الأهم (Super > Ring out > Slap)، عشان ميبقاش رغاي.
- أصوات المصارعين بتطلع مع الحركة نفسها، وبيتغير الـ pitch بتاعها حسب حجم المصارع (الصغير بيطلع صوته أرفع، والبوس أتخن).
- فيه زرار **كتم للمعلق** منفصل عن المؤثرات الصوتية.
