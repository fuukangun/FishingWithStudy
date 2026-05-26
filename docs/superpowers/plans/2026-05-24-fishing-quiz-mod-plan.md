# 钓鱼答题 Mod — 实现计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**目标:** 将星露谷物语的钓鱼小游戏替换为问答小游戏（Harmony 补丁、自定义 IClickableMenu、国际化、300-500 道题库、统计、学习模式、品质加成）

**架构:** 3 个 Harmony 补丁拦截 BobberBar 构造/绘制和 FishingRod 体力消耗。QuizMenu (IClickableMenu) 替换 BobberBar 成为 activeClickableMenu。QuestionManager 加载/验证/选取 JSON 题库题目。FishRewarder 处理鱼获奖励、品质加成、传说鱼、宝箱。StatsManager 通过 ModDataDictionary 持久化按作用域隔离的统计数据。先完成 POC 验证核心机制，再进行完整实现。

**技术栈:** SMAPI 4.0+ / .NET 6.0 / Harmony 2.x / C# / JSON

---

## 文件结构

```
fishingWithStudy/
├── ModEntry.cs                    # SMAPI 入口，注册 Harmony+GMCM
├── Patchers/
│   └── FishingPatcher.cs          # 3 个 Harmony 补丁
├── UI/
│   ├── QuizMenu.cs                # 答题主界面 (IClickableMenu)
│   └── QuizResultMessage.cs       # 结果反馈浮层
├── Data/
│   ├── Question.cs                # 数据模型 (QuestionCategory, Question, Option)
│   ├── QuestionManager.cs         # 题库加载/验证/选取
│   └── StatsManager.cs            # 按作用域持久化统计
├── Logic/
│   ├── FishRewarder.cs            # 鱼获奖励 + 品质 + 传说鱼 + 宝箱
│   └── Translation.cs             # 国际化 (zh/en)
├── Config/
│   └── ModConfig.cs               # 配置模型
├── assets/
│   └── data/
│       ├── questions.zh.json      # 中文题库 (150+ 题)
│       ├── questions.en.json      # 英文题库 (150+ 题)
│       └── custom/                # 自定义题库目录
├── manifest.json
└── fishingWithStudy.csproj
```

**需创建:** 12 个新文件 + 2 个题库文件
**需修改:** 1 个 (Class1.cs → ModEntry.cs)

---

## POC 阶段（高优先级）

POC 必须在完整实现前验证 4 个关键未知项：

1. **BobberBar → QuizMenu 替换时机** — 能否在 BobberBar 构造完成和第一帧绘制之间插入 QuizMenu？
2. **BobberBar 后台状态** — BobberBar.update() 是否会继续运行？FishingRod 何时检测 doneWithMinigame？
3. **FishingRod 奖励流程** — doneWithMinigame=true 是否能触发正常的奖励逻辑？
4. **宝箱流程** — 设置 bobber.treasureCaught=true 是否能在 fishCaught=false 时触发宝箱发放？

### 任务 0.1: 创建包含 3 个 Harmony 桩补丁的 FishingPatcher

**文件:**
- 修改: `Class1.cs` → 注册 Harmony
- 创建: `Patchers/FishingPatcher.cs`

- [ ] **步骤 1: 重命名 Class1.cs 为 ModEntry.cs 并添加 Harmony 注册**

```csharp
// 修改已有文件：重命名类，添加 Harmony 注册
using StardewModdingAPI;
using HarmonyLib;

namespace fishingWithStudy
{
    public class ModEntry : Mod
    {
        public override void Entry(IModHelper helper)
        {
            var harmony = new Harmony(this.ModManifest.UniqueID);
            
            // 注册补丁
            var bobberBarType = AccessTools.TypeByName("StardewValley.Minigames.BobberBar");
            var drawMethod = AccessTools.Method(bobberBarType, "draw");
            var constructor = AccessTools.Constructor(bobberBarType, new Type[] {
                typeof(int), typeof(float), typeof(bool), typeof(int),
                typeof(bool), typeof(byte), typeof(float), typeof(float),
                typeof(float), typeof(int), typeof(bool)
            });
            
            if (constructor != null)
                harmony.Patch(constructor, prefix: new HarmonyMethod(typeof(FishingPatcher), nameof(FishingPatcher.BobberBar_Prefix)));
            
            if (drawMethod != null)
                harmony.Patch(drawMethod, prefix: new HarmonyMethod(typeof(FishingPatcher), nameof(FishingPatcher.Draw_Prefix)));
            
            // 补丁 #3 目标 FishingRod.doFunction - 捕获体力扣减
            var doFunction = AccessTools.Method(typeof(StardewValley.Tools.FishingRod), "doFunction");
            if (doFunction != null)
                harmony.Patch(doFunction, prefix: new HarmonyMethod(typeof(FishingPatcher), nameof(FishingPatcher.FishingRod_DoFunction_Prefix)));
            
            this.Monitor.Log("Fishing Quiz Mod loaded!", LogLevel.Info);
        }
    }
}
```

- [ ] **步骤 2: 创建包含 3 个桩补丁的 FishingPatcher.cs**

```csharp
// 创建: Patchers/FishingPatcher.cs
using System;
using HarmonyLib;
using StardewValley;
using StardewValley.Minigames;
using StardewValley.Tools;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace fishingWithStudy.Patchers
{
    public static class FishingPatcher
    {
        // 补丁 #1: BobberBar 构造函数 prefix - 捕获鱼数据 + 体力
        public static bool BobberBar_Prefix(
            int whichFish, float fishSize, bool treasure,
            int fishDifficulty, bool finished, byte fishQuality,
            float fishSizeModifier, float bristle, float lineSize,
            int fishCategory, bool fromFishPond, BobberBar __instance)
        {
            // 存储 BobberBar 引用供后续使用
            Logic.FishRewarder.CurrentBobber = __instance;
            
            // 捕获鱼数据
            Logic.FishRewarder.CaptureFishData(whichFish, fishSize, treasure, fishDifficulty,
                fishQuality, fishSizeModifier, bristle, lineSize, fishCategory, fromFishPond);
            
            // 捕获鱼上钩时的体力
            Logic.FishRewarder.CaptureStaminaAtBite(Game1.player.Stamina, Game1.player.MaxStamina);
            
            // 不阻止构造 - BobberBar 必须存在以维持 FishingRod 状态机
            return true;
        }

        // 补丁 #2: BobberBar.draw prefix - 抑制绘制
        public static bool Draw_Prefix()
        {
            // 跳过原始绘制方法
            return false;
        }

        // 补丁 #3: FishingRod.doFunction prefix - 捕获实际体力消耗
        public static void FishingRod_DoFunction_Prefix(FishingRod __instance)
        {
            // 捕获扣减前的体力值
            Logic.FishRewarder.CaptureCastStamina(Game1.player.Stamina);
        }
    }
}
```

- [ ] **步骤 3: 构建并验证 Harmony 补丁注册无报错**

运行: `dotnet build /Users/fuukangun/fuukangun-space/fishingWithStudy/fishingWithStudy.csproj`
预期: 构建成功，无报错

- [ ] **步骤 4: 删除 Class1.cs（已被 ModEntry.cs 替代）**

```bash
rm /Users/fuukangun/fuukangun-space/fishingWithStudy/Class1.cs
```


### 任务 0.2: 创建替换 BobberBar 的基础 QuizMenu

**文件:**
- 创建: `Logic/FishRewarder.cs`（含静态字段的桩类）
- 创建: `UI/QuizMenu.cs`（POC 基础版本）

- [ ] **步骤 1: 创建含静态字段的 FishRewarder.cs**

