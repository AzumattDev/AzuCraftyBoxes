using AzuCraftyBoxes.IContainers;
using AzuCraftyBoxes.Util.Functions;

namespace AzuCraftyBoxes.Patches;

[HarmonyPatch(typeof(Turret), nameof(Turret.UseItem))]
static class Turret_UseItem_Patch
{
    static void Prefix(Turret __instance, Humanoid user, ref ItemDrop.ItemData item, ZNetView ___m_nview)
    {
        // Use our keyboard helper instead of Input.GetKey.
        bool pullAll = AzuCraftyBoxesPlugin.fillAllModKey.Value.IsKeyHeld();
        Inventory inventory = user.GetInventory();

        if (MiscFunctions.ShouldPrevent() || item != null || user is not Player)
        {
            AzuCraftyBoxesPlugin.AzuCraftyBoxesLogger.LogIfReleaseAndDebugEnable($"Not allowed {!MiscFunctions.AllowPullingLogic()} {item is null} {user is Player}");
            return;
        }

        if (!___m_nview.HasOwner())
            ___m_nview.ClaimOwnership();

        // Try to find ammo from the player's inventory first.
        item = __instance.FindAmmoItem(user.GetInventory(), true);
        if (item != null)
            return;

        // No ammo found in inventory; check containers.
        AzuCraftyBoxesPlugin.AzuCraftyBoxesLogger.LogIfReleaseAndDebugEnable(
            $"No item found in inventory, checking containers for {__instance.GetAmmoType()}");

        string ammoType = __instance.GetAmmoType();
        GameObject prefab = ZNetScene.instance.GetPrefab(ammoType);
        if (!prefab)
        {
            AzuCraftyBoxesPlugin.AzuCraftyBoxesLogger.LogIfReleaseAndDebugEnable($"No prefab found for {ammoType}");
            ZLog.LogWarning("Turret '" + __instance.name + "' is trying to fire but has no ammo or default ammo!");
            return;
        }

        // Compute the canonical key from the ammo prefab's ItemDrop.
        string sharedName = prefab.GetComponent<ItemDrop>().m_itemData.m_shared.m_name;
        string ammoPrefabName = ammoType; // This key is what your YAML uses.
        if (!Boxes.CanItemBePulled(Utils.GetPrefabName(__instance.gameObject), ammoType))
        {
            AzuCraftyBoxesPlugin.AzuCraftyBoxesLogger.LogIfReleaseAndDebugEnable(
                $"ammoType: {ammoType} could not be pulled due to config");
            return;
        }

        // Try pulling from player's inventory if available.
        if (pullAll && inventory.HaveItem(sharedName))
        {
            int needed = __instance.m_maxAmmo - Mathf.CeilToInt(__instance.GetAmmo());
            int amount = (int)Mathf.Min(needed, inventory.CountItems(sharedName));
            inventory.RemoveItem(sharedName, amount);
            inventory.Changed();
            for (int i = 0; i < amount; ++i)
                ___m_nview.InvokeRPC("RPC_AddAmmo", ammoPrefabName);

            user.Message(MessageHud.MessageType.Center, $"$msg_added {sharedName}");
            return;
        }

        // If player's inventory still lacks ammo, search nearby containers.
        if (inventory.HaveItem(sharedName) || !(Mathf.CeilToInt(__instance.GetAmmo()) < __instance.m_maxAmmo))
            return;

        List<IContainer> nearbyContainers = Boxes.GetNearbyContainers(__instance, AzuCraftyBoxesPlugin.mRange.Value);
        foreach (IContainer c in nearbyContainers)
        {
            if (!c.ContainsItem(sharedName, 1, out int result) ||
                !(Mathf.CeilToInt(__instance.GetAmmo()) < __instance.m_maxAmmo))
                continue;

            if (!Boxes.CanItemBePulled(c.GetPrefabName(), ammoPrefabName))
            {
                AzuCraftyBoxesPlugin.AzuCraftyBoxesLogger.LogIfReleaseAndDebugEnable(
                    $"(TurretUseItemPatch) Container at {c.GetPosition()} has {result} {ammoPrefabName} but it's forbidden by config");
                continue;
            }

            // Remove ammo from container.
            int amount = pullAll ? (int)Mathf.Min(__instance.m_maxAmmo - Mathf.CeilToInt(__instance.GetAmmo()), result) : 1;
            AzuCraftyBoxesPlugin.AzuCraftyBoxesLogger.LogIfReleaseAndDebugEnable(
                $"(TurretUseItemPatch) Container at {c.GetPosition()} has {result} {ammoPrefabName}, taking {amount}");
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

[HarmonyPatch(typeof(Turret), nameof(Turret.GetHoverText))]
static class TurretGetHoverTextPatch
{
    static void Postfix(Turret __instance, ref string __result)
    {
        if (MiscFunctions.ShouldPrevent())
            return;

        if (AzuCraftyBoxesPlugin.fillAllModKey.Value.MainKey == KeyCode.None)
            return;

        double free = __instance.m_maxAmmo - (double)Mathf.CeilToInt(__instance.GetAmmo());
        List<string> suggestions = new List<string>();
        if (free <= 0)
            return;

        string ammoPrefabName = __instance.GetAmmoType();
        GameObject prefab = ZNetScene.instance.GetPrefab(ammoPrefabName);
        if (!prefab)
            return;

        string sharedName = prefab.GetComponent<ItemDrop>().m_itemData.m_shared.m_name;
        int inInv = Player.m_localPlayer?.m_inventory.CountItems(sharedName) ?? 0;
        List<IContainer> nearbyContainers = Boxes.GetNearbyContainers(__instance, AzuCraftyBoxesPlugin.mRange.Value);
        int inContainers = 0;
        foreach (IContainer c in nearbyContainers)
        {
            if (!c.ContainsItem(sharedName, 1, out int result))
                continue;
            if (Boxes.CanItemBePulled(Utils.GetPrefabName(__instance.gameObject), ammoPrefabName))
            {
                inContainers += result;
            }
        }

        if (inInv > 0)
            suggestions.Add($"{inInv} in inventory");
        if (inContainers > 0)
            suggestions.Add($"{inContainers} in nearby containers");
        int needed = (int)(free - inInv - inContainers);
        if (needed > 0 && free < __instance.m_maxAmmo)
            suggestions.Add($"{needed} needed to fill");

        if (suggestions.Any())
        {
            __result += Localization.instance.Localize(
                $"\n[<b><color=yellow>{AzuCraftyBoxesPlugin.fillAllModKey.Value}</color> + <color=yellow>$KEY_Use</color></b>] {string.Join(" and ", suggestions)}");
        }
    }
}