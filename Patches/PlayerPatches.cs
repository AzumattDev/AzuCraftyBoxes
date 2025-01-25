using AzuCraftyBoxes.IContainers;
using AzuCraftyBoxes.Util.Functions;

namespace AzuCraftyBoxes.Patches;

[HarmonyPatch(typeof(Player), nameof(Player.SetCraftingStation))]
static class CacheCurrentCraftingStationPrefabName
{
    public static string CachedStationName = string.Empty;

    static void Postfix(Player __instance, CraftingStation station)
    {
        CachedStationName = station ? Utils.GetPrefabName(station.gameObject) : string.Empty;
    }
}

[HarmonyPatch(typeof(Player), nameof(Player.UpdateKnownRecipesList))]
static class UpdateKnownRecipesListPatch
{
    static void Prefix()
    {
        AzuCraftyBoxesPlugin.skip = true;
    }

    static void Postfix()
    {
        AzuCraftyBoxesPlugin.skip = false;
    }
}

[HarmonyPatch(typeof(Player), nameof(Player.HaveRequirementItems), new[] { typeof(Recipe), typeof(bool), typeof(int), typeof(int) })]
static class PlayerHaveRequirementsPatch
{
    [HarmonyPriority(Priority.VeryHigh)]
    static void Postfix(Player __instance, ref bool __result, Recipe piece, bool discover, int qualityLevel, HashSet<string> ___m_knownMaterial, int amount = 1)
    {
        try
        {
            if (AzuCraftyBoxesPlugin.ModEnabled.Value == AzuCraftyBoxesPlugin.Toggle.Off || __result || discover || !MiscFunctions.AllowByKey())
                return;

            List<IContainer> nearbyContainers = Boxes.GetNearbyContainers(__instance, AzuCraftyBoxesPlugin.mRange.Value);
            if (nearbyContainers.Count == 0)
                return;

            bool cando = false;
            foreach (Piece.Requirement requirement in piece.m_resources)
            {
                if (!requirement.m_resItem) continue;
                bool proceed = MiscFunctions.CheckItemDropIntegrity(requirement.m_resItem);
                if (!proceed) continue;

                int requiredAmount = requirement.GetAmount(qualityLevel) * amount;
                int availableAmount = 0;
                GameObject itemPrefab = MiscFunctions.GetItemPrefabFromGameObject(requirement.m_resItem, requirement.m_resItem.gameObject)!;
                requirement.m_resItem.m_itemData.m_dropPrefab = requirement.m_resItem.gameObject;
                if (itemPrefab == null)
                    continue;

                // Tally up the items by quality level
                for (int quality = 1; quality <= requirement.m_resItem.m_itemData.m_shared.m_maxQuality; ++quality)
                {
                    int invAmount = __instance.GetInventory().CountItems(requirement.m_resItem.m_itemData.m_shared.m_name, quality);
                    if (invAmount > availableAmount) availableAmount = invAmount;

                    string itemPrefabName = Utils.GetPrefabName(requirement.m_resItem.name);
                    string sharedName = requirement.m_resItem.m_itemData.m_shared.m_name;
                    foreach (IContainer container in nearbyContainers)
                    {
                        if (requirement.m_resItem?.m_itemData?.m_dropPrefab == null)
                            continue;
                        var containerPrefabName = container.GetPrefabName();
                        if (Boxes.CanItemBePulled(containerPrefabName, itemPrefabName))
                        {
                            container.ContainsItem(sharedName, quality, out int containerAmount);
                            availableAmount = Boxes.CheckAndDecrement(Math.Max(availableAmount, containerAmount));
                        }
                    }
                }

                if (piece.m_requireOnlyOneIngredient)
                {
                    if (availableAmount >= requiredAmount)
                    {
                        if (__instance.m_knownMaterial.Contains(requirement.m_resItem.m_itemData.m_shared.m_name))
                        {
                            cando = true;
                        }
                    }
                }
                else if (availableAmount < requiredAmount)
                {
                    return;
                }
                else
                {
                    cando = true;
                }
            }

            if (cando)
                __result = true;
        }
        catch
        {
            // Handle exceptions as necessary
        }
    }
}

