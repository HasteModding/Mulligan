using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Localization;
using Landfall.Haste;
using Landfall.Modding;
using Zorro.Settings;
using Zorro.Core;

namespace Mulligan
{
    /// <summary>
    /// This helper encapsulates the meta progression logic that normally runs
    /// in the PostGameScreen. It awards currency and unlocks items as needed.
    /// </summary>
    public static class MetaProgressionHelper
    {
        public static void AwardMetaProgression()
        {
            // Award currency from shard completion, if any.
            int? currencyAwarded = SingletonAsset<MetaProgression>.Instance
                .GetCurrencyAwardedAfterShardComplete();

            if (currencyAwarded.HasValue)
            {
                int currencyValue = currencyAwarded.GetValueOrDefault();
                if (currencyValue > 0)
                {
                    MetaProgression.AddResource(currencyValue);
                    Debug.Log("Awarded " + currencyValue +
                        " meta progression currency.");
                }
            }
            else
            {
                Debug.LogWarning("No currency rewarded for this run.");
            }

            // Unlock items if enough meta progression resource has been gathered.
            System.Random randomInstance = RunHandler.GetCurrentLevelRandomInstance();
            while (FactSystem.GetFact(MetaProgression.MetaProgressionResourceForItemUnlock)
                   >= (float)SingletonAsset<MetaProgression>.Instance.itemEveryCurrency)
            {
                ItemInstance itemToUnlock = ItemDatabase.GetRandomItem(
                    randomInstance,
                    GetRandomItemFlags.Major | GetRandomItemFlags.IncludeLocked |
                    GetRandomItemFlags.ExcludeUnlocked
                );
                if (itemToUnlock != null)
                {
                    FactSystem.AddToFact(
                        MetaProgression.MetaProgressionResourceForItemUnlock,
                        -SingletonAsset<MetaProgression>.Instance.itemEveryCurrency
                    );
                    Debug.Log("Meta progression unlock: " + itemToUnlock.itemName);
                    FactSystem.SetFact(new Fact(itemToUnlock.itemName + "_ShowItem"), 1f);
                    itemToUnlock.IsUnlocked = true;

                    if (ItemDatabase.HasShownAllItems())
                    {
                        AchievementHandler.Unlock(Achievements.ACH_DISCOVER_ALL_ITEMS);
                    }
                }
                else
                {
                    Debug.Log("Meta progression: no more items left to unlock.");
                    break;
                }
            }

            // Save the progression changes.
            SaveSystem.Save();
        }
    }

    /// <summary>
    /// This mod enables a “Mulligan” system on various triggers.
    /// Before clearing and restarting the run, it awards meta progression rewards and
    /// updates run stats so the player gains all progression and stat updates as if the run
    /// ended normally.
    /// </summary>
    [LandfallPlugin]
    public class Mulligan
    {
        // Settings flags that control mulligan behavior.
        private static bool mulliganEnabled = false;
        private static bool mulliganSeedEnabled = true;
        private static bool mulliganRestart = false;

        private static bool mulliganOnHit = false; // trigger on hit
        private static bool mulliganOnDeath = false;
        private static bool mulliganOnLose = false;
        private static bool mulliganOnLanding = false;
        private static bool mulliganOnRank = false;

        /// <summary>
        /// Checks whether a mulligan should trigger based on the current run state
        /// and a supplied level threshold. If so, it awards meta progression rewards
        /// and updates the run stats (via HasteStats.OnRunEnd) before restarting the run.
        /// </summary>
        private static bool MulliganCheck(bool checkAgainst, int threshold)
        {
            int tempId = RunHandler.RunData.shardID;
            RunConfig tempConfig = RunHandler.config;
            int tempSeed = RunHandler.RunData.currentSeed;
            Debug.Log("Mulligan triggered on level: " +
                RunHandler.RunData.currentLevel);
            Debug.Log("Seed of trigger: " + tempSeed);

            if (RunHandler.RunData.currentLevel <= threshold &&
                RunHandler.InRun &&
                !UI_TransitionHandler.IsTransitioning &&
                mulliganEnabled &&
                checkAgainst)
            {
                // Award meta progression rewards.
                MetaProgressionHelper.AwardMetaProgression();
                // Also update run stats so the player gets relevant stat changes.
                // (Here, we pass false to indicate a non-winning run; adjust if needed.)
                HasteStats.OnRunEnd(false, RunHandler.RunData.shardID, false);

                if (mulliganRestart)
                {
                    UI_TransitionHandler.instance.Transition(() =>
                    {
                        SceneManager.LoadScene(
                            SceneManager.GetActiveScene().path);
                    }, "Dots", 0.3f, 0.5f, 0f);
                }
                else
                {
                    RunHandler.ClearCurrentRun();
                    if (mulliganSeedEnabled)
                    {
                        RunHandler.StartAndPlayNewRun(tempConfig, tempId, tempSeed);
                    }
                    else
                    {
                        RunHandler.StartAndPlayNewRun(
                            tempConfig, tempId, RunHandler.GenerateSeed());
                    }
                }
                return true;
            }
            else
            {
                return false;
            }
        }

