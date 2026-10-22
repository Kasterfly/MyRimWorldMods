using HarmonyLib;
using Kasterfly.CombatTweaks;
using Kasterfly.CombatTweaks.Comps;
using Kasterfly.CombatTweaks.MainFunctions;
using Kasterfly.SettingsBuilder;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Kasterfly.CombatTweaks.HarmonyPatches;
using UnityEngine;
using Verse;
using static Kasterfly.CombatTweaks.CombatTweaksModSettings;

namespace Kasterfly.CombatTweaks
{
    public class CombatTweaksMod : Mod
    {
        public static CombatTweaksModSettings Settings;
        private static SettingsGUI _settingsGui = new SettingsGUI();

        public CombatTweaksMod(ModContentPack content) : base(content)
        {
            var harmony = new Harmony("kasterfly.combatTweaks");
            harmony.PatchAll();
            Settings = GetSettings<CombatTweaksModSettings>();

            if (LoadedModManager.RunningModsListForReading.Any(m => m.PackageId.ToLower().Equals("mlie.yayoscombat3")))
                Settings.yayoCombatCompat = false;

            InitSettings();
            YayoCombat3Compat.DoYayoUnpatch();
            Log.Message($"[CombatTweaks] CombatTweaks (v1.4.0) for RimWorld {VersionSpecific.GameVersion}");
        }

        public override string SettingsCategory() => "Combat Tweaks";

        public override void DoSettingsWindowContents(Rect inRect)
        {
            _settingsGui.DrawSettings(inRect);
        }