[HarmonyPatch(typeof(Player), nameof(Player.HaveRequirements), typeof(Recipe), typeof(bool), typeof(int), typeof(int))]
static class PlayerHaveRequirementsPatchRBoolInt
{
    static void Postfix(Player __instance, Recipe recipe, bool discover, int qualityLevel, int amount, ref bool __result)
    {
        if (discover)
        {
            if (recipe.m_craftingStation && !__instance.KnowStationLevel(recipe.m_craftingStation.m_name, recipe.m_minStationLevel))
                return;
        }
        else if (!__instance.RequiredCraftingStation(recipe, qualityLevel, true))
            return;

        bool test = (recipe.m_item.m_itemData.m_shared.m_dlc.Length <= 0 || DLCMan.instance.IsDLCInstalled(recipe.m_item.m_itemData.m_shared.m_dlc)) && HaveRequirementItems(__instance, recipe, discover, qualityLevel, amount);
        if (test && !__result)
            __result = true;
    }

    public static bool HaveRequirementItems(Player p, Recipe piece, bool discover, int qualityLevel, int amountVanilla)
    {
        if (p == null)
            return false;
        foreach (Piece.Requirement resource in piece.m_resources)
        {
            if (resource.m_resItem)
            {
                if (discover)
                {
                    if (resource.m_amount > 0)
                    {
                        if (piece.m_requireOnlyOneIngredient)
                        {
                            if (p.m_knownMaterial.Contains(resource.m_resItem.m_itemData.m_shared.m_name))
                                return true;
                        }
                        else if (!p.m_knownMaterial.Contains(resource.m_resItem.m_itemData.m_shared.m_name))
                            return false;
                    }
                }
                else
                {
                    List<IContainer> nearbyContainers = Boxes.GetNearbyContainers(p, AzuCraftyBoxesPlugin.mRange.Value);
                    int amount = resource.GetAmount(qualityLevel) * amountVanilla;
                    int num = p.m_inventory.CountItems(resource.m_resItem.m_itemData.m_shared.m_name);

                    foreach (IContainer c in nearbyContainers)
                    {
                        resource.m_resItem.m_itemData.m_dropPrefab = resource.m_resItem.gameObject;
                        if (resource.m_resItem.m_itemData.m_dropPrefab == null)
                            continue;
                        string itemPrefabName = resource.m_resItem.name;
                        string sharedName = resource.m_resItem.m_itemData.m_shared.m_name;
                        bool canItemBePulled = false;
                        if (c == null) continue;
                        if (!string.IsNullOrWhiteSpace(c.GetPrefabName()))
                        {
                            canItemBePulled = Boxes.CanItemBePulled(c.GetPrefabName(), itemPrefabName);
                        }

                        if (canItemBePulled)
                        {
                            try
                            {
                                c.ContainsItem(sharedName, 1, out int result);
                                result = Boxes.CheckAndDecrement(result);
                                num += result;
                                if (num >= amount)
                                {
                                    break;
                                }
                            }
                            catch
                            {
// ignored
                            }
                        }
                    }

                    if (piece.m_requireOnlyOneIngredient)
                    {
                        if (num >= amount)
                            return true;
                    }
                    else if (num < amount)
                        return false;
                }
            }
        }

        return !piece.m_requireOnlyOneIngredient;
    }
}