        // Hook into various game events using MonoMod hooks.
        static Mulligan()
        {
            On.Player.TakeDamage += (orig, self, damage,
                sourceTransform, sourceName, source) =>
            {
                if (UI_TransitionHandler.IsTransitioning)
                {
                    return;
                }
                int hitThreshold = GameHandler.Instance.SettingsHandler
                    .GetSetting<MulliganHitThreshold>().Value - 1;
                if (MulliganCheck(mulliganOnHit, hitThreshold))
                {
                    return;
                }
                float tempDamage = damage;
                if (tempDamage < 0f)
                {
                    tempDamage *= -1f;
                }
                tempDamage *= (Player.localPlayer.stats.damageMultiplier.multiplier) *
                              (Player.localPlayer.stats.damageMultiplier.baseValue);
                tempDamage *= GameDifficulty.currentDif.damageTaken;
                if ((Player.localPlayer.data.currentHealth - tempDamage) <= 0f)
                {
                    int deathThreshold = GameHandler.Instance.SettingsHandler
                        .GetSetting<MulliganDeathThreshold>().Value - 1;
                    if (MulliganCheck(mulliganOnDeath, deathThreshold))
                    {
                        Player.localPlayer.data.currentHealth =
                            Player.localPlayer.stats.maxHealth.multiplier *
                            Player.localPlayer.stats.maxHealth.baseValue;
                    }
                    else
                    {
                        orig(self, damage, sourceTransform, sourceName, source);
                    }
                }
                else
                {
                    orig(self, damage, sourceTransform, sourceName, source);
                }
            };

            On.Player.Die += (orig, self) =>
            {
                int deathThreshold = GameHandler.Instance.SettingsHandler
                    .GetSetting<MulliganDeathThreshold>().Value - 1;
                if (MulliganCheck(mulliganOnDeath, deathThreshold))
                {
                    return;
                }
                else
                {
                    orig(self);
                }
            };

            On.RunHandler.LoseRun += (orig, transitionOverride) =>
            {
                int loseThreshold =
                    GameHandler.Instance.SettingsHandler
                        .GetSetting<MulliganLoseThreshold>().Value - 1;
                if (MulliganCheck(mulliganOnLose, loseThreshold))
                {
                    // When mulliganing on lose, ensure the player is fully healed and
                    // lives are restored.
                    Player.localPlayer.data.lives =
                        (int)(Player.localPlayer.stats.lives.baseValue *
                        Player.localPlayer.stats.lives.multiplier);
                    Player.localPlayer.data.currentHealth =
                        Player.localPlayer.stats.maxHealth.multiplier *
                        Player.localPlayer.stats.maxHealth.baseValue;
                    return;
                }
                else
                {
                    orig(transitionOverride);
                }
            };

            On.PlayerMovement.Land += (orig, self, landing) =>
            {
                if (UI_TransitionHandler.IsTransitioning)
                {
                    return;
                }
                float landVal = (float)landing.GetType()
                    .GetField("landingScore")
                    .GetValue(landing);
                Debug.Log(landVal);
                if (landVal < 0.95f)
                {
                    int landingThreshold =
                        GameHandler.Instance.SettingsHandler
                            .GetSetting<MulliganLandingThreshold>().Value - 1;
                    if (MulliganCheck(mulliganOnLanding, landingThreshold))
                    {
                        return;
                    }
                    else
                    {
                        orig(self, landing);
                    }
                }
                else
                {
                    orig(self, landing);
                }
            };

            On.GM_Run.TransitionToLevelSelect += (orig, self) =>
            {
                if (RunHandler.currentTier != 0)
                {
                    int rankThreshold = GameHandler.Instance.SettingsHandler
                        .GetSetting<MulliganRankThreshold>().Value;
                    if (MulliganCheck(mulliganOnRank, rankThreshold))
                    {
                        return;
                    }
                    else
                    {
                        orig(self);
                    }
                }
                else
                {
                    orig(self);
                }
            };

            On.EscapeMenuMainPage.OnAbandonButtonClicked += (orig, self) =>
            {
                // Override the abandon button to simulate run completion.
                MethodInfo compRun = typeof(RunHandler)
                    .GetMethod("CompleteRun",
                        BindingFlags.NonPublic | BindingFlags.Static);
                object[] parameters = new object[] { false, false };
                compRun.Invoke(null, parameters);
                Singleton<EscapeMenu>.Instance.Close();
            };
        }

