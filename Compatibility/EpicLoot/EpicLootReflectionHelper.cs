/*using System.Collections;
using AzuCraftyBoxes.Util.Functions;
using HarmonyLib;

namespace AzuCraftyBoxes.Compatibility.EpicLoot;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

public static class EpicLootReflectionHelper
{
    public const string elGuid = "randyknapp.mods.epicloot";

    // Caching Reflection Calls
    internal static readonly Type? epicLootRootType = AzuCraftyBoxesPlugin.epicLootAssembly?.GetType("EpicLoot.EpicLoot");
    internal static readonly Type? epicLootLibType = AzuCraftyBoxesPlugin.epicLootAssembly?.GetType("EpicLoot_UnityLib.InventoryItemListElement");
    internal static readonly Type? epicLootControllerType = AzuCraftyBoxesPlugin.epicLootAssembly?.GetType("EpicLoot.CraftingV2.EnchantingUIController");
    internal static readonly Type? epicLootItemDataExtentionsType = AzuCraftyBoxesPlugin.epicLootAssembly?.GetType("EpicLoot.ItemDataExtensions");
    internal static readonly MethodInfo? isMagicMethod = epicLootItemDataExtentionsType?.GetMethod("IsMagic", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(ItemDrop.ItemData) }, null);
    internal static readonly MethodInfo? getRarityMethod = epicLootItemDataExtentionsType?.GetMethod("GetRarity", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(ItemDrop.ItemData) }, null);
    internal static readonly Type? enchantTabControllerType = AzuCraftyBoxesPlugin.epicLootAssembly?.GetType("EpicLoot.Crafting.EnchantHelper");

    internal static readonly MethodInfo? getEnchantCostsMethod = enchantTabControllerType?.GetMethod("GetEnchantCosts", BindingFlags.Public | BindingFlags.Static);

    // Additional method to get enchantable items
    internal static readonly MethodInfo? getCanBeMagicItemMethod = epicLootRootType?.GetMethod("CanBeMagicItem", BindingFlags.Public | BindingFlags.Static);

    private static MethodInfo? GetEpicLootMethodFromController(string methodName)
    {
        // Assuming EpicLoot's EnchantingUIController class is in the EpicLoot.Crafting namespace
        Type? epicLootType = Type.GetType("EpicLoot.CraftingV2.EnchantingUIController, EpicLoot");
        if (epicLootType == null)
        {
            AzuCraftyBoxesPlugin.AzuCraftyBoxesLogger.LogWarning("EpicLoot's EnchantingUIController class not found");
        }

        MethodInfo? method = epicLootType?.GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Static);

        if (method == null)
        {
            AzuCraftyBoxesPlugin.AzuCraftyBoxesLogger.LogWarning($"Method {methodName} not found in EpicLoot's EnchantingUIController class");
        }

        return method;
    }

    /*public static List<ItemDrop.ItemData> GetSacrificeItemsIncludingContainers()
    {
        // Get the sacrifice items from the player's inventory using EpicLoot's method
        List<ItemDrop.ItemData>? sacrificeItems = GetEpicLootMethodFromController("GetSacrificeItems")?.Invoke(null, null) as List<ItemDrop.ItemData>;

        if (sacrificeItems == null)
        {
            sacrificeItems = new List<ItemDrop.ItemData>();
        }

        // Append items from nearby containers that can be sacrificed
        foreach (var container in Boxes.Containers)
        {
            var containerItems = container.GetInventory().GetAllItems()
                .Where(item => item != null && CanBeSacrificed(item));
            sacrificeItems.AddRange(containerItems);
        }

        return sacrificeItems;
    }

    public static List<ItemDrop.ItemData> GetAugmentableItemsIncludingContainers()
    {
        // Get the augmentable items from the player's inventory using EpicLoot's method
        var method = GetEpicLootMethodFromController("GetAugmentableItems");
        if (method == null)
        {
            return [];
        }

        List<ItemDrop.ItemData>? augmentableItems = method?.Invoke(null, null) as List<ItemDrop.ItemData>;

        if (augmentableItems == null)
        {
            augmentableItems = new List<ItemDrop.ItemData>();
        }

        // Append items from nearby containers that can be augmented
        foreach (var container in Boxes.Containers)
        {
            var containerItems = container.GetInventory().GetAllItems()
                .Where(item => item != null && CanBeAugmented(item));
            augmentableItems.AddRange(containerItems);
        }

        return augmentableItems;
    }#1#

    /*public static List<ItemDrop.ItemData> GetDisenchantItemsIncludingContainers()
    {
        var method = GetEpicLootMethodFromController("GetDisenchantItems");
        if (method == null)
        {
            return [];
        }

        // Get the disenchantable items from the player's inventory using EpicLoot's method
        List<ItemDrop.ItemData>? disenchantItems = method?.Invoke(null, null) as List<ItemDrop.ItemData>;

        if (disenchantItems == null)
        {
            disenchantItems = new List<ItemDrop.ItemData>();
        }

        // Append items from nearby containers that can be disenchanted
        foreach (var container in Boxes.Containers)
        {
            var containerItems = container.GetInventory().GetAllItems()
                .Where(item => item != null && CanBeDisenchanted(item));
            disenchantItems.AddRange(containerItems);
        }

        return disenchantItems;
    }#1#

    public static object GetEnchantableItemsIncludingContainers()
    {
        AzuCraftyBoxesPlugin.AzuCraftyBoxesLogger.LogDebug("Getting enchantable items including containers");
        Type inventoryItemListElementType = AccessTools.TypeByName("EpicLoot_UnityLib.InventoryItemListElement");
        Type listType = typeof(List<>).MakeGenericType(inventoryItemListElementType);
        object? enchantableItems = Activator.CreateInstance(listType);

        MethodInfo? method = GetEpicLootMethodFromController("GetEnchantableItems");
        if (method != null)
        {
            AzuCraftyBoxesPlugin.AzuCraftyBoxesLogger.LogWarning("Method found");
            object? result = method.Invoke(null, null);
            if (result != null)
            {
                MethodInfo? addMethod = listType.GetMethod("Add");
                foreach (object? item in (IEnumerable)result)
                {
                    if (addMethod != null) addMethod.Invoke(enchantableItems, new[] { item });
                }
            }
        }

        foreach (Container container in Boxes.Containers)
        {
            AzuCraftyBoxesPlugin.AzuCraftyBoxesLogger.LogWarning("Checking container");
            MethodInfo? addMethod = listType.GetMethod("Add");
            foreach (ItemDrop.ItemData item in container.GetInventory().GetAllItems())
            {
                if (!(bool)isMagicMethod?.Invoke(null, new object[] { item }) && CanBeMagicItem(item))
                {
                    object element = CreateInventoryItemListElement(item, inventoryItemListElementType);
                    if (addMethod != null) addMethod.Invoke(enchantableItems, new[] { element });
                }
            }
        }

        AzuCraftyBoxesPlugin.AzuCraftyBoxesLogger.LogWarning("Returning enchantable items");
        return enchantableItems;
    }

    private static object CreateInventoryItemListElement(ItemDrop.ItemData item, Type elementType)
    {
        var elementInstance = Activator.CreateInstance(elementType);
        elementType.GetProperty("Item").SetValue(elementInstance, item);
        return elementInstance;
    }


    private static bool CanBeMagicItem(ItemDrop.ItemData item)
    {
        AzuCraftyBoxesPlugin.AzuCraftyBoxesLogger.LogWarning("Checking if item can be magic");
        return (bool)getCanBeMagicItemMethod?.Invoke(null, new object[] { item });
    }
}

[HarmonyPatch]
public class EpicLootContainerPatch
{
    static MethodBase TargetMethod()
    {
        Type? type = AccessTools.TypeByName("EpicLoot.CraftingV2.EnchantingUIController");
        return AccessTools.Method(type, "GetEnchantableItems");
    }

    /*static bool Prefix(ref object __result)
    {
        try
        {
            List<object> customItemList = EpicLootReflectionHelper.GetEnchantableItemsIncludingContainers();
            AzuCraftyBoxesPlugin.AzuCraftyBoxesLogger.LogWarning($"Custom item list count: {customItemList.Count}");
            Type listType = AccessTools.TypeByName("EpicLoot.EpicLoot_UnityLib.InventoryItemListElement");
            if (listType == null)
            {
                AzuCraftyBoxesPlugin.AzuCraftyBoxesLogger.LogWarning("List type not found.");
                return true; // Fall back to original method
            }

            Type genericListType = typeof(List<>).MakeGenericType(listType);
            var specificListInstance = Activator.CreateInstance(genericListType);
            AzuCraftyBoxesPlugin.AzuCraftyBoxesLogger.LogWarning($"Specific list instance: {specificListInstance}");
            MethodInfo addMethod = genericListType.GetMethod("Add");
            if (addMethod == null)
            {
                AzuCraftyBoxesPlugin.AzuCraftyBoxesLogger.LogWarning("Add method not found.");
                return true; // Fall back to the original method
            }
            AzuCraftyBoxesPlugin.AzuCraftyBoxesLogger.LogWarning($"Add method: {addMethod}");
            foreach (var item in customItemList)
            {
                addMethod.Invoke(specificListInstance, new[] { item });
            }
            AzuCraftyBoxesPlugin.AzuCraftyBoxesLogger.LogWarning($"Specific list instance count: {genericListType.GetProperty("Count")?.GetValue(specificListInstance)}");
            __result = specificListInstance;
            return false; // Skip original execution because we've provided the modified result
        }
        catch (Exception ex)
        {
            AzuCraftyBoxesPlugin.AzuCraftyBoxesLogger.LogWarning($"Error in Prefix: {ex}");
            return true; // If there's an error, fall back to the original method
        }
    }#1#

    static void Postfix(ref object __result)
    {
        try
        {
            var customItemList = EpicLootReflectionHelper.GetEnchantableItemsIncludingContainers();
            if (customItemList == null)
            {
                AzuCraftyBoxesPlugin.AzuCraftyBoxesLogger.LogWarning("Custom item list is null.");
                return;
            }

            // Assuming customItemList is already of the correct type, no need for further conversion
            __result = customItemList;
            AzuCraftyBoxesPlugin.AzuCraftyBoxesLogger.LogWarning($"Custom item list count: {((List<object>)customItemList).Count}");
        }
        catch (Exception ex)
        {
            AzuCraftyBoxesPlugin.AzuCraftyBoxesLogger.LogWarning($"Error in Postfix: {ex}");
        }
    }


    private static object ConvertToInventoryItemListElementList(List<ItemDrop.ItemData> items)
    {
        // You need to dynamically create a List<InventoryItemListElement> and populate it
        Type? listType = AccessTools.TypeByName("EpicLoot_UnityLib.InventoryItemListElement");
        Type genericListType = typeof(List<>).MakeGenericType(listType);
        object? result = Activator.CreateInstance(genericListType);

        foreach (ItemDrop.ItemData? item in items)
        {
            object element = CreateInventoryItemListElement(item);
            genericListType.GetMethod("Add").Invoke(result, new[] { element });
        }

        return result;
    }

    private static object CreateInventoryItemListElement(ItemDrop.ItemData item)
    {
        Type? elementType = AccessTools.TypeByName("EpicLoot_UnityLib.InventoryItemListElement");
        object? elementInstance = Activator.CreateInstance(elementType);
        elementType.GetProperty("Item").SetValue(elementInstance, item);
        return elementInstance;
    }
}*/