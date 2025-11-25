using System.IO;
using System.Text.RegularExpressions;
using AzuCraftyBoxes.Compatibility.EpicLoot;
using AzuCraftyBoxes.IContainers;
using AzuCraftyBoxes.Patches;
using AzuCraftyBoxes.Util;
using AzuCraftyBoxes.Util.Functions;

namespace AzuCraftyBoxes
{
    [BepInPlugin(ModGUID, ModName, ModVersion)]
    [BepInDependency("kg.ItemDrawers", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("org.bepinex.plugins.backpacks", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("org.bepinex.plugins.jewelcrafting", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInIncompatibility("aedenthorn.CraftFromContainers")]
    [BepInIncompatibility("CFCMod")]
    public class AzuCraftyBoxesPlugin : BaseUnityPlugin
    {
        internal const string ModName = "AzuCraftyBoxes";
        internal const string ModVersion = "1.8.9";
        internal const string Author = "Azumatt";
        private const string ModGUID = $"{Author}.{ModName}";
        private static string ConfigFileName = $"{ModGUID}.cfg";
        private static string ConfigFileFullPath = Paths.ConfigPath + Path.DirectorySeparatorChar + ConfigFileName;
        internal static string ConnectionError = "";
        internal static readonly Harmony harmony = new(ModGUID);
        public static readonly ManualLogSource AzuCraftyBoxesLogger = BepInEx.Logging.Logger.CreateLogSource(ModName);
        private static readonly ConfigSync ConfigSync = new(ModGUID) { DisplayName = ModName, CurrentVersion = ModVersion, MinimumRequiredVersion = ModVersion };
        private FileSystemWatcher _watcher = null!;
        private FileSystemWatcher _yamlwatcher = null!;
        private readonly object _reloadLock = new();
        private readonly object _yamlreloadLock = new();
        private DateTime _lastConfigReloadTime;
        private DateTime _yamllastConfigReloadTime;
        private const long RELOAD_DELAY = 10000000; // One second

        internal static bool skip;
        public static Vector3 lastPosition = Vector3.positiveInfinity;
        public static List<IContainer> cachedContainerList = new List<IContainer>();

        internal static readonly string yamlFileName = $"{Author}.{ModName}.yml";
        internal static readonly string yamlPath = Paths.ConfigPath + Path.DirectorySeparatorChar + yamlFileName;
        internal static readonly CustomSyncedValue<string> CraftyContainerData = new(ConfigSync, "craftyboxesData", "");
        internal static readonly CustomSyncedValue<string> CraftyContainerGroupsData = new(ConfigSync, "craftyboxesGroupsData", "");
        internal const string PreventPullingLogicKey = "ACB_PreventPulling";

        //
        internal static Dictionary<string, Dictionary<string, List<string>>> yamlData = null!;
        internal static Dictionary<string, HashSet<string>> groups = new();
        internal static Dictionary<string, bool> CanItemBePulledCache = null!;
        internal static bool BackpacksIsLoaded = false;

        public enum Toggle
        {
            On = 1,
            Off = 0,
        }

        public void Awake()
        {
            bool saveOnSet = Config.SaveOnConfigSet;
            Config.SaveOnConfigSet = false;

            _serverConfigLocked = config("1 - General", "Lock Configuration", Toggle.On, "If on, the configuration is locked and can be changed by server admins only.");
            _ = ConfigSync.AddLockingConfigEntry(_serverConfigLocked);

            ModEnabled = config("1 - General", "Mod Enabled", Toggle.On, "If off, everything in the mod will not run. This is useful if you want to disable the mod without uninstalling it.");
            debugLogsEnabled = config("1 - General", "Output Debug Logs", Toggle.Off, "If on, the debug logs will be displayed in the BepInEx console window when BepInEx debugging is enabled.");
            preventPullingLogicMessage = config("1 - General", "Prevent Pulling Message", Toggle.On, "If on, a message will be displayed above the player's head when the prevention pulling logic is toggled using the keybind.", false);
            preventPullingStringFormat = config("1 - General", "Prevent Pulling Format", "<size=30><color=#ffffff>{0}</color></size>\n<size=25>{1}</size>", "String format for the message displayed when the prevention pulling logic is toggled. {0} is replaced by the message, and {1} is replaced by the on/off status. Set to nothing to leave it as default.", false);
            preventPullingStatusEffectDisplay = config("1 - General", "Prevent Pulling Status", Toggle.On, "If on, the status effect will be displayed when you cannot pull from containers.", false);
            preventPullingStatusEffectDisplay.SettingChanged += (sender, args) =>
            {
                if (Player.m_localPlayer != null)
                    SE_ContainerPull.CheckAndSetStatusEffect(Player.m_localPlayer);
            };
            mRange = config("2 - CraftyBoxes", "Container Range", 20f, "The maximum range from which to pull items from.");
            leaveOne = config("2 - CraftyBoxes", "Leave One Item", Toggle.Off, new ConfigDescription("* If on, leaves one item in the chest when pulling from it, so that you are able to pull from it again and store items more easily with other mods. (Such as AzuAutoStore or QuickStackStore). If off, it will pull all items from the chest.", null, new ConfigurationManagerAttributes() { Order = 2 }));
            resourceString = TextEntryConfig("2 - CraftyBoxes", "ResourceCostString", "{0}/{1}", new ConfigDescription("String used to show required and available resources. {0} is replaced by how much is available, and {1} is replaced by how much is required. Set to nothing to leave it as default.", null, new ConfigurationManagerAttributes() { Order = 1 }), false);
            flashColor = config("2 - CraftyBoxes", "FlashColor", Color.yellow, "Resource amounts will flash to this colour when coming from containers", false);
            unFlashColor = config("2 - CraftyBoxes", "UnFlashColor", Color.white, "Resource amounts will flash from this colour when coming from containers (set both colors to the same color for no flashing)", false);
            canbuildDisplayColor = config("2 - CraftyBoxes", "Can Build Color", Color.green, "The color of the build panel's count of pieces you can build", false);
            cannotbuildDisplayColor = config("2 - CraftyBoxes", "Cannot Build Color", Color.red, "The color of the build panel's count if you cannot build something", false);
            //pulledMessage = TextEntryConfig("2 - CraftyBoxes", "PulledMessage", "Pulled items to inventory", "Message to show after pulling items to player inventory", false);
            //pullItemsKey = config("3 - Keys", "PullItemsKey", new KeyboardShortcut(KeyCode.LeftControl), new ConfigDescription("Holding down this key while crafting or building will pull resources into your inventory instead of building. Use https://docs.unity3d.com/Manual/ConventionalGameInput.html", new AcceptableShortcuts()), false);
            fillAllModKey = config("3 - Keys", "FillAllModKey", new KeyboardShortcut(KeyCode.LeftShift), new ConfigDescription("Modifier key to pull all available fuel or ore when down. Use https://docs.unity3d.com/Manual/ConventionalGameInput.html", new AcceptableShortcuts()), false);
            preventPullingLogic = config("3 - Keys", "Prevent Pulling Logic", new KeyboardShortcut(KeyCode.O, KeyCode.LeftAlt), new ConfigDescription("Key to prevent pulling from nearby containers. This prevents all pulling logic from running, essentially making the mod appear as if it's not installed. This is different from the Mod Enabled option because it allows toggling on the fly (specifically for you as the player)  Use https://docs.unity3d.com/Manual/ConventionalGameInput.html", new AcceptableShortcuts()), false);

            if (!File.Exists(yamlPath))
            {
                WriteConfigFileFromResource(yamlPath);
            }

            CraftyContainerData.ValueChanged += OnValChangedUpdate; // check for file changes
            CraftyContainerData.AssignLocalValue(File.ReadAllText(yamlPath));

            Assembly assembly = Assembly.GetExecutingAssembly();
            harmony.PatchAll(assembly);
            SetupWatcher();

            Config.Save();
            if (saveOnSet)
            {
                Config.SaveOnConfigSet = saveOnSet;
            }

            SE_ContainerPull.CreateEffect();
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

            if (Chainloader.PluginInfos.ContainsKey("org.bepinex.plugins.backpacks"))
            {
                BackpacksIsLoaded = true;
            }

            if (!Chainloader.PluginInfos.TryGetValue(EpicLoot.ElGuid, out PluginInfo? epicLootInfo)) return;
            EpicLoot.Init(epicLootInfo);
        }

        private void Update()
        {
            Player? player = Player.m_localPlayer;
            if (player == null) return;

            if (!player.m_customData.TryGetValue(PreventPullingLogicKey, out string value) || !int.TryParse(value, out int result))
            {
                // Initialize custom data if not set or invalid value present
                player.m_customData[PreventPullingLogicKey] = "1";
                result = 1;
            }

            if (preventPullingLogic.Value.IsKeyDown() && player.TakeInput())
            {
                bool isAllowed = player.TogglePullingAllowed(); // now uses 1=allowed, 0=prevented
                var onOff = isAllowed
                    ? "<color=green>Yes</color>"
                    : "<color=red>No</color>";
                string message = $"Pull from containers?";
                AzuCraftyBoxesLogger.LogIfReleaseAndDebugEnable(message);
                if (preventPullingLogicMessage.Value.isOn())
                {
                    Chat.instance.AddInworldText(
                        player.gameObject,
                        player.GetPlayerID(),
                        player.GetHeadPoint(),
                        Talker.Type.Normal,
                        UserInfo.GetLocalUser(),
                        Localization.instance.Localize(string.Format(preventPullingStringFormat.Value, message, onOff))
                    );
                }

                if (!isAllowed && preventPullingStatusEffectDisplay.Value.isOn())
                {
                    player.m_seman.AddStatusEffect(SE_ContainerPull.SE_ContainerPulling);
                }
                else
                {
                    player.m_seman.RemoveStatusEffect(SE_ContainerPull.SE_ContainerPulling);
                }

                result = isAllowed ? 1 : 0;
                player.m_customData[PreventPullingLogicKey] = result.ToString();
            }
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
            SaveWithRespectToConfigSet();
            _watcher?.Dispose();
            _yamlwatcher?.Dispose();
        }

        private void SetupWatcher()
        {
            _watcher = new(Paths.ConfigPath, ConfigFileName);
            _watcher.Changed += ReadConfigValues;
            _watcher.Created += ReadConfigValues;
            _watcher.Renamed += ReadConfigValues;
            _watcher.IncludeSubdirectories = true;
            _watcher.SynchronizingObject = ThreadingHelper.SynchronizingObject;
            _watcher.EnableRaisingEvents = true;

            _yamlwatcher = new(Paths.ConfigPath, yamlFileName);
            _yamlwatcher.Changed += ReadYamlFiles;
            _yamlwatcher.Created += ReadYamlFiles;
            _yamlwatcher.Renamed += ReadYamlFiles;
            _yamlwatcher.IncludeSubdirectories = true;
            _yamlwatcher.SynchronizingObject = ThreadingHelper.SynchronizingObject;
            _yamlwatcher.EnableRaisingEvents = true;
        }

        private void ReadConfigValues(object sender, FileSystemEventArgs e)
        {
            DateTime now = DateTime.Now;
            long time = now.Ticks - _lastConfigReloadTime.Ticks;
            if (time < RELOAD_DELAY)
            {
                return;
            }

            lock (_reloadLock)
            {
                if (!File.Exists(ConfigFileFullPath))
                {
                    AzuCraftyBoxesLogger.LogWarning("Config file does not exist. Skipping reload.");
                    return;
                }

                try
                {
                    AzuCraftyBoxesLogger.LogDebug("Reloading configuration...");
                    SaveWithRespectToConfigSet(true);
                    AzuCraftyBoxesLogger.LogInfo("Configuration reload complete.");
                }
                catch (Exception ex)
                {
                    AzuCraftyBoxesLogger.LogError($"Error reloading configuration: {ex.Message}");
                }
            }

            _lastConfigReloadTime = now;
        }

        private void ReadYamlFiles(object sender, FileSystemEventArgs e)
        {
            DateTime now = DateTime.Now;
            long time = now.Ticks - _yamllastConfigReloadTime.Ticks;
            if (time < RELOAD_DELAY)
            {
                return;
            }

            lock (_yamlreloadLock)
            {
                if (!File.Exists(yamlPath))
                {
                    AzuCraftyBoxesLogger.LogWarning("Yaml config file does not exist!");
                    return;
                }

                try
                {
                    AzuCraftyBoxesLogger.LogDebug("ReadConfigValues called");
                    CraftyContainerData.AssignLocalValue(File.ReadAllText(yamlPath));
                }
                catch (Exception ex)
                {
                    AzuCraftyBoxesLogger.LogError($"There was an issue loading your {yamlFileName}");
                    AzuCraftyBoxesLogger.LogError($"Please check your entries for spelling and format!\n {ex}");
                }
            }

            _yamllastConfigReloadTime = now;
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

        private void SaveWithRespectToConfigSet(bool reload = false)
        {
            bool originalSaveOnSet = Config.SaveOnConfigSet;
            Config.SaveOnConfigSet = false;
            if (reload)
                Config.Reload();
            Config.Save();
            if (originalSaveOnSet)
            {
                Config.SaveOnConfigSet = originalSaveOnSet;
            }
        }


        #region ConfigOptions

        private static ConfigEntry<Toggle> _serverConfigLocked = null!;
        internal static ConfigEntry<Toggle> ModEnabled = null!;
        internal static ConfigEntry<Toggle> debugLogsEnabled = null!;
        internal static ConfigEntry<Toggle> leaveOne = null!;
        public static ConfigEntry<Color> flashColor = null!;
        public static ConfigEntry<Color> unFlashColor = null!;
        public static ConfigEntry<Color> canbuildDisplayColor = null!;
        public static ConfigEntry<Color> cannotbuildDisplayColor = null!;
        public static ConfigEntry<string> resourceString = null!;
        public static ConfigEntry<string> pulledMessage = null!;
        public static ConfigEntry<KeyboardShortcut> pullItemsKey = null!;
        public static ConfigEntry<KeyboardShortcut> fillAllModKey = null!;
        public static ConfigEntry<KeyboardShortcut> preventPullingLogic = null!;
        public static ConfigEntry<Toggle> preventPullingLogicMessage = null!;
        public static ConfigEntry<string> preventPullingStringFormat = null!;
        public static ConfigEntry<Toggle> preventPullingStatusEffectDisplay = null!;
        public static ConfigEntry<float> mRange = null!;

        private ConfigEntry<T> config<T>(string group, string name, T value, ConfigDescription description, bool synchronizedSetting = true)
        {
            ConfigDescription extendedDescription = new(description.Description + (synchronizedSetting ? " [Synced with Server]" : " [Not Synced with Server]"), description.AcceptableValues, description.Tags);
            ConfigEntry<T> configEntry = Config.Bind(group, name, value, extendedDescription);
            SyncedConfigEntry<T> syncedConfigEntry = ConfigSync.AddConfigEntry(configEntry);
            syncedConfigEntry.SynchronizedConfig = synchronizedSetting;

            return configEntry;
        }

        private ConfigEntry<T> config<T>(string group, string name, T value, string description, bool synchronizedSetting = true)
        {
            return config(group, name, value, new ConfigDescription(description), synchronizedSetting);
        }

        internal ConfigEntry<T> TextEntryConfig<T>(string group, string name, T value, string desc, bool synchronizedSetting = true)
        {
            ConfigurationManagerAttributes attributes = new()
            {
                CustomDrawer = TextAreaDrawer,
            };
            return config(group, name, value, new ConfigDescription(desc, null, attributes), synchronizedSetting);
        }

        internal ConfigEntry<T> TextEntryConfig<T>(string group, string name, T value, ConfigDescription description, bool synchronizedSetting = true)
        {
            ConfigurationManagerAttributes attributes = new()
            {
                CustomDrawer = TextAreaDrawer,
            };
            return config(group, name, value, new ConfigDescription(description.Description, null, attributes), synchronizedSetting);
        }

        internal static void TextAreaDrawer(ConfigEntryBase entry)
        {
            GUILayout.ExpandHeight(true);
            GUILayout.ExpandWidth(true);
            entry.BoxedValue = GUILayout.TextArea((string)entry.BoxedValue, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
        }

        private class ConfigurationManagerAttributes
        {
            [UsedImplicitly] public int? Order = null!;
            [UsedImplicitly] public bool? Browsable = null!;
            [UsedImplicitly] public string Category = null!;
            [UsedImplicitly] public Action<ConfigEntryBase>? CustomDrawer = null!;
        }

        class AcceptableShortcuts : AcceptableValueBase // Used for KeyboardShortcut Configs 
        {
            public AcceptableShortcuts() : base(typeof(KeyboardShortcut))
            {
            }

            public override object Clamp(object value) => value;
            public override bool IsValid(object value) => true;

            public override string ToDescriptionString() => "# Acceptable values: " + string.Join(", ", UnityInput.Current.SupportedKeyCodes);
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

    public static class LoggerExtensions
    {
        public static void LogIfReleaseAndDebugEnable(this ManualLogSource logger, string message)
        {
#if Release
            if (AzuCraftyBoxesPlugin.debugLogsEnabled.Value.isOn())
            {
                logger.LogDebug(message);
            }
#endif
        }

        public static void LogIfDebugBuild(this ManualLogSource logger, string message)
        {
#if DEBUG
            logger.LogDebug(message);
#endif
        }

        public static void LogIfDebuggingEpicLoot(this ManualLogSource logger, string message)
        {
#if EpicLootTesting
            logger.LogDebug(message);
#endif
        }
    }

    public static class ToggleExtensions
    {
        public static bool isOn(this AzuCraftyBoxesPlugin.Toggle toggle)
        {
            return toggle == AzuCraftyBoxesPlugin.Toggle.On;
        }

        public static bool isOff(this AzuCraftyBoxesPlugin.Toggle toggle)
        {
            return toggle == AzuCraftyBoxesPlugin.Toggle.Off;
        }
    }
}