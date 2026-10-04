using System;

namespace Friendslop.Features.FactoryPrototype.Simulation
{
    public enum CommandType : byte
    {
        Place = 1,
        Remove = 2
    }

    /// <summary>
    /// The only way to mutate the factory. Applied at a tick boundary, identically on every peer.
    /// </summary>
    public readonly struct SimCommand : IEquatable<SimCommand>
    {
        private SimCommand(CommandType type, ushort defId, Int3 cell, byte rotation)
        {
            Type = type;
            DefId = defId;
            Cell = cell;
            Rotation = rotation;
        }

        public CommandType Type { get; }

        /// <summary>Place only.</summary>
        public ushort DefId { get; }

        /// <summary>Place: building origin. Remove: any cell of the building.</summary>
        public Int3 Cell { get; }

        /// <summary>Place only. 0-3, clockwise.</summary>
        public byte Rotation { get; }

        public static SimCommand Place(ushort defId, Int3 origin, int rotation) =>
            new SimCommand(CommandType.Place, defId, origin, (byte)(rotation & 3));

        public static SimCommand Remove(Int3 cell) => new SimCommand(CommandType.Remove, 0, cell, 0);

        public void Write(SimWriter writer)
        {
            writer.WriteByte((byte)Type);
            writer.WriteUShort(DefId);
            writer.WriteInt3(Cell);
            writer.WriteByte(Rotation);
        }

        public static SimCommand Read(SimReader reader)
        {
            var type = (CommandType)reader.ReadByte();
            if (type != CommandType.Place && type != CommandType.Remove)
                throw new FormatException($"Unknown command type {(byte)type}.");

            ushort defId = reader.ReadUShort();
            Int3 cell = reader.ReadInt3();
            byte rotation = reader.ReadByte();
            return new SimCommand(type, defId, cell, (byte)(rotation & 3));
        }

        public bool Equals(SimCommand other) =>
            Type == other.Type && DefId == other.DefId && Cell == other.Cell && Rotation == other.Rotation;

        public override bool Equals(object obj) => obj is SimCommand other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                return ((int)Type * 397) ^ (DefId * 31) ^ Cell.GetHashCode() ^ Rotation;
            }
        }

        public override string ToString() =>
            Type == CommandType.Place ? $"Place(def {DefId}, {Cell}, rot {Rotation})" : $"Remove({Cell})";
    }
}
