using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using static Lumberyard.LumberyardPlugin;

namespace Lumberyard.Lumberyard;

public static class MiscPatches
{
    private static readonly Dictionary<string, string> SeedConversionMap = new()
    {
        { "Acorn", "FineWood" },
        { "BirchSeeds", "FineWood" },
        { "BeechSeeds", "Wood" },
        { "AncientSeed", "ElderBark" },
        { "PineCone", "RoundLog" },
        { "Sap", "YggdrasilWood" },
        { "FirCone", "Wood" }
    };

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

            // Set all the initial data based on configurations
            foreach (GameObject obj in objects)
            {
                SetWearNTearScript(obj, "vfx_SawDust", "sfx_wood_destroyed",
                    "vfx_SawDust", "sfx_wood_hit", "vfx_Place_cart", 1000);
                
                SetPieceScript(obj, "vfx_Place_wood_pole", "sfx_build_hammer_wood");
                
            }
            
            SetSmelterScript(__instance, Lumberyard, _FuelItem.Value, _MaxSeed.Value, _MaxFuel.Value, 
                _FuelPerProduct.Value, _SecPerProduct.Value, SeedConversionMap, "sfx_mill_add", "vfx_mill_add", "vfx_mill_add",
                "sfx_mill_add", "vfx_mill_produce");
            
            SetContainerScript(Lumberyard, SmelterPatches.containerWidth, SmelterPatches.containerHeight);
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
                LumberyardLogger.LogDebug("Failed to get effect : " + effectName);
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
        private static bool Prefix(object o) => !o.ToString().StartsWith("adding ");
        
    }
}