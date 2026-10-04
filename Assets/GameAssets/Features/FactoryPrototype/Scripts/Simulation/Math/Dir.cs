namespace Friendslop.Features.FactoryPrototype.Simulation
{
    /// <summary>
    /// Horizontal direction. North = +Z, East = +X. Values increase clockwise seen from above.
    /// </summary>
    public enum Dir : byte
    {
        North = 0,
        East = 1,
        South = 2,
        West = 3
    }

    public static class DirExtensions
    {
        /// <summary>Rotates by 90 degree clockwise steps. Negative steps rotate counter-clockwise.</summary>
        public static Dir Rotate(this Dir dir, int steps) => (Dir)(((int)dir + steps) & 3);

        public static Dir Opposite(this Dir dir) => dir.Rotate(2);

        public static Int3 ToOffset(this Dir dir)
        {
            switch (dir)
            {
                case Dir.North: return new Int3(0, 0, 1);
                case Dir.East: return new Int3(1, 0, 0);
                case Dir.South: return new Int3(0, 0, -1);
                default: return new Int3(-1, 0, 0);
            }
        }
    }
}
