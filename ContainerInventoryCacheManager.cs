using System;
using System.Collections.Generic;
using AzuCraftyBoxes;
using UnityEngine;

/// <summary>
/// Caches container inventories by storing their base64 representation and aggregated item counts.
/// When the container’s base64 data changes (i.e. its inventory changed), the cache is refreshed.
/// </summary>
public class ContainerInventoryCacheManager : MonoBehaviour
{
    // Singleton instance for easy access.
    public static ContainerInventoryCacheManager Instance { get; private set; }

    // Dictionary mapping a container to its cached data.
    private readonly Dictionary<Container, CachedInventoryData> _cache = new Dictionary<Container, CachedInventoryData>();

    // Global event that fires whenever any registered container’s inventory changes.
    public event Action GlobalInventoryChanged;

    private void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    /// <summary>
    /// Registers a container with the cache manager.
    /// This subscribes to its change events and initializes the cache.
    /// </summary>
    public void RegisterContainer(Container container)
    {
        if (container == null)
            return;

        // Subscribe to the container's inventory changed event.
        Inventory inventory = container.GetInventory();
        if (inventory != null)
        {
            // Use a lambda wrapper; be sure to unregister later.
            inventory.m_onChanged += () => OnContainerInventoryChanged(container);
        }

        // Immediately initialize cache for this container.
        UpdateContainerCache(container);
    }

    /// <summary>
    /// Unregisters a container from the cache.
    /// </summary>
    public void UnregisterContainer(Container container)
    {
        if (container == null)
            return;

        // Optionally: unsubscribe from events (if you stored the delegate, you could remove it here).
        _cache.Remove(container);
    }

    /// <summary>
    /// Called when a container's inventory has signaled a change.
    /// Updates its cache and notifies listeners.
    /// </summary>
    private void OnContainerInventoryChanged(Container container)
    {
        UpdateContainerCache(container);
        GlobalInventoryChanged?.Invoke();
    }

    /// <summary>
    /// Reads the container's current base64 inventory string and, if it has changed,
    /// re-aggregates the item counts.
    /// </summary>
    private void UpdateContainerCache(Container container)
    {
        if (container == null || container.m_nview == null)
            return;

        // Read the serialized inventory string from the container's ZDO.
        string currentBase64 = container.m_nview.GetZDO().GetString(ZDOVars.s_items);
        if (!_cache.TryGetValue(container, out var cached))
        {
            cached = new CachedInventoryData { LastBase64 = currentBase64 };
            cached.AggregatedCounts = AggregateInventory(container.GetInventory());
            _cache[container] = cached;
        }
        else if (cached.LastBase64 != currentBase64)
        {
            cached.LastBase64 = currentBase64;
            cached.AggregatedCounts = AggregateInventory(container.GetInventory());
        }
    }

    /// <summary>
    /// Aggregates item counts from the given inventory.
    /// </summary>
    private Dictionary<string, int> AggregateInventory(Inventory inventory)
    {
        var counts = new Dictionary<string, int>();
        if (inventory == null)
            return counts;

        foreach (var item in inventory.GetAllItems())
        {
            string key = ItemKeyHelper.GetCanonicalKey(item);
            if (!counts.ContainsKey(key))
                counts[key] = 0;
            counts[key] += item.m_stack;
        }

        return counts;
    }

    /// <summary>
    /// Returns the aggregated count for a specific item in a given container.
    /// </summary>
    public int GetAggregatedItemCount(Container container, string itemName)
    {
        AzuCraftyBoxesPlugin.AzuCraftyBoxesLogger.LogWarning($"(GetAggregatedItemCount) Container: {container}, Item: {itemName}");
        if (_cache.TryGetValue(container, out CachedInventoryData? cached) && cached.AggregatedCounts.TryGetValue(itemName, out int count))
        {
            return count;
        }

        return 0;
    }

    /// <summary>
    /// Returns the total count for a specific item across all registered containers.
    /// </summary>
    public int GetTotalItemCount(string itemName)
    {
        int total = 0;
        foreach (var cached in _cache.Values)
        {
            if (cached.AggregatedCounts.TryGetValue(itemName, out int count))
                total += count;
        }

        return total;
    }

    /// <summary>
    /// Internal data structure for caching a container's inventory state.
    /// </summary>
    private class CachedInventoryData
    {
        public string LastBase64;
        public Dictionary<string, int> AggregatedCounts;
    }
}