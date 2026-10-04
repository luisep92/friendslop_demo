using System;
using System.Collections.Generic;
using Friendslop.Features.FactoryPrototype.Simulation;
using NUnit.Framework;
using static Friendslop.Features.FactoryPrototype.Tests.SimTestHelpers;

namespace Friendslop.Features.FactoryPrototype.Tests
{
    public class LockstepTests
    {
        private const double FrameSeconds = 1.0 / SimConstants.TicksPerSecond;

        [Test]
        public void ClientFromSnapshot_TracksServer()
        {
            LockstepServer server = NewServerWithDemo(200);
            LockstepClient client = Join(server);
            Dictionary<int, List<SimCommand>> script = DeterminismTests.BuildScript(seed: 7, ticks: 1000);

            Pump(server, client, 1000, script);
            Drain(client);

            AssertInSync(server, client);
            Assert.AreEqual(0, client.Mismatches);
            Assert.AreEqual(0, client.Resyncs);
            Assert.Greater(client.ChecksumsOk, 0);
        }

        [Test]
        public void ChecksumMismatch_Resync_Recovers()
        {
            LockstepServer server = NewServerWithDemo(100);
            LockstepClient client = Join(server);
            Pump(server, client, 100);

            client.Sim.DebugCorruptState();
            for (int i = 0; i < 40 && !client.NeedsResync; i++)
                Pump(server, client, 1);

            Assert.IsTrue(client.NeedsResync);
            Assert.IsTrue(client.ConsumeResyncRequest());
            Assert.IsFalse(client.ConsumeResyncRequest(), "Only one request per resync.");

            Pump(server, client, 5); // Batches sent before the snapshot are dropped.
            byte[] snapshot = server.CreateSnapshot();
            Assert.IsTrue(client.OnSnapshot(snapshot, 0, snapshot.Length));
            Pump(server, client, 200);
            Drain(client);

            AssertInSync(server, client);
            Assert.AreEqual(1, client.Mismatches);
            Assert.AreEqual(1, client.Resyncs);
        }

        [Test]
        public void TickGap_FlagsResync()
        {
            LockstepServer server = NewServerWithDemo(10);
            LockstepClient client = Join(server);

            Deliver(client, server.Step());
            server.Step(); // Lost.
            Deliver(client, server.Step());

            Assert.IsTrue(client.NeedsResync);
        }

        [Test]
        public void InvalidSnapshot_FlagsResyncAndAllowsNewRequest()
        {
            LockstepServer server = NewServerWithDemo(10);
            LockstepClient client = Join(server);
            client.ConsumeResyncRequest();

            Assert.IsFalse(client.OnSnapshot(new byte[] { 1, 2, 3 }, 0, 3));
            Assert.IsTrue(client.NeedsResync);
            Assert.IsTrue(client.ConsumeResyncRequest());
        }

        [Test]
        public void Pacing_WaitsForBuffer_NeverPassesReceived_CatchesUp()
        {
            LockstepServer server = NewServerWithDemo(10);
            LockstepClient client = Join(server);
            int start = client.Sim.Tick;

            Deliver(client, server.Step());
            client.Update(FrameSeconds);
            Assert.AreEqual(start, client.Sim.Tick, "Must wait until TargetBuffer batches are buffered.");

            for (int i = 0; i < 19; i++)
                Deliver(client, server.Step());
            client.Update(FrameSeconds);
            Assert.AreEqual(LockstepClient.TargetBuffer, client.Buffered, "Hard catch-up back to the target buffer.");

            for (int i = 0; i < 10; i++)
            {
                client.Update(FrameSeconds);
                Assert.LessOrEqual(client.Sim.Tick, client.LastReceivedTick);
            }
            Assert.AreEqual(client.LastReceivedTick, client.Sim.Tick);
        }

