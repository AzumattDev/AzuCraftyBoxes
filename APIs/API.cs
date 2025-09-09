using AzuCraftyBoxes.APIs;
using AzuCraftyBoxes.IContainers;
using AzuCraftyBoxes.Util.Functions;
#if ! API
#endif

namespace AzuCraftyBoxes;

[PublicAPI]
public class API
{
    public static bool IsLoaded()
    {
#if API
		return false;
#else
        return true;
#endif
    }


    public static Type GetIContainerType()
    {
        return typeof(IContainer);
    }

    public static Type GetVanillaContainerType()
    {
        return typeof(VanillaContainer);
    }

    public static Type GetKgDrawerType()
    {
        return typeof(kgDrawer);
    }

    public static Type GetItemDrawersAPIType()
    {
        return typeof(ItemDrawers_API);
    }

    public static Type GetBoxesUtilFunctionsType()
    {
        return typeof(Boxes);
    }

    public static IContainer CreateContainer(string type, params object[] args)
    {
        // Factory method to create container instances
        // 'type' could be "Vanilla", "kgDrawer", etc.
        switch (type)
        {
            case "Vanilla":
                return VanillaContainer.Create(args[0] as Container);
            case "kgDrawer":
                return kgDrawer.Create(args[0] as ItemDrawers_API.Drawer);
            default:
                throw new ArgumentException($"Unknown container type: {type}");
        }
    }

    public static void AddContainer(Container container)
    {
        Boxes.AddContainer(container);
    }

    public static void RemoveContainer(Container container)
    {
        Boxes.RemoveContainer(container);
    }

    public static List<IContainer> GetNearbyContainers<T>(T gameObject, float rangeToUse) where T : Component
    {
        return Boxes.QueryFrame.Get(gameObject, rangeToUse);
    }

    public static Dictionary<string, List<string>> GetExcludedPrefabsForAllContainers()
    {
        return Boxes.GetExcludedPrefabsForAllContainers();
    }

    public static bool CanItemBePulled(string container, string prefab)
    {
        return Boxes.CanItemBePulled(container, prefab);
    }
    
    public static int CountItemInContainer(IContainer container, string itemName)
    {
        if (container.ContainsItem(itemName, 1, out int count))
        {
            return count;
        }
        return 0;
    }
    
    public static bool ContainsItem(IContainer container, string itemName, int amount)
    {
        return container.ContainsItem(itemName, amount, out _);
    }
}