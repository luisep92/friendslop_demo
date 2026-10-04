using System;
using System.Collections.Generic;

namespace Friendslop.Features.FactoryPrototype.Simulation
{
    /// <summary>
    /// Replica side of the server-driven lockstep. Starts from a snapshot, then replays the server's
    /// tick batches a few ticks behind it. Any checksum mismatch, tick gap or bad data flags a resync:
    /// the caller requests a new snapshot (see ConsumeResyncRequest).
    /// </summary>
    public sealed class LockstepClient
    {
        /// <summary>Batches kept buffered to absorb network jitter.</summary>
        public const int TargetBuffer = 2;

        /// <summary>Above this, take one extra step per update.</summary>
        public const int SoftCatchUp = 4;

        /// <summary>Above this, jump straight back to TargetBuffer.</summary>
        public const int HardCatchUp = 10;

        private readonly Queue<TickBatch> _queue = new Queue<TickBatch>();
        private readonly FixedStepClock _clock = new FixedStepClock(SimConstants.TicksPerSecond, 4);
        private bool _started;
        private bool _resyncRequested;

        public LockstepClient(FactorySim sim)
        {
            Sim = sim ?? throw new ArgumentNullException(nameof(sim));
        }

        public FactorySim Sim { get; }
        public bool HasState { get; private set; }
        public bool NeedsResync { get; private set; }
        public int LastReceivedTick { get; private set; }
        public int Buffered => _queue.Count;
        public int ChecksumsOk { get; private set; }
        public int Mismatches { get; private set; }
        public int Resyncs { get; private set; }
        public float Alpha => _clock.Alpha;

        /// <summary>Loads a full state. Returns false (and flags a new resync) on invalid data.</summary>
        public bool OnSnapshot(byte[] data, int offset, int count)
        {
            try
            {
                Sim.ReadSnapshot(new SimReader(data, offset, count));
            }
            catch (FormatException)
            {
                FlagResync();
                _resyncRequested = false;
                return false;
            }

            if (HasState)
                Resyncs++;

            HasState = true;
            NeedsResync = false;
            _resyncRequested = false;
            _queue.Clear();
            LastReceivedTick = Sim.Tick;
            _started = false;
            _clock.Reset();
            return true;
        }

        public void OnBatch(byte[] data, int offset, int count)
        {
            if (!HasState || NeedsResync)
                return;

            TickBatch batch;
            try
            {
                batch = TickBatch.Read(new SimReader(data, offset, count));
            }
            catch (FormatException)
            {
                FlagResync();
                return;
            }

            // Already contained in the snapshot.
            if (batch.Tick <= LastReceivedTick)
                return;

            if (batch.Tick != LastReceivedTick + 1)
            {
                FlagResync();
                return;
            }

            _queue.Enqueue(batch);
            LastReceivedTick = batch.Tick;
        }

        /// <summary>Steps the replica for the elapsed time. Returns the number of ticks stepped.</summary>
        public int Update(double deltaSeconds)
        {
            if (!HasState || NeedsResync)
                return 0;

            int due = _clock.Accumulate(deltaSeconds);
            if (!_started)
            {
                if (_queue.Count < TargetBuffer)
                {
                    _clock.Reset();
                    return 0;
                }
                _started = true;
            }

            int steps = due;
            if (_queue.Count > HardCatchUp)
                steps = _queue.Count - TargetBuffer;
            else if (_queue.Count > SoftCatchUp)
                steps = due + 1;
            steps = Math.Min(steps, _queue.Count);

            int stepped = 0;
            while (stepped < steps && !NeedsResync)
            {
                StepOne(_queue.Dequeue());
                stepped++;
            }

            if (_queue.Count == 0)
                _clock.Clamp();
            return stepped;
        }

        /// <summary>True once per resync: the caller must then request a snapshot from the server.</summary>
        public bool ConsumeResyncRequest()
        {
            if (!NeedsResync || _resyncRequested)
                return false;

            _resyncRequested = true;
            return true;
        }

        private void StepOne(TickBatch batch)
        {
            int applied = Sim.Advance(batch.Commands, null);
            if (applied != batch.Commands.Count || Sim.Tick != batch.Tick)
            {
                Mismatches++;
                FlagResync();
                return;
            }

            if (!batch.HasChecksum)
                return;

            if (Sim.ComputeChecksum() == batch.Checksum)
            {
                ChecksumsOk++;
            }
            else
            {
                Mismatches++;
                FlagResync();
            }
        }

        private void FlagResync()
        {
            NeedsResync = true;
            _queue.Clear();
        }
    }
}