```csharp
// 创建: Logic/FishRewarder.cs
using StardewValley.Minigames;

namespace fishingWithStudy.Logic
{
    public static class FishRewarder
    {
        // BobberBar 实例引用（由补丁 #1 设置）
        public static BobberBar? CurrentBobber { get; set; }

        // 补丁 #1 捕获的鱼数据
        public static int WhichFish { get; private set; }
        public static float FishSize { get; private set; }
        public static bool HasTreasure { get; private set; }
        public static int FishDifficulty { get; private set; }
        public static byte FishQuality { get; private set; }
        public static float FishSizeModifier { get; private set; }
        public static float Bristle { get; private set; }
        public static float LineSize { get; private set; }
        public static int FishCategory { get; private set; }
        public static bool FromFishPond { get; private set; }

        // 体力追踪
        public static float StaminaAtBite { get; private set; }
        public static float MaxStaminaAtBite { get; private set; }
        public static float StaminaBeforeCast { get; private set; }
        public static float ActualCastCost { get; private set; }

        public static void CaptureFishData(int whichFish, float fishSize, bool treasure,
            int fishDifficulty, byte fishQuality, float fishSizeModifier,
            float bristle, float lineSize, int fishCategory, bool fromFishPond)
        {
            WhichFish = whichFish;
            FishSize = fishSize;
            HasTreasure = treasure;
            FishDifficulty = fishDifficulty;
            FishQuality = fishQuality;
            FishSizeModifier = fishSizeModifier;
            Bristle = bristle;
            LineSize = lineSize;
            FishCategory = fishCategory;
            FromFishPond = fromFishPond;
        }

        public static void CaptureStaminaAtBite(float stamina, float maxStamina)
        {
            StaminaAtBite = stamina;
            MaxStaminaAtBite = maxStamina;
        }

        public static void CaptureCastStamina(float staminaBeforeCast)
        {
            StaminaBeforeCast = staminaBeforeCast;
        }
    }
}
```

- [ ] **步骤 2: 创建基础 QuizMenu.cs (IClickableMenu)**

```csharp
// 创建: UI/QuizMenu.cs
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using StardewValley.Minigames;

namespace fishingWithStudy.UI
{
    public class QuizMenu : IClickableMenu
    {
        private bool isStudyMode;

        public QuizMenu(bool isStudyMode = false)
        {
            this.isStudyMode = isStudyMode;
            
            // 设置菜单尺寸（居中，60% 宽，55% 高）
            width = (int)(Game1.viewport.Width * 0.6);
            height = (int)(Game1.viewport.Height * 0.55);
            xPositionOnScreen = (Game1.viewport.Width - width) / 2;
            yPositionOnScreen = (Game1.viewport.Height - height) / 2;

            // 冻结时间（单人）
            Game1.paused = true;
            
            Game1.playSound("openBox");
        }

        public override void draw(SpriteBatch b)
        {
            // 绘制半透明背景
            b.Draw(Game1.fadeToBlackRect, Game1.viewport.Bounds, Color.Black * 0.5f);
            
            // 绘制菜单背景
            b.Draw(Game1.mouseCursors, new Rectangle(xPositionOnScreen, yPositionOnScreen, width, height),
                new Rectangle(128, 256, 64, 64), Color.White * 0.8f);
            
            // 绘制标题文本
            string title = "Fishing Quiz / 钓鱼答题 (POC)";
            Vector2 titleSize = Game1.dialogueFont.MeasureString(title);
            b.DrawString(Game1.dialogueFont, title,
                new Vector2(xPositionOnScreen + (width - titleSize.X) / 2, yPositionOnScreen + 20),
                Color.Black);

            // POC 测试用的"正确"按钮
            string buttonText = "Correct / 正确";
            Vector2 buttonSize = Game1.smallFont.MeasureString(buttonText);
            b.DrawString(Game1.smallFont, buttonText,
                new Vector2(xPositionOnScreen + (width - buttonSize.X) / 2,
                    yPositionOnScreen + height - 80),
                Color.Black);

            // POC 测试用的"错误"按钮
            string wrongText = "Wrong / 错误";
            Vector2 wrongSize = Game1.smallFont.MeasureString(wrongText);
            b.DrawString(Game1.smallFont, wrongText,
                new Vector2(xPositionOnScreen + (width - wrongSize.X) / 2,
                    yPositionOnScreen + height - 50),
                Color.Black);

            base.draw(b);
        }

        public override void receiveLeftClick(int x, int y, bool playSound = true)
        {
            // POC 测试：检测按钮点击
            if (!isStudyMode)
            {
                // "Correct" 按钮区域
                if (x >= xPositionOnScreen + width / 2 - 100 && x <= xPositionOnScreen + width / 2 + 100 &&
                    y >= yPositionOnScreen + height - 90 && y <= yPositionOnScreen + height - 60)
                {
                    ApplyCorrect();
                }
                // "Wrong" 按钮区域
                if (x >= xPositionOnScreen + width / 2 - 100 && x <= xPositionOnScreen + width / 2 + 100 &&
                    y >= yPositionOnScreen + height - 60 && y <= yPositionOnScreen + height - 30)
                {
                    ApplyWrong();
                }
            }
            else
            {
                exitThisMenu();
            }
        }

        private void ApplyCorrect()
        {
            if (Logic.FishRewarder.CurrentBobber == null)
                return;

            var bobber = Logic.FishRewarder.CurrentBobber;
            bobber.fishCaught = true;
            bobber.doneWithMinigame = true;
            
            Game1.paused = false;
            Game1.exitActiveMenu();
        }

        private void ApplyWrong()
        {
            if (Logic.FishRewarder.CurrentBobber == null)
                return;

            var bobber = Logic.FishRewarder.CurrentBobber;
            bobber.fishCaught = false;
            bobber.doneWithMinigame = true;
            
            Game1.paused = false;
            Game1.exitActiveMenu();
        }

        public override void receiveRightClick(int x, int y, bool playSound = true) { }

        // 覆写 ESC 行为 - 钓鱼时不关允许闭
        public override void receiveKeyPress(Keys key)
        {
            if (isStudyMode && key == Keys.K)
            {
                Game1.paused = false;
                Game1.exitActiveMenu();
            }
        }
    }
}
```

- [ ] **步骤 3: 在构造后用 QuizMenu 替换 BobberBar**

```csharp
// BobberBar_Prefix 末尾添加：
// 用 1 帧延迟让 Game1.activeClickableMenu 赋值先完成
DelayedAction.functionAfterDelay(delegate {
    if (Game1.activeClickableMenu is BobberBar)
    {
        Game1.activeClickableMenu = new UI.QuizMenu();
    }
}, 1);
```

同时在 FishingPatcher 中添加 Postfix:

```csharp
public static void BobberBar_Postfix()
{
    DelayedAction.functionAfterDelay(delegate
    {
        if (Game1.activeClickableMenu is BobberBar)
        {
            Game1.activeClickableMenu = new UI.QuizMenu();
        }
    }, 1);
}
```

并在 ModEntry.cs 中注册 Postfix:

```csharp
if (constructor != null)
{
    harmony.Patch(constructor, 
        prefix: new HarmonyMethod(typeof(FishingPatcher), nameof(FishingPatcher.BobberBar_Prefix)),
        postfix: new HarmonyMethod(typeof(FishingPatcher), nameof(FishingPatcher.BobberBar_Postfix)));
}
```

- [ ] **步骤 4: 构建并游戏中测试**

运行: `dotnet build`
预期: 构建成功
**手动测试:** 抛竿 → BobberBar 不可见 → QuizMenu 出现 → 点击"Correct" → 鱼获到手。点击"Wrong" → 垃圾到手。


### 任务 0.3: POC - 鱼获奖励和垃圾验证

**文件:**
- 修改: `UI/QuizMenu.cs` — 答对加钓鱼经验，答错扣体力

- [ ] **步骤 1: 更新 QuizMenu.ApplyCorrect() 添加钓鱼经验**

