using AzuCraftyBoxes.Compatibility.WardIsLove;
using AzuCraftyBoxes.IContainers;
using AzuCraftyBoxes.Util.Functions;

namespace AzuCraftyBoxes.Compatibility.EpicLoot;

public static class EpicLoot
{
    public const string ElGuid = "randyknapp.mods.epicloot";
    public const string TablePrefabName = "piece_enchantingtable";
    public static PluginInfo? EpicLootPluginInfo { get; set; }
    public static Assembly? EpicLootAssembly { get; private set; }


    public static void Init(PluginInfo? pluginInfo)
    {
        EpicLootPluginInfo = pluginInfo;
        EpicLootAssembly = pluginInfo?.Instance?.GetType().Assembly;

        AzuCraftyBoxesPlugin.harmony.PatchAll(typeof(EpicLootEnchantingUI));
    }

    public static class EpicLootEnchantingUI
    {
        [HarmonyPatch("EpicLoot_UnityLib.InventoryManagement, EpicLoot-UnityLib", "GetAllItems"), HarmonyPostfix]
        private static void GetAllItemsPostfix(ref List<ItemDrop.ItemData> __result)
        {
            EpicLootReflectionHelpers.AppendContainerItemsToInventory(ref __result);
        }

        [HarmonyPatch("EpicLoot_UnityLib.InventoryManagement, EpicLoot-UnityLib", "HasItem"), HarmonyPostfix]
        private static void HasItemPostfix(ItemDrop.ItemData item, ref bool __result)
        {
            EpicLootReflectionHelpers.DoesContainerHaveItem(item, ref __result);
        }

        [HarmonyPatch("EpicLoot_UnityLib.InventoryManagement, EpicLoot-UnityLib", "CountItem", MethodType.Normal, [typeof(string)]), HarmonyPostfix]
        private static void CountItemPostfix(string item, ref int __result)
        {
            __result += EpicLootReflectionHelpers.CountContainerItems(item);
        }

        [HarmonyPatch("EpicLoot_UnityLib.InventoryManagement, EpicLoot-UnityLib", "RemoveItem", MethodType.Normal, [typeof(string), typeof(int)]), HarmonyPrefix]
        private static void RemoveItemPrefix(string item, int amount, ref int __state)
        {
            // Capture the initial count of the item in the player's inventory
            __state = EpicLootReflectionHelpers.CountPlayerItems(item);
            AzuCraftyBoxesPlugin.AzuCraftyBoxesLogger.LogDebug($"Initial count of '{item}' in player inventory: {__state}");
        }

        [HarmonyPatch("EpicLoot_UnityLib.InventoryManagement, EpicLoot-UnityLib", "RemoveItem", MethodType.Normal, [typeof(string), typeof(int)]), HarmonyPostfix]
        private static void RemoveItemPostfix(string item, int amount, int __state)
        {
            AzuCraftyBoxesPlugin.AzuCraftyBoxesLogger.LogDebug($"Removing {amount} of '{item}' from player inventory.");
            // Capture the new count of the item in the player's inventory after removal
            int newCount = EpicLootReflectionHelpers.CountPlayerItems(item);

            // Calculate how many items were removed from the player's inventory
            int removedFromPlayer = __state - newCount;

            // Ensure we don't have a negative value
            if (removedFromPlayer < 0)
                removedFromPlayer = 0;

            // Calculate the remaining amount to remove from containers
            int remainingToRemove = amount - removedFromPlayer;
            AzuCraftyBoxesPlugin.AzuCraftyBoxesLogger.LogDebug($"Removed {removedFromPlayer} of '{item}' from player inventory. Remaining to remove: {remainingToRemove}");
            // If there's still an amount left to remove, proceed to remove it from containers
            // We can assume the HasItem check has already been done and that there are enough items in the containers
            if (remainingToRemove > 0)
            {
                EpicLootReflectionHelpers.RemoveContainerItems(item, remainingToRemove);
            }
        }

        [HarmonyPatch("EpicLoot_UnityLib.InventoryManagement, EpicLoot-UnityLib", "RemoveExactItem"), HarmonyPrefix]
        private static void RemoveExactItemPrefix(ItemDrop.ItemData item, int amount, ref int __state)
        {
            // Capture the initial count of the item in the player's inventory
            __state = EpicLootReflectionHelpers.CountPlayerItems(item);
            AzuCraftyBoxesPlugin.AzuCraftyBoxesLogger.LogDebug($"Initial count of '{item}' in player inventory: {__state}");
        }

