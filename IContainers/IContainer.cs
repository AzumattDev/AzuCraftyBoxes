using UnityEngine;

namespace AzuCraftyBoxes.IContainers;

public interface IContainer
{
    public int ProcessContainerInventory(string reqName, int totalAmount, int totalRequirement);
    public int ItemCount(string name);
    public void RemoveItem(string name, int amount);
    public Vector3 GetPosition();
    public void Save();
    public string GetPrefabName();
}

static class IContainerExtensions
{
    public static bool ContainsItem(this IContainer container, string name, int amount, out int result)
    {
        result = container.ItemCount(name);
        return result >= amount;
    }
}
