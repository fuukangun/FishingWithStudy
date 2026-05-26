# 钓鱼答题 Mod — 设计文档

## 概述

将星露谷物语的钓鱼小游戏替换为问答小游戏。玩家每次鱼上钩时回答 1 道题目，答对获得鱼获（按原版机制）+ 钓鱼经验 + 返还甩杆消耗的体力，答错获得垃圾并随机扣除 1-3 体力。答题期间游戏时间暂停（单人+多人）。内置学习模式支持非钓鱼状态下刷题，答题数据独立不计入钓鱼统计。内置 300-500 道分类题库，支持玩家加载自定义 JSON 题库。具备全局隐藏品质奖励机制，依据玩家全局历史正确率触发。

## 技术栈

- **平台**: SMAPI 4.0+ / .NET 6.0
- **语言**: C#
- **补丁**: Harmony 2.x (Prefix Patch)
- **数据格式**: JSON
- **操作系统**: macOS / Windows

## 1. 架构设计

### 1.1 目录结构

```
fishingWithStudy/
├── ModEntry.cs                    # SMAPI 入口，注册 Harmony
├── Patchers/
│   └── FishingPatcher.cs          # Harmony Patch: BobberBar 构造 + Draw
├── UI/
│   ├── QuizMenu.cs                # 答题主菜单 (IClickableMenu)
│   └── QuizResultMessage.cs       # 答题结果浮层反馈
├── Data/
│   ├── Question.cs                # 题目数据模型
│   ├── QuestionManager.cs         # 题库加载/验证/乱序/抽取
│   └── StatsManager.cs            # 答题统计持久化（按 scope 隔离）
├── Logic/
│   ├── FishRewarder.cs            # 鱼获奖励 + 隐藏品质计算
│   └── Translation.cs             # i18n (zh/en)
├── Config/
│   └── ModConfig.cs               # mod 配置
├── assets/
│   └── data/
│       ├── questions.zh.json      # 中文题库
│       ├── questions.en.json      # 英文题库
│       └── custom/                # 玩家自定义题库目录
├── manifest.json
└── fishingWithStudy.csproj
```

### 1.2 核心流程

```
FishingRod.doFunction() 计算鱼种
         │
         ▼
BobberBar 构造函数调用  ←── [Patch #1] FishingPatcher.Prefix
         │                     捕获鱼数据 + 当前体力 & 最大体力值
         │                     并存到 FishRewarder
         │                     让 BobberBar 正常构造（保持状态机完整）
         ▼
Game1.activeClickableMenu = BobberBar
         │
         ▼
QuizMenu 立即覆盖为 activeClickableMenu
BobberBar.draw()  ←── [Patch #2] FishingPatcher.Draw (跳过绘制)
         │
         ▼
  ⏸ 冻结游戏时间  ←── Game1.paused = true (SP)
           │           网络同步暂停 (MP)
    ┌──────┴──────┐
    ▼             ▼
  答对           答错/超时
    │             │
    ▼             ▼
 给鱼+经验       给垃圾
 +品质加成       -随机1-3体力
    │             │
    ▼             ▼
 bobber.fishCaught = true    bobber.fishCaught = false
 bobber.doneWithMinigame = true
         │
         ▼
  ⏸ 解冻游戏时间  ←── Game1.paused = false (SP)
           │           网络同步恢复 (MP)
         ▼
FishingRod 自然触发收杆 → 原版奖励发放逻辑
```

## 2. Harmony 补丁设计

### 2.1 Patch #1: BobberBar 构造函数 Prefix

- **目标**: 拦截 `BobberBar` 构造函数，捕获鱼数据 + 体力信息
- **方法**: Harmony Prefix, 不阻止构造，仅捕获数据
- **注意**: 必须保留构造过程以保证 `FishingRod.bobber` 引用不为空

实际上，Patch 方案为三个补丁协同：
- **Patch #1 (Prefix on BobberBar 构造)**: 捕获构造参数中的鱼数据（fish ID、size、quality 等）以及当前体力值（`Game1.player.Stamina`）和最大体力值（`Game1.player.MaxStamina`），不阻止构造。同时将 `__instance`（BobberBar 实例引用）存入 `FishRewarder.CurrentBobber`，供后续 QuizMenu 访问 `bobber.fishCaught` 等字段
- **Patch #2 (Prefix on BobberBar.draw)**: 跳过 `BobberBar.draw()`，使其不可见
- **Patch #3 (Prefix/Postfix on FishingRod 甩杆扣体力方法)**: 捕获甩杆实际消耗的体力值

