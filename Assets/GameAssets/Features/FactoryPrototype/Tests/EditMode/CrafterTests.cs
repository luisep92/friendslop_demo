using Friendslop.Features.FactoryPrototype.Simulation;
using NUnit.Framework;
using static Friendslop.Features.FactoryPrototype.Tests.SimTestHelpers;

namespace Friendslop.Features.FactoryPrototype.Tests
{
    public class CrafterTests
    {
        [Test]
        public void Grinder_SingleInput_RunsAtRecipeRate()
        {
            FactorySim sim = NewSim();
            Place(sim, PrototypeContent.HerbSource, 0, 0);
            Place(sim, PrototypeContent.Belt, 0, 1);
            Place(sim, PrototypeContent.Grinder, 0, 2);
            Place(sim, PrototypeContent.Belt, 0, 3);
            Place(sim, PrototypeContent.SellPoint, 0, 4);
            var sink = At<SinkBuilding>(sim, 0, 4);

            Run(sim, 400);
            long coinsBefore = sim.Coins;

            // 30 ticks per Powder: 20 in 600 ticks, worth 4 each.
            Assert.AreEqual(20, DeliveredDuring(sim, sink, 600));
            Assert.AreEqual(20 * 4, sim.Coins - coinsBefore);
        }

        [Test]
        public void Mixer_TwoInputs_DemoLine_ProducesPotions()
        {
            FactorySim sim = NewSim();
            Apply(sim, DemoLayouts.MinimalLine(Int3.Zero, 0));
            var sink = At<SinkBuilding>(sim, 0, 10);

            Run(sim, 1000);
            long coinsBefore = sim.Coins;

            // 60 ticks per Potion: 10 in 600 ticks, worth 15 each. Only Potions reach the sink.
            Assert.AreEqual(10, DeliveredDuring(sim, sink, 600));
            Assert.AreEqual(10 * 15, sim.Coins - coinsBefore);
        }

        [Test]
        public void Mixer_MissingInput_StallsWithCappedBuffer()
        {
            FactorySim sim = NewSim();
            Place(sim, PrototypeContent.HerbSource, 0, 0);
            Place(sim, PrototypeContent.Belt, 0, 1);
            Place(sim, PrototypeContent.Grinder, 0, 2);
            Place(sim, PrototypeContent.Belt, 0, 3);
            Place(sim, PrototypeContent.Mixer, 0, 4);
            Place(sim, PrototypeContent.SellPoint, 0, 5);

            Run(sim, 1000);

            var mixer = At<CrafterBuilding>(sim, 0, 4);
            Assert.AreEqual(CrafterState.Idle, mixer.State);
            Assert.AreEqual(SimConstants.MachineBufferCapacity, mixer.GetInputCount(mixer.Recipe.IndexOfInput(PrototypeContent.Powder)));
            Assert.AreEqual(0, mixer.GetInputCount(mixer.Recipe.IndexOfInput(PrototypeContent.Crystal)));
            Assert.AreEqual(0, At<SinkBuilding>(sim, 0, 5).Delivered);
        }

        [Test]
        public void Grinder_OutputBlocked_WhenNothingTakesOutput()
        {
            FactorySim sim = NewSim();
            Place(sim, PrototypeContent.HerbSource, 0, 0);
            Place(sim, PrototypeContent.Belt, 0, 1);
            Place(sim, PrototypeContent.Grinder, 0, 2);

            Run(sim, 1000);

            var grinder = At<CrafterBuilding>(sim, 0, 2);
            Assert.AreEqual(CrafterState.OutputBlocked, grinder.State);
            Assert.AreEqual(SimConstants.MachineBufferCapacity, grinder.GetOutputCount(0));
            Assert.AreEqual(SimConstants.MachineBufferCapacity, grinder.GetInputCount(0));
        }

        [Test]
        public void Crafter_RejectsItemsThroughSidesWithoutPort()
        {
            FactorySim sim = NewSim();
            Place(sim, PrototypeContent.HerbSource, 1, 1, 3); // Faces west, into the grinder's east side.
            Place(sim, PrototypeContent.Grinder, 0, 1);

            Run(sim, 100);

            Assert.AreEqual(0, At<CrafterBuilding>(sim, 0, 1).GetInputCount(0));
            Assert.IsTrue(At<SourceBuilding>(sim, 1, 1).IsBlocked);
        }

        [Test]
        public void Layout_RotationInvariance()
        {
            long expected = -1;
            for (int rotation = 0; rotation < 4; rotation++)
            {
                FactorySim sim = NewSim();
                Apply(sim, DemoLayouts.MinimalLine(Int3.Zero, rotation));
                Run(sim, 2000);

                Assert.Greater(sim.Coins, 0, $"Rotation {rotation} produced nothing.");
                if (expected < 0)
                    expected = sim.Coins;
                Assert.AreEqual(expected, sim.Coins, $"Rotation {rotation} differs.");
            }
        }

        [Test]
        public void RemoveMachineMidCraft_ThenReplace_Resumes()
        {
            FactorySim sim = NewSim();
            Apply(sim, DemoLayouts.MinimalLine(Int3.Zero, 0));
            Run(sim, 500);

            Assert.IsTrue(sim.TryApply(SimCommand.Remove(new Int3(0, 0, 4))));
            Run(sim, 100);
            Place(sim, PrototypeContent.Grinder, 0, 4);
            Run(sim, 1000);

            Assert.AreEqual(10, DeliveredDuring(sim, At<SinkBuilding>(sim, 0, 10), 600));
        }
    }
}