        private void InitSettings()
        {
            _settingsGui.AddCategory("General");
            _settingsGui.AddItem("debugLogging", "debugLogging", "General",
                _settingsGui.CreateCheckbox(() => Settings.debugLogging, val => Settings.debugLogging = val,
                    "Enable Debug Logging"));

            _settingsGui.AddItem("showDetails", "showDetails", "General",
                _settingsGui.CreateCheckbox(() => Settings.showDetails, val => Settings.showDetails = val,
                    "Show Settings Explanations"));

            _settingsGui.AddItem("doTracking", "doTracking", "General",
                _settingsGui.CreateCheckbox(() => Settings.doTracking, v => Settings.doTracking = v,
                    "Tracking info in stats"));

            _settingsGui.AddItem("difficultyBar", "difficultyBar", "General", rect =>
            {
                float difficulty = Settings.CalculateDifficultyScore();
                DrawDifficultyBar(rect, difficulty);
            });

            _settingsGui.AddCategory("Compatibility");
            _settingsGui.AddItem("yayoCompat", "yayoCompat", "Compatibility",
                _settingsGui.CreateInfo(() =>
                    $"For Yayo's Combat 3 Compatibility:\n - This mod must be placed before Yayo's Combat 3 in your modlist"),
                90f);

            _settingsGui.AddItem("yayoCombatCompatEnableAAA", "yayoCombatCompatEnableAAA", "Compatibility",
                _settingsGui.CreateCheckbox(() => Settings.yayoCombatCompatEnableAAA,
                    val => Settings.yayoCombatCompatEnableAAA = val,
                    "Enable Yayo's \"Advanced Armor Algorithms\" Compatibility"),
                35f,
                new List<string> { "yayoArmorSlider" });

            _settingsGui.AddItem("yayoArmorSlider", "yayoArmorSlider", "Compatibility",
                _settingsGui.CreateSlider(
                    () => Settings.yayoCombatArmorEffectiveness,
                    val => Settings.yayoCombatArmorEffectiveness = val,
                    0f, 1f,
                    () =>
                        $"Yayo's Improved Defence Option Overwrite Value: {(Settings.yayoCombatArmorEffectiveness * 100f):F0}%",
                    0.01f),
                60f,
                new List<string> { "yayoCombatCompatEnableAAA" });
            _settingsGui.AddConditional("yayoArmorSlider", () => Settings.yayoCombatCompatEnableAAA);

            _settingsGui.AddItem(
                "vanillaCombatReloadedCompatibility",
                "vanillaCombatReloadedCompatibility",
                "Compatibility",
                _settingsGui.CreateCheckbox(
                    () => Settings.vanillaCombatReloadedCompatibility,
                    val =>
                    {
                        if (Settings.vanillaCombatReloadedCompatibility != val)
                        {
                            Settings.vanillaCombatReloadedCompatibility = val;
                            VanillaCombatReloadedCompat.RefreshIfNeeded();
                        }
                    },
                    "Enable Vanilla Combat Reloaded's \"Advanced Armor Behavior\" Compatibility"
                )
            );

            Action<Rect> VcoCheckbox(Func<bool> getter, Action<bool> setter, string label) =>
                _settingsGui.CreateCheckbox(getter, val =>
                {
                    if (getter() != val)
                    {
                        setter(val);
                        VanillaCombatOverhaulCompat.OnSettingsChanged();
                    }
                }, label);
            var vcoMasterKey = new List<string> { "vanillaCombatOverhaulCompatibility" };
            var vcoOptionKeys = new List<string>
                { "vcoUseAdvancedArmor", "vcoUseProjectileWounds", "vcoUseMissSpread", "vcoUseFireModeBursts", "vcoScaleFireModeDistances" };

            _settingsGui.AddItem("vanillaCombatOverhaulCompatibility", "vanillaCombatOverhaulCompatibility", "Compatibility",
                VcoCheckbox(() => Settings.vanillaCombatOverhaulCompatibility, val => Settings.vanillaCombatOverhaulCompatibility = val,
                    "Enable Vanilla Combat Overhaul Compatibility" + (VanillaCombatOverhaulCompat.present ? "" : " (not loaded)")),
                35f, vcoOptionKeys);
            _settingsGui.AddItem("vcoUseAdvancedArmor", "vcoUseAdvancedArmor", "Compatibility",
                VcoCheckbox(() => Settings.vcoUseAdvancedArmor, val => Settings.vcoUseAdvancedArmor = val,
                    "Use VCO's \"Advanced Armor\" Scaling"),
                35f, vcoMasterKey);
            _settingsGui.AddItem("vcoUseProjectileWounds", "vcoUseProjectileWounds", "Compatibility",
                VcoCheckbox(() => Settings.vcoUseProjectileWounds, val => Settings.vcoUseProjectileWounds = val,
                    "Use VCO's Bullet and Arrow Wounds"),
                35f, vcoMasterKey);
            _settingsGui.AddItem("vcoUseMissSpread", "vcoUseMissSpread", "Compatibility",
                VcoCheckbox(() => Settings.vcoUseMissSpread, val => Settings.vcoUseMissSpread = val,
                    "Use VCO's Missed Shot Spread (instead of Projectile Spread)"),
                35f, vcoMasterKey);
            _settingsGui.AddItem("vcoUseFireModeBursts", "vcoUseFireModeBursts", "Compatibility",
                VcoCheckbox(() => Settings.vcoUseFireModeBursts, val => Settings.vcoUseFireModeBursts = val,
                    "Use VCO's Fire Mode Burst Sizes (instead of Burst Size)"),
                35f, vcoMasterKey);
            _settingsGui.AddItem("vcoScaleFireModeDistances", "vcoScaleFireModeDistances", "Compatibility",
                VcoCheckbox(() => Settings.vcoScaleFireModeDistances, val => Settings.vcoScaleFireModeDistances = val,
                    "Scale VCO's Fire Mode Distances with Weapon Range"),
                35f, vcoMasterKey);
            foreach (string key in vcoOptionKeys)
                _settingsGui.AddConditional(key, () => Settings.vanillaCombatOverhaulCompatibility);

            _settingsGui.AddItem("respectNonLethalDamage", "respectNonLethalDamage", "Compatibility",
                _settingsGui.CreateCheckbox(() => Settings.respectNonLethalDamage,
                    val => Settings.respectNonLethalDamage = val,
                    "Try to Respect Non-Lethal Damage"));

            _settingsGui.AddCategory("Features");
            _settingsGui.AddItem("allArmorEffectsEverywhere", "allArmorEffectsEverywhere", "Features",
                _settingsGui.CreateCheckbox(() => Settings.allArmorEffectsEverywhere,
                    v => Settings.allArmorEffectsEverywhere = v, "All Equipped Armor Protects All Body Parts"));

            _settingsGui.AddItem("ignoreWeakArmor", "ignoreWeakArmor", "Features",
                _settingsGui.CreateCheckbox(() => Settings.ignoreWeakArmor, v => Settings.ignoreWeakArmor = v,
                    "That's not armor!"));

            _settingsGui.AddItem("ignoreWeakArmorStillTakeDurabilityDamage", "ignoreWeakArmorStillTakeDurabilityDamage",
                "Features",
                _settingsGui.CreateCheckbox(() => Settings.ignoreWeakArmorStillTakeDurabilityDamage,
                    v => Settings.ignoreWeakArmorStillTakeDurabilityDamage = v,
                    "Non-Armor Still Takes Durability Damage From Combat"),
                35f,
                new List<string> { "ignoreWeakArmor" });
            _settingsGui.AddConditional("ignoreWeakArmorStillTakeDurabilityDamage", () => Settings.ignoreWeakArmor);

            _settingsGui.AddItem("ignoreWeakArmorThreshold", "ignoreWeakArmorThreshold", "Features",
                _settingsGui.CreateSlider(
                    () => Settings.ignoreWeakArmorThreshold,
                    v => Settings.ignoreWeakArmorThreshold = v,
                    0f, 2f,
                    () =>
                        $"If armor % is less than this than it's not counted as armor: {(Settings.ignoreWeakArmorThreshold * 100f):F0}%",
                    0.01f),
                60f,
                new List<string> { "ignoreWeakArmor" });
            _settingsGui.AddConditional("ignoreWeakArmorThreshold", () => Settings.ignoreWeakArmor);

            _settingsGui.AddItem("durabilityEffectsArmor", "durabilityEffectsArmor", "Features",
                _settingsGui.CreateCheckbox(() => Settings.durabilityEffectsArmor,
                    v => Settings.durabilityEffectsArmor = v, "Durability Affects Armor"));

            _settingsGui.AddItem("durabilityEffectsArmorMultiplier", "durabilityEffectsArmorMultiplier", "Features",
                _settingsGui.CreateSlider(
                    () => Settings.durabilityEffectsArmorMultiplier,
                    v => Settings.durabilityEffectsArmorMultiplier = v,
                    0f, 0.05f,
                    () =>
                        $"Armor effectiveness lost per 1% durability missing: {(Settings.durabilityEffectsArmorMultiplier * 100f):F1}%",
                    0.001f),
                35f,
                new List<string> { "durabilityEffectsArmor" });
            _settingsGui.AddConditional("durabilityEffectsArmorMultiplier", () => Settings.durabilityEffectsArmor);

            _settingsGui.AddItem("durabilityEffectsArmorDoTattered", "durabilityEffectsArmorDoTattered", "Features",
                _settingsGui.CreateCheckbox(
                    () => Settings.durabilityEffectsArmorDoTattered,
                    v => Settings.durabilityEffectsArmorDoTattered = v,
                    "Use Tattered Effect"),
                35f,
                new List<string> { "durabilityEffectsArmor" });
            _settingsGui.AddConditional("durabilityEffectsArmorDoTattered", () => Settings.durabilityEffectsArmor);

            _settingsGui.AddItem("doDamageEffect", "doDamageEffect", "Features",
                _settingsGui.CreateCheckbox(() => Settings.doDamageEffect, v => Settings.doDamageEffect = v,
                    "Large Damage Scaling"));

            _settingsGui.AddItem("reliableArmors", "reliableArmors", "Features",
                _settingsGui.CreateCheckbox(() => Settings.reliableArmors, v => Settings.reliableArmors = v,
                    "Reliable Armors Mod's Damage Reductions"));

            _settingsGui.AddItem("thickArmor", "thickArmor", "Features",
                _settingsGui.CreateCheckbox(() => Settings.thickArmor, v => Settings.thickArmor = v,
                    "Thick Armor Mod's Layered Armor Calculations"));

            _settingsGui.AddItem("thickArmorEffectivenessLoss", "thickArmorEffectivenessLoss", "Features",
                _settingsGui.CreateSlider(
                    () => Settings.thickArmorEffectivenessLoss,
                    v => Settings.thickArmorEffectivenessLoss = v,
                    0f, 1f,
                    () => $"Effectiveness Loss Per Layer: {(Settings.thickArmorEffectivenessLoss * 100f):F0}%",
                    0.01f),
                60f,
                new List<string> { "thickArmor" });
            _settingsGui.AddConditional("thickArmorEffectivenessLoss", () => Settings.thickArmor);

            _settingsGui.AddItem("alwaysReduceDamage", "alwaysReduceDamage", "Features",
                _settingsGui.CreateCheckbox(() => Settings.alwaysReduceDamage, v => Settings.alwaysReduceDamage = v,
                    "Almost Always Reduce Damage"));
            _settingsGui.AddItem("doAdrenaline", "doAdrenaline", "Features",
                _settingsGui.CreateCheckbox(() => Settings.doAdrenaline, v => Settings.doAdrenaline = v,
                    "Use Adrenaline System"),
                35f,
                new List<string> { "doAdrenaline" });

            _settingsGui.AddItem("adrenalineChance", "adrenalineChance", "Features",
                _settingsGui.CreateSlider(() => Settings.adrenalineChance, v => Settings.adrenalineChance = v, 0f, 1f,
                    () => $"Adrenaline trigger chance: {(Settings.adrenalineChance * 100f):F0}%", 0.01f),
                60f,
                new List<string> { "doAdrenaline" });
            _settingsGui.AddConditional("adrenalineChance", () => Settings.doAdrenaline);


            _settingsGui.AddItem("doTechLevelEffect", "doTechLevelEffect", "Features",
                _settingsGui.CreateCheckbox(() => Settings.doTechLevelEffect, v => Settings.doTechLevelEffect = v,
                    "Tech Level Scaling"),
                35f,
                new List<string> { "techLevelBoost" });

            _settingsGui.AddItem("techLevelBoost", "techLevelBoost", "Features",
                _settingsGui.CreateSlider(() => Settings.techLevelBoost, v => Settings.techLevelBoost = v, 0f, 2f,
                    () => $"Buff per Tech Level: {FormatBuff(Settings.techLevelBoost, 0f)}", 0.05f),
                60f,
                new List<string> { "doTechLevelEffect" });
            _settingsGui.AddConditional("techLevelBoost", () => Settings.doTechLevelEffect);

            _settingsGui.AddItem("durabilityTypeInteraction", "durabilityTypeInteraction", "Features",
                _settingsGui.CreateCheckbox(() => Settings.durabilityTypeInteraction,
                    v => Settings.durabilityTypeInteraction = v, "Durability Type Interaction"),
                35f,
                new List<string> { "durabilityTypeInteractionMultiplier" });

            _settingsGui.AddItem("durabilityTypeInteractionMultiplier", "durabilityTypeInteractionMultiplier",
                "Features",
                _settingsGui.CreateSlider(() => Settings.durabilityTypeInteractionMultiplier,
                    v => Settings.durabilityTypeInteractionMultiplier = v, 0f, 4f,
                    () =>
                        $"Durability Damage Weakness Multiplier: {(Settings.durabilityTypeInteractionMultiplier * 100f):F0}%",
                    0.05f),
                60f,
                new List<string> { "durabilityTypeInteraction" });
            _settingsGui.AddConditional("durabilityTypeInteractionMultiplier", () => Settings.durabilityTypeInteraction);

            _settingsGui.AddItem("armorDeflectionRework", "armorDeflectionRework", "Features",
                _settingsGui.CreateCheckbox(() => Settings.armorDeflectionRework,
                    v => Settings.armorDeflectionRework = v, "Armor Deflection Change (Rework)"),
                55f,
                new List<string> { "armorDeflectionReworkThreshold" });

            _settingsGui.AddItem("armorTiersRework", "armorTiersRework", "Features",
                _settingsGui.CreateCheckbox(() => Settings.armorTiers, v => Settings.armorTiers = v,
                    "Armor Tiers (Rework)"),
                35f,
                new List<string> { "armorTiersRework" });

            _settingsGui.AddItem("armorDeflectionReworkThreshold", "armorDeflectionReworkThreshold", "Features",
                _settingsGui.CreateSlider(() => Settings.armorDeflectionReworkThreshold,
                    v => Settings.armorDeflectionReworkThreshold = v, 0f, 1f,
                    () => $"Damage Leakage Threshold: {(Settings.armorDeflectionReworkThreshold * 100f):F0}%", 0.05f),
                60f,
                new List<string> { "armorDeflectionRework" });
            _settingsGui.AddConditional("armorDeflectionReworkThreshold", () => Settings.armorDeflectionRework);

            _settingsGui.AddItem("sharpConversionMode", "sharpConversionMode", "Features",
                _settingsGui.CreateRadio(
                    () => (int)Settings.sharpConversionMode,
                    i => Settings.sharpConversionMode = (SharpConversionMode)i,
                    new[] { "Vanilla", "Only Metal Dulls", "Set Armor Threshold" },
                    "When to convert Sharp to Blunt Damage"),
                110f,
                new List<string> { "sharpToBluntThreshold" });

            _settingsGui.AddItem("sharpToBluntThreshold", "sharpToBluntThreshold", "Features",
                _settingsGui.CreateSlider(() => Settings.sharpToBluntThreshold, v => Settings.sharpToBluntThreshold = v,
                    0f, 2f, () => $"Convert Threshold: {(Settings.sharpToBluntThreshold * 100f):F0}%", 0.05f),
                60f,
                new List<string> { "sharpConversionMode" });
            _settingsGui.AddConditional("sharpToBluntThreshold",
                () => Settings.sharpConversionMode == SharpConversionMode.ArmorThreshold);

            _settingsGui.AddItem("finalArmorCalculations", "finalArmorCalculations", "Features",
                _settingsGui.CreateRadio(
                    () => (int)Settings.finalArmorCalculations,
                    i => Settings.finalArmorCalculations = (FinalArmorCalculations)i,
                    new[] { "Vanilla", "Custom", "Yayo" }, "Final Armor Calculations"),
                110f);

            _settingsGui.AddCategory("Tweaks");
            _settingsGui.AddItem("maxArmorAmount", "maxArmorAmount", "Tweaks",
                _settingsGui.CreateSlider(() => Settings.maxArmorAmount, v => Settings.maxArmorAmount = v, 0f, 20.1f,
                    () => Settings.maxArmorAmount > 20f
                        ? "Max Armor Amount: Infinite"
                        : $"Max Armor Amount: {(Settings.maxArmorAmount * 100f):F0}%", 0.1f),
                60f);
            _settingsGui.AddItem("armorStrength", "armorStrength", "Tweaks",
                _settingsGui.CreateSlider(() => Settings.armorStrength, v => Settings.armorStrength = v, 0f, 5f,
                    () => $"Armor Strength: {FormatBuff(Settings.armorStrength)}", 0.05f),
                60f);
            _settingsGui.AddItem("weaponRange", "weaponRange", "Tweaks",
                _settingsGui.CreateSlider(() => Settings.weaponRange, v => Settings.weaponRange = v, 0f, 5f,
                    () => $"Weapon Range Modifier: {FormatBuff(Settings.weaponRange)}", 0.01f),
                60f);
            _settingsGui.AddItem("bulletSpread", "bulletSpread", "Tweaks",
                _settingsGui.CreateSlider(() => Settings.bulletSpread, v => Settings.bulletSpread = v, 0f, 2f,
                    () => $"Projectile Spread Amount: {(Settings.bulletSpread * 100):F0}%", 0.01f),
                60f);
            _settingsGui.AddItem("burstSizeMultiplier", "burstSizeMultiplier", "Tweaks",
                _settingsGui.CreateSlider(() => Settings.burstSizeMultiplier, v => Settings.burstSizeMultiplier = v,
                    0.1f, 8f,
                    () =>
                        $"Burst Size (*Requires Reloading Current Save*): {(FormatBuff(Settings.burstSizeMultiplier))}",
                    0.1f),
                60f);
            _settingsGui.AddItem("skillsEffectBulletSpread", "skillsEffectBulletSpread", "Tweaks",
                _settingsGui.CreateCheckbox(() => Settings.skillsEffectBulletSpread,
                    v => Settings.skillsEffectBulletSpread = v, "Scale with skill level"),
                55f,
                new List<string> { "bulletSpread" });
            _settingsGui.AddConditional("skillsEffectBulletSpread", () => Settings.bulletSpread > 0.0001f);

            _settingsGui.AddItem("armorPenetrationImportance", "armorPenetrationImportance", "Tweaks",
                _settingsGui.CreateSlider(() => Settings.armorPenetrationImportance,
                    v => Settings.armorPenetrationImportance = v, 0f, 5f,
                    () => $"Armor Penetration Importance: {FormatBuff(Settings.armorPenetrationImportance)}", 0.05f),
                60f);
            _settingsGui.AddItem("friendlyFireMultiplier", "friendlyFireMultiplier", "Tweaks",
                _settingsGui.CreateSlider(() => Settings.friendlyFireMultiplier,
                    v => Settings.friendlyFireMultiplier = v, 0.05f, 5f,
                    () => $"Friendly Fire Damage Multiplier: {FormatBuff(Settings.friendlyFireMultiplier)}", 0.05f),
                60f);
            _settingsGui.AddItem("enemyDamageMultiplier", "enemyDamageMultiplier", "Tweaks",
                _settingsGui.CreateSlider(() => Settings.enemyDamageMultiplier, v => Settings.enemyDamageMultiplier = v,
                    0.05f, 5f, () => $"Enemy Damage Boost: {FormatBuff(Settings.enemyDamageMultiplier)}", 0.05f),
                60f);
            _settingsGui.AddItem("deflectionChanceMultiplier", "deflectionChanceMultiplier", "Tweaks",
                _settingsGui.CreateSlider(() => Settings.deflectionChanceMultiplier,
                    v => Settings.deflectionChanceMultiplier = v, 0.05f, 5f,
                    () => $"Armor Deflection Chance Multiplier: {FormatBuff(Settings.deflectionChanceMultiplier)}",
                    0.05f),
                60f);
            _settingsGui.AddItem("reductionChanceMultiplier", "reductionChanceMultiplier", "Tweaks",
                _settingsGui.CreateSlider(() => Settings.reductionChanceMultiplier,
                    v => Settings.reductionChanceMultiplier = v, 0.05f, 5f,
                    () => $"Armor Damage Reduction Chance Multiplier: {FormatBuff(Settings.reductionChanceMultiplier)}",
                    0.05f),
                60f);
            _settingsGui.AddItem("reductionAmountMultiplier", "reductionAmountMultiplier", "Tweaks",
                _settingsGui.CreateSlider(() => Settings.reductionAmountMultiplier,
                    v => Settings.reductionAmountMultiplier = v, 0.01f, 1f,
                    () => $"Armor Damage Reduction Multiplier: {(Settings.reductionAmountMultiplier * 100):F0}%",
                    0.01f),
                60f);
            _settingsGui.AddItem("damageBoost", "damageBoost", "Tweaks",
                _settingsGui.CreateSlider(() => Settings.damageBoost, v => Settings.damageBoost = v, 0.1f, 10f,
                    () => $"Damage Boost: {FormatBuff(Settings.damageBoost)}", 0.1f),
                60f);
            _settingsGui.AddItem("meleeDamageBoost", "meleeDamageBoost", "Tweaks",
                _settingsGui.CreateSlider(() => Settings.meleeDamageBoost, v => Settings.meleeDamageBoost = v, 0.1f, 10f,
                    () => $"Melee Damage Boost: {FormatBuff(Settings.meleeDamageBoost)}", 0.1f),
                60f);
            _settingsGui.AddItem("rangedDamageBoost", "rangedDamageBoost", "Tweaks",
                _settingsGui.CreateSlider(() => Settings.rangedDamageBoost, v => Settings.rangedDamageBoost = v, 0.1f,
                    10f, () => $"Ranged Damage Boost: {FormatBuff(Settings.rangedDamageBoost)}", 0.1f),
                60f);
            _settingsGui.AddItem("itemDurabilityMultiplier", "itemDurabilityMultiplier", "Tweaks",
                _settingsGui.CreateSlider(() => Settings.itemDurabilityMultiplier,
                    v => Settings.itemDurabilityMultiplier = v, 0f, 10f,
                    () => $"Durability Loss Multiplier (All): {FormatBuff(Settings.itemDurabilityMultiplier)}", 0.1f),
                60f);
            _settingsGui.AddItem("mechanoidBuff", "mechanoidBuff", "Tweaks",
                _settingsGui.CreateSlider(() => Settings.mechanoidBuff, v => Settings.mechanoidBuff = v, 0.1f, 10f,
                    () => $"Mechanoid Armor Buff: {FormatBuff(Settings.mechanoidBuff)}", 0.1f),
                60f);
            _settingsGui.AddItem("anomalyBuff", "anomalyBuff", "Tweaks",
                _settingsGui.CreateSlider(() => Settings.anomalyBuff, v => Settings.anomalyBuff = v, 0.1f, 10f,
                    () => $"Anomaly Armor Buff: {FormatBuff(Settings.anomalyBuff)}", 0.1f),
                60f);
            _settingsGui.AddItem("insectBuff", "insectBuff", "Tweaks",
                _settingsGui.CreateSlider(() => Settings.insectBuff, v => Settings.insectBuff = v, 0.1f, 10f,
                    () => $"Insect Armor Buff: {FormatBuff(Settings.insectBuff)}", 0.1f),
                60f);
            _settingsGui.AddItem("humanLikeBuff", "humanLikeBuff", "Tweaks",
                _settingsGui.CreateSlider(() => Settings.humanLikeBuff, v => Settings.humanLikeBuff = v, 0.1f, 10f,
                    () => $"Human-Like Armor Buff: {FormatBuff(Settings.humanLikeBuff)}", 0.1f),
                60f);
            _settingsGui.AddItem("bonusHeatDamageToDurability", "bonusHeatDamageToDurability", "Tweaks",
                _settingsGui.CreateSlider(() => Settings.bonusHeatDamageToDurability,
                    v => Settings.bonusHeatDamageToDurability = v, 0.1f, 10f,
                    () =>
                        $"Durability Loss Multiplier (From Heat Damage): {FormatBuff(Settings.bonusHeatDamageToDurability)}",
                    0.1f),
                60f);

            _settingsGui.AddCategory("Presets");
            _settingsGui.AddItem("presetButtons", "presetButtons", "Presets", rect =>
            {
                float buttonWidth = 180f;
                float y = rect.y;

                if (Widgets.ButtonText(new Rect(rect.x, y, buttonWidth, 30f), "Vanilla Rimworld"))
                {
                    Settings.ResetToVanilla();
                }

                y += 35f;

                if (Widgets.ButtonText(new Rect(rect.x, y, buttonWidth, 30f), "Yayo's Combat 3 Defaults"))
                {
                    Settings.ResetToYayo();
                }

                y += 35f;

                if (Widgets.ButtonText(new Rect(rect.x, y, buttonWidth, 30f), "Default / Modder's Choice"))
                {
                    Settings.ResetToDefaults();
                }

                y += 35f;

                if (Widgets.ButtonText(new Rect(rect.x, y, buttonWidth, 30f), "Random"))
                {
                    Settings.Random();
                }
            }, height: 150f);

            _settingsGui.AddDescription("debugLogging",
                "Enables some detailed log messages for troubleshooting and debugging. I made this to confirm certain features were working while I made the mod, but I changed the what is logged a lot throughout making this mod, so, it might not be very helpful.");
            _settingsGui.AddDescription("doTracking",
                "This will add a running average of damage for weapons and damage reduction for armor then store them in the item stats. This might have a performance effect, especially with large raids, so I would disable it if your not using it.");
            _settingsGui.AddDescription("showDetails", "Toggles the visibility of descriptions under each mod setting.");
            _settingsGui.AddDescription("difficultyBar",
                "An attempt to show how difficult the game will be after the Tweaks. (may not be super accurate)");

            _settingsGui.AddDescription("yayoCombatCompatEnableAAA",
                "Enables patching for Yayo's 'Advanced Armor Algorithms' system.\nYou do not need to have Yayo's Combat 3 in your modlist to use this.");
            _settingsGui.AddDescription("yayoCombatArmorEffectiveness",
                "Multiplier that overrides armor effectiveness when Yayo compatibility is enabled.");
            _settingsGui.AddDescription("vanillaCombatReloadedCompatibility",
                "Enables compatibility with Vanilla Combat Reloaded's 'Advanced Armor Behavior'. If you change the values for the Advanced Armor Behavior in Vanilla Combat Reloaded, disable this setting, then re-enable it for it to update with the new values.");
            _settingsGui.AddDescription("vanillaCombatOverhaulCompatibility",
                "Fixes conflicts with Vanilla Combat Overhaul (VCO) and shows the options below to pick which mod handles the features both mods change. Without it, VCO's Advanced Armor doubles weapon armor penetration and this mod's armor uses that doubled value. Does nothing while VCO isn't loaded.");
            _settingsGui.AddDescription("vcoUseAdvancedArmor",
                "On: armor and armor penetration are scaled like VCO's Advanced Armor (by VCO's armor scale) before this mod's armor calculations, and weapons keep VCO's doubled armor penetration. Off: VCO's armor penetration doubling is removed, so weapons show and use their normal values. Does nothing if Advanced Armor is off in VCO.");
            _settingsGui.AddDescription("vcoUseProjectileWounds",
                "On: VCO's bullet and arrow wounds are used (damage spread to nearby body parts, rounds passing through). They only apply to hits armor didn't reduce, judged by this mod's armor so damage boosts and nerfs don't count as armor. Off: bullets and arrows wound like vanilla.");
            _settingsGui.AddDescription("vcoUseMissSpread",
                "On: VCO decides where missed shots go and Projectile Spread is skipped. Off: Projectile Spread places missed shots instead, without its extra hit roll since VCO already rolled the shot with its own accuracy changes. Only matters if Projectile Spread is above 0%.");
            _settingsGui.AddDescription("vcoUseFireModeBursts",
                "On: VCO's fire modes change burst size and this mod's Burst Size is ignored. Off: Burst Size is used, and VCO's fire modes still change accuracy, aim time and cooldown but not burst size. Like Burst Size, reload the save after changing this.");
            _settingsGui.AddDescription("vcoScaleFireModeDistances",
                "VCO's automatic fire mode is picked by distance (Short Burst from 12 tiles, Precision from 25 by default). When on, those distances scale with the Weapon Range Modifier, so at 2x range they become 24 and 50 tiles.");
            _settingsGui.AddDescription("respectNonLethalDamage",
                "Some events deal damage that is meant to down a pawn without killing it (a Metalhorror emerging from its host, downed refugees from quests and transport pods, etc.). When enabled, this mod's damage boosts and multipliers (Damage Boost, Melee/Ranged Damage Boost, Enemy Damage Boost, Friendly Fire) are not applied to that damage, so it stays non-lethal like vanilla intends.");


            _settingsGui.AddDescription("allArmorEffectsEverywhere",
                "This will simplify armor even more, if you want that. I would recommend weakening armor if you enable this since every single piece of armor is used to protect the wearer.");
            _settingsGui.AddDescription("ignoreWeakArmor",
                "This will ignore armor if the armor % is under a certain threshold. If you disable durability loss too it, it can prevent your colonist from losing all their clothes when the durability loss settings are boosted.");


            _settingsGui.AddDescription("durabilityEffectsArmor",
                "Makes armor protection scale based on the item's remaining durability.");
            _settingsGui.AddDescription("thickArmor",
                "Uses the logic from the Thick Armor Mod. Armor will do its calculations an extra time for each layer it covers (Outer/Middle/Skin/etc), but will have a lesser effect each subsequent time after the first.");
            _settingsGui.AddDescription("thickArmorEffectivenessLoss",
                "Each layer after the first will be reduce their effectiveness by this amount. For example if this is set to 75% and the armor covers 3 layers. If the armor had an armor rating of 100% the second layer would use an armor rating of 75% and the third would be 56.25%.");
            _settingsGui.AddDescription("durabilityEffectsArmorMultiplier",
                "This will effect a multiplier on how fast armor losses effectiveness depending on durability.\nThe math for it is: armorRating *= 1f - ((1f - (currentHP / maxHP)) * thisSetting);");
            _settingsGui.AddDescription("durabilityEffectsArmorDoTattered",
                "If an item is less than 50% hitpoints it will lose effectiveness 3x faster");
            _settingsGui.AddDescription("doDamageEffect",
                "Makes damage reduction of armor scale with damage.\nMakes single target high damage attacks stronger.");
            _settingsGui.AddDescription("reliableArmors",
                "This will use the calculations from the \"Reliable Armors\" Mod. If deflect does not happen, the damage is reduced by a random percentage between 35% and 100% of the remaining armor value (after penetration). For example, if the target has 80% armor and the weapon has 30% penetration, the final armor is 80 - 30 = 50%, and damage will be reduced between 17.5% and 50%.");
            _settingsGui.AddDescription("alwaysReduceDamage",
                "Forces all armor to reduce at least a small portion of incoming damage, as long as the effective armor is above 0.");
            _settingsGui.AddDescription("doAdrenaline",
                "Has a chance to add a custom 'Adrenaline' hediff if a pawn takes damage and has more than 15% pain.");
            _settingsGui.AddDescription("doTechLevelEffect",
                "Applies a scaling bonus to armor and armor penetration based on the difference in tech levels.");
            _settingsGui.AddDescription("techLevelBoost", "Amount of armor bonus per difference in tech levels.");

            _settingsGui.AddDescription("durabilityTypeInteraction",
                "Makes blunt damage increase the durability loss of metal armor, and makes sharp damage do more damage to the durability of non-metal armor.");
            _settingsGui.AddDescription("durabilityTypeInteractionMultiplier",
                "Multiplier applied to damage when a weak damage type is used against the armor material.");

            _settingsGui.AddDescription("armorTiersRework",
                "An attempt to make a CE-like armor calculations. This will assign a tier to each piece of armor and weapon. the math to calculate damage is:\n" +
                "Deflection chance = (2 + ArmorTier - WepTier) * 0.25\nReduction chance = 2 + ArmorTier - WepTier * 0.5;\nWARNING: Since this is a rework it might override some of the other settings you use!");

            _settingsGui.AddDescription("armorDeflectionRework",
                "Reworks armor to always deflect damage at the cost of absorting the damage into the armor itself. " +
                "But after a certain threshold the damage will be reduced instead of being deflected. When the damage is reduced the armor will absorb its armor health percentage of the damage and leak the rest.\nWARNING: Since this is a rework it might override some of the other settings you use!");
            _settingsGui.AddDescription("armorDeflectionReworkThreshold",
                "The armor health percentage where damage stops fully being deflected/absorbed");
            _settingsGui.AddDescription("sharpConversionMode",
                "Controls how sharp damage gets converted to blunt based on material or thresholds.");
            _settingsGui.AddDescription("sharpToBluntThreshold",
                "When the sharp armor value is above this percentage, incoming damage will be converted to blunt.");
            _settingsGui.AddDescription("finalArmorCalculations",
                "Determines which system is used to finalize armor mitigation (vanilla, custom, or Yayo's logic). The 'custom' option will work like vanilla, except it will weaken armor penetration as it passes through the armor. It will also increase the chance to deflect sharp damage and make blunt damage lose its armor penetration extremely quickly.\nNote: BOTH rework settings ignore this setting if they are enabled");

            _settingsGui.AddDescription("maxArmorAmount", "The cap on armor value");
            _settingsGui.AddDescription("armorPenetrationImportance",
                "Scales how much armor penetration reduces armor effectiveness.");
            _settingsGui.AddDescription("armorStrength", "A flat multiplier applied to armor to increase its strength.");
            _settingsGui.AddDescription("weaponRange",
                "A flat multiplier applied to the distance ranged weapons can shoot. This will only apply the effect after you restart your game. If the range exceeds 63 vanilla rimworld stops making the \"range circle thingy\". So, i made a custom one to show up after that point but it doesn't look quite the same as vanilla.");
            _settingsGui.AddDescription("bulletSpread",
                "This changes the spread of projectiles that miss a target. It's set as a percentage of 90 degrees, so for example: 20% = 18 degrees or 200% = 180 degrees. If you set this to 0% it will just use the normal vanilla calculations.");
            _settingsGui.AddDescription("burstSizeMultiplier",
                "This changes the burst size of weapons that shoot in bursts. It will round to the nearest whole number. \nWARNING: This will not apply mid game, you will need to exit to the main menu then reload a save for it to apply. (It will effect the visual 'Burst shot count' mid game, but will not actually update current existing weapons unless you reload the save)");
            _settingsGui.AddDescription("skillsEffectBulletSpread",
                "Depending on the skill of the shooter it will apply a multiplier between 3 (at shooting level 0) and 0.33 (at shooting level 19+) to the spread amount set above.");
            _settingsGui.AddDescription("deflectionChanceMultiplier",
                "Increases the chance for armor to deflect damage.");
            _settingsGui.AddDescription("reductionChanceMultiplier",
                "Increases the chance for armor to reduce damage if it didn't deflect.");
            _settingsGui.AddDescription("reductionAmountMultiplier",
                "The base reduction of damage when armor reduces damage.");
            _settingsGui.AddDescription("enemyDamageMultiplier",
                "Flat multiplier applied to all incoming damage before armor calculations, but it only applies to enemies (Things from non-allied/non-neutral factions).");
            _settingsGui.AddDescription("friendlyFireMultiplier",
                "Flat multiplier applied to all incoming damage before armor calculations, but it only applies to your colonists hitting each other.");
            _settingsGui.AddDescription("damageBoost",
                "Flat multiplier applied to all incoming damage before armor calculations.");
            _settingsGui.AddDescription("meleeDamageBoost",
                "Flat multiplier applied to all incoming damage before armor calculations from melee damage.");
            _settingsGui.AddDescription("rangedDamageBoost",
                "Flat multiplier applied to all incoming damage before armor calculations from ranged damage.");
            _settingsGui.AddDescription("itemDurabilityMultiplier",
                "Multiplier for how fast items lose durability when taking damage.");
            _settingsGui.AddDescription("bonusHeatDamageToDurability",
                "Multiplier for how fast items lose durability when taking damage, but only for heat damage.");
            _settingsGui.AddDescription("mechanoidBuff", "Buff to armor values for mechanoid pawns.");
            _settingsGui.AddDescription("insectBuff", "Buff to armor values for inscect pawns.");
            _settingsGui.AddDescription("anomalyBuff", "Buff to armor values for anomaly related pawns.");
            _settingsGui.AddDescription("humanLikeBuff",
                "Buff to armor values for human-like pawns (should include baseliners and most other human xenotypes).");
        }

