using System.Collections.Generic;
using System.Linq;
using BepInEx;
using HarmonyLib;
using UnityEngine;
using static Lumberyard.LumberyardPlugin;
using Random = System.Random;

namespace Lumberyard.Lumberyard;

public static class LumberyardPatches
{
    private static readonly List<string> AllowedPrefabNames = new()
    {
        "FineWood",
        "Wood",
        "ElderBark",
        "RoundLog",
        "YggdrasilWood",
        "BeechSeeds",
        "BirchSeeds",
        "AncientSeed",
        "FirCone",
        "PineCone",
        "Sap",
        "Acorn"
    };
    
    // Container behavior
    [HarmonyPatch(typeof(Inventory), nameof(Inventory.MoveItemToThis),typeof(Inventory),typeof(ItemDrop.ItemData),typeof(int),typeof(int),typeof(int))]
    static class LumberyardCanAddItem
    {
        private static bool Prefix(Inventory __instance, Inventory fromInventory, ItemDrop.ItemData item)
        {
            if (__instance.m_name != _ContainerName.Value) return true; 
            string[] config = _ListOfAllowedPrefabs.Value.Split(',');
            foreach (string input in config)
            {
                AllowedPrefabNames.Add(input);
            }

            return AllowedPrefabNames.Contains(item.m_dropPrefab.name);
        }
    }

    [HarmonyPatch(typeof(Inventory), nameof(Inventory.MoveItemToThis), typeof(Inventory), typeof(ItemDrop.ItemData))]
    static class LumberyardCanAddStack
    {
        private static bool Prefix(Inventory __instance, Inventory fromInventory, ItemDrop.ItemData item)
        {
            if (__instance.m_name != _ContainerName.Value) return true; 
            string[] config = _ListOfAllowedPrefabs.Value.Split(',');
            foreach (string input in config)
            {
                AllowedPrefabNames.Add(input);
            }
            
            return AllowedPrefabNames.Contains(item.m_dropPrefab.name);
        }
    }
    // Container destroyed
    [HarmonyPatch(typeof(CraftingStation), nameof(CraftingStation.OnDestroy))]
    static class LumberyardOnDestroy
    {
        private static void Postfix(CraftingStation __instance)
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
    
    private static int connectedExtensions = 0;
    [HarmonyPatch(typeof(CraftingStation), nameof(CraftingStation.GetExtensions))]
    static class LumberyardExtensions
    {
        private static void Postfix(CraftingStation __instance, ref List<StationExtension> __result)
        {
            if (!__instance) return;
            string normalizedName = __instance.name.Replace("(Clone)", "");
            if (normalizedName != "LumberYard_RS") return;

            connectedExtensions = __result.Count;
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

            __result = $"{Localization.instance.Localize("$piece_lumberyard")} (<color=orange>{connectedExtensions}</color>)";
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
            
            UpdateSmelterScript(__instance.gameObject, _FuelItem.Value, _MaxSeed.Value, _MaxFuel.Value,
                _FuelPerProduct.Value, _SecPerProduct.Value, _AddSeedHoverText.Value);
            
            ToggleBarrier(__instance);
        }

        private static void SetContainerData(Smelter __instance)
        {
            Container container = __instance.GetComponentInChildren<Container>();
            if (!container) return;
            container.m_name = _ContainerName.Value;
            if (!ZNetScene.instance) return;
            GameObject TreasureChestHeath = ZNetScene.instance.GetPrefab("TreasureChest_heath");
            if (!TreasureChestHeath) return;
            if (!TreasureChestHeath.TryGetComponent(out Container ChestContainer)) return;
            container.m_bkg = ChestContainer.m_bkg;
            GameObject karve = ZNetScene.instance.GetPrefab("Karve");
            Container? karveContainer = karve.GetComponentInChildren<Container>();
            if (!karveContainer) return;
            GameObject CargoCrate = karveContainer.m_destroyedLootPrefab;
            if (!CargoCrate) return;
            container.m_destroyedLootPrefab = CargoCrate;
        }
    }
    
