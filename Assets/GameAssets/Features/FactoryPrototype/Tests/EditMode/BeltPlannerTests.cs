using System.Collections.Generic;
using System.Linq;
using Friendslop.Features.FactoryPrototype.Simulation;
using NUnit.Framework;
using static Friendslop.Features.FactoryPrototype.Tests.SimTestHelpers;

namespace Friendslop.Features.FactoryPrototype.Tests
{
    public class BeltPlannerTests
    {
        private readonly List<BeltTile> _tiles = new List<BeltTile>();

        [Test]
        public void Straight_FacesTravelDirection()
        {
            BeltPlanner.Plan(new Int3(0, 0, 0), new Int3(0, 0, 4), null, null, false, 0, _tiles);

            CollectionAssert.AreEqual(Enumerable.Range(0, 5).Select(z => new Int3(0, 0, z)), _tiles.Select(t => t.Cell));
            Assert.IsTrue(_tiles.All(t => t.Rotation == (int)Dir.North));
        }

        [Test]
        public void LPath_LongerAxisFirst_FlipSwapsLegs()
        {
            BeltPlanner.Plan(Int3.Zero, new Int3(3, 0, 2), null, null, false, 0, _tiles);
            CollectionAssert.AreEqual(new[] { C(0, 0), C(1, 0), C(2, 0), C(3, 0), C(3, 1), C(3, 2) }, _tiles.Select(t => t.Cell));
            CollectionAssert.AreEqual(new[] { 1, 1, 1, 0, 0, 0 }, _tiles.Select(t => t.Rotation));

            BeltPlanner.Plan(Int3.Zero, new Int3(3, 0, 2), null, null, true, 0, _tiles);
            CollectionAssert.AreEqual(new[] { C(0, 0), C(0, 1), C(0, 2), C(1, 2), C(2, 2), C(3, 2) }, _tiles.Select(t => t.Cell));
            CollectionAssert.AreEqual(new[] { 0, 0, 1, 1, 1, 1 }, _tiles.Select(t => t.Rotation));
        }

        [Test]
        public void StartFlow_ForcesFirstLegAxis_EndFlow_SetsLastTile()
        {
            // Longer axis is X, but the feeder pushes north: go north first.
            BeltPlanner.Plan(Int3.Zero, new Int3(4, 0, 1), Dir.North, Dir.South, false, 0, _tiles);

            Assert.AreEqual(C(0, 1), _tiles[1].Cell);
            Assert.AreEqual((int)Dir.North, _tiles[0].Rotation);
            Assert.AreEqual((int)Dir.South, _tiles[_tiles.Count - 1].Rotation);
        }

        [Test]
        public void SingleTile_UsesEndFlowThenStartFlowThenDefault()
        {
            BeltPlanner.Plan(Int3.Zero, Int3.Zero, Dir.North, Dir.West, false, 2, _tiles);
            Assert.AreEqual((int)Dir.West, _tiles.Single().Rotation);

            BeltPlanner.Plan(Int3.Zero, Int3.Zero, Dir.East, null, false, 2, _tiles);
            Assert.AreEqual((int)Dir.East, _tiles.Single().Rotation);

            BeltPlanner.Plan(Int3.Zero, Int3.Zero, null, null, false, 2, _tiles);
            Assert.AreEqual(2, _tiles.Single().Rotation);
        }

        [Test]
        public void TryFindFeeder_MachineOutputAndBeltFront()
        {
            FactorySim sim = NewSim();
            Place(sim, PrototypeContent.Grinder, 0, 0);
            Place(sim, PrototypeContent.Belt, 5, 5, 1);

            Assert.IsTrue(BeltPlanner.TryFindFeeder(sim, C(0, 1), out Dir fromGrinder));
            Assert.AreEqual(Dir.North, fromGrinder);
            Assert.IsTrue(BeltPlanner.TryFindFeeder(sim, C(6, 5), out Dir fromBelt));
            Assert.AreEqual(Dir.East, fromBelt);
            Assert.IsFalse(BeltPlanner.TryFindFeeder(sim, C(1, 0), out _), "Grinder has no port on its east side.");
            Assert.IsFalse(BeltPlanner.TryFindFeeder(sim, C(4, 5), out _), "Behind a belt is not its output.");
        }