        public static string FormatBuff(float percent, float netural = 1f)
        {
            if (percent - netural > -0.0001f && percent - netural < 0.0001f)
            {
                return "0% Buff / Vanilla";
            }

            if (percent > netural)
            {
                return $"{(percent - netural) * 100:F0}% Buff";
            }

            return $"{(netural - percent) * 100:F0}% Nerf";

        }

        private static void DrawDifficultyBar(Rect rect, float percent)
        {
            percent = Mathf.Clamp01(percent);

            Widgets.DrawBoxSolid(rect, new Color(0.6f, 0.6f, 0.6f));

            Color fillColor;
            if (percent >= 0.99f)
                fillColor = new Color(0.5f, 0f, 0f);
            else if (percent >= 0.66f)
                fillColor = Color.red;
            else if (percent >= 0.33f)
                fillColor = new Color(1f, 0.5f, 0f);
            else
                fillColor = Color.green;

            Rect fillRect = new Rect(rect.x, rect.y, rect.width * percent, rect.height);
            Widgets.DrawBoxSolid(fillRect, fillColor);

            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(rect, $"{Mathf.RoundToInt(percent * 100)}% Difficulty");
            Text.Anchor = TextAnchor.UpperLeft;
        }



    }

    public class CombatTweaksModSettings : ModSettings
    {
        public enum SharpConversionMode
        {
            Vanilla,
            OnlyMetal,
            ArmorThreshold
        }

        public enum FinalArmorCalculations
        {
            Vanilla,
            Custom,
            Yayo
        }

        public HashSet<string> favorites;
        public SharpConversionMode sharpConversionMode = SharpConversionMode.ArmorThreshold;
        public FinalArmorCalculations finalArmorCalculations = FinalArmorCalculations.Custom;
        public bool yayoCombatCompat;
        public bool vanillaCombatReloadedCompatibility;
        public bool vanillaCombatOverhaulCompatibility;
        public bool vcoUseAdvancedArmor;
        public bool vcoUseProjectileWounds = true;
        public bool vcoUseMissSpread = true;
        public bool vcoUseFireModeBursts = true;
        public bool vcoScaleFireModeDistances = true;
        public bool reliableArmors;
        public bool doTracking;
        public bool debugLogging;
        public bool durabilityEffectsArmor = true;
        public bool durabilityEffectsArmorDoTattered = true;
        public bool doDamageEffect = true;
        public bool alwaysReduceDamage = true;
        public bool doTechLevelEffect = true;
        public bool showDetails = true;
        public bool durabilityTypeInteraction = true;
        public bool armorDeflectionRework;
        public bool yayoCombatCompatEnableAAA;
        public bool thickArmor;
        public bool armorTiers;
        public bool doAdrenaline;
        public bool allArmorEffectsEverywhere;
        public bool ignoreWeakArmor = true;
        public bool ignoreWeakArmorStillTakeDurabilityDamage = true;
        public bool skillsEffectBulletSpread = true;
        public bool respectNonLethalDamage = true;
        public float thickArmorEffectivenessLoss = 0.5f;
        public float ignoreWeakArmorThreshold = 0.12f;
        public float durabilityEffectsArmorMultiplier = 0.004f;
        public float adrenalineChance = 0.1f;
        public float anomalyBuff = 1.0f;
        public float armorStrength = 1.0f;
        public float insectBuff = 1.0f;
        public float friendlyFireMultiplier = 1.0f;
        public float weaponRange = 1.0f;
        public float lastWeaponRange = 1.0f;
        public float lastMaxArmorAmount = 2.0f;
        public float enemyDamageMultiplier = 1.0f;
        public float bulletSpread;
        public float deflectionChanceMultiplier = 1.0f;
        public float reductionChanceMultiplier = 1.0f;
        public float reductionAmountMultiplier = 0.5f;
        public float bonusHeatDamageToDurability = 2.0f;
        public float sharpToBluntThreshold = 0.2f;
        public float yayoCombatArmorEffectiveness = 0.5f;
        public float techLevelBoost = 0.1f;
        public float maxArmorAmount = 20.1f;
        public float armorPenetrationImportance = 1.0f;
        public float itemDurabilityMultiplier = 1.2f;
        public float mechanoidBuff = 1.2f;
        public float humanLikeBuff = 1.0f;
        public float damageBoost = 1.0f;
        public float meleeDamageBoost = 1.0f;
        public float rangedDamageBoost = 1.0f;
        public float durabilityTypeInteractionMultiplier = 1.5f;
        public float armorDeflectionReworkThreshold = 0.5f;
        public float burstSizeMultiplier = 1.0f;
        public float lastBurstSizeMultiplier = 1.0f;




        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref sharpConversionMode, "sharpConversionMode", SharpConversionMode.ArmorThreshold);
            Scribe_Values.Look(ref finalArmorCalculations, "finalArmorCalculations", FinalArmorCalculations.Custom);
            Scribe_Values.Look(ref sharpToBluntThreshold, "sharpToBluntThreshold", 0.2f);
            Scribe_Values.Look(ref yayoCombatCompat, "yayoCombatCompat");
            Scribe_Values.Look(ref vanillaCombatReloadedCompatibility, "vanillaCombatReloadedCompatibility");
            Scribe_Values.Look(ref vanillaCombatOverhaulCompatibility, "vanillaCombatOverhaulCompatibility");
            Scribe_Values.Look(ref vcoUseAdvancedArmor, "vcoUseAdvancedArmor");
            Scribe_Values.Look(ref vcoUseProjectileWounds, "vcoUseProjectileWounds", true);
            Scribe_Values.Look(ref vcoUseMissSpread, "vcoUseMissSpread", true);
            Scribe_Values.Look(ref vcoUseFireModeBursts, "vcoUseFireModeBursts", true);
            Scribe_Values.Look(ref vcoScaleFireModeDistances, "vcoScaleFireModeDistances", true);
            Scribe_Values.Look(ref reliableArmors, "reliableArmor", true);
            Scribe_Values.Look(ref debugLogging, "debugLogging");
            Scribe_Values.Look(ref doTracking, "doTracking");
            Scribe_Values.Look(ref durabilityEffectsArmor, "durabilityEffectsArmor", true);
            Scribe_Values.Look(ref durabilityEffectsArmorDoTattered, "durabilityEffectsArmorDoTattered", true);
            Scribe_Values.Look(ref doDamageEffect, "doDamageEffect", true);
            Scribe_Values.Look(ref doTechLevelEffect, "doTechLevelEffect", true);
            Scribe_Values.Look(ref alwaysReduceDamage, "alwaysReduceDamage", true);
            Scribe_Values.Look(ref showDetails, "showDetails", true);
            Scribe_Values.Look(ref durabilityTypeInteraction, "durabilityTypeInteraction", true);
            Scribe_Values.Look(ref armorDeflectionRework, "armorDeflectionRework");
            Scribe_Values.Look(ref yayoCombatCompatEnableAAA, "yayoCombatCompatEnableAAA");
            Scribe_Values.Look(ref allArmorEffectsEverywhere, "allArmorEffectsEverywhere");
            Scribe_Values.Look(ref ignoreWeakArmor, "ignoreWeakArmor");
            Scribe_Values.Look(ref ignoreWeakArmorStillTakeDurabilityDamage, "ignoreWeakArmorStillTakeDurabilityDamage",
                true);
            Scribe_Values.Look(ref armorTiers, "armorTiers", true);
            Scribe_Values.Look(ref thickArmor, "thickArmor");
            Scribe_Values.Look(ref doAdrenaline, "doAdrenaline", true);
            Scribe_Values.Look(ref skillsEffectBulletSpread, "skillsEffectBulletSpread", true);
            Scribe_Values.Look(ref respectNonLethalDamage, "respectNonLethalDamage", true);
            Scribe_Values.Look(ref maxArmorAmount, "maxArmorAmount", 20.1f);
            Scribe_Values.Look(ref lastMaxArmorAmount, "lastMaxArmorAmount", 20.1f);
            Scribe_Values.Look(ref armorPenetrationImportance, "armorPenetrationImportance", 1.0f);
            Scribe_Values.Look(ref itemDurabilityMultiplier, "itemDurabilityMultiplier", 1.5f);
            Scribe_Values.Look(ref techLevelBoost, "techLevelBoost", 0.1f);
            Scribe_Values.Look(ref mechanoidBuff, "mechanoidBuff", 1.2f);
            Scribe_Values.Look(ref damageBoost, "damageBoost", 1.0f);
            Scribe_Values.Look(ref durabilityTypeInteractionMultiplier, "durabilityTypeInteractionMultiplier", 1.5f);
            Scribe_Values.Look(ref armorDeflectionReworkThreshold, "armorDeflectionReworkThreshold", 0.5f);
            Scribe_Values.Look(ref yayoCombatArmorEffectiveness, "yayoCombatArmorEffectiveness", 0.5f);
            Scribe_Values.Look(ref bonusHeatDamageToDurability, "bonusHeatDamageToDurability", 2f);
            Scribe_Values.Look(ref ignoreWeakArmorThreshold, "ignoreWeakArmorThreshold", 0.12f);
            Scribe_Values.Look(ref anomalyBuff, "anomalyBuff", 1f);
            Scribe_Values.Look(ref insectBuff, "insectBuff", 1f);
            Scribe_Values.Look(ref humanLikeBuff, "humanLikeBuff", 1f);
            Scribe_Values.Look(ref weaponRange, "weaponRange", 1f);
            Scribe_Values.Look(ref lastWeaponRange, "lastWeaponRange", 1f);
            Scribe_Values.Look(ref friendlyFireMultiplier, "friendlyFireMultiplier", 1f);
            Scribe_Values.Look(ref enemyDamageMultiplier, "enemyDamageMultiplier", 1f);
            Scribe_Values.Look(ref deflectionChanceMultiplier, "deflectionChanceMultiplier", 1f);
            Scribe_Values.Look(ref reductionChanceMultiplier, "reductionChanceMultiplier", 1f);
            Scribe_Values.Look(ref reductionAmountMultiplier, "reductionAmountMultiplier", 0.5f);
            Scribe_Values.Look(ref armorStrength, "armorStrength", 1f);
            Scribe_Values.Look(ref bulletSpread, "bulletSpread", 0f);
            Scribe_Values.Look(ref meleeDamageBoost, "meleeDamageBoost", 1f);
            Scribe_Values.Look(ref rangedDamageBoost, "rangedDamageBoost", 1f);
            Scribe_Values.Look(ref adrenalineChance, "adrenalineChance", 0.1f);
            Scribe_Values.Look(ref durabilityEffectsArmorMultiplier, "durabilityEffectsArmorMultiplier", 0.004f);
            Scribe_Values.Look(ref thickArmorEffectivenessLoss, "thickArmorEffectivenessLoss", 0.5f);
            Scribe_Values.Look(ref burstSizeMultiplier, "burstSizeMultiplier", 1.0f);
            Scribe_Values.Look(ref lastBurstSizeMultiplier, "lastBurstSizeMultiplier", 1.0f);
            Scribe_Collections.Look(ref favorites, "favorites", LookMode.Value);

