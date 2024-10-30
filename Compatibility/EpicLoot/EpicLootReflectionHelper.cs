/*using System.Collections;
using AzuCraftyBoxes.Util.Functions;

namespace AzuCraftyBoxes.Compatibility.EpicLoot;

public static class EpicLoot
{
    public const string elGuid = "randyknapp.mods.epicloot";
    public static PluginInfo? EpicLootPluginInfo { get; set; }
    public static Assembly? EpicLootAssembly { get; private set; }

    internal static Type? epicLootRootType;
    internal static Type? epicLootLibType;
    internal static Type? epicLootControllerType;
    internal static Type? epicLootItemDataExtensionsType;
    internal static Type? epicLootMagicItemExtentionType;
    internal static MethodInfo? isMagicMethod;
    internal static MethodInfo? getRarityMethod;
    internal static Type? enchantTabControllerType;
    internal static Type? enchantCostsHelperType;
    internal static MethodInfo? getEnchantCostsMethod;
    internal static MethodInfo? getCanBeMagicItemMethod;
    internal static MethodInfo? getCanBeAugmentedItemMethod;
    internal static MethodInfo? getCanBeDisenchantedItemMethod;
    internal static MethodInfo dataMethod;
    internal static MethodInfo? getGetSacrificeProductsMethod;


    public static void Init(PluginInfo? pluginInfo)
    {
        EpicLootPluginInfo = pluginInfo;
        EpicLootAssembly = pluginInfo?.Instance?.GetType().Assembly;

        if (EpicLootAssembly != null)
        {
            epicLootRootType = EpicLootAssembly.GetType("EpicLoot.EpicLoot");
            epicLootLibType = EpicLootAssembly.GetType("EpicLoot_UnityLib.InventoryItemListElement");
            epicLootControllerType = EpicLootAssembly.GetType("EpicLoot.CraftingV2.EnchantingUIController");
            epicLootItemDataExtensionsType = EpicLootAssembly.GetType("EpicLoot.ItemDataExtensions");
            epicLootMagicItemExtentionType = EpicLootAssembly.GetType("EpicLoot.MagicItem");
            enchantTabControllerType = EpicLootAssembly.GetType("EpicLoot.Crafting.EnchantHelper");
            enchantCostsHelperType = EpicLootAssembly.GetType("EpicLoot.Crafting.EnchantCostsHelper");
            isMagicMethod = epicLootItemDataExtensionsType?.GetMethod("IsMagic", BindingFlags.Public | BindingFlags.Static, null, new Type[] { typeof(ItemDrop.ItemData) }, null);
            getRarityMethod = epicLootItemDataExtensionsType?.GetMethod("GetRarity", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(ItemDrop.ItemData) }, null);
            getGetSacrificeProductsMethod = enchantCostsHelperType?.GetMethod("GetSacrificeProducts", BindingFlags.Public | BindingFlags.Static, null, new Type[] { typeof(ItemDrop.ItemData) }, null);
            getEnchantCostsMethod = enchantTabControllerType?.GetMethod("GetEnchantCosts", BindingFlags.Public | BindingFlags.Static);
            getCanBeMagicItemMethod = epicLootRootType?.GetMethod("CanBeMagicItem", BindingFlags.Public | BindingFlags.Static);
            getCanBeAugmentedItemMethod = epicLootItemDataExtensionsType?.GetMethod("CanBeAugmented", BindingFlags.Public | BindingFlags.Static);
            getCanBeDisenchantedItemMethod = epicLootMagicItemExtentionType?.GetMethod("CanBeDisenchanted", BindingFlags.Public | BindingFlags.Instance);
            //dataMethod = itemDataExtensionsType?.GetMethod("Data", BindingFlags.Public | BindingFlags.Static);
        }

        AzuCraftyBoxesPlugin.harmony.PatchAll(typeof(EpicLootEnchantingUI));
    }

    public static class EpicLootEnchantingUI
    {
        public static Type listType = AccessTools.TypeByName("EpicLoot_UnityLib.InventoryItemListElement");
        public static Type genericListType = typeof(List<>).MakeGenericType(listType);
        public static MethodInfo addMethod = genericListType.GetMethod("Add");
        public static MethodInfo clearMethod = genericListType.GetMethod("Clear");

        /*[HarmonyPatch("EpicLoot.CraftingV2.EnchantingUIController, EpicLoot", "GetEnchantableItems"), HarmonyPostfix]
        private static void GetEnchantableItemsPostfixPatch(ref object __result)
        {
            EpicLootReflectionHelpers.AppendItemsFromContainers(ref __result, EpicLootReflectionHelpers.CanBeMagicItem);
        }

        /*[HarmonyPatch("EpicLoot.CraftingV2.EnchantingUIController, EpicLoot", "GetSacrificeItems"), HarmonyPostfix]
        private static void GetSacrificeItemsPostfixPatch(ref object __result)
        {
            EpicLootReflectionHelpers.AppendItemsFromContainers(ref __result, EpicLootReflectionHelpers.CanBeMagicItem);
        }#2#

        [HarmonyPatch("EpicLoot.CraftingV2.EnchantingUIController, EpicLoot", "GetAugmentableItems"), HarmonyPostfix]
        private static void GetAugmentableItemsPostfixPatch(ref object __result)
        {
            EpicLootReflectionHelpers.AppendItemsFromContainers(ref __result, EpicLootReflectionHelpers.CanBeAugmented, true);
        }

        /*[HarmonyPatch("EpicLoot.CraftingV2.EnchantingUIController, EpicLoot", "GetDisenchantItems"), HarmonyPostfix]
        private static void GetDisenchantItemsPostfixPatch(ref object __result)
        {
            EpicLootReflectionHelpers.AppendItemsFromContainers(ref __result, EpicLootReflectionHelpers.CanBeDisenchanted, true);
        }#2##1#

        [HarmonyPatch("EpicLoot_UnityLib.InventoryManagement, EpicLoot-UnityLib", "GetAllItems"), HarmonyPostfix]
        private static void GetEnchantableItemsPostfixPatch(ref List<ItemDrop.ItemData> __result)
        {
            EpicLootReflectionHelpers.AppendContainerItemsToInventory(ref __result);
        }

        /*[HarmonyPatch("EpicLoot_UnityLib.InventoryManagement, EpicLoot-UnityLib", "GetInventory"), HarmonyPostfix]
        private static void GetEnchantableItemsPostfixPatch(ref Inventory __result)
        {
            EpicLootReflectionHelpers.AppendContainerItemsToInventory(ref __result);
        }#1#
    }

    public class EpicLootReflectionHelpers
    {
        public static void AppendContainerItemsToInventory(ref List<ItemDrop.ItemData> playerInventory)
        {
            try
            {
                // Create a list to hold combined items from the player and containers
                List<ItemDrop.ItemData> combinedItems = new List<ItemDrop.ItemData>(playerInventory);

                // Iterate over all containers and collect items
                foreach (Container container in Boxes.Containers)
                {
                    Inventory containerInventory = container.GetInventory(); // Assumes Container has GetInventory

                    if (containerInventory != null)
                    {
                        // Add each item in container to the combined list
                        foreach (ItemDrop.ItemData item in containerInventory.GetAllItems())
                        {
                            combinedItems.Add(item);
                        }
                    }
                }

                // Temporarily replace player inventory items with the combined list
                // If Inventory class has a method like SetItems, use that; otherwise, reassign to m_inventory
                playerInventory = combinedItems;
            }
            catch (Exception ex)
            {
                AzuCraftyBoxesPlugin.AzuCraftyBoxesLogger.LogWarning($"Error combining container items: {ex}");
            }
        }

        public static void AppendContainerItemsToInventory(ref Inventory playerInventory)
        {
            try
            {
                // Create a list to hold combined items from the player and containers
                List<ItemDrop.ItemData> combinedItems = new List<ItemDrop.ItemData>(playerInventory.GetAllItems());

                // Iterate over all containers and collect items
                foreach (Container container in Boxes.Containers)
                {
                    Inventory containerInventory = container.GetInventory(); // Assumes Container has GetInventory

                    if (containerInventory != null)
                    {
                        // Add each item in container to the combined list
                        foreach (ItemDrop.ItemData item in containerInventory.GetAllItems())
                        {
                            combinedItems.Add(item);
                        }
                    }
                }

                // Temporarily replace player inventory items with the combined list
                // If Inventory class has a method like SetItems, use that; otherwise, reassign to m_inventory
                playerInventory.m_inventory = combinedItems;
            }
            catch (Exception ex)
            {
                AzuCraftyBoxesPlugin.AzuCraftyBoxesLogger.LogWarning($"Error combining container items: {ex}");
            }
        }
    }
}*/