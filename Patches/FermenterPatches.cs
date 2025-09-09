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
        if (inventory == Player.m_localPlayer.GetInventory() && __result == null)
        {
            List<IContainer> nearbyContainers = Boxes.QueryFrame.Get(__instance, AzuCraftyBoxesPlugin.mRange.Value);

            foreach (IContainer c in nearbyContainers)
            {
                if (c.GetInventory() == null) continue;
                var containerInventory = c.GetInventory();
                if (containerInventory == inventory)
                {
                    continue;
                }

                foreach (Fermenter.ItemConversion itemConversion in __instance.m_conversion)
                {
                    if (!c.ContainsItem(itemConversion.m_from.m_itemData.m_shared.m_name, 1, out int result)) continue;
                    result = Boxes.CheckAndDecrement(result);
                    if(result <= 0) continue;
                    if (!Boxes.CanItemBePulled(c.GetPrefabName(), itemConversion.m_from.name))
                    {
                        continue;
                    }

                    ItemDrop.ItemData cookableItem = containerInventory.GetItem(itemConversion.m_from.m_itemData.m_shared.m_name);
                    if (cookableItem != null)
                    {
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
        if (!Player.m_localPlayer.m_hovering || Player.m_localPlayer.m_hovering.GetComponentInParent<Fermenter>() != __instance)
        {
            return true;
        }

        return false;
    }

    internal static void UpdateAddSwitchHoverText(Fermenter __instance, ref string result)
    {
        bool free = __instance.GetStatus() == Fermenter.Status.Empty;
        List<string> items = [];

        foreach (Fermenter.ItemConversion conversion in __instance.m_conversion)
        {
            if (!free)
            {
                break;
            }

            int inInv = GetItemCountInInventoryAndContainers(conversion.m_from.name, conversion.m_from.m_itemData.m_shared.m_name, __instance);
            if (!MiscFunctions.CheckItemDropIntegrity(conversion.m_from)) continue;
            if (MiscFunctions.GetItemPrefabFromGameObject(conversion.m_from, conversion.m_from.gameObject) == null) continue;
            if (!free || inInv <= 0) continue;
            if (Boxes.CanItemBePulled(Utils.GetPrefabName(__instance.gameObject), Utils.GetPrefabName(conversion.m_from.m_itemData.m_dropPrefab)))
            {
                items.Add($"{conversion.m_from.m_itemData.m_shared.m_name}");
            }
        }

        if (items.Count > 0)
        {
            result += Localization.instance.Localize($"\n[<b><color=yellow>{AzuCraftyBoxesPlugin.fillAllModKey.Value}</color> + <color=yellow>$KEY_Use</color></b>] {__instance.m_addSwitch.m_onHover} {string.Join(", ", items)} from Inventory & Nearby Containers");
        }
    }

    private static int GetItemCountInInventoryAndContainers(string prefabName, string itemName, Fermenter FermenterInstance)
    {
        int inInv = Player.m_localPlayer?.m_inventory.CountItems(itemName) ?? 0;
        List<IContainer> nearbyContainers = Boxes.QueryFrame.Get(FermenterInstance, AzuCraftyBoxesPlugin.mRange.Value);

        foreach (IContainer c in nearbyContainers)
        {
            if (Boxes.CanItemBePulled(prefabName, c.GetPrefabName()))
            {
                c.ContainsItem(itemName, 1, out int result);
                result = Boxes.CheckAndDecrement(result);
                inInv += result;
            }
        }

        return inInv;
    }
}