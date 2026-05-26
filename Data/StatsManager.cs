using System;
using System.Collections.Generic;
using System.Linq;
using StardewModdingAPI;
using StardewValley;

namespace fishingWithStudy.Data
{
    public class StatsManager
    {
        private const string Prefix = "fishingWithStudy";

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
            var scopeStats = LoadScopeStats(scopeKey);
            scopeStats.TotalAnswers++;
            if (correct) scopeStats.CorrectAnswers++;
            SaveScopeStats(scopeKey, scopeStats);

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