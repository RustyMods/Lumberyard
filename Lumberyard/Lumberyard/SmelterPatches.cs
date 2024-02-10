using System.Collections.Generic;
using BepInEx;
using HarmonyLib;
using UnityEngine;
using Random = System.Random;

namespace Lumberyard.Lumberyard;

public static class SmelterPatches
{
    public static readonly int containerWidth = 8;
    public static readonly int containerHeight = 4;
    public static int connectedExtensions = 0;
    private static bool containerFull;
    private static float GrowthTimer = 0;
    
    [HarmonyPatch(typeof(Smelter), nameof(Smelter.OnHoverAddFuel))]
    private static class OnHoverAddFuelPatch
    {
        private static void Postfix(Smelter __instance, ref string __result)
        {
            string normalizedName = __instance.name.Replace("(Clone)", "");
            if (normalizedName != "LumberYard_RS") return;
            if (LumberyardPlugin._AddMaxFuel.Value is LumberyardPlugin.Toggle.Off) return;

            __result = Localization.instance.Localize(
                string.Format("{0} ({1} {2}/{3})\n[<color=yellow><b>$KEY_Use</b></color>] $piece_smelter_add x{5} {4}", 
                    (object) __instance.m_name, 
                    (object) __instance.m_fuelItem.m_itemData.m_shared.m_name, 
                    (object) Mathf.Ceil(__instance.GetFuel()), 
                    (object) __instance.m_maxFuel, 
                    (object) __instance.m_fuelItem.m_itemData.m_shared.m_name,
                    (object) GetTotalAvailableFuel(__instance)
                ));
        }
    }
    
    [HarmonyPatch(typeof(Smelter), nameof(Smelter.OnAddFuel))]
    private static class OnAddFuelPatch
    {
        private static bool Prefix(Smelter __instance, Humanoid user, ItemDrop.ItemData? item, ref bool __result)
        {
            string normalizedName = __instance.name.Replace("(Clone)", "");
            if (normalizedName != "LumberYard_RS") return true;
            if (LumberyardPlugin._AddMaxFuel.Value is LumberyardPlugin.Toggle.Off) return true;
            if (item != null && item.m_shared.m_name != __instance.m_fuelItem.m_itemData.m_shared.m_name)
            {
                user.Message(MessageHud.MessageType.Center, "$msg_wrongitem");
                __result = false;
                return false;
            }

            if (__instance.GetFuel() > __instance.m_maxFuel - 1)
            {
                user.Message(MessageHud.MessageType.Center, "$msg_itsfull");
                __result = false;
                return false;
            }

            if (!user.GetInventory().HaveItem(__instance.m_fuelItem.m_itemData.m_shared.m_name))
            {
                user.Message(MessageHud.MessageType.Center, "$msg_donthaveany " + __instance.m_fuelItem.m_itemData.m_shared.m_name);
                __result = false;
                return false;
            }
            
            int count = GetTotalAvailableFuel(__instance);
            user.Message(MessageHud.MessageType.Center, "$msg_added " + __instance.m_fuelItem.m_itemData.m_shared.m_name + " x" + count);
            user.GetInventory().RemoveItem(__instance.m_fuelItem.m_itemData.m_shared.m_name, count);
            __instance.SetFuel(__instance.GetFuel() + count);
            __result = true;
            return false;
        }
    }
    
    [HarmonyPatch(typeof(Smelter), nameof(Smelter.Awake))]
    static class LumberyardUpdateConfigurations
    {
        private static void Postfix(Smelter __instance)
        {
            if (!__instance) return;
            string normalizedName = __instance.name.Replace("(Clone)", "");
            if (normalizedName != "LumberYard_RS") return;
            
            SetContainerData(__instance);
            
            UpdateSmelterScript(__instance.gameObject, 
                LumberyardPlugin._FuelItem.Value, 
                LumberyardPlugin._MaxSeed.Value, 
                LumberyardPlugin._MaxFuel.Value,
                LumberyardPlugin._FuelPerProduct.Value, 
                LumberyardPlugin._SecPerProduct.Value, 
                LumberyardPlugin._AddSeedHoverText.Value
                );
            
            ToggleBarrier(__instance);
        }
    }
    
