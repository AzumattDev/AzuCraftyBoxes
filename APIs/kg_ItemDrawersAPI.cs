namespace AzuCraftyBoxes.APIs;

public static class ItemDrawers_API
{
    private static readonly bool _IsInstalled;
    private static readonly MethodInfo MI_GetAllDrawers;

    public class Drawer(ZNetView znv)
    {
        public string Prefab = znv.m_zdo.GetString("Prefab");
        public int Amount = znv.m_zdo.GetInt("Amount");
        public int Quality = znv.m_zdo.GetInt("Quality", 1);
        public void Remove(int amount) { znv.ClaimOwnership(); znv.InvokeRPC("ForceRemove", amount); }
        public void Withdraw(int amount) => znv.InvokeRPC("WithdrawItem_Request", amount);
        public void Add(int amount, int quality) => znv.InvokeRPC("AddItem_Request", Prefab, amount, quality);
        public Vector3 Position => znv.transform.position;
        public GameObject gameObject => znv.gameObject;
        public string ZNVName => znv.gameObject.name;
        public ZNetView m_nview => znv;
    }

    public static List<Drawer> AllDrawers => _IsInstalled
        ? ((List<ZNetView>)MI_GetAllDrawers.Invoke(null, null)).Select(znv => new Drawer(znv)).ToList()
        : [];

    public static List<Drawer> AllDrawersInRange(Vector3 pos, float range) => _IsInstalled
        ? ((List<ZNetView>)MI_GetAllDrawers.Invoke(null, null)).Where(znv => Vector3.Distance(znv.transform.position, pos) <= range).Select(znv => new Drawer(znv)).ToList()
        : [];

    static ItemDrawers_API()
    {
        if (Type.GetType("API.ClientSideV2, kg_ItemDrawers") is not { } drawersAPI)
        {
            _IsInstalled = false;
            return;
        }

        _IsInstalled = true;
        MI_GetAllDrawers = drawersAPI.GetMethod("AllDrawers", BindingFlags.Public | BindingFlags.Static);
    }
}