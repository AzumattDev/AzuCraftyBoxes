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
    private static string _canHex, _cantHex;
    private static int _lastColorVersion;

    [HarmonyPriority(Priority.Last)]
    private static void Postfix(Hud __instance, Piece piece, TMP_Text ___m_buildSelection)
    {
        if (MiscFunctions.ShouldPrevent() || piece == null || piece.m_name == "$piece_repair") return;

        // (Re)compute hex when config changes (super cheap anyway)
        int colorVersion = AzuCraftyBoxesPlugin.canbuildDisplayColor.Value.GetHashCode() ^ AzuCraftyBoxesPlugin.cannotbuildDisplayColor.Value.GetHashCode();
        if (colorVersion != _lastColorVersion)
        {
            _lastColorVersion = colorVersion;
            _canHex = ColorUtility.ToHtmlStringRGBA(AzuCraftyBoxesPlugin.canbuildDisplayColor.Value);
            _cantHex = ColorUtility.ToHtmlStringRGBA(AzuCraftyBoxesPlugin.cannotbuildDisplayColor.Value);
        }

        float now = Time.time;
        if (now - _lastUpdate >= UpdateInterval)
        {
            _lastUpdate = now;

            // Seed bank once for this frame/range
            var containers = Boxes.QueryFrame.Get(Player.m_localPlayer, AzuCraftyBoxesPlugin.mRange.Value);
            UiItemBank.Begin(containers);

            // Compute min crafts across requirements (no LINQ)
            int crafts = int.MaxValue;
            var reqs = piece.m_resources;
            for (int i = 0; i < reqs.Length; ++i)
            {
                var r = reqs[i];
                if (r == null || !r.m_resItem || r.m_amount <= 0 || r.m_resItem.m_itemData?.m_shared == null) continue;

                string name = r.m_resItem.m_itemData.m_shared.m_name;
                int have = UiItemBank.GetTotalAnyQuality(name);
                int canDo = have / r.m_amount;
                if (canDo < crafts) crafts = canDo;
                if (crafts == 0) break;
            }

            _cachedItemCount = crafts == int.MaxValue ? int.MaxValue : crafts;
        }

        string color = _cachedItemCount > 0 ? _canHex : _cantHex;
        var pieceName = Localization.instance.Localize(piece.m_name);
        ___m_buildSelection.text = _cachedItemCount == int.MaxValue
            ? $"{pieceName} (<color=#{color}>∞</color>)"
            : $"{pieceName} (<color=#{color}>{_cachedItemCount}</color>)";
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