using System;
using AzuCraftyBoxes.APIs;
using AzuCraftyBoxes.Util.Functions;
using UnityEngine;

namespace AzuCraftyBoxes.IContainers;

public class kgDrawer(ItemDrawers_API.Drawer _drawer) : IContainer
{
    public int ProcessContainerInventory(string reqPrefab, string reqName, int totalAmount, int totalRequirement)
    {
        if (_drawer.Prefab != reqPrefab) return totalAmount;
        int thisAmount = Mathf.Min(_drawer.Amount, totalRequirement - totalAmount);
        _drawer.Remove(thisAmount);
        return totalAmount + thisAmount;
    }

    public bool ContainsItem(string prefab, int amount, out int result)
    {
        result = 0;
        if (_drawer.Prefab != prefab) return false;
        result = _drawer.Amount;
        return result >= amount;
    }

    public bool ContainsItem(string prefab, int amount, string sharedName, out int result)
    {
        result = 0;
        if (_drawer.Prefab != prefab) return false;
        result = _drawer.Amount;
        return result >= amount;
    }

    public void RemoveItem(string prefab, int amount)
    {
        amount = Mathf.Min(amount, _drawer.Amount);
        _drawer.Remove(amount);
    }

    public void RemoveItem(string prefab, string sharedName, int amount)
    {
        amount = Mathf.Min(amount, _drawer.Amount);
        _drawer.Remove(amount);
    }

    public void Save()
    {
    }

    public Vector3 GetPosition() => _drawer.Position;
    public string GetPrefabName() => _drawer.ZNVName;


    public static kgDrawer Create(ItemDrawers_API.Drawer drawer) => new(drawer);
}