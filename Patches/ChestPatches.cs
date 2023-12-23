using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using AzuCraftyBoxes.Compatibility.WardIsLove;
using AzuCraftyBoxes.Util;
using AzuCraftyBoxes.Util.Functions;
using HarmonyLib;
using UnityEngine;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace AzuCraftyBoxes.Patches;

[HarmonyPatch(typeof(Container), nameof(Container.Awake))]
internal static class ContainerAwakePatch
{
    private static void Postfix(Container __instance)
    {
        if (__instance.name.StartsWith("Treasure") || __instance.GetInventory() == null ||
            !__instance.m_nview.IsValid() || __instance.m_nview.GetZDO().GetLong("creator".GetStableHashCode()) == 0L)
            return;

        try
        {
            //if (Player.m_localPlayer == null) return;
            if (__instance.GetComponentInParent<Player>() != null)
            {
                if (__instance.GetComponentInParent<Player>() != Player.m_localPlayer)
                {
                    return;
                }
            }

            // Only add containers that the player should have access to
            if (WardIsLovePlugin.IsLoaded() && WardIsLovePlugin.WardEnabled().Value &&
                WardMonoscript.CheckAccess(__instance.transform.position, flash: false, wardCheck: true))
            {
                long playerId = Game.instance.GetPlayerProfile().GetPlayerID();
                if (__instance.CheckAccess(playerId))
                {
                    Boxes.AddContainer(__instance);
                }
            }
            else
            {
                long playerId = Game.instance.GetPlayerProfile().GetPlayerID();
                if (!__instance.CheckAccess(playerId)) return;
                if (PrivateArea.CheckAccess(__instance.transform.position, flash: false, wardCheck: true))
                    Boxes.AddContainer(__instance);
            }
        }
        catch
        {
            //ignored TODO: Fix this for real later.
        }
    }
}

[HarmonyPatch(typeof(Container), nameof(Container.Load))]
static class ContainerLoadPatch
{
    static void Postfix(Container __instance)
    {
        if (__instance.name.StartsWith("Treasure") || __instance.GetInventory() == null || !__instance.m_nview.IsValid() || __instance.m_nview.GetZDO().GetLong("creator".GetStableHashCode()) == 0L)
            return;

        if (Player.m_localPlayer == null) return;
        if (__instance.GetComponentInParent<Player>() != null)
        {
            if (__instance.GetComponentInParent<Player>() != Player.m_localPlayer)
            {
                return;
            }
        }

        if (Player.m_localPlayer.m_isLoading || Player.m_localPlayer.m_teleporting) return;
        // Only add containers that the player should have access to
        if (WardIsLovePlugin.IsLoaded() && WardIsLovePlugin.WardEnabled()!.Value &&
            WardMonoscript.CheckAccess(__instance.transform.position, flash: false, wardCheck: true))
        {
            long playerId = Game.instance.GetPlayerProfile().GetPlayerID();
            if (__instance.CheckAccess(playerId))
            {
                Boxes.AddContainer(__instance);
            }
        }
        else
        {
            long playerId = Game.instance.GetPlayerProfile().GetPlayerID();
            if (!__instance.CheckAccess(playerId)) return;
            if (PrivateArea.CheckAccess(__instance.transform.position, flash: false, wardCheck: true))
                Boxes.AddContainer(__instance);
        }
    }
}

[HarmonyPatch(typeof(Container), nameof(Container.OnDestroyed))]
internal static class ContainerOnDestroyedPatch
{
    private static void Postfix(Container __instance)
    {
        if (__instance.name.StartsWith("Treasure") || __instance.GetInventory() == null ||
            !__instance.m_nview.IsValid() || __instance.m_nview.GetZDO().GetLong("creator".GetStableHashCode()) == 0L)
            return;

        Boxes.RemoveContainer(__instance);
    }
}

[HarmonyPatch(typeof(Player), nameof(Player.UpdateTeleport))]
public static class PlayerUpdateTeleportPatchCleanupContainers
{
    public static void Prefix(float dt)
    {
        if (!(Player.m_localPlayer != null) || !Player.m_localPlayer.m_teleporting)
            return;
        foreach (Container container in Boxes.Containers.ToList().Where(container =>
                     (!(container != null) || !(container.transform != null)
                         ? 0
                         : (container.GetInventory() != null ? 1 : 0)) == 0).Where(container => container != null))
        {
            Boxes.RemoveContainer(container);
        }
    }
}