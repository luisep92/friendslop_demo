using System;

namespace Friendslop.Features.FactoryPrototype.Simulation
{
    /// <summary>
    /// Save file = header + extra section + full sim snapshot. The snapshot carries its own version.
    /// The extra section holds data outside the sim (player transforms); the sim treats it as opaque bytes.
    /// </summary>
    public static class SaveFile
    {
        // "FSAV", little-endian.
        private const uint Magic = 0x56415346;

        // 1: snapshot only. 2: extra section before the snapshot.
        private const byte FormatVersion = 2;
        private const int MaxExtraBytes = 1 << 20;

        public static byte[] Write(FactorySim sim, byte[] extra = null)
        {
            extra ??= Array.Empty<byte>();
            var writer = new SimWriter(1024 + extra.Length);
            writer.WriteUInt(Magic);
            writer.WriteByte(FormatVersion);
            writer.WriteInt(extra.Length);
            for (int i = 0; i < extra.Length; i++)
                writer.WriteByte(extra[i]);
            sim.WriteSnapshot(writer);
            return writer.ToArray();
        }

        /// <summary>
        /// Replaces the sim state and returns the extra section (empty if none).
        /// Throws FormatException on invalid data and keeps the current state.
        /// </summary>
        public static byte[] Read(FactorySim sim, byte[] data)
        {
            var reader = new SimReader(data);
            if (reader.ReadUInt() != Magic)
                throw new FormatException("Not a factory save file.");

            byte version = reader.ReadByte();
            if (version < 1 || version > FormatVersion)
                throw new FormatException($"Unsupported save format {version}.");

            byte[] extra = Array.Empty<byte>();
            if (version >= 2)
            {
                int length = reader.ReadInt();
                if (length < 0 || length > MaxExtraBytes || length > reader.Remaining)
                    throw new FormatException($"Invalid extra section length {length}.");

                extra = new byte[length];
                for (int i = 0; i < length; i++)
                    extra[i] = reader.ReadByte();
            }

            sim.ReadSnapshot(reader);
            return extra;
        }
    }
}
