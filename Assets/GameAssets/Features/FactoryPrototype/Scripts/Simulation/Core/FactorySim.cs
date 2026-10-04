using System;
using System.Collections.Generic;

namespace Friendslop.Features.FactoryPrototype.Simulation
{
    /// <summary>
    /// Deterministic factory simulation. Integer state only, buildings stepped in creation order,
    /// mutated only through SimCommands. Same content + same commands at the same ticks = same state.
    /// </summary>
    public sealed class FactorySim
    {
        private const byte SnapshotVersion = 1;

        // One building per cell at most.
        private const int MaxBuildings = (SimConstants.GridMax - SimConstants.GridMin + 1) * (SimConstants.GridMax - SimConstants.GridMin + 1);

        private readonly List<Building> _buildings = new List<Building>();

        // Lookups only. Never iterated: iteration order is not deterministic.
        private readonly Dictionary<Int3, Building> _cells = new Dictionary<Int3, Building>();
        private readonly Dictionary<int, Building> _byId = new Dictionary<int, Building>();

        private readonly List<Int3> _cellBuffer = new List<Int3>();
        private SimWriter _checksumWriter;
        private int _nextBuildingId = 1;
        private int _nextItemId = 1;

        public FactorySim(ContentDb content)
        {
            Content = content ?? throw new ArgumentNullException(nameof(content));
        }

        public ContentDb Content { get; }

        /// <summary>Number of completed ticks.</summary>
        public int Tick { get; private set; }

        public long Coins { get; private set; }

        /// <summary>Creation order = update order.</summary>
        public IReadOnlyList<Building> Buildings => _buildings;

        public event Action<Building> BuildingAdded;
        public event Action<Building> BuildingRemoved;

        /// <summary>A snapshot replaced the whole state. Views must rebuild.</summary>
        public event Action StateReset;

        /// <summary>A tick completed.</summary>
        public event Action Ticked;

        public static bool IsInBounds(Int3 cell) =>
            cell.Y == 0
            && cell.X >= SimConstants.GridMin && cell.X <= SimConstants.GridMax
            && cell.Z >= SimConstants.GridMin && cell.Z <= SimConstants.GridMax;

        public Building GetBuildingAt(Int3 cell) => _cells.TryGetValue(cell, out Building building) ? building : null;

        public Building GetBuilding(int id) => _byId.TryGetValue(id, out Building building) ? building : null;

        public bool CanPlace(ushort defId, Int3 origin, int rotation)
        {
            BuildingDef def = Content.GetBuilding(defId);
            if (def == null)
                return false;

            Footprint.GetCells(def, origin, rotation, _cellBuffer);
            foreach (Int3 cell in _cellBuffer)
            {
                if (!IsInBounds(cell) || _cells.ContainsKey(cell))
                    return false;
            }
            return true;
        }

        /// <summary>Applies a command immediately. Returns false if it is invalid for the current state.</summary>
        public bool TryApply(in SimCommand command)
        {
            switch (command.Type)
            {
                case CommandType.Place:
                {
                    if (!CanPlace(command.DefId, command.Cell, command.Rotation))
                        return false;

                    Building building = Create(_nextBuildingId++, Content.GetBuilding(command.DefId), command.Cell, command.Rotation);
                    Register(building);
                    BuildingAdded?.Invoke(building);
                    return true;
                }
                case CommandType.Remove:
                {
                    Building building = GetBuildingAt(command.Cell);
                    if (building == null)
                        return false;

                    Unregister(building);
                    BuildingRemoved?.Invoke(building);
                    return true;
                }
                default:
                    return false;
            }
        }

        /// <summary>
        /// Applies the commands in order, then steps every building once.
        /// Applied commands are added to applied (if not null). Returns the applied count.
        /// </summary>
        public int Advance(IReadOnlyList<SimCommand> commands, List<SimCommand> applied)
        {
            int appliedCount = 0;
            if (commands != null)
            {
                for (int i = 0; i < commands.Count; i++)
                {
                    SimCommand command = commands[i];
                    if (!TryApply(command))
                        continue;

                    appliedCount++;
                    applied?.Add(command);
                }
            }

            Tick++;
            for (int i = 0; i < _buildings.Count; i++)
                _buildings[i].Step(Tick);

            Ticked?.Invoke();
            return appliedCount;
        }

