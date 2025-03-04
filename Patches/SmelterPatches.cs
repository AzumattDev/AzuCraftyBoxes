using AzuCraftyBoxes.IContainers;
using AzuCraftyBoxes.Util.Functions;

namespace AzuCraftyBoxes.Patches;

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
        if (MiscFunctions.ShouldPrevent())
            return true;
        if (AzuCraftyBoxesPlugin.fillAllModKey.Value.MainKey == KeyCode.None)
            return true;
        if (Player.m_localPlayer == null)
            return true;
        // Only proceed if the player is actually hovering over this smelter.
        return !Player.m_localPlayer.m_hovering ||
               Player.m_localPlayer.m_hovering.GetComponentInParent<Smelter>() != __instance;
    }

    /// <summary>
    /// For fuel: Count the total amount of fuel available (player + containers), then update the hover text.
    /// </summary>
    internal static void UpdateAddWoodSwitchHoverText(Smelter __instance, ref string result)
    {
        // Compute canonical key for the fuel item.
        string canonicalKey = ItemKeyHelper.GetCanonicalKey(__instance.m_fuelItem.m_itemData);
        string sharedName = __instance.m_fuelItem.m_itemData.m_shared.m_name;

        // Get count from player's inventory.
        int inInv = Player.m_localPlayer?.m_inventory.CountItems(sharedName) ?? 0;
        int inContainers = 0;

        // Get nearby containers.
        List<IContainer> nearbyContainers = Boxes.GetNearbyContainers(__instance, AzuCraftyBoxesPlugin.mRange.Value);
        foreach (IContainer container in nearbyContainers)
        {
            int count = 0;
            if (container is Container vanilla && ContainerInventoryCacheManager.Instance != null)
            {
                count = ContainerInventoryCacheManager.Instance.GetAggregatedItemCount(vanilla, canonicalKey);
            }
            else
            {
                container.ContainsItem(sharedName, 1, out int res);
                count = res;
            }

            inContainers += count;
        }

        // Determine how much fuel is needed.
        int fuelNeeded = Mathf.Min(__instance.m_maxFuel - Mathf.CeilToInt(__instance.GetFuel()), inInv + inContainers);
        // Ensure the fuel item's dropPrefab is set.
        __instance.m_fuelItem.m_itemData.m_dropPrefab = __instance.m_fuelItem.gameObject;
        if (fuelNeeded > 0)
        {
            if (Boxes.CanItemBePulled(Utils.GetPrefabName(__instance.gameObject), Utils.GetPrefabName(__instance.m_fuelItem.m_itemData.m_dropPrefab)))
            {
                result += Localization.instance.Localize(
                    $"\n[<b><color=yellow>{AzuCraftyBoxesPlugin.fillAllModKey.Value}</color> + <color=yellow>$KEY_Use</color></b>] $piece_smelter_add {sharedName} {fuelNeeded} from Inventory & Nearby Containers");
            }
        }
    }

    /// <summary>
    /// For ore: Iterate over the smelter's conversions, aggregate available ore counts, and update hover text.
    /// </summary>
    internal static void UpdateAddOreSwitchHoverText(Smelter __instance, ref string result)
    {
        int free = __instance.m_maxOre - __instance.GetQueueSize();
        List<string> items = new List<string>();
        if (free <= 0)
            return;

        // Process each ore conversion.
        foreach (Smelter.ItemConversion conversion in __instance.m_conversion)
        {
            if (free <= 0)
                break;

            // Use canonical key for the ore item.
            string canonicalKey = ItemKeyHelper.GetCanonicalKey(conversion.m_from.m_itemData);
            int inInv = GetItemCountInInventoryAndContainers(conversion.m_from.name, conversion.m_from.m_itemData.m_shared.m_name, __instance);
            int count = Mathf.Min(free, inInv);
            free -= count;
            if (!MiscFunctions.CheckItemDropIntegrity(conversion.m_from))
                continue;
            // Ensure dropPrefab is set.
            conversion.m_from.m_itemData.m_dropPrefab = conversion.m_from.gameObject;
            if (MiscFunctions.GetItemPrefabFromGameObject(conversion.m_from, conversion.m_from.gameObject) == null)
                continue;
            if (count > 0)
            {
                if (Boxes.CanItemBePulled(Utils.GetPrefabName(__instance.gameObject), Utils.GetPrefabName(conversion.m_from.m_itemData.m_dropPrefab)))
                {
                    items.Add($"{count} {conversion.m_from.m_itemData.m_shared.m_name}");
                }
            }
        }

        if (items.Any())
        {
            result += Localization.instance.Localize(
                $"\n[<b><color=yellow>{AzuCraftyBoxesPlugin.fillAllModKey.Value}</color> + <color=yellow>$KEY_Use</color></b>] {__instance.m_addOreTooltip} {string.Join(", ", items)} from Inventory & Nearby Containers");
        }
    }

    /// <summary>
    /// Aggregates item counts from both the player's inventory and nearby containers.
    /// Uses the canonical key for vanilla containers (via the cache manager) and falls back to IContainer.ContainsItem.
    /// </summary>
    private static int GetItemCountInInventoryAndContainers(string prefabName, string itemName, Smelter smelterInstance)
    {
        int inInv = Player.m_localPlayer?.m_inventory.CountItems(itemName) ?? 0;
        List<IContainer> nearbyContainers = Boxes.GetNearbyContainers(smelterInstance, AzuCraftyBoxesPlugin.mRange.Value);
        foreach (IContainer container in nearbyContainers)
        {
            if (Boxes.CanItemBePulled(prefabName, container.GetPrefabName()))
            {
                int count = 0;
                // If vanilla container, try to use the cache.
                if (container is Container vanilla && ContainerInventoryCacheManager.Instance != null)
                {
                    // Use the canonical key from itemName.
                    string canonicalKey = itemName.ToLowerInvariant(); // Or use a helper if you can get full ItemDrop.ItemData here.
                    count = ContainerInventoryCacheManager.Instance.GetAggregatedItemCount(vanilla, canonicalKey);
                }
                else
                {
                    container.ContainsItem(itemName, 1, out int res);
                    count = res;
                }

                inInv += count;
            }
        }

        return inInv;
    }
}