        [HarmonyPatch("EpicLoot_UnityLib.InventoryManagement, EpicLoot-UnityLib", "RemoveExactItem"), HarmonyPostfix]
        private static void RemoveExactItem(ItemDrop.ItemData item, int amount, int __state)
        {
            AzuCraftyBoxesPlugin.AzuCraftyBoxesLogger.LogDebug($"Removing {amount} of '{item}' from player inventory.");
            // Capture the new count of the item in the player's inventory after removal
            int newCount = EpicLootReflectionHelpers.CountPlayerItems(item);

            // Calculate how many items were removed from the player's inventory
            int removedFromPlayer = __state - newCount;

            // Ensure we don't have a negative value
            if (removedFromPlayer < 0)
                removedFromPlayer = 0;

            // Calculate the remaining amount to remove from containers
            int remainingToRemove = amount - removedFromPlayer;
            AzuCraftyBoxesPlugin.AzuCraftyBoxesLogger.LogDebug($"Removed {removedFromPlayer} of '{item}' from player inventory. Remaining to remove: {remainingToRemove}");
            // If there's still an amount left to remove, proceed to remove it from containers
            // We can assume the HasItem check has already been done and that there are enough items in the containers
            if (remainingToRemove > 0)
            {
                EpicLootReflectionHelpers.RemoveSpecificContainerItem(item, remainingToRemove);
            }
        }
    }

    public class EpicLootReflectionHelpers
    {
        public static void AppendContainerItemsToInventory(ref List<ItemDrop.ItemData> playerInventory)
        {
            try
            {
                List<ItemDrop.ItemData> combinedItems = [..playerInventory];
                List<IContainer> nearbyContainers = Boxes.GetNearbyContainers(Player.m_localPlayer, AzuCraftyBoxesPlugin.mRange.Value);

                foreach (IContainer container in nearbyContainers)
                {
                    Inventory? containerInventory = container.GetInventory();

                    if (containerInventory == null) continue;
                    foreach (ItemDrop.ItemData item in containerInventory.GetAllItems())
                    {
                        if (item?.m_dropPrefab == null)
                            continue;
                        if (!Boxes.CanItemBePulled(container.GetPrefabName(), item.m_dropPrefab.name, TablePrefabName)) continue;
                        combinedItems.Add(item);
                    }
                }

                playerInventory = combinedItems;
            }
            catch (Exception ex)
            {
                AzuCraftyBoxesPlugin.AzuCraftyBoxesLogger.LogWarning($"Error combining container items: {ex}");
            }
        }

        public static void DoesContainerHaveItem(ItemDrop.ItemData item, ref bool result)
        {
            try
            {
                List<IContainer> nearbyContainers = Boxes.GetNearbyContainers(Player.m_localPlayer, AzuCraftyBoxesPlugin.mRange.Value);

                foreach (IContainer container in nearbyContainers)
                {
                    if (item?.m_dropPrefab == null)
                        continue;
                    if (!Boxes.CanItemBePulled(container.GetPrefabName(), item.m_dropPrefab.name, TablePrefabName)) continue;
                    Inventory? containerInventory = container.GetInventory();

                    if (containerInventory == null || (Boxes.CheckAndDecrement(containerInventory.CountItems(item.m_shared.m_name)) + CountPlayerItems(item.m_shared.m_name)) < item.m_stack) continue;
                    result = true;
                    return;
                }
            }
            catch (Exception ex)
            {
                AzuCraftyBoxesPlugin.AzuCraftyBoxesLogger.LogWarning($"Error checking container items: {ex}");
            }
        }

        public static int CountPlayerItems(string itemName)
        {
            try
            {
                if (Player.m_localPlayer != null)
                {
                    Inventory playerInventory = Player.m_localPlayer.GetInventory();
                    return playerInventory.CountItems(itemName);
                }

                AzuCraftyBoxesPlugin.AzuCraftyBoxesLogger.LogWarning("Player.m_localPlayer is null.");
                return 0;
            }
            catch (Exception ex)
            {
                AzuCraftyBoxesPlugin.AzuCraftyBoxesLogger.LogWarning($"Error counting player items: {ex}");
                return 0;
            }
        }

        public static int CountPlayerItems(ItemDrop.ItemData item)
        {
            try
            {
                if (Player.m_localPlayer != null)
                {
                    Inventory playerInventory = Player.m_localPlayer.GetInventory();
                    string? name = item.m_shared.m_name;
                    int num = 0;
                    foreach (ItemDrop.ItemData itemData in playerInventory.m_inventory)
                    {
                        if ((name == null || itemData.m_shared.m_name == name) && (itemData.m_worldLevel >= Game.m_worldLevel))
                        {
                            if (itemData == item)
                                num += itemData.m_stack;
                        }
                    }

                    return num;
                }

                AzuCraftyBoxesPlugin.AzuCraftyBoxesLogger.LogWarning("Player.m_localPlayer is null.");
                return 0;
            }
            catch (Exception ex)
            {
                AzuCraftyBoxesPlugin.AzuCraftyBoxesLogger.LogWarning($"Error counting player items: {ex}");
                return 0;
            }
        }


        public static int CountContainerItems(string itemName)
        {
            int count = 0;
            try
            {
                List<IContainer> nearbyContainers = Boxes.GetNearbyContainers(Player.m_localPlayer, AzuCraftyBoxesPlugin.mRange.Value);
                foreach (IContainer container in nearbyContainers)
                {
                    Inventory? containerInventory = container.GetInventory();

                    if (containerInventory != null)
                    {
                        count += Boxes.CheckAndDecrement(containerInventory.CountItems(itemName));
                    }
                }
            }
            catch (Exception ex)
            {
                AzuCraftyBoxesPlugin.AzuCraftyBoxesLogger.LogWarning($"Error counting container items: {ex}");
            }

            return count;
        }

