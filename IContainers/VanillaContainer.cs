using AzuCraftyBoxes.Util.Functions;

namespace AzuCraftyBoxes.IContainers;

public class VanillaContainer(Container _container, ContainerCache cache) : IContainer
{
    public int ProcessContainerInventory(string reqName, int totalAmount, int totalRequirement)
    {
        Inventory? cInventory = _container.GetInventory();
        if (cInventory == null) return totalAmount;

        int thisAmount = Mathf.Min(cInventory.CountItems(reqName), totalRequirement - totalAmount);
        if (thisAmount == 0) return totalAmount;

        AzuCraftyBoxesPlugin.AzuCraftyBoxesLogger.LogIfReleaseAndDebugEnable($"(ConsumeResourcesPatch) Container at {_container.transform.position} has {cInventory.CountItems(reqName)} {reqName}");

        List<ItemDrop.ItemData>? items = cInventory.GetAllItems();
        for (int i = 0; i < items.Count; ++i)
        {
            ItemDrop.ItemData? item = items[i];
            if (item?.m_shared?.m_name != reqName) continue;

            int stackAmount = Mathf.Min(item.m_stack, totalRequirement - totalAmount);
            if (stackAmount == item.m_stack)
            {
                bool removed = cInventory.RemoveItem(i);
                --i;
            }
            else
            {
                item.m_stack -= stackAmount;
            }

            totalAmount += stackAmount;
            if (totalAmount >= totalRequirement)
                break;
        }

        cInventory.Changed(); // triggers Container.OnContainerChanged -> Save -> then my RebuildCache

        if (totalAmount >= totalRequirement)
        {
            AzuCraftyBoxesPlugin.AzuCraftyBoxesLogger.LogIfReleaseAndDebugEnable($"(ConsumeResourcesPatch) Consumed enough {reqName}");
        }

        return totalAmount;
    }

    public int ItemCount(string name) => cache.ItemCounts.GetValueOrDefault(name, 0);

    public void RemoveItem(string name, int amount)
    {
        _container.GetInventory()?.RemoveItem(name, amount);
        _container.Save();
        _container.GetInventory()?.Changed(); // keep cache fresh
    }

    public void Save()
    {
        _container.m_inventory?.Changed();
    }

    public Vector3 GetPosition() => _container.transform.position;
    public string GetPrefabName() => Utils.GetPrefabName(_container.gameObject);
    public Inventory GetInventory() => _container.GetInventory();

    public static VanillaContainer Create(Container container, ContainerCache cache) => new(container, cache);

    public static VanillaContainer Create(Container container)
    {
        if (!container) throw new ArgumentNullException(nameof(container));

        Boxes.AddContainer(container);

        var cache = Boxes.GetCache(container);
        if (cache != null) return new VanillaContainer(container, cache);
        cache = new ContainerCache
        {
            Container = container,
            LastPos = container.transform.position
        };

        Boxes.RebuildCache(container);
        cache = Boxes.GetCache(container)!;

        return new VanillaContainer(container, cache);
    }
}