using System;
using System.Collections.Generic;
using System.Linq;
using AzuCraftyBoxes.IContainers;
using AzuCraftyBoxes.Util.Functions;
using HarmonyLib;
using UnityEngine;

namespace AzuCraftyBoxes.Patches;

/*[HarmonyPatch(typeof(Smelter),nameof(Smelter.UpdateHoverTexts))]
[HarmonyBefore("org.bepinex.plugins.conversionsizespeed")]
static class SmelterUpdateHoverTextsPatch
{
    static void Postfix(Smelter __instance)
    {
        if(OverrideHoverText.ShouldReturn(__instance))
        {
            return;
        }
        if (__instance.m_addOreSwitch)
        {
            OverrideHoverText.UpdateAddOreSwitchHoverText(__instance, ref __instance.m_addOreSwitch.m_hoverText);
        }

        if(__instance.m_addWoodSwitch)
        {
            OverrideHoverText.UpdateAddWoodSwitchHoverText(__instance, ref __instance.m_addWoodSwitch.m_hoverText);
        }
    }
}*/

[HarmonyPatch(typeof(Smelter), nameof(Smelter.OnHoverAddOre))]
[HarmonyBefore("org.bepinex.plugins.conversionsizespeed")]
static class SmelterOnHoverAddOrePatch
{
    static void Postfix(Smelter __instance, ref string __result)
    {
        if (OverrideHoverText.ShouldReturn(__instance))
        {
            return;
        }

        OverrideHoverText.UpdateAddOreSwitchHoverText(__instance, ref __result);
    }
}

[HarmonyPatch(typeof(Smelter), nameof(Smelter.OnHoverAddFuel))]
[HarmonyBefore("org.bepinex.plugins.conversionsizespeed")]
static class SmelterOnHoverAddFuelPatch
{
    static void Postfix(Smelter __instance, ref string __result)
    {
        if (OverrideHoverText.ShouldReturn(__instance))
        {
            return;
        }

        OverrideHoverText.UpdateAddWoodSwitchHoverText(__instance, ref __result);
    }
}

public static class OverrideHoverText
{
    public static bool ShouldReturn(Smelter __instance)
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
        if (!Player.m_localPlayer.m_hovering || Player.m_localPlayer.m_hovering.GetComponentInParent<Smelter>() != __instance)
        {
            return true;
        }

        return false;
    }

    internal static void UpdateAddWoodSwitchHoverText(Smelter __instance, ref string result)
    {
        int inInv = GetItemCountInInventoryAndContainers(__instance.m_fuelItem.name, __instance.m_fuelItem.m_itemData.m_shared.m_name, __instance);
        int amount = Math.Min(__instance.m_maxFuel - Mathf.CeilToInt(__instance.GetFuel()), inInv);
        __instance.m_fuelItem.m_itemData.m_dropPrefab = __instance.m_fuelItem.gameObject;
        if (amount > 0)
        {
            if (Boxes.CanItemBePulled(Utils.GetPrefabName(__instance.gameObject), Utils.GetPrefabName(__instance.m_fuelItem.m_itemData.m_dropPrefab)))
            {
                result += Localization.instance.Localize($"\n[<b><color=yellow>{AzuCraftyBoxesPlugin.fillAllModKey.Value}</color> + <color=yellow>$KEY_Use</color></b>] $piece_smelter_add {__instance.m_fuelItem.m_itemData.m_shared.m_name} {amount} from Inventory & Nearby Containers");
            }
        }
    }

    internal static void UpdateAddOreSwitchHoverText(Smelter __instance, ref string result)
    {
        int free = __instance.m_maxOre - __instance.GetQueueSize();
        List<string> items = new();

        foreach (Smelter.ItemConversion conversion in __instance.m_conversion)
        {
            if (free <= 0)
            {
                break;
            }

            int inInv = GetItemCountInInventoryAndContainers(conversion.m_from.name, conversion.m_from.m_itemData.m_shared.m_name, __instance);
            int count = Math.Min(free, inInv);
            free -= count;
            if (!MiscFunctions.CheckItemDropIntegrity(conversion.m_from)) continue;
            conversion.m_from.m_itemData.m_dropPrefab = conversion.m_from.gameObject;
            if (MiscFunctions.GetItemPrefabFromGameObject(conversion.m_from, conversion.m_from.gameObject) == null) continue;
            if (count > 0)
            {
                if (Boxes.CanItemBePulled(Utils.GetPrefabName(__instance.gameObject), Utils.GetPrefabName(conversion.m_from.m_itemData.m_dropPrefab)))
                {
                    items.Add($"{count} {conversion.m_from.m_itemData.m_shared.m_name}");
                }
            }
        }

        if (items.Count > 0)
        {
            result += Localization.instance.Localize($"\n[<b><color=yellow>{AzuCraftyBoxesPlugin.fillAllModKey.Value}</color> + <color=yellow>$KEY_Use</color></b>] {__instance.m_addOreTooltip} {string.Join(", ", items)} from Inventory & Nearby Containers");
        }
    }

    private static int GetItemCountInInventoryAndContainers(string prefabName, string itemName, Smelter smelterInstance)
    {
        int inInv = Player.m_localPlayer?.m_inventory.CountItems(itemName) ?? 0;
        List<IContainer> nearbyContainers = Boxes.GetNearbyContainers(smelterInstance, AzuCraftyBoxesPlugin.mRange.Value);

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
}