        public static void RemoveContainerItems(string itemName, int amount)
        {
            try
            {
                List<IContainer> nearbyContainers = Boxes.GetNearbyContainers(Player.m_localPlayer, AzuCraftyBoxesPlugin.mRange.Value);

                AzuCraftyBoxesPlugin.AzuCraftyBoxesLogger.LogDebug($"Starting to remove {amount} of '{itemName}' from containers.");
                foreach (IContainer container in nearbyContainers)
                {
                    Inventory? containerInventory = container.GetInventory();

                    if (containerInventory == null) continue;
                    List<ItemDrop.ItemData> items = containerInventory.GetAllItems();

                    List<ItemDrop.ItemData> matchingItems = items.FindAll(item => item.m_shared.m_name.Equals(itemName, StringComparison.OrdinalIgnoreCase));

                    foreach (ItemDrop.ItemData? item in matchingItems)
                    {
                        if (amount <= 0)
                            break;

                        int removeAmount = Math.Min(item.m_stack, amount);
                        bool success = containerInventory.RemoveItem(item, removeAmount);
                        if (success)
                        {
                            AzuCraftyBoxesPlugin.AzuCraftyBoxesLogger.LogDebug($"Removed {removeAmount} of '{itemName}' from container '{container.GetPrefabName()}'. Remaining to remove: {amount - removeAmount}");
                            amount -= removeAmount;
                        }
                        else
                        {
                            AzuCraftyBoxesPlugin.AzuCraftyBoxesLogger.LogWarning($"Failed to remove {removeAmount} of '{itemName}' from container '{container.GetPrefabName()}'.");
                        }

                        if (amount <= 0)
                            break;
                    }
                }

                if (amount > 0)
                {
                    AzuCraftyBoxesPlugin.AzuCraftyBoxesLogger.LogWarning($"Unable to remove the full amount of '{itemName}' from containers. {amount} remaining.");
                }
            }
            catch (Exception ex)
            {
                AzuCraftyBoxesPlugin.AzuCraftyBoxesLogger.LogWarning($"Error removing container items: {ex}");
            }
        }

        public static void RemoveSpecificContainerItem(ItemDrop.ItemData item, int amount)
        {
            try
            {
                AzuCraftyBoxesPlugin.AzuCraftyBoxesLogger.LogDebug($"Starting to remove {amount} of '{item.m_shared.m_name}' from containers.");
                foreach (Container container in Boxes.Containers)
                {
                    Inventory containerInventory = container.GetInventory();

                    if (containerInventory == null) continue;
                    List<ItemDrop.ItemData> items = containerInventory.GetAllItems();

                    List<ItemDrop.ItemData> matchingItems = items.FindAll(i => i == item);

                    foreach (ItemDrop.ItemData? containerItem in matchingItems)
                    {
                        if (amount <= 0)
                            break;

                        int removeAmount = Math.Min(containerItem.m_stack, amount);
                        bool success = containerInventory.RemoveItem(containerItem, removeAmount);
                        if (success)
                        {
                            AzuCraftyBoxesPlugin.AzuCraftyBoxesLogger.LogDebug($"Removed {removeAmount} of '{item.m_shared.m_name}' from container '{container.name}'. Remaining to remove: {amount - removeAmount}");
                            amount -= removeAmount;
                        }
                        else
                        {
                            AzuCraftyBoxesPlugin.AzuCraftyBoxesLogger.LogWarning($"Failed to remove {removeAmount} of '{item.m_shared.m_name}' from container '{container.name}'.");
                        }

                        if (amount <= 0)
                            break;
                    }
                }

                if (amount > 0)
                {
                    AzuCraftyBoxesPlugin.AzuCraftyBoxesLogger.LogWarning($"Unable to remove the full amount of '{item.m_shared.m_name}' from containers. {amount} remaining.");
                }
            }
            catch (Exception ex)
            {
                AzuCraftyBoxesPlugin.AzuCraftyBoxesLogger.LogWarning($"Error removing container items: {ex}");
            }
        }


        public static void AppendContainerItemsToInventory(ref Inventory playerInventory)
        {
            try
            {
                // Create a list to hold combined items from the player and containers
                List<ItemDrop.ItemData> combinedItems = [..playerInventory.GetAllItems()];

                List<IContainer> nearbyContainers = Boxes.GetNearbyContainers(Player.m_localPlayer, AzuCraftyBoxesPlugin.mRange.Value);

                // Iterate over all containers and collect items
                foreach (IContainer container in nearbyContainers)
                {
                    Inventory? containerInventory = container.GetInventory();

                    if (containerInventory == null) continue;
                    // Add each item in container to the combined list
                    foreach (ItemDrop.ItemData item in containerInventory.GetAllItems())
                    {
                        combinedItems.Add(item);
                    }
                }

                playerInventory.m_inventory = combinedItems;
            }
            catch (Exception ex)
            {
                AzuCraftyBoxesPlugin.AzuCraftyBoxesLogger.LogWarning($"Error combining container items: {ex}");
            }
        }
    }
}