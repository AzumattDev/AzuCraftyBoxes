using System.Collections.Generic;
using System.Linq;
using AzuCraftyBoxes.IContainers;
using AzuCraftyBoxes.Util.Functions;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AzuCraftyBoxes.Patches;

[HarmonyPatch(typeof(Hud), nameof(Hud.SetupPieceInfo))]
public class HUDPatches
{
    private static float _updateInterval = 3f; // Update every 3 seconds
    private static float _lastUpdate;

    private static void Postfix(Piece piece, TMP_Text ___m_buildSelection)
    {
        if (AzuCraftyBoxesPlugin.ModEnabled.Value == AzuCraftyBoxesPlugin.Toggle.Off || piece == null ||
            piece.m_name == "$piece_repair")
            return;

        float currentTime = Time.time;
        if (currentTime - _lastUpdate < _updateInterval)
            return;

        _lastUpdate = currentTime;

        List<IContainer> containers = Boxes.GetNearbyContainers(Player.m_localPlayer, AzuCraftyBoxesPlugin.mRange.Value);

        int num = piece.m_resources.Select<Piece.Requirement, int>(
            resource =>
            {
                string itemName = resource.m_resItem.m_itemData.m_shared.m_name;
                string itemPrefabName = resource.m_resItem.name;
                
                int playerItemCount = Player.m_localPlayer.GetInventory().CountItems(itemName);
                int containerItemCount = containers.Sum(c => c.ContainsItem(itemPrefabName, 1, out int result) ? result : 0);

                return (playerItemCount + containerItemCount) / resource.m_amount;
            }).Concat(new[] { int.MaxValue }).Min();

        string color = num > 0 ? "green" : "red";
        ___m_buildSelection.text = Localization.instance.Localize(piece.m_name) + $" (<color={color}>" + (num == int.MaxValue ? "∞" : num.ToString()) + "</color>)";
    }
}


[HarmonyPatch(typeof(Hud),nameof(Hud.Awake))]
static class HudAwakePatch
{
    static void Postfix(Hud __instance)
    {
        __instance.m_hoverName.autoSizeTextContainer = true;
    }
}