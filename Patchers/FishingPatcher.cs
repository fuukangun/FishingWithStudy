using System;
using System.Collections.Generic;
using HarmonyLib;
using StardewValley;
using StardewValley.Tools;
using fishingWithStudy.UI;

namespace fishingWithStudy.Patchers
{
    public static class FishingPatcher
    {
        /// <summary>Cached BobberBar type, set from OnGameLaunched so we don't rely on AccessTools.TypeByName at runtime.</summary>
        internal static Type? BobberBarType { get; set; }

        public static void BobberBar_Postfix(object __instance)
        {
            Logic.FishRewarder.CurrentBobber = __instance;
            Logic.FishRewarder.CaptureBobberResultDefaults();

            Game1.delayedActions.Add(
                new DelayedAction(1, delegate
                {
                    var menu = Game1.activeClickableMenu;
                    var bobberType = BobberBarType;
                    ModEntry.StaticMonitor.Log($"QuizMenu delayed action: menu={menu?.GetType().Name}, bobberType={bobberType?.Name}, match={menu?.GetType() == bobberType}, isSame={menu != null && bobberType != null && menu.GetType() == bobberType}", StardewModdingAPI.LogLevel.Info);
                    if (menu != null && bobberType != null && menu.GetType() == bobberType)
                    {
                        var config = ModEntry.Config;
                        var qm = ModEntry.QuestionManager;
                        var sm = ModEntry.StatsManager;

                        if (qm == null || sm == null)
                        {
                            Game1.addHUDMessage(new HUDMessage("Quiz system not ready!", 3));
                            return;
                        }

                        if (!ModEntry.EnsureDefaultQuestionsLoaded())
                        {
                            Game1.addHUDMessage(new HUDMessage(Logic.Translation.Get("ui.no_bank"), 3));
                            ModEntry.StaticMonitor.Log("QuizMenu creation skipped: no questions loaded.", StardewModdingAPI.LogLevel.Warn);
                            return;
                        }

                        string bank = config.SelectedBank;
                        string cat = config.SelectedCategory;
                        string scopeKey = string.IsNullOrEmpty(cat) ? bank : $"{bank}:{cat}";

                        ModEntry.StaticMonitor.Log($"Creating QuizMenu: scopeKey={scopeKey}", StardewModdingAPI.LogLevel.Info);
                        Game1.activeClickableMenu = new QuizMenu(
                            config, qm, sm, scopeKey, false, ModEntry.StaticMonitor);
                        ModEntry.StaticMonitor.Log($"QuizMenu created, activeMenu={Game1.activeClickableMenu?.GetType().Name}", StardewModdingAPI.LogLevel.Info);
                    }
                    else
                    {
                        ModEntry.StaticMonitor.Log($"QuizMenu delayed action SKIPPED: type mismatch or null", StardewModdingAPI.LogLevel.Warn);
                    }
                })
            );
        }

        public static bool BobberBar_Prefix(
            string whichFish, float fishSize, bool treasure,
            List<string> bobbers, string setFlagOnCatch,
            bool isBossFish, string baitID, bool goldenTreasure,
            object __instance)
        {
            Logic.FishRewarder.CurrentBobber = __instance;

            Logic.FishRewarder.CaptureFishData(whichFish, fishSize, treasure, setFlagOnCatch, isBossFish);

            Logic.FishRewarder.CaptureStaminaAtBite(Game1.player.Stamina, Game1.player.MaxStamina);

            return true;
        }

        public static bool Draw_Prefix()
        {
            return false;
        }

        public static void FishingRod_DoFunction_Prefix(FishingRod __instance)
        {
            bool isFishing = Traverse.Create(__instance).Field("isFishing").GetValue<bool>();
            if (!isFishing)
                Logic.FishRewarder.ResetCastCost();

            Logic.FishRewarder.CaptureCastStamina(Game1.player.Stamina);
        }

        public static void FishingRod_DoFunction_Postfix()
        {
            Logic.FishRewarder.FinalizeCastCost(Game1.player.Stamina);
        }
    }
}
