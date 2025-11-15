using System.Collections.Generic;
using UnityEngine;
using AzuCraftyBoxes.IContainers;

namespace AzuCraftyBoxes.Util.Functions
{
    /// <summary>
    /// Per-frame aggregated counts for the current UI context.
    /// Rebuilt at most once per frame. Counts include player inventory.
    /// </summary>
    internal static class UiItemBank
    {
        private struct Key
        {
            public int NameHash; // shared.m_name.GetStableHashCode()
            public int Quality; // 0 means "any quality"

            public Key(int n, int q)
            {
                NameHash = n;
                Quality = q;
            }
        }

        private static int _frameId = -1;
        private static readonly Dictionary<Key, int> _totals = new(256);
        private static readonly Dictionary<int, int> _containersHavingAny = new(256); // per item hash (any quality)
        private static List<IContainer> _containers = new(64);
        private static bool _leaveOne;

        /// <summary>Call once per frame (or at the top of a UI pass) to seed with nearby containers.</summary>
        public static void Begin(List<IContainer> containers)
        {
            int f = Time.frameCount;
            if (_frameId == f) return;

            _frameId = f;
            _totals.Clear();
            _containersHavingAny.Clear();
            _containers = containers;
            _leaveOne = AzuCraftyBoxesPlugin.leaveOne.Value.isOn();
        }

        /// <summary>Total available of shared name (any quality); includes player + containers, with "leave one" applied once per container.</summary>
        public static int GetTotalAnyQuality(string sharedName)
        {
            int hash = sharedName.GetStableHashCode();
            var key = new Key(hash, 0);
            if (_totals.TryGetValue(key, out var v)) return v;

            int total = Player.m_localPlayer.GetInventory().CountItems(sharedName);
            int containersHaving = 0;

            // single pass over containers, zero allocations
            for (int i = 0; i < _containers.Count; ++i)
            {
                var c = _containers[i];
                if (c == null) continue;

                int count = c.ItemCount(sharedName);
                if (count <= 0) continue;

                containersHaving++;
                total += count;
            }

            if (_leaveOne && containersHaving > 0)
                total = Mathf.Max(0, total - containersHaving);

            _containersHavingAny[hash] = containersHaving; // cache for quality lookups
            _totals[key] = total;
            return total;
        }

        /// <summary>Total available of shared name at a specific quality; includes player + containers, with "leave one" applied once per container that has that quality.</summary>
        public static int GetTotalAtQuality(string sharedName, int quality)
        {
            int hash = sharedName.GetStableHashCode();
            var key = new Key(hash, quality);
            if (_totals.TryGetValue(key, out var v)) return v;

            int total = Player.m_localPlayer.GetInventory().CountItems(sharedName, quality);
            int containersHaving = 0;

            for (int i = 0; i < _containers.Count; ++i)
            {
                var c = _containers[i];
                if (c == null) continue;

                if (!c.ContainsItem(sharedName, quality, out int amount) || amount <= 0)
                    continue;

                containersHaving++;
                total += amount;
            }

            if (_leaveOne && containersHaving > 0)
                total = Mathf.Max(0, total - containersHaving);

            _totals[key] = total;
            return total;
        }
    }
}