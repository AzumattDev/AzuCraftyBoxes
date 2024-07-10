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

    /*private static class EpicLootEnchantingUI
    {
        static Type listType = AccessTools.TypeByName("EpicLoot_UnityLib.InventoryItemListElement");
        static Type genericListType = typeof(List<>).MakeGenericType(listType);
        static object? specificListInstance = Activator.CreateInstance(genericListType);

        [HarmonyPatch("EpicLoot.CraftingV2.EnchantingUIController, EpicLoot", "GetEnchantableItems"), HarmonyPostfix]
        private static void GetEnchantableItemsPostfixPatch(ref object __result)
        {
            try
            {
                if (listType == null)
                {
                    AzuCraftyBoxesPlugin.AzuCraftyBoxesLogger.LogWarning("List type not found.");
                    return;
                }

                AzuCraftyBoxesPlugin.AzuCraftyBoxesLogger.LogWarning($"Specific list instance: {specificListInstance}");
                MethodInfo addMethod = genericListType.GetMethod("Add");
                MethodInfo clearMethod = genericListType.GetMethod("Clear");
                if (addMethod == null)
                {
                    AzuCraftyBoxesPlugin.AzuCraftyBoxesLogger.LogWarning("Add method not found.");
                    return;
                }

                if (clearMethod == null)
                {
                    AzuCraftyBoxesPlugin.AzuCraftyBoxesLogger.LogWarning("Clear method not found.");
                    return;
                }

                // Cast __result to the appropriate collection type
                var enchantableItems = (IList)Activator.CreateInstance(genericListType);
                if (__result is IEnumerable existingItems)
                {
                    foreach (var item in existingItems)
                    {
                        enchantableItems.Add(item);
                    }
                }

                clearMethod.Invoke(specificListInstance, null);
                try
                {
                    foreach (Container container in Boxes.Containers)
                    {
                        AzuCraftyBoxesPlugin.AzuCraftyBoxesLogger.LogIfDebuggingEpicLoot("Checking container");
                        foreach (ItemDrop.ItemData item in container.GetInventory().GetAllItems())
                        {
                            AzuCraftyBoxesPlugin.AzuCraftyBoxesLogger.LogIfDebuggingEpicLoot($"Checking item {item.m_shared.m_name}");
                            if (isMagicMethod == null)
                            {
                                AzuCraftyBoxesPlugin.AzuCraftyBoxesLogger.LogIfDebuggingEpicLoot("IsMagic method not found.");
                                return;
                            }

                            if (getCanBeMagicItemMethod == null)
                            {
                                AzuCraftyBoxesPlugin.AzuCraftyBoxesLogger.LogIfDebuggingEpicLoot("GetCanBeMagicItem method not found.");
                                return;
                            }

                            if (!(bool)isMagicMethod?.Invoke(null, [item])! && EpicLootReflectionHelpers.CanBeMagicItem(item))
                            {
                                AzuCraftyBoxesPlugin.AzuCraftyBoxesLogger.LogIfDebuggingEpicLoot("Item can be magic");
                                object element = EpicLootReflectionHelpers.CreateInventoryItemListElement(item, listType);
                                AzuCraftyBoxesPlugin.AzuCraftyBoxesLogger.LogIfDebuggingEpicLoot($"Element: {element}");
                                if (addMethod != null)
                                {
                                    AzuCraftyBoxesPlugin.AzuCraftyBoxesLogger.LogIfDebuggingEpicLoot("Adding item to enchantable items");
                                    addMethod.Invoke(enchantableItems, new[] { element });
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    AzuCraftyBoxesPlugin.AzuCraftyBoxesLogger.LogError($"Error adding items to enchantable items: {ex}");
                }

                AzuCraftyBoxesPlugin.AzuCraftyBoxesLogger.LogIfDebuggingEpicLoot($"Specific list instance count: {genericListType.GetProperty("Count")?.GetValue(specificListInstance)}");


                // Set the result to be the result plus the custom items
                __result = enchantableItems;
            }
            catch (Exception ex)
            {
                AzuCraftyBoxesPlugin.AzuCraftyBoxesLogger.LogWarning($"Error in Prefix: {ex}");
            }
        }

        /*[HarmonyPatch("EpicLoot.CraftingV2.EnchantingUIController, EpicLoot", "GetSacrificeItems"), HarmonyPostfix]
        private static void GetSacrificeItemsPostfixPatch(ref object __result)
        {
            try
            {
                if (listType == null)
                {
                    AzuCraftyBoxesPlugin.AzuCraftyBoxesLogger.LogWarning("List type not found.");
                    return; // Fall back to original method
                }

                MethodInfo addMethod = genericListType.GetMethod("Add");
                if (addMethod == null)
                {
                    AzuCraftyBoxesPlugin.AzuCraftyBoxesLogger.LogWarning("Add method not found.");
                    return; // Fall back to the original method
                }

                // Cast __result to the appropriate collection type
                var sacrificeItems = (IList)Activator.CreateInstance(genericListType);
                if (__result is IEnumerable existingItems)
                {
                    foreach (var item in existingItems)
                    {
                        sacrificeItems.Add(item);
                    }
                }

                try
                {
                    foreach (Container container in Boxes.Containers)
                    {
                        AzuCraftyBoxesPlugin.AzuCraftyBoxesLogger.LogWarning("Checking container");
                        foreach (ItemDrop.ItemData item in container.GetInventory().GetAllItems())
                        {
                            AzuCraftyBoxesPlugin.AzuCraftyBoxesLogger.LogWarning($"Checking item {item.m_shared.m_name}");
                            if (isMagicMethod == null)
                            {
                                AzuCraftyBoxesPlugin.AzuCraftyBoxesLogger.LogWarning("IsMagic method not found.");
                                return;
                            }

                            if (getCanBeMagicItemMethod == null)
                            {
                                AzuCraftyBoxesPlugin.AzuCraftyBoxesLogger.LogWarning("GetCanBeMagicItem method not found.");
                                return;
                            }

                            if (!(bool)isMagicMethod?.Invoke(null, new object[] { item })! && EpicLootReflectionHelpers.CanBeMagicItem(item))
                            {
                                AzuCraftyBoxesPlugin.AzuCraftyBoxesLogger.LogWarning("Item can be magic");
                                object element = EpicLootReflectionHelpers.CreateInventoryItemListElement(item, listType);
                                AzuCraftyBoxesPlugin.AzuCraftyBoxesLogger.LogWarning($"Element: {element}");
                                if (addMethod != null)
                                {
                                    AzuCraftyBoxesPlugin.AzuCraftyBoxesLogger.LogWarning("Adding item to sacrifice items");
                                    addMethod.Invoke(sacrificeItems, new[] { element });
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    AzuCraftyBoxesPlugin.AzuCraftyBoxesLogger.LogError($"Error adding items to sacrifice items: {ex}");
                }

                // Update __result to include the appended items
                __result = sacrificeItems;
            }
            catch (Exception ex)
            {
                AzuCraftyBoxesPlugin.AzuCraftyBoxesLogger.LogWarning($"Error in Prefix: {ex}");
                return; // If there's an error, fall back to the original method
            }
        }#1#

        [HarmonyPatch("EpicLoot.CraftingV2.EnchantingUIController, EpicLoot", "GetAugmentableItems"), HarmonyPostfix]
        private static void GetAugmentableItemsPostfixPatch(ref object __result)
        {
            try
            {
                if (listType == null)
                {
                    AzuCraftyBoxesPlugin.AzuCraftyBoxesLogger.LogWarning("List type not found.");
                    return; // Fall back to original method
                }

                MethodInfo addMethod = genericListType.GetMethod("Add");
                if (addMethod == null)
                {
                    AzuCraftyBoxesPlugin.AzuCraftyBoxesLogger.LogWarning("Add method not found.");
                    return; // Fall back to the original method
                }

                // Cast __result to the appropriate collection type
                var augmentableItems = (IList)Activator.CreateInstance(genericListType);
                if (__result is IEnumerable existingItems)
                {
                    foreach (var item in existingItems)
                    {
                        augmentableItems.Add(item);
                    }
                }

                try
                {
                    foreach (Container container in Boxes.Containers)
                    {
                        AzuCraftyBoxesPlugin.AzuCraftyBoxesLogger.LogWarning("Checking container");
                        foreach (ItemDrop.ItemData item in container.GetInventory().GetAllItems())
                        {
                            AzuCraftyBoxesPlugin.AzuCraftyBoxesLogger.LogWarning($"Checking item {item.m_shared.m_name}");
                            if (isMagicMethod == null)
                            {
                                AzuCraftyBoxesPlugin.AzuCraftyBoxesLogger.LogWarning("IsMagic method not found.");
                                return;
                            }

                            if (getCanBeMagicItemMethod == null)
                            {
                                AzuCraftyBoxesPlugin.AzuCraftyBoxesLogger.LogWarning("GetCanBeMagicItem method not found.");
                                return;
                            }

                            if ((bool)isMagicMethod?.Invoke(null, new object[] { item })! && EpicLootReflectionHelpers.CanBeAugmented(item))
                            {
                                AzuCraftyBoxesPlugin.AzuCraftyBoxesLogger.LogWarning("Item can be magic");
                                object element = EpicLootReflectionHelpers.CreateInventoryItemListElement(item, listType);
                                AzuCraftyBoxesPlugin.AzuCraftyBoxesLogger.LogWarning($"Element: {element}");
                                if (addMethod != null)
                                {
                                    AzuCraftyBoxesPlugin.AzuCraftyBoxesLogger.LogWarning("Adding item to augmentable items");
                                    addMethod.Invoke(augmentableItems, new[] { element });
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    AzuCraftyBoxesPlugin.AzuCraftyBoxesLogger.LogError($"Error adding items to augmentable items: {ex}");
                }

                // Update __result to include the appended items
                __result = augmentableItems;
            }
            catch (Exception ex)
            {
                AzuCraftyBoxesPlugin.AzuCraftyBoxesLogger.LogWarning($"Error in Prefix: {ex}");
                return; // If there's an error, fall back to the original method
            }
        }
    }*/

    public static class EpicLootEnchantingUI
    {
        public static Type listType = AccessTools.TypeByName("EpicLoot_UnityLib.InventoryItemListElement");
        public static Type genericListType = typeof(List<>).MakeGenericType(listType);
        public static MethodInfo addMethod = genericListType.GetMethod("Add");
        public static MethodInfo clearMethod = genericListType.GetMethod("Clear");

        [HarmonyPatch("EpicLoot.CraftingV2.EnchantingUIController, EpicLoot", "GetEnchantableItems"), HarmonyPostfix]
        private static void GetEnchantableItemsPostfixPatch(ref object __result)
        {
            EpicLootReflectionHelpers.AppendItemsFromContainers(ref __result, EpicLootReflectionHelpers.CanBeMagicItem);
        }

        /*[HarmonyPatch("EpicLoot.CraftingV2.EnchantingUIController, EpicLoot", "GetSacrificeItems"), HarmonyPostfix]
        private static void GetSacrificeItemsPostfixPatch(ref object __result)
        {
            EpicLootReflectionHelpers.AppendItemsFromContainers(ref __result, EpicLootReflectionHelpers.CanBeMagicItem);
        }*/

        [HarmonyPatch("EpicLoot.CraftingV2.EnchantingUIController, EpicLoot", "GetAugmentableItems"), HarmonyPostfix]
        private static void GetAugmentableItemsPostfixPatch(ref object __result)
        {
            EpicLootReflectionHelpers.AppendItemsFromContainers(ref __result, EpicLootReflectionHelpers.CanBeAugmented, true);
        }

        /*[HarmonyPatch("EpicLoot.CraftingV2.EnchantingUIController, EpicLoot", "GetDisenchantItems"), HarmonyPostfix]
        private static void GetDisenchantItemsPostfixPatch(ref object __result)
        {
            EpicLootReflectionHelpers.AppendItemsFromContainers(ref __result, EpicLootReflectionHelpers.CanBeDisenchanted, true);
        }*/
    }
}