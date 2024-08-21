/*using System.Reflection.Emit;
using AzuCraftyBoxes.IContainers;
using AzuCraftyBoxes.Util.Functions;

namespace AzuCraftyBoxes.Patches;

[HarmonyPatch(typeof(Recipe), nameof(Recipe.GetAmount))]
public static class RecipeGetAmountTranspiler
{
    private static readonly MethodInfo MethodPlayerGetFirstRequiredItem = AccessTools.Method(typeof(Player), nameof(Player.GetFirstRequiredItem));

    private static readonly MethodInfo MethodGetFirstRequiredItemFromNearbyChests = AccessTools.Method(typeof(RecipeGetAmountTranspiler), nameof(GetFirstRequiredItem));

    [UsedImplicitly]
    [HarmonyPriority(Priority.VeryHigh)]
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        AzuCraftyBoxesPlugin.AzuCraftyBoxesLogger.LogWarning("Transpiling Recipe::GetAmount");
        List<CodeInstruction> il = instructions.ToList();
        for (int i = 0; i < il.Count; ++i)
        {
            if (il[i].Calls(MethodPlayerGetFirstRequiredItem))
            {
                il[i] = new CodeInstruction(OpCodes.Call, MethodGetFirstRequiredItemFromNearbyChests);
                return il.AsEnumerable();
            }
        }

        AzuCraftyBoxesPlugin.AzuCraftyBoxesLogger.LogError("Couldn't transpile `Recipe.GetAmount` like expected");
        return il.AsEnumerable();
    }

    private static ItemDrop.ItemData GetFirstRequiredItem(Player player, Inventory inventory, Recipe recipe, int qualityLevel, out int amount, out int extraAmount)
    {
        ItemDrop.ItemData? result = player.GetFirstRequiredItem(inventory, recipe, qualityLevel, out amount, out extraAmount);
        if (result != null)
        {
            return result;
        }

        Piece.Requirement[]? requirements = recipe.m_resources;
        foreach (IContainer? chest in Boxes.GetNearbyContainers(recipe.m_craftingStation, AzuCraftyBoxesPlugin.mRange.Value))
        {
            if (chest == null) continue;
            foreach (Piece.Requirement? requirement in requirements)
            {
                if (!requirement.m_resItem) continue;

                int requiredAmount = requirement.GetAmount(qualityLevel);
                ItemDrop.ItemData.SharedData? requirementSharedItemData = requirement.m_resItem.m_itemData.m_shared;
                for (int i = 0; i <= requirementSharedItemData.m_maxQuality; ++i)
                {
                    string? requirementName = requirementSharedItemData.m_name;
                    AzuCraftyBoxesPlugin.AzuCraftyBoxesLogger.LogDebug($"Checking {requirementName} in {chest.GetPrefabName()}, we have {chest.ItemCount(requirementName)} and need {requiredAmount}");
                    if (chest.ItemCount(requirementName) < requiredAmount) continue;
                    Inventory? chestInventory = chest.GetInventory();

                    amount = requiredAmount;
                    extraAmount = requirement.m_extraAmountOnlyOneIngredient;
                    return chestInventory?.GetItem(requirementName, i);
                }
            }
        }

        amount = 0;
        extraAmount = 0;
        return null;
    }
}

[HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.DoCrafting))]
public static class InventoryGuiDoCraftingTranspiler
{
    private static MethodInfo _methodPlayerInventoryRemoveItem = AccessTools.Method(typeof(Inventory), nameof(Inventory.RemoveItem), new Type[] { typeof(string), typeof(int), typeof(int), typeof(bool) });
    private static MethodInfo _methodUseItemFromInventoryOrChest = AccessTools.Method(typeof(InventoryGuiDoCraftingTranspiler), nameof(UseItemFromInventoryOrChest));

    [HarmonyPriority(Priority.VeryHigh)]
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        AzuCraftyBoxesPlugin.AzuCraftyBoxesLogger.LogWarning("Transpiling InventoryGui::DoCrafting");
        List<CodeInstruction> il = instructions.ToList();

        for (int i = 0; i < il.Count; i++)
        {
            if (il[i].Calls(_methodPlayerInventoryRemoveItem))
            {
                il[i] = new CodeInstruction(OpCodes.Call, _methodUseItemFromInventoryOrChest);
                il.RemoveAt(i - 8); // removes calls to Player::GetInventory

                return il.AsEnumerable();
            }
        }

        return instructions;
    }

    private static void UseItemFromInventoryOrChest(Player player, string itemName, int quantity, int quality, bool worldLevelBased)
    {
        AzuCraftyBoxesPlugin.AzuCraftyBoxesLogger.LogDebug($"Trying to remove {quantity} {itemName} from player inventory or nearby chests");
        Inventory playerInventory = player.GetInventory();
        if (playerInventory.CountItems(itemName, quality) >= quantity)
        {
            playerInventory.RemoveItem(itemName, quantity, quality, worldLevelBased);
            return;
        }

        foreach (IContainer chest in Boxes.GetNearbyContainers(player, AzuCraftyBoxesPlugin.mRange.Value))
        {
            if (chest.ItemCount(itemName) > 0)
            {
                AzuCraftyBoxesPlugin.AzuCraftyBoxesLogger.LogDebug($"Removing {quantity} {itemName} from {chest.GetPrefabName()}");
                chest.RemoveItem(itemName, quantity);
                chest.Save();
            }
        }
    }
}*/