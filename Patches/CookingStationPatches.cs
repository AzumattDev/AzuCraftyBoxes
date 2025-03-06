using AzuCraftyBoxes.IContainers;
using AzuCraftyBoxes.Util.Functions;

namespace AzuCraftyBoxes.Patches;

[HarmonyPatch(typeof(CookingStation), nameof(CookingStation.OnAddFuelSwitch))]
static class CookingStationOnAddFuelSwitchPatch
{
    static bool Prefix(CookingStation __instance, ref bool __result, Humanoid user, ItemDrop.ItemData item, ZNetView ___m_nview)
    {
        AzuCraftyBoxesPlugin.AzuCraftyBoxesLogger.LogIfReleaseAndDebugEnable($"(CookingStationOnAddFuelSwitchPatch) Looking for fuel");

        // If conditions are already met (player has fuel, or station is full, etc.), let vanilla logic run.
        if (MiscFunctions.ShouldPrevent() || item != null ||
            __instance.GetFuel() > __instance.m_maxFuel - 1 ||
            (user.GetInventory().HaveItem(__instance.m_fuelItem.m_itemData.m_shared.m_name) && Boxes.CanItemBePulled(Utils.GetPrefabName(__instance.gameObject), __instance.m_fuelItem.name)))
            return true;

        AzuCraftyBoxesPlugin.AzuCraftyBoxesLogger.LogIfReleaseAndDebugEnable($"(CookingStationOnAddFuelSwitchPatch) Missing fuel in player inventory");

        string fuelPrefabName = __instance.m_fuelItem.name;
        if (!Boxes.CanItemBePulled(Utils.GetPrefabName(__instance.gameObject), fuelPrefabName))
        {
            AzuCraftyBoxesPlugin.AzuCraftyBoxesLogger.LogIfReleaseAndDebugEnable($"(CookingStationOnAddFuelSwitchPatch) CookingStation is forbidden to pull {fuelPrefabName} by config");
            return true;
        }

        // Get nearby containers (which should be registered in our cache system)
        List<IContainer> nearbyContainers = Boxes.GetNearbyContainers(__instance, AzuCraftyBoxesPlugin.mRange.Value);
        string sharedName = __instance.m_fuelItem.m_itemData.m_shared.m_name;

        foreach (IContainer container in nearbyContainers)
        {
            // Use the IContainer API to check for at least one unit of the fuel item.
            if (!container.ContainsItem(sharedName, 1, out int count))
                continue;

            // Respect configuration checks.
            if (!Boxes.CanItemBePulled(container.GetPrefabName(), fuelPrefabName))
            {
                AzuCraftyBoxesPlugin.AzuCraftyBoxesLogger.LogIfReleaseAndDebugEnable($"(CookingStationOnAddFuelSwitchPatch) Container at {container.GetPosition()} has {count} {fuelPrefabName} but is forbidden by config");
                return true;
            }

            AzuCraftyBoxesPlugin.AzuCraftyBoxesLogger.LogIfReleaseAndDebugEnable($"(CookingStationOnAddFuelSwitchPatch) Container at {container.GetPosition()} has {count} {fuelPrefabName}, taking one");

            // Remove one unit using the container’s API (which calls the vanilla removal logic).
            container.RemoveItem(sharedName, 1);
            container.Save();

            // (Optionally, if the container is a vanilla container, our cache manager will update automatically via its event.)

            user.Message(MessageHud.MessageType.Center, "$msg_added " + __instance.m_fuelItem.m_itemData.m_shared.m_name);
            ___m_nview.InvokeRPC("RPC_AddFuel", Array.Empty<object>());
            __result = true;
            return false; // Skip vanilla logic.
        }

        return true;
    }
}

[HarmonyPatch(typeof(CookingStation), nameof(CookingStation.FindCookableItem))]
static class CookingStationFindCookableItemPatch
{
    static void Postfix(CookingStation __instance, ref ItemDrop.ItemData __result)
    {
        AzuCraftyBoxesPlugin.AzuCraftyBoxesLogger.LogIfReleaseAndDebugEnable($"(CookingStationFindCookableItemPatch) Looking for cookable");

        // If a cookable item is already found, or if prerequisites aren’t met, skip our logic.
        if (MiscFunctions.ShouldPrevent() || __result != null ||
            (__instance.m_requireFire && !__instance.IsFireLit() || __instance.GetFreeSlot() == -1))
            return;

        AzuCraftyBoxesPlugin.AzuCraftyBoxesLogger.LogIfReleaseAndDebugEnable($"(CookingStationFindCookableItemPatch) Missing cookable in player inventory");

        List<IContainer> nearbyContainers = Boxes.GetNearbyContainers(__instance, AzuCraftyBoxesPlugin.mRange.Value);

        foreach (CookingStation.ItemConversion itemConversion in __instance.m_conversion)
        {
            string fromPrefabName = itemConversion.m_from.name;
            string sharedName = itemConversion.m_from.m_itemData.m_shared.m_name;

            if (!Boxes.CanItemBePulled(Utils.GetPrefabName(__instance.gameObject), fromPrefabName))
            {
                AzuCraftyBoxesPlugin.AzuCraftyBoxesLogger.LogIfReleaseAndDebugEnable($"(CookingStationFindCookableItemPatch) CookingStation is forbidden to pull {fromPrefabName} by config");
                continue;
            }

            foreach (IContainer c in nearbyContainers)
            {
                if (!c.ContainsItem(sharedName, 1, out int result)) continue;

                if (!Boxes.CanItemBePulled(c.GetPrefabName(), fromPrefabName))
                {
                    AzuCraftyBoxesPlugin.AzuCraftyBoxesLogger.LogIfReleaseAndDebugEnable($"(CookingStationFindCookableItemPatch) Container at {c.GetPosition()} has {result} {fromPrefabName} but is forbidden by config");
                    continue;
                }

                AzuCraftyBoxesPlugin.AzuCraftyBoxesLogger.LogIfReleaseAndDebugEnable($"(CookingStationFindCookableItemPatch) Container at {c.GetPosition()} has {result} {fromPrefabName}, taking one");

                // Use the base game’s prefab as a template to create a new cookable item.
                GameObject dropTemplate = ObjectDB.instance.m_itemByHash[fromPrefabName.GetStableHashCode()];
                ItemDrop.ItemData newItem = dropTemplate.GetComponent<ItemDrop>().m_itemData.Clone();
                newItem.m_dropPrefab = dropTemplate;
                __result = newItem;

                // Remove one unit from the container via the IContainer API.
                c.RemoveItem(sharedName, 1);

                c.Save();
                return;
            }
        }
    }
}