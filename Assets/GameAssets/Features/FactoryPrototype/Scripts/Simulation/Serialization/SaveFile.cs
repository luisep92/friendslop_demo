using System;

namespace Friendslop.Features.FactoryPrototype.Simulation
{
    /// <summary>
    /// Save file = header + full sim snapshot. The snapshot carries its own version.
    /// </summary>
    public static class SaveFile
    {
        // "FSAV", little-endian.
        private const uint Magic = 0x56415346;
        private const byte FormatVersion = 1;

        public static byte[] Write(FactorySim sim)
        {
            var writer = new SimWriter(1024);
            writer.WriteUInt(Magic);
            writer.WriteByte(FormatVersion);
            sim.WriteSnapshot(writer);
            return writer.ToArray();
        }

        /// <summary>Replaces the sim state. Throws FormatException on invalid data and keeps the current state.</summary>
        public static void Read(FactorySim sim, byte[] data)
        {
            var reader = new SimReader(data);
            if (reader.ReadUInt() != Magic)
                throw new FormatException("Not a factory save file.");

            byte version = reader.ReadByte();
            if (version != FormatVersion)
                throw new FormatException($"Unsupported save format {version}.");

            sim.ReadSnapshot(reader);
        }
    }
}
