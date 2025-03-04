using AzuCraftyBoxes.IContainers;
using AzuCraftyBoxes.Util.Functions;

namespace AzuCraftyBoxes.Patches;

[HarmonyPatch(typeof(Fireplace), nameof(Fireplace.Interact))]
static class FireplaceInteractPatch
{
    static bool Prefix(Fireplace __instance, Humanoid user, bool hold, ref bool __result, ZNetView ___m_nview)
    {
        __result = true;
        bool pullAll = Input.GetKey(AzuCraftyBoxesPlugin.fillAllModKey.Value.MainKey);
        Inventory inventory = user.GetInventory();
        if (MiscFunctions.ShouldPrevent() || hold || inventory == null ||
            (inventory.HaveItem(__instance.m_fuelItem.m_itemData.m_shared.m_name) && !pullAll))
            return true;

        if (!___m_nview.HasOwner())
        {
            ___m_nview.ClaimOwnership();
        }

        if (!Boxes.CanItemBePulled(Utils.GetPrefabName(__instance.gameObject), __instance.m_fuelItem.name))
            return true;

        // Compute canonical key for the fuel item.
        string canonicalKey = ItemKeyHelper.GetCanonicalKey(__instance.m_fuelItem.m_itemData);
        string sharedName = __instance.m_fuelItem.m_itemData.m_shared.m_name;

        // If pullAll is active and the player's inventory has fuel, remove from it.
        if (pullAll && inventory.HaveItem(sharedName))
        {
            int currentFuel = Mathf.CeilToInt(___m_nview.GetZDO().GetFloat(ZDOVars.s_fuel));
            int neededFuel = (int)__instance.m_maxFuel - currentFuel;
            int amountToPull = (int)Mathf.Min(neededFuel, inventory.CountItems(sharedName));
            inventory.RemoveItem(sharedName, amountToPull);
            inventory.Changed();
            for (int i = 0; i < amountToPull; i++)
            {
                ___m_nview.InvokeRPC("RPC_AddFuel");
            }

            user.Message(MessageHud.MessageType.Center,
                Localization.instance.Localize("$msg_fireadding", __instance.m_fuelItem.m_itemData.m_shared.m_name));
            __result = false;
            return false;
        }

        // If player's inventory now has fuel or fuel level is at or above max, skip container search.
        if (inventory.HaveItem(sharedName) || !(Mathf.CeilToInt(___m_nview.GetZDO().GetFloat(ZDOVars.s_fuel)) < __instance.m_maxFuel))
            return __result;

        // Otherwise, search nearby containers.
        List<IContainer> nearbyContainers = Boxes.GetNearbyContainers(__instance, AzuCraftyBoxesPlugin.mRange.Value);
        string fuelPrefabName = __instance.m_fuelItem.name;
        foreach (IContainer container in nearbyContainers)
        {
            if (!container.ContainsItem(sharedName, 1, out int result) ||
                !(Mathf.CeilToInt(___m_nview.GetZDO().GetFloat(ZDOVars.s_fuel)) < __instance.m_maxFuel))
                continue;

            if (!Boxes.CanItemBePulled(container.GetPrefabName(), fuelPrefabName))
            {
                AzuCraftyBoxesPlugin.AzuCraftyBoxesLogger.LogIfReleaseAndDebugEnable(
                    $"(FireplaceInteractPatch) Container at {container.GetPosition()} has {result} {fuelPrefabName} but it's forbidden by config");
                continue;
            }

            int currentFuel = Mathf.CeilToInt(___m_nview.GetZDO().GetFloat(ZDOVars.s_fuel));
            int neededFuel = (int)__instance.m_maxFuel - currentFuel;
            // Use cache manager if possible.
            int available = result;
            if (container is Container vanilla && ContainerInventoryCacheManager.Instance != null)
            {
                available = ContainerInventoryCacheManager.Instance.GetAggregatedItemCount(vanilla, canonicalKey);
            }

            int amountToRemove = pullAll ? (int)Mathf.Min(neededFuel, available) : 1;
            AzuCraftyBoxesPlugin.AzuCraftyBoxesLogger.LogIfReleaseAndDebugEnable($"Pull ALL is {pullAll}");
            AzuCraftyBoxesPlugin.AzuCraftyBoxesLogger.LogIfReleaseAndDebugEnable(
                $"(FireplaceInteractPatch) Container at {container.GetPosition()} has {available} {fuelPrefabName}, taking {amountToRemove}");
            container.RemoveItem(sharedName, amountToRemove);
            container.Save();
            if (__result)
                user.Message(MessageHud.MessageType.Center,
                    Localization.instance.Localize("$msg_fireadding", __instance.m_fuelItem.m_itemData.m_shared.m_name));
            for (int i = 0; i < amountToRemove; i++)
            {
                ___m_nview.InvokeRPC("RPC_AddFuel");
            }

            __result = false;
            if (!pullAll || Mathf.CeilToInt(___m_nview.GetZDO().GetFloat(ZDOVars.s_fuel)) >= __instance.m_maxFuel)
                return false;
        }

        return __result;
    }
}

