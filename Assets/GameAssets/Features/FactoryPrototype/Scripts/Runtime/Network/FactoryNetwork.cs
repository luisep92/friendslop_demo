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
                File.WriteAllBytes(SavePath, SaveFile.Write(_server.Sim));
                Report($"Saved tick {_server.Sim.Tick} to {SavePath}", false);
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

            if (!_server.TryLoad(data, out string error))
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
            Report($"Loaded tick {_server.Sim.Tick}", false);
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
