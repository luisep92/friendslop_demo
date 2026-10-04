namespace Friendslop.Features.FactoryPrototype.Simulation
{
    /// <summary>
    /// Produces one item every SourceIntervalTicks through its output port. Holds it while the output is blocked.
    /// </summary>
    public sealed class SourceBuilding : Building
    {
        private int _timer;
        private bool _hasPending;

        internal SourceBuilding(FactorySim sim, int id, BuildingDef def, Int3 origin, int rotation)
            : base(sim, id, def, origin, rotation)
        {
        }

        /// <summary>True when an item is ready but the output did not accept it.</summary>
        public bool IsBlocked => _hasPending;

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
                    _hasPending = false;
            }
        }

        internal override bool TryAccept(in Item item, Int3 cell, Dir entrySide, int tick) => false;

        internal override void WriteState(SimWriter writer)
        {
            writer.WriteInt(_timer);
            writer.WriteBool(_hasPending);
        }

        internal override void ReadState(SimReader reader)
        {
            _timer = reader.ReadInt();
            _hasPending = reader.ReadBool();
        }

        public override string Describe() =>
            _hasPending
                ? $"{Def.Name} #{Id} | output blocked"
                : $"{Def.Name} #{Id} | next item in {Def.SourceIntervalTicks - _timer} ticks";
    }
}
