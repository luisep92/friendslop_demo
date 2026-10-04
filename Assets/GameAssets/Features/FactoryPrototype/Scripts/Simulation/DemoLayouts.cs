using System.Collections.Generic;
using static Friendslop.Features.FactoryPrototype.Simulation.PrototypeContent;

namespace Friendslop.Features.FactoryPrototype.Simulation
{
    public static class DemoLayouts
    {
        /// <summary>
        /// Herb source -> belts -> Grinder -> belts -> Mixer -> belts -> Sell point, plus a Crystal source
        /// feeding the Mixer through a side-loading corner. Rotated around origin by rotation.
        /// Mixer-limited: 1 Potion every 60 ticks once saturated.
        /// </summary>
        public static List<SimCommand> MinimalLine(Int3 origin, int rotation)
        {
            var commands = new List<SimCommand>();

            void Add(ushort defId, int x, int z, int localRotation) =>
                commands.Add(SimCommand.Place(defId, Footprint.ToWorld(origin, new Int3(x, 0, z), rotation), localRotation + rotation));

            Add(HerbSource, 0, 0, 0);
            Add(Belt, 0, 1, 0);
            Add(Belt, 0, 2, 0);
            Add(Belt, 0, 3, 0);
            Add(Grinder, 0, 4, 0);
            Add(Belt, 0, 5, 0);
            Add(Belt, 0, 6, 0);

            Add(CrystalSource, 3, 5, 3);
            Add(Belt, 2, 5, 3);
            Add(Belt, 1, 5, 0); // Corner: enters from the east side, exits north.
            Add(Belt, 1, 6, 0);

            Add(Mixer, 0, 7, 0);
            Add(Belt, 0, 8, 0);
            Add(Belt, 0, 9, 0);
            Add(SellPoint, 0, 10, 0);

            return commands;
        }
    }
}