            if (favorites == null)
            {
                favorites = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    "doTracking",
                    "debugLogging",
                    "showDetails"
                };
            }

        }

        public void ResetToDefaults()
        {
            durabilityEffectsArmor = true;
            durabilityEffectsArmorDoTattered = true;
            doDamageEffect = true;
            doTechLevelEffect = true;
            alwaysReduceDamage = true;
            sharpConversionMode = SharpConversionMode.ArmorThreshold;
            finalArmorCalculations = FinalArmorCalculations.Custom;
            sharpToBluntThreshold = 0.2f;
            showDetails = true;
            doTracking = false;
            durabilityTypeInteraction = true;
            armorDeflectionRework = false;
            yayoCombatCompatEnableAAA = false;
            allArmorEffectsEverywhere = false;
            ignoreWeakArmor = true;
            ignoreWeakArmorStillTakeDurabilityDamage = true;
            armorTiers = false;
            reliableArmors = true;
            ignoreWeakArmorThreshold = 0.12f;
            anomalyBuff = 1.0f;
            doAdrenaline = true;
            thickArmor = true;
            skillsEffectBulletSpread = true;
            respectNonLethalDamage = true;
            insectBuff = 1.0f;
            humanLikeBuff = 1.0f;
            armorStrength = 1.0f;
            friendlyFireMultiplier = 1.0f;
            enemyDamageMultiplier = 1.0f;
            deflectionChanceMultiplier = 1.0f;
            bonusHeatDamageToDurability = 2.0f;
            reductionChanceMultiplier = 1.0f;
            adrenalineChance = 0.1f;
            weaponRange = 1.0f;
            maxArmorAmount = 20.1f;
            mechanoidBuff = 1.2f;
            bulletSpread = 0.0f;
            armorPenetrationImportance = 1f;
            itemDurabilityMultiplier = 1.5f;
            techLevelBoost = 0.1f;
            damageBoost = 1.0f;
            durabilityTypeInteractionMultiplier = 1.5f;
            reductionAmountMultiplier = 0.5f;
            armorDeflectionReworkThreshold = 0.5f;
            yayoCombatArmorEffectiveness = 0.5f;
            meleeDamageBoost = 1.0f;
            rangedDamageBoost = 1.0f;
            durabilityEffectsArmorMultiplier = 0.004f;
            thickArmorEffectivenessLoss = 0.5f;
            burstSizeMultiplier = 1.0f;
        }

        public void ResetToVanilla()
        {
            durabilityEffectsArmor = false;
            doDamageEffect = false;
            doTechLevelEffect = false;
            alwaysReduceDamage = false;
            durabilityTypeInteraction = false;
            sharpConversionMode = SharpConversionMode.Vanilla;
            finalArmorCalculations = FinalArmorCalculations.Vanilla;
            sharpToBluntThreshold = 0.0f;
            doTracking = false;
            armorDeflectionRework = false;
            yayoCombatCompatEnableAAA = false;
            allArmorEffectsEverywhere = false;
            ignoreWeakArmor = false;
            ignoreWeakArmorStillTakeDurabilityDamage = true;
            skillsEffectBulletSpread = true;
            respectNonLethalDamage = true;
            thickArmor = false;
            armorTiers = false;
            reliableArmors = false;
            ignoreWeakArmorThreshold = 0.15f;
            anomalyBuff = 1.0f;
            insectBuff = 1.0f;
            humanLikeBuff = 1.0f;
            bulletSpread = 0.0f;
            doAdrenaline = false;
            friendlyFireMultiplier = 1.0f;
            bonusHeatDamageToDurability = 1.0f;
            adrenalineChance = 0.1f;
            armorStrength = 1.0f;
            enemyDamageMultiplier = 1.0f;
            deflectionChanceMultiplier = 1.0f;
            reductionChanceMultiplier = 1.0f;
            reductionAmountMultiplier = 0.5f;
            maxArmorAmount = 2.0f;
            mechanoidBuff = 1.0f;
            armorPenetrationImportance = 1.0f;
            weaponRange = 1.0f;
            itemDurabilityMultiplier = 1.0f;
            techLevelBoost = 0.0f;
            damageBoost = 1.0f;
            durabilityTypeInteractionMultiplier = 0.0f;
            armorDeflectionReworkThreshold = 0.5f;
            yayoCombatArmorEffectiveness = 0.5f;
            meleeDamageBoost = 1.0f;
            rangedDamageBoost = 1.0f;
            thickArmorEffectivenessLoss = 0.5f;
            burstSizeMultiplier = 1.0f;
        }

        public void ResetToYayo()
        {
            ResetToVanilla();
            yayoCombatCompatEnableAAA = true;
            finalArmorCalculations = FinalArmorCalculations.Yayo;
            yayoCombatArmorEffectiveness = 0.5f;
        }

        public void Random()
        {
            durabilityEffectsArmor = Rand.Value > 0.5f;
            doDamageEffect = Rand.Value > 0.5f;
            doTechLevelEffect = Rand.Value > 0.5f;
            alwaysReduceDamage = Rand.Value > 0.5f;
            durabilityTypeInteraction = Rand.Value > 0.5f;
            doTracking = Rand.Value > 0.5f;
            yayoCombatCompatEnableAAA = Rand.Value > 0.5f;
            allArmorEffectsEverywhere = Rand.Value > 0.5f;
            ignoreWeakArmor = Rand.Value > 0.5f;
            ignoreWeakArmorStillTakeDurabilityDamage = Rand.Value > 0.5f;
            doAdrenaline = Rand.Value > 0.5f;
            reliableArmors = Rand.Value > 0.5f;
            thickArmor = Rand.Value > 0.5f;

            sharpConversionMode = (SharpConversionMode)Rand.RangeInclusive(0, 2);

            finalArmorCalculations = (FinalArmorCalculations)Rand.RangeInclusive(0, 2);
            ignoreWeakArmorThreshold = RoundTo(Rand.Range(0f, 3f), 0.05f);
            anomalyBuff = RoundTo(Rand.Range(0f, 3f), 0.05f);
            insectBuff = RoundTo(Rand.Range(0f, 3f), 0.05f);
            humanLikeBuff = RoundTo(Rand.Range(0f, 3f), 0.05f);
            bonusHeatDamageToDurability = RoundTo(Rand.Range(0f, 3f), 0.05f);
            friendlyFireMultiplier = RoundTo(Rand.Range(0f, 3f), 0.05f);
            adrenalineChance = RoundTo(Rand.Range(0f, 1f), 0.01f);
            enemyDamageMultiplier = RoundTo(Rand.Range(0f, 1f), 0.05f);
            deflectionChanceMultiplier = RoundTo(Rand.Range(0f, 3f), 0.05f);
            bulletSpread = RoundTo(Rand.Range(0f, 0.9f), 0.01f);
            reductionChanceMultiplier = RoundTo(Rand.Range(0f, 3f), 0.05f);
            sharpToBluntThreshold = RoundTo(Rand.Range(0f, maxArmorAmount), 0.05f);
            maxArmorAmount = RoundTo(Rand.Range(0f, 20.1f), 0.1f);
            mechanoidBuff = RoundTo(Rand.Range(0.1f, 3f), 0.1f);
            armorPenetrationImportance = RoundTo(Rand.Range(0f, 3f), 0.1f);
            weaponRange = RoundTo(Rand.Range(0f, 3f), 0.1f);
            armorStrength = RoundTo(Rand.Range(0f, 3f), 0.1f);
            itemDurabilityMultiplier = RoundTo(Rand.Range(0f, 3f), 0.1f);
            techLevelBoost = RoundTo(Rand.Range(0f, 2f), 0.05f);
            damageBoost = RoundTo(Rand.Range(0.1f, 3f), 0.1f);
            durabilityTypeInteractionMultiplier = RoundTo(Rand.Range(0f, 3f), 0.05f);
            armorDeflectionReworkThreshold = RoundTo(Rand.Range(0f, 1f), 0.05f);
            yayoCombatArmorEffectiveness = RoundTo(Rand.Range(0f, 1f), 0.01f);
            meleeDamageBoost = RoundTo(Rand.Range(0f, 2f), 0.05f);
            rangedDamageBoost = RoundTo(Rand.Range(0f, 2f), 0.05f);
            reductionAmountMultiplier = RoundTo(Rand.Range(0.01f, 1f), 0.01f);
            burstSizeMultiplier = RoundTo(Rand.Range(0.1f, 8f), 0.1f);

            if (finalArmorCalculations == FinalArmorCalculations.Vanilla)
            {
                armorTiers = Rand.Value > 0.5f;
                armorDeflectionRework = !armorTiers && Rand.Value > 0.5f;
            }

        }

        public float CalculateDifficultyScore()
        {
            float score = 0.14f;

            if (durabilityEffectsArmor) score += 0.03f;
            if (doDamageEffect) score += 0.03f;
            if (alwaysReduceDamage) score += 0.03f;
            if (durabilityTypeInteraction) score += 0.03f;
            if (armorDeflectionRework) score += 0.03f;


            if (doTechLevelEffect)
                score += Mathf.Clamp01(techLevelBoost / 0.5f) * 0.05f;

            score -= (1f - Mathf.Clamp01(durabilityTypeInteractionMultiplier / 2f)) * 0.02f;
            score += Mathf.Clamp01(damageBoost / 2f) * 0.1f;
            score += Mathf.Clamp01(itemDurabilityMultiplier / 2f) * 0.05f;
            score += Mathf.Clamp01(armorPenetrationImportance / 1.5f) * 0.05f;
            score += Mathf.Clamp01(durabilityTypeInteractionMultiplier / 2f) * 0.05f;

            if (armorDeflectionRework)
                score += Mathf.Clamp01(armorDeflectionReworkThreshold / 0.75f) * 0.05f;

            score += (mechanoidBuff - 1f) * 0.15f;
            score += (anomalyBuff - 1f) * 0.15f;
            score += (insectBuff - 1f) * 0.15f;
            score += (enemyDamageMultiplier - 1f) * 0.25f;
            score += (friendlyFireMultiplier - 1f) * 0.05f;

            if (sharpConversionMode == SharpConversionMode.ArmorThreshold)
                score += Mathf.Clamp01((maxArmorAmount - sharpToBluntThreshold) / maxArmorAmount) * 0.05f;

            return Mathf.Clamp01(score);
        }


        private float RoundTo(float val, float roundTo)
        {
            return Mathf.Round(val / roundTo) * roundTo;
        }


        public override string ToString()
        {
            return
                $"Mod Settings Variables: [durabilityEffectsArmor: {durabilityEffectsArmor}], [doDamageEffect: {doDamageEffect}], [doTechLevelEffect: {doTechLevelEffect}]," +
                $"[alwaysReduceDamage: {alwaysReduceDamage}], [durabilityTypeInteraction: {durabilityTypeInteraction}], [sharpConversionMode: {sharpConversionMode}], " +
                $"[finalArmorCalculations: {finalArmorCalculations}], [sharpToBluntThreshold: {sharpToBluntThreshold}], [showDetails: {showDetails}]," +
                $"[armorDeflectionRework: {armorDeflectionRework}], [yayoCombatCompatEnableAAA: {yayoCombatCompatEnableAAA}], [maxArmorAmount: {maxArmorAmount}]," +
                $"[mechanoidBuff: {mechanoidBuff}], [armorPenetrationImportance: {armorPenetrationImportance}], [itemDurabilityMultiplier: {itemDurabilityMultiplier}], " +
                $"[techLevelBoost: {techLevelBoost}], [damageBoost: {damageBoost}], [durabilityTypeInteractionMultiplier: {durabilityTypeInteractionMultiplier}], " +
                $"[armorDeflectionReworkThreshold: {armorDeflectionReworkThreshold}], [yayoCombatArmorEffectiveness: {yayoCombatArmorEffectiveness}]";
        }


    }
}

namespace Kasterfly.SettingsBuilder
{
    public class SettingsGUI
    {
        private Vector2 _scrollPos;
        private string _selectedCategory;
        private Dictionary<string, List<SettingItem>> _categoryItems = new Dictionary<string, List<SettingItem>>();
        private Dictionary<string, string> _descriptions = new Dictionary<string, string>();
        private Dictionary<string, Func<bool>> _conditionals = new Dictionary<string, Func<bool>>();

        public void AddCategory(string category)
        {
            if (!_categoryItems.ContainsKey(category))
                _categoryItems[category] = new List<SettingItem>();

            if (_selectedCategory == null)
                _selectedCategory = category;
        }

        public void AddItem(string label, string key, string category, Action<Rect> drawFunc, float height = 35f, List<string> linkedKeys = null)
        {
            if (!_categoryItems.ContainsKey(category))
                AddCategory(category);

            _categoryItems[category].Add(new SettingItem
            {
                key = key,
                label = label,
                drawFunc = drawFunc,
                height = height,
                linkedKeys = linkedKeys ?? new List<string>()
            });
        }

        public void AddDescription(string key, string description)
        {
            _descriptions[key] = description;
        }

        public void AddConditional(string key, Func<bool> condition)
        {
            _conditionals[key] = condition;
        }

        public void DrawSettings(Rect canvas)
        {
            if (CombatTweaksMod.Settings.favorites == null)
                CombatTweaksMod.Settings.favorites = new HashSet<string>();

            var leftRect = new Rect(canvas.x, canvas.y, 180f, canvas.height);
            var rightRect = new Rect(canvas.x + 185f, canvas.y, canvas.width - 190f, canvas.height);

            Widgets.BeginGroup(leftRect);
            float y = 0f;
            foreach (var cat in _categoryItems.Keys)
            {
                if (Widgets.ButtonText(new Rect(0f, y, 170f, 30f), cat))
                {
                    _selectedCategory = cat;
                    _scrollPos = Vector2.zero;
                }

                y += 35f;
            }

            Widgets.EndGroup();

            Widgets.DrawMenuSection(rightRect);
            Widgets.BeginScrollView(rightRect, ref _scrollPos, new Rect(0f, 0f, rightRect.width - 20f, 4000f));
            float curY = 10f;

            if (_selectedCategory != null && _categoryItems.TryGetValue(_selectedCategory, out var itemsRaw))
            {
                Func<SettingItem, string> getId = si => si.key;
                Func<SettingItem, bool> isFav = si => CombatTweaksMod.Settings.favorites.Contains(getId(si));

                var items = itemsRaw
                    .Select((si, index) => new { si, index })
                    .OrderByDescending(x => isFav(x.si))
                    .ThenBy(x => x.index)                    
                    .Select(x => x.si)
                    .ToList();


                var visited = new HashSet<string>();

                foreach (var item in items)
                {
                    if (visited.Contains(item.key)) continue;

                    List<SettingItem> group = new List<SettingItem> { item };
                    visited.Add(item.key);

                    foreach (var other in items)
                    {
                        if (visited.Contains(other.key)) continue;
                        if (item.linkedKeys.Contains(other.key) || other.linkedKeys.Contains(item.key))
                        {
                            group.Add(other);
                            visited.Add(other.key);
                        }
                    }

                    if (CombatTweaksMod.Settings.favorites == null)
                        CombatTweaksMod.Settings.favorites = new HashSet<string>();
                    List<string> groupIds = new List<string>();
                    for (int gi = 0; gi < group.Count; gi++)
                        groupIds.Add(getId(group[gi]));

                    int subIndex = 0;
                    foreach (var subItem in group) {
                        if (_conditionals.TryGetValue(subItem.key, out var cond) && !cond())
                            continue;

                        const float starW = 40f;
                        Rect itemRect = new Rect(10f, curY, rightRect.width - 30f, subItem.height);
                        Rect starRect = new Rect(itemRect.x, itemRect.y, starW, itemRect.height);
                        Rect contentRect = new Rect(itemRect.x + starW + 4f, itemRect.y, itemRect.width - starW - 4f,
                            itemRect.height);

                        bool isLabel = (subIndex == 0);

                        bool groupFav = true;
                        for (int gi = 0; gi < groupIds.Count; gi++)
                        {
                            if (!CombatTweaksMod.Settings.favorites.Contains(groupIds[gi]))
                            {
                                groupFav = false;
                                break;
                            }
                        }

                        var oldAnchor = Text.Anchor;
                        var oldColor = GUI.color;

                        if (isLabel)
                        {
                            Text.Anchor = TextAnchor.MiddleCenter;
                            GUI.color = groupFav ? new Color(1f, 0.85f, 0.2f) : Color.white;

                            if (Widgets.ButtonText(starRect, groupFav ? "★" : "☆", drawBackground: false))
                            {
                                if (groupFav)
                                {
                                    for (int gi = 0; gi < groupIds.Count; gi++)
                                        CombatTweaksMod.Settings.favorites.Remove(groupIds[gi]);
                                }
                                else
                                {
                                    for (int gi = 0; gi < groupIds.Count; gi++)
                                        CombatTweaksMod.Settings.favorites.Add(groupIds[gi]);
                                }
                            }
                        }

                        GUI.color = oldColor;
                        Text.Anchor = oldAnchor;

                        subItem.drawFunc?.Invoke(contentRect);
                        curY += subItem.height;

                        if (_descriptions.TryGetValue(subItem.key, out var desc) && CombatTweaksMod.Settings.showDetails)
                        {
                            Rect descRect = new Rect(20f, curY, rightRect.width - 40f, 90f);
                            Widgets.Label(descRect, desc);
                            curY += 90f;
                        }

                        curY += 10f;
                        subIndex++;
                    }

                    Widgets.DrawLineHorizontal(10f, curY, rightRect.width - 30f);
                    curY += 10f;
                }

            }

            Widgets.EndScrollView();

            if (Mathf.Abs(CombatTweaksMod.Settings.lastMaxArmorAmount - CombatTweaksMod.Settings.maxArmorAmount) > 0.001f)
            {
                ArmorCapInitializer.ApplyArmorCapFromSettings();
                CombatTweaksMod.Settings.lastMaxArmorAmount = CombatTweaksMod.Settings.maxArmorAmount;
            }
            if (Mathf.Abs(CombatTweaksMod.Settings.lastWeaponRange - CombatTweaksMod.Settings.weaponRange) > 0.001f)
            {
                VerbRangeMultiplierPatch.ApplyRangeBuffFromSettings();
                CombatTweaksMod.Settings.lastWeaponRange = CombatTweaksMod.Settings.weaponRange;
            }
            if (Mathf.Abs(CombatTweaksMod.Settings.lastBurstSizeMultiplier - CombatTweaksMod.Settings.burstSizeMultiplier) > 0.001f)
            {
                VerbBurstMultiplierPatch.ApplyBurstBuffFromSettings();
                CombatTweaksMod.Settings.lastBurstSizeMultiplier = CombatTweaksMod.Settings.burstSizeMultiplier;
            }
        }


        public Action<Rect> CreateCheckbox(Func<bool> getter, Action<bool> setter, string label)
        {
            return rect =>
            {
                bool val = getter();
                Widgets.CheckboxLabeled(rect, label.Bold(), ref val);
                setter(val);
            };
        }

        public Action<Rect> CreateSlider(Func<float> getter, Action<float> setter, float min, float max, Func<string> label, float roundTo = 1f)
        {
            return rect =>
            {
                float val = getter();
                string labelText = label();

                Rect labelRect = new Rect(rect.x, rect.y, rect.width, 25f);
                Rect sliderRect = new Rect(rect.x, rect.y + 25f, rect.width, 30f);
                Widgets.Label(labelRect, labelText.Bold());
                val = Widgets.HorizontalSlider(sliderRect, val, min, max);


                val = Mathf.Round(val / roundTo) * roundTo;
                setter(val);
            };
        }