        [Test]
        public void RejectedCommands_AreNotBroadcast()
        {
            var server = new LockstepServer(NewSim());
            server.Enqueue(SimCommand.Place(PrototypeContent.Belt, Int3.Zero, 0));
            server.Enqueue(SimCommand.Place(PrototypeContent.Belt, Int3.Zero, 1));
            server.Enqueue(SimCommand.Remove(new Int3(9, 0, 9)));

            ArraySegment<byte> bytes = server.Step();
            TickBatch batch = TickBatch.Read(new SimReader(bytes.Array, bytes.Offset, bytes.Count));

            Assert.AreEqual(1, batch.Tick);
            Assert.AreEqual(1, batch.Commands.Count);
            Assert.AreEqual(SimCommand.Place(PrototypeContent.Belt, Int3.Zero, 0), batch.Commands[0]);
        }

        [Test]
        public void Server_LimitsCommandsPerTick()
        {
            var server = new LockstepServer(NewSim());
            for (int i = 0; i < LockstepServer.MaxCommandsPerTick; i++)
                Assert.IsTrue(server.Enqueue(SimCommand.Remove(Int3.Zero)));
            Assert.IsFalse(server.Enqueue(SimCommand.Remove(Int3.Zero)));
        }

        [Test]
        public void TickBatch_RoundTrip_AndChecksumInterval()
        {
            LockstepServer server = NewServerWithDemo(0);
            TickBatch last = null;
            for (int i = 0; i < SimConstants.ChecksumInterval; i++)
            {
                ArraySegment<byte> bytes = server.Step();
                last = TickBatch.Read(new SimReader(bytes.Array, bytes.Offset, bytes.Count));
                Assert.AreEqual(last.Tick % SimConstants.ChecksumInterval == 0, last.HasChecksum);
            }

            Assert.IsTrue(last.HasChecksum);
            Assert.AreEqual(server.Sim.ComputeChecksum(), last.Checksum);
        }

        [Test]
        public void FixedStepClock_StepsAndDropsExcess()
        {
            var clock = new FixedStepClock(20, 4);
            Assert.AreEqual(2, clock.Accumulate(0.12));
            Assert.AreEqual(0.4f, clock.Alpha, 0.001f);
            Assert.AreEqual(4, clock.Accumulate(1.0));
            Assert.AreEqual(0f, clock.Alpha);
        }

        private static LockstepServer NewServerWithDemo(int ticks)
        {
            var server = new LockstepServer(NewSim());
            foreach (SimCommand command in DemoLayouts.MinimalLine(Int3.Zero, 0))
                server.Enqueue(command);
            for (int i = 0; i < ticks; i++)
                server.Step();
            return server;
        }

        private static LockstepClient Join(LockstepServer server)
        {
            var client = new LockstepClient(NewSim());
            byte[] snapshot = server.CreateSnapshot();
            Assert.IsTrue(client.OnSnapshot(snapshot, 0, snapshot.Length));
            return client;
        }

        /// <summary>Copies the segment like the network layer must (the server reuses its buffer).</summary>
        private static void Deliver(LockstepClient client, ArraySegment<byte> bytes)
        {
            var copy = new byte[bytes.Count];
            Array.Copy(bytes.Array, bytes.Offset, copy, 0, bytes.Count);
            client.OnBatch(copy, 0, copy.Length);
        }

        private static void Pump(LockstepServer server, LockstepClient client, int ticks,
            Dictionary<int, List<SimCommand>> script = null)
        {
            for (int i = 1; i <= ticks; i++)
            {
                if (script != null && script.TryGetValue(i, out List<SimCommand> commands))
                {
                    foreach (SimCommand command in commands)
                        server.Enqueue(command);
                }

                Deliver(client, server.Step());
                client.Update(FrameSeconds);
            }
        }

        private static void Drain(LockstepClient client)
        {
            for (int i = 0; i < 100 && client.Buffered > 0; i++)
                client.Update(FrameSeconds);
        }

        private static void AssertInSync(LockstepServer server, LockstepClient client)
        {
            Assert.IsFalse(client.NeedsResync);
            Assert.AreEqual(server.Sim.Tick, client.Sim.Tick);
            Assert.AreEqual(server.Sim.ComputeChecksum(), client.Sim.ComputeChecksum());
        }
    }
}
