/*using System.Collections.Generic;
using AzuCraftyBoxes.IContainers;
using AzuCraftyBoxes.Util.Functions;
using HarmonyLib;
using UnityEngine;

namespace AzuCraftyBoxes.Patches;

[HarmonyPatch(typeof(Recipe), nameof(Recipe.GetAmount))]
static class RecipeGetAmountPatch
{
    static void Postfix(Recipe __instance, int quality, out int need, out ItemDrop.ItemData singleReqItem)
    {
        need = 0;
        singleReqItem = null;
        if (__instance.m_requireOnlyOneIngredient)
        {
            List<IContainer> nearbyContainers = Boxes.GetNearbyContainers(Player.m_localPlayer, AzuCraftyBoxesPlugin.mRange.Value);
            foreach (IContainer nearbyContainer in nearbyContainers)
            {
                singleReqItem = GetFirstRequiredItem(nearbyContainer, __instance, quality, out need, out int extraAmount);
                AzuCraftyBoxesPlugin.AzuCraftyBoxesLogger.LogDebug($"Found {singleReqItem.m_shared.m_name} in player inventory, returning {singleReqItem.m_shared.m_name}");
            }
        }
    }

    public static ItemDrop.ItemData GetFirstRequiredItem(IContainer container, Recipe recipe, int qualityLevel, out int amount, out int extraAmount)
    {
        foreach (Piece.Requirement resource in recipe.m_resources)
        {
            if (resource.m_resItem)
            {
                int amount1 = resource.GetAmount(qualityLevel);
                for (int quality = 0; quality <= resource.m_resItem.m_itemData.m_shared.m_maxQuality; ++quality)
                {
                    if (container.ContainsItem(resource.m_resItem.name, 1, out int count) >= amount1)
                    {
                        
                    }
                    if (inventory.CountItems(resource.m_resItem.m_itemData.m_shared.m_name, quality) >= amount1)
                    {
                        amount = amount1;
                        extraAmount = resource.m_extraAmountOnlyOneIngredient;
                        return inventory.GetItem(resource.m_resItem.m_itemData.m_shared.m_name, quality);
                    }
                }
            }
        }

        amount = 0;
        extraAmount = 0;
        return null;
    }
}*/