using AzuCraftyBoxes.IContainers;
using AzuCraftyBoxes.Util.Functions;
using TMPro;

namespace AzuCraftyBoxes.Patches;

[HarmonyPatch(typeof(Hud), nameof(Hud.SetupPieceInfo))]
public class HUDPatches
{
    private const float UpdateInterval = 0.5f;
    private static float _lastUpdate;
    private static int _cachedItemCount = int.MaxValue;

    [HarmonyPriority(Priority.Last)]
    private static void Postfix(Hud __instance, Piece piece, TMP_Text ___m_buildSelection)
    {
        if (MiscFunctions.ShouldPrevent() || piece == null || piece.m_name == "$piece_repair")
            return;

        float currentTime = Time.time;
        if (currentTime - _lastUpdate >= UpdateInterval)
        {
            _lastUpdate = currentTime;
            UpdateItemCount(piece);
        }

        string color = _cachedItemCount > 0 ? ColorUtility.ToHtmlStringRGBA(AzuCraftyBoxesPlugin.canbuildDisplayColor.Value) : ColorUtility.ToHtmlStringRGBA(AzuCraftyBoxesPlugin.cannotbuildDisplayColor.Value);
        ___m_buildSelection.text = Localization.instance.Localize(piece.m_name) + $" (<color=#{color}>" + (_cachedItemCount == int.MaxValue ? "∞" : _cachedItemCount.ToString()) + "</color>)";
    }

    internal static void UpdateItemCount(Piece piece)
    {
        // Get the list of nearby containers using your Boxes system.
        List<IContainer> containers = Boxes.GetNearbyContainers(Player.m_localPlayer, AzuCraftyBoxesPlugin.mRange.Value);

        // For each resource in the piece, use the canonical key for lookups.
        _cachedItemCount = piece.m_resources.Select(resource =>
        {
            // Use the canonical key: prefer prefab name (normalized) then shared m_name.
            string canonicalKey = ItemKeyHelper.GetCanonicalKey(resource.m_resItem.m_itemData);

            // Count how many of this item the player has.
            int playerItemCount = Player.m_localPlayer.GetInventory().CountItems(resource.m_resItem.m_itemData.m_shared.m_name);

            // Sum counts from containers.
            int containerItemCount = 0;
            foreach (IContainer container in containers)
            {
                Inventory inv = container.GetInventory();
                if (inv == null)
                    continue;

                // If the container is a vanilla container registered in our cache manager, use its aggregated value.
                if (container is Container vanillaContainer && ContainerInventoryCacheManager.Instance != null)
                {
                    containerItemCount += ContainerInventoryCacheManager.Instance.GetAggregatedItemCount(vanillaContainer, canonicalKey);
                }
                else
                {
                    // Otherwise, fall back to using the container API.
                    container.ContainsItem(resource.m_resItem.m_itemData.m_shared.m_name, 1, out int result);
                    containerItemCount += result;
                }
            }

            // Optionally apply any decrement logic.
            containerItemCount = Boxes.CheckAndDecrement(containerItemCount);

            // Calculate how many complete resource sets you have.
            return resource.m_amount > 0 ? (playerItemCount + containerItemCount) / resource.m_amount : int.MaxValue;
        }).Concat(new[] { int.MaxValue }).Min();
    }
}

[HarmonyPatch(typeof(Hud), nameof(Hud.Awake))]
static class HudAwakePatch
{
    static void Postfix(Hud __instance)
    {
        __instance.m_hoverName.autoSizeTextContainer = true;
    }
}