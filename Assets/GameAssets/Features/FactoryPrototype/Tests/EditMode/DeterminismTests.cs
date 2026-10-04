using System;
using System.Collections.Generic;
using Friendslop.Features.FactoryPrototype.Simulation;
using NUnit.Framework;
using static Friendslop.Features.FactoryPrototype.Tests.SimTestHelpers;

namespace Friendslop.Features.FactoryPrototype.Tests
{
    public class DeterminismTests
    {
        [Test]
        public void TwoSims_SameCommands_SameChecksumEveryTick()
        {
            FactorySim a = NewSim();
            FactorySim b = NewSim();
            Dictionary<int, List<SimCommand>> script = BuildScript(seed: 1234, ticks: 3000);

            for (int tick = 1; tick <= 3000; tick++)
            {
                script.TryGetValue(tick, out List<SimCommand> commands);
                a.Advance(commands, null);
                b.Advance(commands, null);
                Assert.AreEqual(a.ComputeChecksum(), b.ComputeChecksum(), $"Diverged at tick {tick}.");
            }
        }

        [Test]
        public void Snapshot_RoundTrip_IsByteIdentical()
        {
            FactorySim source = NewSim();
            Apply(source, DemoLayouts.MinimalLine(Int3.Zero, 0));
            Run(source, 500);

            byte[] first = Snapshot(source);
            FactorySim copy = NewSim();
            copy.ReadSnapshot(new SimReader(first));

            CollectionAssert.AreEqual(first, Snapshot(copy));
            Assert.AreEqual(source.Tick, copy.Tick);
            Assert.AreEqual(source.Coins, copy.Coins);
        }

        [Test]
        public void Snapshot_LoadedSimContinuesIdentically()
        {
            FactorySim source = NewSim();
            Apply(source, DemoLayouts.MinimalLine(Int3.Zero, 0));
            Run(source, 500);

            FactorySim copy = NewSim();
            copy.ReadSnapshot(new SimReader(Snapshot(source)));
            Dictionary<int, List<SimCommand>> script = BuildScript(seed: 99, ticks: 1000);

            for (int i = 1; i <= 1000; i++)
            {
                script.TryGetValue(i, out List<SimCommand> commands);
                source.Advance(commands, null);
                copy.Advance(commands, null);
                Assert.AreEqual(source.ComputeChecksum(), copy.ComputeChecksum(), $"Diverged {i} ticks after load.");
            }
        }

        [Test]
        public void Snapshot_InvalidData_ThrowsAndKeepsState()
        {
            FactorySim sim = NewSim();
            Apply(sim, DemoLayouts.MinimalLine(Int3.Zero, 0));
            Run(sim, 50);
            uint before = sim.ComputeChecksum();

            byte[] wrongVersion = Snapshot(sim);
            wrongVersion[0] = 99;
            Assert.Throws<FormatException>(() => sim.ReadSnapshot(new SimReader(wrongVersion)));

            byte[] truncated = Snapshot(sim);
            Assert.Throws<FormatException>(() => sim.ReadSnapshot(new SimReader(truncated, 0, truncated.Length - 3)));

            Assert.AreEqual(before, sim.ComputeChecksum());
        }

        [Test]
        public void Snapshot_RaisesStateReset()
        {
            FactorySim source = NewSim();
            Apply(source, DemoLayouts.MinimalLine(Int3.Zero, 0));
            FactorySim copy = NewSim();
            int resets = 0;
            copy.StateReset += () => resets++;

            copy.ReadSnapshot(new SimReader(Snapshot(source)));

            Assert.AreEqual(1, resets);
            Assert.AreEqual(source.Buildings.Count, copy.Buildings.Count);
        }

        [Test]
        public void Checksum_DetectsCorruption()
        {
            FactorySim sim = NewSim();
            uint before = sim.ComputeChecksum();
            sim.DebugCorruptState();
            Assert.AreNotEqual(before, sim.ComputeChecksum());
        }

        private static byte[] Snapshot(FactorySim sim)
        {
            var writer = new SimWriter();
            sim.WriteSnapshot(writer);
            return writer.ToArray();
        }

        /// <summary>Demo line at tick 1, then random belt placements and removals around it.</summary>
        internal static Dictionary<int, List<SimCommand>> BuildScript(int seed, int ticks)
        {
            var random = new Random(seed);
            var script = new Dictionary<int, List<SimCommand>>
            {
                [1] = DemoLayouts.MinimalLine(Int3.Zero, 0)
            };

            for (int tick = 2; tick <= ticks; tick += random.Next(5, 40))
            {
                var commands = new List<SimCommand>();
                for (int i = random.Next(1, 4); i > 0; i--)
                {
                    var cell = new Int3(random.Next(-6, 7), 0, random.Next(-3, 14));
                    commands.Add(random.Next(4) == 0
                        ? SimCommand.Remove(cell)
                        : SimCommand.Place(PrototypeContent.Belt, cell, random.Next(4)));
                }
                script[tick] = commands;
            }
            return script;
        }
    }
}
