using System.Collections.Concurrent;
using System.Diagnostics;
using AzuCraftyBoxes.APIs;
using AzuCraftyBoxes.IContainers;
using Backpacks;
using ItemDataManager;
using UnityEngine.Pool;
using static AzuCraftyBoxes.Patches.CacheCurrentCraftingStationPrefabName;

namespace AzuCraftyBoxes.Util.Functions;

public sealed class ContainerCache
{
    public Container Container;

    public Vector3 LastPos;

    public readonly Dictionary<string, int> ItemCounts = new(16);
}

public static class Boxes
{
    internal static readonly HashSet<Container> Containers = new();
    private static readonly Dictionary<Container, ContainerCache> CacheByContainer = new();

    internal static ContainerCache? GetCache(Container c) => c && CacheByContainer.TryGetValue(c, out ContainerCache? cache) ? cache : null;

    private static readonly HashSet<Container> ContainersToAdd = new();
    private static readonly HashSet<Container> ContainersToRemove = new();
    private static int _lastRegistryFrame; // so we only flush once per frame

    private static readonly ConcurrentDictionary<float, Stopwatch> stopwatches = new();

    internal static void AddContainer(Container container)
    {
        if (!container) return;

        if (ContainersToAdd.Add(container))
        {
            if (!CacheByContainer.TryGetValue(container, out ContainerCache? cache))
            {
                cache = new ContainerCache
                {
                    Container = container,
                    LastPos = container.transform.position
                };
                CacheByContainer[container] = cache;
            }

            RebuildCache(container);
            AzuCraftyBoxesPlugin.AzuCraftyBoxesLogger.LogIfReleaseAndDebugEnable($"Added container {container.name} to registry");
        }
    }

    internal static void RemoveContainer(Container container)
    {
        if (!container) return;

        if (!Containers.Contains(container) && !ContainersToAdd.Contains(container)) return;
        ContainersToRemove.Add(container);
        CacheByContainer.Remove(container);
        AzuCraftyBoxesPlugin.AzuCraftyBoxesLogger.LogIfReleaseAndDebugEnable($"Removed container {container.name} from registry");
    }

    private static void FlushRegistryIfNeeded()
    {
        int f = Time.frameCount;
        if (_lastRegistryFrame == f) return;
        _lastRegistryFrame = f;

        if (ContainersToAdd.Count > 0)
        {
            foreach (Container? c in ContainersToAdd)
            {
                if (c) Containers.Add(c);
            }

            ContainersToAdd.Clear();
        }

        if (ContainersToRemove.Count <= 0) return;

        foreach (Container? c in ContainersToRemove)
        {
            Containers.Remove(c);
        }

        ContainersToRemove.Clear();
    }

    internal static void UpdateContainers()
    {
        FlushRegistryIfNeeded();
    }

    /// <summary>
    /// Rebuild the per-container item count index from the live Inventory.
    /// Called on container add, OnContainerChanged, and Load.
    /// </summary>
    internal static void RebuildCache(Container c)
    {
        if (!c) return;

        if (!CacheByContainer.TryGetValue(c, out ContainerCache? cache))
        {
            cache = new ContainerCache
            {
                Container = c,
                LastPos = c.transform.position
            };
            CacheByContainer[c] = cache;
        }
        else
        {
            cache.LastPos = c.transform.position;
        }

        Inventory? inv = c.GetInventory();
        cache.ItemCounts.Clear();
        if (inv == null) return;

        List<ItemDrop.ItemData>? items = inv.GetAllItems();
        for (int i = 0; i < items.Count; ++i)
        {
            ItemDrop.ItemData? it = items[i];
            if (it?.m_shared == null) continue;

            string key = it.m_shared.m_name;
            int cur = cache.ItemCounts.GetValueOrDefault(key, 0);
            cache.ItemCounts[key] = cur + it.m_stack;
        }
    }

    private static readonly List<IContainer> _scratchNearby = new(256);
    private static readonly List<IContainer> _scratchkgDrawers = new(128);
    private static readonly List<IContainer> _scratchmkzDrawers = new(128);
    private static readonly List<IContainer> _scratchBackpacks = new(32);
    private static readonly List<IContainer> _scratchGemBags = new(32);

    private static Vector3 _lastQueryPos = Vector3.positiveInfinity;
    private static float _lastQueryRange;
    private static float _lastQueryTime;
    private static readonly float _cacheWindow = 0.25f;

    // The fully built cached list:
    private static readonly List<IContainer> _cachedAll = new(256);

