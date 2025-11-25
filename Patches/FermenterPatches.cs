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
        {
            return;
        }

        // If the inventory is equal to the player's inventory but the result is null, then search the containers
        if (inventory != Player.m_localPlayer.GetInventory() || __result != null) return;
        List<IContainer> nearbyContainers = Boxes.QueryFrame.Get(__instance, AzuCraftyBoxesPlugin.mRange.Value);

        foreach (IContainer c in nearbyContainers)
        {
            if (c.GetInventory() == null) continue;
            Inventory? containerInventory = c.GetInventory();
            if (containerInventory == inventory)
            {
                continue;
            }

            foreach (Fermenter.ItemConversion itemConversion in __instance.m_conversion)
            {
                if (!c.ContainsItem(itemConversion.m_from.m_itemData.m_shared.m_name, 1, out int result)) continue;
                result = Boxes.CheckAndDecrement(result);
                if (result <= 0) continue;
                if (!Boxes.CanItemBePulled(c.GetPrefabName(), itemConversion.m_from.name))
                {
                    continue;
                }

                ItemDrop.ItemData cookableItem = containerInventory.GetItem(itemConversion.m_from.m_itemData.m_shared.m_name);
                if (cookableItem == null) continue;
                __result = cookableItem;
                if (__instance.GetStatus() != Fermenter.Status.Empty || !__instance.IsItemAllowed(cookableItem) || !containerInventory.RemoveOneItem(cookableItem))
                {
                    return;
                }

                __instance.m_nview.InvokeRPC("RPC_AddItem", cookableItem.m_dropPrefab.name);
            }
        }
    }
}

public static class OverrideHoverTextFermenter
{
    public static bool ShouldReturn(Fermenter __instance)
    {
        if (MiscFunctions.ShouldPrevent())
        {
            return true;
        }

        if (AzuCraftyBoxesPlugin.fillAllModKey.Value.MainKey is KeyCode.None)
        {
            return true;
        }

        if (Player.m_localPlayer is null)
        {
            return true;
        }

        // Check if the player is looking at an object
        return !Player.m_localPlayer.m_hovering || Player.m_localPlayer.m_hovering.GetComponentInParent<Fermenter>() != __instance;
    }

    private static readonly Dictionary<int, (float time, List<string> items)> _cache = new();

    internal static void UpdateAddSwitchHoverText(Fermenter __instance, ref string result)
    {
        bool free = __instance.GetStatus() == Fermenter.Status.Empty;


        if (!free) return;

        int id = __instance.GetInstanceID();
        float now = Time.unscaledTime;

        if (!_cache.TryGetValue(id, out (float time, List<string> items) entry) || now - entry.time > 0.25f)
        {
            List<string> items = [];
            foreach (Fermenter.ItemConversion conversion in __instance.m_conversion)
            {
                string sharedName = conversion.m_from.m_itemData.m_shared.m_name;
                string prefabName = conversion.m_from.name;
                if (!HasItemInInventoryOrContainers(prefabName, sharedName, __instance)) continue;
                if (!MiscFunctions.CheckItemDropIntegrity(conversion.m_from)) continue;
                if (!MiscFunctions.GetItemPrefabFromGameObject(conversion.m_from, conversion.m_from.gameObject)) continue;
                if (!Boxes.CanItemBePulled(Utils.GetPrefabName(__instance.gameObject), Utils.GetPrefabName(conversion.m_from.m_itemData.m_dropPrefab))) continue;
                items.Add($"{sharedName}");
            }

            entry = (now, items);
            _cache[id] = entry;
        }

        if (entry.items.Count > 0)
        {
            result += Localization.instance.Localize($"\n[<b><color=yellow>{AzuCraftyBoxesPlugin.fillAllModKey.Value}</color> + <color=yellow>$KEY_Use</color></b>] {__instance.m_addSwitch.m_onHover} {string.Join(", ", entry.items)} from Inventory & Nearby Containers");
        }
    }

    private static bool HasItemInInventoryOrContainers(string prefabName, string itemName, Fermenter fermenter)
    {
        Inventory? inv = Player.m_localPlayer?.m_inventory;
        if (inv != null && inv.CountItems(itemName) > 0)
            return true;

        List<IContainer> nearbyContainers = Boxes.QueryFrame.Get(fermenter, AzuCraftyBoxesPlugin.mRange.Value);

        foreach (IContainer c in nearbyContainers)
        {
            if (!Boxes.CanItemBePulled(prefabName, c.GetPrefabName()))
                continue;

            if (c.ContainsItem(itemName, 1, out _))
                return true; // found one, don't care about counts
        }

        return false;
    }
}