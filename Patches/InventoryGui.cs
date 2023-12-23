using System.Collections.Generic;
using System.Linq;
using AzuCraftyBoxes.Util;
using AzuCraftyBoxes.Util.Functions;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AzuCraftyBoxes.Patches;

/*[HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.SetupRequirement))]
static class InventoryGuiSetupRequirementPatch
{
    static void Postfix(InventoryGui __instance, Transform elementRoot,
        Piece.Requirement req,
        Player player,
        bool craft,
        int quality)
    {
        if (AzuCraftyBoxesPlugin.ModEnabled.Value == AzuCraftyBoxesPlugin.Toggle.Off || !MiscFunctions.AllowByKey())
            return;
        if (req.m_resItem == null) return;
        int invAmount = player.GetInventory().CountItems(req.m_resItem.m_itemData.m_shared.m_name);
        int amount = req.GetAmount(quality);
        if (amount <= 0)
        {
            return;
        }

        Text text = elementRoot.transform.Find("res_amount").GetComponent<Text>();
        if (invAmount < amount)
        {
            List<Container> nearbyContainers =
                Boxes.GetNearbyContainers(Player.m_localPlayer, AzuCraftyBoxesPlugin.mRange.Value);
            invAmount +=
                nearbyContainers.Sum(c => c.GetInventory().CountItems(req.m_resItem.m_itemData.m_shared.m_name));

            if (invAmount >= amount)
                text.color = ((Mathf.Sin(Time.time * 10f) > 0f)
                    ? AzuCraftyBoxesPlugin.flashColor.Value
                    : AzuCraftyBoxesPlugin.unFlashColor.Value);
        }

        text.text = AzuCraftyBoxesPlugin.resourceString.Value.Trim().Length > 0
            ? string.Format(AzuCraftyBoxesPlugin.resourceString.Value, invAmount, amount)
            : amount.ToString();
        text.resizeTextForBestFit = true;
    }
}*/

[HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.SetupRequirement))]
static class InventoryGuiSetupRequirementPatch
{
    static void Postfix(InventoryGui __instance, Transform elementRoot,
        Piece.Requirement req,
        Player player,
        bool craft,
        int quality)
    {
        if (AzuCraftyBoxesPlugin.ModEnabled.Value == AzuCraftyBoxesPlugin.Toggle.Off || !MiscFunctions.AllowByKey())
        {
            return;
        }

        if(req == null) { return; }
        if (req.m_resItem == null) { return; }
        if (req.m_resItem.m_itemData == null) { return; }
        if(req.m_resItem.m_itemData.m_shared == null){ return; }
        req.m_resItem.m_itemData.m_dropPrefab = req.m_resItem.gameObject;
        if(req.m_resItem.m_itemData.m_dropPrefab == null){ return; }
        int invAmount = player.GetInventory().CountItems(req.m_resItem.m_itemData.m_shared.m_name);
        int amount = req.GetAmount(quality);
        if (amount <= 0)
        {
            return;
        }
        TextMeshProUGUI text = elementRoot.transform.Find("res_amount").GetComponent<TextMeshProUGUI>();
        if(text == null) return;
        if (invAmount < amount)
        {
            List<Container> nearbyContainers = Boxes.GetNearbyContainers(Player.m_localPlayer, AzuCraftyBoxesPlugin.mRange.Value);
            GameObject itemPrefab = ObjectDB.instance.GetItemPrefab(req.m_resItem.GetPrefabName(req.m_resItem.gameObject.name));
            if (itemPrefab == null) {return;}
            req.m_resItem.m_itemData.m_dropPrefab = itemPrefab;
            foreach (var container in nearbyContainers)
            {
                try
                {
                    string containerPrefabName = Utils.GetPrefabName(container.gameObject);
                    if (req.m_resItem.m_itemData.m_dropPrefab == null)
                        continue;
                    string itemPrefabName = Utils.GetPrefabName(req.m_resItem.m_itemData.m_dropPrefab);

                    if (Boxes.CanItemBePulled(containerPrefabName, itemPrefabName))
                    {
                        invAmount += container.GetInventory().CountItems(req.m_resItem.m_itemData.m_shared.m_name);
                    }
                }
                catch (System.Exception e)
                {
                    // ignored
                }
            }
            if (invAmount >= amount)
            {
                text.color = ((Mathf.Sin(Time.time * 10f) > 0f)
                    ? AzuCraftyBoxesPlugin.flashColor.Value
                    : AzuCraftyBoxesPlugin.unFlashColor.Value);
            }
        }
        text.text = AzuCraftyBoxesPlugin.resourceString.Value.Trim().Length > 0
            ? string.Format(AzuCraftyBoxesPlugin.resourceString.Value, invAmount, amount)
            : amount.ToString();
    }
}

/*[HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.OnCraftPressed))]
static class InventoryGuiOnCraftPressedPatch
{
    static bool Prefix(InventoryGui __instance, KeyValuePair<Recipe, ItemDrop.ItemData> ___m_selectedRecipe,
        ItemDrop.ItemData ___m_craftUpgradeItem)
    {
        if (AzuCraftyBoxesPlugin.ModEnabled.Value == AzuCraftyBoxesPlugin.Toggle.Off || !MiscFunctions.AllowByKey() ||
            !AzuCraftyBoxesPlugin.pullItemsKey.Value.IsPressed() || ___m_selectedRecipe.Key == null)
            return true;

        int qualityLevel = (___m_craftUpgradeItem != null) ? (___m_craftUpgradeItem.m_quality + 1) : 1;
        if (qualityLevel > ___m_selectedRecipe.Key.m_item.m_itemData.m_shared.m_maxQuality)
        {
            return true;
        }

        AzuCraftyBoxesPlugin.AzuCraftyBoxesLogger.LogDebug(
            $"(InventoryGuiOnCraftPressedPatch) Pulling resources to player inventory for crafting item {___m_selectedRecipe.Key.m_item.m_itemData.m_shared.m_name}");
        Boxes.PullResources(Player.m_localPlayer, ___m_selectedRecipe.Key.m_resources, qualityLevel);

        return false;
    }
}*/