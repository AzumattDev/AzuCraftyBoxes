using AzuCraftyBoxes.Patches;
using static AzuCraftyBoxes.AzuCraftyBoxesPlugin;

namespace AzuCraftyBoxes.Util;

public static class PlayerPullingExtensions
{
    /// <summary>
    /// Returns true if pulling is allowed (state == 1).
    /// </summary>
    public static bool IsPullingAllowed(this Player player)
    {
        if (!player.m_customData.TryGetValue(PreventPullingLogicKey, out string? rawValue) || !int.TryParse(rawValue, out int state))
        {
            // default => 1 (allowed)
            state = 1;
            player.m_customData[PreventPullingLogicKey] = state.ToString();
        }

        return state == 1;
    }

    /// <summary>
    /// Writes back 1=allowed, 0=prevented.
    /// </summary>
    public static void SetPullingAllowed(this Player player, bool allowed)
    {
        player.m_customData[PreventPullingLogicKey] = (allowed ? 1 : 0).ToString();
    }

    /// <summary>
    /// Adds/removes the status effect when pulling is prevented.
    /// </summary>
    public static void ApplyPullingStatusEffect(this Player player)
    {
        bool allowed = player.IsPullingAllowed();
        if (!allowed && preventPullingStatusEffectDisplay.Value.isOn())
            player.m_seman.AddStatusEffect(SE_ContainerPull.SE_ContainerPulling);
        else
            player.m_seman.RemoveStatusEffect(SE_ContainerPull.SE_ContainerPulling);
    }

    /// <summary>
    /// Flip the allowed flag (1↔0), write it back, reapply effect,
    /// and return the new allowed state.
    /// </summary>
    public static bool TogglePullingAllowed(this Player player)
    {
        bool current = player.IsPullingAllowed();
        bool next = !current;
        player.SetPullingAllowed(next);
        player.ApplyPullingStatusEffect();
        return next;
    }
}