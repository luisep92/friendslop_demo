using System.Collections.Generic;
using Friendslop.Features.FactoryPrototype.Simulation;
using NUnit.Framework;

namespace Friendslop.Features.FactoryPrototype.Tests
{
    public class FootprintTests
    {
        [Test]
        public void Dir_RotateOppositeAndOffset()
        {
            Assert.AreEqual(Dir.East, Dir.North.Rotate(1));
            Assert.AreEqual(Dir.West, Dir.North.Rotate(-1));
            Assert.AreEqual(Dir.North, Dir.West.Rotate(1));
            Assert.AreEqual(Dir.South, Dir.North.Opposite());
            Assert.AreEqual(new Int3(1, 0, 0), Dir.East.ToOffset());
            Assert.AreEqual(new Int3(0, 0, -1), Dir.South.ToOffset());
        }

        [Test]
        public void RotateLocal_ClockwiseAndFourStepsIsIdentity()
        {
            var local = new Int3(2, 0, 1);
            Assert.AreEqual(new Int3(1, 0, -2), Footprint.RotateLocal(local, 1));
            Assert.AreEqual(local, Footprint.RotateLocal(local, 4));
            Assert.AreEqual(Dir.East, Footprint.Forward(1));
        }

        [TestCase(0, 1, 0, Dir.North, Dir.South)]
        [TestCase(1, 0, -1, Dir.East, Dir.West)]
        [TestCase(2, -1, 0, Dir.South, Dir.North)]
        [TestCase(3, 0, 1, Dir.West, Dir.East)]
        public void Mixer_RotatesCellsAndPorts(int rotation, int secondX, int secondZ, Dir output, Dir input)
        {
            BuildingDef mixer = PrototypeContent.Create().GetBuilding(PrototypeContent.Mixer);
            var origin = new Int3(5, 0, 5);
            Int3 second = origin + new Int3(secondX, 0, secondZ);

            var cells = new List<Int3>();
            Footprint.GetCells(mixer, origin, rotation, cells);
            CollectionAssert.AreEquivalent(new[] { origin, second }, cells);

            WorldPort in0 = Footprint.GetPort(mixer, 0, origin, rotation);
            WorldPort in1 = Footprint.GetPort(mixer, 1, origin, rotation);
            WorldPort out0 = Footprint.GetPort(mixer, 2, origin, rotation);
            Assert.AreEqual(origin, in0.Cell);
            Assert.AreEqual(input, in0.Side);
            Assert.AreEqual(second, in1.Cell);
            Assert.AreEqual(input, in1.Side);
            Assert.AreEqual(origin, out0.Cell);
            Assert.AreEqual(output, out0.Side);
            Assert.AreEqual(PortType.Output, out0.Type);
        }

        [Test]
        public void Content_IdsResolveAndBuildOrderIsSlotOrder()
        {
            ContentDb content = PrototypeContent.Create();
            Assert.AreEqual("Potion", content.GetItem(PrototypeContent.Potion).Name);
            Assert.IsNull(content.GetItem(0));
            Assert.IsNull(content.GetBuilding(99));
            Assert.AreEqual(6, content.Buildable.Count);
            Assert.AreEqual(PrototypeContent.Belt, content.Buildable[0].Id);
            Assert.AreEqual(PrototypeContent.SellPoint, content.Buildable[5].Id);
        }
    }
}
