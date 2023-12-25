using System;
using System.Collections;
using System.Collections.Generic;
using AzuCraftyBoxes.IContainers;
using AzuCraftyBoxes.Util.Functions;
using HarmonyLib;
using UnityEngine;

namespace AzuCraftyBoxes.Patches;

[HarmonyPatch(typeof(Fireplace), nameof(Fireplace.Interact))]
static class FireplaceInteractPatch
{
    static bool Prefix(Fireplace __instance, Humanoid user, bool hold, ref bool __result, ZNetView ___m_nview)
    {
        __result = true;
        bool pullAll =
            Input.GetKey(AzuCraftyBoxesPlugin.fillAllModKey.Value
                .MainKey); // Used to be fillAllModKey.Value.IsPressed(); something is wrong with KeyboardShortcuts always returning false
        Inventory inventory = user.GetInventory();

        if (!MiscFunctions.AllowByKey() || hold || inventory == null ||
            (inventory.HaveItem(__instance.m_fuelItem.m_itemData.m_shared.m_name) && !pullAll))
            return true;

        if (!___m_nview.HasOwner())
        {
            ___m_nview.ClaimOwnership();
        }


        if (pullAll && inventory.HaveItem(__instance.m_fuelItem.m_itemData.m_shared.m_name))
        {
            int amount =
                (int)Mathf.Min(__instance.m_maxFuel - Mathf.CeilToInt(___m_nview.GetZDO().GetFloat("fuel")),
                    inventory.CountItems(__instance.m_fuelItem.m_itemData.m_shared.m_name));
            inventory.RemoveItem(__instance.m_fuelItem.m_itemData.m_shared.m_name, amount);
            inventory.Changed();
            for (int i = 0; i < amount; ++i)
                ___m_nview.InvokeRPC("AddFuel");

            user.Message(MessageHud.MessageType.Center,
                Localization.instance.Localize("$msg_fireadding", __instance.m_fuelItem.m_itemData.m_shared.m_name));

            __result = false;
        }

        if (inventory.HaveItem(__instance.m_fuelItem.m_itemData.m_shared.m_name) ||
            !(Mathf.CeilToInt(___m_nview.GetZDO().GetFloat("fuel")) < __instance.m_maxFuel)) return __result;
        {
            List<IContainer> nearbyContainers = Boxes.GetNearbyContainers(__instance, AzuCraftyBoxesPlugin.mRange.Value);

            string fuelPrefabName = __instance.m_fuelItem.name;
            foreach (IContainer c in nearbyContainers)
            {
                if (!c.ContainsItem(fuelPrefabName, 1, out int result) || !(Mathf.CeilToInt(___m_nview.GetZDO().GetFloat("fuel")) < __instance.m_maxFuel)) continue;
                if (!Boxes.CanItemBePulled(Utils.GetPrefabName(__instance.gameObject), fuelPrefabName))
                {
                    AzuCraftyBoxesPlugin.AzuCraftyBoxesLogger.LogDebug($"(FireplaceInteractPatch) Container at {c.GetPosition()} has {result} {fuelPrefabName} but it's forbidden by config");
                    continue;
                }

                int amount = pullAll ? (int)Mathf.Min(__instance.m_maxFuel - Mathf.CeilToInt(___m_nview.GetZDO().GetFloat("fuel")), result) : 1;
                AzuCraftyBoxesPlugin.AzuCraftyBoxesLogger.LogDebug($"Pull ALL is {pullAll}");
                AzuCraftyBoxesPlugin.AzuCraftyBoxesLogger.LogDebug($"(FireplaceInteractPatch) Container at {c.GetPosition()} has {result} {fuelPrefabName}, taking {amount}");

                c.RemoveItem(fuelPrefabName, amount);
                c.Save();

                if (__result)
                    user.Message(MessageHud.MessageType.Center, Localization.instance.Localize("$msg_fireadding", __instance.m_fuelItem.m_itemData.m_shared.m_name));

                for (int i = 0; i < amount; ++i)
                    ___m_nview.InvokeRPC("AddFuel");

                __result = false;

                if (!pullAll || Mathf.CeilToInt(___m_nview.GetZDO().GetFloat("fuel")) >= __instance.m_maxFuel)
                    return false;
            }
        }

        return __result;
    }
}

[HarmonyPatch(typeof(Fireplace), nameof(Fireplace.GetHoverText))]
static class FireplaceGetHoverTextPatch
{
    static void Postfix(Fireplace __instance, ref string __result)
    {
        if (AzuCraftyBoxesPlugin.fillAllModKey.Value.MainKey is KeyCode.None)
        {
            return;
        }

        double free = __instance.m_maxFuel - (double)Mathf.CeilToInt(__instance.m_nview.GetZDO().GetFloat("fuel"));
        List<string> items = new();

        if (free <= 0)
        {
            return;
        }

        int inInv = Player.m_localPlayer?.m_inventory.CountItems(__instance.m_fuelItem.m_itemData.m_shared.m_name) ?? 0;
        List<IContainer> nearbyContainers = Boxes.GetNearbyContainers(__instance, AzuCraftyBoxesPlugin.mRange.Value);
        int inContainers = 0;
        __instance.m_fuelItem.m_itemData.m_dropPrefab = __instance.m_fuelItem.gameObject;
        string fuelPrefabName = __instance.m_fuelItem.name;
        foreach (IContainer c in nearbyContainers)
        {
            if (!c.ContainsItem(fuelPrefabName, 1, out int result)) continue;
            /*AzuCraftyBoxesPlugin.AzuCraftyBoxesLogger.LogDebug("Found " + newItem + " of " +
                                                               __instance.m_fuelItem.m_itemData.m_shared.m_name +
                                                               " in " + c.name + "");*/
            if (Boxes.CanItemBePulled(Utils.GetPrefabName(__instance.gameObject), fuelPrefabName));
            {
                inContainers += result;
            }
        }

        if (inInv > 0)
        {
            items.Add($"{inInv} in inventory");
        }

        if (inContainers > 0)
        {
            items.Add($"{inContainers} in nearby containers");
        }

        if (free - inInv - inContainers > 0 && free < __instance.m_maxFuel)
        {
            items.Add($"{free - inInv - inContainers} needed to fill");
        }

        if (items.Count > 0)
        {
            __result += Localization.instance.Localize(
                    $"\n[<b><color=yellow>{AzuCraftyBoxesPlugin.fillAllModKey.Value}</color> + <color=yellow>$KEY_Use</color></b>] {string.Join(" and ", items)}");
        }
    }
}
