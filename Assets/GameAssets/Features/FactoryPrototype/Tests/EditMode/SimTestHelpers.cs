using System.Collections.Generic;
using Friendslop.Features.FactoryPrototype.Simulation;
using NUnit.Framework;

namespace Friendslop.Features.FactoryPrototype.Tests
{
    internal static class SimTestHelpers
    {
        public static FactorySim NewSim() => new FactorySim(PrototypeContent.Create());

        public static void Place(FactorySim sim, ushort defId, int x, int z, int rotation = 0)
        {
            Assert.IsTrue(sim.TryApply(SimCommand.Place(defId, new Int3(x, 0, z), rotation)),
                $"Place def {defId} at ({x}, {z}) rot {rotation} failed.");
        }

        public static void Apply(FactorySim sim, IEnumerable<SimCommand> commands)
        {
            foreach (SimCommand command in commands)
                Assert.IsTrue(sim.TryApply(command), $"{command} failed.");
        }

        public static void Run(FactorySim sim, int ticks)
        {
            for (int i = 0; i < ticks; i++)
                sim.Advance(null, null);
        }

        public static T At<T>(FactorySim sim, int x, int z) where T : Building =>
            (T)sim.GetBuildingAt(new Int3(x, 0, z));

        /// <summary>Items sold by sink during the next ticks.</summary>
        public static int DeliveredDuring(FactorySim sim, SinkBuilding sink, int ticks)
        {
            int before = sink.Delivered;
            Run(sim, ticks);
            return sink.Delivered - before;
        }

        public static int CountBeltItems(FactorySim sim)
        {
            int count = 0;
            foreach (Building building in sim.Buildings)
            {
                if (building is BeltBuilding belt)
                    count += belt.Items.Count;
            }
            return count;
        }
    }
}
