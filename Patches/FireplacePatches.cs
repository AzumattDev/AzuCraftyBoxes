using AzuCraftyBoxes.IContainers;
using AzuCraftyBoxes.Util.Functions;

namespace AzuCraftyBoxes.Patches;

[HarmonyPatch(typeof(Fireplace), nameof(Fireplace.Interact))]
static class FireplaceInteractPatch
{
    static bool Prefix(Fireplace __instance, Humanoid user, bool hold, ref bool __result, ZNetView ___m_nview)
    {
        __result = true;
        if (___m_nview == null || !___m_nview.IsValid() || __instance.m_infiniteFuel || __instance.m_fuelItem?.m_itemData?.m_shared == null)
            return true;

        bool pullAll = Input.GetKey(AzuCraftyBoxesPlugin.fillAllModKey.Value.MainKey); // Used to be fillAllModKey.Value.IsPressed(); something is wrong with KeyboardShortcuts always returning false
        Inventory inventory = user.GetInventory();

        if (MiscFunctions.ShouldPrevent() || hold || inventory == null || (inventory.HaveItem(__instance.m_fuelItem.m_itemData.m_shared.m_name) && !pullAll))
            return true;

        if (!___m_nview.HasOwner())
        {
            ___m_nview.ClaimOwnership();
        }

        if (!Boxes.CanItemBePulled(Utils.GetPrefabName(__instance.gameObject), __instance.m_fuelItem.name))
        {
            return true;
        }

        int fuel = Mathf.CeilToInt(___m_nview.GetZDO().GetFloat(ZDOVars.s_fuel));

        if (pullAll && inventory.HaveItem(__instance.m_fuelItem.m_itemData.m_shared.m_name))
        {
            int amount = (int)Mathf.Min(__instance.m_maxFuel - fuel, inventory.CountItems(__instance.m_fuelItem.m_itemData.m_shared.m_name));
            inventory.RemoveItem(__instance.m_fuelItem.m_itemData.m_shared.m_name, amount);
            inventory.Changed();
            for (int i = 0; i < amount; ++i)
                ___m_nview.InvokeRPC("RPC_AddFuel");

            fuel += amount;

            user.Message(MessageHud.MessageType.Center, Localization.instance.Localize("$msg_fireadding", __instance.m_fuelItem.m_itemData.m_shared.m_name));

            __result = false;
        }

        if (inventory.HaveItem(__instance.m_fuelItem.m_itemData.m_shared.m_name) || !(fuel < __instance.m_maxFuel)) return __result;
        {
            List<IContainer> nearbyContainers = Boxes.QueryFrame.Get(__instance, AzuCraftyBoxesPlugin.mRange.Value);

            string fuelPrefabName = __instance.m_fuelItem.name;
            string sharedName = __instance.m_fuelItem.m_itemData.m_shared.m_name;
            foreach (IContainer c in nearbyContainers)
            {
                if (!c.ContainsItem(sharedName, 1, out int result) || !(fuel < __instance.m_maxFuel)) continue;
                result = Boxes.CheckAndDecrement(result);
                if (result <= 0) continue;
                if (!Boxes.CanItemBePulled(c.GetPrefabName(), fuelPrefabName))
                {
                    AzuCraftyBoxesPlugin.AzuCraftyBoxesLogger.LogIfReleaseAndDebugEnable($"(FireplaceInteractPatch) Container at {c.GetPosition()} has {result} {fuelPrefabName} but it's forbidden by config");
                    continue;
                }

                int amount = pullAll ? (int)Mathf.Min(__instance.m_maxFuel - fuel, result) : 1;
                if (amount <= 0) break;
                AzuCraftyBoxesPlugin.AzuCraftyBoxesLogger.LogIfReleaseAndDebugEnable($"Pull ALL is {pullAll}");
                AzuCraftyBoxesPlugin.AzuCraftyBoxesLogger.LogIfReleaseAndDebugEnable($"(FireplaceInteractPatch) Container at {c.GetPosition()} has {result} {fuelPrefabName}, taking {amount}");

                c.RemoveItem(sharedName, amount);
                c.Save();

                if (__result)
                    user.Message(MessageHud.MessageType.Center, Localization.instance.Localize("$msg_fireadding", __instance.m_fuelItem.m_itemData.m_shared.m_name));

                for (int i = 0; i < amount; ++i)
                    ___m_nview.InvokeRPC("RPC_AddFuel");

                fuel += amount;
                __result = false;

                if (!pullAll || fuel >= __instance.m_maxFuel)
                    return false;
            }
        }

        return __result;
    }
}

[HarmonyPatch(typeof(Fireplace), nameof(Fireplace.RPC_AddFuel))]
static class CapFuel_FireplaceRPC_AddFuelPatch
{
    static bool Prefix(Fireplace __instance, ZNetView ___m_nview)
    {
        if (!___m_nview.IsOwner()) return true;
        return ___m_nview.GetZDO().GetFloat(ZDOVars.s_fuel) < __instance.m_maxFuel;
    }
}

[HarmonyPatch(typeof(Fireplace), nameof(Fireplace.GetHoverText))]
static class FireplaceGetHoverTextPatch
{
    static void Postfix(Fireplace __instance, ref string __result)
    {
        if (MiscFunctions.ShouldPrevent())
        {
            return;
        }

        if (AzuCraftyBoxesPlugin.fillAllModKey.Value.MainKey is KeyCode.None)
        {
            return;
        }

        if (__instance.m_infiniteFuel || __instance.m_fuelItem?.m_itemData?.m_shared == null)
        {
            return;
        }

        ZDO? zdo = __instance.m_nview != null && __instance.m_nview.IsValid() ? __instance.m_nview.GetZDO() : null;
        if (zdo == null)
        {
            return;
        }

        double free = __instance.m_maxFuel - (double)Mathf.CeilToInt(zdo.GetFloat(ZDOVars.s_fuel));
        List<string> items = new();

        if (free <= 0)
        {
            return;
        }

        string fuelPrefabName = __instance.m_fuelItem.name;
        string sharedName = __instance.m_fuelItem.m_itemData.m_shared.m_name;

        if (!Boxes.CanItemBePulled(Utils.GetPrefabName(__instance.gameObject), fuelPrefabName))
        {
            return;
        }

        int inInv = Player.m_localPlayer?.m_inventory?.CountItems(sharedName) ?? 0;
        List<IContainer> nearbyContainers = Boxes.QueryFrame.Get(__instance, AzuCraftyBoxesPlugin.mRange.Value);
        int inContainers = 0;
        __instance.m_fuelItem.m_itemData.m_dropPrefab = __instance.m_fuelItem.gameObject;
        foreach (IContainer c in nearbyContainers)
        {
            if (c == null || !c.ContainsItem(sharedName, 1, out int result)) continue;
            result = Boxes.CheckAndDecrement(result);
            if (Boxes.CanItemBePulled(c.GetPrefabName(), fuelPrefabName))
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
            __result += Localization.instance.Localize($"\n[<b><color=yellow>{AzuCraftyBoxesPlugin.fillAllModKey.Value}</color> + <color=yellow>$KEY_Use</color></b>] {string.Join(" and ", items)}");
        }
    }
}