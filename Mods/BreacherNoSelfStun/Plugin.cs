using BepInEx;
using BepInEx.Unity.IL2CPP;
using FlowFire.Framework.Gameplay.Actions;
using HarmonyLib;

namespace BreacherNoSelfStun;

[BepInPlugin("com.shodan.breachernoselfstun", "Breacher No Self Stun", "2.0.0")]
public class Plugin : BasePlugin
{
    public override void Load() => new Harmony("com.shodan.breachernoselfstun").PatchAll(typeof(Plugin));

    [HarmonyPatch(typeof(Stun), "ApplyResult"), HarmonyPrefix]
    static bool Stun(Stun __instance) => !HitsLocalPlayer(__instance);

    // The grenade also spawns "Stagger [5s]" on everything in range; block only that spawn, only on us.
    const int StaggerId = 508804164;

    [HarmonyPatch(typeof(SpawnAction), "ApplyResult"), HarmonyPrefix]
    static bool Stagger(SpawnAction __instance)
    {
        var actions = __instance.Actions;
        if (actions == null || !HitsLocalPlayer(__instance)) return true;
        foreach (var a in actions)
            if (a._idInDepot == StaggerId) return false;
        return true;
    }

    static bool HitsLocalPlayer(ActionResult result)
    {
        var targets = result.EntityTargets;
        if (targets == null) return false;
        foreach (var t in targets)
            if (t != null && t.IsLocalPlayer) return true;
        return false;
    }
}
