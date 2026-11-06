using BepInEx;
using BepInEx.Unity.IL2CPP;
using FlowFire.Framework.Gameplay.Entities.Weapons;
using FlowFire.Framework.UI;
using HarmonyLib;

namespace DropStarterWeapon;

[BepInPlugin("com.shodan.dropstarterweapon", "Drop Starter Weapon", "2.0.0")]
public class Plugin : BasePlugin
{
    public override void Load() => new Harmony("com.shodan.dropstarterweapon").PatchAll(typeof(Plugin));

    [HarmonyPatch(typeof(WeaponControllerBase), nameof(WeaponControllerBase.IsDroppable), MethodType.Getter), HarmonyPostfix]
    static void Droppable(ref bool __result) => __result = true;

    // After a save is loaded the HUD keeps the class starter weapon's icon for the slot it started in.
    // Re-initialise every HUD weapon item when weapons change so each shows its real weapon.
    [HarmonyPatch(typeof(HUDWeaponsFrame), nameof(HUDWeaponsFrame.Initialize)), HarmonyPostfix]
    static void AfterInit(HUDWeaponsFrame __instance) => Refresh(__instance);

    [HarmonyPatch(typeof(HUDWeaponsFrame), nameof(HUDWeaponsFrame.HandleOnWeaponEquipped)), HarmonyPostfix]
    static void AfterEquip(HUDWeaponsFrame __instance) => Refresh(__instance);

    static void Refresh(HUDWeaponsFrame frame)
    {
        var items = frame._weaponsInInventory;
        if (items == null) return;
        for (int i = 0; i < items.Length; i++)
            if (items[i]?.WeaponInventory != null) items[i].Initialize(i);
    }
}
