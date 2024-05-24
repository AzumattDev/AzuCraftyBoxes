using UnityEngine;

namespace AzuCraftyBoxes.IContainers;

public interface IContainer
{
    public int ProcessContainerInventory(string reqPrefab, string reqName, int totalAmount, int totalRequirement);
    public bool ContainsItem(string prefab, int amount, out int result);
    public bool ContainsItem(string prefab, int amount, string sharedName, out int result);
    public void RemoveItem(string prefab, int amount);
    public Vector3 GetPosition();
    public void Save();
    public string GetPrefabName();
}