using System;
using System.Collections;
using System.Collections.Generic;
using AzuCraftyBoxes.Util.Functions;
using HarmonyLib;
using UnityEngine;

namespace AzuCraftyBoxes.Patches;


[HarmonyPatch(typeof(CookingStation), nameof(CookingStation.OnAddFuelSwitch))]
static class CookingStationOnAddFuelSwitchPatch
{
    static bool Prefix(CookingStation __instance, ref bool __result, Humanoid user, ItemDrop.ItemData item,
        ZNetView ___m_nview)
    {
        AzuCraftyBoxesPlugin.AzuCraftyBoxesLogger.LogDebug($"(CookingStationOnAddFuelSwitchPatch) Looking for fuel");

        if (AzuCraftyBoxesPlugin.ModEnabled.Value == AzuCraftyBoxesPlugin.Toggle.Off || !MiscFunctions.AllowByKey() || item != null ||
            __instance.GetFuel() > __instance.m_maxFuel - 1 ||
            user.GetInventory().HaveItem(__instance.m_fuelItem.m_itemData.m_shared.m_name))
            return true;

        AzuCraftyBoxesPlugin.AzuCraftyBoxesLogger.LogDebug(
            $"(CookingStationOnAddFuelSwitchPatch) Missing fuel in player inventory");


        List<Container> nearbyContainers = Boxes.GetNearbyContainers(__instance, AzuCraftyBoxesPlugin.mRange.Value);

        foreach (Container c in nearbyContainers)
        {
            ItemDrop.ItemData fuelItem = c.GetInventory().GetItem(__instance.m_fuelItem.m_itemData.m_shared.m_name);
            if (fuelItem == null) continue;
            GameObject itemPrefab = ObjectDB.instance.GetItemPrefab(__instance.m_fuelItem.GetPrefabName(__instance.m_fuelItem.gameObject.name));
            fuelItem.m_dropPrefab = itemPrefab;
            string itemPrefabName = Utils.GetPrefabName(fuelItem.m_dropPrefab);
            if (!Boxes.CanItemBePulled(Utils.GetPrefabName(__instance.gameObject),
                    itemPrefabName))
            {
                AzuCraftyBoxesPlugin.AzuCraftyBoxesLogger.LogDebug(
                    $"(CookingStationOnAddFuelSwitchPatch) Container at {c.transform.position} has {fuelItem.m_stack} {fuelItem.m_dropPrefab.name} but it's forbidden by config");
                continue;
            }

            AzuCraftyBoxesPlugin.AzuCraftyBoxesLogger.LogDebug(
                $"(CookingStationOnAddFuelSwitchPatch) Container at {c.transform.position} has {fuelItem.m_stack} {fuelItem.m_dropPrefab.name}, taking one");
            c.GetInventory().RemoveItem(__instance.m_fuelItem.m_itemData.m_shared.m_name, 1);
            c.Save();
            //typeof(Inventory).GetMethod("Changed", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(c.GetInventory(), new object[] { });
            user.Message(MessageHud.MessageType.Center,
                "$msg_added " + __instance.m_fuelItem.m_itemData.m_shared.m_name);
            ___m_nview.InvokeRPC("AddFuel", Array.Empty<object>());
            __result = true;
            return false;
        }

        return true;
    }
}

[HarmonyPatch(typeof(CookingStation), nameof(CookingStation.FindCookableItem))]
static class CookingStationFindCookableItemPatch
{
    static void Postfix(CookingStation __instance, ref ItemDrop.ItemData __result)
    {
        AzuCraftyBoxesPlugin.AzuCraftyBoxesLogger.LogDebug($"(CookingStationFindCookableItemPatch) Looking for cookable");

        if (AzuCraftyBoxesPlugin.ModEnabled.Value == AzuCraftyBoxesPlugin.Toggle.Off || !MiscFunctions.AllowByKey() || __result != null ||
            (__instance.m_requireFire && !__instance.IsFireLit() || __instance.GetFreeSlot() == -1))
            return;

        AzuCraftyBoxesPlugin.AzuCraftyBoxesLogger.LogDebug(
            $"(CookingStationFindCookableItemPatch) Missing cookable in player inventory");


        List<Container> nearbyContainers = Boxes.GetNearbyContainers(__instance, AzuCraftyBoxesPlugin.mRange.Value);

        foreach (CookingStation.ItemConversion itemConversion in __instance.m_conversion)
        {
            foreach (Container c in nearbyContainers)
            {
                ItemDrop.ItemData item = c.GetInventory().GetItem(itemConversion.m_from.m_itemData.m_shared.m_name);
                if (item == null) continue;
                GameObject itemPrefab = ObjectDB.instance.GetItemPrefab(itemConversion.m_from.GetPrefabName(itemConversion.m_from.gameObject.name));
                item.m_dropPrefab = itemPrefab;
                string itemPrefabName = Utils.GetPrefabName(item.m_dropPrefab);
                if (!Boxes.CanItemBePulled(Utils.GetPrefabName(__instance.gameObject),
                        itemPrefabName))
                {
                    AzuCraftyBoxesPlugin.AzuCraftyBoxesLogger.LogDebug(
                        $"(CookingStationFindCookableItemPatch) Container at {c.transform.position} has {item.m_stack} {item.m_dropPrefab.name} but it's forbidden by config");
                    continue;
                }

                AzuCraftyBoxesPlugin.AzuCraftyBoxesLogger.LogDebug(
                    $"(CookingStationFindCookableItemPatch) Container at {c.transform.position} has {item.m_stack} {item.m_dropPrefab.name}, taking one");
                __result = item;
                c.GetInventory().RemoveItem(itemConversion.m_from.m_itemData.m_shared.m_name, 1);
                c.Save();
                //typeof(Inventory).GetMethod("Changed", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(c.GetInventory(), new object[] { });
                return;
            }
        }
    }
}