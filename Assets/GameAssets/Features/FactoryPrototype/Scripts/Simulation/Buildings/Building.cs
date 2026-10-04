using System.Collections.Generic;

namespace Friendslop.Features.FactoryPrototype.Simulation
{
    /// <summary>
    /// Item moving between buildings. Id 0 = no identity yet; a receiver that keeps items visible assigns one.
    /// </summary>
    public readonly struct Item
    {
        public Item(int id, ushort type)
        {
            Id = id;
            Type = type;
        }

        public int Id { get; }
        public ushort Type { get; }
    }

    /// <summary>Coarse state for status lights.</summary>
    public enum BuildingStatus : byte
    {
        None,
        Working,

        /// <summary>Waiting for inputs.</summary>
        Waiting,

        /// <summary>Output cannot leave.</summary>
        Blocked
    }

    /// <summary>
    /// Placed building. Origin, rotation and ports never change after placement.
    /// Step order = creation order. Mutated only by the owning FactorySim.
    /// </summary>
    public abstract class Building
    {
        private readonly WorldPort[] _ports;

        internal Building(FactorySim sim, int id, BuildingDef def, Int3 origin, int rotation)
        {
            Sim = sim;
            Id = id;
            Def = def;
            Origin = origin;
            Rotation = rotation & 3;

            _ports = new WorldPort[def.Ports.Count];
            for (int i = 0; i < _ports.Length; i++)
                _ports[i] = Footprint.GetPort(def, i, origin, Rotation);
        }

        public int Id { get; }
        public BuildingDef Def { get; }
        public Int3 Origin { get; }
        public int Rotation { get; }
        public Dir Forward => Footprint.Forward(Rotation);
        public IReadOnlyList<WorldPort> Ports => _ports;

        protected FactorySim Sim { get; }

        public virtual BuildingStatus Status => BuildingStatus.None;

        /// <summary>Items produced, crafted or sold since placement. Used for measured rates. 0 if not applicable.</summary>
        public virtual int Throughput => 0;

        internal abstract void Step(int tick);

        /// <summary>Offers an item entering through side entrySide of cell (a cell of this building).</summary>
        internal abstract bool TryAccept(in Item item, Int3 cell, Dir entrySide, int tick);

        internal abstract void WriteState(SimWriter writer);
        internal abstract void ReadState(SimReader reader);

        /// <summary>Short state description for debug UI.</summary>
        public abstract string Describe();

        protected bool IsInputPort(Int3 cell, Dir side)
        {
            for (int i = 0; i < _ports.Length; i++)
            {
                WorldPort port = _ports[i];
                if (port.Type == PortType.Input && port.Cell == cell && port.Side == side)
                    return true;
            }
            return false;
        }

        /// <summary>Offers the item to whatever is on the other side of the port.</summary>
        protected bool TryPush(in WorldPort port, in Item item, int tick)
        {
            Int3 targetCell = port.NeighborCell;
            Building target = Sim.GetBuildingAt(targetCell);
            return target != null && target != this && target.TryAccept(item, targetCell, port.Side.Opposite(), tick);
        }
    }
}
