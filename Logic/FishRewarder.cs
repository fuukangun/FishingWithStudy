using System;
using System.Linq;
using System.Collections.Generic;
using HarmonyLib;
using StardewValley;
using StardewValley.Menus;
using StardewValley.Tools;
using fishingWithStudy.Data;

namespace fishingWithStudy.Logic
{
    public static class FishRewarder
    {
        public static object? CurrentBobber { get; set; }

        /// <summary>Item ID of the caught fish (string in SDV 1.6).</summary>
        public static string WhichFish { get; private set; } = "";
        public static float FishSize { get; private set; }
        public static bool HasTreasure { get; private set; }
        public static string SetFlagOnCatch { get; private set; } = "";
        public static bool IsBossFish { get; private set; }
        public static bool FromFishPond { get; private set; }
        public static int FishQuality { get; private set; }
        private static bool fishCaught;
        private static bool treasureCaught;
        private static bool manualTreasureOnly;
        /// <summary>Fish difficulty derived from game data (0-110). Defaults to 50 if unknown.</summary>
        public static int FishDifficulty { get; private set; } = 50;
        public static float StaminaAtBite { get; private set; }
        public static float MaxStaminaAtBite { get; private set; }
        public static float StaminaBeforeCast { get; private set; }
        public static float ActualCastCost { get; private set; }

        public static void CaptureFishData(string whichFish, float fishSize, bool treasure)
        {
            WhichFish = whichFish;
            FishSize = fishSize;
            HasTreasure = treasure;
            FishDifficulty = GetFishDifficulty(whichFish);
            SetFlagOnCatch = "";
            IsBossFish = false;
            FromFishPond = false;
            FishQuality = 0;
            fishCaught = false;
            treasureCaught = false;
            manualTreasureOnly = false;
        }

        public static void CaptureFishData(string whichFish, float fishSize, bool treasure,
            string setFlagOnCatch, bool isBossFish, bool fromFishPond = false)
        {
            CaptureFishData(whichFish, fishSize, treasure);
            SetFlagOnCatch = setFlagOnCatch;
            IsBossFish = isBossFish;
            FromFishPond = fromFishPond;
        }

