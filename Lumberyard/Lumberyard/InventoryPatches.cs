using System.Linq;
using HarmonyLib;

namespace Lumberyard.Lumberyard;

public static class InventoryPatches
{
    // Container behavior
    [HarmonyPatch(typeof(Inventory), nameof(Inventory.MoveItemToThis),typeof(Inventory),typeof(ItemDrop.ItemData),typeof(int),typeof(int),typeof(int))]
    static class LumberyardCanAddItem
    {
        private static bool Prefix(Inventory __instance, ItemDrop.ItemData item) => __instance.m_name != LumberyardPlugin._ContainerName.Value || isPrefabAllowed(item);
    }
    
    private static bool isPrefabAllowed(ItemDrop.ItemData item) => AllowedPrefabs.AllowedPrefabNames.Contains(item.m_dropPrefab.name) || LumberyardPlugin. _ListOfAllowedPrefabs.Value.Split(',').ToList().Contains(item.m_dropPrefab.name);

    [HarmonyPatch(typeof(Inventory), nameof(Inventory.MoveItemToThis), typeof(Inventory), typeof(ItemDrop.ItemData))]
    static class LumberyardCanAddStack
    {
        private static bool Prefix(Inventory __instance, ItemDrop.ItemData item) => __instance.m_name != LumberyardPlugin._ContainerName.Value || isPrefabAllowed(item);
    }
}