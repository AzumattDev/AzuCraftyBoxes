using System.Collections.Concurrent;
using System.Diagnostics;
using AzuCraftyBoxes.IContainers;
using Backpacks;
using ItemDataManager;
using static AzuCraftyBoxes.Patches.CacheCurrentCraftingStationPrefabName;

namespace AzuCraftyBoxes.Util.Functions;

public class Boxes
{
    internal static readonly List<Container> Containers = new();
    private static readonly List<Container> ContainersToAdd = new();
    private static readonly List<Container> ContainersToRemove = new();
    private static ConcurrentDictionary<float, Stopwatch> stopwatches = new ConcurrentDictionary<float, Stopwatch>();

    internal static void AddContainer(Container container)
    {
        if (!Containers.Contains(container))
        {
            ContainersToAdd.Add(container);
            if (container)
                AzuCraftyBoxesPlugin.AzuCraftyBoxesLogger.LogIfReleaseAndDebugEnable($"Added container {container.name} to list");
        }

        UpdateContainers();
    }

    internal static void RemoveContainer(Container container)
    {
        if (Containers.Contains(container))
        {
            ContainersToRemove.Add(container);
            if (container)
                AzuCraftyBoxesPlugin.AzuCraftyBoxesLogger.LogIfReleaseAndDebugEnable($"Removed container {container.name} from list");
        }

        UpdateContainers();
    }

    internal static void UpdateContainers()
    {
        foreach (Container container in ContainersToAdd)
        {
            Containers.Add(container);
        }

        ContainersToAdd.Clear();
        foreach (Container container in ContainersToRemove)
        {
            Containers.Remove(container);
        }

        ContainersToRemove.Clear();
    }

    internal static List<IContainer> GetNearbyContainers<T>(T gameObject, float rangeToUse) where T : Component
    {
        List<IContainer> nearbyContainers = [];
        if (Player.m_localPlayer == null) return nearbyContainers;
        IEnumerable<IContainer> kgDrawers = APIs.ItemDrawers_API.AllDrawersInRange(gameObject.transform.position, rangeToUse).Select(kgDrawer.Create);
        IEnumerable<IContainer> backpacksEnumerable = new List<IContainer>();
        IEnumerable<IContainer> gemBagsEnumerable = new List<IContainer>();
        List<IContainer> backpackList = [];
        if (AzuCraftyBoxesPlugin.BackpacksIsLoaded)
        {
            // Get all backpacks in the player inventory
            foreach (ItemDrop.ItemData? allItem in Player.m_localPlayer.GetInventory().GetAllItems().Where(x => x?.Data("org.bepinex.plugins.backpacks")?.Get<ItemContainer>() != null))
            {
                BackpackContainer backpackContainer = BackpackContainer.Create(allItem?.Data("org.bepinex.plugins.backpacks")?.Get<ItemContainer>()!);
                if (backpackList.Contains(backpackContainer)) continue;
                backpackList.Add(backpackContainer);
            }

            backpacksEnumerable = backpackList;

            /*if (Backpacks.API.GetEquippedBackpack()?.Data("org.bepinex.plugins.backpacks")?.Get<ItemContainer>() is {} backpack)
            {
                backpacksEnumerable = new List<IContainer> { BackpackContainer.Create(backpack) };
            }*/
        }

        List<IContainer> gemBagList = new List<IContainer>();
        if (Jewelcrafting.API.IsLoaded()) // assuming you have a method to verify if Jewelcrafting features are loaded
        {
            // Loop through player inventory items that are identified as gem bags.
            foreach (ItemDrop.ItemData? gemBagItem in Player.m_localPlayer.GetInventory().GetAllItems().Where(x => x != null && Jewelcrafting.API.GetItemContainerInventory(x) is not null && Jewelcrafting.API.IsFreelyAccessibleInventory(x)))
            {
                GemBagContainer gemBagContainer = new GemBagContainer(gemBagItem!);
                if (!gemBagList.Contains(gemBagContainer))
                    gemBagList.Add(gemBagContainer);
            }

            gemBagsEnumerable = gemBagList;
        }


        if (Vector3.Distance(gameObject.transform.position, AzuCraftyBoxesPlugin.lastPosition) < 0.5f)
            return AzuCraftyBoxesPlugin.cachedContainerList.Concat(kgDrawers).Concat(backpacksEnumerable).Concat(gemBagsEnumerable).ToList();

        foreach (Container container in Containers)
        {
            if (gameObject == null || container == null) continue;
            float distance = Vector3.Distance(container.transform.position, gameObject.transform.position);
            if (distance <= rangeToUse)
            {
                // log the distance and the range to use
#if DEBUG
                AzuCraftyBoxesPlugin.AzuCraftyBoxesLogger.LogIfReleaseAndDebugEnable($"Distance to container {container.name} is {distance}m, within the range of {rangeToUse}m set to store items for this chest");
#endif
                if (!container.IsInUse())
                {
                    nearbyContainers.Add(VanillaContainer.Create(container));
                }
            }
        }

        AzuCraftyBoxesPlugin.lastPosition = gameObject.transform.position;
        AzuCraftyBoxesPlugin.cachedContainerList = nearbyContainers;
        return nearbyContainers.Concat(kgDrawers).Concat(backpacksEnumerable).Concat(gemBagsEnumerable).ToList();
    }