        public Action<Rect> CreateRadio(Func<int> getIndex, Action<int> setIndex, string[] labels, string groupLabel)
        {
            return rect =>
            {
                float y = rect.y;

                Widgets.Label(new Rect(rect.x, y, rect.width, 25f), groupLabel.Bold());
                y += 28f;

                for (int i = 0; i < labels.Length; i++)
                {
                    var radioRect = new Rect(rect.x, y, rect.width, 25f);
                    if (Widgets.RadioButtonLabeled(radioRect, labels[i], getIndex() == i))
                    {
                        setIndex(i);
                    }
                    y += 25f;
                }
            };
        }


        public Action<Rect> CreateInfo(Func<string> text)
        {
            return rect => Widgets.Label(rect, text());
        }

        private class SettingItem
        {
            public string key;
            public string label;
            public Action<Rect> drawFunc;
            public float height;
            public List<string> linkedKeys;
        }
    }

    public static class GUIExtensions
    {
        public static string Bold(this string s)
        {
            return "<b>" + s + "</b>";
        }

        public static Rect LeftHalf(this Rect rect)
        {
            return new Rect(rect.x, rect.y, rect.width * 0.5f, rect.height);
        }

        public static Rect RightHalf(this Rect rect)
        {
            return new Rect(rect.x + rect.width * 0.5f, rect.y, rect.width * 0.5f, rect.height);
        }
    }
}

namespace Kasterfly.CombatTweaks.MainFunctions
{
    public static class CombatTweaksArmorUtility
    {
        private static HediffDef _adrenalineDef;
        public static HediffDef AdrenalineCrashDef => _adrenalineCrashDef;
        private static HediffDef _adrenalineCrashDef;
        private static DamageDef _heatDamageDef;
        private static DamageDef _hotTemperatureDamageDef;
        private static bool _defsResolved = false;

        private static void EnsureDefs()
        {
            if (_defsResolved) return;
            _adrenalineDef           = DefDatabase<HediffDef>.GetNamed("Adrenaline", false);
            _adrenalineCrashDef      = DefDatabase<HediffDef>.GetNamed("AdrenalineCrash", false);
            _heatDamageDef           = DefDatabase<DamageDef>.GetNamed("Heat", false);
            _hotTemperatureDamageDef = DefDatabase<DamageDef>.GetNamed("HotTemperature", false);
            _defsResolved = true;
        }

        public static float GetPostArmorDamage(
             Pawn pawn,
             ref float damAmount,
             float armorPenetration,
             ref DamageDef damageDef,
             out bool deflectedByMetalArmor,
             out bool diminishedByMetalArmor,
             BodyPartRecord part,
             DamageInfo dinfo
        )
        {
            CombatTweaksModSettings settings = CombatTweaksMod.Settings;
            deflectedByMetalArmor = false;
            diminishedByMetalArmor = false;

            if (pawn == null || part == null || damageDef == null)
                return damAmount;

            EnsureDefs();

            bool isPlayerPawn = pawn.Faction == Faction.OfPlayer && pawn.IsColonist;
            bool isHumanlike = pawn.RaceProps.Humanlike;
            RaceProperties race = pawn.RaceProps;

            if (settings.debugLogging)
                Log.Message($"[CombatTweaks] ----------- GetPostArmorDamage Called");

            Thing instigatorThing = dinfo.Instigator;

            // Vanilla sizes this damage so it can't kill (see NonLethalEventDamage), boosting it would make it lethal
            if (settings.respectNonLethalDamage && NonLethalEventDamage.Active)
            {
                if (settings.debugLogging)
                    Log.Message($"[CombatTweaks] Non-lethal event damage ({damageDef.defName} {damAmount}) on {pawn}, skipping damage boosts");
            }
            else
            {
                if (instigatorThing != null)
                {
                    bool isPlayerDamage = instigatorThing.Faction == Faction.OfPlayer;
                    bool isEnemyDamage = !instigatorThing.Faction.AllyOrNeutralTo(Faction.OfPlayer);

                    if (isPlayerPawn && isPlayerDamage)
                        damAmount *= settings.friendlyFireMultiplier;
                    else if (!isEnemyDamage)
                        damAmount *= settings.enemyDamageMultiplier;
                }

                damAmount *= damageDef.isRanged ? settings.rangedDamageBoost : settings.meleeDamageBoost;
                damAmount *= settings.damageBoost;
            }

            if (damageDef.armorCategory == null)
            {
                RecordArmorResult(pawn, part, false);
                return damAmount;
            }

            float armorMult = settings.armorStrength;
            float armorAdd = 0f;

            TechLevel targetBodyTech = TechLevel.Undefined;
            TechLevel attackTech = dinfo.Weapon?.techLevel ?? TechLevel.Industrial;

            if (race.IsMechanoid)
            {
                targetBodyTech = TechLevel.Ultra;
                armorAdd += (settings.mechanoidBuff-1f) / 10f;
                armorMult *= settings.mechanoidBuff;
            }
            else if (race.Insect)
            {
                armorAdd += (settings.insectBuff-1f) / 10f;
                armorMult *= settings.insectBuff;
            }
            else if (race.IsAnomalyEntity)
            {
                armorAdd += (settings.anomalyBuff-1f) / 10f;
                armorMult *= settings.anomalyBuff;
            }
            else if (isHumanlike)
            {
                armorAdd += (settings.humanLikeBuff-1f) / 10f;
                armorMult *= settings.humanLikeBuff;
            }

            armorPenetration *= settings.armorPenetrationImportance;
            StatDef armorRatingStat = damageDef.armorCategory.armorRatingStat;

            float originalDamage = damAmount;
            int result = 0, highestResult = 0;
            TechLevel targetTech = TechLevel.Undefined;
            if (pawn.apparel != null)
            {
                var apparelList = pawn.apparel.WornApparel;
                for (int i = apparelList.Count - 1; i >= 0; i--)
                {
                    Apparel armor = apparelList[i];
                    if (settings.allArmorEffectsEverywhere || armor.def.apparel.CoversBodyPart(part))
                    {
                        int layers = armor.def.apparel.layers?.Count ?? 0;
                        if (!settings.thickArmor)
                            layers = 1;
                        if (settings.debugLogging && settings.thickArmor)
                            Log.Message($"[CombatTweaks] Armor Layers: {layers}");
                        targetTech = armor.def.techLevel;
                        float before = damAmount;
                        float rating = armor.GetStatValue(armorRatingStat, true) * armorMult + armorAdd;
                        bool deflected2 = false;
                        bool metalArmor2 = false;
                        for (int x = 0; x < layers; x++)
                        {
                            damAmount = ApplyArmor(ref damAmount, ref armorPenetration, rating, armor, ref damageDef, pawn,
                                out bool metalArmor, out bool deflected, settings, dinfo, ref result, targetTech, attackTech);
                            deflected2 = deflected2 || deflected;
                            metalArmor2 = metalArmor2 || metalArmor;
                            rating *= settings.thickArmorEffectivenessLoss;
                        }
                        if (settings.doTracking)
                        {
                            var trackComp = armor.TryGetComp<CompCombatTracking>();
                            float percentReduced = (before - damAmount) / before;
                            if (trackComp != null)
                            {
                                trackComp.RecordEffect(percentReduced);
                                if (settings.debugLogging)
                                {
                                    Log.Message($"[CombatTweaks] Armor {armor.LabelCap} tracked {percentReduced:P0} damage reduction. Total uses: {trackComp.TotalUses}");
                                }
                            }
                        }
                        if (result > highestResult)
                            highestResult = result;

                        if (damAmount < 0.001f)
                        {
                            deflectedByMetalArmor = deflected2 || metalArmor2;
                            damAmount = 0f;
                            break;
                        }

                        if (damAmount < before && deflected2)
                            diminishedByMetalArmor = true;

                        if (deflected2)
                            deflectedByMetalArmor = true;

                    }
                }
            }

            float finalBefore = damAmount;
            if (damAmount > 0f)
            {
                float rating = pawn.GetStatValue(armorRatingStat, true) * armorMult + armorAdd;
                damAmount = ApplyArmor(ref damAmount, ref armorPenetration, rating, null, ref damageDef, pawn,
                    out bool metalArmor, out bool deflected, settings, dinfo, ref result, targetBodyTech, attackTech);

                if (result > highestResult)
                    highestResult = result;

                if (damAmount < finalBefore && deflected)
                    diminishedByMetalArmor = true;

                if (deflected)
                    deflectedByMetalArmor = true;
            }

            if (
                highestResult != 2 &&
                isHumanlike &&
                pawn.health != null &&
                settings.doAdrenaline
            )
            {
                float pain = pawn.health.hediffSet.PainTotal;
                if (pain > 0.15f && Rand.Value < settings.adrenalineChance)
                {
                    if (_adrenalineDef != null && _adrenalineCrashDef != null &&
                        !pawn.health.hediffSet.HasHediff(_adrenalineDef) &&
                        !pawn.health.hediffSet.HasHediff(_adrenalineCrashDef))
                    {
                        pawn.health.AddHediff(_adrenalineDef);
                        if (settings.debugLogging)
                            Log.Message($"[CombatTweaks] Adrenaline triggered for {pawn.LabelShortCap} (pain: {pain * 100f:F0}%)");
                    }
                }
            }

            if (settings.doTracking && instigatorThing != null && originalDamage > 0f)
            {
                Thing weaponThing = null;

                if (instigatorThing is Pawn p && p.equipment?.Primary != null)
                {
                    weaponThing = p.equipment.Primary;
                }
                else if (instigatorThing.def.IsWeapon)
                {
                    weaponThing = instigatorThing;
                }

                if (weaponThing != null)
                {
                    var weaponComp = weaponThing.TryGetComp<CompCombatTracking>();
                    float percentDealt = damAmount;

                    if (weaponComp != null)
                    {
                        weaponComp.RecordEffect(percentDealt);
                        if (settings.debugLogging)
                        {
                            Log.Message($"[CombatTweaks] Weapon {weaponThing.LabelCap} tracked {percentDealt:P0} damage dealt. Total uses: {weaponComp.TotalUses}");
                        }
                    }
                }
            }


            if (damAmount < 0.001f || deflectedByMetalArmor)
            {
                damAmount = 0f;
                return 0f;
            }

            RecordArmorResult(pawn, part, damAmount < originalDamage);
            return damAmount;
        }

        // Whether armor reduced the last hit, measured after this mod's damage boosts. Vanilla's "diminished" flag compares
        // against the unboosted amount, so it counts boosts/nerfs as armor. Only read by VanillaCombatOverhaulCompat_Wounds_Patch,
        // in the same ApplyDamageToPart call, so a result from another hit or tick is ignored.
        private static Pawn _lastArmorPawn;
        private static BodyPartRecord _lastArmorPart;
        private static int _lastArmorTick = -1;
        private static bool _lastArmorReduced;

        private static void RecordArmorResult(Pawn pawn, BodyPartRecord part, bool reduced)
        {
            if (!VanillaCombatOverhaulCompat.Active)
                return;
            _lastArmorPawn = pawn;
            _lastArmorPart = part;
            _lastArmorTick = Find.TickManager?.TicksGame ?? -1;
            _lastArmorReduced = reduced;
        }

        public static bool TryTakeArmorResult(Pawn pawn, BodyPartRecord part, out bool reduced)
        {
            reduced = _lastArmorReduced;
            bool match = _lastArmorPawn == pawn && _lastArmorPart == part && _lastArmorTick == (Find.TickManager?.TicksGame ?? -1);
            _lastArmorPawn = null;
            _lastArmorPart = null;
            return match;
        }


