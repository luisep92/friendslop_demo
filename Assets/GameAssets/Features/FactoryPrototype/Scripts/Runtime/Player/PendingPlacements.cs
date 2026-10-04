using System.Collections.Generic;
using Friendslop.Features.FactoryPrototype.Simulation;
using UnityEngine;

namespace Friendslop.Features.FactoryPrototype
{
    /// <summary>
    /// Visual-only optimism: placements requested by the local player show as pending ghosts until the
    /// local sim contains them (confirmed) or a timeout passes (rejected). The sim is never touched.
    /// </summary>
    public sealed class PendingPlacements
    {
        private const float TimeoutSeconds = 1.5f;

        private readonly List<Entry> _entries = new List<Entry>();

        public int Count => _entries.Count;

        public void Add(BuildingDef def, Int3 origin, int rotation)
        {
            var ghost = new BuildGhost();
            ghost.Show(def, origin, rotation, FactoryPalette.GhostPending);
            _entries.Add(new Entry
            {
                DefId = def.Id,
                Origin = origin,
                Expires = Time.unscaledTime + TimeoutSeconds,
                Ghost = ghost
            });
        }

        public void Update(FactorySim sim)
        {
            for (int i = _entries.Count - 1; i >= 0; i--)
            {
                Entry entry = _entries[i];
                Building building = sim?.GetBuildingAt(entry.Origin);
                bool confirmed = building != null && building.Def.Id == entry.DefId && building.Origin == entry.Origin;
                if (!confirmed && sim != null && Time.unscaledTime < entry.Expires)
                    continue;

                entry.Ghost.Destroy();
                _entries.RemoveAt(i);
            }
        }

        public void Clear()
        {
            foreach (Entry entry in _entries)
                entry.Ghost.Destroy();
            _entries.Clear();
        }

        private struct Entry
        {
            public ushort DefId;
            public Int3 Origin;
            public float Expires;
            public BuildGhost Ghost;
        }
    }
}