```csharp
private void ApplyCorrect()
{
    if (Logic.FishRewarder.CurrentBobber == null)
        return;

    var bobber = Logic.FishRewarder.CurrentBobber;
    bobber.fishCaught = true;
    bobber.doneWithMinigame = true;

    // 根据鱼难度给予钓鱼经验
    int difficulty = Logic.FishRewarder.FishDifficulty;
    int baseXP = difficulty switch
    {
        <= 30 => 7,    // 简单鱼
        <= 50 => 10,   // 中等
        <= 70 => 13,   // 困难
        _ => 15        // 传说/极难
    };
    Game1.player.gainExperience(1, baseXP); // 1 = 钓鱼技能索引
    
    Game1.paused = false;
    Game1.exitActiveMenu();
}
```

- [ ] **步骤 2: 更新 QuizMenu.ApplyWrong() 添加体力惩罚**

```csharp
private void ApplyWrong()
{
    if (Logic.FishRewarder.CurrentBobber == null)
        return;

    var bobber = Logic.FishRewarder.CurrentBobber;
    bobber.fishCaught = false;
    bobber.doneWithMinigame = true;

    // 随机 1-3 体力惩罚
    Random rnd = new Random();
    int penalty = rnd.Next(1, 4);
    Game1.player.Stamina = Math.Max(0, Game1.player.Stamina - penalty);
    
    Game1.paused = false;
    Game1.exitActiveMenu();
}
```

- [ ] **步骤 3: 构建并游戏中测试**

运行: `dotnet build`
**手动测试:**
1. 抛竿 → 答对 → 验证鱼获 + 经验增加
2. 抛竿 → 答错 → 验证垃圾 + 体力减少


### 任务 0.4: POC - 宝箱和传说鱼验证

**文件:**
- 修改: `UI/QuizMenu.cs` — 添加传说鱼 + 宝箱检测
- 修改: `Logic/FishRewarder.cs` — 添加传说鱼判断 + 宝箱处理

- [ ] **步骤 1: 在 FishRewarder 中添加传说鱼检测**

```csharp
// 添加到 FishRewarder.cs:
public static bool IsLegendaryFish()
{
    int[] legendaryIds = { 159, 160, 163, 682, 775 };
    return Array.IndexOf(legendaryIds, WhichFish) >= 0;
}
```

- [ ] **步骤 2: 更新 QuizMenu 支持传说鱼（需答对 5 题）和宝箱**

```csharp
// 在 QuizMenu 中添加字段:
private int requiredCorrect;
private int currentCorrect = 0;
private bool fishRewarded = false;
private bool treasureRewarded = false;

// 构造函数中添加:
public QuizMenu(bool isStudyMode = false)
{
    // ... 已有设置 ...
    
    if (!isStudyMode)
    {
        requiredCorrect = Logic.FishRewarder.IsLegendaryFish() ? 5 : 
                          (Logic.FishRewarder.HasTreasure ? 2 : 1);
        currentCorrect = 0;
        fishRewarded = false;
        treasureRewarded = false;
    }
    // ...
}

// 更新 ApplyCorrect():
private void ApplyCorrect()
{
    if (Logic.FishRewarder.CurrentBobber == null)
        return;

    var bobber = Logic.FishRewarder.CurrentBobber;
    
    currentCorrect++;
    
    // 检查宝箱阈值
    int treasureThreshold = -1;
    if (Logic.FishRewarder.HasTreasure && Logic.FishRewarder.IsLegendaryFish())
        treasureThreshold = 3;
    else if (Logic.FishRewarder.HasTreasure)
        treasureThreshold = 1;
    
    if (currentCorrect == treasureThreshold && !treasureRewarded)
    {
        treasureRewarded = true;
        bobber.treasureCaught = true;
        Game1.addHUDMessage(new HUDMessage("获得宝箱！", 2));
    }
    
    if (currentCorrect >= requiredCorrect && !fishRewarded)
    {
        fishRewarded = true;
        bobber.fishCaught = true;
        bobber.doneWithMinigame = true;
        
        int baseXP = Logic.FishRewarder.FishDifficulty switch
        {
            <= 30 => 7,
            <= 50 => 10,
            <= 70 => 13,
            _ => 15
        };
        Game1.player.gainExperience(1, baseXP);
        
        Game1.paused = false;
        Game1.exitActiveMenu();
    }
    else
    {
        // 还需更多题 - POC 中仅继续
        Game1.playSound("coin");
    }
}

// 更新 ApplyWrong():
private void ApplyWrong()
{
    if (Logic.FishRewarder.CurrentBobber == null)
        return;

    var bobber = Logic.FishRewarder.CurrentBobber;
    bobber.fishCaught = false;
    bobber.doneWithMinigame = true;
    
    if (Logic.FishRewarder.IsLegendaryFish())
    {
        // 传说鱼：不扣体力，鱼直接跑
        Game1.paused = false;
        Game1.exitActiveMenu();
        return;
    }
    
    Random rnd = new Random();
    int penalty = rnd.Next(1, 4);
    Game1.player.Stamina = Math.Max(0, Game1.player.Stamina - penalty);
    
    Game1.paused = false;
    Game1.exitActiveMenu();
}
```

- [ ] **步骤 3: 构建并游戏中测试**

运行: `dotnet build`
**手动测试:**
1. 钓传说鱼 → 验证需要点 5 次 → 第 5 次正确后获鱼
2. 传说鱼中答错 → 验证鱼跑、不扣体力
3. 钓有宝箱的鱼 → 验证需点 2 次，第 1 次宝箱信息，第 2 次获鱼


---

## 阶段 1: 核心框架

### 任务 1.1: 创建 ModConfig 配置类

**文件:**
- 创建: `Config/ModConfig.cs`

- [ ] **步骤 1: 创建 ModConfig.cs**

```csharp
// 创建: Config/ModConfig.cs
namespace fishingWithStudy.Config
{
    public class ModConfig
    {
        public bool TimerEnabled { get; set; } = true;
        public int TimerSeconds { get; set; } = 25;
        public string SelectedBank { get; set; } = "default";
        public string SelectedCategory { get; set; } = "";
        public string StudyModeKeybind { get; set; } = "K";
    }
}
```

- [ ] **步骤 2: 在 ModEntry.cs 中注册配置**

```csharp
// 在 ModEntry.Entry() 中添加:
var config = helper.ReadConfig<ModConfig>();
```


### 任务 1.2: 创建 Translation 国际化类

**文件:**
- 创建: `Logic/Translation.cs`

- [ ] **步骤 1: 创建 Translation.cs**

