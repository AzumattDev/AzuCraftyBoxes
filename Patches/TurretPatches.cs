using AzuCraftyBoxes.IContainers;
using AzuCraftyBoxes.Util.Functions;

namespace AzuCraftyBoxes.Patches;

[HarmonyPatch(typeof(Turret), nameof(Turret.UseItem))]
static class Turret_UseItem_Patch
{
    static bool Prefix(Turret __instance, Humanoid user, ref ItemDrop.ItemData item, ref bool __result, ZNetView ___m_nview)
    {
        bool pullAll = Input.GetKey(AzuCraftyBoxesPlugin.fillAllModKey.Value.MainKey);
        Inventory inventory = user.GetInventory();
        if (MiscFunctions.ShouldPrevent() || item != null || user is not Player)
            return true;

        if (!___m_nview.HasOwner())
        {
            ___m_nview.ClaimOwnership();
        }

        item = __instance.FindAmmoItem(inventory, true);

        if (!pullAll && item != null)
            return true;

        string ammoType = __instance.GetAmmoType();
        GameObject prefab = ZNetScene.instance.GetPrefab(ammoType);
        if (!prefab)
        {
            AzuCraftyBoxesPlugin.AzuCraftyBoxesLogger.LogIfReleaseAndDebugEnable($"No prefab found for {__instance.GetAmmoType()}");
            ZLog.LogWarning("Turret '" + __instance.name + "' is trying to fire but has no ammo or default ammo!");
            return true;
        }

        string sharedName = prefab.GetComponent<ItemDrop>().m_itemData.m_shared.m_name;

        if (!Boxes.CanItemBePulled(Utils.GetPrefabName(__instance.gameObject), ammoType))
        {
            AzuCraftyBoxesPlugin.AzuCraftyBoxesLogger.LogIfReleaseAndDebugEnable($"ammoType: {ammoType} could not be pulled due to config");
            return true;
        }

        int ammo = Mathf.CeilToInt(__instance.GetAmmo());

        if (ammo >= __instance.m_maxAmmo)
        {
            user.Message(MessageHud.MessageType.Center, "$msg_itsfull");
            __result = true;
            return false;
        }

        __result = true;
        int added = 0;

        if (pullAll && inventory.HaveItem(sharedName))
        {
            int amount = (int)Mathf.Min(__instance.m_maxAmmo - ammo, inventory.CountItems(sharedName));
            if (amount > 0)
            {
                inventory.RemoveItem(sharedName, amount);
                inventory.Changed();
                for (int i = 0; i < amount; ++i)
                    ___m_nview.InvokeRPC("RPC_AddAmmo", ammoType);

                ammo += amount;
                added += amount;
                user.Message(MessageHud.MessageType.TopLeft, Localization.instance.Localize("$msg_fireadding", sharedName));
            }
        }

        if (ammo < __instance.m_maxAmmo)
        {
            List<IContainer> nearbyContainers = Boxes.QueryFrame.Get(__instance, AzuCraftyBoxesPlugin.mRange.Value);

            foreach (IContainer c in nearbyContainers)
            {
                if (!c.ContainsItem(sharedName, 1, out int result)) continue;
                result = Boxes.CheckAndDecrement(result);
                if (result <= 0) continue;
                if (!Boxes.CanItemBePulled(c.GetPrefabName(), ammoType))
                {
                    AzuCraftyBoxesPlugin.AzuCraftyBoxesLogger.LogIfReleaseAndDebugEnable($"(TurretUseItemPatch) Container at {c.GetPosition()} has {result} {ammoType} but it's forbidden by config");
                    continue;
                }

                int amount = pullAll ? (int)Mathf.Min(__instance.m_maxAmmo - ammo, result) : 1;
                if (amount <= 0) break;
                AzuCraftyBoxesPlugin.AzuCraftyBoxesLogger.LogIfReleaseAndDebugEnable($"(TurretUseItemPatch) Container at {c.GetPosition()} has {result} {ammoType}, taking {amount}");

                c.RemoveItem(sharedName, amount);
                c.Save();

                for (int i = 0; i < amount; ++i)
                    ___m_nview.InvokeRPC("RPC_AddAmmo", ammoType);

                ammo += amount;
                added += amount;

                user.Message(MessageHud.MessageType.TopLeft, "$msg_added " + sharedName);

                if (!pullAll || ammo >= __instance.m_maxAmmo)
                    break;
            }
        }

        if (added > 0)
        {
            user.Message(MessageHud.MessageType.Center, $"$msg_added {added} items");
            __result = true;
            return false;
        }

        item = null;
        return true;
    }
}

[HarmonyPatch(typeof(Turret), nameof(Turret.RPC_AddAmmo))]
static class PreventOverfillJIC_TurretRPC_AddAmmoPatch
{
    static bool Prefix(Turret __instance)
    {
        if (!__instance.m_nview.IsOwner()) return true;
        return __instance.GetAmmo() < __instance.m_maxAmmo;
    }
}

[HarmonyPatch(typeof(Turret), nameof(Turret.GetHoverText))]
static class TurretGetHoverTextPatch
{
    static void Postfix(Turret __instance, ref string __result)
    {
        if (!__instance.m_nview.IsValid())
            return;
        if (MiscFunctions.ShouldPrevent())
        {
            return;
        }

        if (AzuCraftyBoxesPlugin.fillAllModKey.Value.MainKey is KeyCode.None)
        {
            return;
        }

        double free = __instance.m_maxAmmo - (double)Mathf.CeilToInt(__instance.GetAmmo());
        List<string> items = new();

        if (free <= 0)
        {
            return;
        }

        string ammoPrefabName = __instance.GetAmmoType();
        GameObject prefab = ZNetScene.instance.GetPrefab(ammoPrefabName);
        if (!prefab)
        {
            return;
        }

        string sharedName = prefab.GetComponent<ItemDrop>().m_itemData.m_shared.m_name;
        int inInv = Player.m_localPlayer?.m_inventory.CountItems(sharedName) ?? 0;
        List<IContainer> nearbyContainers = Boxes.QueryFrame.Get(__instance, AzuCraftyBoxesPlugin.mRange.Value);
        int inContainers = 0;


        foreach (IContainer c in nearbyContainers)
        {
            if (!c.ContainsItem(sharedName, 1, out int result)) continue;
            result = Boxes.CheckAndDecrement(result);
            if (result <= 0) continue;
            if (Boxes.CanItemBePulled(Utils.GetPrefabName(__instance.gameObject), ammoPrefabName))
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

        if (free - inInv - inContainers > 0 && free < __instance.m_maxAmmo)
        {
            items.Add($"{free - inInv - inContainers} needed to fill");
        }

        if (items.Count > 0)
        {
            __result += Localization.instance.Localize($"\n[<b><color=yellow>{AzuCraftyBoxesPlugin.fillAllModKey.Value}</color> + <color=yellow>$KEY_Use</color></b>] {string.Join(" and ", items)}");
        }
    }
}