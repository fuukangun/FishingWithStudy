# fishingWithStudy

[![Stardew Valley](https://img.shields.io/badge/Stardew%20Valley-1.6%2B-brightgreen)](https://www.stardewvalley.net/)
[![SMAPI](https://img.shields.io/badge/SMAPI-4.0%2B-blue)](https://smapi.io/)

[中文](README.md) | English

**fishingWithStudy** is a Stardew Valley mod that transforms the fishing minigame into a quiz-based learning system. Instead of controlling a bouncing green bar, you answer questions to catch fish. Study while you play!

---

## Table of Contents

- [Features](#features)
- [How It Works](#how-it-works)
- [Installation](#installation)
- [Usage](#usage)
- [Configuration](#configuration)
- [Custom Question Banks](#custom-question-banks)
- [Question Bank Format](#question-bank-format)
- [Study Mode](#study-mode)
- [Compatibility](#compatibility)
- [FAQ](#faq)

---

## Features

### Core Mechanics

| Feature | Description |
|---------|-------------|
| Quiz replaces fishing | When a fish bites, a quiz menu appears instead of the vanilla fishing minigame. Answer correctly to catch it. |
| Single & Multi choice | Questions support both single-choice and multiple-choice formats. |
| Configurable timer | Per-question countdown timer (default 25s, adjustable 5–60s), can be disabled entirely. |

### Difficulty Scaling

Four scenarios based on fish type and treasure presence:

| Scenario | Correct Answers Needed | Notes |
|----------|----------------------|-------|
| Normal fish, no treasure | **1** | Catch the fish immediately |
| Normal fish + treasure | **2** | 1st correct gets treasure, 2nd catches fish |
| Legendary fish, no treasure | **5** | Catch after 5 consecutive correct answers |
| Legendary fish + treasure | **5** | 3rd correct gets treasure, 5th catches fish |

If you get a wrong answer or time out midway, the fish escapes (see [How It Works](#how-it-works)).

### Smart Learning Algorithm

The mod tracks which questions you get wrong and periodically re-tests you on them. After the first full round of all questions, every 5th question randomly inserts a previously-missed one to reinforce learning.

### Quality Bonus

After answering **66+ questions** globally, each successful catch has a **70% chance** to trigger a quality bonus based on your overall accuracy:

| Accuracy | Probability | Quality |
|----------|------------|---------|
| \> 80% | 70% | Iridium |
| \> 70% | 70% | Gold |
| \> 60% | 70% | Silver |

If the 70% roll fails, or you have fewer than 66 total answers, the vanilla fish quality logic applies.

### Stats Tracking

- **Per-scope stats**: Track accuracy per question bank and category combination
- **Global stats**: Track lifetime total answers and accuracy
- Data persists per save file via Stardew Valley's `modData` system

### Additional Features

- **GMCM support**: Full integration with [Generic Mod Config Menu](https://www.nexusmods.com/stardewvalley/mods/5098) for in-game settings
- **Bilingual UI**: Automatically switches between Chinese and English based on your game language
- **Time freeze**: Game time is paused during quiz, so you never lose in-game time
- **Stamina refund**: Casting stamina is refunded on correct answer
- **Fishing XP**: Earn fishing experience based on the fish's difficulty
- **Custom banks**: Load your own question banks from JSON files

---

## How It Works

1. Cast your fishing rod and wait for a bite (same as vanilla)
2. When a fish bites, a **quiz menu** opens instead of the vanilla BobberBar minigame
3. Read the question, select your answer(s), and click **Confirm**
4. Answer enough questions correctly before the timer runs out to catch the fish

**Reward differences from vanilla:**

| Aspect | Vanilla | This Mod |
|--------|---------|-----------|
| Fish quality | Based on perfect catches (keeping bar on fish) | Based on long-term quiz accuracy (triggers after 66 answers) |
| Treasure | Keep bar over treasure icon | Answer one extra question correctly |
| Stamina | No refund | Refund casting stamina on correct answer |
| XP | Bonus for perfect catches | Fixed XP by fish difficulty tier (7/10/13/15) |

**Wrong answer:**

| Scenario | Result |
|----------|--------|
| Normal fish (with or without treasure) | Fish escapes, receive 1 trash, lose 1–3 stamina |
| Legendary fish | Fish escapes, no stamina penalty |
| Treasure obtained but fish not yet caught | Keep treasure, fish escapes |

**Timeout (timer reaches zero):**

| Scenario | Result |
|----------|--------|
| Normal fish (with or without treasure) | Fish escapes, receive 1 trash, no stamina penalty |
| Legendary fish | Fish escapes, no stamina penalty |

**Press Escape**: Cancel immediately, fish escapes, no rewards or penalties.

---

## Installation

### Requirements

- [Stardew Valley 1.6+](https://www.stardewvalley.net/)
- [SMAPI 4.0+](https://smapi.io/)
- [Harmony](https://www.nexusmods.com/stardewvalley/mods/20486) (bundled with SMAPI)
- [Generic Mod Config Menu](https://www.nexusmods.com/stardewvalley/mods/5098) (optional, for in-game settings)

### Steps

1. Install SMAPI if you haven't already
2. Download the latest release of `fishingWithStudy`
3. Extract the zip into your `StardewValley/Mods/` folder
4. Launch the game via SMAPI

```
StardewValley/
└── Mods/
    └── fishingWithStudy/
        ├── fishingWithStudy.dll
        ├── manifest.json
        └── assets/
            └── data/
                ├── questions.en.json
                ├── questions.zh.json
                └── custom/          ← Place custom banks here
```

---

## Usage

### Normal Fishing

Just fish as you normally would. When a fish bites, the quiz menu automatically replaces the fishing minigame. Answer correctly to catch the fish.

### Controls During Quiz

| Action | Control |
|--------|---------|
| Select/Deselect option | Left click |
| Submit answer | Click "Confirm" button |
| Cancel fishing | Escape |

### Study Mode

Press the configured hotkey (default: **K**) to open the quiz in study mode at any time. Study mode lets you practice questions without fishing — no stamina cost, no rewards, pure learning. See [Study Mode Introduction](#study-mode-introduction).

---

## Configuration

### Via GMCM (Recommended)

Open the in-game menu → **Mod Options** → **fishingWithStudy** to adjust settings.

### Via config.json

Edit `Mods/fishingWithStudy/config.json`:

```json
{
  "TimerEnabled": true,
  "TimerSeconds": 25,
  "SelectedBank": "default",
  "SelectedCategory": "",
  "StudyModeKeybind": "K"
}
```

| Setting | Type | Default | Description |
|---------|------|---------|-------------|
| `TimerEnabled` | bool | `true` | Enable per-question countdown timer |
| `TimerSeconds` | int | `25` | Time per question in seconds (5–60) |
| `SelectedBank` | string | `"default"` | Active question bank |
| `SelectedCategory` | string | `""` | Question category filter (empty = all) |
| `StudyModeKeybind` | string | `"K"` | Hotkey to open study mode |

---

## Custom Question Banks

Place your own `.json` question files in the `assets/data/custom/` folder. Each file becomes a selectable bank in the GMCM config menu.

File naming:
- Use any filename ending in `.json` (e.g., `math.json`, `history.json`)
- Filenames are displayed as bank names in the menu

---

## Question Bank Format

```json
[
  {
    "category": "math",
    "category_i18n": "Math",
    "questionList": [
      {
        "id": "q001",
        "type": "single",
        "question": "What is 2 + 2?",
        "options": [
          { "tag": "A", "text": "3" },
          { "tag": "B", "text": "4" },
          { "tag": "C", "text": "5" },
          { "tag": "D", "text": "6" }
        ],
        "answer": ["B"]
      },
      {
        "id": "q002",
        "type": "multiple",
        "question": "Which of these are prime numbers?",
        "options": [
          { "tag": "A", "text": "2" },
          { "tag": "B", "text": "4" },
          { "tag": "C", "text": "7" },
          { "tag": "D", "text": "9" }
        ],
        "answer": ["A", "C"]
      }
    ]
  }
]
```

### Field Reference

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `category` | string | Yes | Category identifier for filtering |
| `category_i18n` | string | No | Display name for the category |
| `questionList` | array | Yes | Array of question objects |
| `id` | string | Yes | Unique question identifier |
| `type` | string | Yes | `"single"` for single choice, `"multiple"` for multi choice |
| `question` | string | Yes | The question text |
| `options` | array | Yes | Array of answer options (2+ required) |
| `options[].tag` | string | Yes | Short identifier like "A", "B", "C" |
| `options[].text` | string | Yes | Full option text |
| `answer` | string[] | Yes | Array of correct option tags |

### Notes

- Questions with missing required fields are automatically skipped with a warning
- Duplicate question IDs are automatically skipped
- Question type must be exactly `"single"` or `"multiple"`
- The default bank auto-selects between `questions.zh.json` (Chinese) and `questions.en.json` (English) based on your game language

---

## Study Mode Introduction

Study Mode allows you to practice questions anytime without fishing. This mode was designed so players can use Stardew Valley as a study platform — pair it with custom question banks to prepare for driving tests, certification exams, or any other subject.

### How to Use

1. Press the configured hotkey (default: **K**) while in-game
2. The quiz menu opens in study mode
3. Answer questions — correct answers show green, wrong answers show red with the correct answer
4. Correct answers advance to the next question automatically
5. Wrong answers or timeouts show feedback then advance
6. Press **Escape** or **K** again to exit

### Differences from Fishing Mode

| | Fishing Mode | Study Mode |
|--|-------------|------------|
| Stamina cost | Yes (refunded on correct) | No |
| Fish caught | Yes | No |
| Treasure | Yes | No |
| XP gained | Yes | No |
| Time freeze | Yes | Yes |
| Stats tracked | Yes | Yes |

---

## Compatibility

- **Stardew Valley**: 1.6+
- **SMAPI**: 4.0+
- **Multiplayer**: Not tested
- **Other fishing mods**: Likely incompatible with mods that modify `BobberBar` or `FishingRod.DoFunction`. Compatible with mods that add new fish, new rods, or change loot tables.

---

## FAQ

### What if I have no questions loaded?

The mod will display a warning and fall back to vanilla fishing. Make sure `assets/data/questions.{lang}.json` exists and has valid questions.

### Can I create multiple custom banks?

Yes! Place multiple `.json` files in `assets/data/custom/`. Each file appears as a separate bank option in the GMCM config menu.

### How do I disable the timer?

Set `TimerEnabled` to `false` in GMCM or `config.json`. No time pressure — take as long as you want per question.

### Does the mod work with legendary fish?

Yes! Legendary fish require 5 consecutive correct answers. With treasure, the 3rd correct answer gets the treasure. Wrong answers or timeouts have no stamina penalty, but the fish escapes.

### Will I lose treasure I already earned if I get a wrong answer later?

No. If you've already earned the treasure through a correct answer but later get a wrong answer causing the fish to escape, the treasure is kept.

### Can I customize the study mode hotkey?

Yes, change `StudyModeKeybind` in GMCM or `config.json`. Supports SMAPI keybind format (e.g., `"LeftControl + K"`).

---

## Credits

- **Author**: fuukangun
- **Built with**: [SMAPI](https://smapi.io/), [Harmony](https://github.com/pardeike/Harmony)