### 2.2 Patch #2: BobberBar.draw Prefix

- **目标**: 阻止 BobberBar 的绘制
- **方法**: Harmony Prefix, `return false` 跳过绘制

### 2.3 Patch #3: FishingRod 甩杆扣体力方法

- **目标**: 捕获甩杆实际消耗的体力值
- **方法**: Harmony Prefix/Postfix，记录扣减前的体力值，然后在扣减后计算差值
- **原因**: 玩家可能在甩杆后食用食物恢复体力，仅记录 BobberBar 时的体力值无法还原真实的消耗额。Patch #3 确保精确记录甩杆动作实际消耗的 stamina，答对后返还该值
- **实现方式**: 需要根据反编译结果定位具体的扣体力方法（可能在 `FishingRod.beginUsing`、`FishingRod.doFunction` 或 `Farmer.Stamina` setter 附近）

### 2.4 Patch 捕获数据汇总

```csharp
// Patch #1 (Prefix) 中捕获 BobberBar 实例引用:
// FishRewarder.CurrentBobber = __instance;
//
// 后续通过静态字段 FishRewarder.CurrentBobber 操作:
//   CurrentBobber.fishCaught = true;
//   CurrentBobber.treasureCaught = true;
//   CurrentBobber.doneWithMinigame = true;
//   CurrentBobber.fishQuality = (byte)quality;

// Patch #1 捕获的参数
BobberBar(int whichFish, float fishSize, bool treasure, 
          int fishDifficulty, bool finished, byte fishQuality, 
          float fishSizeModifier, float bristle, float lineSize, 
          int fishCategory, bool fromFishPond)

// Patch #1 捕获的体力信息
float staminaAtBite = Game1.player.Stamina;  // 鱼上钩时的体力

// Patch #3 捕获的消耗
float actualCastCost = fishingRod.lastCastStaminaCost; // 甩杆实际消耗
```

## 3. UI 设计 (QuizMenu)

### 3.1 布局

QuizMenu 继承 `IClickableMenu`，使用相对窗口百分比进行自适应布局：

| 元素 | 位置与尺寸 |
|------|-----------|
| 外框 | 居中，宽 = 60% viewport.w，高 = 55% viewport.h |
| 标题 | 上方，像素字体，i18n |
| 进度 + 倒计时 | 同一行：左对齐答题进度（传说鱼时显示 1/5），右对齐倒计时 |
| 题目区 | 中央 60% 区域 |
| 选项区 | 题目下方，间距自适应 |
| 提交按钮 | 底部居中 |
| 分类标签 | 底部左下，小字 |

**UI 示例（单选题 - 传说鱼）**:

```
┌──────────────────────────────────┐
│  【Fishing Quiz / 钓鱼答题】     │
│  ┌──────────────────────────────┐│
│  │  2/5               ⏱ 25     ││  ← 左: 进度 右: 倒计时
│  │                              ││
│  │  What is Pam's favorite      ││
│  │  item? / 潘姆最喜欢的物品？  ││
│  │                              ││
│  │  ○ A. Fried Egg / 煎鸡蛋     ││
│  │  ○ B. Diamond / 钻石         ││
│  │  ○ C. Poppy / 虞美人         ││
│  │  ○ D. Beer / 啤酒            ││
│  │                              ││
│  │       [Confirm / 确认]       ││
│  └──────────────────────────────┘│
│  Characters / 角色知识           │
└──────────────────────────────────┘
```

**普通鱼时**: 进度区域不显示（无需答题进度），同一行仅显示倒计时。

**UI 示例（多选题）**:

```
│  ☑ A. Farming / 耕种  □ B. Mining / 采矿  │
│  □ C. Smithing / 锻造  ☑ D. Combat / 战斗 │
│     [Submit (2) / 交卷 (2)]                │  ← 显示已选数量
│  (可多选) / (Multiple)                      │
```

### 3.2 交互

- **单选题**: ○ 圆点选择 → 点击"确认"提交
- **多选题**: □ 复选框选择 → 按钮上显示已选数量 → 选 ≥1 项后可提交
- **倒计时**: 默认 25 秒。到达 0 时自动提交（视为答错）
- **反馈**: 提交后显示 1.5 秒反馈浮层，然后自动关闭菜单

### 3.3 反馈界面

普通题（只需答 1 题）:

