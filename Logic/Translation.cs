using System;
using StardewModdingAPI;

namespace fishingWithStudy.Logic
{
    public static class Translation
    {
        private static ITranslationHelper? helper;

        public static void Initialize(ITranslationHelper translationHelper)
        {
            helper = translationHelper ?? throw new ArgumentNullException(nameof(translationHelper));
        }

        public static void ResetForTests()
        {
            helper = null;
        }

        public static string Get(string key, params object[] args)
        {
            string text = helper == null ? key : helper.Get(key).ToString() ?? key;
            return args.Length > 0 ? string.Format(text, args) : text;
        }
    }
}
