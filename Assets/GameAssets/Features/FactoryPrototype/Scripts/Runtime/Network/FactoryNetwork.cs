using System;
using System.Collections.Generic;
using System.IO;
using FishNet.Connection;
using FishNet.Object;
using Friendslop.Features.FactoryPrototype.Simulation;
using UnityEngine;

namespace Friendslop.Features.FactoryPrototype
{
    /// <summary>
    /// Scene network object that runs the factory lockstep over FishNet. Only forwards bytes:
    /// all lockstep logic lives in LockstepServer / LockstepClient.
    /// - Server: steps the canonical sim at a fixed rate and broadcasts each tick batch.
    /// - Client-only peer: replica sim, fed by a snapshot on spawn and then by tick batches.
    /// - Host: uses the server sim directly (one sim per process).
    /// </summary>
    public sealed class FactoryNetwork : NetworkBehaviour
    {
        private const float SnapshotCooldownSeconds = 1f;

        /// <summary>Demo layout origin. Player spawn points are south of it.</summary>
        private static readonly Int3 DemoOrigin = new Int3(4, 0, -4);

        private readonly Dictionary<int, float> _lastSnapshotSent = new Dictionary<int, float>();
        private LockstepServer _server;
        private LockstepClient _client;
        private FixedStepClock _serverClock;

        public static FactoryNetwork Instance { get; private set; }

        public static string SavePath => Path.Combine(Application.persistentDataPath, "factory_prototype.sav");

        /// <summary>Last save/load result, for the overlay.</summary>
        public string PersistenceMessage { get; private set; }
        public float PersistenceMessageTime { get; private set; }

        /// <summary>Sim to present on this peer. Null until the server started or the first snapshot arrived.</summary>
        public FactorySim ActiveSim
        {
            get
            {
                if (_server != null)
                    return _server.Sim;
                return _client != null && _client.HasState ? _client.Sim : null;
            }
        }

        public bool IsAuthority => _server != null;

        /// <summary>Null on the server and host.</summary>
        public LockstepClient Replica => _client;

        /// <summary>Elapsed fraction of the current tick, for interpolation.</summary>
        public float InterpolationAlpha => _server != null ? _serverClock.Alpha : _client?.Alpha ?? 0f;

        public override void OnStartServer()
        {
            base.OnStartServer();
            Instance = this;
            _server = new LockstepServer(new FactorySim(PrototypeContent.Create()));
            _serverClock = new FixedStepClock(SimConstants.TicksPerSecond, 4);
        }

        public override void OnStopServer()
        {
            base.OnStopServer();
            _server = null;
            _lastSnapshotSent.Clear();
            ClearInstance();
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            Instance = this;
            if (IsServerInitialized)
                return;

            _client = new LockstepClient(new FactorySim(PrototypeContent.Create()));
        }

        public override void OnStopClient()
        {
            base.OnStopClient();
            _client = null;
            ClearInstance();
        }

        public override void OnSpawnServer(NetworkConnection connection)
        {
            base.OnSpawnServer(connection);
            // The host's own client reads the server sim.
            if (connection.IsLocalClient)
                return;

            SendSnapshot(connection);
        }

        public void RequestPlace(ushort defId, Int3 origin, int rotation)
        {
            if (_server != null)
                _server.Enqueue(SimCommand.Place(defId, origin, rotation));
            else if (_client != null)
                ServerPlace(defId, origin.X, origin.Y, origin.Z, (byte)(rotation & 3));
        }

        public void RequestRemove(Int3 cell)
        {
            if (_server != null)
                _server.Enqueue(SimCommand.Remove(cell));
            else if (_client != null)
                ServerRemove(cell.X, cell.Y, cell.Z);
        }

        /// <summary>Server only. Queues the demo line next to the spawn area.</summary>
        public void RequestDemoLayout()
        {
            if (_server == null)
                return;

            foreach (SimCommand command in DemoLayouts.MinimalLine(DemoOrigin, 0))
                _server.Enqueue(command);
        }

        /// <summary>Server only. Writes the canonical sim to SavePath.</summary>
        public void SaveToDisk()
        {
            if (_server == null)
                return;

            try
            {
                byte[] players = WritePlayers(out int playerCount);
                File.WriteAllBytes(SavePath, SaveFile.Write(_server.Sim, players));
                Report($"Saved tick {_server.Sim.Tick} and {playerCount} players to {SavePath}", false);
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                Report($"Save failed: {exception.Message}", true);
            }
        }

        /// <summary>Server only. Replaces the sim with the save and pushes a fresh snapshot to every client.</summary>
        public void LoadFromDisk()
        {
            if (_server == null)
                return;

            byte[] data;
            try
            {
                data = File.ReadAllBytes(SavePath);
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                Report($"Load failed: {exception.Message}", true);
                return;
            }

            if (!_server.TryLoad(data, out byte[] extra, out string error))
            {
                Report($"Load failed: {error}", true);
                return;
            }

            // Ticks may have gone backwards: replicas cannot continue from their current state.
            foreach (NetworkConnection connection in ServerManager.Clients.Values)
            {
                if (!connection.IsLocalClient)
                    SendSnapshot(connection);
            }

            int moved = RestorePlayers(extra);
            Report($"Loaded tick {_server.Sim.Tick}, moved {moved} players", false);
        }