答对:
```
┌─────────────────────────┐
│  ✓ Correct! / ✓ 正确！  │
│  Caught: [鱼名]         │  ← 如有品质提升则附加提示
└─────────────────────────┘
```

答错:
```
┌──────────────────────────────────┐
│  ✗ Wrong! / ✗ 错误！            │
│  Answer: [正确选项内容]          │
└──────────────────────────────────┘
```

多题连续答题（传说鱼 / 宝箱）:

答对但未完成时（过渡动画）:
```
┌──────────────────────────────┐
│  ✓ Correct! / ✓ 正确！       │
│  3/5  Continue... / 继续...  │
│                              │
│  ┌──────────────────────────┐    │
│  │  Next in 3s / 下一题 3秒  │    │  ← real-time 计时，非游戏帧率依赖
│  └──────────────────────────┘    │
└──────────────────────────────┘
```

最后 1 题答对时:
```
┌──────────────────────────────┐
│  ✓ Correct! / ✓ 正确！       │
│  5/5  Complete! / 完成！     │
│  Caught: [传说鱼名]          │
└──────────────────────────────┘
```

多题中途答错:
```
┌──────────────────────────────────┐
│  ✗ Wrong! / ✗ 错误！            │
│  答对 2 题后失败，鱼跑了！      │  ← 显示已连续答对题数
│  Answer: [正确选项内容]          │
└──────────────────────────────────┘
```

### 3.4 学习模式

独立于钓鱼的刷题功能，玩家可在非钓鱼状态下主动进入。

**触发方式**:
- 配置一个快捷键（默认为 `K` 键）
- 任意时刻按下快捷键：
  - 钓鱼中（`Game1.activeClickableMenu` 为 BobberBar 或 QuizMenu）→ **不响应**，保持钓鱼状态
  - 学习模式已打开（当前 QuizMenu 且 isStudyMode=true）→ **关闭学习模式**
  - 其他情况 → 打开 QuizMenu（学习模式）

**学习模式与钓鱼答题的区别**:

| 维度 | 钓鱼答题 | 学习模式 |
|------|---------|---------|
| 触发 | 鱼上钩自动触发 | 快捷键手动触发 |
| 时间 | 答题期间冻结 | 始终冻结 |
| 体力消耗 | 答对返还，答错扣 1-3 | 无 |
| 鱼获奖励 | 有 | 无 |
| 统计影响 | 计入 scope + global | 不计入任何统计 |
| 品质加成 | 可触发 | 不涉及 |

**实现**:
- QuizMenu 构造函数增加 `isStudyMode` 参数
- 学习模式下 `OnClose()` 不调用 FishRewarder 的任何方法
- 学习模式下不更新 StatsManager
- 时间始终通过 `Game1.paused = true` 冻结，关闭后恢复
- **学习模式关闭时**检查玩家体力：`Game1.player.Stamina <= 0` 时自动补充到 1，防止体力为 0 的玩家在恢复时间后立刻晕倒

## 4. 题库数据模型

### 4.1 JSON 格式

```json
[
  {
    "category": "characters",
    "category_i18n": {
      "zh": "角色知识",
      "en": "Characters"
    },
    "questionList": [
      {
        "id": "sv_001",
        "type": "single",
        "question": {
          "zh": "潘姆最喜欢的物品是什么？",
          "en": "What is Pam's favorite item?"
        },
        "options": [
          { "tag": "A", "text": { "zh": "煎鸡蛋", "en": "Fried Egg" } },
          { "tag": "B", "text": { "zh": "钻石", "en": "Diamond" } },
          { "tag": "C", "text": { "zh": "虞美人", "en": "Poppy" } },
          { "tag": "D", "text": { "zh": "啤酒", "en": "Beer" } }
        ],
        "answer": ["D"]
      },
      {
        "id": "sv_002",
        "type": "multiple",
        "question": {
          "zh": "以下哪些属于星露谷物语中的技能？",
          "en": "Which of the following are skills in Stardew Valley?"
        },
        "options": [
          { "tag": "A", "text": { "zh": "耕种", "en": "Farming" } },
          { "tag": "B", "text": { "zh": "采矿", "en": "Mining" } },
          { "tag": "C", "text": { "zh": "锻造", "en": "Blacksmithing" } },
          { "tag": "D", "text": { "zh": "战斗", "en": "Combat" } }
        ],
        "answer": ["A", "B", "D"]
      }
    ]
  }
]
```

### 4.2 数据模型 (C#)