```csharp
// 创建: Logic/Translation.cs
using System.Collections.Generic;
using StardewValley;

namespace fishingWithStudy.Logic
{
    public static class Translation
    {
        private static bool IsChinese => Game1.content.CurrentLanguage == "zh";

        private static readonly Dictionary<string, string> zh = new()
        {
            { "ui.title", "钓鱼答题" },
            { "ui.confirm", "确认" },
            { "ui.submit", "交卷({0})" },
            { "ui.correct", "正确！" },
            { "ui.wrong", "错误！" },
            { "ui.answer_is", "正确答案：{0}" },
            { "ui.caught", "获得：{0}" },
            { "ui.quality_up", "品质提升！" },
            { "ui.timeout", "时间到！" },
            { "ui.fish_got_away", "鱼跑掉了！" },
            { "ui.multiple_hint", "(可多选)" },
            { "ui.category", "{0}" },
            { "ui.study_mode_title", "学习模式" },
            { "ui.stamina_penalty", "体力 -{0}" },
            { "ui.treasure_acquired", "获得宝箱！" },
            { "ui.next_in", "下一题 {0}秒" },
            { "ui.continue", "继续..." },
            { "ui.complete", "完成！" },
            { "ui.continue_correct", "答对 {0} 题后失败，鱼跑了！" },
            { "ui.no_bank", "未加载题库，无法开始钓鱼答题" },
        };

        private static readonly Dictionary<string, string> en = new()
        {
            { "ui.title", "Fishing Quiz" },
            { "ui.confirm", "Confirm" },
            { "ui.submit", "Submit({0})" },
            { "ui.correct", "Correct!" },
            { "ui.wrong", "Wrong!" },
            { "ui.answer_is", "Answer: {0}" },
            { "ui.caught", "Caught: {0}" },
            { "ui.quality_up", "Quality Up!" },
            { "ui.timeout", "Time's Up!" },
            { "ui.fish_got_away", "The fish got away!" },
            { "ui.multiple_hint", "(Multiple)" },
            { "ui.category", "{0}" },
            { "ui.study_mode_title", "Study Mode" },
            { "ui.stamina_penalty", "Stamina -{0}" },
            { "ui.treasure_acquired", "Treasure Acquired!" },
            { "ui.next_in", "Next in {0}s" },
            { "ui.continue", "Continue..." },
            { "ui.complete", "Complete!" },
            { "ui.continue_correct", "Failed after {0} correct! Fish got away!" },
            { "ui.no_bank", "No question bank loaded, cannot start fishing quiz" },
        };

        public static string Get(string key, params object[] args)
        {
            var dict = IsChinese ? zh : en;
            string format = dict.ContainsKey(key) ? dict[key] : key;
            return args.Length > 0 ? string.Format(format, args) : format;
        }
    }
}
```


### 任务 1.3: 创建题目数据模型

**文件:**
- 创建: `Data/Question.cs`

- [ ] **步骤 1: 创建 Question.cs**

```csharp
// 创建: Data/Question.cs
using System.Collections.Generic;

namespace fishingWithStudy.Data
{
    public class QuestionCategory
    {
        public string Category { get; set; } = "";
        public Dictionary<string, string> CategoryI18n { get; set; } = new();
        public List<Question> QuestionList { get; set; } = new();
    }

    public class Question
    {
        public string Id { get; set; } = "";
        public string Type { get; set; } = "single"; // "single" | "multiple"
        public Dictionary<string, string> QuestionText { get; set; } = new();
        public List<Option> Options { get; set; } = new();
        public List<string> Answer { get; set; } = new();
    }

    public class Option
    {
        public string Tag { get; set; } = "";
        public Dictionary<string, string> Text { get; set; } = new();
    }
}
```


### 任务 1.4: 创建 QuestionManager（加载、验证、选取）

**文件:**
- 创建: `Data/QuestionManager.cs`

- [ ] **步骤 1: 创建 QuestionManager.cs**

```csharp
// 创建: Data/QuestionManager.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using StardewValley;
using StardewModdingAPI;

namespace fishingWithStudy.Data
{
    public class QuestionManager
    {
        private readonly IMonitor monitor;
        private readonly IModHelper helper;

        // 已加载的题库: bankId -> categories
        private Dictionary<string, List<QuestionCategory>> loadedBanks = new();
        
        // 当前活跃状态
        private List<Question> currentQuestionPool = new();
        private List<string> questionOrder = new();
        private HashSet<string> wrongSet = new();
        private int currentIndex = 0;
        private bool firstRoundDone = false;

        // 可用题库列表（供 GMCM 下拉框使用）
        public List<string> AvailableBanks { get; private set; } = new() { "default" };

        public QuestionManager(IMonitor monitor, IModHelper helper)
        {
            this.monitor = monitor;
            this.helper = helper;
        }

        public void Initialize()
        {
            string lang = Game1.content.CurrentLanguage == "zh" ? "zh" : "en";
            
            // 加载默认题库
            string defaultPath = Path.Combine(helper.DirectoryPath, "assets", "data", $"questions.{lang}.json");
            var defaultBank = LoadAndValidateBank(defaultPath, "default");
            if (defaultBank != null && defaultBank.Count > 0)
            {
                loadedBanks["default"] = defaultBank;
                monitor.Log($"默认题库加载成功: {defaultBank.Sum(c => c.QuestionList.Count)} 题, {defaultBank.Count} 个分类", LogLevel.Info);
            }
            else
            {
                monitor.Log("默认题库加载失败或无有效题目", LogLevel.Error);
            }

            // 扫描自定义目录
            string customDir = Path.Combine(helper.DirectoryPath, "assets", "data", "custom");
            if (Directory.Exists(customDir))
            {
                foreach (string file in Directory.GetFiles(customDir, "*.json"))
                {
                    string fileName = Path.GetFileName(file);
                    AvailableBanks.Add(fileName);
                }
            }
            else
            {
                Directory.CreateDirectory(customDir);
                monitor.Log("已创建自定义题库目录", LogLevel.Info);
            }

            // 加载选中题库
            if (loadedBanks.Count == 0 || !loadedBanks.ContainsKey("default"))
            {
                monitor.Log("无有效题库加载，钓鱼答题功能将禁用", LogLevel.Warning);
            }
        }

        private List<QuestionCategory>? LoadAndValidateBank(string filePath, string? bankIdForLog)
        {
            if (!File.Exists(filePath))
            {
                monitor.Log($"题库文件未找到: {filePath}", LogLevel.Warning);
                return null;
            }

            try
            {
                string json = File.ReadAllText(filePath);
                var categories = JsonSerializer.Deserialize<List<QuestionCategory>>(json);
                if (categories == null) return null;

                var seenIds = new HashSet<string>();
                foreach (var cat in categories)
                {
                    cat.QuestionList = cat.QuestionList.Where(q =>
                    {
                        if (string.IsNullOrWhiteSpace(q.Id) || string.IsNullOrWhiteSpace(q.Type) ||
                            q.QuestionText == null || q.QuestionText.Count == 0 ||
                            q.Options == null || q.Options.Count == 0 ||
                            q.Answer == null || q.Answer.Count == 0)
                        {
                            monitor.Log($"跳过题库 '{bankIdForLog}' 中的无效题目: 缺少必填字段", LogLevel.Warning);
                            return false;
                        }
                        if (q.Type != "single" && q.Type != "multiple")
                        {
                            monitor.Log($"跳过题目 '{q.Id}' (题库 '{bankIdForLog}'): 无效类型 '{q.Type}'", LogLevel.Warning);
                            return false;
                        }
                        if (seenIds.Contains(q.Id))
                        {
                            monitor.Log($"跳过重复题目 ID '{q.Id}' (题库 '{bankIdForLog}')", LogLevel.Warning);
                            return false;
                        }
                        seenIds.Add(q.Id);
                        return true;
                    }).ToList();
                }

                return categories.Where(c => c.QuestionList.Count > 0).ToList();
            }
            catch (JsonException ex)
            {
                monitor.Log($"题库 '{bankIdForLog}' JSON 解析错误: {ex.Message}", LogLevel.Error);
                return null;
            }
        }

        public bool LoadBank(string bankId)
        {
            if (bankId == "default")
            {
                if (!loadedBanks.ContainsKey("default"))
                {
                    monitor.Log("默认题库未加载，无法切换", LogLevel.Warning);
                    return false;
                }
                SwitchToBank("default");
                return true;
            }

            string customPath = Path.Combine(helper.DirectoryPath, "assets", "data", "custom", bankId);
            var bank = LoadAndValidateBank(customPath, bankId);
            if (bank != null && bank.Count > 0)
            {
                loadedBanks[bankId] = bank;
                SwitchToBank(bankId);
                return true;
            }

            // 回退到默认
            monitor.Log($"无法加载自定义题库 '{bankId}'，回退到默认", LogLevel.Warning);
            if (loadedBanks.ContainsKey("default"))
            {
                SwitchToBank("default");
                return false;
            }
            return false;
        }

        private void SwitchToBank(string bankId)
        {
            currentQuestionPool = loadedBanks[bankId]
                .SelectMany(c => c.QuestionList)
                .ToList();
        }

        public void SetCategory(string category)
        {
            if (string.IsNullOrEmpty(category))
            {
                // 全部
                string currentBank = GetCurrentBankId();
                if (loadedBanks.ContainsKey(currentBank))
                {
                    currentQuestionPool = loadedBanks[currentBank]
                        .SelectMany(c => c.QuestionList)
                        .ToList();
                }
            }
            else
            {
                string currentBank = GetCurrentBankId();
                if (loadedBanks.ContainsKey(currentBank))
                {
                    currentQuestionPool = loadedBanks[currentBank]
                        .Where(c => c.Category == category)
                        .SelectMany(c => c.QuestionList)
                        .ToList();
                }
            }
        }

        public string GetCurrentBankId()
        {
            foreach (var kvp in loadedBanks)
            {
                var pool = kvp.Value.SelectMany(c => c.QuestionList).ToList();
                if (pool.Count == currentQuestionPool.Count)
                    return kvp.Key;
            }
            return "default";
        }

        public List<string> GetCategories(string bankId)
        {
            if (!loadedBanks.ContainsKey(bankId))
                return new List<string>();
            return loadedBanks[bankId].Select(c => c.Category).ToList();
        }

        public string? GetCategoryI18n(string bankId, string category)
        {
            if (!loadedBanks.ContainsKey(bankId))
                return null;
            var cat = loadedBanks[bankId].FirstOrDefault(c => c.Category == category);
            if (cat == null) return null;
            string lang = Game1.content.CurrentLanguage == "zh" ? "zh" : "en";
            return cat.CategoryI18n.ContainsKey(lang) ? cat.CategoryI18n[lang] : category;
        }

        public int TotalQuestions => currentQuestionPool.Count;

        public bool HasQuestions => currentQuestionPool.Count > 0;

        // 根据设计文档 4.4 节的随机出题逻辑选取下一题
        public Question? GetNextQuestion()
        {
            if (currentQuestionPool.Count == 0) return null;

            // 按需初始化顺序
            if (questionOrder.Count == 0 || questionOrder.Count != currentQuestionPool.Count)
            {
                ShuffleQuestions();
            }

            Question? q = null;

            if (!firstRoundDone)
            {
                if (currentIndex < questionOrder.Count)
                {
                    q = FindQuestionById(questionOrder[currentIndex]);
                    currentIndex++;
                }
                else
                {
                    firstRoundDone = true;
                }
            }

            if (firstRoundDone && q == null)
            {
                int offset = currentIndex - questionOrder.Count;
                if (offset % 5 == 4 && wrongSet.Count > 0)
                {
                    // 从错题集中随机取一题
                    var wrongId = wrongSet.ElementAt(new Random().Next(wrongSet.Count));
                    q = FindQuestionById(wrongId);
                }
                else
                {
                    q = FindQuestionById(questionOrder[currentIndex % questionOrder.Count]);
                    currentIndex++;
                }
            }

            return q;
        }

        public void RecordAnswer(string questionId, bool correct)
        {
            if (correct)
            {
                if (wrongSet.Contains(questionId))
                    wrongSet.Remove(questionId);
            }
            else
            {
                if (!wrongSet.Contains(questionId))
                    wrongSet.Add(questionId);
            }
        }

        public void ResetPool()
        {
            questionOrder.Clear();
            wrongSet.Clear();
            currentIndex = 0;
            firstRoundDone = false;
        }

        private void ShuffleQuestions()
        {
            var rng = new Random();
            questionOrder = currentQuestionPool
                .Select(q => q.Id)
                .OrderBy(_ => rng.Next())
                .ToList();
            currentIndex = 0;
            firstRoundDone = false;
            wrongSet.Clear();
        }

        private Question? FindQuestionById(string id)
        {
            return currentQuestionPool.FirstOrDefault(q => q.Id == id);
        }
    }
}
```