    private static void ToggleBarrier(Smelter __instance)
    {
        Transform plot = Utils.FindChild(__instance.gameObject.transform, "plotBase");
        if (!plot) return;
        plot.gameObject.SetActive(_HidePlot.Value is Toggle.On);
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

            switch (__result.m_dropPrefab.name)
            {
                case "Acorn":
                    if (connectedExtensions != _OakRequiredExt.Value) __result = null; break;
                case "Sap":
                    if (connectedExtensions != _YggRequiredExt.Value) __result = null; break;
                case "AncientSeed":
                    if (connectedExtensions != _AncientRequiredExt.Value) __result = null; break;
                case "BeechSeeds":
                    if (connectedExtensions != _BeechRequiredExt.Value) __result = null; break;
                case "BirchSeeds":
                    if (connectedExtensions != _BirchRequiredExt.Value) __result = null!; break;
                case "FirCone":
                    if (connectedExtensions != _FirRequiredExt.Value) __result = null!; break;
                case "PineCone":
                    if (connectedExtensions != _PineRequiredExt.Value) __result = null!; break;
            }
        }
    }
    private static bool containerFull;
    
    [HarmonyPatch(typeof(Smelter), nameof(Smelter.Spawn))]
    static class LumberyardQueueProcessed
    {
        private static bool Prefix(Smelter __instance, string ore, int stack)
        {
            if (!__instance) return false;
            if (!__instance.m_nview.IsValid()) return false;

            if (containerFull) return false;
            
            string normalizedName = __instance.name.Replace("(Clone)", "");
            if (normalizedName != "LumberYard_RS") return true;
            
            Smelter.ItemConversion itemConversion = __instance.GetItemConversion(ore);
            if (itemConversion == null) return false;
            GameObject lumber = itemConversion.m_to.gameObject;
            GameObject seed = itemConversion.m_from.gameObject;
            Container container = __instance.GetComponentInChildren<Container>();

            Random random = new Random();
            int randomIndex = random.Next(_ChanceOfSeed.Value, 100);
            int randomMatch = random.Next(_ChanceOfSeed.Value, 100);
            
            int value = stack;
            switch (seed.name)
            {
                case "FirCone": value *= _FirSeedMultiplier.Value; break;
                case "BeechSeeds": value *= _BeechSeedMultiplier.Value; break;
                case "Acorn": value *= _OakSeedMultiplier.Value; break;
                case "PineCone": value *= _PineSeedMultiplier.Value; break;
                case "BirchSeeds": value *= _BirchSeedMultiplier.Value; break;
                case "Sap": value *= _YggSeedMultiplier.Value; break;
                case "AncientSeed": value *= _AncientSeedMultiplier.Value; break;
            }
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
            
            UpdateLogs(__instance, (containerWidth * containerHeight) - container.m_inventory.m_inventory.Count);

            return !containerFull;
        }
    }
    
    private static void UpdateLogs(Smelter __instance, int containerAmount)
    {
        Transform logs = Utils.FindChild(__instance.gameObject.transform, "Logs");
        if (!logs) return;

        int maxContainerSize = (containerWidth * containerHeight);
        for (int i = 0; i < logs.childCount; ++i) logs.GetChild(i).gameObject.SetActive(containerAmount <= maxContainerSize / (i + 1));
    }

    private static float GrowthTimer = 0;

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

            if (containerFull) return;

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

