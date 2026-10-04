using System.Collections.Generic;
using Friendslop.Features.FactoryPrototype.Simulation;
using NUnit.Framework;
using static Friendslop.Features.FactoryPrototype.Tests.SimTestHelpers;

namespace Friendslop.Features.FactoryPrototype.Tests
{
    public class BeltTests
    {
        // Herb source emits 1 item / 20 ticks: exactly 30 items in any 600 tick window once saturated.
        private const int Warmup = 400;
        private const int Window = 600;
        private const int ItemsPerWindow = Window / 20;

        [Test]
        public void StraightLine_DeliversAtSourceRate()
        {
            FactorySim sim = NewSim();
            Place(sim, PrototypeContent.HerbSource, 0, 0);
            for (int z = 1; z <= 5; z++)
                Place(sim, PrototypeContent.Belt, 0, z);
            Place(sim, PrototypeContent.SellPoint, 0, 6);
            var sink = At<SinkBuilding>(sim, 0, 6);

            Run(sim, Warmup);
            long coinsBefore = sim.Coins;

            Assert.AreEqual(ItemsPerWindow, DeliveredDuring(sim, sink, Window));
            Assert.AreEqual(ItemsPerWindow, sim.Coins - coinsBefore);
        }

        [Test]
        public void Corner_SideLoading_Delivers()
        {
            FactorySim sim = NewSim();
            Place(sim, PrototypeContent.HerbSource, 2, 0, 3);
            Place(sim, PrototypeContent.Belt, 1, 0, 3);
            Place(sim, PrototypeContent.Belt, 0, 0, 0);
            Place(sim, PrototypeContent.Belt, 0, 1, 0);
            Place(sim, PrototypeContent.SellPoint, 0, 2);
            var sink = At<SinkBuilding>(sim, 0, 2);

            Run(sim, Warmup);
            Assert.AreEqual(ItemsPerWindow, DeliveredDuring(sim, sink, Window));

            int seen = 0;
            for (int i = 0; i < 40; i++)
            {
                sim.Advance(null, null);
                foreach (BeltItem item in At<BeltBuilding>(sim, 0, 0).Items)
                {
                    Assert.AreEqual(Dir.East, item.EntrySide, "Corner items enter through the east side.");
                    seen++;
                }
            }
            Assert.Greater(seen, 0);
        }

        [Test]
        public void DeadEnd_BackPressure_SaturatesWithoutOverlap()
        {
            FactorySim sim = NewSim();
            Place(sim, PrototypeContent.HerbSource, 0, 0);
            for (int z = 1; z <= 4; z++)
                Place(sim, PrototypeContent.Belt, 0, z);

            Run(sim, 1000);
            int saturated = CountBeltItems(sim);
            Run(sim, 200);

            Assert.AreEqual(saturated, CountBeltItems(sim), "Item count must stay constant once saturated.");
            Assert.GreaterOrEqual(saturated, 16);
            Assert.IsTrue(At<SourceBuilding>(sim, 0, 0).IsBlocked);
            AssertLineSpacing(sim, 1, 4);
        }

        [Test]
        public void Unblock_Resumes()
        {
            FactorySim sim = NewSim();
            Place(sim, PrototypeContent.HerbSource, 0, 0);
            for (int z = 1; z <= 4; z++)
                Place(sim, PrototypeContent.Belt, 0, z);
            Run(sim, 1000);

            Place(sim, PrototypeContent.SellPoint, 0, 5);
            var sink = At<SinkBuilding>(sim, 0, 5);
            Run(sim, Warmup);

            Assert.AreEqual(ItemsPerWindow, DeliveredDuring(sim, sink, Window));
            Assert.IsFalse(At<SourceBuilding>(sim, 0, 0).IsBlocked);
        }

        [Test]
        public void RemoveBeltWithItems_ThenReplace_Resumes()
        {
            FactorySim sim = NewSim();
            Place(sim, PrototypeContent.HerbSource, 0, 0);
            for (int z = 1; z <= 5; z++)
                Place(sim, PrototypeContent.Belt, 0, z);
            Place(sim, PrototypeContent.SellPoint, 0, 6);
            var sink = At<SinkBuilding>(sim, 0, 6);
            Run(sim, 300);

            Assert.IsTrue(sim.TryApply(SimCommand.Remove(new Int3(0, 0, 3))));
            Run(sim, 200);
            Assert.AreEqual(0, DeliveredDuring(sim, sink, 100), "Downstream must drain and stop.");

            Place(sim, PrototypeContent.Belt, 0, 3);
            Run(sim, Warmup);
            Assert.AreEqual(ItemsPerWindow, DeliveredDuring(sim, sink, Window));
        }

        [Test]
        public void Belt_RejectsItemsEnteringThroughItsFront()
        {
            FactorySim sim = NewSim();
            Place(sim, PrototypeContent.HerbSource, 0, 0);
            Place(sim, PrototypeContent.Belt, 0, 1, 2); // Faces south, towards the source.

            Run(sim, 100);

            Assert.AreEqual(0, CountBeltItems(sim));
            Assert.IsTrue(At<SourceBuilding>(sim, 0, 0).IsBlocked);
        }

        /// <summary>Straight north line from zStart to zEnd: consecutive items are at least BeltSpacing apart.</summary>
        private static void AssertLineSpacing(FactorySim sim, int zStart, int zEnd)
        {
            var positions = new List<int>();
            for (int z = zStart; z <= zEnd; z++)
            {
                foreach (BeltItem item in At<BeltBuilding>(sim, 0, z).Items)
                    positions.Add((z - zStart) * SimConstants.TileLength + item.Progress);
            }

            positions.Sort();
            for (int i = 1; i < positions.Count; i++)
                Assert.GreaterOrEqual(positions[i] - positions[i - 1], SimConstants.BeltSpacing, $"Items too close at {positions[i]}.");
        }
    }
}