        public static float ApplyArmor(
            ref float damAmount,
            ref float armorPenetrationValue,
            float armorRating,
            Thing armorThing,
            ref DamageDef damageDef,
            Pawn pawn,
            out bool metalArmor,
            out bool forcedDefl,
            CombatTweaksModSettings settings,
            DamageInfo dinfo,
            ref int result,
            TechLevel targetTechLevel,
            TechLevel damageTechLevel
        )
        {
            metalArmor = false || pawn.RaceProps.IsMechanoid;
            forcedDefl = false;
            float armorPenetration = armorPenetrationValue;


            if (settings.vanillaCombatReloadedCompatibility && VanillaCombatReloadedCompat.AArmor)
            {
                if (settings.debugLogging)
                {
                    Log.Message($"[CombatTweaks] -- VanillaCombatReloadedCompat effects: armor rating {armorRating} -> {armorRating * VanillaCombatReloadedCompat.AArmorScale} | armor penetration {armorPenetration} -> {armorPenetration * VanillaCombatReloadedCompat.AArmorScale}");

                }
                armorRating *= VanillaCombatReloadedCompat.AArmorScale;
                armorPenetration *= VanillaCombatReloadedCompat.AArmorScale;
            }

            // Same as VCO's prefix on vanilla's ApplyArmor, which never runs since this mod replaces GetPostArmorDamage
            if (VanillaCombatOverhaulCompat.Active)
            {
                if (settings.vcoUseAdvancedArmor && VanillaCombatOverhaulCompat.TryGetArmorScale(out float vcoArmorScale))
                {
                    if (settings.debugLogging)
                        Log.Message($"[CombatTweaks] -- Vanilla Combat Overhaul Advanced Armor (x{vcoArmorScale}): armor rating {armorRating} -> {armorRating * vcoArmorScale} | armor penetration {armorPenetration} -> {armorPenetration * vcoArmorScale}");
                    armorRating *= vcoArmorScale;
                    armorPenetration *= vcoArmorScale;
                }
                else if (settings.debugLogging)
                {
                    Log.Message($"[CombatTweaks] -- Vanilla Combat Overhaul compat: armor rating {armorRating} | armor penetration {armorPenetration} ({VanillaCombatOverhaulCompat.DescribePenetration()})");
                }
            }

            if (settings.ignoreWeakArmor && armorRating < settings.ignoreWeakArmorThreshold && !settings.ignoreWeakArmorStillTakeDurabilityDamage)
            {
                if (settings.debugLogging)
                    Log.Message($"[CombatTweaks] ----------- That's Not Armor! activated -Skipped...");

                result = 0;
                return damAmount;
            }

            int techLevelDiff = (int)targetTechLevel - (int)damageTechLevel;
            float techLevelMultiplier = (techLevelDiff * settings.techLevelBoost);
            bool flag = false;
            float yayoArmorPenetration = armorPenetration;
            float startDamage = damAmount;




            if (armorThing != null)
            {
                targetTechLevel = armorThing.def.techLevel;
                flag = true;
                bool flag2;
                if (!armorThing.def.apparel.useDeflectMetalEffect)
                {
                    ThingDef stuff = armorThing.Stuff;
                    flag2 = (stuff != null && stuff.IsMetal);
                }
                else
                {
                    flag2 = true;
                }

                metalArmor = flag2;


                if (targetTechLevel == TechLevel.Undefined && armorThing.Stuff != null)
                {
                    targetTechLevel = armorThing.Stuff.techLevel;
                }
            }

            if (settings.debugLogging)
                Log.Message($"[CombatTweaks] ----------- Before ApplyArmor() edits start: [Damage Amount: {damAmount}], [Armor Penetration: {armorPenetration}], [Armor Rating: {armorRating}], " +
                    $"[Target Pawn: {pawn?.LabelShortCap ?? "null"}], [Armor Item: {armorThing?.LabelCap ?? "natural"}], [Damage Type: {damageDef?.defName ?? "null"}]," +
                    $" [isMetal: {metalArmor}] | " + settings.ToString());


            if (settings.doTechLevelEffect)
            {
                armorRating *= (1 + techLevelMultiplier * 0.9f);
                armorPenetration *= (1 - techLevelMultiplier * 0.1f);
            }
            else if (settings.yayoCombatCompatEnableAAA)
            {
                if (flag && dinfo.Weapon != null)
                {
                    if (armorThing.def.techLevel >= TechLevel.Spacer || pawn.RaceProps.IsMechanoid)
                    {
                        if (dinfo.Weapon.IsMeleeWeapon)
                        {
                            if (dinfo.Weapon.techLevel <= TechLevel.Medieval)
                            {
                                yayoArmorPenetration *= 0.5f;
                            }
                        }
                        else if (dinfo.Weapon.techLevel <= TechLevel.Medieval)
                        {
                            yayoArmorPenetration *= 0.35f;
                        }
                    }
                    else if (armorThing.def.techLevel >= TechLevel.Industrial)
                    {
                        if (dinfo.Weapon.IsMeleeWeapon)
                        {
                            if (dinfo.Weapon.techLevel <= TechLevel.Neolithic)
                            {
                                yayoArmorPenetration *= 0.75f;
                            }
                        }
                        else if (dinfo.Weapon.techLevel <= TechLevel.Medieval)
                        {
                            yayoArmorPenetration *= 0.5f;
                        }
                    }
                }
            }

            float yayoNum2 = Mathf.Max(armorRating - yayoArmorPenetration, 0f);
            float yayoNum3 = (yayoArmorPenetration - armorRating * 0.15f) * 5f;
            yayoNum3 = Mathf.Clamp01(yayoNum3);

            if (settings.debugLogging)
                Log.Message($"[CombatTweaks] (Durability Effects Armor) - PostTechLevel: [Armor Rating: {armorRating}], [armorPenetration: {armorPenetration}], [techLevelMultiplier: {techLevelMultiplier}]");
            float currentHP = 0;
            float maxHP = 0;
            if (armorThing != null)
            {
                currentHP = (float)armorThing.HitPoints;
                maxHP = (float)armorThing.MaxHitPoints;
            }

            if (settings.durabilityEffectsArmor)
            {
                if (settings.debugLogging)
                    Log.Message($"[CombatTweaks] (Durability Effects Armor) - PreEffectInfo: [Armor Rating: {armorRating}]");

                float DEM  = settings.durabilityEffectsArmorMultiplier*100;
                if (maxHP != 0)
                {
                    if (settings.durabilityEffectsArmorDoTattered && maxHP / 2 >= currentHP)
                        DEM *= 3;
                    if (currentHP / maxHP >= 0)
                    {
                        if (settings.debugLogging)
                            Log.Message($"[CombatTweaks] Modified Stats (Durability Effects Armor) - Info: [Armor Rating: {armorRating}], Item health ratio: {currentHP / maxHP}");

                        armorRating *= Mathf.Clamp01(1f - ((1f - (currentHP / maxHP)) * DEM));
                    }
                    else
                    {
                        if (settings.debugLogging)
                            Log.Message($"[CombatTweaks] Modified Stats (Durability Effects Armor) - Info: Skipped because durability does not exist");

                    }

                }
            }




            float DurabilityMultiplier = settings.itemDurabilityMultiplier;

            if (armorThing != null && !settings.armorDeflectionRework)
            {
               

                if (settings.durabilityTypeInteraction)
                {
                    bool isBlunt = damageDef == DamageDefOf.Blunt;
                    bool isSharp = damageDef.armorCategory == DamageArmorCategoryDefOf.Sharp;

                    if ((isBlunt && metalArmor) || (isSharp && !metalArmor))
                    {
                        DurabilityMultiplier *= settings.durabilityTypeInteractionMultiplier;
                    }
                }

                if (damageDef == DamageDefOf.Flame || damageDef == DamageDefOf.Burn ||
                    damageDef == _heatDamageDef || damageDef == _hotTemperatureDamageDef)
                {
                    DurabilityMultiplier *= settings.bonusHeatDamageToDurability;
                }

                float f = damAmount * 0.25f * DurabilityMultiplier;
                if (settings.yayoCombatCompatEnableAAA)
                    f = damAmount * (0.2f + yayoNum3 * 0.5f) * DurabilityMultiplier;


                if (settings.debugLogging)
                    Log.Message($"[CombatTweaks] Info: [Item Durability Loss Multiplier: {settings.itemDurabilityMultiplier}], [Item Durability Loss: 0-{f}]");


                if (f > 0.5f)
                    armorThing.TakeDamage(new DamageInfo(damageDef, (float)GenMath.RoundRandom(f)));

                if (settings.ignoreWeakArmor && armorRating < settings.ignoreWeakArmorThreshold)
                {
                    metalArmor = false;
                    forcedDefl = false;
                    result = 0;
                    return damAmount;

                }
            }

            float yayoNum4 = (!flag) ? pawn.health.summaryHealth.SummaryHealthPercent : ((float)armorThing.HitPoints / (float)armorThing.MaxHitPoints);
            float yayoNum5 = Mathf.Max(armorRating * 0.9f - yayoArmorPenetration, 0f);
            float yayoNum6 = 1f - settings.yayoCombatArmorEffectiveness;

            if (settings.maxArmorAmount < 20.05f && armorRating > settings.maxArmorAmount)
            {
                armorRating = settings.maxArmorAmount;
            }
            float effectiveArmorRating = Mathf.Max(armorRating - armorPenetration, 0f);



            bool changeToBlunt = (metalArmor && settings.sharpConversionMode == SharpConversionMode.OnlyMetal) ||
                settings.sharpConversionMode == SharpConversionMode.Vanilla ||
                (effectiveArmorRating > settings.sharpToBluntThreshold && settings.sharpConversionMode == SharpConversionMode.ArmorThreshold);


            if (settings.armorDeflectionRework && armorThing != null)
            {
                float durabilityDamage = 0;
                if (effectiveArmorRating > 0f)
                {
                    durabilityDamage = damAmount * DurabilityMultiplier / Mathf.Max(effectiveArmorRating, 1f);
                }
                else
                {
                    durabilityDamage = damAmount * DurabilityMultiplier * (1 + armorPenetration);
                }


                if (currentHP > maxHP * settings.armorDeflectionReworkThreshold)
                {
                    damAmount = 0f;
                    result = 2;
                }
                else
                {
                    durabilityDamage *= currentHP / maxHP;
                    damAmount = (float)GenMath.RoundRandom(damAmount * currentHP / maxHP);
                    result = 1;

                }
                if (durabilityDamage > 0.5f)
                    armorThing.TakeDamage(new DamageInfo(damageDef, (float)GenMath.RoundRandom(durabilityDamage)));

                if (damageDef.armorCategory == DamageArmorCategoryDefOf.Sharp && changeToBlunt)
                {
                    damageDef = DamageDefOf.Blunt;
                }
            }

            if (settings.armorDeflectionRework)
            {

                if (settings.debugLogging)
                    Log.Message($"[CombatTweaks] ApplyArmor() ENDED! Info: [Damage Amount: {damAmount}], [Armor Penetration: {armorPenetration}]" +
                    $", [Armor Rating: {armorRating}], [Effective Armor Rating: {effectiveArmorRating}], [Armor Item: {armorThing?.LabelCap ?? "natural"}]");

                return damAmount;
            }

            float value = Rand.Value;
            float DeflectionModifier = 1.0f;
            float DeflectionCap = 1.0f;
            float newArmorPen = armorPenetration;

            float defChance = -999999998f;
            float redChance = -999999998f;
            if (settings.armorTiers)
            {
                int armorTier = (int)Math.Floor((-15.0f + Mathf.Sqrt(225.0f + 8.0f * (armorRating * 100))) / 4.0f) + 1;
                float discriminant = 3.5f * 3.5f - 4f * 0.75f * (0.75f - (armorPenetration * 100));
                if (discriminant < 0f)
                    discriminant = 0;
                int apTier = Mathf.FloorToInt((-3.5f + Mathf.Sqrt(discriminant)) / (2f * 0.75f));
                if (settings.debugLogging)
                    Log.Message($"[CombatTweaks] Armor Tiers Tier Checking: [Weapon Tier: T{apTier} - {armorPenetration * 100}%], [Armor Tier: T{armorTier} - {armorRating * 100}%]");

                if (armorTier - 1.01 > apTier)
                {
                    damAmount = 0;
                    if (settings.debugLogging)
                        Log.Message($"[CombatTweaks] (Guaranteed Deflection) ArmorUtility.ApplyArmor ENDED! Info: [Damage Amount: {damAmount}], [Armor Penetration: {armorPenetration}]" +
                        $", [Armor Rating: {armorRating}], [Effective Armor Rating: {effectiveArmorRating}], [Armor Item: {armorThing?.LabelCap ?? "natural"}]");
                    result = 2;
                    return 0;
                }
                else if (apTier - 1.01 > armorTier)
                {
                    if (settings.debugLogging)
                        Log.Message($"[CombatTweaks]  (Guaranteed Full Damage) ArmorUtility.ApplyArmor ENDED! Info: [Damage Amount: {damAmount}], [Armor Penetration: {armorPenetration}]" +
                        $", [Armor Rating: {armorRating}], [Effective Armor Rating: {effectiveArmorRating}], [Armor Item: {armorThing?.LabelCap ?? "natural"}]");
                    result = 0;
                    return damAmount;
                }
                defChance = Mathf.Clamp01((2 + armorTier - apTier) * 0.25f);
                redChance = Mathf.Clamp01((2 + armorTier - apTier) * 0.5f);
            }
            else if (settings.finalArmorCalculations == FinalArmorCalculations.Yayo)
            {
                if (value * yayoNum6 < yayoNum5 * yayoNum4)
                {
                    if (Rand.Value < Mathf.Min(yayoNum2, 0.9f) * settings.deflectionChanceMultiplier)
                    {
                        damAmount = 0f;
                        if (settings.debugLogging)
                            Log.Message($"[CombatTweaks] ----------- After ApplyArmor() edits ends: [Damage Amount: {damAmount}], [Armor Penetration: {armorPenetration}], [Armor Rating: {armorRating}], " +
                                $" [isMetal: {metalArmor}]");
                        result = 2;
                        return damAmount;
                    }
                    if (flag)
                    {
                        forcedDefl = true;
                        damAmount = (float)GenMath.RoundRandom(damAmount * (0.25f + yayoNum3 * 0.25f));
                        if (damageDef.armorCategory == DamageArmorCategoryDefOf.Sharp && changeToBlunt)
                        {
                            damageDef = DamageDefOf.Blunt;
                        }
                    }
                    else
                    {
                        forcedDefl = true;
                        damAmount = (float)GenMath.RoundRandom(damAmount * (0.25f + yayoNum3 * 0.5f));
                        if (damageDef.armorCategory == DamageArmorCategoryDefOf.Sharp && changeToBlunt)
                        {
                            damageDef = DamageDefOf.Blunt;
                        }
                    }
                }
                else if (value < yayoNum2 * (0.5f + yayoNum4 * 0.5f) * settings.reductionChanceMultiplier)
                {
                    damAmount = (float)GenMath.RoundRandom(damAmount * 0.5f);
                    if (damageDef.armorCategory == DamageArmorCategoryDefOf.Sharp)
                    {
                        damageDef = DamageDefOf.Blunt;
                    }
                }
                result = 2;
                if (settings.alwaysReduceDamage && effectiveArmorRating > 0f)
                {
                    damAmount = Mathf.Max(damAmount * 0.9f, damAmount - 3);
                    result = 1;
                    if (settings.debugLogging)
                        Log.Message($"[CombatTweaks] Modified Stats (Almost Always Reduce Damage) - Info: [Damage Amount: {damAmount}]");
                }
                if (settings.debugLogging)
                    Log.Message($"[CombatTweaks] ----------- After ApplyArmor() edits ends: [Damage Amount: {damAmount}], [Armor Penetration: {armorPenetration}], [Armor Rating: {armorRating}], " +
                        $" [isMetal: {metalArmor}]");

                return damAmount;
            }
            else if (settings.finalArmorCalculations == FinalArmorCalculations.Custom)
            {
                newArmorPen = Math.Min(0.0f, armorPenetration - Math.Max(effectiveArmorRating * 0.25f, armorPenetration * 0.05f));
                if (damageDef.armorCategory == DamageArmorCategoryDefOf.Sharp)
                {
                    DeflectionModifier *= Mathf.Min(effectiveArmorRating * 0.5f * DeflectionModifier, DeflectionCap) * settings.deflectionChanceMultiplier;
                }
                else
                {
                    newArmorPen = Math.Min(0.0f, armorPenetration - Math.Max(effectiveArmorRating * 0.5f, armorPenetration * 0.1f));
                }
            }



            if (defChance < -9999f || redChance < -9999f)
            {
                defChance = Mathf.Min(effectiveArmorRating * 0.5f * DeflectionModifier, DeflectionCap);
                redChance = effectiveArmorRating;
            }
            defChance *= settings.deflectionChanceMultiplier;
            redChance *= settings.reductionChanceMultiplier;
            if (settings.reliableArmors) {
                redChance = 2f;
            }


            if (settings.debugLogging)
                Log.Message($"[CombatTweaks] Info: [Deflection Chance: {defChance}], [damage reduction chance: {redChance}]");


            if (value < defChance)
            {
                damAmount = 0f;
                result = 2;
            }
            else if (value < redChance)
            {
                float damReduction = settings.reductionAmountMultiplier;
                if (settings.debugLogging)
                    Log.Message($"[CombatTweaks] ----------- Damage Reduction Amount: {Mathf.Clamp01(damReduction) * 100:F0}%");

                if (settings.reliableArmors)
                {
                    damReduction *= (armorRating - armorPenetration) * Rand.Range(0.7f, 2f);
                    if (settings.debugLogging)
                        Log.Message($"[CombatTweaks] ----------- Damage Reduction Amount (After Reliable Armors): {Mathf.Clamp01(damReduction) * 100:F0}%");
                }

                if (settings.doDamageEffect && damAmount > 0f)
                {
                    if(armorPenetration < 0.01f)
                    {
                        armorPenetration = 0.01f;
                    }
                    damReduction *= Mathf.Clamp(((armorRating * 12) / (armorPenetration * damAmount)), 0.4f, 1.6f);
                    if (settings.debugLogging)
                        Log.Message($"[CombatTweaks] ----------- Damage Reduction Amount (Damage Effect): {Mathf.Clamp01(damReduction) * 100:F0}%");
                }

                

                damAmount *= (1-Mathf.Clamp01(damReduction));
                if (damAmount < 0.5f)
                    damAmount = 0f;

                if (damageDef.armorCategory == DamageArmorCategoryDefOf.Sharp && changeToBlunt)
                {
                    damageDef = DamageDefOf.Blunt;
                }
                result = 1;
            } else if (settings.alwaysReduceDamage && effectiveArmorRating > 0f)
            {
                damAmount = Mathf.Min(Mathf.Max(damAmount * 1.8f * settings.reductionAmountMultiplier, damAmount * 0.95f), damAmount - 3);
                if (settings.debugLogging)
                    Log.Message($"[CombatTweaks] Modified Stats (Almost Always Reduce Damage) - Info: [Damage Amount: {damAmount}]");
            }
            if (settings.debugLogging)
                Log.Message($"[CombatTweaks] ----------- After ApplyArmor() edits ends: [Damage Amount: {damAmount}], [Armor Penetration: {armorPenetration}], [Armor Rating: {armorRating}], " +
                    $" [isMetal: {metalArmor}]");
            armorPenetration = newArmorPen;
            return damAmount;
        }
    }

    public static class CustomArmorUtility
    {
        public static float CalculatePostArmorDamage(Pawn pawn, float amount, float armorPenetration, BodyPartRecord part, ref DamageDef damageDef, ref bool deflectedByMetalArmor, ref bool diminishedByMetalArmor, DamageInfo dinfoOrNull)
        {

            DamageInfo dinfo;
            if (dinfoOrNull is DamageInfo setDInfo)
            {
                dinfo = new DamageInfo(setDInfo);
                if (CombatTweaksMod.Settings.debugLogging)
                    Log.Message($"[CombatTweaks] ----------- dinfo toString(): {dinfo}");
            }
            else
            {
                dinfo = new DamageInfo(damageDef, (float)GenMath.RoundRandom(amount));
                dinfo.SetHitPart(part);
                dinfo.SetAmount(amount);
                if (CombatTweaksMod.Settings.debugLogging)
                    Log.Message($"[CombatTweaks] ----------- dinfo is missing, creating a generic one instead");
            }

            if (CombatTweaksMod.Settings.debugLogging)
                Log.Message($"[CombatTweaks] ----------- DamageWorker_AddInjury Called");

            float result = CombatTweaksArmorUtility.GetPostArmorDamage(pawn, ref amount, armorPenetration, ref damageDef, out deflectedByMetalArmor, out diminishedByMetalArmor, part, dinfo);
            return result;

        }

    }
}

namespace Kasterfly.CombatTweaks.Hediff
{
    public class Adrenaline : HediffWithComps
    {
        public override void PostRemoved()
        {
            base.PostRemoved();

            if (pawn != null && !pawn.Dead && pawn.Map != null)
            {
                HediffDef crashDef = CombatTweaksArmorUtility.AdrenalineCrashDef;
                if (crashDef != null && !pawn.health.hediffSet.HasHediff(crashDef))
                {
                    pawn.health.AddHediff(crashDef);
                }
            }
        }
    }
}

namespace Kasterfly.CombatTweaks.Stats
{
    public class StatWorker_CombatTweaks : StatWorker
    {
        public override bool ShouldShowFor(StatRequest req)
        {
            return CombatTweaksMod.Settings.doTracking && req.HasThing;
        }

        public override float GetValueUnfinalized(StatRequest req, bool applyPostProcess = true)
        {
            if (!CombatTweaksMod.Settings.doTracking || req.Thing == null)
                return 0f;

            var comp = req.Thing.TryGetComp<CompCombatTracking>();
            if (comp == null || comp.TotalUses <= 1 || comp.TrackingValue < 0.001f)
                return 0f;

            float avg = comp.TrackingValue / comp.TotalUses;

            if (req.Thing.def.IsApparel)
                return avg * 100f;
            else
                return avg;
        }

        public override string GetExplanationUnfinalized(StatRequest req, ToStringNumberSense numberSense)
        {
            if (!CombatTweaksMod.Settings.doTracking)
                return "Stat tracking is disabled.";

            var comp = req.Thing?.TryGetComp<CompCombatTracking>();
            if (comp == null || comp.TotalUses <= 1)
                return "Not enough combat data recorded.";

            float average = comp.TotalUses > 0 ? comp.TrackingValue / comp.TotalUses : 0f;
            if (average < 0.005f)
                average = 0f;

            string usesString = comp.TotalUses > 10000 ? "over 10000" : comp.TotalUses.ToString();

            if (req.Thing.def.IsApparel)
                return $"Average damage prevented: {average * 100f:F1}% across {usesString} hits.";
            else
                return $"Average damage inflicted: {average:F1} points across {usesString} attacks.";
        }
    }
}

