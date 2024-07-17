/*using System.Collections;
using AzuCraftyBoxes.Util.Functions;

namespace AzuCraftyBoxes.Compatibility.EpicLoot;

public class EpicLootReflectionHelpers
{
    public static void LogAvailableConstructors(Type type)
    {
        var constructors = type.GetConstructors();
        foreach (var ctor in constructors)
        {
            AzuCraftyBoxesPlugin.AzuCraftyBoxesLogger.LogWarning($"Constructor: {ctor}");
            var parameters = ctor.GetParameters();
            foreach (var param in parameters)
            {
                AzuCraftyBoxesPlugin.AzuCraftyBoxesLogger.LogWarning($"Parameter: {param.ParameterType} {param.Name}");
            }
        }
    }

    public static void AppendItemsFromContainers(ref object __result, Func<ItemDrop.ItemData, bool> itemFilter, bool magicCheck = false)
    {
        try
        {
            if (EpicLoot.EpicLootEnchantingUI.listType == null || EpicLoot.EpicLootEnchantingUI.addMethod == null || EpicLoot.EpicLootEnchantingUI.clearMethod == null)
            {
                AzuCraftyBoxesPlugin.AzuCraftyBoxesLogger.LogWarning("List type, add method, or clear method not found.");
                return;
            }

            // Cast __result to the appropriate collection type
            var augmentableItems = (IList)Activator.CreateInstance(EpicLoot.EpicLootEnchantingUI.genericListType);
            if (__result is IEnumerable existingItems)
            {
                foreach (var item in existingItems)
                {
                    augmentableItems.Add(item);
                }
            }

            if (augmentableItems.Count > 0)
            {
                AzuCraftyBoxesPlugin.AzuCraftyBoxesLogger.LogIfDebuggingEpicLoot($"Items count: {augmentableItems.Count}");
            }
            //EpicLoot.EpicLootEnchantingUI.clearMethod.Invoke(augmentableItems, null);

            foreach (Container container in Boxes.Containers)
            {
                AzuCraftyBoxesPlugin.AzuCraftyBoxesLogger.LogIfDebuggingEpicLoot("Checking container");
                foreach (ItemDrop.ItemData item in container.GetInventory().GetAllItems())
                {
                    AzuCraftyBoxesPlugin.AzuCraftyBoxesLogger.LogIfDebuggingEpicLoot($"Checking item {item.m_shared.m_name}");

                    if (EpicLootReflectionHelpers.CanBeProcessed(item, itemFilter, magicCheck))
                    {
                        AzuCraftyBoxesPlugin.AzuCraftyBoxesLogger.LogIfDebuggingEpicLoot("Item can be processed");
                        object element = EpicLootReflectionHelpers.CreateInventoryItemListElement(item, EpicLoot.EpicLootEnchantingUI.listType);
                        AzuCraftyBoxesPlugin.AzuCraftyBoxesLogger.LogIfDebuggingEpicLoot($"Element: {element}");
                        EpicLoot.EpicLootEnchantingUI.addMethod.Invoke(augmentableItems, new[] { element });
                    }
                }
            }

            __result = augmentableItems;
        }
        catch (Exception ex)
        {
            AzuCraftyBoxesPlugin.AzuCraftyBoxesLogger.LogWarning($"Error in Postfix: {ex}");
        }
    }

    public static bool CanBeProcessed(ItemDrop.ItemData item, Func<ItemDrop.ItemData, bool> filter, bool isMagic = false)
    {
        if (isMagic)
        {
            return (bool)filter?.Invoke(item) && (bool)EpicLoot.isMagicMethod?.Invoke(null, [item])!;
        }

        return (bool)filter?.Invoke(item) && !(bool)EpicLoot.isMagicMethod?.Invoke(null, [item])!;
    }


    public static MethodInfo? GetEpicLootMethodFromController(string methodName)
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

    public static object ConvertToInventoryItemListElementList(List<ItemDrop.ItemData> items)
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

    public static object CreateInventoryItemListElement(ItemDrop.ItemData item, Type inventoryItemListElementType)
    {
        try
        {
            AzuCraftyBoxesPlugin.AzuCraftyBoxesLogger.LogIfDebuggingEpicLoot("Creating InventoryItemListElement instance");
            object elementInstance = Activator.CreateInstance(inventoryItemListElementType);
            AzuCraftyBoxesPlugin.AzuCraftyBoxesLogger.LogIfDebuggingEpicLoot("InventoryItemListElement instance created");

            if (elementInstance == null)
            {
                AzuCraftyBoxesPlugin.AzuCraftyBoxesLogger.LogError("elementInstance is null");
                return null;
            }

            FieldInfo itemField = inventoryItemListElementType.GetField("Item");
            if (itemField == null)
            {
                AzuCraftyBoxesPlugin.AzuCraftyBoxesLogger.LogError("Item field not found on InventoryItemListElement");
                return null;
            }

            AzuCraftyBoxesPlugin.AzuCraftyBoxesLogger.LogIfDebuggingEpicLoot($"Setting Item field with value: {item}");
            itemField.SetValue(elementInstance, item);

            AzuCraftyBoxesPlugin.AzuCraftyBoxesLogger.LogIfDebuggingEpicLoot("Item field set successfully");
            return elementInstance;
        }
        catch (Exception ex)
        {
            AzuCraftyBoxesPlugin.AzuCraftyBoxesLogger.LogError($"Error creating InventoryItemListElement: {ex}");
            return null;
        }
    }


    public static bool CanBeMagicItem(ItemDrop.ItemData item)
    {
        AzuCraftyBoxesPlugin.AzuCraftyBoxesLogger.LogIfDebuggingEpicLoot($"Checking if item {item.m_shared.m_name} can be magic");
        return (bool)EpicLoot.getCanBeMagicItemMethod?.Invoke(null, parameters: [item]);
    }

    public static bool CanBeAugmented(ItemDrop.ItemData item)
    {
        AzuCraftyBoxesPlugin.AzuCraftyBoxesLogger.LogIfDebuggingEpicLoot($"Checking if item {item.m_shared.m_name} can be augmented");

        // Assuming the extension method CanBeAugmented is defined in EpicLoot.ItemDataExtensions
        if (EpicLoot.getCanBeAugmentedItemMethod == null)
        {
            AzuCraftyBoxesPlugin.AzuCraftyBoxesLogger.LogWarning("getCanBeAugmentedItemMethod is null");
            return false;
        }

        bool yep = (bool)EpicLoot.getCanBeAugmentedItemMethod.Invoke(null, new object[] { item });
        AzuCraftyBoxesPlugin.AzuCraftyBoxesLogger.LogIfDebuggingEpicLoot($"Item {item.m_shared.m_name} can be augmented: {yep}");
        return yep;
    }

    /*public static bool CanBeDisenchanted(ItemDrop.ItemData item)
    {
        AzuCraftyBoxesPlugin.AzuCraftyBoxesLogger.LogWarning($"Checking if item {item.m_shared.m_name} can be disenchanted");

        // Assuming the extension method CanBeAugmented is defined in EpicLoot.ItemDataExtensions
        if (EpicLoot.getCanBeDisenchantedItemMethod == null)
        {
            AzuCraftyBoxesPlugin.AzuCraftyBoxesLogger.LogWarning("getCanBeDisenchantedItemMethod is null");
            return false;
        }

        bool yep = (bool)EpicLoot.getCanBeDisenchantedItemMethod.Invoke(null, new object[] { item });
        AzuCraftyBoxesPlugin.AzuCraftyBoxesLogger.LogIfDebuggingEpicLoot($"Item {item.m_shared.m_name} can be disenchanted: {yep}");
        return yep;
    }#1#

    /*public static bool CanBeDisenchanted(ItemDrop.ItemData item)
    {
        if (EpicLoot.getCanBeDisenchantedItemMethod == null)
        {
            AzuCraftyBoxesPlugin.AzuCraftyBoxesLogger.LogWarning("getCanBeDisenchantedItemMethod is null");
            return false;
        }

        var itemData = EpicLoot.dataMethod.Invoke(null, new object[] { item });
        var magicItem = itemData?.GetType().GetProperty("MagicItem")?.GetValue(itemData);
        if (magicItem != null)
        {
            return (bool)EpicLoot.getCanBeDisenchantedItemMethod.Invoke(magicItem, null);
        }
        return false;
    }#1#


    public static object CreateInventoryItemListElement(ItemDrop.ItemData item)
    {
        Type? elementType = AccessTools.TypeByName("EpicLoot_UnityLib.InventoryItemListElement");
        object? elementInstance = Activator.CreateInstance(elementType);
        elementType.GetProperty("Item").SetValue(elementInstance, item);
        return elementInstance;
    }

    public static object GetEnchantableItemsIncludingContainers()
    {
        try
        {
            AzuCraftyBoxesPlugin.AzuCraftyBoxesLogger.LogDebug("Getting enchantable items including containers");

            // Get the InventoryItemListElement type from EpicLoot assembly
            Type inventoryItemListElementType = AccessTools.TypeByName("EpicLoot_UnityLib.InventoryItemListElement");
            if (inventoryItemListElementType == null)
            {
                AzuCraftyBoxesPlugin.AzuCraftyBoxesLogger.LogError("Failed to find type 'EpicLoot_UnityLib.InventoryItemListElement'");
                return null;
            }

            // Create a list of the found type
            Type listType = typeof(List<>).MakeGenericType(inventoryItemListElementType);
            object enchantableItems = Activator.CreateInstance(listType);

            // Get the 'GetEnchantableItems' method from the controller
            MethodInfo? method = GetEpicLootMethodFromController("GetEnchantableItems");
            if (method == null)
            {
                AzuCraftyBoxesPlugin.AzuCraftyBoxesLogger.LogError("Failed to find method 'GetEnchantableItems'");
                return enchantableItems;
            }

            // Invoke the method and add the results to the list
            AzuCraftyBoxesPlugin.AzuCraftyBoxesLogger.LogDebug("Invoking 'GetEnchantableItems' method");
            object? result = null!;
            try
            {
                result = method.Invoke(null, null);
            }
            catch (Exception ex)
            {
                AzuCraftyBoxesPlugin.AzuCraftyBoxesLogger.LogError($"Error invoking 'GetEnchantableItems' method: {ex}");
            }

            AzuCraftyBoxesPlugin.AzuCraftyBoxesLogger.LogDebug("Invoked 'GetEnchantableItems' method successfully");

            if (result != null)
            {
                MethodInfo? addMethod = listType.GetMethod("Add");
                if (addMethod != null)
                {
                    foreach (object item in (IEnumerable)result)
                    {
                        addMethod.Invoke(enchantableItems, new[] { item });
                    }
                }
            }
            
            foreach (Container container in Boxes.Containers)
            {
                AzuCraftyBoxesPlugin.AzuCraftyBoxesLogger.LogDebug("Checking container");
                foreach (ItemDrop.ItemData item in container.GetInventory().GetAllItems())
                {
                    if (!(bool)EpicLoot.isMagicMethod?.Invoke(null, [item]) && CanBeMagicItem(item))
                    {
                        object element = CreateInventoryItemListElement(item, inventoryItemListElementType);
                        MethodInfo? addMethod = listType.GetMethod("Add");
                        if (addMethod != null)
                        {
                            AzuCraftyBoxesPlugin.AzuCraftyBoxesLogger.LogDebug("Adding item to enchantable items");
                            addMethod.Invoke(enchantableItems, new[] { element });
                        }
                    }
                }
            }

            AzuCraftyBoxesPlugin.AzuCraftyBoxesLogger.LogDebug("Returning enchantable items");
            return enchantableItems;
        }
        catch (Exception ex)
        {
            AzuCraftyBoxesPlugin.AzuCraftyBoxesLogger.LogError($"Error in GetEnchantableItemsIncludingContainers: {ex}");
            return null;
        }
    }
}*/