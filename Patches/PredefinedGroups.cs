using System.Linq;
using AzuCraftyBoxes.Util.Functions;
using HarmonyLib;
using UnityEngine;

namespace AzuCraftyBoxes.Patches;

[HarmonyPatch(typeof(ObjectDB), nameof(ObjectDB.Awake))]
static class PredefinedGroupGrab
{
    static void Postfix(ObjectDB __instance)
    {
        if (!ZNetScene.instance)
            return;
        MiscFunctions.CreatePredefinedGroups(__instance);
    }
}