using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewModdingAPI.Utilities;
using HarmonyLib;
using StardewValley;
using StardewValley.Menus;
using fishingWithStudy.Data;
using System.Linq;
using System.Collections.Generic;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace fishingWithStudy
{
    public class ModEntry : Mod
    {
        internal static Config.ModConfig Config { get; private set; } = new();
        internal static QuestionManager? QuestionManager { get; private set; }
        internal static StatsManager? StatsManager { get; private set; }
        internal static IMonitor StaticMonitor { get; private set; } = null!;
        internal static string? DetectedLocale { get; set; }
        private Harmony? harmonyInstance;
        private Config.IGenericModConfigMenuApi? gmcmApi;
        private int postLaunchLanguageTicks;
        private bool postLaunchLanguageRefreshDone;
        private bool rebuildingGmcm;
        private string previousConfigBank = "default";
        private string gmcmDisplayedBank = "default";
        private string? pendingConfigBank;
        private bool applyPendingBankOnNextTick;
        private bool mouseLeftWasDown;

        public override void Entry(IModHelper helper)
        {
            Config = helper.ReadConfig<Config.ModConfig>();
            StaticMonitor = Monitor;

            var questionManager = new QuestionManager(Monitor, helper);
            questionManager.Initialize();
            QuestionManager = questionManager;

            var statsManager = new Data.StatsManager(Monitor);
            StatsManager = statsManager;

            // Language-sensitive loading and bank validation deferred to GameLaunched
            helper.Events.GameLoop.GameLaunched += this.OnGameLaunched;
            helper.Events.GameLoop.UpdateTicked += this.OnUpdateTicked;
            helper.Events.Input.ButtonPressed += this.OnButtonPressed;
        }

        private void RegisterGenericModConfigMenu()
        {
            previousConfigBank = Config.SelectedBank;
            gmcmDisplayedBank = Config.SelectedBank;
            gmcmApi = Helper.ModRegistry.GetApi<Config.IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");
            if (gmcmApi != null)
            {
                try
                {
                    rebuildingGmcm = true;
                    gmcmApi.Unregister(this.ModManifest);
                    gmcmApi.Register(
                        mod: this.ModManifest,
                        reset: () => Config = new Config.ModConfig(),
                        save: SaveGenericModConfig
                    );

                    gmcmApi.AddBoolOption(
                        mod: this.ModManifest,
                        name: () => Logic.Translation.Get("config.timer_enabled"),
                        tooltip: () => Logic.Translation.Get("config.timer_enabled_tip"),
                        getValue: () => Config.TimerEnabled,
                        setValue: (val) => Config.TimerEnabled = val,
                        fieldId: "timer_enabled"
                    );

                    gmcmApi.AddNumberOption(
                        mod: this.ModManifest,
                        name: () => Logic.Translation.Get("config.timer_seconds"),
                        tooltip: () => Logic.Translation.Get("config.timer_seconds_tip"),
                        getValue: () => Config.TimerSeconds,
                        setValue: (val) => Config.TimerSeconds = val,
                        min: 5, max: 60, interval: 1,
                        fieldId: "timer_seconds"
                    );

                    gmcmApi.AddTextOption(
                        mod: this.ModManifest,
                        name: () => Logic.Translation.Get("config.bank"),
                        tooltip: () => Logic.Translation.Get("config.bank_tip"),
                        getValue: () => gmcmDisplayedBank,
                        setValue: (val) => {
                            if (!IsFishing())
                            {
                                gmcmDisplayedBank = val;
                            }
                        },
                        allowedValues: QuestionManager?.AvailableBanks.ToArray() ?? new[] { "default" },
                        fieldId: "bank"
                    );

                    gmcmApi.AddTextOption(
                        mod: this.ModManifest,
                        name: () => Logic.Translation.Get("config.category"),
                        tooltip: () => Logic.Translation.Get("config.category_tip"),
                        getValue: () => Config.SelectedCategory,
                        setValue: (val) => Config.SelectedCategory = val,
                        allowedValues: GetCategoryConfigValues(),
                        formatAllowedValue: FormatCategoryConfigValue,
                        fieldId: "category"
                    );

                    gmcmApi.AddKeybindList(
                        mod: this.ModManifest,
                        name: () => Logic.Translation.Get("config.study_key"),
                        tooltip: () => Logic.Translation.Get("config.study_key_tip"),
                        getValue: () => KeybindList.Parse(Config.StudyModeKeybind),
                        setValue: (val) => Config.StudyModeKeybind = val.ToString(),
                        fieldId: "study_key"
                    );

                    gmcmApi.OnFieldChanged(this.ModManifest, OnGenericModConfigFieldChanged);
                    Monitor.Log("Generic Mod Config Menu registered successfully.", LogLevel.Info);
                }
                finally
                {
                    rebuildingGmcm = false;
                }
            }
            else
            {
                Monitor.Log("Generic Mod Config Menu not installed; config menu disabled.", LogLevel.Info);
            }
        }

        private void OnGenericModConfigFieldChanged(string fieldId, object value)
        {
            if (rebuildingGmcm || fieldId != "bank")
                return;

            pendingConfigBank = value?.ToString();
            gmcmDisplayedBank = string.IsNullOrEmpty(pendingConfigBank) ? Config.SelectedBank : pendingConfigBank;
            mouseLeftWasDown = Mouse.GetState().LeftButton == ButtonState.Pressed;
            Monitor.Log($"GMCM bank field changed: pendingBank={pendingConfigBank}", LogLevel.Trace);
        }

        private void ApplyPendingGenericModConfigBank()
        {
            if (gmcmApi == null || string.IsNullOrEmpty(pendingConfigBank))
                return;

            string newBank = pendingConfigBank;
            pendingConfigBank = null;

            if (newBank == Config.SelectedBank && Config.SelectedCategory == "")
                return;

            Config.SelectedBank = newBank;
            Config.SelectedCategory = "";

            if (Config.SelectedBank != "default")
                QuestionManager?.LoadBank(Config.SelectedBank);
            else
                QuestionManager?.LoadBank("default");

            previousConfigBank = Config.SelectedBank;
            Helper.WriteConfig(Config);

            Monitor.Log($"GMCM bank applied: bank={Config.SelectedBank}, category reset to all.", LogLevel.Info);
            RegisterGenericModConfigMenu();
            gmcmApi.OpenModMenu(this.ModManifest);
        }

        private void SaveGenericModConfig()
        {
            if (Config.SelectedBank != previousConfigBank)
            {
                Config.SelectedCategory = "";
                if (Config.SelectedBank != "default")
                    QuestionManager?.LoadBank(Config.SelectedBank);
                else
                    QuestionManager?.LoadBank("default");

                previousConfigBank = Config.SelectedBank;
                Monitor.Log($"GMCM bank saved: bank={Config.SelectedBank}, category reset to all.", LogLevel.Info);
            }

            Helper.WriteConfig(Config);
        }

        private static string[] GetCategoryConfigValues()
        {
            if (QuestionManager == null) return new[] { "" };

            var bankId = string.IsNullOrEmpty(Config.SelectedBank) ? "default" : Config.SelectedBank;
            var categories = QuestionManager.GetCategories(bankId)
                .Distinct()
                .OrderBy(category => category)
                .ToList();

            categories.Insert(0, "");
            return categories.ToArray();
        }

        private static string FormatCategoryConfigValue(string category)
        {
            if (string.IsNullOrEmpty(category))
                return Logic.Translation.Get("config.category_all");

            if (QuestionManager == null)
                return category;

            var bankId = string.IsNullOrEmpty(Config.SelectedBank) ? "default" : Config.SelectedBank;
            string? displayName = QuestionManager.GetCategoryI18n(bankId, category);
            if (!string.IsNullOrEmpty(displayName))
                return displayName;

            return category;
        }

        private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
        {
            if (QuestionManager == null) return;

            // Detect and store locale for use by other components
            DetectedLocale = Helper.Translation.Locale;
            Monitor.Log($"SMAPI locale: {DetectedLocale}", LogLevel.Info);

            LoadQuestionsAfterLanguageReady();
            RegisterGenericModConfigMenu();

            harmonyInstance = new Harmony(this.ModManifest.UniqueID);

            var bobberBarType = typeof(Game1).Assembly.GetType("StardewValley.Menus.BobberBar");
            Monitor.Log($"BobberBar type: {bobberBarType?.FullName ?? "null"}", LogLevel.Info);

            // Cache the BobberBar type in FishingPatcher for DelayedAction use
            Patchers.FishingPatcher.BobberBarType = bobberBarType;

            if (bobberBarType == null) return;

            var drawMethod = AccessTools.Method(bobberBarType, "draw", new Type[] { typeof(SpriteBatch) });
            Monitor.Log($"BobberBar.draw found: {drawMethod != null}", LogLevel.Info);

            var constructor = AccessTools.Constructor(bobberBarType, new Type[] {
                typeof(string), typeof(float), typeof(bool), typeof(List<string>),
                typeof(string), typeof(bool), typeof(string), typeof(bool)
            });
            Monitor.Log($"BobberBar constructor (8-param) found: {constructor != null}", LogLevel.Info);

            if (constructor != null)
                harmonyInstance.Patch(constructor,
                    prefix: new HarmonyMethod(typeof(Patchers.FishingPatcher), nameof(Patchers.FishingPatcher.BobberBar_Prefix)),
                    postfix: new HarmonyMethod(typeof(Patchers.FishingPatcher), nameof(Patchers.FishingPatcher.BobberBar_Postfix)));

            if (drawMethod != null)
                harmonyInstance.Patch(drawMethod,
                    prefix: new HarmonyMethod(typeof(Patchers.FishingPatcher), nameof(Patchers.FishingPatcher.Draw_Prefix)));

            var doFunction = AccessTools.Method(typeof(StardewValley.Tools.FishingRod), "DoFunction");
            Monitor.Log($"FishingRod.DoFunction found: {doFunction != null}", LogLevel.Info);
            if (doFunction != null)
                harmonyInstance.Patch(doFunction,
                    prefix: new HarmonyMethod(typeof(Patchers.FishingPatcher), nameof(Patchers.FishingPatcher.FishingRod_DoFunction_Prefix)),
                    postfix: new HarmonyMethod(typeof(Patchers.FishingPatcher), nameof(Patchers.FishingPatcher.FishingRod_DoFunction_Postfix)));

            Monitor.Log("Fishing Quiz Harmony patches registered successfully", LogLevel.Info);
        }

        private void OnUpdateTicked(object? sender, UpdateTickedEventArgs e)
        {
            if (postLaunchLanguageRefreshDone)
            {
                bool mouseLeftDown = Mouse.GetState().LeftButton == ButtonState.Pressed;
                if (mouseLeftWasDown && !mouseLeftDown && !string.IsNullOrEmpty(pendingConfigBank))
                    applyPendingBankOnNextTick = true;
                mouseLeftWasDown = mouseLeftDown;

                if (applyPendingBankOnNextTick)
                {
                    applyPendingBankOnNextTick = false;
                    ApplyPendingGenericModConfigBank();
                }
                return;
            }

            postLaunchLanguageTicks++;
            if (postLaunchLanguageTicks < 120)
                return;

            postLaunchLanguageRefreshDone = true;

            string beforeLocale = DetectedLocale ?? "";
            var beforeLanguage = LocalizedContentManager.CurrentLanguageCode;
            LoadQuestionsAfterLanguageReady();
            RegisterGenericModConfigMenu();
            Monitor.Log($"Post-launch language refresh complete: locale '{beforeLocale}' -> '{DetectedLocale}', gameLanguage={beforeLanguage}.", LogLevel.Info);
        }

        internal static bool EnsureDefaultQuestionsLoaded()
        {
            if (QuestionManager == null) return false;

            QuestionManager.EnsureLanguageFileLoaded();
            ApplyConfiguredQuestionSelection();
            return QuestionManager.HasQuestions;
        }

        private static void ApplyConfiguredQuestionSelection()
        {
            if (QuestionManager == null) return;

            if (Config.SelectedBank != "default")
            {
                bool bankLoaded = QuestionManager.LoadBank(Config.SelectedBank);
                if (!bankLoaded)
                    Config.SelectedBank = "default";
            }

            Config.SelectedCategory = QuestionManager.ValidateAndFixCategory(
                Config.SelectedBank, Config.SelectedCategory);
            QuestionManager.SetCategory(Config.SelectedCategory);
        }

        private void LoadQuestionsAfterLanguageReady()
        {
            if (QuestionManager == null) return;

            DetectedLocale = Helper.Translation.Locale;
            Monitor.Log($"Delayed SMAPI locale: {DetectedLocale}", LogLevel.Info);

            QuestionManager.EnsureLanguageFileLoaded();

            if (!QuestionManager.HasQuestions)
            {
                Monitor.Log("No questions available, fishing quiz disabled. Vanilla fishing preserved.", LogLevel.Warn);
                return;
            }

            ApplyConfiguredQuestionSelection();
            Helper.WriteConfig(Config);

            Monitor.Log($"Fishing Quiz Mod loaded: {QuestionManager.TotalQuestions} questions", LogLevel.Info);
        }

        private static bool IsFishing()
        {
            var bobberType = AccessTools.TypeByName("StardewValley.Menus.BobberBar");
            return Game1.activeClickableMenu?.GetType() == bobberType;
        }

        private void OnButtonPressed(object? sender, ButtonPressedEventArgs e)
        {
            if (!Context.IsWorldReady) return;

            if (e.Button.ToString() != Config.StudyModeKeybind) return;

            // Don't activate during the fishing minigame
            if (IsFishing()) return;

            // Toggle: if already in study mode QuizMenu, close it
            if (Game1.activeClickableMenu is UI.QuizMenu qm && qm.IsStudyMode)
            {
                qm.ExitQuizMenu();
                return;
            }

            // Don't open while another menu is active
            if (Game1.activeClickableMenu != null) return;

            if (!EnsureDefaultQuestionsLoaded())
            {
                Game1.addHUDMessage(new HUDMessage(Logic.Translation.Get("ui.no_bank"), 3));
                return;
            }

            var config = Config;
            string scopeKey = string.IsNullOrEmpty(config.SelectedCategory)
                ? config.SelectedBank
                : $"{config.SelectedBank}:{config.SelectedCategory}";

            Game1.activeClickableMenu = new UI.QuizMenu(
                config, QuestionManager!, StatsManager!, scopeKey, true, StaticMonitor
            );
        }
    }
}