[HarmonyPatch(typeof(Fireplace), nameof(Fireplace.GetHoverText))]
static class FireplaceGetHoverTextPatch
{
    static void Postfix(Fireplace __instance, ref string __result)
    {
        if (MiscFunctions.ShouldPrevent())
            return;
        if (AzuCraftyBoxesPlugin.fillAllModKey.Value.MainKey == KeyCode.None)
            return;

        float currentFuel = Mathf.CeilToInt(__instance.m_nview.GetZDO().GetFloat(ZDOVars.s_fuel));
        double freeFuel = __instance.m_maxFuel - currentFuel;
        List<string> items = new List<string>();
        if (freeFuel <= 0)
            return;

        string fuelPrefabName = __instance.m_fuelItem.name;
        string sharedName = __instance.m_fuelItem.m_itemData.m_shared.m_name;
        if (!Boxes.CanItemBePulled(Utils.GetPrefabName(__instance.gameObject), fuelPrefabName))
            return;

        // Use player's inventory count (fallback; could be improved with a canonical lookup if desired)
        int inInventory = Player.m_localPlayer?.m_inventory.CountItems(sharedName) ?? 0;
        List<IContainer> nearbyContainers = Boxes.GetNearbyContainers(__instance, AzuCraftyBoxesPlugin.mRange.Value);
        int inContainers = 0;
        __instance.m_fuelItem.m_itemData.m_dropPrefab = __instance.m_fuelItem.gameObject;
        string canonicalKey = ItemKeyHelper.GetCanonicalKey(__instance.m_fuelItem.m_itemData);
        foreach (IContainer container in nearbyContainers)
        {
            if (!container.ContainsItem(sharedName, 1, out int result))
                continue;
            if (container is Container vanilla && ContainerInventoryCacheManager.Instance != null)
            {
                result = ContainerInventoryCacheManager.Instance.GetAggregatedItemCount(vanilla, canonicalKey);
            }

            if (Boxes.CanItemBePulled(container.GetPrefabName(), fuelPrefabName))
            {
                inContainers += result;
            }
        }

        List<string> parts = new List<string>();
        if (inInventory > 0)
            parts.Add($"{inInventory} in inventory");
        if (inContainers > 0)
            parts.Add($"{inContainers} in nearby containers");
        int remaining = (int)(freeFuel - inInventory - inContainers);
        if (remaining > 0 && freeFuel < __instance.m_maxFuel)
            parts.Add($"{remaining} needed to fill");

        if (parts.Count > 0)
        {
            __result += Localization.instance.Localize($"\n[<b><color=yellow>{AzuCraftyBoxesPlugin.fillAllModKey.Value}</color> + <color=yellow>$KEY_Use</color></b>] {string.Join(" and ", parts)}");
        }
    }
}