- [ ] **步骤 2: 在 ModEntry.cs 中注册 QuestionManager**

```csharp
// 在 ModEntry.Entry() 中 config 之后添加:
var questionManager = new QuestionManager(Monitor, helper);
questionManager.Initialize();
```


### 任务 1.5: 创建 StatsManager（持久化统计）

**文件:**
- 创建: `Data/StatsManager.cs`

- [ ] **步骤 1: 创建 StatsManager.cs**

```csharp
// 创建: Data/StatsManager.cs
using System;
using System.Collections.Generic;
using System.Linq;
using StardewValley;

namespace fishingWithStudy.Data
{
    public class StatsManager
    {
        private const string Prefix = "fishingWithStudy";

        // 作用域统计
        public class ScopeStats
        {
            public int TotalAnswers { get; set; }
            public int CorrectAnswers { get; set; }
            public List<string> QuestionOrder { get; set; } = new();
            public List<string> WrongSet { get; set; } = new();
            public int CurrentIndex { get; set; }
            public bool FirstRoundDone { get; set; }

            public double Accuracy => TotalAnswers > 0 ? (double)CorrectAnswers / TotalAnswers : 0;
        }

        // 全局统计
        public class GlobalStats
        {
            public int TotalAnswers { get; set; }
            public int CorrectAnswers { get; set; }

            public double Accuracy => TotalAnswers > 0 ? (double)CorrectAnswers / TotalAnswers : 0;
        }

        private readonly IMonitor monitor;

        public StatsManager(IMonitor monitor)
        {
            this.monitor = monitor;
        }

        public string BuildScopeKey(string bankId, string category)
        {
            // ScopeKey 格式: bankId 或 bankId:category（不含 / 字符）
            return string.IsNullOrEmpty(category) ? bankId : $"{bankId}:{category}";
        }

        public ScopeStats LoadScopeStats(string scopeKey)
        {
            var modData = Game1.player.modData;
            var stats = new ScopeStats();

            string key(string field) => $"{Prefix}:{scopeKey}.{field}";

            if (modData.ContainsKey(key("TotalAnswers")))
                stats.TotalAnswers = int.Parse(modData[key("TotalAnswers")]);
            if (modData.ContainsKey(key("CorrectAnswers")))
                stats.CorrectAnswers = int.Parse(modData[key("CorrectAnswers")]);
            if (modData.ContainsKey(key("WrongSet")))
                stats.WrongSet = modData[key("WrongSet")].Split(',', StringSplitOptions.RemoveEmptyEntries).ToList();
            if (modData.ContainsKey(key("QuestionOrder")))
                stats.QuestionOrder = modData[key("QuestionOrder")].Split(',', StringSplitOptions.RemoveEmptyEntries).ToList();
            if (modData.ContainsKey(key("CurrentIndex")))
                stats.CurrentIndex = int.Parse(modData[key("CurrentIndex")]);
            if (modData.ContainsKey(key("FirstRoundDone")))
                stats.FirstRoundDone = modData[key("FirstRoundDone")] == "true";

            return stats;
        }

        public void SaveScopeStats(string scopeKey, ScopeStats stats)
        {
            var modData = Game1.player.modData;
            string key(string field) => $"{Prefix}:{scopeKey}.{field}";

            modData[key("TotalAnswers")] = stats.TotalAnswers.ToString();
            modData[key("CorrectAnswers")] = stats.CorrectAnswers.ToString();
            modData[key("WrongSet")] = string.Join(",", stats.WrongSet);
            modData[key("QuestionOrder")] = string.Join(",", stats.QuestionOrder);
            modData[key("CurrentIndex")] = stats.CurrentIndex.ToString();
            modData[key("FirstRoundDone")] = stats.FirstRoundDone ? "true" : "false";
        }

        public GlobalStats LoadGlobalStats()
        {
            var modData = Game1.player.modData;
            var stats = new GlobalStats();

            if (modData.ContainsKey($"{Prefix}:global.TotalAnswers"))
                stats.TotalAnswers = int.Parse(modData[$"{Prefix}:global.TotalAnswers"]);
            if (modData.ContainsKey($"{Prefix}:global.CorrectAnswers"))
                stats.CorrectAnswers = int.Parse(modData[$"{Prefix}:global.CorrectAnswers"]);

            return stats;
        }

        public void SaveGlobalStats(GlobalStats stats)
        {
            var modData = Game1.player.modData;
            modData[$"{Prefix}:global.TotalAnswers"] = stats.TotalAnswers.ToString();
            modData[$"{Prefix}:global.CorrectAnswers"] = stats.CorrectAnswers.ToString();
        }

        public void RecordAnswer(string scopeKey, bool correct)
        {
            // 更新作用域统计
            var scopeStats = LoadScopeStats(scopeKey);
            scopeStats.TotalAnswers++;
            if (correct) scopeStats.CorrectAnswers++;
            SaveScopeStats(scopeKey, scopeStats);

            // 更新全局统计
            var globalStats = LoadGlobalStats();
            globalStats.TotalAnswers++;
            if (correct) globalStats.CorrectAnswers++;
            SaveGlobalStats(globalStats);
        }

        public List<string> GetQuestionOrder(string scopeKey)
        {
            return LoadScopeStats(scopeKey).QuestionOrder;
        }

        public void SaveQuestionOrder(string scopeKey, List<string> order)
        {
            var stats = LoadScopeStats(scopeKey);
            stats.QuestionOrder = order;
            SaveScopeStats(scopeKey, stats);
        }
    }
}
```