```csharp
class QuestionCategory {
    string Category;             // 内部键
    Dictionary<string, string> CategoryI18n; // zh → 角色知识, en → Characters
    List<Question> QuestionList;
}

class Question {
    string Id;
    string Type;                 // "single" | "multiple"
    Dictionary<string, string> Question; // zh/en 文本
    List<Option> Options;
    List<string> Answer;         // 正确答案 tag 列表
}

class Option {
    string Tag;                  // A/B/C/D
    Dictionary<string, string> Text; // zh/en 文本
}
```

### 4.3 题库加载与验证流程

```
启动时:

QuestionManager.Initialize()
    │
    ├─ 1. 检测游戏语言
    │      └─ Game1.content.CurrentLanguage → "zh" / other
    │
    ├─ 2. 加载默认题库
    │      └─ assets/data/questions.{lang}.json
    │      └─ 验证每道题的数据完整性
    │      └─ 记录为默认题库（bankId = "default"）
    │
    ├─ 3. 扫描 assets/data/custom/*.json
    │      └─ 仅记录文件名（如 my_quiz.json, community.json）
    │      └─ 不解析内容，仅用于填充 GMCM 下拉列表
    │
    ├─ 4. 根据 config.SelectedBank 加载当前题库:
    │      ├─ "default" → 使用步骤 2 已加载的默认题库
    │      └─ "xxx.json" → 加载 custom/xxx.json 并验证
    │
    ├─ 5. 有效题目 == 0:
    │      └─ 不注册 Harmony Patch，回退原版钓鱼
    │
    └─ [完成]

题库切换时（GMCM 修改 SelectedBank）:

OnBankChanged(newBank)
    │
    ├─ 1. 检查当前是否正在钓鱼:
    │      └─ Game1.activeClickableMenu 是 QuizMenu / BobberBar
    │      └─ 是 → 拒绝切换，保持原题库，日志 Info + 下次钓鱼前生效
    │
    ├─ 2. newBank == "default":
    │      └─ 使用启动时已加载的默认题库
    │
    ├─ 3. newBank 为自定义文件:
    │      ├─ 加载并验证 assets/data/custom/{newBank}
    │      ├─ 有效题目 > 0 → 替换当前为新的题库
    │      └─ 有效题目 == 0 OR 文件不存在 → 回退 "default" + 日志 Warning
    │
    ├─ 4. 清空当前 scope 缓存（下次钓鱼重新生成）
    │
    └─ [完成]

钓鱼时做题:

QuizMenu.Open()
    │
    ├─ 1. 读取当前配置:
    │      └─ SelectedBank: "default" / "my_quiz.json"
    │      └─ SelectedCategory: "characters" / "" (空=全部)
    │
    ├─ 2. 确定当前作用域 key:
    │      └─ scopeKey = SelectedBank + ":" + (SelectedCategory or "")
    │      └─ 示例: "default" / "default:characters" / "my_quiz:fishing"
    │
    ├─ 3. 从持久化数据加载该 scope 的出题顺序 S
    │      └─ 不存在 → 根据当前题库+分类筛选题目 → 打乱生成
    │
    ├─ 4. 取下一题 → 显示 QuizMenu
    │
    └─ [完成]
```

### 4.4 随机出题逻辑

```
作用域: 每个 quiz scope (bank__category) 有独立的出题状态

初始化（某 scope 首次使用时）:
  根据当前 bank + 分类筛选题目
  S = 打乱后的有效题目列表 (Shuffle)
  i = 0          // 当前出题指针
  W = ∅          // 错题集 (HashSet<string> id)
  firstRoundDone = false

每次钓鱼 → 取一题:
  if not firstRoundDone:
    if i < |S|:
      q = S[i]; i++
    else:
      firstRoundDone = true
      // 进入复循环模式，继续往下走

  if firstRoundDone:
    offset = i - |S|       // 第一轮结束后的偏移
    if offset % 5 == 4 AND W ≠ ∅:
      // 每 5 题中的第 5 题（0-indexed: 4）
      q = W.随机取一题
    else:
      q = S[i % |S|]; i++

答题后:
  答对 AND q.id ∈ W → W.Remove(q.id)
  答错 AND q.id ∉ W → W.Add(q.id)

数据持久化（按 scopeKey 独立存储）:
  QuestionOrder_{scopeKey}: S 的 id 数组
  WrongSet_{scopeKey}: W 的 id 数组
  CurrentIndex_{scopeKey}: i
  FirstRoundDone_{scopeKey}: bool
  TotalAnswers_{scopeKey}: 总答题数
  CorrectAnswers_{scopeKey}: 正确数
```