        [Test]
        public void TryFindConsumer_AheadFirst_ThenOnlyMachineInput_NeverSideBelts()
        {
            FactorySim sim = NewSim();
            Place(sim, PrototypeContent.Grinder, 0, 2);
            Place(sim, PrototypeContent.Belt, 1, 1, 1); // East of cell (0, 1), facing east.

            Assert.IsTrue(BeltPlanner.TryFindConsumer(sim, C(0, 1), Dir.East, out Dir ahead));
            Assert.AreEqual(Dir.East, ahead, "What is straight ahead wins.");

            Assert.IsTrue(BeltPlanner.TryFindConsumer(sim, C(0, 1), Dir.West, out Dir machine));
            Assert.AreEqual(Dir.North, machine, "Nothing ahead: the only machine input.");

            Place(sim, PrototypeContent.Belt, 5, 5, 0); // Parallel line.
            Assert.IsFalse(BeltPlanner.TryFindConsumer(sim, C(4, 5), Dir.North, out _), "Side belts never snap on their own.");
            Assert.IsFalse(BeltPlanner.TryFindConsumer(sim, C(1, 2), null, out _), "Grinder has no port on its east side.");
        }

        [Test]
        public void PlanRun_GapInLine_ContinuesIntoBeltAheadNotIntoSideLine()
        {
            // Line flowing west with a gap at (1, 0). Ahead, (0, 0) turns north (side-load from the east).
            // Above the gap, (1, 1) faces north and would take items through its back.
            FactorySim sim = NewSim();
            Place(sim, PrototypeContent.Belt, 2, 0, 3);
            Place(sim, PrototypeContent.Belt, 0, 0, 0);
            Place(sim, PrototypeContent.Belt, 1, 1, 0);

            Assert.IsTrue(BeltPlanner.TryFindFeeder(sim, C(1, 0), out Dir startFlow));
            BeltPlanner.PlanRun(sim, C(1, 0), C(1, 0), startFlow, null, false, 0, _tiles);

            Assert.AreEqual((int)Dir.West, _tiles.Single().Rotation);
        }

        [Test]
        public void PlanRun_EndOverride_Wins()
        {
            FactorySim sim = NewSim();
            Place(sim, PrototypeContent.Grinder, 0, 3);

            BeltPlanner.PlanRun(sim, C(0, 0), C(0, 2), null, null, false, 0, _tiles);
            Assert.AreEqual((int)Dir.North, _tiles.Last().Rotation, "Snaps into the grinder input.");

            BeltPlanner.PlanRun(sim, C(0, 0), C(0, 2), null, Dir.East, false, 0, _tiles);
            Assert.AreEqual((int)Dir.East, _tiles.Last().Rotation);
        }

        [Test]
        public void PlannedRun_IntoMachine_Delivers()
        {
            FactorySim sim = NewSim();
            Place(sim, PrototypeContent.HerbSource, 0, 0);
            Place(sim, PrototypeContent.SellPoint, 4, 3);

            Assert.IsTrue(BeltPlanner.TryFindFeeder(sim, C(0, 1), out Dir startFlow));
            // End next to the sell point's west side.
            BeltPlanner.PlanRun(sim, C(0, 1), C(3, 3), startFlow, null, false, 0, _tiles);
            Assert.AreEqual((int)Dir.East, _tiles.Last().Rotation);
            foreach (BeltTile tile in _tiles)
                Assert.IsTrue(sim.TryApply(SimCommand.Place(PrototypeContent.Belt, tile.Cell, tile.Rotation)), tile.Cell.ToString());

            Run(sim, 400);
            Assert.AreEqual(30, DeliveredDuring(sim, At<SinkBuilding>(sim, 4, 3), 600));
        }

        private static Int3 C(int x, int z) => new Int3(x, 0, z);
    }
}
