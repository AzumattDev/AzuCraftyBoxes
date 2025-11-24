using AzuCraftyBoxes.Compatibility.WardIsLove;
using AzuCraftyBoxes.Util.Functions;
using static AzuCraftyBoxes.Util.Functions.MiscFunctions;

namespace AzuCraftyBoxes.Patches;

[HarmonyPatch(typeof(Container), nameof(Container.Awake))]
internal static class ContainerAwakePatch
{
    private static void Postfix(Container __instance)
    {
        if (ShouldSkipContainer(__instance)) return;

        try
        {
            Player? parentPlayer = __instance.GetComponentInParent<Player>();
            if (parentPlayer && parentPlayer != Player.m_localPlayer)
            {
                return;
            }

            if (HasAccessToContainer(__instance))
            {
                Boxes.AddContainer(__instance);
            }
        }
        catch
        {
            // ignored, TODO: better handling later
        }
    }
}

[HarmonyPatch(typeof(Container), nameof(Container.OnContainerChanged))]
internal static class ContainerOnContainerChangedPatch
{
    private static void Postfix(Container __instance)
    {
        if (!__instance.IsOwner()) return;
        if (ShouldSkipContainer(__instance)) return;

        Boxes.AddContainer(__instance);
        Boxes.RebuildCache(__instance);
    }
}

[HarmonyPatch(typeof(Container), nameof(Container.Load))]
internal static class ContainerLoadPatch
{
    private static void Postfix(Container __instance, bool __result)
    {
        if (!__result) return;
        if (ShouldSkipContainer(__instance)) return;

        Player? player = Player.m_localPlayer;
        if (!player) return;

        Player? parentPlayer = __instance.GetComponentInParent<Player>();
        if (parentPlayer && parentPlayer != player)
        {
            return;
        }

        if (player.m_isLoading || player.m_teleporting) return;

        if (!HasAccessToContainer(__instance)) return;
        Boxes.AddContainer(__instance);
        Boxes.RebuildCache(__instance); // reflect remote/server changes
    }
}

[HarmonyPatch(typeof(Container), nameof(Container.OnDestroyed))]
internal static class ContainerOnDestroyedPatch
{
    private static void Postfix(Container __instance)
    {
        if (ShouldSkipContainer(__instance)) return;

        Boxes.RemoveContainer(__instance);
    }
}

[HarmonyPatch(typeof(WearNTear), nameof(WearNTear.OnDestroy))]
static class WearNTearOnDestroyPatch
{
    static void Prefix(WearNTear __instance)
    {
        if (ShouldPrevent()) return;

        Container[]? children = __instance.GetComponentsInChildren<Container>();
        Container[]? parents = __instance.GetComponentsInParent<Container>();

        if (children.Length > 0)
        {
            foreach (Container c in children)
            {
                Boxes.RemoveContainer(c);
            }
        }

        if (parents.Length <= 0) return;

        foreach (Container c in parents)
        {
            Boxes.RemoveContainer(c);
        }
    }
}

[HarmonyPatch(typeof(Player), nameof(Player.UpdateTeleport))]
public static class PlayerUpdateTeleportPatchCleanupContainers
{
    public static void Prefix(float dt)
    {
        if (ShouldPrevent()) return;

        if (!Player.m_localPlayer || !Player.m_localPlayer.m_teleporting)
            return;

        // Clean out broken container refs
        foreach (Container? container in Boxes.Containers.ToList())
        {
            if (!container || !container.transform || container.GetInventory() == null)
            {
                Boxes.RemoveContainer(container!);
            }
        }
    }
}