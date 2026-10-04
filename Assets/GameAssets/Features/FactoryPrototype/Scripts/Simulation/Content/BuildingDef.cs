using System;
using System.Collections.Generic;

namespace Friendslop.Features.FactoryPrototype.Simulation
{
    public enum BuildingKind : byte
    {
        Source,
        Belt,
        Crafter,
        Sink
    }

    public enum PortType : byte
    {
        Input,
        Output
    }

    /// <summary>
    /// Port of a rotation-0 building, in local space. Side is the face of Cell that items cross.
    /// </summary>
    public readonly struct PortDef
    {
        public PortDef(Int3 cell, Dir side, PortType type)
        {
            Cell = cell;
            Side = side;
            Type = type;
        }

        public Int3 Cell { get; }
        public Dir Side { get; }
        public PortType Type { get; }

        public static PortDef In(int x, int z, Dir side) => new PortDef(new Int3(x, 0, z), side, PortType.Input);
        public static PortDef Out(int x, int z, Dir side) => new PortDef(new Int3(x, 0, z), side, PortType.Output);
    }

    /// <summary>
    /// Static description of a building type. Footprint spans SizeX x SizeZ cells from local (0, 0).
    /// Belts have no ports: they accept from back and sides and output forward (see BeltBuilding).
    /// </summary>
    public sealed class BuildingDef
    {
        private readonly PortDef[] _ports;

        private BuildingDef(ushort id, string name, BuildingKind kind, int sizeX, int sizeZ, PortDef[] ports,
            RecipeDef recipe, ushort sourceItem, int sourceIntervalTicks)
        {
            Id = id;
            Name = name;
            Kind = kind;
            SizeX = sizeX;
            SizeZ = sizeZ;
            _ports = ports;
            Recipe = recipe;
            SourceItem = sourceItem;
            SourceIntervalTicks = sourceIntervalTicks;
        }

        public ushort Id { get; }
        public string Name { get; }
        public BuildingKind Kind { get; }
        public int SizeX { get; }
        public int SizeZ { get; }
        public IReadOnlyList<PortDef> Ports => _ports;

        /// <summary>Crafter only.</summary>
        public RecipeDef Recipe { get; }

        /// <summary>Source only.</summary>
        public ushort SourceItem { get; }

        /// <summary>Source only.</summary>
        public int SourceIntervalTicks { get; }

        public static BuildingDef Source(ushort id, string name, ushort item, int intervalTicks) =>
            new BuildingDef(id, name, BuildingKind.Source, 1, 1, new[] { PortDef.Out(0, 0, Dir.North) }, null, item, intervalTicks);

        public static BuildingDef Belt(ushort id, string name) =>
            new BuildingDef(id, name, BuildingKind.Belt, 1, 1, Array.Empty<PortDef>(), null, 0, 0);

        public static BuildingDef Crafter(ushort id, string name, int sizeX, int sizeZ, PortDef[] ports, RecipeDef recipe) =>
            new BuildingDef(id, name, BuildingKind.Crafter, sizeX, sizeZ, ports, recipe, 0, 0);

        public static BuildingDef Sink(ushort id, string name) =>
            new BuildingDef(id, name, BuildingKind.Sink, 1, 1, new[]
            {
                PortDef.In(0, 0, Dir.North),
                PortDef.In(0, 0, Dir.East),
                PortDef.In(0, 0, Dir.South),
                PortDef.In(0, 0, Dir.West)
            }, null, 0, 0);
    }
}