    [HarmonyPatch(typeof(Smelter), nameof(Smelter.FindCookableItem))]
    static class LumberyardFindSeedItem
    {
        private static void Postfix(Smelter __instance, ref ItemDrop.ItemData? __result)
        {
            if (!__instance) return;
            string normalizedName = __instance.name.Replace("(Clone)", "");
            if (normalizedName != "LumberYard_RS") return;
            if (__result == null) return;

            // Control which conversion recipes are allowed based on extensions
            switch (__result.m_dropPrefab.name)
            {
                case "Acorn":
                    if (connectedExtensions != LumberyardPlugin._OakRequiredExt.Value) __result = null; break;
                case "Sap":
                    if (connectedExtensions != LumberyardPlugin._YggRequiredExt.Value) __result = null; break;
                case "AncientSeed":
                    if (connectedExtensions != LumberyardPlugin._AncientRequiredExt.Value) __result = null; break;
                case "BeechSeeds":
                    if (connectedExtensions != LumberyardPlugin._BeechRequiredExt.Value) __result = null; break;
                case "BirchSeeds":
                    if (connectedExtensions != LumberyardPlugin._BirchRequiredExt.Value) __result = null!; break;
                case "FirCone":
                    if (connectedExtensions != LumberyardPlugin._FirRequiredExt.Value) __result = null!; break;
                case "PineCone":
                    if (connectedExtensions != LumberyardPlugin._PineRequiredExt.Value) __result = null!; break;
            }
        }
    }
    
    [HarmonyPatch(typeof(Smelter), nameof(Smelter.Spawn))]
    static class LumberyardQueueProcessed
    {
        private static bool Prefix(Smelter __instance, string ore, int stack)
        {
            if (!__instance) return false;
            if (!__instance.m_nview.IsValid()) return false;
            
            string normalizedName = __instance.name.Replace("(Clone)", "");
            if (normalizedName != "LumberYard_RS") return true;
            
            if (containerFull) return false; // If container is full, stop production
            
            Smelter.ItemConversion itemConversion = __instance.GetItemConversion(ore);
            if (itemConversion == null) return false;
            
            GameObject lumber = itemConversion.m_to.gameObject;
            GameObject seed = itemConversion.m_from.gameObject;
            Container container = __instance.GetComponentInChildren<Container>();

            // Create two values to compare to simulate a chance of getting seeds
            Random random = new Random();
            int randomIndex = random.Next(LumberyardPlugin._ChanceOfSeed.Value, 100);
            int randomMatch = random.Next(LumberyardPlugin._ChanceOfSeed.Value, 100);
            
            // Set the multiplier value based on the configurations
            int value = stack;
            switch (seed.name)
            {
                case "FirCone": value *= LumberyardPlugin._FirSeedMultiplier.Value; break;
                case "BeechSeeds": value *= LumberyardPlugin._BeechSeedMultiplier.Value; break;
                case "Acorn": value *= LumberyardPlugin._OakSeedMultiplier.Value; break;
                case "PineCone": value *= LumberyardPlugin._PineSeedMultiplier.Value; break;
                case "BirchSeeds": value *= LumberyardPlugin._BirchSeedMultiplier.Value; break;
                case "Sap": value *= LumberyardPlugin._YggSeedMultiplier.Value; break;
                case "AncientSeed": value *= LumberyardPlugin._AncientSeedMultiplier.Value; break;
            }
            
            // Instead of spawning, add items into lumberyard container
            if (randomIndex == randomMatch) container.m_inventory.AddItem(seed, 1);
            container.m_inventory.AddItem(lumber, value);
            
            return false;
        }
    }
    
    [HarmonyPatch(typeof(Smelter), nameof(Smelter.UpdateSmelter))]
    static class LumberyardIsActive
    {
        private static bool Prefix(Smelter __instance)
        {
            string normalizedName = __instance.name.Replace("(Clone)", "");
            if (normalizedName != "LumberYard_RS") return true;
            Container container = __instance.GetComponentInChildren<Container>();
            
            containerFull = container.m_inventory.GetEmptySlots() == 0;
            
            // Update log visuals based on contents
            UpdateLogs(__instance, (containerWidth * containerHeight) - container.m_inventory.m_inventory.Count);

            return !containerFull;
        }
    }
    
