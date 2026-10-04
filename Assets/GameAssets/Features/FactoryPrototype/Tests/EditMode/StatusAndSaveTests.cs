using System;
using Friendslop.Features.FactoryPrototype.Simulation;
using NUnit.Framework;
using static Friendslop.Features.FactoryPrototype.Tests.SimTestHelpers;

namespace Friendslop.Features.FactoryPrototype.Tests
{
    public class StatusAndSaveTests
    {
        [Test]
        public void Status_MapsCrafterAndSourceStates()
        {
            FactorySim sim = NewSim();
            Place(sim, PrototypeContent.Grinder, 5, 5);
            Assert.AreEqual(BuildingStatus.Waiting, At<CrafterBuilding>(sim, 5, 5).Status, "No inputs yet.");

            // Grinder fed, nothing takes its output: works, then blocks.
            Place(sim, PrototypeContent.HerbSource, 0, 0);
            Place(sim, PrototypeContent.Belt, 0, 1);
            Place(sim, PrototypeContent.Grinder, 0, 2);
            Run(sim, 80);
            Assert.AreEqual(BuildingStatus.Working, At<CrafterBuilding>(sim, 0, 2).Status);

            Run(sim, 1000);
            Assert.AreEqual(BuildingStatus.Blocked, At<CrafterBuilding>(sim, 0, 2).Status);
            Assert.AreEqual(BuildingStatus.Blocked, At<SourceBuilding>(sim, 0, 0).Status);
            Assert.AreEqual(BuildingStatus.None, At<BeltBuilding>(sim, 0, 1).Status);
        }

        [Test]
        public void Throughput_CountsProducedCraftedAndSold()
        {
            FactorySim sim = NewSim();
            Place(sim, PrototypeContent.HerbSource, 0, 0);
            Place(sim, PrototypeContent.Belt, 0, 1);
            Place(sim, PrototypeContent.Grinder, 0, 2);
            Place(sim, PrototypeContent.Belt, 0, 3);
            Place(sim, PrototypeContent.SellPoint, 0, 4);
            // Long enough for the belt and the grinder buffer to saturate: the source then runs at the grinder rate.
            Run(sim, 1000);

            var source = At<SourceBuilding>(sim, 0, 0);
            var grinder = At<CrafterBuilding>(sim, 0, 2);
            var sink = At<SinkBuilding>(sim, 0, 4);
            int produced = source.Throughput;
            int crafted = grinder.Throughput;
            int sold = sink.Throughput;
            Run(sim, 600);

            Assert.AreEqual(20, source.Throughput - produced);
            Assert.AreEqual(20, grinder.Throughput - crafted);
            Assert.AreEqual(20, sink.Throughput - sold);
        }

        [Test]
        public void SaveFile_RoundTrip_RestoresState()
        {
            FactorySim original = NewSim();
            Apply(original, DemoLayouts.MinimalLine(Int3.Zero, 0));
            Run(original, 700);
            byte[] save = SaveFile.Write(original);

            FactorySim loaded = NewSim();
            SaveFile.Read(loaded, save);

            Assert.AreEqual(original.ComputeChecksum(), loaded.ComputeChecksum());
            Run(original, 100);
            Run(loaded, 100);
            Assert.AreEqual(original.ComputeChecksum(), loaded.ComputeChecksum());
        }

        [Test]
        public void SaveFile_ExtraSection_RoundTrips()
        {
            FactorySim sim = NewSim();
            Apply(sim, DemoLayouts.MinimalLine(Int3.Zero, 0));
            var extra = new byte[] { 7, 0, 255, 42 };

            byte[] save = SaveFile.Write(sim, extra);
            CollectionAssert.AreEqual(extra, SaveFile.Read(NewSim(), save));
            CollectionAssert.IsEmpty(SaveFile.Read(NewSim(), SaveFile.Write(sim)));
        }

        [Test]
        public void SaveFile_InvalidExtraLength_KeepsState()
        {
            FactorySim sim = NewSim();
            Apply(sim, DemoLayouts.MinimalLine(Int3.Zero, 0));
            uint before = sim.ComputeChecksum();

            byte[] save = SaveFile.Write(NewSim(), new byte[] { 1, 2, 3 });
            save[5] = 0xFF; // Extra length low byte: now larger than the file.
            save[6] = 0xFF;
            Assert.Throws<FormatException>(() => SaveFile.Read(sim, save));

            Assert.AreEqual(before, sim.ComputeChecksum());
        }

        [Test]
        public void SaveFile_RejectsForeignData_AndKeepsState()
        {
            FactorySim sim = NewSim();
            Apply(sim, DemoLayouts.MinimalLine(Int3.Zero, 0));
            uint before = sim.ComputeChecksum();

            Assert.Throws<FormatException>(() => SaveFile.Read(sim, new byte[] { 1, 2, 3, 4, 5, 6 }));
            byte[] save = SaveFile.Write(sim);
            save[4] = 99; // Format version.
            Assert.Throws<FormatException>(() => SaveFile.Read(sim, save));

            Assert.AreEqual(before, sim.ComputeChecksum());
        }

        [Test]
        public void ServerLoad_OlderSave_ClientResyncsFromSnapshot()
        {
            var server = new LockstepServer(NewSim());
            foreach (SimCommand command in DemoLayouts.MinimalLine(Int3.Zero, 0))
                server.Enqueue(command);
            for (int i = 0; i < 200; i++)
                server.Step();
            byte[] save = SaveFile.Write(server.Sim);
            for (int i = 0; i < 300; i++)
                server.Step();

            var client = new LockstepClient(NewSim());
            byte[] join = server.CreateSnapshot();
            client.OnSnapshot(join, 0, join.Length);
            Pump(server, client, 50);

            Assert.IsTrue(server.TryLoad(save, out _, out string error), error);
            Assert.AreEqual(200, server.Sim.Tick, "Ticks go back to the saved tick.");
            byte[] resync = server.CreateSnapshot();
            Assert.IsTrue(client.OnSnapshot(resync, 0, resync.Length));
            Pump(server, client, 200);
            for (int i = 0; i < 20 && client.Buffered > 0; i++)
                client.Update(1.0 / SimConstants.TicksPerSecond);

            Assert.IsFalse(client.NeedsResync);
            Assert.AreEqual(server.Sim.Tick, client.Sim.Tick);
            Assert.AreEqual(server.Sim.ComputeChecksum(), client.Sim.ComputeChecksum());
            Assert.AreEqual(0, client.Mismatches);
        }

        [Test]
        public void ServerLoad_InvalidData_KeepsState()
        {
            var server = new LockstepServer(NewSim());
            server.Step();
            uint before = server.Sim.ComputeChecksum();

            Assert.IsFalse(server.TryLoad(new byte[] { 0, 0 }, out _, out string error));
            Assert.IsNotEmpty(error);
            Assert.AreEqual(before, server.Sim.ComputeChecksum());
        }

        private static void Pump(LockstepServer server, LockstepClient client, int ticks)
        {
            for (int i = 0; i < ticks; i++)
            {
                ArraySegment<byte> bytes = server.Step();
                var copy = new byte[bytes.Count];
                Array.Copy(bytes.Array, bytes.Offset, copy, 0, bytes.Count);
                client.OnBatch(copy, 0, copy.Length);
                client.Update(1.0 / SimConstants.TicksPerSecond);
            }
        }
    }
}