            if (fraction < 0.8f && fraction >= 0.5f)
            {
                saplingList[0].gameObject.SetActive(true);
                treeList[1].gameObject.SetActive(true);
                treeList[2].gameObject.SetActive(true);
                saplingList[3].gameObject.SetActive(true);
            }
            else if (fraction < 0.75f && fraction >= 0.5f)
            {
                treeList[0].gameObject.SetActive(true);
                saplingList[1].gameObject.SetActive(true);
                saplingList[2].gameObject.SetActive(true);
                treeList[3].gameObject.SetActive(true);
            }
            else if (fraction < 0.5f && fraction >= 0.20f)
            {
                treeList[0].gameObject.SetActive(true);
                stumpList[1].gameObject.SetActive(true);
                stumpList[2].gameObject.SetActive(true);
                treeList[3].gameObject.SetActive(true);
            }
            else if (fraction < 0.35f && fraction >= 0.20f)
            {
                stumpList[0].gameObject.SetActive(true);
                treeList[1].gameObject.SetActive(true);
                treeList[2].gameObject.SetActive(true);
                stumpList[3].gameObject.SetActive(true);
            }
            else if (fraction < 0.20f && fraction >= 0.05f)
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
    }

    private static readonly int containerWidth = 8;
    private static readonly int containerHeight = 4;
    [HarmonyPatch(typeof(ZNetScene), nameof(ZNetScene.Awake))]
    static class AddEffectsPatch
    {
        private static void Postfix(ZNetScene __instance)
        {
            if (!__instance) return;

            GameObject Lumberyard = __instance.GetPrefab("LumberYard_RS");
            GameObject Lumberyard_Ext1 = __instance.GetPrefab("LumberYard_ext1_RS");
            GameObject Lumberyard_Ext2 = __instance.GetPrefab("LumberYard_ext2_RS");

            if (!Lumberyard || !Lumberyard_Ext1 || !Lumberyard_Ext2) return;

            List<GameObject> objects = new List<GameObject>() { Lumberyard, Lumberyard_Ext1, Lumberyard_Ext2 };

            foreach (GameObject obj in objects)
            {
                SetWearNTearScript(obj, "vfx_SawDust", "sfx_wood_destroyed",
                    "vfx_SawDust", "sfx_wood_hit", "vfx_Place_cart", 1000);
                
                SetPieceScript(obj, "vfx_Place_wood_pole", "sfx_build_hammer_wood");
                
            }
            
            SetSmelterScript(__instance, Lumberyard, _FuelItem.Value, _MaxSeed.Value, _MaxFuel.Value, 
                _FuelPerProduct.Value, _SecPerProduct.Value, new Dictionary<string, string>()
                {
                    {"Acorn","FineWood"},
                    {"BirchSeeds","FineWood"},
                    {"BeechSeeds","Wood"},
                    {"AncientSeed", "ElderBark"},
                    {"PineCone","RoundLog"},
                    {"Sap","YggdrasilWood"},
                    {"FirCone","Wood"}
                    
                }, "sfx_mill_add", "vfx_mill_add", "vfx_mill_add",
                "sfx_mill_add", "vfx_mill_produce");
            
            SetContainerScript(Lumberyard, containerWidth, containerHeight);
        }
    }

    private static void SetContainerScript(GameObject prefab, int width, int height)
    {
        Container containerScript = prefab.GetComponentInChildren<Container>();
        if (!containerScript) return;

        containerScript.m_width = width;
        containerScript.m_height = height;
    }
    
    private static void SetWearNTearScript(GameObject prefab, 
        string destroyedEffectName1, string destroyEffectName2, 
        string hitEffectName1, string hitEffectName2, 
        string switchEffectName, float health)
    {
        if (!prefab.TryGetComponent(out WearNTear WearNTearScript)) return;

        if (!CreateEffectList(new[] { destroyedEffectName1, destroyEffectName2 }, out EffectList? destroyEffectList)) return;
        if (!CreateEffectList(new[] { hitEffectName1, hitEffectName2 }, out EffectList? hitEffects)) return;
        if (!CreateEffectList(new[] { switchEffectName }, out EffectList? switchEffects)) return;

        WearNTearScript.m_destroyedEffect = destroyEffectList;
        WearNTearScript.m_hitEffect = hitEffects;
        WearNTearScript.m_switchEffect = switchEffects;
        WearNTearScript.m_health = health;
    }
        
    private static void SetPieceScript(
        GameObject prefab, 
        string placementEffectName1,
        string placementEffectName2)
    {
        prefab.TryGetComponent(out Piece pieceScript);
        CreateEffectList(new[] { placementEffectName1, placementEffectName2 }, out EffectList? placementEffects);
        
        pieceScript.m_placeEffect = placementEffects;
        // Configure piece placement restrictions
        // pieceScript.m_groundPiece = true;
        // pieceScript.m_allowAltGroundPlacement = false;
        // pieceScript.m_cultivatedGroundOnly = false;
        // pieceScript.m_waterPiece = false;
        // pieceScript.m_clipGround = true;
        // pieceScript.m_clipEverything = false;
        // pieceScript.m_noInWater = false;
        // pieceScript.m_notOnWood = false;
        // pieceScript.m_notOnTiltingSurface = false;
        // pieceScript.m_inCeilingOnly = false;
        // pieceScript.m_notOnFloor = false;
        // pieceScript.m_noClipping = false;
        // pieceScript.m_onlyInTeleportArea = false;
        // pieceScript.m_allowedInDungeons = false;
        // pieceScript.m_spaceRequirement = 0f;
        // pieceScript.m_repairPiece = false;
        // pieceScript.m_canBeRemoved = true;
        // pieceScript.m_allowRotatedOverlap = true;
        // pieceScript.m_vegetationGroundOnly = false;
    }
    private static void SetSmelterScript(ZNetScene scene, GameObject prefab, string fuelItem, int maxOre, int maxFuel,
        int fuelPerProduct, int secPerProduct, Dictionary<string, string> conversion, string AddEffect1,
        string AddEffect2, string AddFuelEffect1, string AddFuelEffect2, string ProduceEffect)
    {
        List<Smelter.ItemConversion> smelterConversions = new();
        foreach (KeyValuePair<string, string> kvp in conversion)
        {
            GameObject fromPrefab = scene.GetPrefab(kvp.Key);
            GameObject toPrefab = scene.GetPrefab(kvp.Value);
            if (!fromPrefab || !toPrefab) continue;
            if (!fromPrefab.TryGetComponent(out ItemDrop fromItemDrop)) continue;
            if (!toPrefab.TryGetComponent(out ItemDrop toItemDrop)) continue;
            
            Smelter.ItemConversion conv = new Smelter.ItemConversion()
            {
                m_from = fromItemDrop,
                m_to = toItemDrop
            };
            smelterConversions.Add(conv);
        }

        CreateEffectList(new[] { AddFuelEffect1, AddFuelEffect2 }, out EffectList? AddFuelList);
        CreateEffectList(new[] { AddEffect1, AddEffect2 }, out EffectList? AddEffectList);
        CreateEffectList(new[] { ProduceEffect }, out EffectList? ProduceEffectList);
        ItemDrop? fuelItemPrefab = scene.GetPrefab(fuelItem).GetComponent<ItemDrop>();
        if (!fuelItemPrefab) return;
        
        if (!prefab.TryGetComponent(out Smelter smelter)) return;
        smelter.m_oreAddedEffects = AddEffectList;
        smelter.m_fuelItem = fuelItemPrefab;
        smelter.m_maxOre = maxOre;
        smelter.m_maxFuel = maxFuel;
        smelter.m_fuelPerProduct = fuelPerProduct;
        smelter.m_secPerProduct = secPerProduct;
        smelter.m_conversion = smelterConversions;
        smelter.m_fuelAddedEffects = AddFuelList;
        smelter.m_produceEffects = ProduceEffectList;
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

    private static bool CreateEffectList(string[] effectNames, out EffectList? __result)
    {
        __result = null;
        if (!ZNetScene.instance) return false;
        EffectList EffectList = new EffectList() { m_effectPrefabs = new EffectList.EffectData[effectNames.Length] };

        for (int index = 0; index < effectNames.Length; index++)
        {
            string effectName = effectNames[index];
            GameObject effect = ZNetScene.instance.GetPrefab(effectName);
            if (!effect)
            {
                LumberyardLogger.LogWarning("Failed to get effect : " + effectName);
                return false;
            }
            EffectList.EffectData data = new EffectList.EffectData()
            {
                m_prefab = effect,
                m_enabled = true,
                m_variant = -1,
                m_attach = false,
                m_inheritParentRotation = false,
                m_inheritParentScale = false,
                m_randomRotation = false,
                m_scale = false,
                m_childTransform = ""
            };
            EffectList.m_effectPrefabs[index] = data;
        }

        __result = EffectList;
        return true;
    }
    
    [HarmonyPatch(typeof(ZLog), nameof(ZLog.Log))]
    static class ZLogMutePatch
    {
        private static bool Prefix(object o)
        {
            if (o.ToString().StartsWith("adding ")) return false;
            return true;
        }
    }
}