    public static void AddContainerIfNotExists(string containerName)
    {
        if (!AzuCraftyBoxesPlugin.yamlData.ContainsKey(containerName))
        {
            AzuCraftyBoxesPlugin.yamlData[containerName] = new Dictionary<string, List<string>>
            {
                { "exclude", new List<string>() },
                { "includeOverride", new List<string>() },
            };

            YamlUtils.WriteYaml(AzuCraftyBoxesPlugin.yamlPath);
        }
    }

    // Get a list of all excluded prefabs for all containers in the container data

    public static Dictionary<string, List<string>> GetExcludedPrefabsForAllContainers()
    {
        Dictionary<string, List<string>> excludedPrefabsForAllContainers = new Dictionary<string, List<string>>();

        foreach (string? container in GetAllContainers())
        {
            excludedPrefabsForAllContainers[container] = GetExcludedPrefabs(container);
        }

        return excludedPrefabsForAllContainers;
    }

    // Get a list of all containers
    public static List<string> GetAllContainers()
    {
        return AzuCraftyBoxesPlugin.yamlData.Keys.Where(key => key != "groups").ToList();
    }

    private static bool PassesIncludeExcludeChecks(Dictionary<string, List<string>> data, string prefab)
    {
        // 1. Grab "includeOverride" list
        List<string> includeOverrideList = data.TryGetValue("includeOverride", out List<string> includeValues) ? includeValues : new List<string>();

        // If the prefab is in 'includeOverride', allow immediately
        if (includeOverrideList.Contains(prefab))
        {
            return true;
        }

        // 2. Grab "exclude" list
        List<string> excludeList = data.TryGetValue("exclude", out List<string> excludeValues) ? excludeValues : new List<string>();

        // If the prefab is in 'exclude', disallow
        // or if part of an excluded group, disallow
        foreach (string? excludedItem in excludeList)
        {
            if (prefab.Equals(excludedItem))
            {
                return false;
            }

            // Check group membership
            if (GroupUtils.IsGroupDefined(excludedItem))
            {
                List<string> groupItems = GroupUtils.GetItemsInGroup(excludedItem);
                if (groupItems.Contains(prefab))
                {
                    return false;
                }
            }
        }

        // 3. If we got here, no exclude matched and no includeOverride was needed
        // By default, allow pulling
        return true;
    }

    public static bool CanItemBePulled(string container, string prefab, string stationName = "")
    {
        if (AzuCraftyBoxesPlugin.yamlData == null)
        {
            AzuCraftyBoxesPlugin.AzuCraftyBoxesLogger.LogError("yamlData is null. Make sure to call DeserializeYamlFile() before using CanItemBePulled.");
            return false;
        }

        if (!string.IsNullOrWhiteSpace(CachedStationName))
        {
            stationName = CachedStationName;
        }

        // -----------------------------------------------------------------------
        // 1) Check stationName first (if not empty)
        // -----------------------------------------------------------------------
        if (!string.IsNullOrWhiteSpace(stationName) && AzuCraftyBoxesPlugin.yamlData.TryGetValue(stationName, out Dictionary<string, List<string>>? stationData))
        {
            // Apply station include/exclude logic
            bool stationPass = PassesIncludeExcludeChecks(stationData, prefab);

            if (!stationPass)
            {
                // If the station explicitly excludes this item,
                // we can return false immediately, no need to check container
                return false;
            }
            // If station passed (i.e. not excluded), we still continue
            // to container checks.
            // (If you prefer station "includeOverride" to skip container checks,
            // you can detect that here and return true. But that changes logic.)
        }

        // -----------------------------------------------------------------------
        // 2) Now apply container filters
        // -----------------------------------------------------------------------
        // If container is NOT in yaml, we allow by default
        if (!AzuCraftyBoxesPlugin.yamlData.TryGetValue(container, out Dictionary<string, List<string>>? containerData))
        {
            // Container not found => allow pulling
            return true;
        }

        // Check container include/exclude logic
        bool containerPass = PassesIncludeExcludeChecks(containerData, prefab);
        return containerPass;
    }