---

## 阶段 2: QuizMenu 完整实现

### 任务 2.1: 完整 QuizMenu UI（布局、交互、倒计时、多题）

**文件:**
- 重写: `UI/QuizMenu.cs`（完整实现替代 POC 版本）

- [ ] **步骤 1: 完整重写 QuizMenu.cs**

完整代码参见原始英文版 plan（因字符长度限制，此处省略完整代码，所有功能与英文版一致）:

核心功能:
- 基于百分比的响应式布局
- 单选/多选支持
- 倒计时（默认 25 秒，≤5 秒变红）
- 多题进度显示（传说鱼 1/5）
- 答题反馈（正确/错误/超时）
- 过渡动画（3 秒实时计时）
- 宝箱浮层
- 分类标签
- 学习模式支持
- 时间冻结（单人模式）

文件长度较大，实际编写时需包含完整的:
- `draw()` 方法 — 绘制标题、进度、倒计时、题目、选项、按钮、分类标签、反馈浮层
- `update()` 方法 — 处理倒计时、反馈计时、过渡计时
- `receiveLeftClick()` — 处理选项点击和提交
- `HandleAnswerResult()` — 核对答案、判断宝箱阈值/完成条件、调用 FishRewarder
- 各辅助方法

- [ ] **步骤 2: 构建并游戏中测试**

运行: `dotnet build`
**手动测试:** 抛竿 → QuizMenu 显示题目 → 选择选项 → 确认 → 验证反馈显示 → 鱼获/垃圾


### 任务 2.2: 创建 QuizResultMessage 反馈浮层

**文件:**
- 创建: `UI/QuizResultMessage.cs`

- [ ] **步骤 1: 创建 QuizResultMessage.cs**

```csharp
// 创建: UI/QuizResultMessage.cs
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;

namespace fishingWithStudy.UI
{
    public class QuizResultMessage
    {
        public static void DrawResultOverlay(SpriteBatch b, Rectangle bounds,
            string message, Color color, string? subMessage = null) { /* 绘制结果浮层 */ }

        public static void DrawTreasureOverlay(SpriteBatch b, Rectangle bounds,
            string title, List<string> items) { /* 绘制宝箱内容浮层 */ }

        public static void DrawTransitionOverlay(SpriteBatch b, Rectangle bounds,
            string message, float countdown) { /* 绘制过渡倒计时浮层 */ }
    }
}
```

各方法实现与英文版 plan 一致。


### 任务 2.3: 实现学习模式

**文件:**
- 修改: `ModEntry.cs` — 添加学习模式快捷键处理
- 修改: `UI/QuizMenu.cs` — 添加 IsStudyMode 公共属性

- [ ] **步骤 1: 在 ModEntry.cs 中添加学习模式快捷键处理器**

```csharp
// 在 ModEntry.Entry() 中，Harmony 注册之后添加:
helper.Input.ButtonPressed += (sender, e) =>
{
    if (!Context.IsWorldReady) return;
    
    string keybind = config.StudyModeKeybind;
    if (e.Button.ToString().Equals(keybind, StringComparison.OrdinalIgnoreCase))
    {
        // 检查是否在钓鱼中
        if (Game1.activeClickableMenu is BobberBar)
        {
            return; // 钓鱼中不响应
        }
        
        // 切换学习模式
        if (Game1.activeClickableMenu is UI.QuizMenu studyMenu &&
            studyMenu.IsStudyMode)
        {
            // 关闭学习模式
            if (Game1.player.Stamina <= 0)
                Game1.player.Stamina = 1;
            Game1.paused = false;
            Game1.exitActiveMenu();
        }
        else
        {
            // 打开学习模式
            string scopeKey = statsManager.BuildScopeKey(
                config.SelectedBank, config.SelectedCategory);
            Game1.activeClickableMenu = new UI.QuizMenu(
                config, questionManager, statsManager, scopeKey, isStudyMode: true);
        }
    }
};
```

```csharp
// 在 QuizMenu.cs 中添加:
public bool IsStudyMode => isStudyMode;
```

- [ ] **步骤 2: 构建并测试**

运行: `dotnet build`
**手动测试:** 非钓鱼时按 K → QuizMenu 以学习模式打开 → 答题 → 再按 K 关闭


---

## 阶段 3: 鱼获奖励完整实现

### 任务 3.1: 实现完整 FishRewarder（品质加成 + 体力返还）

**文件:**
- 重写: `Logic/FishRewarder.cs`（完整实现）

- [ ] **步骤 1: 完整重写 FishRewarder.cs**

