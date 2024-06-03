using Backpacks;
using UnityEngine;

namespace AzuCraftyBoxes.IContainers;

public class BackpackContainer(ItemContainer _container) : IContainer
{
    public int ProcessContainerInventory(string reqName, int totalAmount, int totalRequirement)
    {
        Inventory cInventory = _container.Inventory;
        int thisAmount = Mathf.Min(cInventory.CountItems(reqName), totalRequirement - totalAmount);


        if (thisAmount == 0) return totalAmount;

        for (int i = 0; i < cInventory.GetAllItems().Count; ++i)
        {
            ItemDrop.ItemData item = cInventory.GetItem(i);
            if (item?.m_shared?.m_name != reqName) continue;
            AzuCraftyBoxesPlugin.AzuCraftyBoxesLogger.LogDebug($"Container Total Items Count is {cInventory.GetAllItems().Count}");
            AzuCraftyBoxesPlugin.AzuCraftyBoxesLogger.LogDebug($"(ConsumeResourcesPatch) Got stack of {item.m_stack} {reqName}");

            int stackAmount = Mathf.Min(item.m_stack, totalRequirement - totalAmount);
            if (stackAmount == item.m_stack)
            {
                AzuCraftyBoxesPlugin.AzuCraftyBoxesLogger.LogDebug($"(ConsumeResourcesPatch) Removing item {reqName} from container");
                AzuCraftyBoxesPlugin.AzuCraftyBoxesLogger.LogDebug($"Container inventory before removal: {cInventory.GetAllItems().Count}, Item at index {i}: {cInventory.GetItem(i)?.m_shared?.m_name}");

                bool removed = cInventory.RemoveItem(i);
                AzuCraftyBoxesPlugin.AzuCraftyBoxesLogger.LogDebug("Removed was " + removed);
                AzuCraftyBoxesPlugin.AzuCraftyBoxesLogger.LogDebug($"Container inventory after attempted removal: {cInventory.GetAllItems().Count}");

                --i;
            }
            else
            {
                AzuCraftyBoxesPlugin.AzuCraftyBoxesLogger.LogDebug($"(ConsumeResourcesPatch) Removing {stackAmount} {reqName} from container");
                item.m_stack -= stackAmount;
            }

            totalAmount += stackAmount;

            AzuCraftyBoxesPlugin.AzuCraftyBoxesLogger.LogDebug($"(ConsumeResourcesPatch) Total amount is now {totalAmount}/{totalRequirement} {reqName}");

            if (totalAmount >= totalRequirement)
            {
                break;
            }
        }

        try
        {
            _container.Save();
            cInventory.Changed();
        }
        catch
        {
            // Do nothing because this occasionally fails on backpacks. Fix better later.
        }

        AzuCraftyBoxesPlugin.AzuCraftyBoxesLogger.LogDebug("Saved container");

        if (totalAmount >= totalRequirement)
        {
            AzuCraftyBoxesPlugin.AzuCraftyBoxesLogger.LogDebug($"(ConsumeResourcesPatch) Consumed enough {reqName}");
        }

        return totalAmount;
    }

    public int ItemCount(string name)
    {
        int result = Backpacks.API.CountItemsInBackpacks(Player.m_localPlayer.GetInventory(), name);
        AzuCraftyBoxesPlugin.AzuCraftyBoxesLogger.LogDebug($"Found {result} {name} in backpacks");
        return result;
    }

    public void RemoveItem(string name, int amount)
    {
        Backpacks.API.DeleteItemsFromBackpacks(Player.m_localPlayer.GetInventory(), name, amount);
    }

    public void RemoveItem(string prefab, string sharedName, int amount)
    {
        Backpacks.API.DeleteItemsFromBackpacks(Player.m_localPlayer.GetInventory(), sharedName, amount);
    }

    public void Save()
    {
        try
        {
            _container.Save();
            _container.Inventory?.Changed();
        }
        catch (Exception e)
        {
            // Ignored for Backpacks.
        }
    }

    public Vector3 GetPosition() => Player.m_localPlayer.transform.position;
    public string GetPrefabName() => _container.Item.m_dropPrefab.name;


    public static BackpackContainer Create(ItemContainer container) => new(container);
}