namespace Kasterfly.CombatTweaks.Comps
{
    public class CompProperties_CombatTracking : CompProperties
    {
        public CompProperties_CombatTracking()
        {
            this.compClass = typeof(CompCombatTracking);
        }
    }

    public class CompCombatTracking : ThingComp
    {
        private float totalEffect = 0f;
        private int uses = 0;

        public float TrackingValue => totalEffect;
        public int TotalUses => uses;

        public string Explanation => uses > 0
            ? $"Average effectiveness based on {uses} tracked instance(s)."
            : "Not enough combat data to evaluate item performance.";

        public void RecordEffect(float value)
        { 
            if (uses < 10000)
            {
                totalEffect += value;
                uses++;
            }
            else
                totalEffect += (value - totalEffect) * 0.001f;
            
        }

        public override void PostExposeData()
        {
            Scribe_Values.Look(ref totalEffect, "totalEffect", 0f);
            Scribe_Values.Look(ref uses, "uses", 0);
        }
    }
}

namespace Kasterfly.CombatTweaks.HarmonyPatches
{

    [HarmonyPatch(typeof(DamageWorker_AddInjury), "ApplyDamageToPart")]
    public static class DamageWorker_AddInjury_ApplyDamageToPart_Patch
    {
        [HarmonyPriority(Priority.First)]
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator il)
        {
            var codes = new List<CodeInstruction>(instructions);

            try
            {
                MethodInfo targetMethod = AccessTools.Method(typeof(ArmorUtility), "GetPostArmorDamage");
                MethodInfo replacementMethod = AccessTools.Method(typeof(CustomArmorUtility), "CalculatePostArmorDamage");

                if (targetMethod == null || replacementMethod == null)
                {
                    return instructions;
                }

                for (int i = 0; i < codes.Count; i++)
                {
                    if (codes[i].Calls(targetMethod))
                    {
                        codes.Insert(i, new CodeInstruction(OpCodes.Ldarg_1));
                        i++;
                        codes[i] = new CodeInstruction(OpCodes.Call, replacementMethod);


                        break;
                    }
                }

                return codes;
            }
            catch (Exception ex)
            {
                if (CombatTweaksMod.Settings.debugLogging)
                    Log.Warning($"[CombatTweaks] Transpiler for ApplyDamageToPart failed. Reverting to vanilla. Error: {ex}");
                return instructions;
            }
        }
    }

    // Vanilla deals some damage sized to down a pawn without killing it: it picks an amount just under the
    // part's health and checks WouldDieAfterAddingHediff with that unboosted amount before applying it
    // (Metalhorror emerging from its host, refugee pods/quests, ancient cryptosleep pods, etc.).
    // While one of these runs, Active is true so GetPostArmorDamage can skip the damage boosts.
    public static class NonLethalEventDamage
    {
        private static int depth;
        public static bool Active => depth > 0;

        public static void Enter() => depth++;
        public static void Exit() => depth--;
    }

    [HarmonyPatch]
    public static class NonLethalEventDamage_Patch
    {
        static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(HealthUtility), nameof(HealthUtility.DamageUntilDowned));
            yield return AccessTools.Method(typeof(HealthUtility), nameof(HealthUtility.DamageLegsUntilIncapableOfMoving));
            yield return AccessTools.Method(typeof(HealthUtility), nameof(HealthUtility.DamageLimbsUntilIncapableOfManipulation));
            // Covers the already-downed branch too, which deals a flat 20 Cut instead of DamageUntilDowned
            yield return AccessTools.Method(typeof(Hediff_MetalhorrorImplant), nameof(Hediff_MetalhorrorImplant.Emerge));
        }

        static void Prefix(out bool __state)
        {
            NonLethalEventDamage.Enter();
            __state = true;
        }

        // Finalizer runs even if the original throws, so the flag can't get stuck on
        static void Finalizer(bool __state)
        {
            if (__state)
                NonLethalEventDamage.Exit();
        }
    }

    [StaticConstructorOnStartup]
    public static class ArmorCapInitializer
    {
        static ArmorCapInitializer()
        {
            ApplyArmorCapFromSettings();
        }

        public static void ApplyArmorCapFromSettings()
        {
            float settingValue = CombatTweaksMod.Settings.maxArmorAmount;
            float cap = settingValue > 20f ? float.PositiveInfinity : settingValue;
            StatDefOf.ArmorRating_Sharp.maxValue = cap;
            StatDefOf.ArmorRating_Blunt.maxValue = cap;
            StatDefOf.ArmorRating_Heat.maxValue  = cap;
            if(CombatTweaksMod.Settings.debugLogging)
                Log.Message($"[CombatTweaks] Updated armor cap to {Mathf.Round(cap*100)}%");
        }
    }


    [StaticConstructorOnStartup]
    public static class YayoCombat3Compat
    {
        static YayoCombat3Compat()
        {

            new Harmony("kasterfly.combattweaks").Patch(
                original: AccessTools.Method(typeof(Game), nameof(Game.FinalizeInit)),
                postfix: new HarmonyMethod(typeof(YayoCombat3Compat), nameof(DoYayoUnpatch))
            );
        }

        public static void DoYayoUnpatch()
        {
            var harmony = new Harmony("kasterfly.combattweaks.unpatcher");
            MethodInfo target = AccessTools.Method(typeof(DamageWorker_AddInjury), "ApplyDamageToPart");

            var patchInfo = Harmony.GetPatchInfo(target);
            if (patchInfo == null) return;

            bool unpatched = false;

            foreach (var prefix in patchInfo.Prefixes.Where(p => p.owner.ToLowerInvariant().Contains("yayoscombat")))
            {
                harmony.Unpatch(target, HarmonyPatchType.Prefix, prefix.owner);
                unpatched = true;
            }

            foreach (var transpiler in patchInfo.Transpilers.Where(p => p.owner.ToLowerInvariant().Contains("yayoscombat")))
            {
                harmony.Unpatch(target, HarmonyPatchType.Transpiler, transpiler.owner);
                unpatched = true;
            }

            if (unpatched)
            {
                CombatTweaksMod.Settings.yayoCombatCompat = true;
                Log.Message("[CombatTweaks] Yayo's Combat 3 patch found and unpatched successfully.");
            }
            else
            {
                if (Current.ProgramState == ProgramState.Playing)
                    Log.Message("[CombatTweaks] Yayo's Combat 3 patch not found or already removed.");
            }
        }

    }

    [StaticConstructorOnStartup]
    public static class VanillaCombatReloadedCompat
    {
        public static bool present = false;
        public static bool AArmor = false;
        public static float AArmorScale = 1f;

        static VanillaCombatReloadedCompat()
        {
            var t = AccessTools.TypeByName("VCR.ArmorUtility_ApplyArmor_Patch");
            present = t != null;
            if (!present) return;
            try
            {
                var fEnable = AccessTools.Field(t, "AArmor");
                var fScale  = AccessTools.Field(t, "AArmorScale");
                if (fEnable != null) AArmor = (bool) (fEnable.GetValue(null) ?? false);
                if (fScale  != null) AArmorScale = (float)(fScale.GetValue(null) ?? 1f);
                if (CombatTweaksMod.Settings.debugLogging)
                    Log.Message($"[CombatTweaks] Vanilla Combat Reloaded Detected: AArmor: {AArmor}, AArmorScale: {AArmorScale}");
            }
            catch { }

        }

        public static void RefreshIfNeeded()
        {
            if (!present) return;
            var t = AccessTools.TypeByName("VCR.ArmorUtility_ApplyArmor_Patch");
            if (t == null) { present = false; return; }
            try
            {
                var fEnable = AccessTools.Field(t, "AArmor");
                var fScale  = AccessTools.Field(t, "AArmorScale");
                if (fEnable != null) AArmor = (bool)(fEnable.GetValue(null) ?? false);
                if (fScale  != null) AArmorScale = (float)(fScale.GetValue(null) ?? 1f);
                if (CombatTweaksMod.Settings.debugLogging)
                    Log.Message($"[CombatTweaks] Refreshed Vanilla Combat Reloaded Values: AArmor: {AArmor}, AArmorScale: {AArmorScale}");
            }
            catch { }
        }
    }

    // Vanilla Combat Overhaul (cruesoe.vanillacombatoverhaul). Its settings are read live since they can change mid-game.
    // Its "Advanced Armor" multiplies armor penetration by penetrationScale where it's calculated (weapons, projectiles, extra
    // damage, also what the info card shows), then armor and penetration by armorScale in a prefix on vanilla's ApplyArmor.
    // That prefix never runs since this mod replaces GetPostArmorDamage, so either the penetration scaling is removed again
    // (VanillaCombatOverhaulCompat_Patches.PenetrationPostfix) or ApplyArmor applies armorScale itself (vcoUseAdvancedArmor).
    public static class VanillaCombatOverhaulCompat
    {
        public static readonly bool present;

        // VCO's own methods are patched at startup (VanillaCombatOverhaulCompatStartup), not in the mod constructor,
        // so none of VCO's classes get touched before its defs and textures are loaded
        private static readonly List<MethodBase> penetrationMethods = new List<MethodBase>();
        private static readonly MethodInfo rawPenetration;
        private static readonly MethodInfo woundsPrefix;
        private static readonly MethodInfo burstShotCountFor;
        private static readonly MethodInfo modeForDistance;
        private static readonly List<string> missing = new List<string>();
        private static bool penetrationPatched;
        private static int patchedCount;

        private static readonly PropertyInfo settingsProperty;
        private static readonly FieldInfo fAdvancedArmor, fArmorScale, fPenetrationScale, fBulletWounds, fArrowWounds, fFireModes;

        static VanillaCombatOverhaulCompat()
        {
            Type modType = AccessTools.TypeByName("VanillaCombatOverhaul.VCOMod");
            present = modType != null;
            if (!present) return;

            settingsProperty = AccessTools.Property(modType, "Settings");
            Type settingsType = AccessTools.TypeByName("VanillaCombatOverhaul.VCOSettings");
            fAdvancedArmor = AccessTools.Field(settingsType, "enableAdvancedArmor");
            fArmorScale = AccessTools.Field(settingsType, "armorScale");
            fPenetrationScale = AccessTools.Field(settingsType, "penetrationScale");
            fBulletWounds = AccessTools.Field(settingsType, "enableBulletWorker");
            fArrowWounds = AccessTools.Field(settingsType, "enableArrowWorker");
            fFireModes = AccessTools.Field(settingsType, "enableFireModes");
            if (settingsProperty == null || fAdvancedArmor == null || fArmorScale == null || fPenetrationScale == null)
                missing.Add("VCO's armor settings");

            try
            {
                // The same methods VCO scales, so the two patches cancel out exactly
                MethodInfo targetMethods = AccessTools.Method(AccessTools.TypeByName("VanillaCombatOverhaul.Patch_ArmorPenetration"), "TargetMethods");
                if (targetMethods?.Invoke(null, null) is IEnumerable<MethodBase> methods)
                    penetrationMethods.AddRange(methods.Where(m => m != null));
            }
            catch (Exception ex)
            {
                Log.Warning($"[CombatTweaks] Could not read Vanilla Combat Overhaul's armor penetration methods: {ex}");
            }

            rawPenetration = AccessTools.Method(AccessTools.TypeByName("VanillaCombatOverhaul.SuppressionUtility"), "RawPenetration",
                new[] { typeof(float) });
            woundsPrefix = AccessTools.Method(AccessTools.TypeByName("VanillaCombatOverhaul.Patch_DamageWorker_AddInjury_ApplySpecialEffectsToPart"), "Prefix",
                new[] { typeof(DamageWorker_AddInjury), typeof(Pawn), typeof(float), typeof(DamageInfo), typeof(DamageWorker.DamageResult) });
            Type fireModeUtility = AccessTools.TypeByName("VanillaCombatOverhaul.FireModeUtility");
            burstShotCountFor = AccessTools.Method(fireModeUtility, "BurstShotCountFor", new[] { typeof(Verb), typeof(int) });
            modeForDistance = AccessTools.Method(fireModeUtility, "ModeForDistance", new[] { typeof(float) });
        }

        public static bool Active => present && CombatTweaksMod.Settings != null && CombatTweaksMod.Settings.vanillaCombatOverhaulCompatibility;

        private static object VcoSettings
        {
            get
            {
                try { return settingsProperty?.GetValue(null, null); }
                catch { return null; }
            }
        }

        private static T Read<T>(FieldInfo field, T fallback)
        {
            object settings = VcoSettings;
            if (settings == null || field == null) return fallback;
            try { return (T)field.GetValue(settings); }
            catch { return fallback; }
        }

        private static bool VcoAdvancedArmor => Read(fAdvancedArmor, false);
        private static float VcoArmorScale => Read(fArmorScale, 1f);
        private static float VcoPenetrationScale => Read(fPenetrationScale, 1f);

        // Same conditions VCO checks before scaling penetration
        public static bool TryGetPenetrationScaleToUndo(out float scale)
        {
            scale = 1f;
            if (!Active || !penetrationPatched || CombatTweaksMod.Settings.vcoUseAdvancedArmor || !VcoAdvancedArmor)
                return false;
            scale = VcoPenetrationScale;
            return scale > 0f && !Mathf.Approximately(scale, 1f);
        }

        // Same conditions VCO checks before scaling armor and penetration in vanilla's ApplyArmor
        public static bool TryGetArmorScale(out float scale)
        {
            scale = 1f;
            if (!VcoAdvancedArmor)
                return false;
            scale = VcoArmorScale;
            return scale > 0f && !Mathf.Approximately(scale, 1f);
        }

        public static string DescribePenetration()
        {
            if (!VcoAdvancedArmor)
                return "VCO's Advanced Armor is off, nothing to adjust";
            if (!Active)
                return $"compatibility off, this mod's armor uses VCO's x{VcoPenetrationScale} penetration without VCO's armor scaling";
            if (CombatTweaksMod.Settings.vcoUseAdvancedArmor)
                return $"using VCO's Advanced Armor: penetration x{VcoPenetrationScale} at the source, armor and penetration x{VcoArmorScale} at the armor check";
            if (TryGetPenetrationScaleToUndo(out float scale))
                return $"VCO's x{scale} armor penetration scaling removed at the source";
            return penetrationPatched
                ? $"VCO's penetration scale is x{VcoPenetrationScale}, nothing to remove"
                : "VCO's armor penetration scaling could not be removed, see the startup log";
        }

        public static string Describe()
        {
            CombatTweaksModSettings s = CombatTweaksMod.Settings;
            return $"compatibility {OnOff(s.vanillaCombatOverhaulCompatibility)} | " +
                   $"VCO: Advanced Armor {OnOff(VcoAdvancedArmor)} (armor x{VcoArmorScale}, penetration x{VcoPenetrationScale}), " +
                   $"bullet wounds {OnOff(Read(fBulletWounds, false))}, arrow wounds {OnOff(Read(fArrowWounds, false))}, fire modes {OnOff(Read(fFireModes, false))} | " +
                   $"Combat Tweaks options: VCO Advanced Armor {OnOff(s.vcoUseAdvancedArmor)}, VCO wounds {OnOff(s.vcoUseProjectileWounds)}, " +
                   $"VCO missed shot spread {OnOff(s.vcoUseMissSpread)} (Projectile Spread {s.bulletSpread * 100:F0}%), " +
                   $"VCO fire mode bursts {OnOff(s.vcoUseFireModeBursts)} (Burst Size x{s.burstSizeMultiplier}), " +
                   $"scale fire mode distances {OnOff(s.vcoScaleFireModeDistances)} (Weapon Range x{s.weaponRange}) | " +
                   $"armor penetration: {DescribePenetration()} | patched {patchedCount} of {penetrationMethods.Count + 4} VCO compat targets";
        }

        private static string OnOff(bool value) => value ? "on" : "off";

        public static void ApplyPatches()
        {
            var harmony = new Harmony("kasterfly.combatTweaks");
            Type patches = typeof(VanillaCombatOverhaulCompat_Patches);

            if (penetrationMethods.Count == 0)
                missing.Add("VCO's armor penetration methods");
            penetrationPatched = penetrationMethods.Count > 0;
            foreach (MethodBase method in penetrationMethods)
            {
                // All or nothing, a partly removed scaling would be worse than none
                if (!TryPatch(harmony, method, $"{method.DeclaringType?.Name}.{method.Name}",
                        postfix: new HarmonyMethod(patches, nameof(VanillaCombatOverhaulCompat_Patches.PenetrationPostfix))))
                    penetrationPatched = false;
            }

            TryPatch(harmony, rawPenetration, "SuppressionUtility.RawPenetration",
                prefix: new HarmonyMethod(patches, nameof(VanillaCombatOverhaulCompat_Patches.RawPenetrationPrefix)));
            TryPatch(harmony, woundsPrefix, "VCO's bullet/arrow wounds prefix",
                prefix: new HarmonyMethod(patches, nameof(VanillaCombatOverhaulCompat_Patches.WoundsPrefix)),
                finalizer: new HarmonyMethod(patches, nameof(VanillaCombatOverhaulCompat_Patches.WoundsFinalizer)));
            TryPatch(harmony, burstShotCountFor, "FireModeUtility.BurstShotCountFor",
                prefix: new HarmonyMethod(patches, nameof(VanillaCombatOverhaulCompat_Patches.BurstShotCountPrefix)));
            TryPatch(harmony, modeForDistance, "FireModeUtility.ModeForDistance",
                prefix: new HarmonyMethod(patches, nameof(VanillaCombatOverhaulCompat_Patches.ModeForDistancePrefix)));
        }

        private static bool TryPatch(Harmony harmony, MethodBase original, string name,
            HarmonyMethod prefix = null, HarmonyMethod postfix = null, HarmonyMethod finalizer = null)
        {
            if (original == null)
            {
                missing.Add(name);
                return false;
            }
            try
            {
                harmony.Patch(original, prefix: prefix, postfix: postfix, finalizer: finalizer);
                patchedCount++;
                return true;
            }
            catch (Exception ex)
            {
                missing.Add(name);
                Log.Warning($"[CombatTweaks] Vanilla Combat Overhaul compat failed to patch {name}: {ex}");
                return false;
            }
        }

        public static void LogStatus()
        {
            Log.Message("[CombatTweaks] Vanilla Combat Overhaul detected, compatibility is " +
                        (CombatTweaksMod.Settings.vanillaCombatOverhaulCompatibility ? "on" : "off (Combat Tweaks settings > Compatibility)"));
            if (missing.Count > 0)
                Log.Warning($"[CombatTweaks] Vanilla Combat Overhaul compat could not find or patch: {string.Join(", ", missing)}. " +
                            "Those parts of the compatibility are off, VCO probably changed.");
            if (CombatTweaksMod.Settings.debugLogging)
                Log.Message("[CombatTweaks] Vanilla Combat Overhaul compat status: " + Describe());
        }

        public static void OnSettingsChanged()
        {
            VerbBurstMultiplierPatch.ApplyBurstBuffFromSettings();
            if (present && CombatTweaksMod.Settings.debugLogging)
                Log.Message("[CombatTweaks] Vanilla Combat Overhaul compat settings changed: " + Describe());
        }
    }

    [StaticConstructorOnStartup]
    public static class VanillaCombatOverhaulCompatStartup
    {
        static VanillaCombatOverhaulCompatStartup()
        {
            if (!VanillaCombatOverhaulCompat.present) return;
            VanillaCombatOverhaulCompat.ApplyPatches();
            VanillaCombatOverhaulCompat.LogStatus();
        }
    }

    // Applied by VanillaCombatOverhaulCompat.ApplyPatches, only while Vanilla Combat Overhaul is loaded
    public static class VanillaCombatOverhaulCompat_Patches
    {
        // On the same methods as VCO's penetration postfix, so the two cancel out
        public static void PenetrationPostfix(ref float __result)
        {
            if (VanillaCombatOverhaulCompat.TryGetPenetrationScaleToUndo(out float scale))
                __result /= scale;
        }

        // VCO's suppression divides by its penetration scale to get the unscaled value, which PenetrationPostfix already did
        public static bool RawPenetrationPrefix(float shown, ref float __result)
        {
            if (!VanillaCombatOverhaulCompat.TryGetPenetrationScaleToUndo(out _))
                return true;
            __result = shown;
            return false;
        }

        // On VCO's prefix for DamageWorker_AddInjury.ApplySpecialEffectsToPart (bullet/arrow wounds), which skips hits with
        // result.diminished. Returning false with __result = true makes VCO's prefix let vanilla run instead.
        public static bool WoundsPrefix(Pawn pawn, DamageInfo dinfo, DamageWorker.DamageResult result, ref bool __result, out int __state)
        {
            __state = -1;
            CombatTweaksModSettings settings = CombatTweaksMod.Settings;
            if (!VanillaCombatOverhaulCompat.Active || result == null)
                return true;
            bool log = settings.debugLogging && dinfo.Def != null && dinfo.Def.isRanged;

            if (!settings.vcoUseProjectileWounds)
            {
                if (log)
                    Log.Message($"[CombatTweaks] Vanilla Combat Overhaul compat: VCO's bullet/arrow wounds skipped for {dinfo.Def.defName} on {pawn}, vanilla wounds used");
                __result = true;
                return false;
            }

            if (CombatTweaksArmorUtility.TryTakeArmorResult(pawn, dinfo.HitPart, out bool reducedByArmor))
            {
                if (log)
                    Log.Message($"[CombatTweaks] Vanilla Combat Overhaul compat: {dinfo.Def.defName} hit on {pawn} counts as {(reducedByArmor ? "reduced" : "not reduced")} by armor for VCO's bullet/arrow wounds" +
                                (result.diminished != reducedByArmor ? $" (vanilla's check said {(result.diminished ? "reduced" : "not reduced")}, from damage boosts)" : ""));
                if (result.diminished != reducedByArmor)
                {
                    __state = result.diminished ? 1 : 0;
                    result.diminished = reducedByArmor;
                }
            }
            return true;
        }

        public static void WoundsFinalizer(DamageWorker.DamageResult result, int __state)
        {
            if (__state >= 0)
                result.diminished = __state == 1;
        }

        // "Use VCO's fire mode burst sizes" off: Burst Size owns burst size, VCO's fire modes keep their other effects
        public static bool BurstShotCountPrefix(int baseBurst, ref int __result)
        {
            if (!VanillaCombatOverhaulCompat.Active || CombatTweaksMod.Settings.vcoUseFireModeBursts)
                return true;
            if (CombatTweaksMod.Settings.debugLogging)
                Log.Message($"[CombatTweaks] Vanilla Combat Overhaul compat: fire mode burst size skipped, burst stays {baseBurst} (Burst Size)");
            __result = baseBurst;
            return false;
        }

        public static void ModeForDistancePrefix(ref float distance)
        {
            if (!VanillaCombatOverhaulCompat.Active || !CombatTweaksMod.Settings.vcoScaleFireModeDistances)
                return;
            float range = CombatTweaksMod.Settings.weaponRange;
            if (range > 0.01f)
                distance /= range;
        }
    }

    [StaticConstructorOnStartup]
    public static class VerbBurstMultiplierPatch
    {
        private static float _lastMult = 1f;

        private static readonly Dictionary<string, int> _baselineBurstByKey = new Dictionary<string, int>(1024);
        private static bool _baselinesInitialized = false;

        static VerbBurstMultiplierPatch()
        {
            _lastMult = 1f;
            ApplyBurstBuffFromSettings();
        }

        public static void ApplyBurstBuffFromSettings()
        {
            float m = Mathf.Clamp(CombatTweaksMod.Settings.burstSizeMultiplier, 0.01f, 8f);
            // Vanilla Combat Overhaul's fire modes own burst size instead (see VanillaCombatOverhaulCompat_BurstShotCount_Patch)
            bool vcoOwnsBursts = VanillaCombatOverhaulCompat.Active && CombatTweaksMod.Settings.vcoUseFireModeBursts;
            if (vcoOwnsBursts)
                m = 1f;

            if (!_baselinesInitialized)
            {
                InitializeBaselines();
                _baselinesInitialized = true;
            }

            if (Mathf.Approximately(m, _lastMult))
                return;

            foreach (ThingDef def in DefDatabase<ThingDef>.AllDefs)
            {
                var verbs = def.Verbs;
                if (verbs == null) continue;

                for (int i = 0; i < verbs.Count; i++)
                {
                    var vp = verbs[i];
                    if (vp == null || vp.IsMeleeAttack) continue;
                    if (vp.verbClass != typeof(Verb_Shoot)) continue;
                    if (vp.defaultProjectile == null) continue;

                    string key = MakeKey(def, i);

                    if (!_baselineBurstByKey.TryGetValue(key, out int baseline))
                    {
                        int estimatedBase = Mathf.Max(1, Mathf.RoundToInt(vp.burstShotCount / Mathf.Max(0.01f, _lastMult)));
                        _baselineBurstByKey[key] = baseline = estimatedBase;
                    }

                    int newValue = Mathf.Max(1, Mathf.RoundToInt(baseline * m));
                    vp.burstShotCount = newValue;
                }
            }

            if (CombatTweaksMod.Settings.debugLogging)
                Log.Message($"[CombatTweaks] Burst sizes set to baseline × {m:0.###} (was × {_lastMult:0.###})." +
                    (vcoOwnsBursts ? " Burst Size is ignored, Vanilla Combat Overhaul's fire modes set burst size." : ""));

            _lastMult = m;
        }

        private static void InitializeBaselines()
        {
            foreach (ThingDef def in DefDatabase<ThingDef>.AllDefs)
            {
                var verbs = def.Verbs;
                if (verbs == null) continue;

                for (int i = 0; i < verbs.Count; i++)
                {
                    var vp = verbs[i];
                    if (vp == null || vp.IsMeleeAttack) continue;
                    if (vp.verbClass != typeof(Verb_Shoot)) continue;
                    if (vp.defaultProjectile == null) continue;

                    string key = MakeKey(def, i);

                    int original = vp.burstShotCount;
                    if (original <= 0) original = 1;
                    _baselineBurstByKey[key] = original;
                }
            }

            if (CombatTweaksMod.Settings.debugLogging)
                Log.Message($"[CombatTweaks] Cached {_baselineBurstByKey.Count} burst baselines.");
        }

        private static string MakeKey(ThingDef def, int verbIndex)
        {
            return $"{def.defName}#{verbIndex}";
        }
    }



    [StaticConstructorOnStartup]
    public static class VerbRangeMultiplierPatch
    {
        private static float _lastMult = 1f;

        static VerbRangeMultiplierPatch()
        {
            _lastMult = 1f;
            ApplyRangeBuffFromSettings();
        }
        public static void ApplyRangeBuffFromSettings()
        {
            float m = CombatTweaksMod.Settings.weaponRange;
            if (Mathf.Approximately(m, _lastMult)) return;
            if (Mathf.Approximately(_lastMult, 0f))
            {
                Log.Warning("[CombatTweaks] Last multiplier was 0; ratio scaling needs a baseline. Consider the zero-safe variant.");
                _lastMult = 1f;
            }

            float ratio = (_lastMult == 0f) ? m : (m / _lastMult);

            foreach (ThingDef def in DefDatabase<ThingDef>.AllDefs)
            {
                var verbs = def.Verbs;
                if (verbs == null) continue;

                for (int i = 0; i < verbs.Count; i++)
                {
                    var vp = verbs[i];
                    if (vp == null || vp.IsMeleeAttack) continue;
                    vp.range *= ratio;
                }
            }

            _lastMult = m;
            if(CombatTweaksMod.Settings.debugLogging)
                Log.Message($"[CombatTweaks] Rescaled ranged weapon ranges by x{ratio:0.###} (now x{_lastMult:0.###} total)");
        }
    }



    [HarmonyPatch(typeof(VerbProperties), nameof(VerbProperties.DrawRadiusRing))]
    public static class VerbProperties_DrawRadiusRing_Patch
    {
        static bool Prefix(VerbProperties __instance, IntVec3 center, Verb verb = null)
        {
            if (Find.CurrentMap == null || __instance.IsMeleeAttack || !__instance.targetable)
                return false;

            float minRange = __instance.EffectiveMinRange(true);
            float maxRange = (verb != null) ? verb.EffectiveRange : __instance.range;

            if (minRange > 0f)
            {
                if (minRange <= GenRadial.MaxRadialPatternRadius)
                    GenDraw.DrawRadiusRing(center, minRange);
                else
                    DrawHollowRadiusRing(center, minRange, Color.white);
            }

            if (__instance.drawHighlightWithLineOfSight)
            {
                if (maxRange <= GenRadial.MaxRadialPatternRadius)
                {
                    GenDraw.DrawRadiusRing(center, maxRange, Color.white,
                        (IntVec3 c) => GenSight.LineOfSight(center, c, Find.CurrentMap));
                }
                else
                {
                    DrawHollowRadiusRing(center, maxRange, Color.white,
                        (IntVec3 c) => GenSight.LineOfSight(center, c, Find.CurrentMap));
                }
            }
            else
            {
                if (maxRange <= GenRadial.MaxRadialPatternRadius)
                    GenDraw.DrawRadiusRing(center, maxRange, Color.white);
                else
                    DrawHollowRadiusRing(center, maxRange, Color.white);
            }

            return false;
        }

        private static void DrawHollowRadiusRing(IntVec3 center, float radius, Color color, System.Func<IntVec3, bool> predicate = null)
        {
            Map map = Find.CurrentMap;
            float outerSq = radius * radius;
            float innerSq = (radius - 1f) * (radius - 1f);

            CellRect bounds = CellRect.CenteredOn(center, (int)(radius + 2));
            bounds.ClipInsideMap(map);

            List<IntVec3> edgeCells = new List<IntVec3>();
            foreach (var cell in bounds)
            {
                float distSq = (cell - center).LengthHorizontalSquared;
                if (distSq <= outerSq && distSq >= innerSq)
                {
                    if (predicate == null || predicate(cell))
                        edgeCells.Add(cell);
                }
            }

            if (edgeCells.Count > 0)
                GenDraw.DrawFieldEdges(edgeCells, color);
        }
    }



    [HarmonyPatch(typeof(Projectile), "Launch", new Type[] {
    typeof(Thing), typeof(Vector3), typeof(LocalTargetInfo), typeof(LocalTargetInfo),
    typeof(ProjectileHitFlags), typeof(bool), typeof(Thing), typeof(ThingDef)
})]
    public static class Patch_Projectile_Launch_OverrideSpread
    {
        static void Prefix(
            ref LocalTargetInfo usedTarget,
            Thing launcher,
            Vector3 origin,
            LocalTargetInfo intendedTarget,
            ProjectileHitFlags hitFlags,
            bool preventFriendlyFire,
            Thing equipment,
            ThingDef targetCoverDef
        )
        {
            float baseSpread = CombatTweaksMod.Settings.bulletSpread;
            if (baseSpread <= 0.0001f || launcher == null || !usedTarget.IsValid || !intendedTarget.IsValid)
                return;
            Verb verb = launcher is Pawn pawn ? pawn?.equipment?.PrimaryEq?.PrimaryVerb : null;
            if (verb == null)
            {
                return;
            }
            // With Vanilla Combat Overhaul the shot was already rolled with VCO's accuracy changes, so either VCO places
            // the miss, or only shots that already went wide (not hits, not cover hits) are re-aimed, without a second roll
            bool vcoCompat = VanillaCombatOverhaulCompat.Active;
            float aimChance = -1f;
            if (vcoCompat)
            {
                if (CombatTweaksMod.Settings.vcoUseMissSpread)
                {
                    if (CombatTweaksMod.Settings.debugLogging)
                        Log.Message("[CombatTweaks] Vanilla Combat Overhaul compat: Projectile Spread skipped, VCO places missed shots");
                    return;
                }
                if (usedTarget.HasThing || usedTarget.Cell == intendedTarget.Cell)
                    return;
            }
            else
            {
                ShotReport report = ShotReport.HitReportFor(launcher, verb, intendedTarget);
                aimChance = report.AimOnTargetChance;
                if (Rand.Chance(aimChance))
                {
                    return;
                }
            }

            float maxAngle = Mathf.Min(baseSpread * 90f, 180f);
            if (CombatTweaksMod.Settings.skillsEffectBulletSpread && launcher is Pawn pawn2 && pawn2.skills != null)
                maxAngle = Mathf.Max(maxAngle / 3, maxAngle * (3 - pawn2.skills.GetSkill(SkillDefOf.Shooting).Level / 7));
            maxAngle = Mathf.Min(maxAngle, 180f);

            Vector3 originPos = origin;
            Vector3 targetPos = intendedTarget.HasThing
                ? intendedTarget.Thing.Position.ToVector3Shifted()
                : intendedTarget.Cell.ToVector3Shifted();

            float distance = Vector3.Distance(originPos, targetPos);
            Vector3 forward = (targetPos - originPos).normalized;
            if (forward.sqrMagnitude < 1e-6f)
                return;
            Vector3 right = new Vector3(-forward.z, 0f, forward.x);

            float deviationAngle = Rand.Range(maxAngle * (-0.5f), maxAngle * 0.5f);

            float angleRad = deviationAngle * Mathf.Deg2Rad;
            float lateral = Mathf.Tan(Mathf.Abs(angleRad)) * distance;
            lateral = Mathf.Min(lateral, distance * 0.75f);
            lateral *= Mathf.Sign(deviationAngle);

            Vector3 missPoint = targetPos + right * lateral;
            missPoint += forward * Rand.Range(-1f, 1f);

            if (float.IsNaN(missPoint.x) || float.IsNaN(missPoint.y) || float.IsNaN(missPoint.z) ||
                float.IsInfinity(missPoint.x) || float.IsInfinity(missPoint.y) || float.IsInfinity(missPoint.z) || float.IsNaN(distance))
                return;

            usedTarget = new LocalTargetInfo(missPoint.ToIntVec3());

            if (CombatTweaksMod.Settings.debugLogging)
            {
                Log.Message(
                    $"[CombatTweaks] Forced MISS via spread logic\n" +
                    (vcoCompat ? "- Shot already missed (rolled by Vanilla Combat Overhaul)" : $"- AimChance: {aimChance:P1}") +
                    $" | Deviation: {deviationAngle:F2}°" +
                    $"- | Final Target Cell: {usedTarget.Cell} | Max Angle: {maxAngle/2:F2}° | missPoint: {missPoint.ToString()}"
                 );
            }
        }
    }
}