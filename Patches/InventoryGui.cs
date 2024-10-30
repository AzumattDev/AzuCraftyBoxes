using AzuCraftyBoxes.IContainers;
using AzuCraftyBoxes.Util.Functions;
using TMPro;

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

[HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.SetupRequirementList))]
static class InventoryGuiCollectRequirements
{
    public static Dictionary<Piece.Requirement, int> actualAmounts = new();

    private static void Prefix()
    {
        actualAmounts.Clear();
    }
}

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
        if (!int.TryParse(text.text, out int amount))
        {
            amount = req.GetAmount(quality);
        }

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
                    string sharedName = req.m_resItem.m_itemData.m_shared.m_name;

                    if (Boxes.CanItemBePulled(containerPrefabName, itemPrefabName))
                    {
                        container.ContainsItem(sharedName, 1, out int result);
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
                InventoryGuiCollectRequirements.actualAmounts[req] = amount;
            }
        }

        text.text = AzuCraftyBoxesPlugin.resourceString.Value.Trim().Length > 0
            ? string.Format(AzuCraftyBoxesPlugin.resourceString.Value, invAmount, amount)
            : amount.ToString();
    }
}

/*
[HarmonyPatch(typeof(Recipe), nameof(Recipe.GetAmount))]
public static class RecipeGetAmountPatch
{
    [HarmonyEmitIL]
    static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator il)
    {
        var matcher = new CodeMatcher(instructions);

        // Find the location where the player's inventory is checked and singleReqItem might be set
        matcher.MatchForward(false, new CodeMatch(OpCodes.Callvirt, AccessTools.Method(typeof(Player), nameof(Player.GetFirstRequiredItem))));

        if (matcher.IsInvalid)
        {
            UnityEngine.Debug.LogError("RecipeGetAmountPatch: Could not find the point after checking player inventory in Recipe.GetAmount.");
            return instructions;
        }

        matcher.Advance(1);

        // Insert the call to CheckNearbyContainers after the inventory check
        matcher.InsertAndAdvance(
            new CodeInstruction(OpCodes.Ldarg_0), // Load 'this' (Recipe instance)
            new CodeInstruction(OpCodes.Ldarg_1), // Load 'quality'
            new CodeInstruction(OpCodes.Ldarg_2), // Load 'need'
            new CodeInstruction(OpCodes.Ldarg_3), // Load 'singleReqItem'
            new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(RecipeGetAmountPatch), nameof(CheckNearbyContainers)))
        );
        AzuCraftyBoxesPlugin.AzuCraftyBoxesLogger.LogInfo(matcher.Instruction);
        return matcher.InstructionEnumeration();
    }


    private static void CheckNearbyContainers(Recipe recipe, int quality, ref int need, ref ItemDrop.ItemData singleReqItem)
    {
        if (singleReqItem == null)
        {
            UnityEngine.Debug.Log("singleReqItem is null, checking nearby containers.");
            if(Player.m_localPlayer == null)
            {
                UnityEngine.Debug.Log("Player.m_localPlayer is null, returning.");
                return;
            }
            List<IContainer> nearbyContainers = Boxes.GetNearbyContainers(Player.m_localPlayer, AzuCraftyBoxesPlugin.mRange.Value);
            if (nearbyContainers.Count == 0)
                return;
            foreach (Piece.Requirement requirement in recipe.m_resources)
            {
                if (!requirement.m_resItem) continue;
                bool proceed = MiscFunctions.CheckItemDropIntegrity(requirement.m_resItem);
                if (!proceed)
                    continue;
                int amount = requirement.GetAmount(quality);
                int invAmount = 0;
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
                    if (c == null)
                        continue;

                    string containerPrefabName = c.GetPrefabName();
                    if (requirement.m_resItem?.m_itemData?.m_dropPrefab == null)
                        continue;
                    AzuCraftyBoxesPlugin.AzuCraftyBoxesLogger.LogIfReleaseAndDebugEnable($"(CheckNearbyContainers) Checking container {containerPrefabName} for item {itemPrefabName}");

                    if (Boxes.CanItemBePulled(c.GetPrefabName(), itemPrefabName))
                    {
                        c.ContainsItem(itemPrefabName, 1, out int result);
                        if (result > 0)
                        {
                            // Update the singleReqItem if the required item is found
                            singleReqItem = requirement.m_resItem.m_itemData; // Or get the item data in the correct way
                            AzuCraftyBoxesPlugin.AzuCraftyBoxesLogger.LogIfReleaseAndDebugEnable($"(CheckNearbyContainers) Found required item {requirement.m_resItem.name} in container {containerPrefabName}");
                            need += result;
                            break; // Stop checking after finding the required item
                        }

                        AzuCraftyBoxesPlugin.AzuCraftyBoxesLogger.LogIfReleaseAndDebugEnable($"(CheckNearbyContainers) Required item {itemPrefabName} not found in container {containerPrefabName}");
                    }
                }
            }
        }
    }
}
*/

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

        AzuCraftyBoxesPlugin.AzuCraftyBoxesLogger.LogIfReleaseAndDebugEnable(
            $"(InventoryGuiOnCraftPressedPatch) Pulling resources to player inventory for crafting item {___m_selectedRecipe.Key.m_item.m_itemData.m_shared.m_name}");
        Boxes.PullResources(Player.m_localPlayer, ___m_selectedRecipe.Key.m_resources, qualityLevel);

        return false;
    }
}*/