## 5. 鱼获奖励与隐藏品质

### 5.1 隐藏品质判定（全局统计）

品质判定使用**全局**答题统计而非 scope 隔离的统计，确保玩家切换题库后品质感知一致。

```csharp
// FishRewarder.ApplyCorrect()
// 在 BobberBar 构造时已经捕获了以下数据:
//   whichFish, fishSize, fishQuality(original),
//   fishDifficulty, fishCategory, ...
// 以及体力信息:
//   staminaAtBite, maxStamina
//   actualCastCost (来自 Patch #3)

// 1. 读取当前 scope 的出题状态（题序、指针、错题集）
var scopeKey = GetCurrentScopeKey(); // config.SelectedBank + ":" + (category or "")
var scopeStats = StatsManager.LoadScopeStats(scopeKey);

// 2. 读取全局答题统计（用于品质判定）
var globalStats = StatsManager.LoadGlobalStats();
// globalStats = { TotalAnswers, CorrectAnswers }  = 所有 scope 汇总

// 3. 判断隐藏品质（基于全局统计，门槛 66 题起）
if (globalStats.TotalAnswers > 66) {
    double accuracy = (double)globalStats.CorrectAnswers / globalStats.TotalAnswers;
    
    int quality = -1; // -1 表示不覆盖
    double roll = Random.NextDouble();
    
    if (accuracy > 0.80) {      // > 80%
        if (roll < 0.70) quality = 3; // Iridium (70%概率)
    } else if (accuracy > 0.70) { // > 70%
        if (roll < 0.70) quality = 2; // Gold
    } else if (accuracy > 0.60) { // > 60%
        if (roll < 0.70) quality = 1; // Silver
    }
    
    if (quality >= 0) {
        bobber.fishQuality = (byte)quality;
    }
    // quality == -1 → 不走覆盖，FishingRod 后续使用原版品质
    //                   原版品质依据地点、时间、钓具、等级等计算
}

// 4. 返还甩杆消耗的体力（精确值，不超出最大体力上限）
//    传说鱼等需要多题答对的场景: 仅在全部答完后统一返还一次体力
//    中间答对的题不单独返还，避免多题场景下累积返还过多
float refund = Math.Min(actualCastCost, Game1.player.MaxStamina - Game1.player.Stamina);
Game1.player.Stamina += refund;

// 5. 标记钓鱼结果
bobber.fishCaught = true;
bobber.doneWithMinigame = true;
```

### 5.2 答错/超时流程

```csharp
// FishRewarder.ApplyWrong() - 答错（非超时）
bobber.fishCaught = false;
bobber.doneWithMinigame = true;

// 随机扣除 1-3 体力（答错额外惩罚），体力最少为 0
int staminaPenalty = Random.Next(1, 4);
Game1.player.Stamina = Math.Max(0, Game1.player.Stamina - staminaPenalty);

// FishingRod 自动发放垃圾
```

```csharp
// FishRewarder.ApplyTimeout() - 超时（不扣体力）
bobber.fishCaught = false;
bobber.doneWithMinigame = true;
// 不扣除体力
// FishingRod 自动发放垃圾
```

### 5.3 传说鱼需要多次答题

**触发条件**: 鱼上钩时，通过 `whichFish` 判断是否是传说鱼。

**机制**:

```
普通鱼: 答对 1 题 → 钓起
传说鱼: 需要连续答对 5 题 → 钓起
        答错/超时任意 1 题 → 鱼跑掉（无奖励，无垃圾，不扣体力——与 5.2 超时处理一致）
```

**实现**:

| 参数 | 说明 |
|------|------|
| `requiredCorrect` | 普通鱼 = 1，传说鱼 = 5 |
| `currentCorrect` | QuizMenu 内部计数器 |

```
QuizMenu.Open() 时:
  ├─ 判断 whichFish 是否属于传说鱼
  ├─ requiredCorrect = 传说鱼 ? 5 : 1
  ├─ currentCorrect = 0
  └─ 普通鱼时 UI 不显示进度

答对时:
  ├─ currentCorrect++
  ├─ UI 更新进度: "currentCorrect/requiredCorrect"
  └─ currentCorrect >= requiredCorrect → 触发 ApplyCorrect()

答错/超时时:
  ├─ 传说鱼 → 直接结束（fishCaught = false, 不扣除体力惩罚）
  └─ 普通鱼 → 触发 ApplyWrong()
```

