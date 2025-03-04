namespace AzuCraftyBoxes;

public static class ItemKeyHelper
{
    /// <summary>
    /// Returns a canonical key for an item.
    /// Prefers the sanitized prefab name; falls back to the shared name.
    /// Optionally, you could incorporate localization if needed.
    /// </summary>
    public static string GetCanonicalKey(ItemDrop.ItemData item)
    {
        if (item == null)
            return string.Empty;

        if (!string.IsNullOrEmpty(item.m_shared.m_name))
        {
            return item.m_shared.m_name.ToLowerInvariant();
        }

        // Try to get the prefab name.
        string prefabKey = Utils.GetPrefabName(item.m_dropPrefab);
        if (!string.IsNullOrEmpty(prefabKey))
        {
            return prefabKey.ToLowerInvariant();
        }
        
        // Fallback to the shared name if all else fails.
        return item.m_shared.m_name;
    }
}
