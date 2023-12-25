using System;
using System.Collections.Generic;
using System.Linq;
using AzuCraftyBoxes.IContainers;
using AzuCraftyBoxes.Util;
using AzuCraftyBoxes.Util.Functions;
using HarmonyLib;
using UnityEngine;

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

/*[HarmonyPatch(typeof(Player), nameof(Player.UpdatePlacement))]
static class UpdatePlacementPatch
{
    static bool Prefix(Player __instance, bool takeInput, float dt, PieceTable ___m_buildPieces,
        GameObject ___m_placementGhost)
    {
        if (AzuCraftyBoxesPlugin.ModEnabled.Value == AzuCraftyBoxesPlugin.Toggle.Off || !MiscFunctions.AllowByKey() ||
            !AzuCraftyBoxesPlugin.pullItemsKey.Value.IsPressed() || !__instance.InPlaceMode() ||
            !takeInput || Hud.IsPieceSelectionVisible())
        {
            return true;
        }

        if (!ZInput.GetButtonDown("Attack") && !ZInput.GetButtonDown("JoyPlace")) return true;
        Piece selectedPiece = ___m_buildPieces.GetSelectedPiece();
        if (selectedPiece == null) return false;
        if (selectedPiece.m_repairPiece)
            return true;
        if (___m_placementGhost == null) return false;
        Player.PlacementStatus placementStatus = __instance.m_placementStatus;
        if (placementStatus != 0) return false;
        AzuCraftyBoxesPlugin.AzuCraftyBoxesLogger.LogDebug(
            $"(UpdatePlacementPatch) Pulling resources to player inventory for piece {selectedPiece.name}");
        Boxes.PullResources(__instance, selectedPiece.m_resources, 0);

        return false;
    }
}*/

[HarmonyPatch(typeof(Player), nameof(Player.HaveRequirementItems), new[] { typeof(Recipe), typeof(bool), typeof(int) })]
static class PlayerHaveRequirementsPatch
{
    static void Postfix(Player __instance, ref bool __result, Recipe piece, bool discover,
        int qualityLevel, HashSet<string> ___m_knownMaterial)
    {
        try
        {
            if (AzuCraftyBoxesPlugin.ModEnabled.Value == AzuCraftyBoxesPlugin.Toggle.Off || __result || discover ||
                !MiscFunctions.AllowByKey())
                return;
            List<IContainer> nearbyContainers = Boxes.GetNearbyContainers(__instance, AzuCraftyBoxesPlugin.mRange.Value);
            if (nearbyContainers.Count == 0)
                return;
            foreach (Piece.Requirement requirement in piece.m_resources)
            {
                if (!requirement.m_resItem) continue;
                var proceed = MiscFunctions.CheckItemDropIntegrity(requirement.m_resItem);
                if (!proceed)
                    continue;
                int amount = requirement.GetAmount(qualityLevel);
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
                foreach (IContainer c in nearbyContainers)
                {
                    if (requirement.m_resItem?.m_itemData?.m_dropPrefab == null)
                        continue;
                    if (Boxes.CanItemBePulled(c.GetPrefabName(), itemPrefabName))
                    {
                        c.ContainsItem(itemPrefabName, 1, out int result);
                        invAmount += result;
                    }
                }

                if (invAmount < amount)
                    return;
            }

            __result = true;
        } catch {}
    }
}

[HarmonyPatch(typeof(Player), nameof(Player.HaveRequirements), typeof(Piece), typeof(Player.RequirementMode))]
static class HaveRequirementsPatch2
{
    [HarmonyWrapSafe]
    static void Postfix(Player __instance, ref bool __result, Piece piece, Player.RequirementMode mode,
        HashSet<string> ___m_knownMaterial, Dictionary<string, int> ___m_knownStations)
    {
        try
        {
            if (AzuCraftyBoxesPlugin.ModEnabled.Value == AzuCraftyBoxesPlugin.Toggle.Off || __result ||
                AzuCraftyBoxesPlugin.skip || __instance?.transform?.position == null ||
                !MiscFunctions.AllowByKey())
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
                            string resPrefabName = requirement.m_resItem.name;
                            foreach (IContainer c in nearbyContainers)
                            {
                                requirement.m_resItem.m_itemData.m_dropPrefab = requirement.m_resItem.gameObject;
                                if (requirement.m_resItem.m_itemData.m_dropPrefab == null)
                                    continue;
                                string itemPrefabName = Utils.GetPrefabName(requirement.m_resItem.m_itemData.m_dropPrefab);
                                bool canItemBePulled = Boxes.CanItemBePulled(c.GetPrefabName(), itemPrefabName);

                                if (canItemBePulled && c.ContainsItem(resPrefabName,1,out _))
                                {
                                    hasItem = true;
                                    break;
                                }
                            }

                            if (!hasItem)
                                return;
                            break;
                        }
                        case Player.RequirementMode.CanBuild
                            when __instance.GetInventory()
                                     .CountItems(requirement.m_resItem.m_itemData.m_shared.m_name) <
                                 requirement.m_amount:
                        {
                            int hasItems = __instance.GetInventory()
                                .CountItems(requirement.m_resItem.m_itemData.m_shared.m_name);
                            foreach (IContainer c in nearbyContainers)
                            {
                                requirement.m_resItem.m_itemData.m_dropPrefab = requirement.m_resItem.gameObject;
                                if (requirement.m_resItem.m_itemData.m_dropPrefab == null)
                                    continue;
                                string itemPrefabName = requirement.m_resItem.name;
                                bool canItemBePulled = Boxes.CanItemBePulled(c.GetPrefabName(), itemPrefabName);

                                if (canItemBePulled)
                                {
                                    try
                                    {
                                        c.ContainsItem(itemPrefabName, 1, out int result);
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
        } catch {}
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

[HarmonyPatch(typeof(Game),nameof(Game.Logout))]
static class GameLogoutPatch
{
    static void Prefix(Game __instance)
    {
        AzuCraftyBoxesPlugin.lastPosition = Vector3.zero;
    }
}