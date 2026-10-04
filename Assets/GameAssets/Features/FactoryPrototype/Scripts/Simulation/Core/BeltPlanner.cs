using System;
using System.Collections.Generic;

namespace Friendslop.Features.FactoryPrototype.Simulation
{
    public struct BeltTile
    {
        public Int3 Cell;
        public int Rotation;
    }

    /// <summary>
    /// Read-only helpers for the belt tool: L-shaped paths and snapping to ports.
    /// Client-side planning only; the planned tiles are sent as regular Place commands.
    /// "Flow" = direction items move.
    /// </summary>
    public static class BeltPlanner
    {
        /// <summary>
        /// Finds an adjacent output pushing into cell: a machine output port or a belt front.
        /// flow = direction items leave that output.
        /// </summary>
        public static bool TryFindFeeder(FactorySim sim, Int3 cell, out Dir flow)
        {
            for (int d = 0; d < 4; d++)
            {
                var toNeighbor = (Dir)d;
                Building building = sim.GetBuildingAt(cell + toNeighbor.ToOffset());
                if (building == null)
                    continue;

                if (building is BeltBuilding belt)
                {
                    if (belt.Forward == toNeighbor.Opposite())
                    {
                        flow = belt.Forward;
                        return true;
                    }
                    continue;
                }

                foreach (WorldPort port in building.Ports)
                {
                    if (port.Type == PortType.Output && port.NeighborCell == cell)
                    {
                        flow = port.Side;
                        return true;
                    }
                }
            }

            flow = default;
            return false;
        }

        /// <summary>
        /// Finds an adjacent input that would take items from cell: a machine input port first, then a belt
        /// continuing straight (entering through its back). flow = direction from cell into it.
        /// </summary>
        public static bool TryFindConsumer(FactorySim sim, Int3 cell, out Dir flow)
        {
            for (int d = 0; d < 4; d++)
            {
                var toNeighbor = (Dir)d;
                Building building = sim.GetBuildingAt(cell + toNeighbor.ToOffset());
                if (building == null || building is BeltBuilding)
                    continue;

                foreach (WorldPort port in building.Ports)
                {
                    if (port.Type == PortType.Input && port.NeighborCell == cell)
                    {
                        flow = toNeighbor;
                        return true;
                    }
                }
            }

            for (int d = 0; d < 4; d++)
            {
                var toNeighbor = (Dir)d;
                if (sim.GetBuildingAt(cell + toNeighbor.ToOffset()) is BeltBuilding belt && belt.Forward == toNeighbor)
                {
                    flow = toNeighbor;
                    return true;
                }
            }

            flow = default;
            return false;
        }

        /// <summary>
        /// L-shaped path from start to end, both inclusive, at start's level. Each tile faces the next one.
        /// startFlow: items arrive moving this way; the first leg follows its axis.
        /// endFlow: the last tile faces this way (into a consumer). Otherwise it keeps the travel direction.
        /// flipCorner swaps the leg order. defaultRotation is used for a single tile without flows.
        /// </summary>
        public static void Plan(Int3 start, Int3 end, Dir? startFlow, Dir? endFlow, bool flipCorner, int defaultRotation, List<BeltTile> result)
        {
            result.Clear();
            int dx = end.X - start.X;
            int dz = end.Z - start.Z;

            bool xFirst;
            if (startFlow.HasValue)
                xFirst = IsXAxis(startFlow.Value);
            else if (endFlow.HasValue)
                xFirst = !IsXAxis(endFlow.Value);
            else
                xFirst = Math.Abs(dx) >= Math.Abs(dz);
            if (flipCorner)
                xFirst = !xFirst;

            var cells = new List<Int3> { start };
            Int3 current = start;
            if (xFirst)
            {
                current = Walk(cells, current, dx, 0);
                Walk(cells, current, 0, dz);
            }
            else
            {
                current = Walk(cells, current, 0, dz);
                Walk(cells, current, dx, 0);
            }

            for (int i = 0; i < cells.Count; i++)
            {
                int rotation;
                if (i < cells.Count - 1)
                    rotation = (int)DirBetween(cells[i], cells[i + 1]);
                else if (endFlow.HasValue)
                    rotation = (int)endFlow.Value;
                else if (cells.Count > 1)
                    rotation = (int)DirBetween(cells[i - 1], cells[i]);
                else
                    rotation = startFlow.HasValue ? (int)startFlow.Value : defaultRotation & 3;

                result.Add(new BeltTile { Cell = cells[i], Rotation = rotation });
            }
        }

        private static Int3 Walk(List<Int3> cells, Int3 from, int dx, int dz)
        {
            int steps = Math.Abs(dx) + Math.Abs(dz);
            var step = new Int3(Math.Sign(dx), 0, Math.Sign(dz));
            Int3 current = from;
            for (int i = 0; i < steps; i++)
            {
                current += step;
                cells.Add(current);
            }
            return current;
        }

        private static bool IsXAxis(Dir dir) => dir == Dir.East || dir == Dir.West;

        private static Dir DirBetween(Int3 from, Int3 to)
        {
            if (to.X > from.X)
                return Dir.East;
            if (to.X < from.X)
                return Dir.West;
            return to.Z > from.Z ? Dir.North : Dir.South;
        }
    }
}