**传说鱼判定**: 硬编码传说鱼 ID 列表，与游戏版本保持同步。

```csharp
bool IsLegendaryFish(int whichFish) {
    int[] legendaryIds = { 159, 160, 163, 682, 775 }; // SDV 1.6 传说鱼 ID
    return legendaryIds.Contains(whichFish);
}
```

### 5.4 宝箱与多题联动

**触发条件**: BobberBar 构造时 `treasure` 参数为 `true`（有宝箱）。

宝箱始终在鱼的"下一个"阶段获得。不同场景的题数和宝箱触发点由 `requiredCorrect` 和 `treasureThreshold` 两个参数决定：

| 场景 | requiredCorrect | treasureThreshold | 说明 |
|------|:---:|:---:|------|
| 普通鱼 + 无宝箱 | 1 | - | 答 1 题 |
| 普通鱼 + 宝箱 | 2 | 1 | Q1→鱼, Q2→宝箱 |
| 传说鱼 + 无宝箱 | 5 | - | 答 5 题获得传说鱼 |
| 传说鱼 + 宝箱 | 5 | 3 | Q3→宝箱, Q5→传说鱼 |

**状态标志**: 使用 `fishRewarded` / `treasureRewarded` 两个独立标志替代单一的 `fishCaught`，避免宝箱到手但鱼跑掉时 FishingRod 发不存在的鱼。

```
----------------
QuizMenu.Open():
  ├─ requiredCorrect = 传说鱼 ? 5 : (宝箱 ? 2 : 1)
  ├─ treasureThreshold = (宝箱 && 传说鱼) ? 3 : (宝箱 ? 1 : -1)
  ├─ currentCorrect = 0
  └─ fishRewarded = false; treasureRewarded = false

每次答对:
  ├─ currentCorrect++
  ├─ if currentCorrect == treasureThreshold:
  │      treasureRewarded = true
  │      bobber.treasureCaught = true
  ├─ if currentCorrect >= requiredCorrect:
  │      fishRewarded = true
  │      bobber.fishCaught = true
  │      钓鱼结束
  └─ else → 下一题

答错:
  ├─ if treasureRewarded:
  │      bobber.fishCaught = false  // 鱼没拿到，宝箱已发
  ├─ else:
  │      bobber.fishCaught = false  // 无鱼无宝箱
  └─ 钓鱼结束
```

**宝箱内容显示**: 宝箱阈值达成时，在正确反馈浮层结束后额外展示宝箱内容浮层 1 秒，然后进入下一题过渡（Next in 3s 倒计时）。时序为: 答对反馈(1.5s) → 宝箱浮层(1.0s，含物品清单) → 下一题倒计时(3s)。

```
┌──────────────────────────────────┐
│  Treasure Acquired! / 获得宝箱！ │
│  ├ 5 Iridium Ore / 5 个铱矿      │
│  ├ 1 Diamond / 1 个钻石          │
│  └ 100g / 100 金币               │
└──────────────────────────────────┘
```

**UI 进度指示**: 传说鱼+宝箱显示 `1/5`→`3/5`→`5/5`；普通鱼+宝箱显示 `1/2`→`2/2`。

#### 宝箱实现风险（POC 必须验证）

核心假设: `bobber.treasureCaught = true` 能让 FishingRod 自动发放宝箱。

**关键验证点**: 答错时 `fishCaught = false` + `treasureCaught = true` 的组合状态。需要确认原版 FishingRod 是否在 `fishCaught = false` 时仍会检查并发放 `treasureCaught` 的宝箱内容。如果原版代码以 `fishCaught` 为发放宝箱的前置条件，则需要改用方案 A/B 手动触发宝箱发放。

**原版 BobberBar 的工作方式**:
1. 构造时接收 `treasure = true` 参数
2. 玩家完成钓鱼后 BobberBar **内部生成**宝箱物品列表
3. 宝箱内容解析完成后传给 FishingRod

如果 `BobberBar.update()` 从未运行，宝箱生成逻辑可能未执行。

**替代方案**:

| 方案 | 做法 |
|------|------|
| **方案 A**（优先） | BobberBar 运行 `update()` 生成宝箱内容，跳过 `draw()` |
| **方案 B**（备选） | 反编译后手动调用宝箱生成方法 |

### 5.5 答题统计持久化

