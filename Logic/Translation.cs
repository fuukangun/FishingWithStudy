using System.Collections.Generic;
using StardewValley;

namespace fishingWithStudy.Logic
{
    public static class Translation
    {
        private static bool IsChinese
        {
            get
            {
                if (ModEntry.DetectedLocale == "zh") return true;
                if (LocalizedContentManager.CurrentLanguageCode == LocalizedContentManager.LanguageCode.zh) return true;
                return false;
            }
        }

        private static readonly Dictionary<string, string> zh = new()
        {
            { "ui.title", "钓鱼答题" },
            { "ui.confirm", "确认" },
            { "ui.submit", "确认" },
            { "ui.correct", "正确！" },
            { "ui.wrong", "错误！" },
            { "ui.answer_is", "正确答案：{0}" },
            { "ui.caught", "获得：{0}" },
            { "ui.quality_up", "品质提升！" },
            { "ui.timeout", "时间到！" },
            { "ui.fish_got_away", "鱼跑掉了！" },
            { "ui.multiple_hint", "(可多选)" },
            { "ui.type_single", "单选" },
            { "ui.type_multiple", "多选" },
            { "ui.category", "{0}" },
            { "ui.study_mode_title", "学习模式" },
            { "ui.stamina_penalty", "体力 -{0}" },
            { "ui.treasure_acquired", "获得宝箱！" },
            { "ui.next_in", "下一题 {0}秒" },
            { "ui.continue", "继续..." },
            { "ui.complete", "完成！" },
            { "ui.continue_correct", "答对 {0} 题后失败，鱼跑了！" },
            { "ui.no_bank", "未加载题库，无法开始钓鱼答题" },
            { "config.timer_enabled", "启用倒计时" },
            { "config.timer_enabled_tip", "答题时是否显示倒计时" },
            { "config.timer_seconds", "倒计时秒数" },
            { "config.timer_seconds_tip", "每题可用的答题时间 (5-60秒)" },
            { "config.bank", "题库" },
            { "config.bank_tip", "选择当前使用的题库" },
            { "config.category", "分类" },
            { "config.category_all", "全部" },
            { "config.category_tip", "选择题目分类（空=全部）" },
            { "config.study_key", "学习模式快捷键" },
            { "config.study_key_tip", "按此键打开/关闭学习模式" },
        };

        private static readonly Dictionary<string, string> en = new()
        {
            { "ui.title", "Fishing Quiz" },
            { "ui.confirm", "Confirm" },
            { "ui.submit", "Confirm" },
            { "ui.correct", "Correct!" },
            { "ui.wrong", "Wrong!" },
            { "ui.answer_is", "Answer: {0}" },
            { "ui.caught", "Caught: {0}" },
            { "ui.quality_up", "Quality Up!" },
            { "ui.timeout", "Time's Up!" },
            { "ui.fish_got_away", "The fish got away!" },
            { "ui.multiple_hint", "(Multiple)" },
            { "ui.type_single", "Single" },
            { "ui.type_multiple", "Multiple" },
            { "ui.category", "{0}" },
            { "ui.study_mode_title", "Study Mode" },
            { "ui.stamina_penalty", "Stamina -{0}" },
            { "ui.treasure_acquired", "Treasure Acquired!" },
            { "ui.next_in", "Next in {0}s" },
            { "ui.continue", "Continue..." },
            { "ui.complete", "Complete!" },
            { "ui.continue_correct", "Failed after {0} correct! Fish got away!" },
            { "ui.no_bank", "No question bank loaded, cannot start fishing quiz" },
            { "config.timer_enabled", "Enable Timer" },
            { "config.timer_enabled_tip", "Show countdown timer during quiz" },
            { "config.timer_seconds", "Timer Seconds" },
            { "config.timer_seconds_tip", "Time per question (5-60 seconds)" },
            { "config.bank", "Question Bank" },
            { "config.bank_tip", "Select the question bank to use" },
            { "config.category", "Category" },
            { "config.category_all", "All" },
            { "config.category_tip", "Filter questions by category (empty=all)" },
            { "config.study_key", "Study Mode Key" },
            { "config.study_key_tip", "Press this key to open/close study mode" },
        };

        public static string Get(string key, params object[] args)
        {
            var dict = IsChinese ? zh : en;
            string format = dict.ContainsKey(key) ? dict[key] : key;
            return args.Length > 0 ? string.Format(format, args) : format;
        }
    }
}
