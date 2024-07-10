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
        if (AzuCraftyBoxesPlugin.ModEnabled.Value == AzuCraftyBoxesPlugin.Toggle.Off || piece == null || piece.m_name == "$piece_repair")
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
        List<IContainer> containers = Boxes.GetNearbyContainers(Player.m_localPlayer, AzuCraftyBoxesPlugin.mRange.Value);

        _cachedItemCount = piece.m_resources.Select<Piece.Requirement, int>(
            resource =>
            {
                string itemName = resource.m_resItem.m_itemData.m_shared.m_name;

                int playerItemCount = Player.m_localPlayer.GetInventory().CountItems(itemName);
                int containerItemCount = containers.Sum(c => c.ContainsItem(itemName, 1, out int result) ? result : 0);
#if DEBUG
                AzuCraftyBoxesPlugin.AzuCraftyBoxesLogger.LogDebug($"Found {playerItemCount} {itemName} in player inventory and {containerItemCount} in containers, returning {(playerItemCount + containerItemCount) / resource.m_amount}");
#endif
                return (playerItemCount + containerItemCount) / resource.m_amount;
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