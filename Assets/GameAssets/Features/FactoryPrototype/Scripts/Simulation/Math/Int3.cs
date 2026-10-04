using System;

namespace Friendslop.Features.FactoryPrototype.Simulation
{
    /// <summary>
    /// Integer grid coordinate. Y is the level. Hash is deterministic (no System.HashCode).
    /// </summary>
    public readonly struct Int3 : IEquatable<Int3>
    {
        public static readonly Int3 Zero = new Int3(0, 0, 0);

        public readonly int X;
        public readonly int Y;
        public readonly int Z;

        public Int3(int x, int y, int z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public static Int3 operator +(Int3 a, Int3 b) => new Int3(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
        public static Int3 operator -(Int3 a, Int3 b) => new Int3(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
        public static bool operator ==(Int3 a, Int3 b) => a.X == b.X && a.Y == b.Y && a.Z == b.Z;
        public static bool operator !=(Int3 a, Int3 b) => !(a == b);

        public bool Equals(Int3 other) => this == other;
        public override bool Equals(object obj) => obj is Int3 other && this == other;

        public override int GetHashCode()
        {
            unchecked
            {
                return (X * 73856093) ^ (Y * 19349663) ^ (Z * 83492791);
            }
        }

        public override string ToString() => $"({X}, {Y}, {Z})";
    }
}
