# fishingWithStudy

[![Stardew Valley](https://img.shields.io/badge/Stardew%20Valley-1.6%2B-brightgreen)](https://www.stardewvalley.net/)
[![SMAPI](https://img.shields.io/badge/SMAPI-4.0%2B-blue)](https://smapi.io/)
[![License](https://img.shields.io/badge/license-MIT-green)](LICENSE)

**fishingWithStudy** is a Stardew Valley mod that transforms the fishing minigame into a quiz-based learning system. Instead of controlling a bouncing green bar, you answer questions to catch fish. Perfect for studying while playing!

**fishingWithStudy** 是一个将星露谷物语钓鱼小游戏替换为答题系统的模组。你无需再控制那个弹跳的绿色条，只要答对题目就能钓上鱼。边玩游戏边学习！

---

## Table of Contents / 目录

- [Features / 功能特性](#features--功能特性)
- [How It Works / 工作原理](#how-it-works--工作原理)
- [Installation / 安装](#installation--安装)
- [Usage / 使用方法](#usage--使用方法)
- [Configuration / 配置](#configuration--配置)
- [Custom Question Banks / 自定义题库](#custom-question-banks--自定义题库)
- [Question Bank Format / 题库格式](#question-bank-format--题库格式)
- [Study Mode / 学习模式](#study-mode--学习模式)
- [Compatibility / 兼容性](#compatibility--兼容性)
- [FAQ / 常见问题](#faq--常见问题)

---

## Features / 功能特性

### Core Mechanics / 核心机制

| Feature | Description |
|---------|-------------|
| **Quiz replaces fishing** | When a fish bites, a quiz menu appears instead of the vanilla fishing minigame. Answer correctly to catch it. |
| **答题替代钓鱼** | 鱼上钩后，弹出的是答题界面而非原版钓鱼小游戏。答对即可钓上鱼。 |
| **Single & Multi choice** | Questions support both single-choice and multiple-choice formats. |
| **单选与多选** | 题目支持单选题和多选题两种格式。 |
| **Configurable timer** | Per-question countdown timer (5–60 seconds), can be disabled entirely. |
| **可配置倒计时** | 每题倒计时（5-60秒），可完全关闭。 |

### Difficulty Scaling / 难度分级

| Fish Type / 鱼类 | Required Correct Answers / 需要答对数 |
|------------------|--------------------------------------|
| Normal / 普通鱼 | **1** correct answer |
| Treasure / 有宝箱 | **2** correct answers (1 for fish + 1 for treasure) |
| Legendary / 传说鱼 | **5** correct answers |

### Smart Learning Algorithm / 智能学习算法

The mod tracks which questions you get wrong and periodically re-tests you on them. Every 5th question after the first round, a random previously-missed question is inserted to reinforce learning.

模组会追踪你答错的题目，并在后续答题中定期复习。第一轮结束后，每答5题会随机插入一道之前答错的题，帮助巩固记忆。

### Quality Bonus / 品质奖励

After answering **66+ questions** globally, your fish gain quality bonuses based on overall accuracy:

全局累计答对 **66 题以上**后，根据总正确率获得鱼的品质加成：

| Accuracy / 正确率 | Bonus Chance / 概率 | Quality / 品质 |
|-------------------|---------------------|----------------|
| \> 80% | 70% chance | Iridium / 铱星 |
| \> 70% | 70% chance | Gold / 金 |
| \> 60% | 70% chance | Silver / 银 |

### Stats Tracking / 数据统计

- **Per-scope stats**: Track accuracy per question bank and category combination
- **Global stats**: Track lifetime total answers and accuracy
- Data persists per save file via Stardew Valley's `modData` system

- **分类统计**: 按题库和分类组合追踪正确率
- **全局统计**: 追踪终生答题总数和正确率
- 数据通过星露谷的 `modData` 系统按存档持久化保存

### Additional Features / 其他特性

- **GMCM support**: Full integration with [Generic Mod Config Menu](https://www.nexusmods.com/stardewvalley/mods/5098) for in-game settings
- **Bilingual UI**: Automatically switches between Chinese and English based on your game language
- **Time freeze**: Game time is paused during quiz, so you never lose in-game time
- **Stamina refund**: Casting stamina is refunded on correct answer
- **Fishing XP**: Earn fishing experience based on the fish's difficulty
- **Custom banks**: Load your own question banks from JSON files

- **GMCM 支持**: 完整集成通用模组配置菜单，游戏内直接修改设置
- **双语界面**: 根据游戏语言自动切换中文/英文
- **时间冻结**: 答题期间游戏时间暂停，不浪费游戏内时间
- **体力返还**: 答对返还抛竿消耗的体力
- **钓鱼经验**: 根据鱼的难度获得钓鱼经验值
- **自定义题库**: 支持从 JSON 文件加载自定义题库

---

## How It Works / 工作原理

1. You cast your fishing rod and wait for a bite (same as vanilla)
2. When a fish bites, instead of the BobberBar minigame, a **quiz menu** opens
3. Read the question, select your answer(s), and click **Confirm**
4. Get enough correct answers before the timer runs out to catch the fish
5. The fish quality, treasure, and rewards all work the same as vanilla

**If you get a wrong answer or time out:**
- Normal fish: escapes, receive trash, small stamina penalty
- Legendary fish: escapes, no penalty (the fish is just too powerful)

**If you press Escape**: Fish escapes immediately, no rewards or penalties.

---

1. 抛竿等鱼上钩（和原版一样）
2. 鱼上钩后，不是弹出钓鱼小游戏，而是**答题界面**
3. 阅读题目，选择答案，点击**确认**
4. 在倒计时结束前答对足够数量的题目即可钓上鱼
5. 鱼的品质、宝箱和奖励机制与原版一致

**答错或超时：**
- 普通鱼：逃脱，获得垃圾，少量体力惩罚
- 传说鱼：逃脱，无惩罚（传说鱼太强了）

**按 Escape 键**：直接放弃，鱼逃走，无奖励也无惩罚。

---

## Installation / 安装

### Requirements / 前置要求

- [Stardew Valley 1.6+](https://www.stardewvalley.net/)
- [SMAPI 4.0+](https://smapi.io/)
- [Harmony](https://www.nexusmods.com/stardewvalley/mods/20486) (bundled with SMAPI)
- [Generic Mod Config Menu](https://www.nexusmods.com/stardewvalley/mods/5098) (optional, for in-game settings)

### Steps / 步骤

1. Install SMAPI if you haven't already / 如果还没安装 SMAPI，请先安装
2. Download the latest release of `fishingWithStudy` / 下载 `fishingWithStudy` 最新版本
3. Extract the zip into your `StardewValley/Mods/` folder / 解压到 `StardewValley/Mods/` 文件夹
4. Launch the game via SMAPI / 通过 SMAPI 启动游戏

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
                └── custom/          ← 放自定义题库
```

---

## Usage / 使用方法

### Normal Fishing / 正常钓鱼

Just fish as you normally would. When a fish bites, the quiz menu automatically replaces the fishing minigame. Answer the question(s) correctly to catch the fish.

像往常一样钓鱼即可。鱼上钩后答题界面会自动替换钓鱼小游戏。答对题目就能钓上鱼。

### Controls During Quiz / 答题时操作

| Action / 操作 | Control / 按键 |
|---------------|---------------|
| Select/Deselect option / 选择/取消选项 | Left click / 鼠标左键 |
| Submit answer / 提交答案 | Click "Confirm" button / 点击"确认"按钮 |
| Cancel fishing / 放弃钓鱼 | Escape |

### Study Mode / 学习模式

Press the configured hotkey (default: **K**) to open the quiz in study mode at any time. Study mode lets you practice questions without needing to fish. No stamina cost, no rewards — pure learning.

按下配置的快捷键（默认：**K**）可以随时打开学习模式。学习模式不需要钓鱼，无体力消耗，无奖励——纯粹学习。

---

## Configuration / 配置

### Via GMCM (Recommended) / 通过 GMCM（推荐）

Open the in-game menu → **Mod Options** → **fishingWithStudy** to adjust settings.

游戏内菜单 → **模组选项** → **fishingWithStudy** 调整设置。

### Via config.json / 直接编辑配置文件

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

| Setting / 设置 | Type / 类型 | Default / 默认 | Description / 说明 |
|---------------|------------|---------------|-------------------|
| `TimerEnabled` | bool | `true` | Enable per-question countdown timer / 是否启用每题倒计时 |
| `TimerSeconds` | int | `25` | Time per question in seconds (5–60) / 每题答题时间，秒（5-60） |
| `SelectedBank` | string | `"default"` | Active question bank / 当前使用的题库 |
| `SelectedCategory` | string | `""` | Question category filter (empty = all) / 题目分类筛选（空=全部） |
| `StudyModeKeybind` | string | `"K"` | Hotkey to open study mode / 打开学习模式的快捷键 |

---

## Custom Question Banks / 自定义题库

Place your own `.json` question files in the `assets/data/custom/` folder. Each file becomes a selectable bank in the GMCM config menu.

将自定义的 `.json` 题库文件放入 `assets/data/custom/` 文件夹。每个文件会自动出现在 GMCM 配置菜单的题库选项中。

File naming / 文件命名:
- Use any filename ending in `.json` (e.g., `math.json`, `history.json`)
- Filenames are displayed as bank names in the menu
- 任何以 `.json` 结尾的文件名都可以（如 `math.json`、`history.json`）
- 文件名将作为题库名称显示在菜单中选择

---

## Question Bank Format / 题库格式

```json
[
  {
    "category": "math",
    "category_i18n": "数学",
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

### Field Reference / 字段说明

| Field / 字段 | Type / 类型 | Required / 必填 | Description / 说明 |
|-------------|------------|----------------|-------------------|
| `category` | string | Yes / 是 | Category identifier for filtering / 分类标识符，用于筛选 |
| `category_i18n` | string | No / 否 | Display name for the category (supports Chinese) / 分类显示名称 |
| `questionList` | array | Yes / 是 | Array of question objects / 题目数组 |
| `id` | string | Yes / 是 | Unique question identifier / 题目唯一标识 |
| `type` | string | Yes / 是 | `"single"` for single choice, `"multiple"` for multi choice / 单选 `"single"`，多选 `"multiple"` |
| `question` | string | Yes / 是 | The question text (supports Chinese) / 题目文本 |
| `options` | array | Yes / 是 | Array of answer options (2+ required) / 选项数组（至少2个） |
| `options[].tag` | string | Yes / 是 | Short identifier like "A", "B", "C" / 选项标识，如"A"、"B"、"C" |
| `options[].text` | string | Yes / 是 | Full option text / 选项文本 |
| `answer` | string[] | Yes / 是 | Array of correct option tags / 正确答案的 tag 数组 |

### Notes / 注意事项

- Questions with missing required fields are automatically skipped with a warning
- Duplicate question IDs are automatically skipped
- Question type must be exactly `"single"` or `"multiple"`
- The default bank auto-selects between `questions.zh.json` (Chinese) and `questions.en.json` (English) based on your game language

- 缺少必填字段的题目会被自动跳过并发出警告
- 重复的题目 ID 会被自动跳过
- 题型必须是 `"single"` 或 `"multiple"`
- 默认题库根据游戏语言自动选择 `questions.zh.json` 或 `questions.en.json`

---

## Study Mode / 学习模式

Study Mode allows you to practice questions anytime without fishing.

学习模式让你随时可以练习题目，无需钓鱼。

### How to Use / 使用方式

1. Press the configured hotkey (default: **K**) while in-game
2. The quiz menu opens in study mode
3. Answer questions — correct answers show green, wrong answers show red with the correct answer
4. Each correct answer advances to the next question
5. Wrong answers or timeouts show feedback then advance
6. Press **Escape** or **K** again to exit

1. 游戏中按下配置的快捷键（默认：**K**）
2. 学习模式答题界面打开
3. 答题——正确显示绿色，错误显示红色并展示正确答案
4. 答对自动进入下一题
5. 答错或超时显示反馈后自动进入下一题
6. 按 **Escape** 或再次按 **K** 退出

### Differences from Fishing Mode / 与钓鱼模式的区别

| | Fishing / 钓鱼 | Study / 学习 |
|--|---------------|-------------|
| Stamina cost / 体力消耗 | Yes (refunded on correct) / 是（答对返还） | No / 无 |
| Fish caught / 钓到鱼 | Yes / 是 | No / 否 |
| Treasure / 宝箱 | Yes / 是 | No / 否 |
| XP gained / 获得经验 | Yes / 是 | No / 否 |
| Time freeze / 时间冻结 | Yes / 是 | Yes / 是 |
| Stats tracked / 统计记录 | Yes / 是 | Yes / 是 |

---

## Compatibility / 兼容性

- **Stardew Valley**: 1.6+
- **SMAPI**: 4.0+
- **Multiplayer**: Not tested / 未测试
- **Other fishing mods**: Likely incompatible with mods that modify `BobberBar` or `FishingRod.DoFunction`. Compatible with mods that add new fish, new rods, or change loot tables.
- **其他钓鱼模组**: 与修改 `BobberBar` 或 `FishingRod.DoFunction` 的模组可能不兼容。与添加新鱼、新钓竿或修改掉落表的模组兼容。

---

## FAQ / 常见问题

### Q: What if I have no questions loaded?
### 没有加载题库怎么办？

The mod will display a warning and fall back to vanilla fishing. Fish normally without the quiz. Make sure `assets/data/questions.{lang}.json` exists and has valid questions.

模组会显示警告并回退到原版钓鱼。确保 `assets/data/questions.{lang}.json` 文件存在且包含有效题目。

### Q: Can I create multiple custom banks?
### 可以创建多个自定义题库吗？

Yes! Place multiple `.json` files in `assets/data/custom/`. Each file appears as a separate bank option in the GMCM config menu.

可以！在 `assets/data/custom/` 中放入多个 `.json` 文件，每个文件都会作为独立题库出现在 GMCM 配置菜单中。

### Q: How do I disable the timer?
### 如何关闭倒计时？

Set `TimerEnabled` to `false` in GMCM or `config.json`. No time pressure — take as long as you want per question.

在 GMCM 或 `config.json` 中将 `TimerEnabled` 设为 `false`。没有时间压力，每道题想答多久答多久。

### Q: Does the mod work with legendary fish?
### 模组对传说鱼有效吗？

Yes! Legendary fish require 5 consecutive correct answers and have no stamina penalty on failure (the fish simply escapes).

有效！传说鱼需要连续答对 5 题，答错无体力惩罚（鱼直接逃走）。

### Q: Can I customize the study mode hotkey?
### 可以自定义学习模式快捷键吗？

Yes, change `StudyModeKeybind` in GMCM or `config.json`. Supports SMAPI keybind format (e.g., `"LeftControl + K"`).

可以，在 GMCM 或 `config.json` 中修改 `StudyModeKeybind`。支持 SMAPI 的按键绑定格式（如 `"LeftControl + K"`）。

---

## Credits / 致谢

- **Author / 作者**: fuukangun
- **Built with / 基于**: [SMAPI](https://smapi.io/), [Harmony](https://github.com/pardeike/Harmony)