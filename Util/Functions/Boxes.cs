using System.Collections.Concurrent;
using System.Diagnostics;
using AzuCraftyBoxes.IContainers;
using Backpacks;
using ItemDataManager;
using UnityEngine.Pool;
using static AzuCraftyBoxes.Patches.CacheCurrentCraftingStationPrefabName;

namespace AzuCraftyBoxes.Util.Functions;

public class Boxes
{
    internal static readonly HashSet<Container> Containers = new();
    private static readonly HashSet<Container> ContainersToAdd = new();
    private static readonly HashSet<Container> ContainersToRemove = new();
    private static int _lastRegistryFrame; // so we only flush once per frame
    private static ConcurrentDictionary<float, Stopwatch> stopwatches = new ConcurrentDictionary<float, Stopwatch>();

    internal static void AddContainer(Container container)
    {
        if (!container) return;
        if (!Containers.Contains(container))
        {
            ContainersToAdd.Add(container);
            if (container)
                AzuCraftyBoxesPlugin.AzuCraftyBoxesLogger.LogIfReleaseAndDebugEnable($"Added container {container.name} to list");
        }
    }

    internal static void RemoveContainer(Container container)
    {
        if (!container) return;
        if (Containers.Contains(container))
        {
            ContainersToRemove.Add(container);
            if (container)
                AzuCraftyBoxesPlugin.AzuCraftyBoxesLogger.LogIfReleaseAndDebugEnable($"Removed container {container.name} from list");
        }
    }

    private static void FlushRegistryIfNeeded()
    {
        int f = Time.frameCount;
        if (_lastRegistryFrame == f) return;
        _lastRegistryFrame = f;

        if (ContainersToAdd.Count > 0)
        {
            foreach (var c in ContainersToAdd) Containers.Add(c);
            ContainersToAdd.Clear();
        }

        if (ContainersToRemove.Count > 0)
        {
            foreach (var c in ContainersToRemove) Containers.Remove(c);
            ContainersToRemove.Clear();
        }
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

    private static readonly List<IContainer> _scratchNearby = new(256);
    private static readonly List<IContainer> _scratchDrawers = new(128);
    private static readonly List<IContainer> _scratchBackpacks = new(32);
    private static readonly List<IContainer> _scratchGemBags = new(32);

    private static Vector3 _lastQueryPos = Vector3.positiveInfinity;
    private static float _lastQueryRange;
    private static float _lastQueryTime;
    private static readonly float _cacheWindow = 0.25f; // tweakable

    // The fully built cached list:
    private static readonly List<IContainer> _cachedAll = new(256);

    internal static List<IContainer> GetNearbyContainers<T>(T src, float rangeMeters) where T : Component
    {
        if (!Player.m_localPlayer || !src) return EmptyIContainers();

        FlushRegistryIfNeeded();

        Vector3 pos = src.transform.position;

        // Reuse cache if player hasn't moved much & within time window
        if ((Time.time - _lastQueryTime) <= _cacheWindow &&
            (pos - _lastQueryPos).sqrMagnitude < 0.25f * 0.25f &&
            Mathf.Approximately(rangeMeters, _lastQueryRange))
        {
            return _cachedAll; // already a combined (nearby + drawers + bags) list
        }

        _scratchNearby.Clear();
        _scratchDrawers.Clear();
        _scratchBackpacks.Clear();
        _scratchGemBags.Clear();
        _cachedAll.Clear();

        // 1) Nearby vanilla containers (no LINQ, squared distance, in-use check)
        float r2 = rangeMeters * rangeMeters;
        foreach (var c in Containers)
        {
            if (!c) continue;
            // Optional: skip closed wagons in-use
            if (c.m_wagon != null && c.m_wagon.InUse()) continue;

            Vector3 d = c.transform.position - pos;
            if (d.sqrMagnitude > r2) continue;

            if (!c.IsInUse() || c.IsInUse() && c.IsOwner())
            {
                _scratchNearby.Add(VanillaContainer.Create(c));
            }
        }


        var drawers = APIs.ItemDrawers_API.AllDrawersInRange(pos, rangeMeters);
        foreach (var d in drawers)
            _scratchDrawers.Add(kgDrawer.Create(d));
        
        if (AzuCraftyBoxesPlugin.BackpacksIsLoaded)
        {
            var items = Player.m_localPlayer.GetInventory().GetAllItems();
            // de-dupe by reference
            var seen = HashSetPool<ItemContainer>.Get();
            for (int i = 0; i < items.Count; ++i)
            {
                var it = items[i];
                if (it == null) continue;

                var data = it.Data("org.bepinex.plugins.backpacks");
                if (data == null) continue;

                var cont = data.Get<ItemContainer>();
                if (cont == null) continue;

                if (seen.Add(cont))
                    _scratchBackpacks.Add(BackpackContainer.Create(cont));
            }

            HashSetPool<ItemContainer>.Release(seen);
        }
        
        if (Jewelcrafting.API.IsLoaded())
        {
            var items = Player.m_localPlayer.GetInventory().GetAllItems();
            for (int i = 0; i < items.Count; ++i)
            {
                var it = items[i];
                if (it == null) continue;

                if (Jewelcrafting.API.IsFreelyAccessibleInventory(it))
                {
                    // fast path: we assume GetItemContainerInventory(it) != null when accessible
                    if (Jewelcrafting.API.GetItemContainerInventory(it) != null)
                        _scratchGemBags.Add(new GemBagContainer(it));
                }
            }
        }


        _cachedAll.AddRange(_scratchNearby);
        _cachedAll.AddRange(_scratchDrawers);
        _cachedAll.AddRange(_scratchBackpacks);
        _cachedAll.AddRange(_scratchGemBags);

        _lastQueryPos = pos;
        _lastQueryRange = rangeMeters;
        _lastQueryTime = Time.time;
        
        AzuCraftyBoxesPlugin.lastPosition = pos;
        AzuCraftyBoxesPlugin.cachedContainerList = _scratchNearby;

        return _cachedAll;
    }

    private static readonly List<IContainer> _empty = new(0);
    private static List<IContainer> EmptyIContainers() => _empty;

    internal static class QueryFrame
    {
        public static int FrameId;
        public static List<IContainer> Nearby;

        public static List<IContainer> Get<T>(T src, float range) where T : Component
        {
            int f = Time.frameCount;
            if (FrameId != f)
            {
                FrameId = f;
                Nearby = Boxes.GetNearbyContainers(src, range);
            }

            return Nearby;
        }
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
        if (AzuCraftyBoxesPlugin.leaveOne.Value.isOn())
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