using AzuCraftyBoxes.IContainers;
using AzuCraftyBoxes.Util.Functions;

namespace AzuCraftyBoxes.Patches
{
    [HarmonyPatch(typeof(ShieldGenerator), nameof(ShieldGenerator.OnHoverAddFuel))]
    [HarmonyBefore("org.bepinex.plugins.conversionsizespeed")]
    static class ShieldGeneratorOnHoverAddFuelPatch
    {
        static void Postfix(ShieldGenerator __instance, ref string __result)
        {
            if (OverrideHoverTextSg.ShouldReturn(__instance))
            {
                return;
            }

            OverrideHoverTextSg.UpdateAddFuelSwitchHoverText(__instance, ref __result);
        }
    }

    public static class OverrideHoverTextSg
    {
        public static bool ShouldReturn(ShieldGenerator __instance)
        {
            if (MiscFunctions.ShouldPrevent())
                return true;
            if (AzuCraftyBoxesPlugin.fillAllModKey.Value.MainKey == KeyCode.None)
                return true;
            if (Player.m_localPlayer == null)
                return true;
            // Only proceed if the player's hovering object is this ShieldGenerator.
            return !Player.m_localPlayer.m_hovering || Player.m_localPlayer.m_hovering.GetComponentInParent<ShieldGenerator>() != __instance;
        }

        internal static void UpdateAddFuelSwitchHoverText(ShieldGenerator __instance, ref string result)
        {
            double free = __instance.m_maxFuel - __instance.GetFuel();
            List<string> suggestions = new List<string>();
            if (free <= 0)
                return;

            // For each fuel item, use the canonical key.
            foreach (ItemDrop fuelItem in __instance.m_fuelItems)
            {
                // Compute canonical key for this fuel item.
                string canonicalKey = ItemKeyHelper.GetCanonicalKey(fuelItem.m_itemData);
                string sharedName = fuelItem.m_itemData.m_shared.m_name;
                // Count player's fuel in inventory (fallback uses raw shared name).
                int inInv = Player.m_localPlayer?.m_inventory.CountItems(sharedName) ?? 0;
                int inContainers = 0;

                List<IContainer> nearbyContainers = Boxes.GetNearbyContainers(__instance, AzuCraftyBoxesPlugin.mRange.Value);
                foreach (IContainer c in nearbyContainers)
                {
                    // Only if pulling is allowed by config for this fuel.
                    if (!Boxes.CanItemBePulled(Utils.GetPrefabName(__instance.gameObject), fuelItem.name))
                        continue;

                    int containerCount = 0;
                    // If container is vanilla and registered, use our cache.
                    if (c is Container vanilla && ContainerInventoryCacheManager.Instance != null)
                    {
                        containerCount = ContainerInventoryCacheManager.Instance.GetAggregatedItemCount(vanilla, canonicalKey);
                    }
                    else
                    {
                        c.ContainsItem(sharedName, 1, out int res);
                        containerCount = res;
                    }

                    inContainers += containerCount;
                }

                // Build suggestion parts.
                if (inInv > 0)
                    suggestions.Add($"{inInv} in inventory");
                if (inContainers > 0)
                    suggestions.Add($"{inContainers} in nearby containers");

                int needed = (int)(free - inInv - inContainers);
                if (needed > 0 && free < __instance.m_maxFuel)
                    suggestions.Add($"{needed} needed to fill");
            }

            if (suggestions.Count > 0)
            {
                result += Localization.instance.Localize($"\n[<b><color=yellow>{AzuCraftyBoxesPlugin.fillAllModKey.Value}</color> + <color=yellow>$KEY_Use</color></b>] Add {string.Join(" and ", suggestions)}");
            }
        }
    }

