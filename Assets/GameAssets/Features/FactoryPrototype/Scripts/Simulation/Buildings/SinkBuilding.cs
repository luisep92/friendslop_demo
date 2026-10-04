namespace Friendslop.Features.FactoryPrototype.Simulation
{
    /// <summary>
    /// Sell point. Accepts any item through its input ports and adds the item value to the shared coins.
    /// </summary>
    public sealed class SinkBuilding : Building
    {
        private int _delivered;

        internal SinkBuilding(FactorySim sim, int id, BuildingDef def, Int3 origin, int rotation)
            : base(sim, id, def, origin, rotation)
        {
        }

        public int Delivered => _delivered;

        public override BuildingStatus Status => BuildingStatus.Working;
        public override int Throughput => _delivered;

        internal override void Step(int tick)
        {
        }

        internal override bool TryAccept(in Item item, Int3 cell, Dir entrySide, int tick)
        {
            if (!IsInputPort(cell, entrySide))
                return false;

            ItemDef itemDef = Sim.Content.GetItem(item.Type);
            _delivered++;
            Sim.AddCoins(itemDef != null ? itemDef.Value : 0);
            return true;
        }

        internal override void WriteState(SimWriter writer) => writer.WriteInt(_delivered);
        internal override void ReadState(SimReader reader) => _delivered = reader.ReadInt();

        public override string Describe() => $"{Def.Name} #{Id} | sold {_delivered}";
    }
}
