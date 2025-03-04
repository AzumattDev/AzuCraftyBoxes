using AzuCraftyBoxes.IContainers;
using AzuCraftyBoxes.Util.Functions;
using TMPro;

namespace AzuCraftyBoxes.Patches;

[HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.SetupRequirementList))]
static class InventoryGuiCollectRequirements
{
    public static Dictionary<Piece.Requirement, int> actualAmounts = new();

    private static void Prefix()
    {
        actualAmounts.Clear();
    }
}

// This patch updates the requirement UI element based on aggregated counts
[HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.SetupRequirement))]
static class InventoryGuiSetupRequirementPatch
{
    static void Postfix(InventoryGui __instance, Transform elementRoot, Piece.Requirement req, Player player, bool craft, int quality, int craftMultiplier = 1)
    {
        if (MiscFunctions.ShouldPrevent() || req == null || req.m_resItem == null || req.m_resItem.m_itemData == null || req.m_resItem.m_itemData.m_shared == null)
        {
            return;
        }

        // Make sure the dropPrefab is set correctly.
        req.m_resItem.m_itemData.m_dropPrefab = req.m_resItem.gameObject;
        if (req.m_resItem.m_itemData.m_dropPrefab == null)
            return;

        // Use the canonical key (prefab name preferred, fallback to shared name)
        string canonicalKey = ItemKeyHelper.GetCanonicalKey(req.m_resItem.m_itemData);
        // Count player's inventory items using the shared name (or you could implement a similar canonical lookup for player inventory)
        int invAmount = player.GetInventory().CountItems(req.m_resItem.m_itemData.m_shared.m_name);

        // Get the UI Text for the resource amount
        TextMeshProUGUI text = elementRoot.transform.Find("res_amount").GetComponent<TextMeshProUGUI>();
        if (text == null)
            return;
        if (!int.TryParse(text.text, out int required))
        {
            required = req.GetAmount(quality) * craftMultiplier;
        }

        if (required <= 0)
            return;

        // If the player’s own inventory count is less than required, check nearby containers.
        if (invAmount < required)
        {
            List<IContainer> nearbyContainers = Boxes.GetNearbyContainers(Player.m_localPlayer, AzuCraftyBoxesPlugin.mRange.Value);
            // (Optional) Make sure we have an item prefab to validate config rules.
            GameObject itemPrefab = ObjectDB.instance.GetItemPrefab(req.m_resItem.GetPrefabName(req.m_resItem.gameObject.name));
            if (itemPrefab == null)
                return;
            // Ensure the dropPrefab is set from the prefab if needed.
            req.m_resItem.m_itemData.m_dropPrefab = itemPrefab;

            // For each container, try to get the aggregated count using the cache if possible.
            foreach (IContainer container in nearbyContainers)
            {
                try
                {
                    if (Boxes.CanItemBePulled(container.GetPrefabName(), canonicalKey))
                    {
                        int containerCount = 0;
                        // Check if the container is a vanilla container (i.e. of type Container) and registered in our cache manager.
                        if (container is Container vanillaContainer && ContainerInventoryCacheManager.Instance != null)
                        {
                            containerCount = ContainerInventoryCacheManager.Instance.GetAggregatedItemCount(vanillaContainer, canonicalKey);
                        }
                        else
                        {
                            // Fallback: use the container's ContainsItem API (using the shared name)
                            container.ContainsItem(req.m_resItem.m_itemData.m_shared.m_name, 1, out int result);
                            containerCount = result;
                        }

                        // Apply any decrement logic (like leaving one item behind)
                        containerCount = Boxes.CheckAndDecrement(containerCount);
                        invAmount += containerCount;
                    }
                }
                catch (Exception ex)
                {
                    // Optionally log exceptions.
                    AzuCraftyBoxesPlugin.AzuCraftyBoxesLogger.LogIfReleaseAndDebugEnable($"Error aggregating container: {ex.Message}");
                }
            }

            // If aggregated count now meets required amount, update the text color and record the requirement.
            if (invAmount >= required)
            {
                text.color = (Mathf.Sin(Time.time * 10f) > 0f)
                    ? AzuCraftyBoxesPlugin.flashColor.Value
                    : AzuCraftyBoxesPlugin.unFlashColor.Value;
                InventoryGuiCollectRequirements.actualAmounts[req] = required;
            }
        }

        // Finally, update the UI text with either a formatted resource string or the required number.
        text.text = AzuCraftyBoxesPlugin.resourceString.Value.Trim().Length > 0
            ? string.Format(AzuCraftyBoxesPlugin.resourceString.Value, invAmount, required)
            : required.ToString();
    }
}