使用 SMAPI 的 `ModDataDictionary`（每个存档独立）。统计按作用域 scopeKey 隔离存储。

**scopeKey 格式**: `{bankId}`（全部题目）或 `{bankId}:{category}`（指定分类）
**ModData key 格式**: 避免 `/` 字符，使用 `fishingWithStudy:{scopeKey}.{field}` 模式

示例：

```
作用域 default（默认题库全部题目）:
  fishingWithStudy:default.TotalAnswers = "45"
  fishingWithStudy:default.CorrectAnswers = "38"
  fishingWithStudy:default.WrongSet = "sv_015,gk_042"
  fishingWithStudy:default.QuestionOrder = "sv_001,sv_005,gk_003,..."
  fishingWithStudy:default.CurrentIndex = "47"
  fishingWithStudy:default.FirstRoundDone = "false"

作用域 my_quiz:characters（my_quiz.json 的角色知识分类）:
  fishingWithStudy:my_quiz:characters.TotalAnswers = "12"
  fishingWithStudy:my_quiz:characters.CorrectAnswers = "10"
  fishingWithStudy:my_quiz:characters.WrongSet = "mq_003"
  fishingWithStudy:my_quiz:characters.QuestionOrder = "mq_001,mq_005,..."
  fishingWithStudy:my_quiz:characters.CurrentIndex = "5"
  fishingWithStudy:my_quiz:characters.FirstRoundDone = "false"
```

#### 全局统计（品质判定）

所有 scope 的答题数汇总，用于隐藏品质判定的门槛和正确率计算。

```
fishingWithStudy:global.TotalAnswers = "57"
fishingWithStudy:global.CorrectAnswers = "43"
```

**更新时机**: 每次钓鱼答题完成后同时更新当前 scope 统计和全局统计。
**聚合逻辑**: `global.TotalAnswers = Σ(scope.TotalAnswers)`，`global.CorrectAnswers = Σ(scope.CorrectAnswers)`

**切换 scope 的行为**
- 切换题库/分类只是切换 scopeKey，已有数据不受影响
- 首次进入某 scope 时字段为默认值
- 全局统计始终累计，不受 scope 切换影响
- 玩家可以在不同 scope 之间自由切换，每个 scope 的出题进度独立保留

## 6. 国际化 (i18n)

### 6.1 翻译策略

- 使用 `Translation.cs` 类，基于 `Game1.content.CurrentLanguage`
- `"zh"` → 中文，其他 → 英文
- 所有 UI 文本集中管理，代码中无硬编码

### 6.2 翻译键值表

| 键 | 中文 | English |
|----|------|---------|
| `ui.title` | 钓鱼答题 | Fishing Quiz |
| `ui.confirm` | 确认 | Confirm |
| `ui.submit` | 交卷({0}) | Submit({0}) |
| `ui.correct` | 正确！ | Correct! |
| `ui.wrong` | 错误！ | Wrong! |
| `ui.answer_is` | 正确答案：{0} | Answer: {0} |
| `ui.caught` | 获得：{0} | Caught: {0} |
| `ui.quality_up` | 品质提升！ | Quality Up! |
| `ui.timeout` | 时间到！ | Time's Up! |
| `ui.fish_got_away` | 鱼跑掉了！ | The fish got away! |
| `ui.multiple_hint` | (可多选) | (Multiple) |
| `ui.category` | {0} | {0} |
| `ui.study_mode_title` | 学习模式 | Study Mode |
| `ui.stamina_penalty` | 体力 -{0} | Stamina -{0} |

### 6.3 题库翻译

中文环境 → `questions.zh.json`
其他语言 → `questions.en.json`

## 7. 配置 (ModConfig)

```csharp
class ModConfig {
    bool TimerEnabled = true;          // 是否开启倒计时
    int TimerSeconds = 25;             // 倒计时秒数
    string SelectedBank = "default";   // 当前选中的题库: "default" / "my_quiz.json"
    string SelectedCategory = "";      // 当前选中的分类: ""(全部) / "characters" / "skills" / ...
    string StudyModeKeybind = "K";     // 学习模式快捷键
}
```

- 通过 `config.json` 管理
- 可选集成 `GenericModConfigMenu` 提供游戏内 UI：
  - **TimerEnabled**: 复选框（开启/关闭倒计时）
  - **TimerSeconds**: 滑动条（5-60秒），TimerEnabled 为 true 时显示
  - **SelectedBank**: 下拉选择框，列出可用题库（包括 "default"）
  - **SelectedCategory**: 下拉选择框，根据 SelectedBank 读取该题库的分类列表，附加"全部"选项
  - **StudyModeKeybind**: 按键绑定（用于打开学习模式）

