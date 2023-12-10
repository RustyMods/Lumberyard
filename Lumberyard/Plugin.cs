using System;
using System.IO;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using JetBrains.Annotations;
using PieceManager;
using ServerSync;
using UnityEngine;

namespace Lumberyard
{
    [BepInPlugin(ModGUID, ModName, ModVersion)]
    public class LumberyardPlugin : BaseUnityPlugin
    {
        internal const string ModName = "Lumberyard";
        internal const string ModVersion = "0.0.2";
        internal const string Author = "RustyMods";
        private const string ModGUID = Author + "." + ModName;
        private static string ConfigFileName = ModGUID + ".cfg";
        private static string ConfigFileFullPath = Paths.ConfigPath + Path.DirectorySeparatorChar + ConfigFileName;
        internal static string ConnectionError = "";
        private readonly Harmony _harmony = new(ModGUID);

        public static readonly ManualLogSource LumberyardLogger = BepInEx.Logging.Logger.CreateLogSource(ModName);
        private static readonly ConfigSync ConfigSync = new(ModGUID) { DisplayName = ModName, CurrentVersion = ModVersion, MinimumRequiredVersion = ModVersion };
        public enum Toggle { On = 1, Off = 0 }

        public void Awake()
        {
            _serverConfigLocked = config("1 - General", "Lock Configuration", Toggle.On,
                "If on, the configuration is locked and can be changed by server admins only.");
            _ = ConfigSync.AddLockingConfigEntry(_serverConfigLocked);

            BuildPiece Lumberyard = new("lumberyardbundle", "LumberYard_RS");
            Lumberyard.Name.English("Lumberyard");
            Lumberyard.Description.English("Automatic lumbering station");
            Lumberyard.RequiredItems.Add("FineWood", 20, true); 
            Lumberyard.RequiredItems.Add("SurtlingCore", 20, true);
            Lumberyard.RequiredItems.Add("RoundLog", 20, true);
            Lumberyard.RequiredItems.Add("Bronze", 20, true);
            Lumberyard.Category.Set(BuildPieceCategory.Crafting);
            Lumberyard.Crafting.Set(CraftingTable.Workbench);
            MaterialReplacer.RegisterGameObjectForMatSwap(Lumberyard.Prefab);

            BuildPiece Lumberyard_Ext1 = new("lumberyardbundle", "LumberYard_ext1_RS"); 
            Lumberyard_Ext1.Name.English("Lumber Wagon");
            Lumberyard_Ext1.Description.English("Lumberyard extension");
            Lumberyard_Ext1.RequiredItems.Add("FineWood", 20, true);
            Lumberyard_Ext1.RequiredItems.Add("Iron", 20, true);
            Lumberyard_Ext1.RequiredItems.Add("ElderBark", 20, true);
            Lumberyard_Ext1.RequiredItems.Add("BronzeNails", 20, true);
            Lumberyard_Ext1.Category.Set(BuildPieceCategory.Crafting);
            Lumberyard_Ext1.Crafting.Set("LumberYard_RS");
            Lumberyard_Ext1.Extension.Set("LumberYard_RS", 20);
            
            MaterialReplacer.RegisterGameObjectForMatSwap(Lumberyard_Ext1.Prefab);


            BuildPiece Lumberyard_Ext2 = new("lumberyardbundle", "LumberYard_ext2_RS");
            Lumberyard_Ext2.Name.English("Lumber Tent");
            Lumberyard_Ext2.Description.English("Lumberyard extension");
            Lumberyard_Ext2.RequiredItems.Add("LinenThread", 20, true);
            Lumberyard_Ext2.RequiredItems.Add("BlackMetal", 20, true);
            Lumberyard_Ext2.RequiredItems.Add("LoxPelt", 20, true);
            Lumberyard_Ext2.RequiredItems.Add("YggdrasilWood", 20, true);
            Lumberyard_Ext2.Category.Set(BuildPieceCategory.Crafting);
            Lumberyard_Ext2.Crafting.Set("LumberYard_RS");
            Lumberyard_Ext2.Extension.Set("LumberYard_RS", 20);
            
            MaterialReplacer.RegisterGameObjectForMatSwap(Lumberyard_Ext2.Prefab.transform.GetChild(0).gameObject);
            MaterialReplacer.RegisterGameObjectForMatSwap(Lumberyard_Ext2.Prefab.transform.GetChild(1).gameObject);
            
            InitConfigs();

            Assembly assembly = Assembly.GetExecutingAssembly();
            _harmony.PatchAll(assembly);
            SetupWatcher();
        }

