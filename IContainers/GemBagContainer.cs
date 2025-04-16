using System;
using System.Linq;
using UnityEngine;
using Jewelcrafting; // assuming the API methods are in this namespace
using AzuCraftyBoxes.IContainers;
using ItemDataManager;

namespace AzuCraftyBoxes.IContainers
{
    public class GemBagContainer(ItemDrop.ItemData gemBagItem) : IContainer
    {
        public const string GemBagPrefabName = "JC_Gem_Bag";

        private readonly ItemDrop.ItemData _gemBagItem = gemBagItem ?? throw new ArgumentNullException(nameof(gemBagItem));

        public Inventory? GetInventory()
        {
            return Jewelcrafting.API.GetItemContainerInventory(_gemBagItem);
        }

        public int ProcessContainerInventory(string reqName, int totalAmount, int totalRequirement)
        {
            Inventory? inv = GetInventory();
            if (inv == null)
            {
                AzuCraftyBoxesPlugin.AzuCraftyBoxesLogger.LogWarning("[GemBagContainer] No inventory returned from API.");
                return totalAmount;
            }

            int itemsNeeded = totalRequirement - totalAmount;
            int inBag = inv.CountItems(reqName);
            int thisAmount = Mathf.Min(inBag, itemsNeeded);

            if (thisAmount <= 0)
                return totalAmount;

            for (int i = 0; i < inv.GetAllItems().Count && itemsNeeded > 0; ++i)
            {
                ItemDrop.ItemData? item = inv.GetItem(i);
                if (item == null || item.m_shared?.m_name != reqName)
                    continue;

                int removeCount = Mathf.Min(item.m_stack, itemsNeeded);
                if (removeCount >= item.m_stack)
                {
                    inv.RemoveItem(i);
                    i--;
                }
                else
                {
                    item.m_stack -= removeCount;
                }

                totalAmount += removeCount;
                itemsNeeded -= removeCount;
                if (totalAmount >= totalRequirement)
                    break;
            }

            if (!Jewelcrafting.API.IsFreelyAccessibleInventory(_gemBagItem))
            {
                _gemBagItem.Data().Save();
            }
            else
            {
                inv.Changed();
            }

            return totalAmount;
        }


        public int ItemCount(string name)
        {
            Inventory? inv = GetInventory();
            if (inv == null)
                return 0;
            return inv.GetAllItems()
                .Where(it => it?.m_shared?.m_name == name)
                .Sum(it => it.m_stack);
        }

        public void RemoveItem(string name, int amount)
        {
            Inventory? inv = GetInventory();
            if (inv == null)
                return;

            int toRemove = amount;
            for (int i = 0; i < inv.GetAllItems().Count && toRemove > 0; ++i)
            {
                ItemDrop.ItemData? item = inv.GetItem(i);
                if (item == null || item.m_shared?.m_name != name)
                    continue;

                int removeNow = Mathf.Min(item.m_stack, toRemove);
                item.m_stack -= removeNow;
                toRemove -= removeNow;

                if (item.m_stack <= 0)
                {
                    inv.RemoveItem(i);
                    i--;
                }
            }

            if (!Jewelcrafting.API.IsFreelyAccessibleInventory(_gemBagItem))
            {
                _gemBagItem.Data().Save();
            }
            else
            {
                inv.Changed();
            }
        }


        public void RemoveItem(string prefab, string sharedName, int amount)
        {
            RemoveItem(sharedName, amount);
        }


        public void Save()
        {
            Inventory? inv = GetInventory();
            if (inv != null)
            {
                inv.Changed();
            }

            if (!Jewelcrafting.API.IsFreelyAccessibleInventory(_gemBagItem))
            {
                _gemBagItem.Data().Save();
            }
        }

        public Vector3 GetPosition()
        {
            return Player.m_localPlayer.transform.position;
        }

        public string GetPrefabName()
        {
            return _gemBagItem.m_dropPrefab?.name ?? GemBagPrefabName;
        }
    }
}