using AzuCraftyBoxes.APIs;
using AzuCraftyBoxes.Util.Functions;
using UnityEngine;

namespace AzuCraftyBoxes.IContainers;

public class mkzDrawer(MkzItemDrawers_API.mkzDrawer _drawer) : IContainer
{
    // Safely resolve the shared name from the prefab name.
    private string? Name
    {
        get
        {
            // ObjectDB not ready yet? Just say "no item".
            if (ObjectDB.instance == null)
                return null;

            string? prefabName = _drawer.Prefab;
            if (string.IsNullOrEmpty(prefabName))
                return null;

            GameObject prefab = ObjectDB.instance.GetItemPrefab(prefabName);
            if (!prefab)
                return null;

            ItemDrop itemDrop = prefab.GetComponent<ItemDrop>();
            return itemDrop?.m_itemData?.m_shared?.m_name;
        }
    }

    public int ProcessContainerInventory(string reqName, int totalAmount, int totalRequirement)
    {
        string? name = Name;
        if (string.IsNullOrEmpty(name) || name != reqName)
            return totalAmount;

        int thisAmount = Mathf.Min(_drawer.Amount, totalRequirement - totalAmount);
        _drawer.ConsumeSilently(thisAmount);
        return totalAmount + thisAmount;
    }

    public int ItemCount(string name)
    {
        string? drawerName = Name;
        return !string.IsNullOrEmpty(drawerName) && drawerName == name ? _drawer.Amount : 0;
    }

    public void RemoveItem(string name, int amount)
    {
        amount = Mathf.Min(amount, _drawer.Amount);
        _drawer.ConsumeSilently(amount);
    }

    public void RemoveItem(string prefab, string sharedName, int amount)
    {
        amount = Mathf.Min(amount, _drawer.Amount);
        _drawer.ConsumeSilently(amount);
    }

    public void Save()
    {
    }

    public Vector3 GetPosition() => _drawer.Position;

    // public string GetPrefabName() => _drawer.Prefab ?? _drawer.ZNVName;
    public string GetPrefabName() => _drawer.ZNVName;

    public Inventory GetInventory() => null;

    public static mkzDrawer Create(MkzItemDrawers_API.mkzDrawer drawer) => new(drawer);
}