[HarmonyPatch(typeof(Smelter), nameof(Smelter.OnAddOre))]
static class SmelterOnAddOrePatch
{
    static bool Prefix(Smelter __instance, Humanoid user, ItemDrop.ItemData item, ZNetView ___m_nview,
        out KeyValuePair<ItemDrop.ItemData?, int> __state)
    {
        int ore = __instance.GetQueueSize();
        __state = new KeyValuePair<ItemDrop.ItemData?, int>(item, ore);
        /*bool pullAll =
            Input.GetKey(AzuCraftyBoxesPlugin.fillAllModKey.Value
                .MainKey);*/
        // Used to be fillAllModKey.Value.isPressed(); something is wrong with KeyboardShortcuts always returning false
        bool pullAll = AzuCraftyBoxesPlugin.fillAllModKey.Value.IsKeyHeld();
        if (AzuCraftyBoxesPlugin.ModEnabled.Value == AzuCraftyBoxesPlugin.Toggle.Off ||
            (!MiscFunctions.AllowByKey() && !pullAll) || item != null ||
            __instance.GetQueueSize() >= __instance.m_maxOre)
            return true;

        Inventory inventory = user.GetInventory();


        if (__instance.m_conversion.Any(itemConversion =>
            {
                string itemName = itemConversion?.m_from?.m_itemData?.m_shared?.m_name;
                return itemName != null && inventory.HaveItem(itemName) && !pullAll && Boxes.CanItemBePulled(Utils.GetPrefabName(__instance.gameObject), itemConversion.m_from.name);
            }))
        {
            return true;
        }


        Dictionary<string, int> added = new();

        List<IContainer> nearbyContainers = Boxes.GetNearbyContainers(user, AzuCraftyBoxesPlugin.mRange.Value);
        foreach (Smelter.ItemConversion itemConversion in __instance.m_conversion)
        {
            if (__instance.GetQueueSize() >= __instance.m_maxOre || (added.Any() && !pullAll))
                break;

            string name = itemConversion.m_from.m_itemData.m_shared.m_name;
            string prefabName = itemConversion.m_from.name;
            if (pullAll && inventory.HaveItem(name))
            {
                ItemDrop.ItemData newItem = inventory.GetItem(name);
                if (newItem == null) continue;
                try
                {
                    GameObject itemPrefab = ObjectDB.instance.GetItemPrefab(__instance.m_fuelItem.GetPrefabName(itemConversion.m_from.gameObject.name));

                    newItem.m_dropPrefab = itemPrefab;
                }
                catch (Exception e)
                {
                    // AzuCraftyBoxesPlugin.AzuCraftyBoxesLogger.LogError(e);
                }

                if (newItem.m_dropPrefab == null) continue;
                string itemPrefabName = Utils.GetPrefabName(newItem.m_dropPrefab);
                if (!Boxes.CanItemBePulled(Utils.GetPrefabName(__instance.gameObject), itemPrefabName))
                {
                    AzuCraftyBoxesPlugin.AzuCraftyBoxesLogger.LogDebug($"(SmelterOnAddOrePatch) debug log 1:  Container at {user.transform.position} has {newItem.m_stack} {newItem.m_dropPrefab.name} but it's forbidden by config");
                    continue;
                }

                int amount = pullAll
                    ? Mathf.Min(__instance.m_maxOre - __instance.GetQueueSize(), inventory.CountItems(name))
                    : 1;
                if (!added.ContainsKey(name))
                    added[name] = 0;
                added[name] += amount;

                inventory.RemoveItem(itemConversion.m_from.m_itemData.m_shared.m_name, amount);
                //typeof(Inventory).GetMethod("Changed", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(inventory, new object[] { });

                for (int i = 0; i < amount; ++i)
                    ___m_nview.InvokeRPC("RPC_AddOre", newItem.m_dropPrefab.name);

                user.Message(MessageHud.MessageType.TopLeft, $"$msg_added {amount} {name}");
                if (__instance.GetQueueSize() >= __instance.m_maxOre)
                    break;
            }

            if (Boxes.CanItemBePulled(Utils.GetPrefabName(__instance.gameObject), prefabName))
            {
                foreach (IContainer c in nearbyContainers)
                {
                    if (!c.ContainsItem(name, 1, out int result)) continue;
                    if (!Boxes.CanItemBePulled(c.GetPrefabName(), prefabName))
                    {
                        AzuCraftyBoxesPlugin.AzuCraftyBoxesLogger.LogDebug($"(SmelterOnAddOrePatch) Container at {c.GetPosition()} has {result} {prefabName} but it's forbidden by config");
                        continue;
                    }

                    int amount = pullAll ? Mathf.Min(__instance.m_maxOre - __instance.GetQueueSize(), result) : 1;

                    if (!added.ContainsKey(name))
                        added[name] = 0;
                    added[name] += amount;
                    AzuCraftyBoxesPlugin.AzuCraftyBoxesLogger.LogDebug($"Pull ALL is {pullAll}");
                    AzuCraftyBoxesPlugin.AzuCraftyBoxesLogger.LogDebug($"(SmelterOnAddOrePatch) Container at {c.GetPosition()} has {result} {prefabName}, taking {amount}");

                    c.RemoveItem(name, amount);
                    c.Save();

                    for (int i = 0; i < amount; ++i)
                        ___m_nview.InvokeRPC("RPC_AddOre", prefabName);

                    user.Message(MessageHud.MessageType.TopLeft, $"$msg_added {amount} {name}");

                    if (__instance.GetQueueSize() >= __instance.m_maxOre ||
                        !pullAll)
                        break;
                }
            }
        }

        if (!added.Any())
            user.Message(MessageHud.MessageType.Center, "$msg_noprocessableitems");
        else
        {
            List<string> outAdded = added.Select(kvp => $"$msg_added {kvp.Value} {kvp.Key}").ToList();

            user.Message(MessageHud.MessageType.Center, string.Join("\n", outAdded));
        }

        return false;
    }

