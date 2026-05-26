# 执行计划：数据模型简化

**关联规格文档:** `docs/superpowers/specs/2026-05-25-data-model-simplification.md`

## 执行步骤

### 步骤 1: 修改 `Data/Question.cs`

将 `CategoryI18n`、`QuestionText`、`Text` 从 `Dictionary<string, string>` 改为 `string`。

### 步骤 2: 修改 `Data/QuestionManager.cs`

- L83: `q.QuestionText == null || q.QuestionText.Count == 0` → `string.IsNullOrEmpty(q.QuestionText)`
- L199: 删除语言检测逻辑，直接返回 `cat.CategoryI18n`（空时返回 `category`）

### 步骤 3: 修改 `UI/QuizMenu.cs`

- 删除 `GetLocalizedText()` 方法（L153-160）
- 替换全部 5 处 `GetLocalizedText(...)` 调用为直接属性访问

### 步骤 4: 清理 JSON 文件

用 Python 脚本批量处理两个 JSON 文件：
- `questions.zh.json`：将 `{"zh": "文本", "en": "text"}` 替换为 `"文本"`
- `questions.en.json`：将 `{"zh": "文本", "en": "text"}` 替换为 `"text"`

### 步骤 5: 编译验证

`dotnet build` 确认 0 错误。