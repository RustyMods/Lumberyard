using HarmonyLib;

namespace Lumberyard.Lumberyard;

public static class PiecePatches
{
    // Container destroyed
    [HarmonyPatch(typeof(Piece), nameof(Piece.DropResources))]
    static class LumberyardDropResources
    {
        private static void Postfix(Piece __instance)
        {
            if (!__instance) return;
            string normalizedName = __instance.name.Replace("(Clone)", "");
            if (normalizedName != "LumberYard_RS") return;

            Container container = __instance.GetComponentInChildren<Container>();
            if (!container) return;
            
            if (!__instance.m_nview) return;
            container.DropAllItems(container.m_destroyedLootPrefab);
        }
    }
}