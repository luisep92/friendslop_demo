namespace Friendslop.Features.FactoryPrototype.Simulation
{
    /// <summary>
    /// Produces one item every SourceIntervalTicks through its output port. Holds it while the output is blocked.
    /// </summary>
    public sealed class SourceBuilding : Building
    {
        private int _timer;
        private bool _hasPending;
        private int _produced;

        internal SourceBuilding(FactorySim sim, int id, BuildingDef def, Int3 origin, int rotation)
            : base(sim, id, def, origin, rotation)
        {
        }

        /// <summary>True when an item is ready but the output did not accept it.</summary>
        public bool IsBlocked => _hasPending;

        public override BuildingStatus Status => _hasPending ? BuildingStatus.Blocked : BuildingStatus.Working;
        public override int Throughput => _produced;

        internal override void Step(int tick)
        {
            if (!_hasPending && ++_timer >= Def.SourceIntervalTicks)
            {
                _hasPending = true;
                _timer = 0;
            }

            if (_hasPending)
            {
                WorldPort output = Ports[0];
                if (TryPush(output, new Item(0, Def.SourceItem), tick))
                {
                    _hasPending = false;
                    _produced++;
                }
            }
        }

        internal override bool TryAccept(in Item item, Int3 cell, Dir entrySide, int tick) => false;

        internal override void WriteState(SimWriter writer)
        {
            writer.WriteInt(_timer);
            writer.WriteBool(_hasPending);
            writer.WriteInt(_produced);
        }

        internal override void ReadState(SimReader reader)
        {
            _timer = reader.ReadInt();
            _hasPending = reader.ReadBool();
            _produced = reader.ReadInt();
        }

        public override string Describe() =>
            _hasPending
                ? $"{Def.Name} #{Id} | output blocked"
                : $"{Def.Name} #{Id} | next item in {Def.SourceIntervalTicks - _timer} ticks";
    }
}
