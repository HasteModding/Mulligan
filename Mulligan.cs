using Landfall.Haste; // dont think these are all necessary, might fix later
using Landfall.Modding; 
using MonoMod.RuntimeDetour;
using Steamworks;
using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.SmartFormat.Utilities;
using UnityEngine.SceneManagement;
using Zorro.Settings;
namespace Mulligan;

[LandfallPlugin]
public class Mulligan
{
    public static bool mulliganEnabled = false;
    public static bool mulliganSeedEnabled = true;
    public static bool mulliganRestart = false;
   
    public static bool mulliganOnHit = false; // different triggers
    public static bool mulliganOnDeath = false;
    public static bool mulliganOnLose = false;
    public static bool mulliganOnLanding = false;

    private static bool MulliganCheck(bool checkAgainst) // main function to check if mulligan should trigger
    {
        int threshold = GameHandler.Instance.SettingsHandler.GetSetting<MulliganThreshold>().Value - 1;
        int tempId = RunHandler.RunData.shardID;
        RunConfig tempConfig = RunHandler.config;
        int tempSeed = RunHandler.RunData.currentSeed;
        Debug.Log("Mulligan Was Triggerd On Level: " + RunHandler.RunData.currentLevel);
        Debug.Log("Seed Of Trigger: " + tempSeed);
        if (RunHandler.RunData.currentLevel <= threshold && RunHandler.InRun && !UI_TransitionHandler.IsTransitioning && mulliganEnabled && checkAgainst)
        {
            if (mulliganRestart)
            {
                UI_TransitionHandler.instance.Transition(delegate
                {
                    SceneManager.LoadScene(SceneManager.GetActiveScene().path);
                }, "Dots", 0.3f, 0.5f, 0f);
            }
            else
            {
                RunHandler.LoseRun(false);
                RunHandler.ClearCurrentRun();
                if (mulliganSeedEnabled)
                {
                    RunHandler.StartAndPlayNewRun(tempConfig, tempId, tempSeed);
                }
                else
                {
                    RunHandler.StartAndPlayNewRun(tempConfig, tempId, RunHandler.GenerateSeed());
                }
            }
            return true;
        }
        else
        {
            return false;
        }
    }

    static Mulligan()
    {
        On.Player.TakeDamage += (orig, self, damage, sourceTransform, sourceName, source) =>
        {
            if (MulliganCheck(mulliganOnHit))
            {
                return;
            }
            float tempDamage = damage; // temp value so damage multipliers aren't applied twice
            if (tempDamage < 0f)
            {
                tempDamage *= -1f;
            }
            tempDamage *= (Player.localPlayer.stats.damageMultiplier.multiplier) * (Player.localPlayer.stats.damageMultiplier.baseValue);
            tempDamage *= GameDifficulty.currentDif.damageTaken;
            if ((Player.localPlayer.data.currentHealth - tempDamage) <= 0f) //check if damage would kill player
            {
                if (MulliganCheck(mulliganOnDeath))
                {
                    Player.localPlayer.data.currentHealth = (Player.localPlayer.stats.maxHealth.multiplier) * (Player.localPlayer.stats.maxHealth.baseValue);
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
        On.Player.Die += (orig, self) => //this is for instant death
        {
            if (MulliganCheck(mulliganOnDeath))
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
            if (MulliganCheck(mulliganOnLose))
            {
                Player.localPlayer.data.lives = (int)((Player.localPlayer.stats.lives.baseValue) * (Player.localPlayer.stats.lives.multiplier));
                Player.localPlayer.data.currentHealth = (Player.localPlayer.stats.maxHealth.multiplier) * (Player.localPlayer.stats.maxHealth.baseValue);
                return;
            }
            else
            {
                orig(transitionOverride);
            }
        };
        On.Player.TriggerItemsOfType += (orig, self, type) =>
        {
            if (type == ItemTriggerType.NonPerfectLanding)
            {
                if (MulliganCheck(mulliganOnLanding)){
                    return;
                }
                else
                {
                    orig(self, type);
                }
            }
            else
            {
                orig(self, type);
            }
        };
}


    [HasteSetting]
    public class MulliganEnabledSetting : OffOnSetting, IExposedSetting
    {
        public override void ApplyValue()
        {
            Mulligan.mulliganEnabled = base.Value == OffOnMode.OFF;
        }
        public string GetCategory() => "Mulligan";
        protected override OffOnMode GetDefaultValue()
        {
            return OffOnMode.OFF;
        }
        public LocalizedString GetDisplayName() => new UnlocalizedString("Enable Mulligan?");
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
        protected override OffOnMode GetDefaultValue()
        {
            return OffOnMode.ON;
        }
        public LocalizedString GetDisplayName() => new UnlocalizedString("Use Same Seed for Mulligan?");
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
        protected override OffOnMode GetDefaultValue()
        {
            return OffOnMode.OFF;
        }
        public LocalizedString GetDisplayName() => new UnlocalizedString("Restart From Same Level?");
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
    public class MulliganThreshold : IntSetting, IExposedSetting
    {
        public override void ApplyValue() => Debug.Log($"Set Mulligan threshold to {Value}");
        protected override int GetDefaultValue() => 1;
        public LocalizedString GetDisplayName() => new UnlocalizedString("Mulligan Level Threshold:");
        public string GetCategory() => "Mulligan";
    }
    [HasteSetting]
    public class MulliganOnHitSetting : OffOnSetting, IExposedSetting
    {
        public override void ApplyValue()
        {
            Mulligan.mulliganOnHit = base.Value == OffOnMode.OFF;
        }
        public string GetCategory() => "Mulligan";
        protected override OffOnMode GetDefaultValue()
        {
            return OffOnMode.OFF;
        }
        public LocalizedString GetDisplayName() => new UnlocalizedString("Trigger Mulligan On Hit?");
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
    public class MulliganOnDeathSetting : OffOnSetting, IExposedSetting
    {
        public override void ApplyValue()
        {
            Mulligan.mulliganOnDeath = base.Value == OffOnMode.OFF;
        }
        public string GetCategory() => "Mulligan";
        protected override OffOnMode GetDefaultValue()
        {
            return OffOnMode.OFF;
        }
        public LocalizedString GetDisplayName() => new UnlocalizedString("Trigger Mulligan On Death?");
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
    public class MulliganOnLoseSetting : OffOnSetting, IExposedSetting
    {
        public override void ApplyValue()
        {
            Mulligan.mulliganOnLose = base.Value == OffOnMode.OFF;
        }
        public string GetCategory() => "Mulligan";
        protected override OffOnMode GetDefaultValue()
        {
            return OffOnMode.OFF;
        }
        public LocalizedString GetDisplayName() => new UnlocalizedString("Trigger Mulligan On Lose?");
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
    public class MulliganOnLandingSetting : OffOnSetting, IExposedSetting
    {
        public override void ApplyValue()
        {
            Mulligan.mulliganOnLanding = base.Value == OffOnMode.OFF;
        }
        public string GetCategory() => "Mulligan";
        protected override OffOnMode GetDefaultValue()
        {
            return OffOnMode.OFF;
        }
        public LocalizedString GetDisplayName() => new UnlocalizedString("Trigger Mulligan On Non-Perfect Landing?");
        public override List<LocalizedString> GetLocalizedChoices()
        {
            return new List<LocalizedString>
        {
            new LocalizedString("Settings", "EnabledGraphicOption"),
            new LocalizedString("Settings", "DisabledGraphicOption")
        };
        }
    }
}