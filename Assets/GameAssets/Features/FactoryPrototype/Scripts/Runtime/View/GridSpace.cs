using Friendslop.Features.FactoryPrototype.Simulation;
using UnityEngine;

namespace Friendslop.Features.FactoryPrototype
{
    /// <summary>
    /// Sim grid to Unity world. Cell (x, y, z) covers [x, x + 1) x [y, y + 1) x [z, z + 1) meters.
    /// </summary>
    public static class GridSpace
    {
        public static Vector3 CellCenter(Int3 cell) => new Vector3(cell.X + 0.5f, cell.Y, cell.Z + 0.5f);

        public static Int3 WorldToCell(Vector3 position) =>
            new Int3(Mathf.FloorToInt(position.x), Mathf.FloorToInt(position.y), Mathf.FloorToInt(position.z));

        public static Quaternion ToRotation(int rotation) => Quaternion.Euler(0f, 90f * (rotation & 3), 0f);

        public static Vector3 ToVector(Dir dir)
        {
            Int3 offset = dir.ToOffset();
            return new Vector3(offset.X, 0f, offset.Z);
        }
    }
}
