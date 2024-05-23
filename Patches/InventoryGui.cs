﻿using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using AzuCraftyBoxes.IContainers;
using AzuCraftyBoxes.Util;
using AzuCraftyBoxes.Util.Functions;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AzuCraftyBoxes.Patches;

[HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.SetupRequirement))]
static class InventoryGuiSetupRequirementPatch
{
    static void Postfix(InventoryGui __instance, Transform elementRoot, Piece.Requirement req, Player player, bool craft, int quality)
    {
        if (AzuCraftyBoxesPlugin.ModEnabled.Value == AzuCraftyBoxesPlugin.Toggle.Off || !MiscFunctions.AllowByKey())
        {
            return;
        }

        if (req == null)
        {
            return;
        }

        if (req.m_resItem == null)
        {
            return;
        }

        if (req.m_resItem.m_itemData == null)
        {
            return;
        }

        if (req.m_resItem.m_itemData.m_shared == null)
        {
            return;
        }

        req.m_resItem.m_itemData.m_dropPrefab = req.m_resItem.gameObject;
        if (req.m_resItem.m_itemData.m_dropPrefab == null)
        {
            return;
        }

        int invAmount = player.GetInventory().CountItems(req.m_resItem.m_itemData.m_shared.m_name);
        TextMeshProUGUI text = elementRoot.transform.Find("res_amount").GetComponent<TextMeshProUGUI>();
        if (text == null) return;
        int amount = int.Parse(text.text);
        /*if (MiscFunctions.GetCurrentCraftAmountMethod != null)
        {
            if (craft)
            {
                amount *= (int)MiscFunctions.GetCurrentCraftAmountMethod.Invoke(null, null);
            }
        }*/
        if (amount <= 0)
        {
            return;
        }


        if (invAmount < amount)
        {
            List<IContainer> nearbyContainers = Boxes.GetNearbyContainers(Player.m_localPlayer, AzuCraftyBoxesPlugin.mRange.Value);
            GameObject itemPrefab = ObjectDB.instance.GetItemPrefab(req.m_resItem.GetPrefabName(req.m_resItem.gameObject.name));
            if (itemPrefab == null)
            {
                return;
            }

            req.m_resItem.m_itemData.m_dropPrefab = itemPrefab;
            foreach (IContainer? container in nearbyContainers)
            {
                try
                {
                    string containerPrefabName = container.GetPrefabName();
                    if (req.m_resItem.m_itemData.m_dropPrefab == null)
                        continue;
                    string itemPrefabName = req.m_resItem.name;

                    if (Boxes.CanItemBePulled(containerPrefabName, itemPrefabName))
                    {
                        container.ContainsItem(itemPrefabName, 1, out int result);
                        invAmount += result;
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