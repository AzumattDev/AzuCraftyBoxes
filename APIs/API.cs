using AzuCraftyBoxes.IContainers;
#if !API
using AzuCraftyBoxes.APIs;
using AzuCraftyBoxes.Util.Functions;
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

#if !API
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
#endif

    public static List<IContainer> GetNearbyContainers<T>(T gameObject, float rangeToUse) where T : Component
    {
#if API
        return new List<IContainer>();
#else
        return Boxes.QueryFrame.Get(gameObject, rangeToUse);
#endif
    }

    public static Dictionary<string, List<string>> GetExcludedPrefabsForAllContainers()
    {
#if API
        return new Dictionary<string, List<string>>();
#else
        return Boxes.GetExcludedPrefabsForAllContainers();
#endif
    }

    public static bool CanItemBePulled(string container, string prefab)
    {
#if API
        return false;
#else
        return Boxes.CanItemBePulled(container, prefab);
#endif
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
