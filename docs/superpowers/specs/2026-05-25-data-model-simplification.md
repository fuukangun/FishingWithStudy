# 数据模型简化：移除冗余双语字典

## 概述

将题库数据模型从双语 `Dictionary<string, string>` 简化为单语言 `string`，消除按语言分文件加载后的数据冗余。

## 当前问题

`QuestionManager.Initialize()` 根据游戏语言加载不同的题库文件：
- `LocalizedContentManager.LanguageCode.zh` → `questions.zh.json`
- 其他 → `questions.en.json`

但数据模型仍保留双语结构，每个 JSON 文件同时包含 `zh` 和 `en` 两套文本。此冗余始于设计初期采用"单文件+运行时选语言"方案，后改为"按语言分文件加载"时未同步简化数据模型。

## 数据流分析

```
用户启动游戏
    ↓
QuestionManager.Initialize()
    ↓ 检测游戏语言
questions.zh.json (如果中文)
questions.en.json (如果英文)
    ↓ 反序列化到 C# 模型
Question.QuestionText 为 Dictionary<string, string>
    ↓ GetLocalizedText() 按语言 key 取值
最终显示单语言文本
```

冗余存在于：`questions.zh.json` 中的 `en` 字段和 `questions.en.json` 中的 `zh` 字段永远不会被读取，因为 `GetLocalizedText()` 的 fallback 链是 `当前语言 → "en" → ""`：

- 中文用户：取 `"zh"` key → 命中，不会 fallback 到 `"en"`
- 英文用户：取 `"en"` key → 命中，不会 fallback 到 `"zh"`（即使有 fallback 也只到 `"en"`）

## 变更方案

### 1. 数据模型 `Data/Question.cs`

| 类 | 字段 | JSON key | 变更前 | 变更后 |
|---|---|---|---|---|
| `QuestionCategory` | `CategoryI18n` | `category_i18n` | `Dictionary<string, string>` | `string` |
| `Question` | `QuestionText` | `question` | `Dictionary<string, string>` | `string` |
| `Option` | `Text` | `text` | `Dictionary<string, string>` | `string` |

### 2. 验证逻辑 `Data/QuestionManager.cs`

```csharp
// 变更前 (L83)
q.QuestionText == null || q.QuestionText.Count == 0

// 变更后
string.IsNullOrEmpty(q.QuestionText)


// 变更前 (L199)
return cat.CategoryI18n != null && cat.CategoryI18n.ContainsKey(lang)
    ? cat.CategoryI18n[lang] : category;

// 变更后
return string.IsNullOrEmpty(cat.CategoryI18n) ? category : cat.CategoryI18n;
```

### 3. 显示逻辑 `UI/QuizMenu.cs`

移除 `GetLocalizedText(Dictionary<string, string>?)` 方法（L153-160），5 处调用点直接使用字符串属性：

| 位置 | 变更前 | 变更后 |
|---|---|---|
| L190 | `GetLocalizedText(opt.Text)` | `opt.Text` |
| L241 | `GetLocalizedText(currentQuestion.QuestionText)` | `currentQuestion.QuestionText` |
| L565 | `GetLocalizedText(currentQuestion.QuestionText)` | `currentQuestion.QuestionText` |
| L586 | `GetLocalizedText(opt.Text)` | `opt.Text` |

### 4. JSON 结构变更

**`questions.zh.json`**：删除所有 `"en"` 字段，`"question"`、`"text"`、`"category_i18n"` 从对象改为字符串。

```json
{
  "category": "characters",
  "category_i18n": "角色知识",
  "questionList": [
    {
      "id": "sv_char_101",
      "type": "single",
      "question": "莉亚最喜欢的礼物是什么？",
      "options": [
        { "tag": "A", "text": "山羊奶酪" },
        { "tag": "B", "text": "沙拉" },
        { "tag": "C", "text": "虞美人" },
        { "tag": "D", "text": "宝石" }
      ],
      "answer": ["A"]
    }
  ]
}
```

**`questions.en.json`**：同理，删除 `"zh"` 字段。

## 不变的部分

- `Question.Options` 仍为 `List<Option>`
- `Question.Answer` 仍为 `List<string>`
- `QuestionCategory.Category` 仍为 `string`
- `Question.Id`、`Question.Type` 不变
- `QuestionManager.Initialize()` 语言检测逻辑不变
- JSON key 名不变（`question`、`text`、`category_i18n`）
- 其他字段的验证逻辑不变

## 影响范围

| 文件 | 改动类型 | 行数变更 |
|------|---------|---------|
| `Data/Question.cs` | 3 个字段类型声明 | 3 行 |
| `Data/QuestionManager.cs` | 2 处逻辑修改 | 2 行 |
| `UI/QuizMenu.cs` | 删除方法 + 5 处替换 | 约 -10 行 |
| `assets/data/questions.zh.json` | 全文件重写结构 | 约 453 个字段剥离 |
| `assets/data/questions.en.json` | 全文件重写结构 | 约 450 个字段剥离 |

## 回滚方案

如简化后出现问题，恢复步骤：
1. `git checkout` 5 个变更文件即可回滚
2. 无数据库迁移，纯文件替换