        public static void CaptureBobberResultDefaults()
        {
            if (CurrentBobber == null) return;

            try
            {
                FishQuality = Traverse.Create(CurrentBobber).Field("fishQuality").GetValue<int>();
                FromFishPond = Traverse.Create(CurrentBobber).Field("fromFishPond").GetValue<bool>();
            }
            catch (Exception ex)
            {
                ModEntry.StaticMonitor.Log($"Failed to capture BobberBar defaults: {ex.Message}", StardewModdingAPI.LogLevel.Warn);
            }
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

        public static void ResetCastCost()
        {
            ActualCastCost = 0;
        }

        public static void FinalizeCastCost(float staminaAfterCast)
        {
            float cost = Math.Max(0, StaminaBeforeCast - staminaAfterCast);
            if (cost > 0)
                ActualCastCost = Math.Max(ActualCastCost, cost);
            ModEntry.StaticMonitor.Log($"Fishing cast stamina cost captured: before={StaminaBeforeCast:F1}, after={staminaAfterCast:F1}, cost={ActualCastCost:F1}", StardewModdingAPI.LogLevel.Trace);
        }

        /// <summary>Parse fish difficulty from Data/Fish asset. Falls back to 50.</summary>
        private static int GetFishDifficulty(string whichFish)
        {
            if (string.IsNullOrEmpty(whichFish)) return 50;
            try
            {
                var fishData = Game1.content.Load<Dictionary<string, string>>("Data\\Fish");
                if (fishData != null && fishData.TryGetValue(whichFish, out string? raw))
                {
                    string[] parts = raw.Split('/');
                    // SDV 1.6 format: "difficulty/motionType/..."
                    // SDV 1.5 format: "category/difficulty/..." (first field is negative int for category)
                    // Try position 0 first (SDV 1.6)
                    if (parts.Length >= 1 && int.TryParse(parts[0], out int d0) && d0 >= 0 && d0 <= 110)
                        return d0;
                    // Fallback: position 1 (SDV 1.5 format where position 0 is category)
                    if (parts.Length >= 2 && int.TryParse(parts[1], out int d1) && d1 >= 0 && d1 <= 110)
                        return d1;
                }
            }
            catch { }
            return 50;
        }

        /// <summary>SDV 1.6 legendary/mutant fish IDs (unqualified item IDs).</summary>
        private static readonly HashSet<string> LegendaryFishIds = new(StringComparer.OrdinalIgnoreCase)
        {
            "159", // Crimsonfish
            "160", // Angler
            "163", // Legend
            "682", // Glacierfish
            "775", // Mutant Carp
        };

        public static bool IsLegendaryFish()
        {
            return LegendaryFishIds.Contains(WhichFish);
        }

        public static bool IsTreasureAvailable()
        {
            return HasTreasure;
        }

        public static void SetFishCaught(bool val)
        {
            fishCaught = val;
            SetBobberCatchProgress(val ? 1f : 0f);
        }

        public static void SetDoneWithMinigame(bool val)
        {
            if (CurrentBobber == null) return;

            if (val)
                PrepareBobberForImmediateResult();
        }

        public static void SetTreasureCaught(bool val)
        {
            treasureCaught = val;
            if (CurrentBobber == null) return;
            Traverse.Create(CurrentBobber).Field("treasureCaught").SetValue(val);
        }

        public static void SetManualTreasureOnly(bool val)
        {
            manualTreasureOnly = val;
        }

        public static bool GetFishCaught()
        {
            if (CurrentBobber == null) return false;
            return fishCaught;
        }

        public static bool GetDoneWithMinigame()
        {
            if (CurrentBobber == null) return false;
            return Traverse.Create(CurrentBobber).Field("fadeOut").GetValue<bool>();
        }

        public static void FinishFishingImmediately()
        {
            if (Game1.player.CurrentTool is not FishingRod rod)
            {
                Game1.exitActiveMenu();
                return;
            }

            if (manualTreasureOnly)
            {
                PrepareRodForTreasureOnly(rod);
                rod.openTreasureMenuEndFunction(0);
                return;
            }

            if (fishCaught)
            {
                int numCaught = GetChallengeBaitFishCount();
                rod.pullFishFromWater(WhichFish, (int)FishSize, FishQuality, FishDifficulty,
                    treasureCaught, wasPerfect: false, FromFishPond, SetFlagOnCatch, IsBossFish, numCaught);
            }
            else
            {
                Game1.player.completelyStopAnimatingOrDoingAction();
                rod.doneFishing(Game1.player, consumeBaitAndTackle: true);
            }

            Game1.exitActiveMenu();
        }

        private static void PrepareRodForTreasureOnly(FishingRod rod)
        {
            var rodTraverse = Traverse.Create(rod);
            rodTraverse.Field("treasureCaught").SetValue(true);
            rodTraverse.Field("numberOfFishCaught").SetValue(1);
            rodTraverse.Field("fishSize").SetValue((int)FishSize);
            rodTraverse.Field("fishQuality").SetValue(FishQuality);
            rodTraverse.Field("fromFishPond").SetValue(FromFishPond);
        }

        private static int GetChallengeBaitFishCount()
        {
            if (CurrentBobber == null) return 1;

            try
            {
                int challengeBaitFishes = Traverse.Create(CurrentBobber).Field("challengeBaitFishes").GetValue<int>();
                return challengeBaitFishes > 0 ? challengeBaitFishes : 1;
            }
            catch
            {
                return 1;
            }
        }

        private static void SetBobberCatchProgress(float value)
        {
            if (CurrentBobber == null) return;
            Traverse.Create(CurrentBobber).Field("distanceFromCatching").SetValue(value);
        }

        private static void PrepareBobberForImmediateResult()
        {
            if (CurrentBobber == null) return;

            var bobber = Traverse.Create(CurrentBobber);
            bobber.Field("fadeOut").SetValue(true);
            bobber.Field("scale").SetValue(0f);
            bobber.Field("everythingShakeTimer").SetValue(0f);
            bobber.Field("sparkleText").SetValue(null);
        }

        // -----------------------------------------------------------------------
        // Answer result helpers (called by QuizMenu)
        // -----------------------------------------------------------------------

        /// <summary>Finalize a correct answer chain: mark fish as caught, apply quality bonus, grant XP, refund stamina.</summary>
        public static void ApplyCorrect(StatsManager statsManager, string scopeKey)
        {
            if (CurrentBobber == null) return;

            // 1. Quality bonus based on global accuracy (after 66 answers)
            var globalStats = statsManager.LoadGlobalStats();
            if (globalStats.TotalAnswers > 66)
            {
                double accuracy = globalStats.Accuracy;
                int quality = -1;
                double roll = new Random().NextDouble();

                if (accuracy > 0.80 && roll < 0.70)
                    quality = 3; // Iridium
                else if (accuracy > 0.70 && roll < 0.70)
                    quality = 2; // Gold
                else if (accuracy > 0.60 && roll < 0.70)
                    quality = 1; // Silver

                if (quality >= 0)
                {
                    FishQuality = quality;
                    Traverse.Create(CurrentBobber).Field("fishQuality").SetValue(quality);
                }
            }

            // 2. Mark fish as caught
            SetFishCaught(true);
            SetDoneWithMinigame(true);

            // 3. Grant fishing XP based on difficulty
            int baseXP = FishDifficulty switch
            {
                <= 30 => 7,
                <= 50 => 10,
                <= 70 => 13,
                _ => 15
            };
            Game1.player.gainExperience(1, baseXP);

            // 4. Refund cast stamina (clamped to max stamina)
            float refund = Math.Min(ActualCastCost,
                Game1.player.MaxStamina - Game1.player.Stamina);
            Game1.player.Stamina += refund;
            ModEntry.StaticMonitor.Log($"Fishing quiz correct: refunded stamina={refund:F1}, castCost={ActualCastCost:F1}", StardewModdingAPI.LogLevel.Info);
        }

        /// <summary>Finalize a wrong answer: fish escapes, stamina penalty for non-legendary.</summary>
        public static void ApplyWrong()
        {
            if (CurrentBobber == null) return;
            SetFishCaught(false);
            SetDoneWithMinigame(true);

            if (!IsLegendaryFish())
            {
                GiveTrashReward();

                Random rnd = new Random();
                int penalty = rnd.Next(1, 4);
                Game1.player.Stamina = Math.Max(0, Game1.player.Stamina - penalty);
            }
        }

        /// <summary>Legendary fish escapes: no stamina penalty, no catch.</summary>
        public static void ApplyLegendaryEscape()
        {
            if (CurrentBobber == null) return;
            SetFishCaught(false);
            SetDoneWithMinigame(true);
            // Legendary fish: no stamina penalty, fish just escapes
        }

        /// <summary>Timeout: fish escapes, stamina penalty for non-legendary.</summary>
        public static void ApplyTimeout()
        {
            if (CurrentBobber == null) return;
            SetFishCaught(false);
            SetDoneWithMinigame(true);
            GiveTrashReward();
        }

        /// <summary>Cancel the quiz: fish escapes immediately, with no rewards or penalties.</summary>
        public static void ApplyCancel()
        {
            if (CurrentBobber == null) return;
            manualTreasureOnly = false;
            SetTreasureCaught(false);
            SetFishCaught(false);
            SetDoneWithMinigame(true);
        }

        private static void GiveTrashReward()
        {
            Item trash = ItemRegistry.Create("(O)168", 1, 0, allowNull: false);
            Game1.player.addItemByMenuIfNecessary(trash, (item, who) => { }, forceQueue: true);
        }

        /// <summary>Refund stamina for multi-question scenarios (intermediate questions).</summary>
        public static void ApplyMultiQuestionStaminaRefund()
        {
            if (CurrentBobber == null) return;
            float refund = Math.Min(ActualCastCost,
                Game1.player.MaxStamina - Game1.player.Stamina);
            Game1.player.Stamina += refund;
        }
    }
}
