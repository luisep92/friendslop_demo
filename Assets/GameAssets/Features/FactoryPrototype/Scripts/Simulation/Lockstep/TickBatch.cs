using System;
using System.Collections.Generic;

namespace Friendslop.Features.FactoryPrototype.Simulation
{
    /// <summary>
    /// What the server sends every tick: the commands it applied at that tick and, every
    /// ChecksumInterval ticks, the checksum of the resulting state.
    /// </summary>
    public sealed class TickBatch
    {
        public readonly List<SimCommand> Commands = new List<SimCommand>();
        public int Tick;
        public bool HasChecksum;
        public uint Checksum;

        public void Write(SimWriter writer)
        {
            if (Commands.Count > ushort.MaxValue)
                throw new InvalidOperationException("Too many commands in one tick.");

            writer.WriteInt(Tick);
            writer.WriteBool(HasChecksum);
            if (HasChecksum)
                writer.WriteUInt(Checksum);

            writer.WriteUShort((ushort)Commands.Count);
            for (int i = 0; i < Commands.Count; i++)
                Commands[i].Write(writer);
        }

        public static TickBatch Read(SimReader reader)
        {
            var batch = new TickBatch
            {
                Tick = reader.ReadInt(),
                HasChecksum = reader.ReadBool()
            };
            if (batch.HasChecksum)
                batch.Checksum = reader.ReadUInt();

            int count = reader.ReadUShort();
            for (int i = 0; i < count; i++)
                batch.Commands.Add(SimCommand.Read(reader));

            if (reader.Remaining != 0)
                throw new FormatException("Trailing data after tick batch.");
            return batch;
        }
    }
}