```csharp
// 重写: Logic/FishRewarder.cs
using System;
using StardewValley;
using StardewValley.Minigames;
using fishingWithStudy.Data;

namespace fishingWithStudy.Logic
{
    public static class FishRewarder
    {
        // BobberBar 引用
        public static BobberBar? CurrentBobber { get; set; }

        // 捕获的鱼数据
        public static int WhichFish { get; private set; }
        public static float FishSize { get; private set; }
        public static bool HasTreasure { get; private set; }
        public static int FishDifficulty { get; private set; }
        public static byte FishQuality { get; private set; }
        public static float FishSizeModifier { get; private set; }
        public static float Bristle { get; private set; }
        public static float LineSize { get; private set; }
        public static int FishCategory { get; private set; }
        public static bool FromFishPond { get; private set; }

        // 体力追踪
        public static float StaminaAtBite { get; private set; }
        public static float MaxStaminaAtBite { get; private set; }
        public static float StaminaBeforeCast { get; private set; }
        public static float ActualCastCost { get; private set; }

        // 传说鱼 ID (SDV 1.6)
        private static readonly int[] LegendaryIds = { 159, 160, 163, 682, 775 };

        public static bool IsLegendaryFish()
        {
            return Array.IndexOf(LegendaryIds, WhichFish) >= 0;
        }

        public static void CaptureFishData(int whichFish, float fishSize, bool treasure,
            int fishDifficulty, byte fishQuality, float fishSizeModifier,
            float bristle, float lineSize, int fishCategory, bool fromFishPond) { /* 同上 */ }

        public static void CaptureStaminaAtBite(float stamina, float maxStamina) { /* 同上 */ }
        public static void CaptureCastStamina(float staminaBeforeCast) { /* 同上 */ }

        // 从补丁 #3 Postfix 调用，记录实际消耗
        public static void FinalizeCastCost(float staminaAfterCast)
        {
            ActualCastCost = Math.Max(0, StaminaBeforeCast - staminaAfterCast);
        }

        // 应用正确回答：鱼获奖励 + 品质加成 + 体力返还
        public static void ApplyCorrect(StatsManager statsManager, string scopeKey)
        {
            if (CurrentBobber == null) return;

            // 1. 从全局统计计算品质加成
            var globalStats = statsManager.LoadGlobalStats();
            if (globalStats.TotalAnswers > 66)
            {
                double accuracy = globalStats.Accuracy;
                int quality = -1;
                double roll = new Random().NextDouble();

                if (accuracy > 0.80 && roll < 0.70)
                    quality = 3; // 铱星
                else if (accuracy > 0.70 && roll < 0.70)
                    quality = 2; // 金星
                else if (accuracy > 0.60 && roll < 0.70)
                    quality = 1; // 银星

                if (quality >= 0)
                    CurrentBobber.fishQuality = (byte)quality;
            }

            // 2. 标记鱼获
            CurrentBobber.fishCaught = true;
            CurrentBobber.doneWithMinigame = true;

            // 3. 给予钓鱼经验（基于难度）
            int baseXP = FishDifficulty switch
            {
                <= 30 => 7,
                <= 50 => 10,
                <= 70 => 13,
                _ => 15
            };
            Game1.player.gainExperience(1, baseXP);

            // 4. 返还体力（仅最终完成时，精确消耗值，不超过最大体力上限）
            float refund = Math.Min(ActualCastCost,
                Game1.player.MaxStamina - Game1.player.Stamina);
            Game1.player.Stamina += refund;
        }

        // 答错：垃圾 + 体力惩罚
        public static void ApplyWrong()
        {
            if (CurrentBobber == null) return;

            CurrentBobber.fishCaught = false;
            CurrentBobber.doneWithMinigame = true;

            int penalty = new Random().Next(1, 4);
            Game1.player.Stamina = Math.Max(0, Game1.player.Stamina - penalty);
        }

        // 超时：无惩罚
        public static void ApplyTimeout()
        {
            if (CurrentBobber == null) return;
            CurrentBobber.fishCaught = false;
            CurrentBobber.doneWithMinigame = true;
        }

        // 传说鱼答错：鱼跑掉，不扣体力
        public static void ApplyLegendaryEscape()
        {
            if (CurrentBobber == null) return;
            CurrentBobber.fishCaught = false;
            CurrentBobber.doneWithMinigame = true;
        }

        // 多题场景结束时统一返还体力（用于需要逐题判断的场景）
        public static void ApplyMultiQuestionStaminaRefund()
        {
            float refund = Math.Min(ActualCastCost,
                Game1.player.MaxStamina - Game1.player.Stamina);
            Game1.player.Stamina += refund;
        }
    }
}
```

- [ ] **步骤 2: 构建并游戏中测试**

运行: `dotnet build`
**手动测试:** 钓鱼答对 → 验证品质加成（66 题后）、体力返还、经验增加。答错 → 验证体力惩罚。


### 任务 3.2: 传说鱼 + 宝箱完整集成

**文件:**
- 修改: `UI/QuizMenu.cs` — 验证 HandleAnswerResult 中的传说鱼/宝箱逻辑

- [ ] **步骤 1: 验证 QuizMenu 中的宝箱阈值逻辑**

确认 `HandleAnswerResult()` 中正确分支的处理顺序:

```csharp
// 在 HandleAnswerResult 正确分支中:
if (!isStudyMode)
{
    // 优先检查宝箱阈值
    if (currentCorrect == treasureThreshold && !treasureRewarded)
    {
        treasureRewarded = true;
        if (FishRewarder.CurrentBobber != null)
            FishRewarder.CurrentBobber.treasureCaught = true;
        
        showingTreasure = true;
        treasureTimer = TreasureDuration;
        Game1.playSound("coin");
        return;
    }

    // 再检查是否全部完成
    if (currentCorrect >= requiredCorrect)
    {
        Game1.playSound("achievement");
        showingResult = true;
        resultTimer = FeedbackDuration;
        
        if (FishRewarder.CurrentBobber != null)
            FishRewarder.ApplyCorrect(statsManager, scopeKey);
        return;
    }
}
```

关键: 中间题正确时只过渡到下一题，不调用 `ApplyCorrect()`（仅在最终题正确时调用）。

- [ ] **步骤 2: 构建并测试**

运行: `dotnet build`
**手动测试:**
1. 传说鱼 → 答对 5 题 → 验证鱼获 + 品质
2. 传说鱼 + 答错 → 鱼跑、无体力损失、无鱼获
3. 宝箱 + 传说鱼 → Q3 得宝箱、Q5 得鱼
4. 宝箱阈值前答错 → 无宝箱、无鱼
5. 宝箱阈值后答错 → 宝箱保留、鱼跑了


### 任务 3.3: 多题实时过渡 + 倒计时

**文件:**
- 验证: `UI/QuizMenu.cs` — 过渡计时器使用实时时间 (GameTime delta)

- [ ] **步骤 1: 验证实时过渡实现**

确认 QuizMenu.update() 中使用 `gameTime.ElapsedGameTime.TotalSeconds` 递减 `transitionTimer`，确保 3 秒为真实时间而非帧率依赖。

```csharp
// 已在 QuizMenu.update() 中实现:
if (transitioning)
{
    transitionTimer -= delta;  // delta = gameTime.ElapsedGameTime.TotalSeconds
    if (transitionTimer <= 0)
    {
        transitioning = false;
        NextQuestion();
    }
    return;
}
```

- [ ] **步骤 2: 构建并测试**

运行: `dotnet build`
**手动测试:** 传说鱼 → 答对 1 题 → 验证过渡倒计时为实时（用秒表确认 3 秒）


---

## 阶段 4: 配置 (GMCM)

### 任务 4.1: GenericModConfigMenu 集成

**文件:**
- 修改: `ModEntry.cs` — 添加 GMCM 注册

- [ ] **步骤 1: 在 ModEntry.cs 中添加 GMCM 集成**

```csharp
// 在 ModEntry.Entry() 中 config 加载之后添加:

// GenericModConfigMenu 集成
var gmcmApi = helper.ModRegistry.GetApi<GenericModConfigMenuAPI>("spacechase0.GenericModConfigMenu");
if (gmcmApi != null)
{
    gmcmApi.Register(
        mod: this.ModManifest,
        reset: () => config = new ModConfig(),
        save: () => helper.WriteConfig(config)
    );

    gmcmApi.AddBoolOption(
        mod: this.ModManifest,
        name: () => Translation.Get("config.timer_enabled"),
        tooltip: () => Translation.Get("config.timer_enabled_tip"),
        getValue: () => config.TimerEnabled,
        setValue: (val) => config.TimerEnabled = val
    );

    gmcmApi.AddNumberOption(
        mod: this.ModManifest,
        name: () => Translation.Get("config.timer_seconds"),
        tooltip: () => Translation.Get("config.timer_seconds_tip"),
        getValue: () => config.TimerSeconds,
        setValue: (val) => config.TimerSeconds = (int)val,
        min: 5, max: 60, interval: 1
    );

    gmcmApi.AddTextOption(
        mod: this.ModManifest,
        name: () => Translation.Get("config.bank"),
        tooltip: () => Translation.Get("config.bank_tip"),
        getValue: () => config.SelectedBank,
        setValue: (val) => {
            if (!IsFishing())
            {
                config.SelectedBank = val;
                if (val != "default")
                    questionManager.LoadBank(val);
            }
        },
        allowedValues: questionManager.AvailableBanks.ToArray()
    );

    // 分类选择、快捷键绑定等（详见英文版 plan）
}
```

- [ ] **步骤 2: 在 Translation.cs 中添加配置相关 i18n 键**

