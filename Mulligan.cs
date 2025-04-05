using Landfall.Haste;
using Landfall.Modding;
using System;
using UnityEngine;
using UnityEngine.Localization;
using Zorro.Settings;
namespace Mulligan;

[LandfallPlugin]
public class Mulligan
{
    public static bool mulliganEnabled = false;
    public static bool mulliganSeedEnabled = true;
    static Mulligan()
    {

        On.Player.TakeDamage += (orig, self, damage, sourceTransform, sourceName, source) =>
        {
            int threshold = GameHandler.Instance.SettingsHandler.GetSetting<MulliganThreshold>().Value - 1;
            int tempId = RunHandler.RunData.shardID;
            RunConfig tempConfig = RunHandler.config;
            int tempSeed = RunHandler.RunData.currentSeed;
            Debug.Log("Player was hit on level " + RunHandler.RunData.currentLevel);
            Debug.Log("Seed of Hit: " + tempSeed);
            if(RunHandler.RunData.currentLevel <= threshold && RunHandler.InRun && mulliganEnabled && !UI_TransitionHandler.IsTransitioning)
            {
                switch (mulliganSeedEnabled)
                {
                    case true:
                        RunHandler.LoseRun(false);
                        RunHandler.ClearCurrentRun();
                        RunHandler.StartAndPlayNewRun(tempConfig, tempId, tempSeed);
                        break;
                    case false:
                        RunHandler.LoseRun(false);
                        RunHandler.ClearCurrentRun();
                        RunHandler.StartAndPlayNewRun(tempConfig, tempId, RunHandler.GenerateSeed());
                        break;
                }
            }
            else
            {
                orig(self, damage, sourceTransform, sourceName, source);
            }
        };
        On.Player.Die += (orig, self) =>
        {
            int threshold = GameHandler.Instance.SettingsHandler.GetSetting<MulliganThreshold>().Value - 1;
            int tempId = RunHandler.RunData.shardID;
            RunConfig tempConfig = RunHandler.config;
            int tempSeed = RunHandler.RunData.currentSeed;
            Debug.Log("Player died on level " + RunHandler.RunData.currentLevel);
            Debug.Log("Seed of Death: " + tempSeed);
            if (RunHandler.RunData.currentLevel <= threshold && RunHandler.InRun && mulliganEnabled && !UI_TransitionHandler.IsTransitioning)
            {
                switch (mulliganSeedEnabled)
                {
                    case true:
                        RunHandler.LoseRun(false);
                        RunHandler.ClearCurrentRun();
                        RunHandler.StartAndPlayNewRun(tempConfig, tempId, tempSeed);
                        break;
                    case false:
                        RunHandler.LoseRun(false);
                        RunHandler.ClearCurrentRun();
                        RunHandler.StartAndPlayNewRun(tempConfig, tempId, RunHandler.GenerateSeed());
                        break;
                }
            }
            else
            {
                orig(self);
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
        public string GetCategory() => "Mods";
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
        public string GetCategory() => "Mods";
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
    public class MulliganThreshold : IntSetting, IExposedSetting
    {
        public override void ApplyValue() => Debug.Log($"Mod apply value {Value}");
        protected override int GetDefaultValue() => 1;
        public LocalizedString GetDisplayName() => new UnlocalizedString("Mulligan Level Threshold:");
        public string GetCategory() => "Mods";
    }
}