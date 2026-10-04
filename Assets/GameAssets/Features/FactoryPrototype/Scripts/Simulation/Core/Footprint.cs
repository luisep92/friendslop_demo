using System.Collections.Generic;

namespace Friendslop.Features.FactoryPrototype.Simulation
{
    public readonly struct WorldPort
    {
        public WorldPort(Int3 cell, Dir side, PortType type)
        {
            Cell = cell;
            Side = side;
            Type = type;
        }

        public Int3 Cell { get; }
        public Dir Side { get; }
        public PortType Type { get; }

        /// <summary>Cell on the other side of the port.</summary>
        public Int3 NeighborCell => Cell + Side.ToOffset();
    }

    /// <summary>
    /// Local to world mapping. Shared by the simulation, the placement preview and server validation.
    /// Rotation r = number of 90 degree clockwise steps seen from above. Matches Unity Euler(0, 90 * r, 0).
    /// </summary>
    public static class Footprint
    {
        public static Dir Forward(int rotation) => Dir.North.Rotate(rotation);

        public static Int3 RotateLocal(Int3 local, int rotation)
        {
            int x = local.X;
            int z = local.Z;
            for (int i = 0; i < (rotation & 3); i++)
            {
                int previousX = x;
                x = z;
                z = -previousX;
            }
            return new Int3(x, local.Y, z);
        }

        public static Int3 ToWorld(Int3 origin, Int3 local, int rotation) => origin + RotateLocal(local, rotation);

        public static void GetCells(BuildingDef def, Int3 origin, int rotation, List<Int3> result)
        {
            result.Clear();
            for (int x = 0; x < def.SizeX; x++)
            {
                for (int z = 0; z < def.SizeZ; z++)
                    result.Add(ToWorld(origin, new Int3(x, 0, z), rotation));
            }
        }

        public static WorldPort GetPort(BuildingDef def, int index, Int3 origin, int rotation)
        {
            PortDef port = def.Ports[index];
            return new WorldPort(ToWorld(origin, port.Cell, rotation), port.Side.Rotate(rotation), port.Type);
        }
    }
}