        // ===== Settings Classes =====

        [HasteSetting]
        public class MulliganEnabledSetting : OffOnSetting, IExposedSetting
        {
            public override void ApplyValue()
            {
                Mulligan.mulliganEnabled = base.Value == OffOnMode.OFF;
            }
            public string GetCategory() => "Mulligan";
            protected override OffOnMode GetDefaultValue() => OffOnMode.OFF;
            public LocalizedString GetDisplayName() =>
                new UnlocalizedString("Enable Mulligan?");
            public override List<LocalizedString> GetLocalizedChoices()
            {
                return new List<LocalizedString>
                {
                    new LocalizedString("Settings", "EnabledGraphicOption"),
                    new LocalizedString("Settings", "DisabledGraphicOption")
                };
            }
        }

        [HasteSetting]
        public class MulliganSeedSetting : OffOnSetting, IExposedSetting
        {
            public override void ApplyValue()
            {
                Mulligan.mulliganSeedEnabled = base.Value == OffOnMode.ON;
            }
            public string GetCategory() => "Mulligan";
            protected override OffOnMode GetDefaultValue() => OffOnMode.ON;
            public LocalizedString GetDisplayName() =>
                new UnlocalizedString("Use Same Seed for Mulligan?");
            public override List<LocalizedString> GetLocalizedChoices()
            {
                return new List<LocalizedString>
                {
                    new LocalizedString("Settings", "DisabledGraphicOption"),
                    new LocalizedString("Settings", "EnabledGraphicOption")
                };
            }
        }

        [HasteSetting]
        public class MulliganRestartSetting : OffOnSetting, IExposedSetting
        {
            public override void ApplyValue()
            {
                Mulligan.mulliganRestart = base.Value == OffOnMode.OFF;
            }
            public string GetCategory() => "Mulligan";
            protected override OffOnMode GetDefaultValue() => OffOnMode.OFF;
            public LocalizedString GetDisplayName() =>
                new UnlocalizedString("Restart From Same Level?");
            public override List<LocalizedString> GetLocalizedChoices()
            {
                return new List<LocalizedString>
                {
                    new LocalizedString("Settings", "EnabledGraphicOption"),
                    new LocalizedString("Settings", "DisabledGraphicOption")
                };
            }
        }

        [HasteSetting]
        public class MulliganOnHitSetting : OffOnSetting, IExposedSetting
        {
            public override void ApplyValue()
            {
                Mulligan.mulliganOnHit = base.Value == OffOnMode.OFF;
            }
            public string GetCategory() => "Mulligan";
            protected override OffOnMode GetDefaultValue() => OffOnMode.OFF;
            public LocalizedString GetDisplayName() =>
                new UnlocalizedString("Trigger Mulligan On Hit?");
            public override List<LocalizedString> GetLocalizedChoices()
            {
                return new List<LocalizedString>
                {
                    new LocalizedString("Settings", "EnabledGraphicOption"),
                    new LocalizedString("Settings", "DisabledGraphicOption")
                };
            }
        }

        [HasteSetting]
        public class MulliganHitThreshold : IntSetting, IExposedSetting
        {
            public override void ApplyValue() =>
                Debug.Log($"Set Mulligan threshold to {Value}");
            protected override int GetDefaultValue() => 1;
            public LocalizedString GetDisplayName() =>
                new UnlocalizedString("On Hit Level Threshold:");
            public string GetCategory() => "Mulligan";
        }

