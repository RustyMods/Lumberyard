using System.Collections.Generic;
using HarmonyLib;

namespace Lumberyard.Lumberyard;

public static class CraftingStationPatches
{
    [HarmonyPatch(typeof(CraftingStation), nameof(CraftingStation.GetExtensions))]
    static class LumberyardExtensions
    {
        private static void Postfix(CraftingStation __instance, ref List<StationExtension> __result)
        {
            if (!__instance) return;
            string normalizedName = __instance.name.Replace("(Clone)", "");
            if (normalizedName != "LumberYard_RS") return;

            SmelterPatches.connectedExtensions = __result.Count;
        }
    }
    
    [HarmonyPatch(typeof(CraftingStation), nameof(CraftingStation.GetHoverText))]
    static class LumberyardHoverTextPatch
    {
        private static void Postfix(CraftingStation __instance, ref string __result)
        {
            if (!__instance) return;
            string normalizedName = __instance.name.Replace("(Clone)", "");
            if (normalizedName != "LumberYard_RS") return;

            __result = $"{Localization.instance.Localize("$piece_lumberyard")} (<color=orange>{SmelterPatches.connectedExtensions}</color>)";
        }
    }
}