    // Check if a prefab is excluded from a container

    /*public static bool CanItemBePulled(string container, string prefab, string stationName = "")
    {
        if (AzuCraftyBoxesPlugin.yamlData == null)
        {
            AzuCraftyBoxesPlugin.AzuCraftyBoxesLogger.LogError("yamlData is null. Make sure to call DeserializeYamlFile() before using CanItemBePulled.");
            return false;
        }

        if (!AzuCraftyBoxesPlugin.yamlData.TryGetValue(container, out Dictionary<string, List<string>> containerData))
        {
            //AzuCraftyBoxesPlugin.AzuCraftyBoxesLogger.LogInfo($"Container '{container}' not found in yamlData.");
            return true; // Allow pulling by default if the container is not defined in yamlData
        }

        List<string> excludeList = containerData.TryGetValue("exclude", out List<string> value1) ? value1 : new List<string>();
        List<string> includeOverrideList = containerData.TryGetValue("includeOverride", out List<string> value) ? value : new List<string>();

        if (includeOverrideList.Contains(prefab))
        {
            return true;
        }

        foreach (object? excludedItem in excludeList)
        {
            if (prefab.Equals(excludedItem))
            {
                return false;
            }

            if (GroupUtils.IsGroupDefined((string)excludedItem))
            {
                List<string> groupItems = GroupUtils.GetItemsInGroup((string)excludedItem);
                if (groupItems.Contains(prefab))
                {
                    return false;
                }
            }
        }

        return true;
    }*/


    internal static bool IsPrefabExcluded(string prefab, List<object> exclusionList)
    {
        if (exclusionList != null)
        {
            foreach (object? excludeItem in exclusionList)
            {
                string excludeItemName = excludeItem.ToString();

                if (AzuCraftyBoxesPlugin.groups.TryGetValue(excludeItemName, out HashSet<string> groupPrefabs))
                {
                    if (groupPrefabs.Contains(prefab))
                    {
                        return true;
                    }
                }
                else if (excludeItemName == prefab)
                {
                    return true;
                }
            }
        }

        return false;
    }

    public static List<string> GetExcludedPrefabs(string container)
    {
        if (AzuCraftyBoxesPlugin.yamlData.TryGetValue(container, out Dictionary<string, List<string>> containerData))
        {
            if (containerData.TryGetValue("exclude", out List<string> excludeList))
            {
                List<string> excludedPrefabs = new List<string>();
                foreach (string excludeItem in excludeList)
                {
                    if (AzuCraftyBoxesPlugin.groups.TryGetValue(excludeItem, out HashSet<string> groupPrefabs))
                    {
                        excludedPrefabs.AddRange(groupPrefabs);
                    }
                    else
                    {
                        excludedPrefabs.Add(excludeItem);
                    }
                }

                return excludedPrefabs;
            }
        }

        return new List<string>();
    }

    public static Stopwatch GetStopwatch(GameObject o)
    {
        float hash = GetGameObjectPosHash(o);
        Stopwatch stopwatch = null;

        if (!stopwatches.TryGetValue(hash, out stopwatch))
        {
            stopwatch = new Stopwatch();
            stopwatches.TryAdd(hash, stopwatch);
        }

        return stopwatch;
    }

    private static float GetGameObjectPosHash(GameObject o)
    {
        return (1000f * o.transform.position.x) + o.transform.position.y + (.001f * o.transform.position.z);
    }


    internal static int CheckAndDecrement(int amount)
    {
        if (amount <= 0) return amount;
        if (AzuCraftyBoxesPlugin.leaveOne.Value == AzuCraftyBoxesPlugin.Toggle.On)
        {
            return amount - 1;
        }

        return amount;
    }

    public class LaterConsumption(string name, int amount, int quality, IContainer sourceContainer, ItemDrop.ItemData requiredItem)
    {
        public string Name { get; set; } = name;
        public int Amount { get; set; } = amount;
        public int Quality { get; set; } = quality;
        public IContainer SourceContainer { get; set; } = sourceContainer;
        public ItemDrop.ItemData RequiredItem { get; set; } = requiredItem;
    }
}

public static class ConsumptionManager
{
    public static ConcurrentBag<Boxes.LaterConsumption> PendingConsumptions { get; } = [];
}