using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using AzuCraftyBoxes.IContainers;
using HarmonyLib;
using UnityEngine;

namespace AzuCraftyBoxes.Util.Functions;

public class MiscFunctions
{
    public static MethodInfo? GetCurrentCraftAmountMethod; 
    internal static bool AllowByKey()
    {
        /*if (AzuCraftyBoxesPlugin.preventModKey.Value.IsPressed())
            return AzuCraftyBoxesPlugin.switchPrevent.Value;
        return !AzuCraftyBoxesPlugin.switchPrevent.Value;*/
        return true;
    }

    /* Consume Resources */
    internal static void ProcessRequirements(Piece.Requirement[] requirements, int qualityLevel, Inventory pInventory,
        List<IContainer> nearbyContainers, int itemQuality)
    {
        foreach (Piece.Requirement requirement in requirements)
        {
            if (!IsValidRequirement(requirement)) continue;
            int totalRequirement = requirement.GetAmount(qualityLevel);
            if (totalRequirement <= 0) continue;

            string reqPrefab = requirement.m_resItem.name;
            string reqName = requirement.m_resItem.m_itemData.m_shared.m_name;
            int totalAmount = pInventory.CountItems(reqName);
            LogResourceInfo(totalAmount, totalRequirement, reqName);
            pInventory.RemoveItem(reqName, Math.Min(totalAmount, totalRequirement), itemQuality);

            if (totalAmount < totalRequirement)
            {
                int newTotalAmount = ConsumeResourcesFromContainers(reqPrefab, reqName, totalAmount, totalRequirement, nearbyContainers);
                if (newTotalAmount >= totalRequirement)
                {
                    AzuCraftyBoxesPlugin.AzuCraftyBoxesLogger.LogDebug($"(ConsumeResourcesPatch) Consumed enough {reqName}");
                }
            }
        }
    }



    private static bool IsValidRequirement(Piece.Requirement requirement)
    {
        return requirement.m_resItem &&
               requirement.m_resItem.m_itemData is { m_shared: not null };
    }

    private static void LogResourceInfo(int totalAmount, int totalRequirement, string reqName)
    {
        AzuCraftyBoxesPlugin.AzuCraftyBoxesLogger.LogDebug($"(ConsumeResourcesPatch) Have {totalAmount}/{totalRequirement} {reqName} in player inventory");
    }

    private static int ConsumeResourcesFromContainers(string reqPrefab, string reqName, int totalAmount, int totalRequirement,
        List<IContainer> nearbyContainers)
    {
        int newTotalAmount = totalAmount;
        foreach (IContainer c in nearbyContainers)
        {
            newTotalAmount = c.ProcessContainerInventory(reqPrefab, reqName, newTotalAmount, totalRequirement);
            if (newTotalAmount >= totalRequirement)
            {
                break;
            }
        }
        return newTotalAmount;
    }
    

    public static string GetPrefabName(string name)
    {
        char[] anyOf = new char[2] { '(', ' ' };
        int length = name.IndexOfAny(anyOf);
        return length < 0 ? name : name.Substring(0, length);
    }

    internal static GameObject? GetItemPrefabFromGameObject(ItemDrop itemDropComponent, GameObject inputGameObject)
    {
        GameObject? itemPrefab = ObjectDB.instance.GetItemPrefab(GetPrefabName(inputGameObject.name));
        itemDropComponent.m_itemData.m_dropPrefab = itemPrefab;
        return itemPrefab != null ? itemPrefab : null;
    }

    internal static bool CheckItemDropIntegrity(ItemDrop itemDropComp)
    {
        if(itemDropComp.m_itemData == null) return false;
        return itemDropComp.m_itemData.m_shared != null;
    }

