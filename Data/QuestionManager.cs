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

        private Dictionary<string, List<QuestionCategory>> loadedBanks = new();
        private List<Question> currentQuestionPool = new();
        private List<string> questionOrder = new();
        private HashSet<string> wrongSet = new();
        private int currentIndex = 0;
        private bool firstRoundDone = false;
        private string? loadedDefaultLanguage;

        public List<string> AvailableBanks { get; private set; } = new() { "default" };

        public QuestionManager(IMonitor monitor, IModHelper helper)
        {
            this.monitor = monitor;
            this.helper = helper;
        }

        public void Initialize()
        {
            // Defer language-sensitive file loading to EnsureLanguageFileLoaded(),
            // called from GameLaunched. Here we only set up the custom directory.
            string customDir = Path.Combine(helper.DirectoryPath, "assets", "data", "custom");
            if (Directory.Exists(customDir))
            {
                foreach (string file in Directory.GetFiles(customDir, "*.json"))
                {
                    AvailableBanks.Add(Path.GetFileName(file));
                }
            }
            else
            {
                Directory.CreateDirectory(customDir);
                monitor.Log("已创建自定义题库目录", LogLevel.Info);
            }
        }

        /// <summary>Load the default question file matching the game's actual language.
        /// Must be called after game is fully initialized (e.g., from GameLaunched).</summary>
        public void EnsureLanguageFileLoaded()
        {
            string lang = DetectLanguage();
            if (loadedDefaultLanguage == lang && loadedBanks.ContainsKey("default"))
            {
                return;
            }

            string defaultPath = Path.Combine(helper.DirectoryPath, "assets", "data", $"questions.{lang}.json");
            var defaultBank = LoadAndValidateBank(defaultPath, "default");
            if (defaultBank != null && defaultBank.Count > 0)
            {
                loadedBanks["default"] = defaultBank;
                loadedDefaultLanguage = lang;
                monitor.Log($"默认题库加载成功 ({lang}): {defaultBank.Sum(c => c.QuestionList.Count)} 题, {defaultBank.Count} 个分类", LogLevel.Info);
            }
            else
            {
                monitor.Log($"默认题库加载失败 ({defaultPath})", LogLevel.Error);
            }

            SetCategory("");
        }

        private string DetectLanguage()
        {
            // 1. SMAPI locale (set from game language, may be empty on macOS)
            try
            {
                string locale = helper.Translation.Locale;
                monitor.Log($"helper.Translation.Locale='{locale}'", LogLevel.Info);
                if (locale == "zh") return "zh";
            }
            catch { }

            // 2. Game content manager language
            try
            {
                var current = LocalizedContentManager.CurrentLanguageCode;
                monitor.Log($"CurrentLanguageCode={current}", LogLevel.Info);
                if (current == LocalizedContentManager.LanguageCode.zh) return "zh";
            }
            catch { }

            return "en";
        }

        private List<QuestionCategory>? LoadAndValidateBank(string filePath, string? bankIdForLog)
        {
            if (!File.Exists(filePath))
            {
                monitor.Log($"题库文件未找到: {filePath}", LogLevel.Warn);
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
                    if (cat.QuestionList == null) cat.QuestionList = new List<Question>();
                    cat.QuestionList = cat.QuestionList.Where(q =>
                    {
                        if (string.IsNullOrWhiteSpace(q.Id) || string.IsNullOrWhiteSpace(q.Type) ||
                            string.IsNullOrEmpty(q.QuestionText) ||
                            q.Options == null || q.Options.Count == 0 ||
                            q.Answer == null || q.Answer.Count == 0)
                        {
                            monitor.Log($"跳过题库 '{bankIdForLog}' 中的无效题目: 缺少必填字段", LogLevel.Warn);
                            return false;
                        }
                        if (q.Type != "single" && q.Type != "multiple")
                        {
                            monitor.Log($"跳过题目 '{q.Id}' (题库 '{bankIdForLog}'): 无效类型 '{q.Type}'", LogLevel.Warn);
                            return false;
                        }
                        if (seenIds.Contains(q.Id))
                        {
                            monitor.Log($"跳过重复题目 ID '{q.Id}' (题库 '{bankIdForLog}')", LogLevel.Warn);
                            return false;
                        }
                        seenIds.Add(q.Id);
                        return true;
                    }).ToList();
                }

                return categories.Where(c => c.QuestionList != null && c.QuestionList.Count > 0).ToList();
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
                    monitor.Log("默认题库未加载，无法切换", LogLevel.Warn);
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

            monitor.Log($"无法加载自定义题库 '{bankId}'，回退到默认", LogLevel.Warn);
            if (loadedBanks.ContainsKey("default"))
            {
                SwitchToBank("default");
                return false;
            }
            return false;
        }

        private void SwitchToBank(string bankId)
        {
            if (!loadedBanks.ContainsKey(bankId)) return;
            currentQuestionPool = loadedBanks[bankId]
                .SelectMany(c => c.QuestionList ?? new List<Question>())
                .ToList();
            ResetPool();
        }

        public void SetCategory(string category)
        {
            string currentBank = GetCurrentBankId();
            if (!loadedBanks.ContainsKey(currentBank)) return;

            if (string.IsNullOrEmpty(category))
            {
                currentQuestionPool = loadedBanks[currentBank]
                    .SelectMany(c => c.QuestionList ?? new List<Question>())
                    .ToList();
            }
            else
            {
                currentQuestionPool = loadedBanks[currentBank]
                    .Where(c => c.Category == category)
                    .SelectMany(c => c.QuestionList ?? new List<Question>())
                    .ToList();
            }
            ResetPool();
        }

        public string GetCurrentBankId()
        {
            foreach (var kvp in loadedBanks)
            {
                var pool = kvp.Value.SelectMany(c => c.QuestionList ?? new()).ToList();
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
            if (!loadedBanks.ContainsKey(bankId)) return null;
            var cat = loadedBanks[bankId].FirstOrDefault(c => c.Category == category);
            if (cat == null) return null;
            return string.IsNullOrEmpty(cat.CategoryI18n) ? category : cat.CategoryI18n;
        }

        public string ValidateAndFixCategory(string bankId, string category)
        {
            if (string.IsNullOrEmpty(category)) return "";
            var categories = GetCategories(bankId);
            if (!categories.Contains(category))
            {
                monitor.Log($"Category '{category}' not found in bank '{bankId}', resetting to all", LogLevel.Warn);
                return "";
            }
            return category;
        }

        public int TotalQuestions => currentQuestionPool.Count;
        public bool HasQuestions => currentQuestionPool.Count > 0;

        public Question? GetNextQuestion()
        {
            if (currentQuestionPool.Count == 0) return null;

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
