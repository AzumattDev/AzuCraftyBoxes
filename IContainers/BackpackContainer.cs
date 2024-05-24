using System;
using AzuCraftyBoxes.Util.Functions;
using Backpacks;
using UnityEngine;

namespace AzuCraftyBoxes.IContainers;

public class BackpackContainer(ItemContainer _container) : IContainer
{
    public int ProcessContainerInventory(string reqPrefab, string reqName, int totalAmount, int totalRequirement)
    {
        Inventory cInventory = _container.Inventory;
        if (cInventory == null) return totalAmount;
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
        _container.Save();
        cInventory.Changed();
        AzuCraftyBoxesPlugin.AzuCraftyBoxesLogger.LogDebug("Saved container");

        if (totalAmount >= totalRequirement)
        {
            AzuCraftyBoxesPlugin.AzuCraftyBoxesLogger.LogDebug($"(ConsumeResourcesPatch) Consumed enough {reqName}");
        }

        return totalAmount;
    }

    public bool ContainsItem(string prefab, int amount, out int result)
    {
        AzuCraftyBoxesPlugin.AzuCraftyBoxesLogger.LogDebug($"Checking for {amount} {prefab} in backpacks");
        result = Backpacks.API.CountItemsInBackpacks(Player.m_localPlayer.GetInventory(), prefab);
        AzuCraftyBoxesPlugin.AzuCraftyBoxesLogger.LogDebug($"Found {result} {prefab} in backpacks");
        return result >= amount;
    }
    public bool ContainsItem(string prefab, int amount, string sharedName, out int result)
    {
        AzuCraftyBoxesPlugin.AzuCraftyBoxesLogger.LogDebug($"Checking for {amount} {prefab} or {sharedName} in backpacks");
        result = Backpacks.API.CountItemsInBackpacks(Player.m_localPlayer.GetInventory(), prefab);
        result += Backpacks.API.CountItemsInBackpacks(Player.m_localPlayer.GetInventory(), sharedName);
        AzuCraftyBoxesPlugin.AzuCraftyBoxesLogger.LogDebug($"Found {result} {prefab} or {sharedName} in backpacks");
        return result >= amount;
    }

    public void RemoveItem(string prefab, int amount)
    {
        Backpacks.API.DeleteItemsFromBackpacks(Player.m_localPlayer.GetInventory(), prefab, amount);
    }
    
    public void Save()
    {
        _container.Save();
        _container.Inventory?.Changed();
    }

    public Vector3 GetPosition() => Player.m_localPlayer.transform.position;
    public string GetPrefabName() => "bp_explorer";


    public static BackpackContainer Create(ItemContainer container) => new(container);
}