    [HarmonyPatch(typeof(ShieldGenerator), nameof(ShieldGenerator.OnAddFuel))]
    [HarmonyBefore("org.bepinex.plugins.conversionsizespeed")]
    static class ShieldGeneratorOnAddFuelPatch
    {
        static bool Prefix(ShieldGenerator __instance, ref bool __result, ZNetView ___m_nview, Humanoid user, ItemDrop.ItemData item)
        {
            bool pullAll = AzuCraftyBoxesPlugin.fillAllModKey.Value.IsKeyHeld();
            Inventory inventory = user.GetInventory();
            if (MiscFunctions.ShouldPrevent() || item != null || inventory == null)
                return true;

            __result = true;
            int added = 0;

            if (__instance.GetFuel() > __instance.m_maxFuel - 1)
            {
                user.Message(MessageHud.MessageType.Center, "$msg_itsfull");
                __result = false;
                return false;
            }

            // Process each fuel item available on the shield generator.
            foreach (ItemDrop fuelItem in __instance.m_fuelItems)
            {
                string sharedName = fuelItem.m_itemData.m_shared.m_name;
                // Compute canonical key.
                string canonicalKey = ItemKeyHelper.GetCanonicalKey(fuelItem.m_itemData);

                // First, try to pull from player's inventory.
                if (pullAll && inventory.HaveItem(sharedName))
                {
                    if (Boxes.CanItemBePulled(Utils.GetPrefabName(__instance.gameObject), fuelItem.name))
                    {
                        float currentFuel = __instance.GetFuel();
                        float neededFuel = __instance.m_maxFuel - currentFuel;
                        int availableInInv = inventory.CountItems(sharedName);
                        int amount = (int)Mathf.Min(neededFuel, availableInInv);
                        inventory.RemoveItem(sharedName, amount);
                        inventory.Changed();
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

                // Then, search nearby containers.
                List<IContainer> nearbyContainers = Boxes.GetNearbyContainers(__instance, AzuCraftyBoxesPlugin.mRange.Value);
                if (Boxes.CanItemBePulled(Utils.GetPrefabName(__instance.gameObject), fuelItem.name))
                {
                    foreach (IContainer c in nearbyContainers)
                    {
                        if (!c.ContainsItem(sharedName, 1, out int result))
                            continue;
                        if (!Boxes.CanItemBePulled(c.GetPrefabName(), fuelItem.name))
                        {
                            AzuCraftyBoxesPlugin.AzuCraftyBoxesLogger.LogIfReleaseAndDebugEnable(
                                $"(ShieldGeneratorOnAddFuelPatch) Container at {c.GetPosition()} has {result} {sharedName} but it's forbidden by config");
                            continue;
                        }

                        float currentFuel = __instance.GetFuel();
                        float neededFuel = __instance.m_maxFuel - currentFuel;
                        int available = result;
                        // Use cache manager for vanilla containers.
                        if (c is Container vanilla && ContainerInventoryCacheManager.Instance != null)
                        {
                            available = ContainerInventoryCacheManager.Instance.GetAggregatedItemCount(vanilla, canonicalKey);
                        }

                        int amount = pullAll ? (int)Mathf.Min(neededFuel, available) : 1;
                        AzuCraftyBoxesPlugin.AzuCraftyBoxesLogger.LogIfReleaseAndDebugEnable($"(ShieldGeneratorOnAddFuelPatch) Container at {c.GetPosition()} has {available} {sharedName}, taking {amount}");
                        c.RemoveItem(sharedName, amount);
                        c.Save();
                        for (int i = 0; i < amount; i++)
                        {
                            ___m_nview.InvokeRPC("RPC_AddFuel");
                        }

                        added += amount;
                        user.Message(MessageHud.MessageType.TopLeft, Localization.instance.Localize("$msg_added " + sharedName));
                        __result = false;
                        if (!pullAll || Mathf.CeilToInt(___m_nview.GetZDO().GetFloat(ZDOVars.s_fuel)) >= __instance.m_maxFuel)
                            return false;
                    }
                }
            }

            user.Message(MessageHud.MessageType.Center,
                added == 0 ? "$msg_noprocessableitems" : $"$msg_added {added} items");
            return __result;
        }
    }
}