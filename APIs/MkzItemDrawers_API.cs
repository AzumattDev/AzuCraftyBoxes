using Object = UnityEngine.Object;

namespace AzuCraftyBoxes.APIs;

public static class MkzItemDrawers_API
{
    private static readonly bool _isInstalled;
    internal static Type? _drawerType; // DrawerContainer
    private static readonly FieldInfo? _fiItem; // private ItemDrop.ItemData _item
    private static readonly FieldInfo? _fiQuantity; // private int _quantity

    internal static readonly MethodInfo? MiUpdateInventory; // void UpdateInventory()
    internal static readonly MethodInfo? MiOnContainerChanged; // void OnContainerChanged()
    internal static readonly MethodInfo? MiClear; // void Clear()

    public class mkzDrawer
    {
        private readonly Component _drawer;
        private readonly ZNetView _nview;

        internal mkzDrawer(Component drawer)
        {
            _drawer = drawer;
            _nview = drawer.GetComponent<ZNetView>();
        }

        public GameObject gameObject => _drawer.gameObject;
        public ZNetView m_nview => _nview;
        public Vector3 Position => _drawer.transform.position;

        public string ZNVName => _nview.gameObject.name;

        public string Prefab
        {
            get
            {
                if (_fiItem == null) return null;
                ItemDrop.ItemData? itemData = _fiItem.GetValue(_drawer) as ItemDrop.ItemData;
                GameObject? go = itemData?.m_dropPrefab;
                return go ? go.name : null;
            }
        }

        public int Amount
        {
            get
            {
                if (_fiQuantity == null) return 0;
                return (int)_fiQuantity.GetValue(_drawer);
            }
        }

        public bool Accepts(string prefab)
        {
            string current = Prefab;
            return string.IsNullOrEmpty(current) || string.Equals(current, prefab, StringComparison.Ordinal);
        }

        public void Add(string prefab, int amount)
        {
            if (amount <= 0 || string.IsNullOrEmpty(prefab)) return;
            if (!_nview) return;
            _nview.ClaimOwnership();
            _nview.InvokeRPC("AddItem", prefab, amount);
        }

        public void Remove(int amount)
        {
            if (amount <= 0) return;
            if (!_nview) return;
            _nview.ClaimOwnership();
            _nview.InvokeRPC("Drop", amount);
        }

        // Original behavior: drop to world then give to player.
        public void DropToPlayer(int amount)
        {
            if (amount <= 0) return;
            if (!_nview || !_nview.IsValid()) return;

            _nview.ClaimOwnership();
            _nview.InvokeRPC("Drop", amount);
        }


        // Bypass original Drop RPC behaviour and directly decrement.
        internal void ConsumeSilently(int amount)
        {
            if (amount <= 0) return;
            if (!_nview || !_nview.IsValid()) return;
            if (_fiQuantity == null) return;

            _nview.ClaimOwnership();

            int current = Amount;
            if (current <= 0) return;

            int toRemove = Math.Min(amount, current);
            if (toRemove <= 0) return;

            int newQty = current - toRemove;

            if (newQty <= 0 && MiClear != null)
            {
                MiClear.Invoke(_drawer, null);
            }
            else
            {
                _fiQuantity.SetValue(_drawer, newQty);

                if (MiUpdateInventory != null && _fiItem?.GetValue(_drawer) != null)
                {
                    MiUpdateInventory.Invoke(_drawer, null);
                }
            }

            if (MiOnContainerChanged != null)
            {
                MiOnContainerChanged.Invoke(_drawer, null);
            }
        }
    }

    public static List<mkzDrawer> AllDrawers => !_isInstalled ? [] : FindAllRuntimeDrawers();

    public static List<mkzDrawer> AllDrawersInRange(Vector3 pos, float range) => !_isInstalled ? [] : FindAllRuntimeDrawers().Where(d => Vector3.Distance(d.Position, pos) <= range).ToList();

    static MkzItemDrawers_API()
    {
        _drawerType = Type.GetType("DrawerContainer, itemdrawers");

        if (_drawerType == null)
        {
            _isInstalled = false;
            return;
        }

        _fiItem = _drawerType.GetField("_item", BindingFlags.Instance | BindingFlags.NonPublic);
        _fiQuantity = _drawerType.GetField("_quantity", BindingFlags.Instance | BindingFlags.NonPublic);
        
        MiUpdateInventory = _drawerType.GetMethod("UpdateInventory", BindingFlags.Instance | BindingFlags.NonPublic);
        MiOnContainerChanged = _drawerType.GetMethod("OnContainerChanged", BindingFlags.Instance | BindingFlags.NonPublic);
        MiClear = _drawerType.GetMethod("Clear", BindingFlags.Instance | BindingFlags.NonPublic);

        _isInstalled = _fiItem != null && _fiQuantity != null;
    }

    private static List<mkzDrawer> FindAllRuntimeDrawers()
    {
        List<mkzDrawer> list = [];

        Object[]? components = Object.FindObjectsByType(_drawerType, FindObjectsSortMode.None);
        foreach (Object c in components)
        {
            if (c is not Component comp) continue;
            ZNetView? znv = comp.GetComponent<ZNetView>();
            if (!znv || !znv.IsValid()) continue;

            list.Add(new mkzDrawer(comp));
        }

        return list;
    }
}