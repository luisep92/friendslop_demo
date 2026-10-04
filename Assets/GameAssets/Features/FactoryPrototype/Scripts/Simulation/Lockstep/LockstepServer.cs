using System;
using System.Collections.Generic;

namespace Friendslop.Features.FactoryPrototype.Simulation
{
    /// <summary>
    /// Authoritative side of the server-driven lockstep. Owns the canonical sim and the command order.
    /// Transport agnostic: produces bytes, the network layer only forwards them.
    /// </summary>
    public sealed class LockstepServer
    {
        public const int MaxCommandsPerTick = 256;

        private readonly List<SimCommand> _pending = new List<SimCommand>();
        private readonly TickBatch _batch = new TickBatch();
        private readonly SimWriter _batchWriter = new SimWriter();
        private readonly SimWriter _snapshotWriter = new SimWriter(1024);

        public LockstepServer(FactorySim sim)
        {
            Sim = sim ?? throw new ArgumentNullException(nameof(sim));
        }

        public FactorySim Sim { get; }

        /// <summary>Queues a request for the next tick, where it is validated. False if the tick is full.</summary>
        public bool Enqueue(in SimCommand command)
        {
            if (_pending.Count >= MaxCommandsPerTick)
                return false;

            _pending.Add(command);
            return true;
        }

        /// <summary>
        /// Runs one tick. Returns the encoded batch with the applied commands only.
        /// The segment is reused: valid until the next call.
        /// </summary>
        public ArraySegment<byte> Step()
        {
            _batch.Commands.Clear();
            Sim.Advance(_pending, _batch.Commands);
            _pending.Clear();

            _batch.Tick = Sim.Tick;
            _batch.HasChecksum = Sim.Tick % SimConstants.ChecksumInterval == 0;
            _batch.Checksum = _batch.HasChecksum ? Sim.ComputeChecksum() : 0;

            _batchWriter.Clear();
            _batch.Write(_batchWriter);
            return new ArraySegment<byte>(_batchWriter.Buffer, 0, _batchWriter.Length);
        }

        /// <summary>Full state at the current tick. Clients apply batches with a greater tick after it.</summary>
        public byte[] CreateSnapshot()
        {
            _snapshotWriter.Clear();
            Sim.WriteSnapshot(_snapshotWriter);
            return _snapshotWriter.ToArray();
        }
    }
}