        private void OnDestroy()
        {
            Config.Save();
        }

        private void SetupWatcher()
        {
            FileSystemWatcher watcher = new(Paths.ConfigPath, ConfigFileName);
            watcher.Changed += ReadConfigValues;
            watcher.Created += ReadConfigValues;
            watcher.Renamed += ReadConfigValues;
            watcher.IncludeSubdirectories = true;
            watcher.SynchronizingObject = ThreadingHelper.SynchronizingObject;
            watcher.EnableRaisingEvents = true;
        }

        private void ReadConfigValues(object sender, FileSystemEventArgs e)
        {
            if (!File.Exists(ConfigFileFullPath)) return;
            try
            {
                LumberyardLogger.LogDebug("ReadConfigValues called");
                Config.Reload();
            }
            catch
            {
                LumberyardLogger.LogError($"There was an issue loading your {ConfigFileName}");
                LumberyardLogger.LogError("Please check your config entries for spelling and format!");
            }
        }


        #region ConfigOptions

        private static ConfigEntry<Toggle> _serverConfigLocked = null!;
        
        #region CustomConfigs

        public static ConfigEntry<int> _BeechSeedMultiplier = null!;
        public static ConfigEntry<int> _BirchSeedMultiplier = null!;
        public static ConfigEntry<int> _FirSeedMultiplier = null!;
        public static ConfigEntry<int> _PineSeedMultiplier = null!;
        public static ConfigEntry<int> _OakSeedMultiplier = null!;
        public static ConfigEntry<int> _YggSeedMultiplier = null!;
        public static ConfigEntry<int> _AncientSeedMultiplier = null!;

        public static ConfigEntry<string> _FuelItem = null!;
        public static ConfigEntry<int> _MaxFuel = null!;
        public static ConfigEntry<int> _MaxSeed = null!;
        public static ConfigEntry<int> _FuelPerProduct = null!;
        public static ConfigEntry<int> _SecPerProduct = null!;

        public static ConfigEntry<int> _BeechRequiredExt = null!;
        public static ConfigEntry<int> _BirchRequiredExt = null!;
        public static ConfigEntry<int> _FirRequiredExt = null!;
        public static ConfigEntry<int> _PineRequiredExt = null!;
        public static ConfigEntry<int> _OakRequiredExt = null!;
        public static ConfigEntry<int> _YggRequiredExt = null!;
        public static ConfigEntry<int> _AncientRequiredExt = null!;

        public static ConfigEntry<string> _ContainerName = null!;
        public static ConfigEntry<string> _AddSeedHoverText = null!;
        public static ConfigEntry<Toggle> _HidePlot = null!;

        public static ConfigEntry<int> _ChanceOfSeed = null!;
        public static ConfigEntry<string> _ListOfAllowedPrefabs = null!;
        #endregion

