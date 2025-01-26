/*using ItemDataManager;

namespace AzuCraftyBoxes.IContainers;

public class GemBagContainer : IContainer
{
    private readonly object _bagObject; // The actual SocketBag or InventoryBag instance
    private readonly ItemDrop.ItemData _gemBag; // The item itself
    private readonly MethodInfo _readInventory; // Reflection for "Inventory ReadInventory()"
    private readonly MethodInfo _saveInventory; // Reflection for "Inventory SaveSocketsInventory()"
    private readonly MethodInfo _save; // Reflection for "void Save()"
    private readonly PropertyInfo _itemProperty; // Reflection for "ItemDrop.ItemData Item" (if it exists)
    public const string GemBagPrefabName = "JC_Gem_Bag";

    /// <summary>
    /// Constructs a GemBagContainer from the unknown 'bagObject' (which should be a SocketBag or InventoryBag).
    /// </summary>
    public GemBagContainer(ItemDrop.ItemData gemBag, object bagObject)
    {
        if (bagObject == null)
            throw new ArgumentNullException(nameof(bagObject));

        if (gemBag == null)
            throw new ArgumentNullException(nameof(gemBag));

        _gemBag = gemBag;
        _bagObject = bagObject;
        Type bagType = bagObject.GetType();

        _readInventory = bagType.GetMethod("ReadInventory", BindingFlags.Public | BindingFlags.Instance);
        if (_readInventory == null)
            throw new MissingMethodException($"{bagType.FullName} is missing a public 'ReadInventory()' method.");

        _saveInventory = bagType.GetMethod("SaveSocketsInventory");
        if (_saveInventory == null)
            throw new MissingMethodException($"{bagType.FullName} is missing a public 'SaveSocketsInventory()' method.");

        _save = bagType.GetMethod("Save", BindingFlags.Public | BindingFlags.Instance);
        if (_save == null)
            throw new MissingMethodException($"{bagType.FullName} is missing a public 'Save()' method.");

        _itemProperty = bagType.GetProperty("Item", BindingFlags.Public | BindingFlags.Instance);
    }

    private Inventory GetInventoryInvoke()
    {
        // readInventory returns an Inventory object
        object result = _readInventory.Invoke(_bagObject, null);
        return result as Inventory;
    }

    private void SaveBag()
    {
        _save.Invoke(_bagObject, null);
        _saveInventory.Invoke(_bagObject, [GetInventory()]);
    }

    // -------------------------------------------
    // IContainer Implementation
    // -------------------------------------------

    public int ProcessContainerInventory(string reqName, int totalAmount, int totalRequirement)
    {
        Inventory? cInventory = GetInventory(out bool isOpenBag);
        if (cInventory == null) return totalAmount;

        int itemsNeeded = totalRequirement - totalAmount;
        int inBag = cInventory.CountItems(reqName);
        int thisAmount = Mathf.Min(inBag, itemsNeeded);

        AzuCraftyBoxesPlugin.AzuCraftyBoxesLogger.LogWarning($"[GemBagContainer] Found {inBag} '{reqName}' in gem bag. Need up to {itemsNeeded}.");

        if (thisAmount <= 0)
        {
            return totalAmount;
        }

        // Remove items from ephemeral Inventory
        for (int i = 0; i < cInventory.GetAllItems().Count; ++i)
        {
            ItemDrop.ItemData? item = cInventory.GetItem(i);
            if (item == null || item.m_shared?.m_name != reqName)
                continue;

            int removeCount = Mathf.Min(item.m_stack, itemsNeeded);
            if (removeCount == item.m_stack)
            {
                cInventory.RemoveItem(i);
                --i;
            }
            else
            {
                item.m_stack -= removeCount;
            }

            totalAmount += removeCount;
            itemsNeeded -= removeCount;

            AzuCraftyBoxesPlugin.AzuCraftyBoxesLogger.LogIfReleaseAndDebugEnable($"[GemBagContainer] Removed {removeCount}, total used: {totalAmount}/{totalRequirement}");

            if (totalAmount >= totalRequirement)
            {
                break;
            }
        }

        if (!isOpenBag)
        {
            try
            {
                Save(cInventory);
            }
            catch
            {
                AzuCraftyBoxesPlugin.AzuCraftyBoxesLogger.LogWarning("[GemBagContainer] Failed to save bag after processing items.");
            }
        }
        else
        {
            cInventory.Save(new ZPackage());
            cInventory.Changed();
        }

        return totalAmount;
    }

    public int ItemCount(string name)
    {
        Inventory? cInventory = GetInventory();
        if (cInventory == null) return 0;
        int result = cInventory.GetAllItems().Where(it => it.m_shared?.m_name == name).Sum(it => it.m_stack);
        return result;
    }

    public void RemoveItem(string name, int amount)
    {
        Inventory? cInventory = GetInventory(out bool isOpenBag);
        if (cInventory == null) return;

        int toRemove = amount;
        AzuCraftyBoxesPlugin.AzuCraftyBoxesLogger.LogIfReleaseAndDebugEnable($"[GemBagContainer] Removing {amount} '{name}' from gem bag.");
        for (int i = 0; i < cInventory.GetAllItems().Count && toRemove > 0; ++i)
        {
            ItemDrop.ItemData? item = cInventory.GetItem(i);
            if (item == null || item.m_shared?.m_name != name)
                continue;
            AzuCraftyBoxesPlugin.AzuCraftyBoxesLogger.LogIfReleaseAndDebugEnable($"[GemBagContainer] Found {item.m_stack} '{name}' in gem bag.");
            int removeNow = Mathf.Min(item.m_stack, toRemove);
            item.m_stack -= removeNow;
            toRemove -= removeNow;

            if (item.m_stack <= 0)
            {
                AzuCraftyBoxesPlugin.AzuCraftyBoxesLogger.LogIfReleaseAndDebugEnable($"[GemBagContainer] Removing item '{name}' from gem bag.");
                cInventory.RemoveItem(i);
                --i;
            }
        }

        if (!isOpenBag)
        {
            try
            {
                Save(cInventory);
            }
            catch
            {
                AzuCraftyBoxesPlugin.AzuCraftyBoxesLogger.LogWarning("[GemBagContainer] Failed to save bag after removing items.");
            }
        }
        else
        {
            cInventory.Save(new ZPackage());
            cInventory.Changed();
        }
    }

    public void RemoveItem(string prefab, string sharedName, int amount)
    {
        Inventory? cInventory = GetInventory(out bool isOpenBag);
        if (cInventory == null) return;

        int toRemove = amount;
        for (int i = 0; i < cInventory.GetAllItems().Count && toRemove > 0; ++i)
        {
            ItemDrop.ItemData? item = cInventory.GetItem(i);
            if (item == null || item.m_shared?.m_name != sharedName)
                continue;

            int removeNow = Mathf.Min(item.m_stack, toRemove);
            item.m_stack -= removeNow;
            toRemove -= removeNow;

            if (item.m_stack <= 0)
            {
                cInventory.RemoveItem(i);
                --i;
            }
        }

        if (!isOpenBag)
        {
            try
            {
                Save(cInventory);
            }
            catch
            {
                AzuCraftyBoxesPlugin.AzuCraftyBoxesLogger.LogWarning("[GemBagContainer] Failed to save bag after removing items.");
            }
        }
        else
        {
            cInventory.Save(new ZPackage());
            cInventory.Changed();
        }
    }

    public void Save(Inventory inv)
    {
        Save();
        _saveInventory.Invoke(_bagObject, [inv]);
    }

    public void Save()
    {
        SaveBag();
    }

    public Vector3 GetPosition()
    {
        return Player.m_localPlayer.transform.position;
    }

    public string GetPrefabName()
    {
        // If the underlying type has a property "Item" => "ItemDrop.ItemData" => "m_dropPrefab"
        if (_itemProperty == null) return GemBagPrefabName;
        object itemObj = _itemProperty.GetValue(_bagObject, null);
        if (itemObj == null) return GemBagPrefabName;
        // fetch "m_dropPrefab" from the ItemData
        FieldInfo? dropPrefabField = itemObj.GetType().GetField("m_dropPrefab", BindingFlags.Public | BindingFlags.Instance);
        if (dropPrefabField == null) return GemBagPrefabName;
        object dropPrefab = dropPrefabField.GetValue(itemObj);
        if (dropPrefab is GameObject go)
        {
            return go.name;
        }

        // Fallback if we cannot reflect it
        return GemBagPrefabName;
    }

    public Inventory? GetInventory() => GetInventory(out _);

    public Inventory? GetInventory(out bool isOpenBag)
    {
        isOpenBag = true;
        if (GetOpenEquipment() == _gemBag && GetOpenInventory() is { } openInv) return openInv;
        isOpenBag = false;
        Inventory inv = GetInventoryInvoke();
        if (inv != null) return inv;
        AzuCraftyBoxesPlugin.AzuCraftyBoxesLogger.LogWarning("[GemBagContainer] No inventory returned!");
        return null;
    }


    internal static object FindJewelcraftingBagObject(ItemDrop.ItemData gemBagItem)
    {
        if (gemBagItem == null) return null;

        // Try to retrieve the “Jewelcrafting” data block, similar to how I do with backpacks
        ForeignItemInfo? dataManager = gemBagItem.Data("org.bepinex.plugins.jewelcrafting");
        if (dataManager == null) return null;

        // Call .Get<object>() so it returns a fully closed type => no reflection error
        object bagObject = dataManager.Get<object>();
        if (bagObject == null) return null;

        string typeName = bagObject.GetType().FullName;
        if (typeName != null && (typeName.Contains("Jewelcrafting.SocketBag") || typeName.Contains("Jewelcrafting.InventoryBag")))
        {
            return bagObject;
        }

        // Otherwise no recognized bag
        return null;
    }

    private static Inventory? GetOpenInventory() => Type.GetType("Jewelcrafting.GemStones+AddFakeSocketsContainer, Jewelcrafting")?.GetField("openInventory", BindingFlags.Public | BindingFlags.Static)?.GetValue(null) as Inventory;
    private static ItemDrop.ItemData? GetOpenEquipment() => Type.GetType("Jewelcrafting.GemStones+AddFakeSocketsContainer, Jewelcrafting")?.GetField("openEquipment", BindingFlags.Public | BindingFlags.Static)?.GetValue(null) as ItemDrop.ItemData;
}*/