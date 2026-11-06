using BepInEx;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using Synthetik2.Custom_Framework.Meta.Lobby;
using Synthetik2.Meta.Difficulty;

namespace AllModifiers;

[BepInPlugin("com.shodan.allmodifiers", "All Modifiers", "2.0.0")]
public class Plugin : BasePlugin
{
    public override void Load() => new Harmony("com.shodan.allmodifiers").PatchAll(typeof(Plugin));

    // Skip the game's filters that hide locked/droppable modifiers from the list.
    [HarmonyPatch(typeof(DifficultyModifierHelper), nameof(DifficultyModifierHelper.RemoveExcludedModifiers)), HarmonyPrefix]
    static bool SkipExcluded() => false;

    [HarmonyPatch(typeof(DifficultyModifierHelper), nameof(DifficultyModifierHelper.RemoveDroppableModifiers)), HarmonyPrefix]
    static bool SkipDroppable() => false;

    [HarmonyPatch(typeof(DifficultyModifierHelper), nameof(DifficultyModifierHelper.RemoveNonDroppableModifiers)), HarmonyPrefix]
    static bool SkipNonDroppable() => false;

    // Add the droppable (normally earned in runs) modifiers to the default list.
    [HarmonyPatch(typeof(DifficultyModifierHelper), nameof(DifficultyModifierHelper.GetDefaultDifficultyModifiersConfigs)), HarmonyPostfix]
    static void AddDroppable(Il2CppSystem.Collections.Generic.List<FlowFire.Framework.Meta.DifficultyModifier> __result)
    {
        var droppable = DifficultyModifierHelper.GetDroppableModifiers();
        if (__result == null || droppable == null) return;
        foreach (var m in droppable)
            if (m != null && !__result.Contains(m)) __result.Add(m);
        AddExcluded(__result);
    }

    // Modifiers the developers flagged "ExcludedFromGame" (Arcade, Swat, Elite Enclave, S1 test).
    static void AddExcluded(Il2CppSystem.Collections.Generic.List<FlowFire.Framework.Meta.DifficultyModifier> list)
    {
        var configs = FlowFire.Framework.Meta.FrameworkEnvironment.DepotDataManager?
            .GetAllLoadedConfigsOfType<FlowFire.Framework.AssetManagement.DifficultyModifierConfig>();
        if (configs == null) return;
        foreach (var c in configs)
        {
            if (c == null || c.ConfigState != FlowFire.Framework.AssetManagement.ConfigState.ExcludedFromGame) continue;
            bool have = false;
            foreach (var m in list)
                if (m?.Source != null && m.Source.ResourceID._idInDepot == c.ResourceID._idInDepot) { have = true; break; }
            if (have) continue;
            var mod = new FlowFire.Framework.Meta.DifficultyModifier();
            mod.InitializeData(c);
            list.Add(mod);
        }
    }

    // Don't write selections to the profile, so droppable modifiers are never used up.
    [HarmonyPatch(typeof(S2LobbyDifficultyModifierManager), nameof(S2LobbyDifficultyModifierManager.SaveSelectedDifficultyModifiersToProfile)), HarmonyPrefix]
    static bool SkipSave() => false;

    [HarmonyPatch(typeof(S2LobbyDifficultyModifierManager), nameof(S2LobbyDifficultyModifierManager.RemoveSelectedDroppableModifiersFromProfile)), HarmonyPrefix]
    static bool SkipRemove() => false;
}
