using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
//using AzuCraftyBoxes.Compatibility.EpicLoot;
using AzuCraftyBoxes.IContainers;
using AzuCraftyBoxes.Util.Functions;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using JetBrains.Annotations;
using ServerSync;
using UnityEngine;

namespace AzuCraftyBoxes
{
    [BepInPlugin(ModGUID, ModName, ModVersion)]
    [BepInDependency("kg.ItemDrawers", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("org.bepinex.plugins.backpacks", BepInDependency.DependencyFlags.SoftDependency)]
    //[BepInDependency(EpicLootReflectionHelper.elGuid, BepInDependency.DependencyFlags.SoftDependency)]
    [BepInIncompatibility("aedenthorn.CraftFromContainers")]
    [BepInIncompatibility("CFCMod")]
    public class AzuCraftyBoxesPlugin : BaseUnityPlugin
    {
        internal const string ModName = "AzuCraftyBoxes";
        internal const string ModVersion = "1.4.1";
        internal const string Author = "Azumatt";
        private const string ModGUID = $"{Author}.{ModName}";
        private static string ConfigFileName = $"{ModGUID}.cfg";
        private static string ConfigFileFullPath = Paths.ConfigPath + Path.DirectorySeparatorChar + ConfigFileName;
        internal static string ConnectionError = "";
        private readonly Harmony _harmony = new(ModGUID);
        public static readonly ManualLogSource AzuCraftyBoxesLogger = BepInEx.Logging.Logger.CreateLogSource(ModName);
        private static readonly ConfigSync ConfigSync = new(ModGUID) { DisplayName = ModName, CurrentVersion = ModVersion, MinimumRequiredVersion = ModVersion };

        internal static bool skip;
        public static Vector3 lastPosition = Vector3.positiveInfinity;
        public static List<IContainer> cachedContainerList = new List<IContainer>();

        internal static readonly string yamlFileName = $"{Author}.{ModName}.yml";
        internal static readonly string yamlPath = Paths.ConfigPath + Path.DirectorySeparatorChar + yamlFileName;
        internal static readonly CustomSyncedValue<string> CraftyContainerData = new(ConfigSync, "craftyboxesData", "");
        internal static readonly CustomSyncedValue<string> CraftyContainerGroupsData = new(ConfigSync, "craftyboxesGroupsData", "");

        //
        internal static Dictionary<string, object> yamlData;
        internal static Dictionary<string, HashSet<string>> groups;
        internal static Dictionary<string, bool> CanItemBePulledCache = null!;

        internal static Assembly? epicLootAssembly;
        internal static bool BackpacksIsLoaded = false;

        public enum Toggle
        {
            On = 1,
            Off = 0,
        }

        public void Awake()
        {
            _serverConfigLocked = config("1 - General", "Lock Configuration", Toggle.On, "If on, the configuration is locked and can be changed by server admins only.");
            _ = ConfigSync.AddLockingConfigEntry(_serverConfigLocked);

            ModEnabled = config("1 - General", "Mod Enabled", Toggle.On, "If off, everything in the mod will not run. This is useful if you want to disable the mod without uninstalling it.");
            mRange = config("2 - CraftyBoxes", "Container Range", 20f, "The maximum range from which to pull items from.");
            resourceString = TextEntryConfig("2 - CraftyBoxes", "ResourceCostString", "{0}/{1}", "String used to show required and available resources. {0} is replaced by how much is available, and {1} is replaced by how much is required. Set to nothing to leave it as default.", false);
            flashColor = config("2 - CraftyBoxes", "FlashColor", Color.yellow, "Resource amounts will flash to this colour when coming from containers", false);
            unFlashColor = config("2 - CraftyBoxes", "UnFlashColor", Color.white, "Resource amounts will flash from this colour when coming from containers (set both colors to the same color for no flashing)", false);
            canbuildDisplayColor = config("2 - CraftyBoxes", "Can Build Color", Color.green, "The color of the build panel's count of pieces you can build", false);
            cannotbuildDisplayColor = config("2 - CraftyBoxes", "Cannot Build Color", Color.red, "The color of the build panel's count if you cannot build something", false);
            pulledMessage = TextEntryConfig("2 - CraftyBoxes", "PulledMessage", "Pulled items to inventory", "Message to show after pulling items to player inventory", false);
            //pullItemsKey = config("3 - Keys", "PullItemsKey", new KeyboardShortcut(KeyCode.LeftControl), new ConfigDescription("Holding down this key while crafting or building will pull resources into your inventory instead of building. Use https://docs.unity3d.com/Manual/ConventionalGameInput.html", new AcceptableShortcuts()), false);
            fillAllModKey = config("3 - Keys", "FillAllModKey", new KeyboardShortcut(KeyCode.LeftShift), new ConfigDescription("Modifier key to pull all available fuel or ore when down. Use https://docs.unity3d.com/Manual/ConventionalGameInput.html", new AcceptableShortcuts()), false);


            if (!File.Exists(yamlPath))
            {
                WriteConfigFileFromResource(yamlPath);
            }

            CraftyContainerData.ValueChanged += OnValChangedUpdate; // check for file changes
            CraftyContainerData.AssignLocalValue(File.ReadAllText(yamlPath));

            Assembly assembly = Assembly.GetExecutingAssembly();
            _harmony.PatchAll(assembly);
            SetupWatcher();
        }

        private static void WriteConfigFileFromResource(string configFilePath)
        {
            Assembly assembly = Assembly.GetExecutingAssembly();
            string resourceName = "AzuCraftyBoxes.Example.yml";

            using Stream resourceStream = assembly.GetManifestResourceStream(resourceName);
            if (resourceStream == null)
            {
                throw new FileNotFoundException($"Resource '{resourceName}' not found in the assembly.");
            }

            using StreamReader reader = new StreamReader(resourceStream);
            string contents = reader.ReadToEnd();

            File.WriteAllText(configFilePath, contents);
        }

        private void Start()
        {
            AutoDoc();

            // Get Azumatt.AzuAntiArthriticCrafting from the chainloader if possible
            Chainloader.PluginInfos.TryGetValue("Azumatt.AzuAntiArthriticCrafting", out PluginInfo antiArthriticCraftingPlugin);
            if (antiArthriticCraftingPlugin != null)
            {
                AzuCraftyBoxesLogger.LogInfo("AzuAntiArthriticCrafting found, enabling compatibility");
                // Get the AzuAntiArthriticCrafting.Patches.HaveRequirementItemsTranspiler.GetCurrentCraftAmount method
                var aaaCraftingAssembly = antiArthriticCraftingPlugin.Instance.GetType().Assembly;
                MethodInfo getCurrentCraftAmountMethod = aaaCraftingAssembly.GetType("AzuAntiArthriticCrafting.Patches.HaveRequirementItemsTranspiler").GetMethod("GetCurrentCraftAmount");
                if (getCurrentCraftAmountMethod != null)
                {
                    // Add the method to the AzuCraftyBoxes.Util.Functions.MiscFunctions.GetCurrentCraftAmountMethod
                    MiscFunctions.GetCurrentCraftAmountMethod = getCurrentCraftAmountMethod;
                }
            }

            if (Chainloader.PluginInfos.ContainsKey("org.bepinex.plugins.backpacks"))
            {
                BackpacksIsLoaded = true;
            }
            
            // if (!Chainloader.PluginInfos.ContainsKey(EpicLootReflectionHelper.elGuid)) return;
            // epicLootAssembly = Chainloader.PluginInfos[EpicLootReflectionHelper.elGuid].Instance.GetType().Assembly;
        }

        private void LateUpdate()
        {
            skip = false;
        }

        private void AutoDoc()
        {
#if DEBUG
            // Store Regex to get all characters after a [
            Regex regex = new(@"\[(.*?)\]");

            // Strip using the regex above from Config[x].Description.Description
            string Strip(string x) => regex.Match(x).Groups[1].Value;
            StringBuilder sb = new();
            string lastSection = "";
            foreach (ConfigDefinition x in Config.Keys)
            {
                // skip first line
                if (x.Section != lastSection)
                {
                    lastSection = x.Section;
                    sb.Append($"{Environment.NewLine}`{x.Section}`{Environment.NewLine}");
                }

                sb.Append($"\n{x.Key} [{Strip(Config[x].Description.Description)}]" +
                          $"{Environment.NewLine}   * {Config[x].Description.Description.Replace("[Synced with Server]", "").Replace("[Not Synced with Server]", "")}" +
                          $"{Environment.NewLine}     * Default Value: {Config[x].GetSerializedValue()}{Environment.NewLine}");
            }

            File.WriteAllText(Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!, $"{ModName}_AutoDoc.md"), sb.ToString());
#endif
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

            FileSystemWatcher yamlwatcher = new(Paths.ConfigPath, yamlFileName);
            yamlwatcher.Changed += ReadYamlFiles;
            yamlwatcher.Created += ReadYamlFiles;
            yamlwatcher.Renamed += ReadYamlFiles;
            yamlwatcher.IncludeSubdirectories = true;
            yamlwatcher.SynchronizingObject = ThreadingHelper.SynchronizingObject;
            yamlwatcher.EnableRaisingEvents = true;
        }

        private void ReadConfigValues(object sender, FileSystemEventArgs e)
        {
            if (!File.Exists(ConfigFileFullPath)) return;
            try
            {
                AzuCraftyBoxesLogger.LogDebug("ReadConfigValues called");
                Config.Reload();
            }
            catch
            {
                AzuCraftyBoxesLogger.LogError($"There was an issue loading your {ConfigFileName}");
                AzuCraftyBoxesLogger.LogError("Please check your config entries for spelling and format!");
            }
        }

        private void ReadYamlFiles(object sender, FileSystemEventArgs e)
        {
            if (!File.Exists(yamlPath)) return;
            try
            {
                AzuCraftyBoxesLogger.LogDebug("ReadConfigValues called");
                CraftyContainerData.AssignLocalValue(File.ReadAllText(yamlPath));
            }
            catch
            {
                AzuCraftyBoxesLogger.LogError($"There was an issue loading your {yamlFileName}");
                AzuCraftyBoxesLogger.LogError("Please check your entries for spelling and format!");
            }
        }

        private static void OnValChangedUpdate()
        {
            AzuCraftyBoxesLogger.LogDebug("OnValChanged called");
            try
            {
                YamlUtils.ReadYaml(CraftyContainerData.Value);
                YamlUtils.ParseGroups();
            }
            catch (Exception e)
            {
                AzuCraftyBoxesLogger.LogError($"Failed to deserialize {yamlFileName}: {e}");
            }
        }


        #region ConfigOptions

        private static ConfigEntry<Toggle> _serverConfigLocked = null!;
        internal static ConfigEntry<Toggle> ModEnabled = null!;
        public static ConfigEntry<Color> flashColor = null!;
        public static ConfigEntry<Color> unFlashColor = null!;
        public static ConfigEntry<Color> canbuildDisplayColor = null!;
        public static ConfigEntry<Color> cannotbuildDisplayColor = null!;
        public static ConfigEntry<string> resourceString = null!;
        public static ConfigEntry<string> pulledMessage = null!;
        public static ConfigEntry<KeyboardShortcut> pullItemsKey = null!;
        public static ConfigEntry<KeyboardShortcut> fillAllModKey = null!;
        public static ConfigEntry<float> mRange = null!;

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

        internal ConfigEntry<T> TextEntryConfig<T>(string group, string name, T value, string desc,
            bool synchronizedSetting = true)
        {
            ConfigurationManagerAttributes attributes = new()
            {
                CustomDrawer = TextAreaDrawer,
            };
            return config(group, name, value, new ConfigDescription(desc, null, attributes), synchronizedSetting);
        }

        internal static void TextAreaDrawer(ConfigEntryBase entry)
        {
            GUILayout.ExpandHeight(true);
            GUILayout.ExpandWidth(true);
            entry.BoxedValue = GUILayout.TextArea((string)entry.BoxedValue, GUILayout.ExpandWidth(true),
                GUILayout.ExpandHeight(true));
        }

        private class ConfigurationManagerAttributes
        {
            [UsedImplicitly] public int? Order;
            [UsedImplicitly] public bool? Browsable;
            [UsedImplicitly] public string? Category;
            [UsedImplicitly] public Action<ConfigEntryBase>? CustomDrawer;
        }

        class AcceptableShortcuts : AcceptableValueBase // Used for KeyboardShortcut Configs 
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

    public static class KeyboardExtensions
    {
        public static bool IsKeyDown(this KeyboardShortcut shortcut)
        {
            return shortcut.MainKey != KeyCode.None && Input.GetKeyDown(shortcut.MainKey) && shortcut.Modifiers.All(Input.GetKey);
        }

        public static bool IsKeyHeld(this KeyboardShortcut shortcut)
        {
            return shortcut.MainKey != KeyCode.None && Input.GetKey(shortcut.MainKey) && shortcut.Modifiers.All(Input.GetKey);
        }
    }
}