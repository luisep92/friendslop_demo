using System;
using System.Collections.Generic;

namespace Friendslop.Features.FactoryPrototype.Simulation
{
    /// <summary>
    /// Id-indexed lookup of items and buildings. Id 0 is reserved for "none".
    /// All peers must use identical content for the simulation to stay in sync.
    /// </summary>
    public sealed class ContentDb
    {
        private readonly ItemDef[] _items;
        private readonly BuildingDef[] _buildings;
        private readonly BuildingDef[] _buildable;

        /// <param name="buildings">Order defines the build slot order.</param>
        public ContentDb(IReadOnlyList<ItemDef> items, IReadOnlyList<BuildingDef> buildings)
        {
            _items = new ItemDef[MaxId(items, i => i.Id) + 1];
            foreach (ItemDef item in items)
                Register(_items, item.Id, item, "item");

            _buildings = new BuildingDef[MaxId(buildings, b => b.Id) + 1];
            foreach (BuildingDef building in buildings)
                Register(_buildings, building.Id, building, "building");

            _buildable = new BuildingDef[buildings.Count];
            for (int i = 0; i < buildings.Count; i++)
                _buildable[i] = buildings[i];
        }

        public IReadOnlyList<BuildingDef> Buildable => _buildable;

        public ItemDef GetItem(ushort id) => id < _items.Length ? _items[id] : null;
        public BuildingDef GetBuilding(ushort id) => id < _buildings.Length ? _buildings[id] : null;

        private static int MaxId<T>(IReadOnlyList<T> defs, Func<T, ushort> getId)
        {
            int max = 0;
            foreach (T def in defs)
                max = Math.Max(max, getId(def));
            return max;
        }

        private static void Register<T>(T[] table, ushort id, T def, string label) where T : class
        {
            if (id == 0)
                throw new ArgumentException($"Content {label} id 0 is reserved.");
            if (table[id] != null)
                throw new ArgumentException($"Duplicate content {label} id {id}.");
            table[id] = def;
        }
    }
}