    internal static List<IContainer> GetNearbyContainers<T>(T src, float rangeMeters) where T : Component
    {
        if (!Player.m_localPlayer || !src) return EmptyIContainers();

        FlushRegistryIfNeeded();

        Vector3 pos = src.transform.position;

        // Reuse cache if player hasn't moved much & within time window
        if (Time.time - _lastQueryTime <= _cacheWindow && (pos - _lastQueryPos).sqrMagnitude < 0.25f * 0.25f && Mathf.Approximately(rangeMeters, _lastQueryRange))
        {
            return _cachedAll; // already a combined (nearby + drawers + bags) list
        }

        _scratchNearby.Clear();
        _scratchkgDrawers.Clear();
        _scratchmkzDrawers.Clear();
        _scratchBackpacks.Clear();
        _scratchGemBags.Clear();
        _cachedAll.Clear();

        // 1) Nearby vanilla containers (no LINQ, squared distance, in-use check)
        float r2 = rangeMeters * rangeMeters;
        foreach (Container? c in Containers)
        {
            if (!c) continue;
            if (c.m_wagon && c.m_wagon.InUse()) continue;

            ContainerCache? cache = GetCache(c);
            if (cache == null) continue;

            Vector3 d = cache.LastPos - pos;
            if (d.sqrMagnitude > r2) continue;

            if (!c.IsInUse() || (c.IsInUse() && c.IsOwner()))
            {
                _scratchNearby.Add(VanillaContainer.Create(c, cache));
            }
        }

        // 2) Drawer containers
        List<ItemDrawers_API.Drawer> drawers = APIs.ItemDrawers_API.AllDrawersInRange(pos, rangeMeters);
        foreach (ItemDrawers_API.Drawer? d in drawers)
            _scratchkgDrawers.Add(kgDrawer.Create(d));
        List<MkzItemDrawers_API.mkzDrawer> mkzdrawers = APIs.MkzItemDrawers_API.AllDrawersInRange(pos, rangeMeters);
        foreach (MkzItemDrawers_API.mkzDrawer? d in mkzdrawers)
            _scratchmkzDrawers.Add(mkzDrawer.Create(d));

        // 3) Backpack containers
        if (AzuCraftyBoxesPlugin.BackpacksIsLoaded)
        {
            List<ItemDrop.ItemData>? items = Player.m_localPlayer.GetInventory().GetAllItems();
            HashSet<ItemContainer>? seen = HashSetPool<ItemContainer>.Get();
            for (int i = 0; i < items.Count; ++i)
            {
                ItemDrop.ItemData? it = items[i];
                if (it == null) continue;

                ForeignItemInfo? data = it.Data("org.bepinex.plugins.backpacks");
                if (data == null) continue;

                ItemContainer? cont = data.Get<ItemContainer>();
                if (cont == null) continue;

                if (seen.Add(cont))
                    _scratchBackpacks.Add(BackpackContainer.Create(cont));
            }

            HashSetPool<ItemContainer>.Release(seen);
        }

        // 4) Gem bag containers
        if (Jewelcrafting.API.IsLoaded())
        {
            List<ItemDrop.ItemData>? items = Player.m_localPlayer.GetInventory().GetAllItems();
            for (int i = 0; i < items.Count; ++i)
            {
                ItemDrop.ItemData? it = items[i];
                if (it == null) continue;

                if (Jewelcrafting.API.IsFreelyAccessibleInventory(it))
                {
                    if (Jewelcrafting.API.GetItemContainerInventory(it) != null)
                        _scratchGemBags.Add(new GemBagContainer(it));
                }
            }
        }


        _cachedAll.AddRange(_scratchNearby);
        _cachedAll.AddRange(_scratchkgDrawers);
        _cachedAll.AddRange(_scratchmkzDrawers);
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

    public static Dictionary<string, List<string>> GetExcludedPrefabsForAllContainers()
    {
        Dictionary<string, List<string>> excludedPrefabsForAllContainers = new Dictionary<string, List<string>>();
        foreach (string? container in GetAllContainers())
        {
            excludedPrefabsForAllContainers[container] = GetExcludedPrefabs(container);
        }

        return excludedPrefabsForAllContainers;
    }

    public static List<string> GetAllContainers()
    {
        return AzuCraftyBoxesPlugin.yamlData.Keys.Where(key => key != "groups").ToList();
    }

    private static bool PassesIncludeExcludeChecks(Dictionary<string, List<string>> data, string prefab)
    {
        List<string> includeOverrideList = data.TryGetValue("includeOverride", out List<string> includeValues) ? includeValues : new List<string>();

        if (includeOverrideList.Contains(prefab))
            return true;

        List<string> excludeList = data.TryGetValue("exclude", out List<string> excludeValues) ? excludeValues : new List<string>();

        foreach (string excludedItem in excludeList)
        {
            if (prefab.Equals(excludedItem))
                return false;

            if (GroupUtils.IsGroupDefined(excludedItem))
            {
                List<string> groupItems = GroupUtils.GetItemsInGroup(excludedItem);
                if (groupItems.Contains(prefab))
                    return false;
            }
        }

        return true;
    }

    public static bool CanItemBePulled(string container, string prefab, string stationName = "")
    {
        if (AzuCraftyBoxesPlugin.yamlData == null)
        {
            AzuCraftyBoxesPlugin.AzuCraftyBoxesLogger.LogError("yamlData is null. Call DeserializeYamlFile() first.");
            return false;
        }

        if (!string.IsNullOrWhiteSpace(CachedStationName))
        {
            stationName = CachedStationName;
        }

        if (!string.IsNullOrWhiteSpace(stationName) && AzuCraftyBoxesPlugin.yamlData.TryGetValue(stationName, out Dictionary<string, List<string>>? stationData))
        {
            bool stationPass = PassesIncludeExcludeChecks(stationData, prefab);
            if (!stationPass)
            {
                return false;
            }
        }

        if (!AzuCraftyBoxesPlugin.yamlData.TryGetValue(container, out Dictionary<string, List<string>>? containerData))
        {
            return true;
        }

        return PassesIncludeExcludeChecks(containerData, prefab);
    }

    internal static bool IsPrefabExcluded(string prefab, List<object> exclusionList)
    {
        if (exclusionList == null) return false;
        foreach (object excludeItem in exclusionList)
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

        return false;
    }

    public static List<string> GetExcludedPrefabs(string container)
    {
        if (!AzuCraftyBoxesPlugin.yamlData.TryGetValue(container, out Dictionary<string, List<string>>? containerData) || !containerData.TryGetValue("exclude", out List<string>? excludeList)) return new List<string>();
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

    public static Stopwatch GetStopwatch(GameObject o)
    {
        float hash = GetGameObjectPosHash(o);
        if (stopwatches.TryGetValue(hash, out Stopwatch? stopwatch)) return stopwatch;
        stopwatch = new Stopwatch();
        stopwatches.TryAdd(hash, stopwatch);

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