    public static void Postfix(Smelter __instance, Switch sw, Humanoid user,
        KeyValuePair<ItemDrop.ItemData?, int> __state, bool __result)
    {
        if (AzuCraftyBoxesPlugin.fillAllModKey.Value.IsKeyHeld() && __result && __state.Key is null)
        {
            if (!__instance.m_nview.IsOwner())
            {
                if (__instance.m_nview.GetZDO() != null)
                {
                    int ore = __instance.GetQueueSize();
                    if (ore == __state.Value)
                    {
                        __instance.m_nview.GetZDO().Set(ZDOVars.s_queued, ore + 1);
                    }
                }
            }

            MessageHud originalMessageHud = MessageHud.m_instance;
            MessageHud.m_instance = null;
            try
            {
                __instance.OnAddOre(sw, user, null);
            }
            finally
            {
                MessageHud.m_instance = originalMessageHud;
            }
        }
    }
}

[HarmonyPatch(typeof(Smelter), nameof(Smelter.OnAddFuel))]
[HarmonyBefore("org.bepinex.plugins.conversionsizespeed")]
static class SmelterOnAddFuelPatch
{
    static bool Prefix(Smelter __instance, ref bool __result, ZNetView ___m_nview, Humanoid user, ItemDrop.ItemData item)
    {
        /*bool pullAll =
            Input.GetKey(AzuCraftyBoxesPlugin.fillAllModKey.Value
                .MainKey);*/
        // Used to be fillAllModKey.Value.IsPressed(); something is wrong with KeyboardShortcuts always returning false
        bool pullAll = AzuCraftyBoxesPlugin.fillAllModKey.Value.IsKeyHeld();
        Inventory inventory = user.GetInventory();
        if (AzuCraftyBoxesPlugin.ModEnabled.Value == AzuCraftyBoxesPlugin.Toggle.Off ||
            (!MiscFunctions.AllowByKey() && !pullAll) || item != null ||
            inventory == null ||
            ((inventory.HaveItem(__instance.m_fuelItem.m_itemData.m_shared.m_name) && !pullAll) && Boxes.CanItemBePulled(Utils.GetPrefabName(__instance.gameObject), __instance.m_fuelItem.name)))
            return true;

        __result = true;

        int added = 0;

        if (__instance.GetFuel() > __instance.m_maxFuel - 1)
        {
            user.Message(MessageHud.MessageType.Center, "$msg_itsfull");
            __result = false;
            return false;
        }

        if (pullAll && inventory.HaveItem(__instance.m_fuelItem.m_itemData.m_shared.m_name))
        {
            if (Boxes.CanItemBePulled(Utils.GetPrefabName(__instance.gameObject), __instance.m_fuelItem.name))
            {
                int amount = (int)Mathf.Min(__instance.m_maxFuel - __instance.GetFuel(), inventory.CountItems(__instance.m_fuelItem.m_itemData.m_shared.m_name));
                inventory.RemoveItem(__instance.m_fuelItem.m_itemData.m_shared.m_name, amount);
                //typeof(Inventory).GetMethod("Changed", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(inventory, new object[] { });
                for (int i = 0; i < amount; ++i)
                    ___m_nview.InvokeRPC("RPC_AddFuel");

                added += amount;

                user.Message(MessageHud.MessageType.TopLeft, Localization.instance.Localize("$msg_fireadding", __instance.m_fuelItem.m_itemData.m_shared.m_name));

                __result = false;
            }
        }

        List<IContainer> nearbyContainers = Boxes.GetNearbyContainers(__instance, AzuCraftyBoxesPlugin.mRange.Value);
        string fuelPrefabName = __instance.m_fuelItem.name;
        string sharedName = __instance.m_fuelItem.m_itemData.m_shared.m_name;
        if (Boxes.CanItemBePulled(Utils.GetPrefabName(__instance.gameObject), fuelPrefabName))
        {
            foreach (IContainer c in nearbyContainers)
            {
                if (!c.ContainsItem(sharedName, 1, out int result)) continue;
                if (!Boxes.CanItemBePulled(c.GetPrefabName(), fuelPrefabName))
                {
                    AzuCraftyBoxesPlugin.AzuCraftyBoxesLogger.LogDebug($"(SmelterOnAddFuelPatch) Container at {c.GetPosition()} has {result} {sharedName} but it's forbidden by config");
                    continue;
                }

                AzuCraftyBoxesPlugin.AzuCraftyBoxesLogger.LogDebug($"Pull ALL is {pullAll}");
                int amount = pullAll
                    ? (int)Mathf.Min(__instance.m_maxFuel - __instance.GetFuel(), result)
                    : 1;

                AzuCraftyBoxesPlugin.AzuCraftyBoxesLogger.LogDebug($"(SmelterOnAddFuelPatch) Container at {c.GetPosition()} has {result} {sharedName}, taking {amount}");

                c.RemoveItem(sharedName, amount);
                c.Save();

                for (int i = 0; i < amount; ++i)
                    ___m_nview.InvokeRPC("RPC_AddFuel");

                added += amount;

                user.Message(MessageHud.MessageType.TopLeft, "$msg_added " + __instance.m_fuelItem.m_itemData.m_shared.m_name);

                __result = false;

                if (!pullAll || Mathf.CeilToInt(___m_nview.GetZDO().GetFloat("fuel")) >= __instance.m_maxFuel)
                    return false;
            }
        }

        user.Message(MessageHud.MessageType.Center, added == 0
            ? "$msg_noprocessableitems"
            : $"$msg_added {added} {__instance.m_fuelItem.m_itemData.m_shared.m_name}");

        return __result;
    }
}