    [HarmonyPatch(typeof(Smelter), nameof(Smelter.UpdateState))]
    static class LumberyardUpdatePatch
    {
        private static void Postfix(Smelter __instance)
        {
            if (!__instance) return;
            string normalizedName = __instance.name.Replace("(Clone)", "");
            if (normalizedName != "LumberYard_RS") return;

            string? QueuedOre = __instance.GetQueuedOre();
            if (QueuedOre.IsNullOrWhiteSpace()) return;
            if (__instance.GetFuel() == 0) return;

            GrowthTimer = (GrowthTimer + 1) % 100; // value between 0 - 100

            if (containerFull) return; // Stop updating visuals if container is full

            switch (QueuedOre)
            {
                case "BeechSeeds": SwitchTrees(__instance, "beech", GrowthTimer); break;
                case "Acorn": SwitchTrees(__instance, "oak", GrowthTimer); break;
                case "PineCone": SwitchTrees(__instance, "pine", GrowthTimer); break;
                case "BirchSeeds": SwitchTrees(__instance, "birch", GrowthTimer); break;
                case "FirCone": SwitchTrees(__instance, "fir", GrowthTimer); break;
                case "Sap": SwitchTrees(__instance, "ygg", GrowthTimer); break;
                case "AncientSeed": SwitchTrees(__instance, "ancient", GrowthTimer); break;
                default: SwitchTrees(__instance, "fir", GrowthTimer); break;
            }
        }
    }

    private static int GetTotalAvailableFuel(Smelter instance)
    {
        if (!Player.m_localPlayer) return 0;

        int available = Player.m_localPlayer.GetInventory().CountItems(instance.m_fuelItem.m_itemData.m_shared.m_name);
        float max = instance.m_maxFuel - instance.GetFuel();

        int count;
        if (max >= available)
        {
            count = available;
        }
        else
        {
            count = available - (int)max;
        }

        return count;
    }
    
    private static void SwitchTrees(Smelter __instance, string treeName, float timer)
        {
            Transform TreeGroup = Utils.FindChild(__instance.gameObject.transform, "trees");
            if (!TreeGroup) return;
            for (int i = 0; i < TreeGroup.childCount; ++i) TreeGroup.GetChild(i).gameObject.SetActive(false);

            Transform saplings = Utils.FindChild(TreeGroup, $"{treeName}Saplings");
            Transform trees = Utils.FindChild(TreeGroup, $"{treeName}Trees");
            Transform stumps = Utils.FindChild(TreeGroup, $"{treeName}Stubs");
            
            UpdateTreeGrowth(saplings, stumps, trees, timer / 100);
        }

