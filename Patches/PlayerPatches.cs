using AzuCraftyBoxes.IContainers;
using AzuCraftyBoxes.Util.Functions;

namespace AzuCraftyBoxes.Patches;

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

[HarmonyPatch(typeof(Player), nameof(Player.HaveRequirementItems), new[] { typeof(Recipe), typeof(bool), typeof(int) })]
static class PlayerHaveRequirementsPatch
{
    static void Postfix(Player __instance, ref bool __result, Recipe piece, bool discover, int qualityLevel, HashSet<string> ___m_knownMaterial)
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
                if (!proceed)
                    continue;
                if (!InventoryGuiCollectRequirements.actualAmounts.TryGetValue(requirement, out int amount))
                {
                    amount = requirement.GetAmount(qualityLevel);
                }
                int invAmount = __instance.GetInventory().CountItems(requirement.m_resItem.m_itemData.m_shared.m_name);
                if (invAmount >= amount) continue;

                GameObject itemPrefab = MiscFunctions.GetItemPrefabFromGameObject(requirement.m_resItem, requirement.m_resItem.gameObject)!;
                requirement.m_resItem.m_itemData.m_dropPrefab = requirement.m_resItem.gameObject;
                if (itemPrefab == null)
                    continue;
                if (requirement.m_resItem.m_itemData.m_dropPrefab == null)
                {
                    AzuCraftyBoxesPlugin.AzuCraftyBoxesLogger.LogWarning(
                        $"Skipping {requirement.m_resItem?.gameObject.name} also known as " +
                        $"{Localization.instance.Localize(requirement.m_resItem?.m_itemData?.m_shared?.m_name)} is listed as a " +
                        $"requirement but cannot be found in the ObjectDB in order to populate the m_dropPrefab like the ItemDrop " +
                        $"script expects. Value was null. This will cause issues when attempting to drop the item on the ground, or " +
                        $"any mod that patches recipes expecting this value to be populated.");
                    continue;
                }

                string itemPrefabName = Utils.GetPrefabName(requirement.m_resItem.name);
                string sharedName = requirement.m_resItem.m_itemData.m_shared.m_name;
                foreach (IContainer c in nearbyContainers)
                {
                    if (requirement.m_resItem?.m_itemData?.m_dropPrefab == null)
                        continue;
                    if (Boxes.CanItemBePulled(c.GetPrefabName(), itemPrefabName))
                    {
                        c.ContainsItem(sharedName, 1, out int result);
                        invAmount += result;
                    }
                }

                if (piece.m_requireOnlyOneIngredient)
                {
                    if (invAmount < amount) continue;
                    //cando = true;
                    if (__instance.m_knownMaterial.Contains(requirement.m_resItem.m_itemData.m_shared.m_name))
                    {
                        cando = true;
                    }
                }
                else if (invAmount < amount)
                    return;
                else
                {
                    cando = true;
                }
            }
            if(cando)
                __result = true;
        }
        catch
        {
        }
    }
}

[HarmonyPatch(typeof(Player), nameof(Player.HaveRequirements), typeof(Piece), typeof(Player.RequirementMode))]
static class HaveRequirementsPatch2
{
    [HarmonyWrapSafe]
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
                else if (!CraftingStation.HaveBuildStationInRange(piece.m_craftingStation.m_name,
                             __instance.transform.position))
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
                        case Player.RequirementMode.CanAlmostBuild when __instance.GetInventory()
                            .HaveItem(requirement.m_resItem.m_itemData.m_shared.m_name):
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
                            int hasItems = __instance.GetInventory()
                                .CountItems(requirement.m_resItem.m_itemData.m_shared.m_name);
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
    static bool Prefix(Player __instance, Piece.Requirement[] requirements, int qualityLevel, int itemQuality = -1)
    {
        try
        {
            if (AzuCraftyBoxesPlugin.ModEnabled.Value == AzuCraftyBoxesPlugin.Toggle.Off || !MiscFunctions.AllowByKey())
                return true;

            Inventory pInventory = __instance.GetInventory();
            List<IContainer> nearbyContainers = Boxes.GetNearbyContainers(__instance, AzuCraftyBoxesPlugin.mRange.Value);
            MiscFunctions.ProcessRequirements(requirements, qualityLevel, pInventory, nearbyContainers, itemQuality);
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