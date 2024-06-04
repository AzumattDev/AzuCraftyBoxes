/*using System;
using System.Collections.Generic;
using System.Linq;
using AzuCraftyBoxes.IContainers;
using AzuCraftyBoxes.Util.Functions;
using HarmonyLib;
using UnityEngine;

namespace AzuCraftyBoxes.Patches;

[HarmonyPatch(typeof(Fermenter), nameof(Fermenter.GetHoverText))]
[HarmonyBefore("org.bepinex.plugins.conversionsizespeed")]
static class SmelterUpdateHoverTextsPatch
{
    static void Postfix(Fermenter __instance)
    {
        if (OverrideHoverTextFermenter.ShouldReturn(__instance))
        {
            return;
        }

        if (__instance.m_addSwitch)
        {
            OverrideHoverTextFermenter.UpdateAddSwitchHoverText(__instance, ref __instance.m_addSwitch.m_hoverText);
        }

        if (__instance.m_tapSwitch)
        {
            OverrideHoverTextFermenter.UpdateTapSwitchHoverText(__instance, ref __instance.m_tapSwitch.m_hoverText);
        }
    }
}

public static class OverrideHoverTextFermenter
{
    public static bool ShouldReturn(Fermenter __instance)
    {
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

    internal static void UpdateTapSwitchHoverText(Fermenter __instance, ref string result)
    {
        string? content = __instance.GetContent();
        if (string.IsNullOrEmpty(content))
            result += "";
        else
        {
            Fermenter.ItemConversion itemConversion = __instance.GetItemConversion(content);
            if (itemConversion != null)
            {
                string contentName = itemConversion.m_from.m_itemData.m_shared.m_name;
                string contentPrefabName = Utils.GetPrefabName(itemConversion.m_from.name);
                int inInv = GetItemCountInInventoryAndContainers(contentPrefabName, contentName, __instance);
                int fuelAmount = __instance.GetStatus() switch
                {
                    Fermenter.Status.Empty or Fermenter.Status.Exposed => 0,
                    Fermenter.Status.Fermenting => 1,
                    Fermenter.Status.Ready => 0,
                    _ => 0
                };

                int amount = Math.Min(1 - Mathf.CeilToInt(fuelAmount), inInv);
                if (amount <= 0) return;
                if (Boxes.CanItemBePulled(Utils.GetPrefabName(__instance.gameObject), Utils.GetPrefabName(itemConversion.m_from.m_itemData.m_dropPrefab)))
                {
                    result += Localization.instance.Localize($"\n[<b><color=yellow>{AzuCraftyBoxesPlugin.fillAllModKey.Value}</color> + <color=yellow>$KEY_Use</color></b>] $piece_Fermenter_add {contentName} {amount} from Inventory & Nearby Containers");
                }
            }
        }
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
            if (free && inInv > 0)
            {
                if (Boxes.CanItemBePulled(Utils.GetPrefabName(__instance.gameObject), Utils.GetPrefabName(conversion.m_from.m_itemData.m_dropPrefab)))
                {
                    items.Add($"{conversion.m_from.m_itemData.m_shared.m_name}");
                }
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
        List<IContainer> nearbyContainers = Boxes.GetNearbyContainers(FermenterInstance, AzuCraftyBoxesPlugin.mRange.Value);

        foreach (IContainer c in nearbyContainers)
        {
            if (Boxes.CanItemBePulled(prefabName, c.GetPrefabName()))
            {
                c.ContainsItem(itemName, 1, out int result);
                inInv += result;
            }
        }

        return inInv;
    }
}*/