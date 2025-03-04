using System;
using System.Collections.Generic;
using System.Linq;
using AzuCraftyBoxes.IContainers;
using AzuCraftyBoxes.Util.Functions;
using HarmonyLib;
using UnityEngine;

namespace AzuCraftyBoxes.Patches;

[HarmonyPatch(typeof(Fermenter), nameof(Fermenter.GetHoverText))]
[HarmonyBefore("org.bepinex.plugins.conversionsizespeed")]
static class FermenterGetHoverTextPatch
{
    static void Postfix(Fermenter __instance, ref string __result)
    {
        if (OverrideHoverTextFermenter.ShouldReturn(__instance))
        {
            return;
        }

        OverrideHoverTextFermenter.UpdateAddSwitchHoverText(__instance, ref __result);
    }
}

[HarmonyPatch(typeof(Fermenter), nameof(Fermenter.FindCookableItem))]
static class SearchContainersAsWell
{
    static void Postfix(Fermenter __instance, Inventory inventory, ref ItemDrop.ItemData __result)
    {
        if (MiscFunctions.ShouldPrevent())
            return;

        // Only search if the inventory equals the player's inventory and nothing was found.
        if (inventory != Player.m_localPlayer.GetInventory() || __result != null)
            return;

        List<IContainer> nearbyContainers = Boxes.GetNearbyContainers(__instance, AzuCraftyBoxesPlugin.mRange.Value);

        foreach (IContainer container in nearbyContainers)
        {
            Inventory contInv = container.GetInventory();
            if (contInv == null || contInv == inventory)
                continue;

            // Loop over each conversion defined on the fermenter.
            foreach (Fermenter.ItemConversion conversion in __instance.m_conversion)
            {
                // Use the canonical key for this item.
                string canonicalKey = ItemKeyHelper.GetCanonicalKey(conversion.m_from.m_itemData);
                // Check whether the container has at least one unit of the item.
                if (!container.ContainsItem(conversion.m_from.m_itemData.m_shared.m_name, 1, out int count))
                    continue;

                // Check config permissions (using prefab names).
                string fromPrefabName = conversion.m_from.name;
                if (!Boxes.CanItemBePulled(container.GetPrefabName(), fromPrefabName))
                    continue;

                // Try to retrieve the item from the container inventory using its shared name.
                ItemDrop.ItemData cookableItem = contInv.GetItem(conversion.m_from.m_itemData.m_shared.m_name);
                if (cookableItem != null)
                {
                    __result = cookableItem;
                    // Only remove the item if the fermenter is empty, the item is allowed, and removal succeeds.
                    if (__instance.GetStatus() != Fermenter.Status.Empty ||
                        !__instance.IsItemAllowed(cookableItem) ||
                        !contInv.RemoveOneItem(cookableItem))
                    {
                        return;
                    }

                    __instance.m_nview.InvokeRPC("RPC_AddItem", cookableItem.m_dropPrefab.name);
                    return;
                }
            }
        }
    }
}

public static class OverrideHoverTextFermenter
{
    public static bool ShouldReturn(Fermenter __instance)
    {
        if (MiscFunctions.ShouldPrevent())
            return true;
        if (AzuCraftyBoxesPlugin.fillAllModKey.Value.MainKey == KeyCode.None)
            return true;
        if (Player.m_localPlayer == null)
            return true;
        // Only proceed if the player's hovering object is this fermenter.
        if (!Player.m_localPlayer.m_hovering ||
            Player.m_localPlayer.m_hovering.GetComponentInParent<Fermenter>() != __instance)
            return true;

        return false;
    }

    internal static void UpdateAddSwitchHoverText(Fermenter __instance, ref string result)
    {
        bool isEmpty = __instance.GetStatus() == Fermenter.Status.Empty;
        List<string> itemsToSuggest = new List<string>();

        // Loop over each conversion in the fermenter.
        foreach (Fermenter.ItemConversion conversion in __instance.m_conversion)
        {
            if (!isEmpty)
                break;

            // Use canonical key for consistency.
            string canonicalKey = ItemKeyHelper.GetCanonicalKey(conversion.m_from.m_itemData);
            // Get aggregated count from player's inventory.
            int totalCount = Player.m_localPlayer?.m_inventory.CountItems(conversion.m_from.m_itemData.m_shared.m_name) ?? 0;
            // Also query nearby containers.
            List<IContainer> nearbyContainers = Boxes.GetNearbyContainers(__instance, AzuCraftyBoxesPlugin.mRange.Value);
            foreach (IContainer container in nearbyContainers)
            {
                if (!Boxes.CanItemBePulled(Utils.GetPrefabName(__instance.gameObject), Utils.GetPrefabName(conversion.m_from.m_itemData.m_dropPrefab)))
                    continue;

                // For vanilla containers, query our cache manager.
                int contCount = 0;
                if (container is Container vanilla && ContainerInventoryCacheManager.Instance != null)
                {
                    contCount = ContainerInventoryCacheManager.Instance.GetAggregatedItemCount(vanilla, canonicalKey);
                }
                else
                {
                    container.ContainsItem(conversion.m_from.m_itemData.m_shared.m_name, 1, out int res);
                    contCount = res;
                }

                totalCount += contCount;
            }

            // Only if we have items available do we add them to our suggestion list.
            if (isEmpty && totalCount > 0)
            {
                // Use the shared name as display text (you could also localize here if needed).
                itemsToSuggest.Add(conversion.m_from.m_itemData.m_shared.m_name);
            }
        }

        if (itemsToSuggest.Count > 0)
        {
            string keyInfo = $"[<b><color=yellow>{AzuCraftyBoxesPlugin.fillAllModKey.Value}</color> + <color=yellow>$KEY_Use</color></b>]";
            string suggestion = string.Join(", ", itemsToSuggest);
            result += Localization.instance.Localize($"\n{keyInfo} {__instance.m_addSwitch.m_onHover} {suggestion} from Inventory & Nearby Containers");
        }
    }

    // Updated helper method to aggregate counts using canonical keys.
    private static int GetItemCountInInventoryAndContainers(string prefabName, string itemName, Fermenter fermenterInstance)
    {
        int count = Player.m_localPlayer?.m_inventory.CountItems(itemName) ?? 0;
        List<IContainer> nearbyContainers = Boxes.GetNearbyContainers(fermenterInstance, AzuCraftyBoxesPlugin.mRange.Value);

        // Use canonical key for lookups.
        string canonicalKey = itemName.ToLowerInvariant(); // Or call ItemKeyHelper if you have access to the full item.
        foreach (IContainer container in nearbyContainers)
        {
            if (Boxes.CanItemBePulled(prefabName, container.GetPrefabName()))
            {
                int containerCount = 0;
                if (container is Container vanilla && ContainerInventoryCacheManager.Instance != null)
                {
                    containerCount = ContainerInventoryCacheManager.Instance.GetAggregatedItemCount(vanilla, canonicalKey);
                }
                else
                {
                    container.ContainsItem(itemName, 1, out int res);
                    containerCount = res;
                }

                count += containerCount;
            }
        }

        return count;
    }
}