        private void InitConfigs()
        {
            _BeechSeedMultiplier = config("Conversion Rate", "Beech Seed", 5,
                new ConfigDescription("Seed to wood conversion multiplier", new AcceptableValueRange<int>(1, 50)));
            _BirchSeedMultiplier = config("Conversion Rate", "Birch Seed", 20,
                new ConfigDescription("Seed to wood conversion multiplier", new AcceptableValueRange<int>(1, 50)));
            _FirSeedMultiplier = config("Conversion Rate", "Fir Cone", 5,
                new ConfigDescription("Seed to wood conversion multiplier", new AcceptableValueRange<int>(1, 50)));
            _PineSeedMultiplier = config("Conversion Rate", "Pine Cone", 20, new ConfigDescription("Seed to wood conversion multiplier", new AcceptableValueRange<int>(1, 50)));
            _OakSeedMultiplier = config("Conversion Rate", "Acorn", 40, new ConfigDescription("Seed to wood conversion multiplier", new AcceptableValueRange<int>(1, 50)));
            _YggSeedMultiplier = config("Conversion Rate", "Sap", 10, new ConfigDescription("Seed to wood conversion multiplier", new AcceptableValueRange<int>(1, 50)));
            _AncientSeedMultiplier = config("Conversion Rate", "Ancient Seed", 15, new ConfigDescription("Seed to wood conversion multiplier", new AcceptableValueRange<int>(1, 50)));

            _FuelItem = config("Lumberyard Settings", "Fuel Item", "Coins",
                "Item prefab name used to fuel the lumberyard");
            _MaxFuel = config("Lumberyard Settings", "Max Fuel", 500, new ConfigDescription("Total capacity of fuel lumberyard can hold", new AcceptableValueRange<int>(1, 1000)));
            _MaxSeed = config("Lumberyard Settings", "Max Seeds", 100, new ConfigDescription("Total capacity of seeds lumberyard can hold", new AcceptableValueRange<int>(1, 1000)));
            _FuelPerProduct = config("Lumberyard Settings", "Fuel Per Product", 10, new ConfigDescription("Fuel needed to convert seed to lumber", new AcceptableValueRange<int>(1, 50)));
            _SecPerProduct = config("Lumberyard Settings", "Sec Per Product", 300, new ConfigDescription("Seconds to convert seed to lumber", new AcceptableValueRange<int>(1, 5000)));
            _ContainerName = config("Lumberyard Settings", "Container Display Name", "Lumberyard Barrel", "");
            _AddSeedHoverText = config("Lumberyard Settings", "Add Seed Hover Text", "Add Seeds", "");
            _HidePlot = config("Lumberyard Settings", "Plot Visible", Toggle.On, "If on, logs around lumberyard are visible");
            _ChanceOfSeed = config("Lumberyard Settings", "Chance of Seed", 1, new ConfigDescription("Chance of returning seed", new AcceptableValueRange<int>(0, 100)));
            _ListOfAllowedPrefabs = config("Lumberyard Settings", "Allowed Prefabs in Barrel", "", "ex: Hammer,Hoe,Stone");
            
            _BeechRequiredExt = config("Required Extensions", "Beech", 0, new ConfigDescription("Required number of extensions for lumberyard to accept seeds", new AcceptableValueList<int>(0, 1, 2)));
            _BirchRequiredExt = config("Required Extensions", "Birch", 0, new ConfigDescription("Required number of extensions for lumberyard to accept seeds", new AcceptableValueList<int>(0, 1, 2)));
            _FirRequiredExt = config("Required Extensions", "Fir", 0, new ConfigDescription("Required number of extensions for lumberyard to accept seeds", new AcceptableValueList<int>(0, 1, 2)));
            _PineRequiredExt = config("Required Extensions", "Pine", 0, new ConfigDescription("Required number of extensions for lumberyard to accept seeds", new AcceptableValueList<int>(0, 1, 2)));
            _AncientRequiredExt = config("Required Extensions", "Ancient", 1, new ConfigDescription("Required number of extensions for lumberyard to accept seeds", new AcceptableValueList<int>(0, 1, 2)));
            _OakRequiredExt = config("Required Extensions", "Oak", 1, new ConfigDescription("Required number of extensions for lumberyard to accept seeds", new AcceptableValueList<int>(0, 1, 2)));
            _YggRequiredExt = config("Required Extensions", "Yggashoot", 2, new ConfigDescription("Required number of extensions for lumberyard to accept seeds", new AcceptableValueList<int>(0, 1, 2)));

        }
        

        private ConfigEntry<T> config<T>(string group, string name, T value, ConfigDescription description,
            bool synchronizedSetting = true)
        {
            ConfigDescription extendedDescription =
                new(
                    description.Description +
                    (synchronizedSetting ? " [Synced with Server]" : " [Not Synced with Server]"),
                    description.AcceptableValues, description.Tags);
            ConfigEntry<T> configEntry = Config.Bind(group, name, value, extendedDescription);
            //var configEntry = Config.Bind(group, name, value, description);

            SyncedConfigEntry<T> syncedConfigEntry = ConfigSync.AddConfigEntry(configEntry);
            syncedConfigEntry.SynchronizedConfig = synchronizedSetting;

            return configEntry;
        }

        private ConfigEntry<T> config<T>(string group, string name, T value, string description,
            bool synchronizedSetting = true)
        {
            return config(group, name, value, new ConfigDescription(description), synchronizedSetting);
        }

        private class ConfigurationManagerAttributes
        {
            [UsedImplicitly] public int? Order = null!;
            [UsedImplicitly] public bool? Browsable = null!;
            [UsedImplicitly] public string? Category = null!;
            [UsedImplicitly] public Action<ConfigEntryBase>? CustomDrawer = null!;
        }

        class AcceptableShortcuts : AcceptableValueBase
        {
            public AcceptableShortcuts() : base(typeof(KeyboardShortcut))
            {
            }

            public override object Clamp(object value) => value;
            public override bool IsValid(object value) => true;

            public override string ToDescriptionString() =>
                "# Acceptable values: " + string.Join(", ", UnityInput.Current.SupportedKeyCodes);
        }

        #endregion
    }
}