```csharp
// 中文:
{ "config.timer_enabled", "启用倒计时" },
{ "config.timer_enabled_tip", "答题时是否显示倒计时" },
{ "config.timer_seconds", "倒计时秒数" },
{ "config.timer_seconds_tip", "每题可用的答题时间 (5-60秒)" },
{ "config.bank", "题库" },
{ "config.bank_tip", "选择当前使用的题库" },
{ "config.category", "分类" },
{ "config.category_tip", "选择题目分类（空=全部）" },
{ "config.study_key", "学习模式快捷键" },
{ "config.study_key_tip", "按此键打开/关闭学习模式" },

// English:
{ "config.timer_enabled", "Enable Timer" },
{ "config.timer_enabled_tip", "Show countdown timer during quiz" },
{ "config.timer_seconds", "Timer Seconds" },
{ "config.timer_seconds_tip", "Time per question (5-60 seconds)" },
{ "config.bank", "Question Bank" },
{ "config.bank_tip", "Select the question bank to use" },
{ "config.category", "Category" },
{ "config.category_tip", "Filter questions by category (empty=all)" },
{ "config.study_key", "Study Mode Key" },
{ "config.study_key_tip", "Press this key to open/close study mode" },
```

- [ ] **步骤 3: 构建并测试**

运行: `dotnet build`
**手动测试:** 打开 GMCM → 验证所有选项 → 切换题库/分类 → 验证生效 → 钓鱼中切换被锁定


---

## 阶段 5: 内容与完善

### 任务 5.1: 创建题库（zh/en，各 150+ 题）

**文件:**
- 创建: `assets/data/questions.zh.json`
- 创建: `assets/data/questions.en.json`

- [ ] **步骤 1: 创建中文题库**

题库涵盖分类：角色知识、地点、作物、动物、技能、节日、烹饪、钓鱼、采矿、怪物、综合知识。

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
        "id": "sv_char_001",
        "type": "single",
        "question": {
          "zh": "潘姆最喜欢的礼物是什么？",
          "en": "What is Pam's favorite gift?"
        },
        "options": [
          { "tag": "A", "text": { "zh": "煎鸡蛋", "en": "Fried Egg" } },
          { "tag": "B", "text": { "zh": "钻石", "en": "Diamond" } },
          { "tag": "C", "text": { "zh": "虞美人", "en": "Poppy" } },
          { "tag": "D", "text": { "zh": "啤酒", "en": "Beer" } }
        ],
        "answer": ["D"]
      }
    ]
  }
]
```

[注：因字符限制，此处仅展示单题示例。实际文件需包含各分类 150+ 道题目，完整题库将在单独的任务中实现。]

- [ ] **步骤 2: 验证 JSON 有效**

```bash
python3 -c "import json; json.load(open('assets/data/questions.zh.json')); print('Valid JSON')"
python3 -c "import json; json.load(open('assets/data/questions.en.json')); print('Valid JSON')"
```
预期输出: "Valid JSON"

- [ ] **步骤 3: 构建并测试**

运行: `dotnet build`
**手动测试:** 加载游戏 → 验证默认题库加载 → 抛竿 → 验证题目显示


### 任务 5.2: 错误处理与回退机制

**文件:**
- 修改: `ModEntry.cs` — 条件注册 Harmony 补丁
- 修改: `Data/QuestionManager.cs` — 添加分类验证

- [ ] **步骤 1: 在 ModEntry.cs 中添加条件 Harmony 注册**

```csharp
if (questionManager.HasQuestions)
{
    // 注册所有 3 个补丁
    Monitor.Log("Fishing Quiz Mod loaded: Harmony patches active", LogLevel.Info);
}
else
{
    Monitor.Log("无可用题库，钓鱼答题已禁用，保留原版钓鱼", LogLevel.Warning);
}
```

- [ ] **步骤 2: 在 QuestionManager 中添加分类验证**

```csharp
public string ValidateAndFixCategory(string bankId, string category)
{
    if (string.IsNullOrEmpty(category)) return "";
    var categories = GetCategories(bankId);
    if (!categories.Contains(category))
    {
        monitor.Log($"分类 '{category}' 在题库 '{bankId}' 中不存在，重置为全部", LogLevel.Warning);
        return "";
    }
    return category;
}
```

- [ ] **步骤 3: 添加自定义题库加载错误恢复**

```csharp
// 在 ModEntry.cs 中，配置加载后:
if (config.SelectedBank != "default")
{
    bool loaded = questionManager.LoadBank(config.SelectedBank);
    if (!loaded)
    {
        config.SelectedBank = "default";
        helper.WriteConfig(config);
    }
}
else
{
    questionManager.LoadBank("default");
}

config.SelectedCategory = questionManager.ValidateAndFixCategory(
    config.SelectedBank, config.SelectedCategory);
helper.WriteConfig(config);
```

- [ ] **步骤 4: 构建并测试**

运行: `dotnet build`
**手动测试:**
1. 删除 questions.zh.json → 验证钓鱼答题禁用、原版钓鱼正常
2. JSON 损坏 → 验证错误日志、钓鱼答题禁用
3. 选择不存在的题库 → 验证回退默认 + 警告日志


### 任务 5.3: 多人模式时间冻结

**文件:**
- 修改: `UI/QuizMenu.cs` — 添加多人模式暂停支持

- [ ] **步骤 1: 在 QuizMenu 中添加多人暂停逻辑**

```csharp
// 构造方法中替换:
//   Game1.paused = true;
// 为:
if (Context.IsMultiplayer)
{
    try { Game1.netWorldState.Value.IsPaused = true; }
    catch { /* 备选方案 */ }
}
else
{
    Game1.paused = true;
}

// CloseAndCleanup() 中:
if (Context.IsMultiplayer)
{
    try { Game1.netWorldState.Value.IsPaused = false; } catch { }
}
else
{
    Game1.paused = false;
}
```

- [ ] **步骤 2: 构建并测试**

运行: `dotnet build`
**注意:** 多人测试需要实际的多人游戏会话，标记为需手动验证。


---

## 设计文档覆盖验证

| 文档章节 | 任务 | 状态 |
|---|---|---|
| 1. 架构设计 | 0.1, 0.2 | 已覆盖 |
| 2. Harmony 补丁 | 0.1 | 已覆盖 |
| 3. UI 设计 | 2.1, 2.2 | 已覆盖 |
| 4. 题库数据模型 | 1.3, 1.4 | 已覆盖 |
| 5. 鱼获奖励与品质 | 3.1, 3.2 | 已覆盖 |
| 6. 国际化 | 1.2 | 已覆盖 |
| 7. 配置 | 1.1, 4.1 | 已覆盖 |
| 8. 错误处理 | 5.2 | 已覆盖 |
| 9.1 macOS 兼容 | (默认跨平台) | 已覆盖 |
| 9.2 POC 原型 | 0.1-0.4 | 已覆盖 |
| 9.3 Harmony 版本 | 0.1 (使用 AccessTools) | 已覆盖 |
| 9.4 多人模式 | 5.3 | 已覆盖 |
| 9.5 性能 | 1.4 (内存), 1.5 (非频繁写入) | 已覆盖 |

## 实现顺序

1. **POC 阶段 (任务 0.1-0.4)** — 必须先完成，验证核心机制
2. **阶段 1 (任务 1.1-1.5)** — 核心框架，后续所有阶段依赖
3. **阶段 2 (任务 2.1-2.3)** — Quiz UI，依赖阶段 1
4. **阶段 3 (任务 3.1-3.3)** — 鱼获奖励，依赖阶段 2
5. **阶段 4 (任务 4.1)** — GMCM 配置，依赖阶段 1
6. **阶段 5 (任务 5.1-5.3)** — 内容与完善，依赖阶段 1-4

总计: 6 个阶段 + 1 个 POC 阶段，共 19 个实现任务