[HarmonyPatch(typeof(Smelter), nameof(Smelter.OnAddOre))]
static class SmelterOnAddOrePatch
{
    static bool Prefix(Smelter __instance, Humanoid user, ItemDrop.ItemData item, ZNetView ___m_nview, out KeyValuePair<ItemDrop.ItemData?, int> __state)
    {
        int initialOre = __instance.GetQueueSize();
        __state = new KeyValuePair<ItemDrop.ItemData?, int>(item, initialOre);
        bool pullAll = AzuCraftyBoxesPlugin.fillAllModKey.Value.IsKeyHeld();
        if (MiscFunctions.ShouldPrevent() || item != null || __instance.GetQueueSize() >= __instance.m_maxOre)
            return true;

        Inventory inventory = user.GetInventory();
        // If player's inventory already has a cookable item for any conversion and pullAll is false, use vanilla logic.
        if (__instance.m_conversion.Any(conv =>
            {
                string itemName = conv?.m_from?.m_itemData?.m_shared?.m_name;
                return itemName != null && inventory.HaveItem(itemName) && !pullAll &&
                       Boxes.CanItemBePulled(Utils.GetPrefabName(__instance.gameObject), conv.m_from.name);
            }))
        {
            return true;
        }

        Dictionary<string, int> added = new Dictionary<string, int>();
        List<IContainer> nearbyContainers = Boxes.GetNearbyContainers(user, AzuCraftyBoxesPlugin.mRange.Value);

        // Process each conversion for ore.
        foreach (Smelter.ItemConversion conv in __instance.m_conversion)
        {
            if (__instance.GetQueueSize() >= __instance.m_maxOre || (added.Any() && !pullAll))
                break;

            string sharedName = conv.m_from.m_itemData.m_shared.m_name;
            // Compute the canonical key for this ore item.
            string canonicalKey = ItemKeyHelper.GetCanonicalKey(conv.m_from.m_itemData);
            string convPrefabName = conv.m_from.name;

            // Try pulling from player's inventory first.
            if (pullAll && inventory.HaveItem(sharedName))
            {
                ItemDrop.ItemData newItem = inventory.GetItem(sharedName);
                if (newItem != null)
                {
                    try
                    {
                        // Set dropPrefab using the fuelItem method from the smelter.
                        GameObject itemPrefab = ObjectDB.instance.GetItemPrefab(__instance.m_fuelItem.GetPrefabName(conv.m_from.gameObject.name));
                        newItem.m_dropPrefab = itemPrefab;
                    }
                    catch (Exception e)
                    {
                        // Log if necessary.
                    }

                    if (newItem.m_dropPrefab != null && Boxes.CanItemBePulled(Utils.GetPrefabName(__instance.gameObject), Utils.GetPrefabName(newItem.m_dropPrefab)))
                    {
                        int neededOre = __instance.m_maxOre - __instance.GetQueueSize();
                        int availableInInv = inventory.CountItems(sharedName);
                        int amount = pullAll ? Mathf.Min(neededOre, availableInInv) : 1;
                        if (!added.ContainsKey(sharedName))
                            added[sharedName] = 0;
                        added[sharedName] += amount;
                        inventory.RemoveItem(sharedName, amount);
                        for (int i = 0; i < amount; i++)
                        {
                            ___m_nview.InvokeRPC("RPC_AddOre", newItem.m_dropPrefab.name);
                        }

                        user.Message(MessageHud.MessageType.TopLeft, $"$msg_added {amount} {sharedName}");
                        if (__instance.GetQueueSize() >= __instance.m_maxOre)
                            break;
                    }
                }
            }

            // Then, attempt pulling from nearby containers.
            if (Boxes.CanItemBePulled(Utils.GetPrefabName(__instance.gameObject), convPrefabName))
            {
                foreach (IContainer c in nearbyContainers)
                {
                    if (!c.ContainsItem(sharedName, 1, out int result))
                        continue;
                    if (!Boxes.CanItemBePulled(c.GetPrefabName(), convPrefabName))
                    {
                        AzuCraftyBoxesPlugin.AzuCraftyBoxesLogger.LogIfReleaseAndDebugEnable(
                            $"(SmelterOnAddOrePatch) Container at {c.GetPosition()} has {result} {convPrefabName} but it's forbidden by config");
                        continue;
                    }

                    int amount = pullAll ? Mathf.Min(__instance.m_maxOre - __instance.GetQueueSize(), result) : 1;
                    if (!added.ContainsKey(sharedName))
                        added[sharedName] = 0;
                    added[sharedName] += amount;
                    AzuCraftyBoxesPlugin.AzuCraftyBoxesLogger.LogIfReleaseAndDebugEnable($"Pull ALL is {pullAll}");
                    AzuCraftyBoxesPlugin.AzuCraftyBoxesLogger.LogIfReleaseAndDebugEnable(
                        $"(SmelterOnAddOrePatch) Container at {c.GetPosition()} has {result} {convPrefabName}, taking {amount}");
                    c.RemoveItem(sharedName, amount);
                    c.Save();
                    for (int i = 0; i < amount; i++)
                    {
                        ___m_nview.InvokeRPC("RPC_AddOre", convPrefabName);
                    }

                    user.Message(MessageHud.MessageType.TopLeft, $"$msg_added {amount} {sharedName}");
                    if (__instance.GetQueueSize() >= __instance.m_maxOre || !pullAll)
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

    static void Postfix(Smelter __instance, Switch sw, Humanoid user, KeyValuePair<ItemDrop.ItemData?, int> __state, bool __result)
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
        bool pullAll = AzuCraftyBoxesPlugin.fillAllModKey.Value.IsKeyHeld();
        Inventory inventory = user.GetInventory();
        string sharedName = __instance.m_fuelItem.m_itemData.m_shared.m_name;
        if (MiscFunctions.ShouldPrevent() || item != null || inventory == null ||
            ((inventory.HaveItem(sharedName) && !pullAll) &&
             Boxes.CanItemBePulled(Utils.GetPrefabName(__instance.gameObject), __instance.m_fuelItem.name)))
            return true;

        __result = true;
        int added = 0;

        if (__instance.GetFuel() > __instance.m_maxFuel - 1)
        {
            user.Message(MessageHud.MessageType.Center, "$msg_itsfull");
            __result = false;
            return false;
        }

        // Process pulling from player's inventory first.
        if (pullAll && inventory.HaveItem(sharedName))
        {
            if (Boxes.CanItemBePulled(Utils.GetPrefabName(__instance.gameObject), __instance.m_fuelItem.name))
            {
                float neededFuel = __instance.m_maxFuel - __instance.GetFuel();
                int amount = (int)Mathf.Min(neededFuel, inventory.CountItems(sharedName));
                inventory.RemoveItem(sharedName, amount);
                for (int i = 0; i < amount; i++)
                {
                    ___m_nview.InvokeRPC("RPC_AddFuel");
                }

                added += amount;
                user.Message(MessageHud.MessageType.TopLeft,
                    Localization.instance.Localize("$msg_fireadding", sharedName));
                __result = false;
            }
        }

        // Then, process nearby containers.
        List<IContainer> nearbyContainers = Boxes.GetNearbyContainers(__instance, AzuCraftyBoxesPlugin.mRange.Value);
        string fuelPrefabName = __instance.m_fuelItem.name;
        // Compute canonical key for the fuel item.
        string canonicalKey = ItemKeyHelper.GetCanonicalKey(__instance.m_fuelItem.m_itemData);
        if (Boxes.CanItemBePulled(Utils.GetPrefabName(__instance.gameObject), fuelPrefabName))
        {
            foreach (IContainer c in nearbyContainers)
            {
                if (!c.ContainsItem(sharedName, 1, out int result))
                    continue;
                if (!Boxes.CanItemBePulled(c.GetPrefabName(), fuelPrefabName))
                {
                    AzuCraftyBoxesPlugin.AzuCraftyBoxesLogger.LogIfReleaseAndDebugEnable(
                        $"(SmelterOnAddFuelPatch) Container at {c.GetPosition()} has {result} {sharedName} but it's forbidden by config");
                    continue;
                }

                int available = result;
                if (c is Container vanilla && ContainerInventoryCacheManager.Instance != null)
                {
                    available = ContainerInventoryCacheManager.Instance.GetAggregatedItemCount(vanilla, canonicalKey);
                }

                float neededFuel = __instance.m_maxFuel - __instance.GetFuel();
                int amount = pullAll ? (int)Mathf.Min(neededFuel, available) : 1;
                AzuCraftyBoxesPlugin.AzuCraftyBoxesLogger.LogIfReleaseAndDebugEnable(
                    $"(SmelterOnAddFuelPatch) Container at {c.GetPosition()} has {available} {sharedName}, taking {amount}");
                c.RemoveItem(sharedName, amount);
                c.Save();
                for (int i = 0; i < amount; i++)
                {
                    ___m_nview.InvokeRPC("RPC_AddFuel");
                }

                added += amount;
                user.Message(MessageHud.MessageType.TopLeft, "$msg_added " + sharedName);
                __result = false;
                if (!pullAll || Mathf.CeilToInt(___m_nview.GetZDO().GetFloat(ZDOVars.s_fuel)) >= __instance.m_maxFuel)
                    return false;
            }
        }

        user.Message(MessageHud.MessageType.Center, added == 0
            ? "$msg_noprocessableitems"
            : $"$msg_added {added} {sharedName}");
        return __result;
    }
}