    internal static void CreatePredefinedGroups(ObjectDB __instance)
    {
        foreach (GameObject gameObject in __instance.m_items.Where(x => x.GetComponentInChildren<ItemDrop>() != null))
        {
            ItemDrop? itemDrop = gameObject.GetComponentInChildren<ItemDrop>();
            if (!CheckItemDropIntegrity(itemDrop)) continue;
            GameObject? drop = GetItemPrefabFromGameObject(itemDrop, gameObject);
            itemDrop.m_itemData.m_dropPrefab = itemDrop.gameObject; // Fix all drop prefabs to be the actual item
            if (drop != null)
            {
                ItemDrop.ItemData.SharedData sharedData = itemDrop.m_itemData.m_shared;
                string groupName = "";

                if (sharedData.m_food > 0.0 && sharedData.m_foodStamina > 0.0)
                {
                    groupName = "Food";
                }

                if (sharedData.m_food > 0.0 && sharedData.m_foodStamina == 0.0)
                {
                    groupName = "Potion";
                }
                else if (sharedData.m_itemType == ItemDrop.ItemData.ItemType.Fish)
                {
                    groupName = "Fish";
                }

                switch (sharedData.m_itemType)
                {
                    case ItemDrop.ItemData.ItemType.OneHandedWeapon or ItemDrop.ItemData.ItemType.TwoHandedWeapon
                        or ItemDrop.ItemData.ItemType.TwoHandedWeaponLeft or ItemDrop.ItemData.ItemType.Bow:
                        switch (sharedData.m_skillType)
                        {
                            case Skills.SkillType.Swords:
                                groupName = "Swords";
                                break;
                            case Skills.SkillType.Bows:
                                groupName = "Bows";
                                break;
                            case Skills.SkillType.Crossbows:
                                groupName = "Crossbows";
                                break;
                            case Skills.SkillType.Axes:
                                groupName = "Axes";
                                break;
                            case Skills.SkillType.Clubs:
                                groupName = "Clubs";
                                break;
                            case Skills.SkillType.Knives:
                                groupName = "Knives";
                                break;
                            case Skills.SkillType.Pickaxes:
                                groupName = "Pickaxes";
                                break;
                            case Skills.SkillType.Polearms:
                                groupName = "Polearms";
                                break;
                            case Skills.SkillType.Spears:
                                groupName = "Spears";
                                break;
                        }

                        break;
                    case ItemDrop.ItemData.ItemType.Torch:
                        groupName = "Equipment";
                        break;
                    case ItemDrop.ItemData.ItemType.Trophy:
                        string[] bossTrophies =
                            { "eikthyr", "elder", "bonemass", "dragonqueen", "goblinking", "SeekerQueen" };
                        groupName = bossTrophies.Any(sharedData.m_name.EndsWith) ? "Boss Trophy" : "Trophy";
                        break;
                    case ItemDrop.ItemData.ItemType.Material:
                        if (ObjectDB.instance.GetItemPrefab("Cultivator").GetComponent<ItemDrop>().m_itemData.m_shared
                                .m_buildPieces.m_pieces.FirstOrDefault(p =>
                                {
                                    Piece.Requirement[] requirements = p.GetComponent<Piece>().m_resources;
                                    return requirements.Length == 1 &&
                                           requirements[0].m_resItem.m_itemData.m_shared.m_name == sharedData.m_name;
                                }) is { } piece)
                        {
                            groupName = piece.GetComponent<Plant>()?.m_grownPrefabs[0].GetComponent<Pickable>()
                                ?.m_amount > 1
                                ? "Crops"
                                : "Seeds";
                        }

                        if (ZNetScene.instance.GetPrefab("smelter").GetComponent<Smelter>().m_conversion
                            .Any(c => c.m_from.m_itemData.m_shared.m_name == sharedData.m_name))
                        {
                            groupName = "Ores";
                        }

                        if (ZNetScene.instance.GetPrefab("smelter").GetComponent<Smelter>().m_conversion
                            .Any(c => c.m_to.m_itemData.m_shared.m_name == sharedData.m_name))
                        {
                            groupName = "Metals";
                        }

                        if (ZNetScene.instance.GetPrefab("blastfurnace").GetComponent<Smelter>().m_conversion
                            .Any(c => c.m_from.m_itemData.m_shared.m_name == sharedData.m_name))
                        {
                            groupName = "Ores";
                        }

                        if (ZNetScene.instance.GetPrefab("blastfurnace").GetComponent<Smelter>().m_conversion
                            .Any(c => c.m_to.m_itemData.m_shared.m_name == sharedData.m_name))
                        {
                            groupName = "Metals";
                        }

                        if (ZNetScene.instance.GetPrefab("charcoal_kiln").GetComponent<Smelter>().m_conversion
                            .Any(c => c.m_from.m_itemData.m_shared.m_name == sharedData.m_name))
                        {
                            groupName = "Woods";
                        }
                        
                        if (sharedData.m_name == "$item_elderbark")
                        {
                            groupName = "Woods";
                        }

                        break;
                }

                if (!string.IsNullOrEmpty(groupName))
                {
                    AddItemToGroup(groupName, itemDrop);
                }

                if (sharedData != null)
                {
                    groupName = "All";
                    AddItemToGroup(groupName, itemDrop);
                }
            }
        }
    }

    private static void AddItemToGroup(string groupName, ItemDrop itemDrop)
    {
        // Check if the group exists, and if not, create it
        if (!GroupUtils.GroupExists(groupName))
        {
            AzuCraftyBoxesPlugin.groups[groupName] = new HashSet<string>();
        }

        // Add the item to the group
        string prefabName = Utils.GetPrefabName(itemDrop.m_itemData.m_dropPrefab);
        if (AzuCraftyBoxesPlugin.groups[groupName].Contains(prefabName)) return;
        AzuCraftyBoxesPlugin.groups[groupName].Add(prefabName);
        AzuCraftyBoxesPlugin.AzuCraftyBoxesLogger.LogDebug($"(CreatePredefinedGroups) Added {prefabName} to {groupName}");
    }
}