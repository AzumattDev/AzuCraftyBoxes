using UnityEngine;

namespace AzuCraftyBoxes.IContainers;

public class VanillaContainer(Container _container) : IContainer
{
    public int ProcessContainerInventory(string reqName, int totalAmount, int totalRequirement)
    {
        Inventory cInventory = _container.GetInventory();
        if (cInventory == null) return totalAmount;
        int thisAmount = Mathf.Min(cInventory.CountItems(reqName), totalRequirement - totalAmount);

        AzuCraftyBoxesPlugin.AzuCraftyBoxesLogger.LogDebug($"(ConsumeResourcesPatch) Container at {_container.transform.position} has {cInventory.CountItems(reqName)}");

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

    public int ItemCount(string name) => _container.GetInventory()?.CountItems(name) ?? 0;

    public void RemoveItem(string name, int amount) => _container.GetInventory()?.RemoveItem(name, amount);
    
    public void Save()
    {
        _container.Save();
        _container.m_inventory?.Changed();
    }

    public Vector3 GetPosition() => _container.transform.position;
    public string GetPrefabName() => Utils.GetPrefabName(_container.gameObject);


    public static VanillaContainer Create(Container container) => new(container);
}