[HarmonyPatch(typeof(Player), nameof(Player.HaveRequirements), typeof(Piece), typeof(Player.RequirementMode))]
static class HaveRequirementsPatch2
{
    [HarmonyWrapSafe]
    [HarmonyPriority(Priority.VeryHigh)]
    static void Postfix(Player __instance, ref bool __result, Piece piece, Player.RequirementMode mode, HashSet<string> ___m_knownMaterial, Dictionary<string, int> ___m_knownStations)
    {
        try
        {
            if (AzuCraftyBoxesPlugin.ModEnabled.Value == AzuCraftyBoxesPlugin.Toggle.Off || __result || AzuCraftyBoxesPlugin.skip || __instance?.transform?.position == null || !MiscFunctions.AllowByKey())
                return;
            if (piece == null)
                return;

            if (piece.m_craftingStation)
            {
                if (mode is Player.RequirementMode.IsKnown or Player.RequirementMode.CanAlmostBuild)
                {
                    if (!___m_knownStations.ContainsKey(piece.m_craftingStation.m_name))
                    {
                        return;
                    }
                }
                else if (!CraftingStation.HaveBuildStationInRange(piece.m_craftingStation.m_name, __instance.transform.position))
                {
                    return;
                }
            }

            if (piece.m_dlc.Length > 0 && !DLCMan.instance.IsDLCInstalled(piece.m_dlc))
            {
                return;
            }

            List<IContainer> nearbyContainers = Boxes.GetNearbyContainers(__instance, AzuCraftyBoxesPlugin.mRange.Value);

            foreach (Piece.Requirement requirement in piece.m_resources)
            {
                if (requirement.m_resItem == null)
                    continue;
                if (requirement.m_resItem && requirement.m_amount > 0)
                {
                    if (!MiscFunctions.CheckItemDropIntegrity(requirement.m_resItem))
                        continue;
                    requirement.m_resItem.m_itemData.m_dropPrefab = requirement.m_resItem.gameObject;
                    if (requirement.m_resItem.m_itemData.m_dropPrefab == null)
                        continue;
                    switch (mode)
                    {
                        case Player.RequirementMode.IsKnown
                            when !___m_knownMaterial.Contains(requirement.m_resItem.m_itemData.m_shared.m_name):
                            return;
                        case Player.RequirementMode.CanAlmostBuild when __instance.GetInventory().HaveItem(requirement.m_resItem.m_itemData.m_shared.m_name):
                            continue;
                        case Player.RequirementMode.CanAlmostBuild:
                        {
                            bool hasItem = false;
                            string sharedName = requirement.m_resItem.m_itemData.m_shared.m_name;
                            foreach (IContainer c in nearbyContainers)
                            {
                                requirement.m_resItem.m_itemData.m_dropPrefab = requirement.m_resItem.gameObject;
                                if (requirement.m_resItem.m_itemData.m_dropPrefab == null)
                                    continue;
                                string itemPrefabName = Utils.GetPrefabName(requirement.m_resItem.m_itemData.m_dropPrefab);
                                bool canItemBePulled = Boxes.CanItemBePulled(c.GetPrefabName(), itemPrefabName);

                                if (canItemBePulled && c.ContainsItem(sharedName, 1, out _))
                                {
                                    hasItem = true;
                                    break;
                                }
                            }

                            if (!hasItem)
                                return;
                            break;
                        }
                        case Player.RequirementMode.CanBuild when __instance.GetInventory().CountItems(requirement.m_resItem.m_itemData.m_shared.m_name) < requirement.m_amount:
                        {
                            int hasItems = __instance.GetInventory().CountItems(requirement.m_resItem.m_itemData.m_shared.m_name);
                            foreach (IContainer c in nearbyContainers)
                            {
                                requirement.m_resItem.m_itemData.m_dropPrefab = requirement.m_resItem.gameObject;
                                if (requirement.m_resItem.m_itemData.m_dropPrefab == null)
                                    continue;
                                string itemPrefabName = requirement.m_resItem.name;
                                string sharedName = requirement.m_resItem.m_itemData.m_shared.m_name;
                                bool canItemBePulled = Boxes.CanItemBePulled(c.GetPrefabName(), itemPrefabName);

                                if (canItemBePulled)
                                {
                                    try
                                    {
                                        c.ContainsItem(sharedName, 1, out int result);
                                        result = Boxes.CheckAndDecrement(result);
                                        hasItems += result;
                                        if (hasItems >= requirement.m_amount)
                                        {
                                            break;
                                        }
                                    }
                                    catch
                                    {
// ignored
                                    }
                                }
                            }

                            if (hasItems < requirement.m_amount)
                                return;
                            break;
                        }
                    }
                }
            }

            __result = true;
        }
        catch
        {
        }
    }
}

[HarmonyPatch(typeof(Player), nameof(Player.ConsumeResources))]
static class ConsumeResourcesPatch
{
    static bool Prefix(Player __instance, Piece.Requirement[] requirements, int qualityLevel, int itemQuality = -1, int multiplier = 1)
    {
        try
        {
            if (AzuCraftyBoxesPlugin.ModEnabled.Value == AzuCraftyBoxesPlugin.Toggle.Off || !MiscFunctions.AllowByKey())
                return true;

            Inventory pInventory = __instance.GetInventory();
            List<IContainer> nearbyContainers = Boxes.GetNearbyContainers(__instance, AzuCraftyBoxesPlugin.mRange.Value);
            MiscFunctions.ProcessRequirements(requirements, qualityLevel, pInventory, nearbyContainers, itemQuality, multiplier);
        }
        catch (Exception ex)
        {
            AzuCraftyBoxesPlugin.AzuCraftyBoxesLogger.LogError($"Error in ConsumeResourcesPatch: {ex.Message}");
        }

        return false;
    }
}

[HarmonyPatch(typeof(Game), nameof(Game.Logout))]
static class GameLogoutPatch
{
    static void Prefix(Game __instance)
    {
        AzuCraftyBoxesPlugin.lastPosition = Vector3.zero;
    }
}