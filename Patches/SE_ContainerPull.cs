﻿using System.IO;

namespace AzuCraftyBoxes.Patches;

[HarmonyPatch(typeof(ObjectDB), nameof(ObjectDB.Awake))]
static class ObjectDBAwakePatch
{
    [HarmonyPriority(Priority.VeryHigh)]
    static void Postfix(ObjectDB __instance)
    {
        if (!__instance.m_StatusEffects.Contains(SE_ContainerPull.SE_ContainerPulling))
        {
            __instance.m_StatusEffects.Add(SE_ContainerPull.SE_ContainerPulling);
        }

        __instance.UpdateRegisters();
    }
}

[HarmonyPatch(typeof(Player), nameof(Player.SetLocalPlayer))]
static class PlayerSetLocalPlayerPatch
{
    static void Postfix(Player __instance)
    {
        SE_ContainerPull.CheckAndSetStatusEffect(__instance);
    }
}

public class SE_ContainerPull
{
    public static readonly int s_statusEffectPreventPulling = "Pull from containers".GetStableHashCode();
    public static StatusEffect SE_ContainerPulling = null!;

    public static void CreateEffect()
    {
        SE_ContainerPulling = ScriptableObject.CreateInstance<StatusEffect>();
        SE_ContainerPulling.name = "PreventPulling";
        SE_ContainerPulling.m_name = "Preventing Pulling";
        SE_ContainerPulling.m_icon = LoadSprite("pullingicon.png");
        SE_ContainerPulling.m_tooltip = "Prevents pulling from nearby containers & backpacks";
        SE_ContainerPulling.m_startMessageType = MessageHud.MessageType.TopLeft;
        SE_ContainerPulling.m_startMessage = "";
        SE_ContainerPulling.m_stopMessageType = MessageHud.MessageType.TopLeft;
        SE_ContainerPulling.m_stopMessage = "";
    }

    private static byte[] ReadEmbeddedFileBytes(string name)
    {
        using MemoryStream stream = new();
        Assembly.GetExecutingAssembly().GetManifestResourceStream(Assembly.GetExecutingAssembly().GetName().Name + "." + name)!.CopyTo(stream);
        return stream.ToArray();
    }

    private static Texture2D LoadTexture(string name)
    {
        Texture2D texture = new(0, 0);
        texture.LoadImage(ReadEmbeddedFileBytes("images." + name));
        return texture;
    }

    private static Sprite LoadSprite(string name)
    {
        Texture2D texture = LoadTexture(name);
        return texture != null ? Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), Vector2.zero) : null!;
    }
    
    public static void CheckAndSetStatusEffect(Player instance = null)
    {
        Player? player = Player.m_localPlayer;
        if (player == null || (player != null && instance != Player.m_localPlayer)) return;

        if (!player.m_customData.TryGetValue(AzuCraftyBoxesPlugin.PreventPullingLogicKey, out string value) || !int.TryParse(value, out int result))
        {
            // Initialize custom data if not set or invalid value present
            player.m_customData[AzuCraftyBoxesPlugin.PreventPullingLogicKey] = "1";
            result = 1;
        }

        bool isAllowed = result == 0;
        if (!isAllowed && AzuCraftyBoxesPlugin.preventPullingStatusEffectDisplay.Value.isOn())
        {
            player.m_seman.AddStatusEffect(SE_ContainerPull.SE_ContainerPulling);
        }
        else
        {
            player.m_seman.RemoveStatusEffect(SE_ContainerPull.SE_ContainerPulling);
        }
    }
}