        /// <summary>Desync drill only. Changes state outside of commands.</summary>
        public void DebugCorruptState() => Coins++;

        public void WriteSnapshot(SimWriter writer)
        {
            writer.WriteByte(SnapshotVersion);
            writer.WriteInt(Tick);
            writer.WriteLong(Coins);
            writer.WriteInt(_nextBuildingId);
            writer.WriteInt(_nextItemId);
            writer.WriteInt(_buildings.Count);
            for (int i = 0; i < _buildings.Count; i++)
            {
                Building building = _buildings[i];
                writer.WriteInt(building.Id);
                writer.WriteUShort(building.Def.Id);
                writer.WriteInt3(building.Origin);
                writer.WriteByte((byte)building.Rotation);
                building.WriteState(writer);
            }
        }

        /// <summary>Replaces the whole state. On invalid data, throws and keeps the current state.</summary>
        public void ReadSnapshot(SimReader reader)
        {
            byte version = reader.ReadByte();
            if (version != SnapshotVersion)
                throw new FormatException($"Unsupported snapshot version {version}.");

            int tick = reader.ReadInt();
            long coins = reader.ReadLong();
            int nextBuildingId = reader.ReadInt();
            int nextItemId = reader.ReadInt();
            int count = reader.ReadInt();
            if (count < 0 || count > MaxBuildings)
                throw new FormatException($"Invalid building count {count}.");

            var loaded = new List<Building>(count);
            for (int i = 0; i < count; i++)
            {
                int id = reader.ReadInt();
                ushort defId = reader.ReadUShort();
                Int3 origin = reader.ReadInt3();
                byte rotation = reader.ReadByte();
                BuildingDef def = Content.GetBuilding(defId) ?? throw new FormatException($"Unknown building def {defId}.");

                Building building = Create(id, def, origin, rotation);
                building.ReadState(reader);
                loaded.Add(building);
            }

            _buildings.Clear();
            _cells.Clear();
            _byId.Clear();
            foreach (Building building in loaded)
                Register(building);

            Tick = tick;
            Coins = coins;
            _nextBuildingId = nextBuildingId;
            _nextItemId = nextItemId;
            StateReset?.Invoke();
        }

        /// <summary>FNV-1a over the snapshot bytes.</summary>
        public uint ComputeChecksum()
        {
            if (_checksumWriter == null)
                _checksumWriter = new SimWriter(1024);

            _checksumWriter.Clear();
            WriteSnapshot(_checksumWriter);
            return Fnv1a.Hash(_checksumWriter.Buffer, 0, _checksumWriter.Length);
        }

        internal int AllocateItemId() => _nextItemId++;
        internal void AddCoins(int amount) => Coins += amount;

        private Building Create(int id, BuildingDef def, Int3 origin, int rotation)
        {
            switch (def.Kind)
            {
                case BuildingKind.Source: return new SourceBuilding(this, id, def, origin, rotation);
                case BuildingKind.Belt: return new BeltBuilding(this, id, def, origin, rotation);
                case BuildingKind.Crafter: return new CrafterBuilding(this, id, def, origin, rotation);
                case BuildingKind.Sink: return new SinkBuilding(this, id, def, origin, rotation);
                default: throw new FormatException($"Unknown building kind {def.Kind}.");
            }
        }

        private void Register(Building building)
        {
            _buildings.Add(building);
            _byId[building.Id] = building;
            Footprint.GetCells(building.Def, building.Origin, building.Rotation, _cellBuffer);
            foreach (Int3 cell in _cellBuffer)
                _cells[cell] = building;
        }

        private void Unregister(Building building)
        {
            _buildings.Remove(building);
            _byId.Remove(building.Id);
            Footprint.GetCells(building.Def, building.Origin, building.Rotation, _cellBuffer);
            foreach (Int3 cell in _cellBuffer)
                _cells.Remove(cell);
        }
    }
}