        /// <summary>
        /// Player poses, keyed by FishNet ClientId. Not stable across sessions (host 0, then join order):
        /// good enough for the prototype; key by Steam ID later.
        /// </summary>
        private byte[] WritePlayers(out int count)
        {
            var players = new List<(int Key, Transform Transform)>();
            foreach (NetworkConnection connection in ServerManager.Clients.Values)
            {
                FirstPersonController player = GetPlayer(connection);
                if (player != null)
                    players.Add((connection.ClientId, player.transform));
            }

            var writer = new SimWriter();
            writer.WriteInt(players.Count);
            foreach ((int key, Transform player) in players)
            {
                Vector3 position = player.position;
                writer.WriteInt(key);
                writer.WriteInt(BitConverter.SingleToInt32Bits(position.x));
                writer.WriteInt(BitConverter.SingleToInt32Bits(position.y));
                writer.WriteInt(BitConverter.SingleToInt32Bits(position.z));
                writer.WriteInt(BitConverter.SingleToInt32Bits(player.eulerAngles.y));
            }

            count = players.Count;
            return writer.ToArray();
        }

        /// <summary>Teleports connected players found in the save. Their owners move them (client-authoritative).</summary>
        private int RestorePlayers(byte[] extra)
        {
            if (extra == null || extra.Length == 0)
                return 0;

            var poses = new Dictionary<int, (Vector3 Position, float Yaw)>();
            try
            {
                var reader = new SimReader(extra);
                int count = reader.ReadInt();
                for (int i = 0; i < count; i++)
                {
                    int key = reader.ReadInt();
                    var position = new Vector3(ReadFloat(reader), ReadFloat(reader), ReadFloat(reader));
                    poses[key] = (position, ReadFloat(reader));
                }
            }
            catch (FormatException exception)
            {
                Debug.LogWarning($"[FactoryNetwork] Ignoring invalid player data in save: {exception.Message}");
                return 0;
            }

            int moved = 0;
            foreach (NetworkConnection connection in ServerManager.Clients.Values)
            {
                FirstPersonController player = GetPlayer(connection);
                if (player == null || !poses.TryGetValue(connection.ClientId, out (Vector3 Position, float Yaw) pose))
                    continue;

                player.TargetTeleport(connection, pose.Position, pose.Yaw);
                moved++;
            }
            return moved;
        }

        private static float ReadFloat(SimReader reader) => BitConverter.Int32BitsToSingle(reader.ReadInt());

        private static FirstPersonController GetPlayer(NetworkConnection connection)
        {
            NetworkObject first = connection.FirstObject;
            return first != null ? first.GetComponent<FirstPersonController>() : null;
        }

        /// <summary>Client only. Breaks the replica to exercise mismatch detection and resync.</summary>
        public void DebugCorruptReplica()
        {
            if (_client != null && _client.HasState)
                _client.Sim.DebugCorruptState();
        }

        private void Update()
        {
            if (_server != null)
            {
                int steps = _serverClock.Accumulate(Time.unscaledDeltaTime);
                for (int i = 0; i < steps; i++)
                    ObserversTickBatch(_server.Step());
            }
            else if (_client != null)
            {
                _client.Update(Time.unscaledDeltaTime);
                if (_client.ConsumeResyncRequest())
                {
                    Debug.LogWarning($"[FactoryNetwork] Replica out of sync at tick {_client.Sim.Tick}. Requesting snapshot.");
                    ServerRequestSnapshot();
                }
            }
        }

        private void SendSnapshot(NetworkConnection connection)
        {
            _lastSnapshotSent[connection.ClientId] = Time.unscaledTime;
            byte[] snapshot = _server.CreateSnapshot();
            TargetSnapshot(connection, new ArraySegment<byte>(snapshot));
        }

        private void Report(string message, bool isError)
        {
            PersistenceMessage = message;
            PersistenceMessageTime = Time.unscaledTime;
            if (isError)
                Debug.LogError($"[FactoryNetwork] {message}");
            else
                Debug.Log($"[FactoryNetwork] {message}");
        }

        private void ClearInstance()
        {
            if (Instance == this && _server == null && _client == null)
                Instance = null;
        }

        [ServerRpc(RequireOwnership = false)]
        private void ServerPlace(ushort defId, int x, int y, int z, byte rotation, NetworkConnection caller = null)
        {
            _server?.Enqueue(SimCommand.Place(defId, new Int3(x, y, z), rotation));
        }

        [ServerRpc(RequireOwnership = false)]
        private void ServerRemove(int x, int y, int z, NetworkConnection caller = null)
        {
            _server?.Enqueue(SimCommand.Remove(new Int3(x, y, z)));
        }

        [ServerRpc(RequireOwnership = false)]
        private void ServerRequestSnapshot(NetworkConnection caller = null)
        {
            if (_server == null || caller == null)
                return;

            if (_lastSnapshotSent.TryGetValue(caller.ClientId, out float lastSent)
                && Time.unscaledTime - lastSent < SnapshotCooldownSeconds)
                return;

            Debug.Log($"[FactoryNetwork] Resync snapshot for client {caller.ClientId} at tick {_server.Sim.Tick}.");
            SendSnapshot(caller);
        }

        // Parsed synchronously, so FishNet's pooled receive buffer is never kept.
        [ObserversRpc(ExcludeServer = true)]
        private void ObserversTickBatch(ArraySegment<byte> batch)
        {
            _client?.OnBatch(batch.Array, batch.Offset, batch.Count);
        }

        [TargetRpc(ExcludeServer = true)]
        private void TargetSnapshot(NetworkConnection connection, ArraySegment<byte> snapshot)
        {
            if (_client == null)
                return;

            if (_client.OnSnapshot(snapshot.Array, snapshot.Offset, snapshot.Count))
                Debug.Log($"[FactoryNetwork] Snapshot loaded at tick {_client.Sim.Tick}.");
            else
                Debug.LogError("[FactoryNetwork] Invalid snapshot received.");
        }
    }
}
