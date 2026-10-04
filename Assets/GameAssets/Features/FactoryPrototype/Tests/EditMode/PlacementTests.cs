using Friendslop.Features.FactoryPrototype.Simulation;
using NUnit.Framework;
using static Friendslop.Features.FactoryPrototype.Tests.SimTestHelpers;

namespace Friendslop.Features.FactoryPrototype.Tests
{
    public class PlacementTests
    {
        [Test]
        public void Placement_RejectsOverlap()
        {
            FactorySim sim = NewSim();
            Place(sim, PrototypeContent.Mixer, 0, 0);

            Assert.IsFalse(sim.CanPlace(PrototypeContent.Belt, new Int3(1, 0, 0), 0));
            Assert.IsFalse(sim.TryApply(SimCommand.Place(PrototypeContent.Belt, new Int3(1, 0, 0), 0)));
            Assert.AreEqual(1, sim.Buildings.Count);
        }

        [Test]
        public void Placement_RejectsOutOfBoundsOtherLevelsAndUnknownDefs()
        {
            FactorySim sim = NewSim();
            Assert.IsTrue(sim.CanPlace(PrototypeContent.Belt, new Int3(SimConstants.GridMax, 0, 0), 0));
            Assert.IsFalse(sim.CanPlace(PrototypeContent.Belt, new Int3(SimConstants.GridMax + 1, 0, 0), 0));
            Assert.IsFalse(sim.CanPlace(PrototypeContent.Mixer, new Int3(SimConstants.GridMax, 0, 0), 0));
            Assert.IsTrue(sim.CanPlace(PrototypeContent.Mixer, new Int3(SimConstants.GridMax, 0, 0), 2));
            Assert.IsFalse(sim.CanPlace(PrototypeContent.Belt, new Int3(0, 1, 0), 0));
            Assert.IsFalse(sim.CanPlace(99, Int3.Zero, 0));
        }

        [Test]
        public void Remove_ByAnyCell_FreesAllCells()
        {
            FactorySim sim = NewSim();
            Place(sim, PrototypeContent.Mixer, 0, 0);

            Assert.IsTrue(sim.TryApply(SimCommand.Remove(new Int3(1, 0, 0))));
            Assert.IsNull(sim.GetBuildingAt(Int3.Zero));
            Assert.IsNull(sim.GetBuildingAt(new Int3(1, 0, 0)));
            Assert.IsFalse(sim.TryApply(SimCommand.Remove(Int3.Zero)));
        }

        [Test]
        public void Ids_AreMonotonicAfterRemoval()
        {
            FactorySim sim = NewSim();
            Place(sim, PrototypeContent.Belt, 0, 0);
            int firstId = sim.GetBuildingAt(Int3.Zero).Id;
            sim.TryApply(SimCommand.Remove(Int3.Zero));
            Place(sim, PrototypeContent.Belt, 0, 0);

            Assert.Greater(sim.GetBuildingAt(Int3.Zero).Id, firstId);
        }

        [Test]
        public void Events_RaisedForAddAndRemove()
        {
            FactorySim sim = NewSim();
            int added = 0;
            int removed = 0;
            sim.BuildingAdded += _ => added++;
            sim.BuildingRemoved += _ => removed++;

            Place(sim, PrototypeContent.Belt, 0, 0);
            sim.TryApply(SimCommand.Remove(Int3.Zero));
            sim.TryApply(SimCommand.Remove(Int3.Zero));

            Assert.AreEqual(1, added);
            Assert.AreEqual(1, removed);
        }

        [Test]
        public void Advance_AppliesValidCommandsAndReportsThem()
        {
            FactorySim sim = NewSim();
            var commands = new[]
            {
                SimCommand.Place(PrototypeContent.Belt, Int3.Zero, 0),
                SimCommand.Place(PrototypeContent.Belt, Int3.Zero, 1),
                SimCommand.Remove(new Int3(5, 0, 5))
            };
            var applied = new System.Collections.Generic.List<SimCommand>();

            int count = sim.Advance(commands, applied);

            Assert.AreEqual(1, count);
            CollectionAssert.AreEqual(new[] { commands[0] }, applied);
            Assert.AreEqual(1, sim.Tick);
        }
    }
}
