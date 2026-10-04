using System;
using System.Collections.Generic;

namespace Friendslop.Features.FactoryPrototype.Simulation
{
    public struct BeltItem
    {
        public int Id;
        public ushort Type;

        /// <summary>0 = entry edge, TileLength = exit edge.</summary>
        public int Progress;

        /// <summary>World side the item came in through. Used to draw corners and side loading.</summary>
        public Dir EntrySide;

        internal int MovedTick;
    }

    /// <summary>
    /// One belt tile. Accepts from back and sides, outputs forward. Items keep BeltSpacing between them,
    /// also across tile seams. A blocked front item stops the tile (back-pressure).
    /// </summary>
    public sealed class BeltBuilding : Building
    {
        // Index 0 = front (highest progress).
        private readonly List<BeltItem> _items = new List<BeltItem>();

        internal BeltBuilding(FactorySim sim, int id, BuildingDef def, Int3 origin, int rotation)
            : base(sim, id, def, origin, rotation)
        {
        }

        /// <summary>Front first.</summary>
        public IReadOnlyList<BeltItem> Items => _items;

        public bool AcceptsFrom(Dir side) => side != Forward;

        internal override void Step(int tick)
        {
            for (int i = 0; i < _items.Count; i++)
            {
                BeltItem item = _items[i];
                // Items handed over from another belt this tick already moved.
                if (item.MovedTick == tick)
                    continue;

                int limit = i == 0 ? FrontLimit() : _items[i - 1].Progress - SimConstants.BeltSpacing;
                int target = Math.Min(item.Progress + SimConstants.BeltSpeed, limit);
                if (target > item.Progress)
                    item.Progress = target;
                item.MovedTick = tick;
                _items[i] = item;

                if (i == 0 && item.Progress >= SimConstants.TileLength && TryHandOff(item, tick))
                {
                    _items.RemoveAt(0);
                    i--;
                }
            }
        }

        internal override bool TryAccept(in Item item, Int3 cell, Dir entrySide, int tick)
        {
            if (!AcceptsFrom(entrySide))
                return false;
            if (_items.Count > 0 && _items[_items.Count - 1].Progress < SimConstants.BeltSpacing)
                return false;

            _items.Add(new BeltItem
            {
                Id = item.Id != 0 ? item.Id : Sim.AllocateItemId(),
                Type = item.Type,
                Progress = 0,
                EntrySide = entrySide,
                MovedTick = tick
            });
            return true;
        }

        internal override void WriteState(SimWriter writer)
        {
            writer.WriteInt(_items.Count);
            for (int i = 0; i < _items.Count; i++)
            {
                BeltItem item = _items[i];
                writer.WriteInt(item.Id);
                writer.WriteUShort(item.Type);
                writer.WriteInt(item.Progress);
                writer.WriteByte((byte)item.EntrySide);
            }
        }

        internal override void ReadState(SimReader reader)
        {
            int count = reader.ReadInt();
            if (count < 0 || count > SimConstants.TileLength / SimConstants.BeltSpacing + 1)
                throw new FormatException($"Invalid belt item count {count}.");

            _items.Clear();
            for (int i = 0; i < count; i++)
            {
                _items.Add(new BeltItem
                {
                    Id = reader.ReadInt(),
                    Type = reader.ReadUShort(),
                    Progress = reader.ReadInt(),
                    EntrySide = (Dir)(reader.ReadByte() & 3),
                    MovedTick = -1
                });
            }
        }

        public override string Describe() => $"{Def.Name} #{Id} | {_items.Count} items";

        /// <summary>
        /// Max progress for the front item: the exit edge, or less if the next belt's last item is too close.
        /// </summary>
        private int FrontLimit()
        {
            Dir forward = Forward;
            if (Sim.GetBuildingAt(Origin + forward.ToOffset()) is BeltBuilding next && next.AcceptsFrom(forward.Opposite()))
            {
                int count = next._items.Count;
                if (count > 0)
                    return Math.Min(SimConstants.TileLength, SimConstants.TileLength + next._items[count - 1].Progress - SimConstants.BeltSpacing);
            }
            return SimConstants.TileLength;
        }

        private bool TryHandOff(in BeltItem item, int tick)
        {
            Dir forward = Forward;
            Int3 targetCell = Origin + forward.ToOffset();
            Building target = Sim.GetBuildingAt(targetCell);
            return target != null && target.TryAccept(new Item(item.Id, item.Type), targetCell, forward.Opposite(), tick);
        }
    }
}
