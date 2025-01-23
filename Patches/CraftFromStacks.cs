/*namespace AzuCraftyBoxes.Patches
{
    public class CraftFromStacks
    {
        // Precompute hash codes for vanillaStacks to avoid hashing at runtime
        private static readonly HashSet<int> vanillaStackHashes =
        [
            "wood_stack".GetStableHashCode(),
            "wood_fine_stack".GetStableHashCode(),
            "wood_core_stack".GetStableHashCode(),
            "wood_yggdrasil_stack".GetStableHashCode(),
            "blackwood_stack".GetStableHashCode(),
            "stone_pile".GetStableHashCode(),
            "coal_pile".GetStableHashCode(),
            "blackmarble_pile".GetStableHashCode(),
            "grausten_pile".GetStableHashCode(),
            "skull_pile".GetStableHashCode(),
            "treasure_stack".GetStableHashCode(),
            "bone_stack".GetStableHashCode()
        ];

        [HarmonyPatch(typeof(ZNetScene), nameof(ZNetScene.Awake))]
        static class AddVisualStackElementZNetSceneAwakePatch
        {
            [HarmonyPriority(Priority.Last)]
            static void Postfix(ZNetScene __instance)
            {
                // Loop through named prefabs and add the component if it's in vanillaStacks
                foreach (KeyValuePair<int, GameObject> prefabEntry in __instance.m_namedPrefabs)
                {
                    if (!vanillaStackHashes.Contains(prefabEntry.Key)) continue;
                    GameObject prefab = prefabEntry.Value;
                    if (prefab == null || prefab.GetComponent<VisualStack>() != null) continue;
                    VisualStack visualStack = prefab.AddComponent<VisualStack>();
                    ZLog.Log($"Added VisualStack to prefab: {prefab.name}");
                    foreach (MeshRenderer meshRenderer in prefab.GetComponentsInChildren<MeshRenderer>()) {
                        if (!meshRenderer.gameObject.GetComponent<Collider>()) {
                            meshRenderer.gameObject.AddComponent<BoxCollider>();
                        }

                        visualStack.stackMeshes.Add(meshRenderer.transform);
                    }
                }
            }
        }
    }

    public class VisualStack : MonoBehaviour
    {
        public static List<VisualStack> s_instances = [];
        public List<Transform> stackMeshes = [];
        private Piece piece;
        private ZNetView m_nview;
        private int defaultStacks = 50;

        private void Start()
        {
            piece = GetComponentInParent<Piece>();
            if (!piece) return;
            m_nview = piece.m_nview;
            if (!m_nview || !m_nview.IsValid()) return;
            m_nview.Register(nameof(RPC_UpdateVisuals), RPC_UpdateVisuals);
            s_instances.Add(this);
            UpdateVisuals();
        }

        private void UpdateVisuals()
        {
            SetVisualsActive(GetStackPercentage());
        }

        public void SetVisualsActive(float fillPercentage)
        {
            float fillCount = Mathf.Ceil(fillPercentage / 100f * stackMeshes.Count);

            for (int i = 0; i < stackMeshes.Count; i++)
            {
                bool active = i == 0 || i < fillCount;
                stackMeshes[i].gameObject.SetActive(active);
            }
        }

        public int GetStack()
        {
            return m_nview && m_nview.IsValid() ? m_nview.GetZDO().GetInt(ZDOVars.s_value, this.defaultStacks) : this.defaultStacks;
        }

        public float GetStackPercentage()
        {
            return (float)Mathf.Max(GetStack(), 0) / (float)this.defaultStacks;
        }

        public void DecreaseStack(int amount)
        {
            if (!m_nview.IsOwner()) return;

            int currentStack = GetStack();
            int newStack = Mathf.Max(currentStack - amount, 0);
            m_nview.GetZDO().Set(ZDOVars.s_value, newStack);

            UpdateVisuals();
            m_nview.InvokeRPC(ZNetView.Everybody, "RPC_UpdateVisuals");
        }

        private void RPC_UpdateVisuals(long sender)
        {
            UpdateVisuals();
        }

        public static bool CraftFromStack(Piece piece, int requiredAmount)
        {
            if (piece == null) return false;

            VisualStack visualStack = piece.GetComponent<VisualStack>();
            if (visualStack == null) return false;

            int currentStack = visualStack.GetStack();
            if (currentStack < requiredAmount) return false;

            visualStack.DecreaseStack(requiredAmount);
            return true;
        }
    }
}*/