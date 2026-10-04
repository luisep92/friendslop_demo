using System.Collections.Generic;
using Friendslop.Features.FactoryPrototype.Simulation;
using UnityEngine;

namespace Friendslop.Features.FactoryPrototype
{
    /// <summary>
    /// Presents whatever sim FactoryNetwork exposes on this peer. Not networked: keeps working
    /// before connecting and after disconnecting. Buildings are greybox GameObjects keyed by id.
    /// Belt items are drawn with GPU instancing, interpolated between the last two ticks.
    /// </summary>
    public sealed class FactoryView : MonoBehaviour
    {
        private const int MaxInstancesPerCall = 1023;
        private const float ItemSize = 0.22f;
        private const float ItemHeight = 0.1f + ItemSize * 0.5f;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        private readonly Dictionary<int, GameObject> _buildingViews = new Dictionary<int, GameObject>();
        private readonly Dictionary<ushort, ItemBatch> _batches = new Dictionary<ushort, ItemBatch>();
        private Dictionary<int, Vector3> _previous = new Dictionary<int, Vector3>();
        private Dictionary<int, Vector3> _current = new Dictionary<int, Vector3>();
        private readonly List<(int Id, ushort Type)> _currentItems = new List<(int, ushort)>();

        private FactorySim _sim;
        private Mesh _itemMesh;
        private Material _itemBaseMaterial;

        private void Awake()
        {
            GameObject probe = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _itemMesh = probe.GetComponent<MeshFilter>().sharedMesh;
            _itemBaseMaterial = probe.GetComponent<MeshRenderer>().sharedMaterial;
            Destroy(probe);
        }

        private void OnDestroy()
        {
            Bind(null);
            foreach (ItemBatch batch in _batches.Values)
                Destroy(batch.Material);
        }

        private void LateUpdate()
        {
            FactoryNetwork network = FactoryNetwork.Instance;
            FactorySim sim = network != null ? network.ActiveSim : null;
            if (sim != _sim)
                Bind(sim);

            if (_sim != null)
                DrawItems(network.InterpolationAlpha);
        }

        private void Bind(FactorySim sim)
        {
            if (_sim != null)
            {
                _sim.BuildingAdded -= OnBuildingAdded;
                _sim.BuildingRemoved -= OnBuildingRemoved;
                _sim.StateReset -= Rebuild;
                _sim.Ticked -= CaptureItems;
            }

            _sim = sim;
            Rebuild();

            if (_sim != null)
            {
                _sim.BuildingAdded += OnBuildingAdded;
                _sim.BuildingRemoved += OnBuildingRemoved;
                _sim.StateReset += Rebuild;
                _sim.Ticked += CaptureItems;
            }
        }

        private void Rebuild()
        {
            foreach (GameObject view in _buildingViews.Values)
                Destroy(view);
            _buildingViews.Clear();
            _previous.Clear();
            _current.Clear();
            _currentItems.Clear();

            if (_sim == null)
                return;

            foreach (Building building in _sim.Buildings)
                OnBuildingAdded(building);
            CaptureItems();
            CaptureItems();
        }

        private void OnBuildingAdded(Building building)
        {
            _buildingViews[building.Id] = BuildingVisuals.Create(building.Def, building.Origin, building.Rotation, transform, false);
        }

        private void OnBuildingRemoved(Building building)
        {
            if (_buildingViews.TryGetValue(building.Id, out GameObject view))
            {
                Destroy(view);
                _buildingViews.Remove(building.Id);
            }
        }

        /// <summary>Runs after every tick: current positions become previous, then recompute current.</summary>
        private void CaptureItems()
        {
            (_previous, _current) = (_current, _previous);
            _current.Clear();
            _currentItems.Clear();

            foreach (Building building in _sim.Buildings)
            {
                if (!(building is BeltBuilding belt))
                    continue;

                foreach (BeltItem item in belt.Items)
                {
                    _current[item.Id] = ItemPosition(belt, item);
                    _currentItems.Add((item.Id, item.Type));
                }
            }
        }

        /// <summary>Path: entry edge -> cell center -> exit edge. Corners and side loading need no extra sim state.</summary>
        private static Vector3 ItemPosition(BeltBuilding belt, in BeltItem item)
        {
            Vector3 center = GridSpace.CellCenter(belt.Origin) + Vector3.up * ItemHeight;
            float t = (float)item.Progress / SimConstants.TileLength;
            if (t < 0.5f)
                return Vector3.Lerp(center + GridSpace.ToVector(item.EntrySide) * 0.5f, center, t * 2f);
            return Vector3.Lerp(center, center + GridSpace.ToVector(belt.Forward) * 0.5f, (t - 0.5f) * 2f);
        }

        private void DrawItems(float alpha)
        {
            foreach (ItemBatch batch in _batches.Values)
                batch.Count = 0;

            var scale = new Vector3(ItemSize, ItemSize, ItemSize);
            foreach ((int id, ushort type) in _currentItems)
            {
                Vector3 current = _current[id];
                Vector3 position = _previous.TryGetValue(id, out Vector3 previous) ? Vector3.Lerp(previous, current, alpha) : current;
                GetBatch(type).Add(Matrix4x4.TRS(position, Quaternion.identity, scale));
            }

            foreach (ItemBatch batch in _batches.Values)
            {
                for (int start = 0; start < batch.Count; start += MaxInstancesPerCall)
                {
                    int count = Mathf.Min(MaxInstancesPerCall, batch.Count - start);
                    Graphics.RenderMeshInstanced(batch.RenderParams, _itemMesh, 0, batch.Matrices, count, start);
                }
            }
        }

        private ItemBatch GetBatch(ushort type)
        {
            if (_batches.TryGetValue(type, out ItemBatch batch))
                return batch;

            var material = new Material(_itemBaseMaterial) { enableInstancing = true };
            material.SetColor(BaseColorId, FactoryPalette.Item(type));
            batch = new ItemBatch(material);
            _batches.Add(type, batch);
            return batch;
        }

        private sealed class ItemBatch
        {
            public ItemBatch(Material material)
            {
                Material = material;
                RenderParams = new RenderParams(material);
            }

            public Material Material { get; }
            public RenderParams RenderParams { get; }
            public Matrix4x4[] Matrices { get; private set; } = new Matrix4x4[64];
            public int Count { get; set; }

            public void Add(Matrix4x4 matrix)
            {
                if (Count == Matrices.Length)
                {
                    Matrix4x4[] grown = new Matrix4x4[Matrices.Length * 2];
                    System.Array.Copy(Matrices, grown, Count);
                    Matrices = grown;
                }
                Matrices[Count++] = matrix;
            }
        }
    }
}