**切换题库或分类时的行为**:
- 修改 `SelectedBank` 或 `SelectedCategory` 后，下次钓鱼时生效
- 统计数据和出题进度按 scopeKey 独立存储，切换不影响其他 scope 的数据
- 如果当前选中的题库文件已被删除或失效 → 回退到 "default"

## 8. 错误处理与回退

| 场景 | 行为 |
|------|------|
| 默认题库 JSON 解析失败 | 日志 Error，默认题库不可用 |
| 自定义题库 JSON 解析失败 | 日志 Warning，跳过该文件 |
| 单题数据不完整 | 日志 Warning（含 ID），跳过该题 |
| ID 冲突（同文件） | 后出现的跳过 + 日志 Warning |
| 自定义目录不存在 | 日志 Info，静默跳过 |
| 自定义文件全部加载失败（有效题目为 0） | 日志 Warning，该文件不会出现在可用题库列表中 |
| 所有题库均无有效题目 | Harmony Patch 不注册，完全回退原版钓鱼 |
| 配置的 SelectedBank 文件已被删除或无效 | 自动回退至 "default"，日志 Warning |
| 配置的 SelectedCategory 在当前题库中不存在 | 自动回退至 ""（全部），日志 Warning |
| 错题集为空但需要抽错题 | 跳过此轮抽错题，正常按顺序出题 |

## 9. 开发注意事项

### 9.1 macOS 兼容性

- .NET 6.0 跨平台支持，csproj 无需平台特殊配置
- 文件路径使用 `Path.Combine` 而非硬编码 `\`
- SMAPI on macOS 使用 Mono 或 .NET 运行，C# 代码本身兼容

### 9.2 POC 原型验证（高优先级）

在开发完整功能前，必须先实现一个最小 POC 原型，验证以下链路：

1. **BobberBar 构造后何时被设为 activeClickableMenu**
   - 反编译 `FishingRod.doFunction()`，定位 `BobberBar` 构造后到 `Game1.activeClickableMenu = bobberBar` 之间的帧时序
   - 确认能否在构造完成后、绘制第一帧前插入 QuizMenu

2. **QuizMenu 显示期间 BobberBar 的状态**
   - BobberBar 的 `update()` 是否会因 activeClickableMenu 被覆盖而继续执行
   - FishingRod 在哪一帧、何种条件下检测 `bobber.doneWithMinigame`
   - 设置 `doneWithMinigame = true` 后 FishingRod 的收杆奖励发放是否正常（鱼种、品质、经验）

3. **QuizMenu 关闭后的恢复**
   - 关闭 QuizMenu 后，是否需要额外清理 BobberBar？
   - FishingRod 的 `doneFishing()` 是否正常触发

4. **宝箱奖励验证**
   - 设 `bobber.treasureCaught = true` 后，FishingRod 是否自动发放宝箱
   - 若否，尝试方案 A（BobberBar 后台 update 生成宝箱内容）或方案 B（手动调用宝箱生成方法）

**POC 范围**: BobberBar → QuizMenu 替换 → 答对给鱼 → 答错给垃圾 → 宝箱发放验证。不含题库管理、统计、配置。

### 9.3 Harmony 版本兼容性

- **目标游戏版本**: Stardew Valley 1.6.x 全版本（需在注释中标注反编译时的具体版本号）
- `BobberBar` 构造函数的可见性（`public` / `internal`）通过反编译确认后使用正确的访问方式
- Harmony 补丁使用 `nameof` / `typeof` 强类型方式指定目标，避免 magic string
- 更新日志中记录每个 Harmony 补丁依赖的游戏版本号

### 9.4 多人模式时间冻结

- **单人**: `Game1.paused = true` 即可
- **多人**: 需要通过网络同步实现全局暂停
  - 优先尝试 `Game1.netWorldState.Value.IsPaused`（需 host 权限）
  - 备选：使用 Harmony patch 冻结 `Game1.gameTimeInterval` 的累加
  - POC 阶段需验证多人模式下时间冻结的可行性

### 9.5 性能

- 题库直接加载到内存，每次钓鱼从内存中取题
- 错题集使用 `HashSet<string>` 实现 O(1) 查找/插入
- 数据持久化仅在每次答题完成后写入，非频繁操作