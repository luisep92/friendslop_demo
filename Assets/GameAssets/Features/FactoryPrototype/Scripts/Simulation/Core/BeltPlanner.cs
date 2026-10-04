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
        /// Finds an adjacent building that would take items leaving cell. flow = direction from cell into it.
        /// Priority: whatever accepts straight ahead (preferred = travel direction), including belts that
        /// side-load; then the only adjacent machine input. Belts on the sides never snap on their own, so
        /// parallel lines do not merge by accident.
        /// </summary>
        public static bool TryFindConsumer(FactorySim sim, Int3 cell, Dir? preferred, out Dir flow)
        {
            int machineInputs = 0;
            Dir machineFlow = default;
            for (int d = 0; d < 4; d++)
            {
                var toNeighbor = (Dir)d;
                Building building = sim.GetBuildingAt(cell + toNeighbor.ToOffset());
                if (building == null || !AcceptsFrom(building, cell, toNeighbor))
                    continue;

                if (preferred == toNeighbor)
                {
                    flow = toNeighbor;
                    return true;
                }

                if (!(building is BeltBuilding))
                {
                    machineInputs++;
                    machineFlow = toNeighbor;
                }
            }

            flow = machineFlow;
            return machineInputs == 1;
        }

        /// <summary>
        /// Plans a run and orients its last tile: endOverride if set (player choice), else the consumer found
        /// with TryFindConsumer using the travel direction, else the travel direction.
        /// </summary>
        public static void PlanRun(FactorySim sim, Int3 start, Int3 end, Dir? startFlow, Dir? endOverride, bool flipCorner,
            int defaultRotation, List<BeltTile> result)
        {
            Plan(start, end, startFlow, endOverride, flipCorner, defaultRotation, result);
            if (endOverride.HasValue || result.Count == 0)
                return;

            var travel = (Dir)result[result.Count - 1].Rotation;
            if (TryFindConsumer(sim, end, travel, out Dir consumer) && consumer != travel)
                Plan(start, end, startFlow, consumer, flipCorner, defaultRotation, result);
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

        /// <summary>True if building takes items that leave cell moving towards toNeighbor.</summary>
        private static bool AcceptsFrom(Building building, Int3 cell, Dir toNeighbor)
        {
            if (building is BeltBuilding belt)
                return belt.AcceptsFrom(toNeighbor.Opposite());

            foreach (WorldPort port in building.Ports)
            {
                if (port.Type == PortType.Input && port.NeighborCell == cell)
                    return true;
            }
            return false;
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
