using System;
using System.Collections.Generic;
using System.Linq;
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

    internal static List<Container> GetNearbyContainers<T>(T gameObject, float rangeToUse) where T : Component
    {
        List<Container> nearbyContainers = new();
        if (Player.m_localPlayer == null) return nearbyContainers;
        if (Vector3.Distance(gameObject.transform.position, AzuCraftyBoxesPlugin.lastPosition) < 0.5f)
            return AzuCraftyBoxesPlugin.cachedContainerList;
        foreach (Container container in Containers)
        {
            if (gameObject == null || container == null) continue;
            var distance = Vector3.Distance(container.transform.position, gameObject.transform.position);
            if (distance <= rangeToUse)
            {
                // log the distance and the range to use
                #if DEBUG
                AzuCraftyBoxesPlugin.AzuCraftyBoxesLogger.LogDebug($"Distance to container {container.name} is {distance}m, within the range of {rangeToUse}m set to store items for this chest");
                #endif
                if (!container.IsInUse())
                {
                    nearbyContainers.Add(container);
                }
            }
        }

        AzuCraftyBoxesPlugin.lastPosition = gameObject.transform.position;
        AzuCraftyBoxesPlugin.cachedContainerList = nearbyContainers;
        return nearbyContainers;
    }

    public static void AddContainerIfNotExists(string containerName)
    {
        if (!AzuCraftyBoxesPlugin.yamlData.ContainsKey(containerName))
        {
            AzuCraftyBoxesPlugin.yamlData[containerName] = new Dictionary<string, object>
            {
                { "exclude", new List<string>() },
                { "includeOverride", new List<string>() }
            };

            YamlUtils.WriteYaml(AzuCraftyBoxesPlugin.yamlPath);
        }
    }

    // Get a list of all excluded prefabs for all containers in the container data

    public static Dictionary<string, List<string>> GetExcludedPrefabsForAllContainers()
    {
        Dictionary<string, List<string>> excludedPrefabsForAllContainers = new Dictionary<string, List<string>>();

        foreach (var container in GetAllContainers())
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
            AzuCraftyBoxesPlugin.AzuCraftyBoxesLogger.LogError(
                "yamlData is null. Make sure to call DeserializeYamlFile() before using CanItemBePulled.");
            return false;
        }

        if (!AzuCraftyBoxesPlugin.yamlData.ContainsKey(container))
        {
            //AzuCraftyBoxesPlugin.AzuCraftyBoxesLogger.LogInfo($"Container '{container}' not found in yamlData.");
            return true; // Allow pulling by default if the container is not defined in yamlData
        }

        var containerData = AzuCraftyBoxesPlugin.yamlData[container] as Dictionary<object, object>;
        if (containerData == null)
        {
            AzuCraftyBoxesPlugin.AzuCraftyBoxesLogger.LogError(
                $"Unable to cast containerData for container '{container}' to Dictionary<object, object>.");
            return false;
        }

        var excludeList = containerData.TryGetValue("exclude", out object? value1)
            ? value1 as List<object>
            : new List<object>();
        var includeOverrideList = containerData.TryGetValue("includeOverride", out object? value)
            ? value as List<object>
            : new List<object>();

        if (excludeList == null)
        {
            AzuCraftyBoxesPlugin.AzuCraftyBoxesLogger.LogError(
                $"Unable to cast excludeList for container '{container}' to List<object>.");
            return false;
        }

        if (includeOverrideList == null)
        {
            AzuCraftyBoxesPlugin.AzuCraftyBoxesLogger.LogError(
                $"Unable to cast includeOverrideList for container '{container}' to List<object>.");
            return false;
        }

        if (includeOverrideList.Contains(prefab))
        {
            return true;
        }

        foreach (var excludedItem in excludeList)
        {
            if (prefab.Equals(excludedItem))
            {
                return false;
            }

            if (GroupUtils.IsGroupDefined((string)excludedItem))
            {
                var groupItems = GroupUtils.GetItemsInGroup((string)excludedItem);
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
            foreach (var excludeItem in exclusionList)
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
            var containerInfo = containerData as Dictionary<object, object>;
            if (containerInfo != null && containerInfo.TryGetValue("exclude", out object excludeData))
            {
                var excludeList = excludeData as List<object>;
                if (excludeList != null)
                {
                    List<string> excludedPrefabs = new List<string>();
                    foreach (var excludeItem in excludeList)
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

    internal static void PullResources(Player player, Piece.Requirement[] resources, int qualityLevel)
    {
        Inventory pInventory = Player.m_localPlayer.GetInventory();
        List<Container> nearbyContainers = GetNearbyContainers(Player.m_localPlayer, AzuCraftyBoxesPlugin.mRange.Value);

        foreach (Piece.Requirement requirement in resources)
        {
            if (requirement.m_resItem)
            {
                ProcessResourceRequirement(player, requirement, qualityLevel, pInventory, nearbyContainers);
            }
        }
    }

    private static void ProcessResourceRequirement(Player player, Piece.Requirement requirement, int qualityLevel,
        Inventory pInventory, List<Container> nearbyContainers)
    {
        int totalRequirement = requirement.GetAmount(qualityLevel);
        if (totalRequirement <= 0) return;

        string reqName = requirement.m_resItem.m_itemData.m_shared.m_name;
        int totalAmount = 0;

        for (int index = 0; index < nearbyContainers.Count; ++index)
        {
            Container c = nearbyContainers[index];
            totalAmount = ProcessContainer(reqName, totalRequirement, totalAmount, c, pInventory);

            if (totalAmount >= totalRequirement) break;
        }

        if (AzuCraftyBoxesPlugin.pulledMessage.Value?.Length > 0)
            player.Message(MessageHud.MessageType.Center, AzuCraftyBoxesPlugin.pulledMessage.Value);
    }

    private static int ProcessContainer(string reqName, int totalRequirement, int totalAmount,
        Container c, Inventory pInventory)
    {
        Inventory cInventory = c.GetInventory();

        for (int i = 0; i < cInventory.GetAllItems().Count; ++i)
        {
            ItemDrop.ItemData item = cInventory.GetItem(i);
            if (item.m_shared.m_name != reqName) continue;

            // Check if the item can be pulled from the container
            string containerPrefabName = Utils.GetPrefabName(c.gameObject);
            string itemPrefabName = Utils.GetPrefabName(item.m_dropPrefab);
            if (CanItemBePulled(containerPrefabName, itemPrefabName))
            {
                int stackAmount = Mathf.Min(item.m_stack, totalRequirement - totalAmount);
                if (!pInventory.HaveEmptySlot())
                    stackAmount = Math.Min(pInventory.FindFreeStackSpace(item.m_shared.m_name, item.m_worldLevel), stackAmount);

                AddItemToPlayerInventory(item, stackAmount, pInventory, cInventory);

                //AddItemToPlayerInventory(item, stackAmount, pInventory);

                totalAmount += stackAmount;
                if (totalAmount >= totalRequirement) break;
            }
        }

        c.Save();
        cInventory.Changed();

        return totalAmount;
    }
    
    private static void AddItemToPlayerInventory(ItemDrop.ItemData item, int stackAmount, Inventory pInventory, Inventory cInventory)
    {
        ItemDrop.ItemData sendItem = item.Clone();
        sendItem.m_stack = stackAmount;
        pInventory.AddItem(sendItem);

        if (stackAmount == item.m_stack)
        {
            cInventory.RemoveItem(item);
        }
        else
        {
            item.m_stack -= stackAmount;
        }
    }


    /*private static void AddItemToPlayerInventory(ItemDrop.ItemData item, int stackAmount, Inventory pInventory)
    {
        ItemDrop.ItemData sendItem = item.Clone();
        sendItem.m_stack = stackAmount;
        pInventory.AddItem(sendItem);

        if (stackAmount == item.m_stack)
        {
            pInventory.RemoveItem(item);
        }
        else
        {
            item.m_stack -= stackAmount;
        }
    }*/
}