        [HasteSetting]
        public class MulliganOnDeathSetting : OffOnSetting, IExposedSetting
        {
            public override void ApplyValue()
            {
                Mulligan.mulliganOnDeath = base.Value == OffOnMode.OFF;
            }
            public string GetCategory() => "Mulligan";
            protected override OffOnMode GetDefaultValue() => OffOnMode.OFF;
            public LocalizedString GetDisplayName() =>
                new UnlocalizedString("Trigger Mulligan On Death?");
            public override List<LocalizedString> GetLocalizedChoices()
            {
                return new List<LocalizedString>
                {
                    new LocalizedString("Settings", "EnabledGraphicOption"),
                    new LocalizedString("Settings", "DisabledGraphicOption")
                };
            }
        }

        [HasteSetting]
        public class MulliganDeathThreshold : IntSetting, IExposedSetting
        {
            public override void ApplyValue() =>
                Debug.Log($"Set Mulligan threshold to {Value}");
            protected override int GetDefaultValue() => 1;
            public LocalizedString GetDisplayName() =>
                new UnlocalizedString("On Death Level Threshold:");
            public string GetCategory() => "Mulligan";
        }

        [HasteSetting]
        public class MulliganOnLoseSetting : OffOnSetting, IExposedSetting
        {
            public override void ApplyValue()
            {
                Mulligan.mulliganOnLose = base.Value == OffOnMode.OFF;
            }
            public string GetCategory() => "Mulligan";
            protected override OffOnMode GetDefaultValue() => OffOnMode.OFF;
            public LocalizedString GetDisplayName() =>
                new UnlocalizedString("Trigger Mulligan On Lose?");
            public override List<LocalizedString> GetLocalizedChoices()
            {
                return new List<LocalizedString>
                {
                    new LocalizedString("Settings", "EnabledGraphicOption"),
                    new LocalizedString("Settings", "DisabledGraphicOption")
                };
            }
        }

        [HasteSetting]
        public class MulliganLoseThreshold : IntSetting, IExposedSetting
        {
            public override void ApplyValue() =>
                Debug.Log($"Set Mulligan threshold to {Value}");
            protected override int GetDefaultValue() => 1;
            public LocalizedString GetDisplayName() =>
                new UnlocalizedString("On Lose Level Threshold:");
            public string GetCategory() => "Mulligan";
        }

        [HasteSetting]
        public class MulliganOnLandingSetting : OffOnSetting, IExposedSetting
        {
            public override void ApplyValue()
            {
                Mulligan.mulliganOnLanding = base.Value == OffOnMode.OFF;
            }
            public string GetCategory() => "Mulligan";
            protected override OffOnMode GetDefaultValue() => OffOnMode.OFF;
            public LocalizedString GetDisplayName() =>
                new UnlocalizedString("Trigger Mulligan On Non-Perfect Landing?");
            public override List<LocalizedString> GetLocalizedChoices()
            {
                return new List<LocalizedString>
                {
                    new LocalizedString("Settings", "EnabledGraphicOption"),
                    new LocalizedString("Settings", "DisabledGraphicOption")
                };
            }
        }

        [HasteSetting]
        public class MulliganLandingThreshold : IntSetting, IExposedSetting
        {
            public override void ApplyValue() =>
                Debug.Log($"Set Mulligan threshold to {Value}");
            protected override int GetDefaultValue() => 1;
            public LocalizedString GetDisplayName() =>
                new UnlocalizedString("On Non-Perfect Landing Level Threshold:");
            public string GetCategory() => "Mulligan";
        }

        [HasteSetting]
        public class MulliganOnRankSetting : OffOnSetting, IExposedSetting
        {
            public override void ApplyValue()
            {
                Mulligan.mulliganOnRank = base.Value == OffOnMode.OFF;
            }
            public string GetCategory() => "Mulligan";
            protected override OffOnMode GetDefaultValue() => OffOnMode.OFF;
            public LocalizedString GetDisplayName() =>
                new UnlocalizedString("Trigger Mulligan On Non-S Rank?");
            public override List<LocalizedString> GetLocalizedChoices()
            {
                return new List<LocalizedString>
                {
                    new LocalizedString("Settings", "EnabledGraphicOption"),
                    new LocalizedString("Settings", "DisabledGraphicOption")
                };
            }
        }

        [HasteSetting]
        public class MulliganRankThreshold : IntSetting, IExposedSetting
        {
            public override void ApplyValue() =>
                Debug.Log($"Set Mulligan threshold to {Value}");
            protected override int GetDefaultValue() => 1;
            public LocalizedString GetDisplayName() =>
                new UnlocalizedString("On Non-S Rank Threshold:");
            public string GetCategory() => "Mulligan";
        }
    }
}
