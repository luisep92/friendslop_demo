namespace Friendslop.Features.FactoryPrototype.Simulation
{
    public static class SimConstants
    {
        public const int TicksPerSecond = 20;

        /// <summary>Belt progress units per tile.</summary>
        public const int TileLength = 100;

        /// <summary>Progress units per tick. Must divide TileLength. 10 = 2 tiles/s.</summary>
        public const int BeltSpeed = 10;

        /// <summary>Minimum distance between items on belts, in progress units.</summary>
        public const int BeltSpacing = 25;

        /// <summary>Per ingredient and per output.</summary>
        public const int MachineBufferCapacity = 4;

        public const int GridMin = -32;
        public const int GridMax = 31;

        /// <summary>Server sends a state checksum every N ticks.</summary>
        public const int ChecksumInterval = 20;
    }
}