    private static void UpdateTreeGrowth(Transform saplings, Transform stumps, Transform trees, float fraction)
        {
            saplings.gameObject.SetActive(true);
            stumps.gameObject.SetActive(true);
            trees.gameObject.SetActive(true);
            
            List<Transform> saplingList = new();
            List<Transform> stumpList = new();
            List<Transform> treeList = new();
            for (int i = 0; i < saplings.childCount; ++i)
            {
                saplings.GetChild(i).gameObject.SetActive(false);
                saplingList.Add(saplings.GetChild(i));
            }
            for (int i = 0; i < stumps.childCount; ++i)
            {
                stumps.GetChild(i).gameObject.SetActive(false);
                stumpList.Add(stumps.GetChild(i));
            }
            for (int i = 0; i < trees.childCount; ++i)
            {
                trees.GetChild(i).gameObject.SetActive(false);
                treeList.Add(trees.GetChild(i));
            }

            if (fraction is < 0.8f and >= 0.5f)
            {
                saplingList[0].gameObject.SetActive(true);
                treeList[1].gameObject.SetActive(true);
                treeList[2].gameObject.SetActive(true);
                saplingList[3].gameObject.SetActive(true);
            }
            else if (fraction is < 0.75f and >= 0.5f)
            {
                treeList[0].gameObject.SetActive(true);
                saplingList[1].gameObject.SetActive(true);
                saplingList[2].gameObject.SetActive(true);
                treeList[3].gameObject.SetActive(true);
            }
            else if (fraction is < 0.5f and >= 0.20f)
            {
                treeList[0].gameObject.SetActive(true);
                stumpList[1].gameObject.SetActive(true);
                stumpList[2].gameObject.SetActive(true);
                treeList[3].gameObject.SetActive(true);
            }
            else if (fraction is < 0.35f and >= 0.20f)
            {
                stumpList[0].gameObject.SetActive(true);
                treeList[1].gameObject.SetActive(true);
                treeList[2].gameObject.SetActive(true);
                stumpList[3].gameObject.SetActive(true);
            }
            else if (fraction is < 0.20f and >= 0.05f)
            {
                stumpList[0].gameObject.SetActive(true);
                saplingList[1].gameObject.SetActive(true);
                saplingList[2].gameObject.SetActive(true);
                stumpList[3].gameObject.SetActive(true);
            }
            else
            {
                saplingList[0].gameObject.SetActive(true);
                stumpList[1].gameObject.SetActive(true);
                stumpList[2].gameObject.SetActive(true);
                saplingList[3].gameObject.SetActive(true);
            }
            
        }
    
    private static void UpdateLogs(Smelter __instance, int containerAmount)
    {
        Transform logs = Utils.FindChild(__instance.gameObject.transform, "Logs");
        if (!logs) return;

        int maxContainerSize = (containerWidth * containerHeight);
        for (int i = 0; i < logs.childCount; ++i) logs.GetChild(i).gameObject.SetActive(containerAmount <= maxContainerSize / (i + 1));
    }
    
    private static void ToggleBarrier(Smelter __instance)
    {
        Transform plot = Utils.FindChild(__instance.gameObject.transform, "plotBase");
        if (!plot) return;
        plot.gameObject.SetActive(LumberyardPlugin._HidePlot.Value is LumberyardPlugin.Toggle.On);
    }
    
    private static void SetContainerData(Smelter __instance)
    {
        Container container = __instance.GetComponentInChildren<Container>();
        if (!container) return;
            
        // Set container display name to configurable name
        container.m_name = LumberyardPlugin._ContainerName.Value;
        if (!ZNetScene.instance) return;
            
        // Get a chest background and set it to lumberyard container background
        GameObject TreasureChestHeath = ZNetScene.instance.GetPrefab("TreasureChest_heath");
        if (!TreasureChestHeath) return;
        if (!TreasureChestHeath.TryGetComponent(out Container ChestContainer)) return;
        container.m_bkg = ChestContainer.m_bkg;
            
        // Get the cargo crate and set it to lumberyard destroyed loot prefab
        GameObject karve = ZNetScene.instance.GetPrefab("Karve");
        Container? karveContainer = karve.GetComponentInChildren<Container>();
        if (!karveContainer) return;
        GameObject CargoCrate = karveContainer.m_destroyedLootPrefab;
        if (!CargoCrate) return;
        container.m_destroyedLootPrefab = CargoCrate;
    }

    private static void UpdateSmelterScript(GameObject prefab, string fuelItemName, int maxOre, int maxFuel, int fuelPerProduct, int secPerProduct, string AddOreHoverText)
    {
        if (!prefab.TryGetComponent(out Smelter smelter)) return;
        if ((!ZNetScene.instance)) return;
        GameObject fuelItem = ZNetScene.instance.GetPrefab(fuelItemName);
        if (!fuelItem) return;
        if (!fuelItem.TryGetComponent(out ItemDrop fuelItemPrefab)) return;
        
        smelter.m_fuelItem = fuelItemPrefab;
        smelter.m_maxOre = maxOre;
        smelter.m_maxFuel = maxFuel;
        smelter.m_fuelPerProduct = fuelPerProduct;
        smelter.m_secPerProduct = secPerProduct;
        smelter.m_addOreTooltip = AddOreHoverText;
    }
}