using System;
using System.Collections.Generic;
using System.Linq;
using AzuCraftyBoxes.IContainers;
using Backpacks;
using HarmonyLib;
using ItemDataManager;
using UnityEngine;

namespace AzuCraftyBoxes.Util.Functions;

public class Boxes
{
    internal static readonly List<Container> Containers = new();
    private static readonly List<Container> ContainersToAdd = new();
    private static readonly List<Container> ContainersToRemove = new();

    internal static void AddContainer(Container container)
    {
        if (!Containers.Contains(container))
        {
            ContainersToAdd.Add(container);
            AzuCraftyBoxesPlugin.AzuCraftyBoxesLogger.LogDebug($"Added container {container.name} to list");
        }

        UpdateContainers();
    }

    internal static void RemoveContainer(Container container)
    {
        if (Containers.Contains(container))
        {
            ContainersToRemove.Add(container);
            AzuCraftyBoxesPlugin.AzuCraftyBoxesLogger.LogDebug($"Removed container {container.name} from list");
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
        if (AzuCraftyBoxesPlugin.BackpacksIsLoaded)
        {
            if (Backpacks.API.GetEquippedBackpack() != null)
            {
                BackpackContainer backpack = BackpackContainer.Create(Backpacks.API.GetEquippedBackpack().Data().Get<ItemContainer>());
                backpacksEnumerable = new List<IContainer> { backpack };
            }
        }


        if (Vector3.Distance(gameObject.transform.position, AzuCraftyBoxesPlugin.lastPosition) < 0.5f)
            return AzuCraftyBoxesPlugin.cachedContainerList.Concat(kgDrawers).Concat(backpacksEnumerable).ToList();

        foreach (Container container in Containers)
        {
            if (gameObject == null || container == null) continue;
            float distance = Vector3.Distance(container.transform.position, gameObject.transform.position);
            if (distance <= rangeToUse)
            {
                // log the distance and the range to use
#if DEBUG
                AzuCraftyBoxesPlugin.AzuCraftyBoxesLogger.LogDebug($"Distance to container {container.name} is {distance}m, within the range of {rangeToUse}m set to store items for this chest");
#endif
                if (!container.IsInUse())
                {
                    nearbyContainers.Add(VanillaContainer.Create(container));
                }
            }
        }

        AzuCraftyBoxesPlugin.lastPosition = gameObject.transform.position;
        AzuCraftyBoxesPlugin.cachedContainerList = nearbyContainers;
        return nearbyContainers.Concat(kgDrawers).Concat(backpacksEnumerable).ToList();
    }

    public static void AddContainerIfNotExists(string containerName)
    {
        if (!AzuCraftyBoxesPlugin.yamlData.ContainsKey(containerName))
        {
            AzuCraftyBoxesPlugin.yamlData[containerName] = new Dictionary<string, object>
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

    // Check if a prefab is excluded from a container

    public static bool CanItemBePulled(string container, string prefab)
    {
        if (AzuCraftyBoxesPlugin.yamlData == null)
        {
            AzuCraftyBoxesPlugin.AzuCraftyBoxesLogger.LogError("yamlData is null. Make sure to call DeserializeYamlFile() before using CanItemBePulled.");
            return false;
        }

        if (!AzuCraftyBoxesPlugin.yamlData.ContainsKey(container))
        {
            //AzuCraftyBoxesPlugin.AzuCraftyBoxesLogger.LogInfo($"Container '{container}' not found in yamlData.");
            return true; // Allow pulling by default if the container is not defined in yamlData
        }

        Dictionary<object, object>? containerData = AzuCraftyBoxesPlugin.yamlData[container] as Dictionary<object, object>;
        if (containerData == null)
        {
            AzuCraftyBoxesPlugin.AzuCraftyBoxesLogger.LogError($"Unable to cast containerData for container '{container}' to Dictionary<object, object>.");
            return false;
        }

        List<object>? excludeList = containerData.TryGetValue("exclude", out object? value1)
            ? value1 as List<object>
            : new List<object>();
        List<object>? includeOverrideList = containerData.TryGetValue("includeOverride", out object? value)
            ? value as List<object>
            : new List<object>();

        if (excludeList == null)
        {
            AzuCraftyBoxesPlugin.AzuCraftyBoxesLogger.LogError($"Unable to cast excludeList for container '{container}' to List<object>.");
            return false;
        }

        if (includeOverrideList == null)
        {
            AzuCraftyBoxesPlugin.AzuCraftyBoxesLogger.LogError($"Unable to cast includeOverrideList for container '{container}' to List<object>.");
            return false;
        }

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
    }


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
        if (AzuCraftyBoxesPlugin.yamlData.TryGetValue(container, out object containerData))
        {
            Dictionary<object, object>? containerInfo = containerData as Dictionary<object, object>;
            if (containerInfo != null && containerInfo.TryGetValue("exclude", out object excludeData))
            {
                List<object>? excludeList = excludeData as List<object>;
                if (excludeList != null)
                {
                    List<string> excludedPrefabs = new List<string>();
                    foreach (object? excludeItem in excludeList)
                    {
                        string excludeItemName = excludeItem.ToString();
                        if (AzuCraftyBoxesPlugin.groups.TryGetValue(excludeItemName, out HashSet<string> groupPrefabs))
                        {
                            excludedPrefabs.AddRange(groupPrefabs);
                        }
                        else
                        {
                            excludedPrefabs.Add(excludeItemName);
                        }
                    }

                    return excludedPrefabs;
                }
            }
        }

        return new List<string>();
    }
}