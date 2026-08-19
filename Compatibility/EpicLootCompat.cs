using AzuCraftyBoxes.IContainers;
using AzuCraftyBoxes.Util.Functions;
using EpicLootApi = EpicLootAPI.EpicLoot;

namespace AzuCraftyBoxes.Compatibility;

public static class EpicLootCompat
{
    private const string TablePrefabName = "piece_enchantingtable";

    internal static void Init(string providerId)
    {
        if (!EpicLootApi.IsLoaded()) return;
        EpicLootApi.RegisterInventoryProvider(providerId, GetItems, CountItem, RemoveItem, RemoveExactItem);
    }

    private static List<IContainer> Nearby() =>
        MiscFunctions.ShouldPrevent() ? [] : Boxes.QueryFrame.Get(Player.m_localPlayer, AzuCraftyBoxesPlugin.mRange.Value);

    // EpicLoot needs the live instances, not copies, so magic data survives the round trip.
    private static List<ItemDrop.ItemData> GetItems()
    {
        List<ItemDrop.ItemData> items = [];
        foreach (IContainer container in Nearby())
        {
            Inventory? inventory = container.GetInventory();
            if (inventory == null) continue;

            foreach (ItemDrop.ItemData item in inventory.GetAllItems())
            {
                if (item?.m_dropPrefab == null) continue;
                if (!Boxes.CanItemBePulled(container.GetPrefabName(), item.m_dropPrefab.name, TablePrefabName)) continue;
                items.Add(item);
            }
        }

        return items;
    }

    private static int CountItem(string itemName)
    {
        int count = 0;
        foreach (IContainer container in Nearby())
        {
            count += Boxes.CheckAndDecrement(container.ItemCount(itemName));
        }

        return count;
    }

    private static int RemoveItem(string itemName, int amount)
    {
        int removed = 0;
        foreach (IContainer container in Nearby())
        {
            if (removed >= amount) break;

            int available = Boxes.CheckAndDecrement(container.ItemCount(itemName));
            if (available <= 0) continue;

            int take = Math.Min(available, amount - removed);
            container.RemoveItem(itemName, take);
            container.Save();
            removed += take;
        }

        if (removed < amount)
            AzuCraftyBoxesPlugin.AzuCraftyBoxesLogger.LogIfReleaseAndDebugEnable($"Only removed {removed}/{amount} of '{itemName}' from containers.");

        return removed;
    }

    // Match by reference: a name match would consume the wrong enchanted item.
    private static int RemoveExactItem(ItemDrop.ItemData item, int amount)
    {
        foreach (IContainer container in Nearby())
        {
            Inventory? inventory = container.GetInventory();
            if (inventory == null || !inventory.GetAllItems().Contains(item)) continue;

            int take = Math.Min(item.m_stack, amount);
            if (!inventory.RemoveItem(item, take)) continue;

            container.Save();
            return take;
        }

        return 0;
    }
}
