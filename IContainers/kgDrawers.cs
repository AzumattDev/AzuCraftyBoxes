using AzuCraftyBoxes.APIs;
using AzuCraftyBoxes.Util.Functions;

namespace AzuCraftyBoxes.IContainers;

public class kgDrawer(ItemDrawers_API.Drawer _drawer) : IContainer
{
    private string? Name => ObjectDB.instance.GetItemPrefab(_drawer.Prefab)?.GetComponent<ItemDrop>()?.m_itemData.m_shared.m_name;

    public int ProcessContainerInventory(string reqName, int totalAmount, int totalRequirement)
    {
        if (Name != reqName) return totalAmount;
        int thisAmount = Mathf.Min(_drawer.Amount, totalRequirement - totalAmount);
        _drawer.Remove(thisAmount);
        return totalAmount + thisAmount;
    }

    public int ItemCount(string name) => Name == name ? _drawer.Amount : 0;

    public void RemoveItem(string name, int amount)
    {
        amount = Mathf.Min(amount, _drawer.Amount);
        _drawer.Remove(amount);
    }

    public void RemoveItem(string prefab, string sharedName, int amount)
    {
        amount = Mathf.Min(amount, _drawer.Amount);
        _drawer.Remove(amount);
    }

    public void Save()
    {
    }

    public Vector3 GetPosition() => _drawer.Position;
    public string GetPrefabName() => _drawer.ZNVName;
    public Inventory GetInventory() => null;


    public static kgDrawer Create(ItemDrawers_API.Drawer drawer) => new(drawer);
}