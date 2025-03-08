using AzuCraftyBoxes.IContainers;
using AzuCraftyBoxes.Util.Functions;

namespace AzuCraftyBoxes.Patches;

[HarmonyPatch(typeof(Turret), nameof(Turret.UseItem))]
static class Turret_UseItem_Patch
{
    static void Prefix(Turret __instance, Humanoid user, ref ItemDrop.ItemData item, ZNetView ___m_nview)
    {
        bool pullAll = Input.GetKey(AzuCraftyBoxesPlugin.fillAllModKey.Value.MainKey);
        Inventory inventory = user.GetInventory();
        if (MiscFunctions.ShouldPrevent() || item != null || user is not Player)
        {
            AzuCraftyBoxesPlugin.AzuCraftyBoxesLogger.LogIfReleaseAndDebugEnable($"Not allowed {!MiscFunctions.AllowPullingLogic()} {item is null} {user is Player}");
            return;
        }

        if (!___m_nview.HasOwner())
        {
            ___m_nview.ClaimOwnership();
        }


        item = __instance.FindAmmoItem(user.GetInventory(), true);
        if (item is null)
        {
            AzuCraftyBoxesPlugin.AzuCraftyBoxesLogger.LogIfReleaseAndDebugEnable($"No item found in inventory, checking containers for {__instance.GetAmmoType()}");
            string? ammoType = __instance.GetAmmoType();
            GameObject prefab = ZNetScene.instance.GetPrefab(ammoType);
            if (!prefab)
            {
                AzuCraftyBoxesPlugin.AzuCraftyBoxesLogger.LogIfReleaseAndDebugEnable($"No prefab found for {__instance.GetAmmoType()}");
                ZLog.LogWarning("Turret '" + __instance.name + "' is trying to fire but has no ammo or default ammo!");
                return;
            }

            string? sharedName = prefab.GetComponent<ItemDrop>().m_itemData.m_shared.m_name;
            string? ammoPrefabName = ammoType;
            if (!Boxes.CanItemBePulled(Utils.GetPrefabName(__instance.gameObject), ammoType))
            {
                AzuCraftyBoxesPlugin.AzuCraftyBoxesLogger.LogIfReleaseAndDebugEnable($"ammoType: {ammoType} could not be pulled due to config");
                return;
            }

            if (pullAll && inventory.HaveItem(sharedName))
            {
                int amount = (int)Mathf.Min(__instance.m_maxAmmo - Mathf.CeilToInt(__instance.GetAmmo()), inventory.CountItems(sharedName));
                inventory.RemoveItem(sharedName, amount);
                inventory.Changed();
                for (int i = 0; i < amount; ++i)
                    ___m_nview.InvokeRPC("RPC_AddAmmo", ammoPrefabName);

                user.Message(MessageHud.MessageType.Center, $"$msg_added {sharedName}");
            }

            if (inventory.HaveItem(sharedName) || !(Mathf.CeilToInt(__instance.GetAmmo()) < __instance.m_maxAmmo)) return;
            {
                List<IContainer> nearbyContainers = Boxes.GetNearbyContainers(__instance, AzuCraftyBoxesPlugin.mRange.Value);

                foreach (IContainer c in nearbyContainers)
                {
                    if (!c.ContainsItem(sharedName, 1, out int result) || !(Mathf.CeilToInt(__instance.GetAmmo()) < __instance.m_maxAmmo)) continue;
                    if (!Boxes.CanItemBePulled(c.GetPrefabName(), ammoPrefabName))
                    {
                        AzuCraftyBoxesPlugin.AzuCraftyBoxesLogger.LogIfReleaseAndDebugEnable($"(TurretUseItemPatch) Container at {c.GetPosition()} has {result} {ammoPrefabName} but it's forbidden by config");
                        continue;
                    }

                    int amount = pullAll ? (int)Mathf.Min(__instance.m_maxAmmo - Mathf.CeilToInt(__instance.GetAmmo()), result) : 1;
                    AzuCraftyBoxesPlugin.AzuCraftyBoxesLogger.LogIfReleaseAndDebugEnable($"Pull ALL is {pullAll}");
                    AzuCraftyBoxesPlugin.AzuCraftyBoxesLogger.LogIfReleaseAndDebugEnable($"(TurretUseItemPatch) Container at {c.GetPosition()} has {result} {ammoPrefabName}, taking {amount}");

                    c.RemoveItem(sharedName, amount);
                    c.Save();

                    user.Message(MessageHud.MessageType.Center, $"$msg_added {sharedName}");

                    for (int i = 0; i < amount; ++i)
                        ___m_nview.InvokeRPC("RPC_AddAmmo", ammoPrefabName);


                    if (!pullAll || Mathf.CeilToInt(__instance.GetAmmo()) >= __instance.m_maxAmmo)
                        return;
                }
            }
        }
    }
}

[HarmonyPatch(typeof(Turret), nameof(Turret.GetHoverText))]
static class TurretGetHoverTextPatch
{
    static void Postfix(Turret __instance, ref string __result)
    {
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
        List<IContainer> nearbyContainers = Boxes.GetNearbyContainers(__instance, AzuCraftyBoxesPlugin.mRange.Value);
        int inContainers = 0;


        foreach (IContainer c in nearbyContainers)
        {
            if (!c.ContainsItem(sharedName, 1, out int result)) continue;
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