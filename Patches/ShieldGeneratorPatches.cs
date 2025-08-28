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
            {
                return true;
            }

            if (AzuCraftyBoxesPlugin.fillAllModKey.Value.MainKey == KeyCode.None)
            {
                return true;
            }

            if (Player.m_localPlayer == null)
            {
                return true;
            }

            return !Player.m_localPlayer.m_hovering || Player.m_localPlayer.m_hovering.GetComponentInParent<ShieldGenerator>() != __instance;
        }

        internal static void UpdateAddFuelSwitchHoverText(ShieldGenerator __instance, ref string result)
        {
            double free = __instance.m_maxFuel - __instance.GetFuel();
            List<string> items = new();

            foreach (ItemDrop fuelItem in __instance.m_fuelItems)
            {
                string sharedName = fuelItem.m_itemData.m_shared.m_name;
                int inInv = Player.m_localPlayer?.m_inventory.CountItems(sharedName) ?? 0;
                int inContainers = 0;

                List<IContainer> nearbyContainers = Boxes.GetNearbyContainers(__instance, AzuCraftyBoxesPlugin.mRange.Value);
                foreach (IContainer c in nearbyContainers)
                {
                    if (Boxes.CanItemBePulled(Utils.GetPrefabName(__instance.gameObject), fuelItem.name))
                    {
                        c.ContainsItem(sharedName, 1, out int resultCount);
                        resultCount = Boxes.CheckAndDecrement(resultCount);
                        inContainers += resultCount;
                    }
                }

                if (inInv > 0)
                {
                    items.Add($"{inInv} {sharedName} in inventory");
                }

                if (inContainers > 0)
                {
                    items.Add($"{inContainers} {sharedName} in nearby containers");
                }

                if (free - inInv - inContainers > 0 && free < __instance.m_maxFuel)
                {
                    items.Add($"{free - inInv - inContainers} needed to fill");
                }
            }

            if (items.Count > 0)
            {
                result += Localization.instance.Localize($"\n[<b><color=yellow>{AzuCraftyBoxesPlugin.fillAllModKey.Value}</color> + <color=yellow>$KEY_Use</color></b>] Add {string.Join(" and ", items)}");
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
            if (MiscFunctions.ShouldPrevent()
                || item != null
                || inventory == null)
                return true;

            __result = true;

            int added = 0;

            if (__instance.GetFuel() > __instance.m_maxFuel - 1)
            {
                user.Message(MessageHud.MessageType.Center, "$msg_itsfull");
                __result = false;
                return false;
            }

            foreach (ItemDrop fuelItem in __instance.m_fuelItems)
            {
                string sharedName = fuelItem.m_itemData.m_shared.m_name;
                if (pullAll && inventory.HaveItem(sharedName))
                {
                    if (Boxes.CanItemBePulled(Utils.GetPrefabName(__instance.gameObject), fuelItem.name))
                    {
                        int amount = (int)Mathf.Min(__instance.m_maxFuel - __instance.GetFuel(), inventory.CountItems(sharedName));
                        inventory.RemoveItem(sharedName, amount);
                        for (int i = 0; i < amount; ++i)
                            ___m_nview.InvokeRPC("RPC_AddFuel");

                        added += amount;

                        user.Message(MessageHud.MessageType.TopLeft, Localization.instance.Localize("$msg_fireadding", sharedName));

                        __result = false;
                    }
                }

                List<IContainer> nearbyContainers = Boxes.GetNearbyContainers(__instance, AzuCraftyBoxesPlugin.mRange.Value);
                if (Boxes.CanItemBePulled(Utils.GetPrefabName(__instance.gameObject), fuelItem.name))
                {
                    foreach (IContainer c in nearbyContainers)
                    {
                        if (!c.ContainsItem(sharedName, 1, out int result)) continue;
                        result = Boxes.CheckAndDecrement(result);
                        if(result <= 0) continue;
                        if (!Boxes.CanItemBePulled(c.GetPrefabName(), fuelItem.name))
                        {
                            AzuCraftyBoxesPlugin.AzuCraftyBoxesLogger.LogIfReleaseAndDebugEnable($"(ShieldGeneratorOnAddFuelPatch) Container at {c.GetPosition()} has {result} {sharedName} but it's forbidden by config");
                            continue;
                        }

                        AzuCraftyBoxesPlugin.AzuCraftyBoxesLogger.LogIfReleaseAndDebugEnable($"Pull ALL is {pullAll}");
                        int amount = pullAll
                            ? (int)Mathf.Min(__instance.m_maxFuel - __instance.GetFuel(), result)
                            : 1;

                        AzuCraftyBoxesPlugin.AzuCraftyBoxesLogger.LogIfReleaseAndDebugEnable($"(ShieldGeneratorOnAddFuelPatch) Container at {c.GetPosition()} has {result} {sharedName}, taking {amount}");

                        c.RemoveItem(sharedName, amount);
                        c.Save();

                        for (int i = 0; i < amount; ++i)
                            ___m_nview.InvokeRPC("RPC_AddFuel");

                        added += amount;

                        user.Message(MessageHud.MessageType.TopLeft, "$msg_added " + sharedName);

                        __result = false;

                        if (!pullAll || Mathf.CeilToInt(___m_nview.GetZDO().GetFloat(ZDOVars.s_fuel)) >= __instance.m_maxFuel)
                            return false;
                    }
                }
            }

            user.Message(MessageHud.MessageType.Center, added == 0
                ? "$msg_noprocessableitems"
                : $"$msg_added {added} items");

            return __result;
        }
    }
}