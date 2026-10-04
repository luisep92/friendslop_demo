using System.Collections.Generic;
using FishNet.Object;
using Friendslop.Features.FactoryPrototype.Simulation;
using UnityEngine;

namespace Friendslop.Features.FactoryPrototype
{
    public enum BuildMode
    {
        Build,
        Dismantle
    }

    /// <summary>
    /// Owner-only build gun. Raycasts from the screen center: the place target is the cell in front of the
    /// hit face, the remove target is the cell behind it.
    /// - Build: LMB places. Belts use two clicks (start, end) with an L-shaped path that snaps to ports;
    ///   R flips the corner, RMB cancels. Other buildings: R rotates.
    /// - Dismantle (F): hold LMB to remove everything aimed at.
    /// - MMB samples type and rotation of the aimed building.
    /// Requests go through FactoryNetwork; the server validates them with the same Footprint rules.
    /// </summary>
    [RequireComponent(typeof(FirstPersonController))]
    public sealed class BuildTool : NetworkBehaviour
    {
        private const float SurfaceEpsilon = 0.01f;
        private const int MaxBeltRun = 64;

        [SerializeField] private float _reach = 12f;

        private readonly List<BeltTile> _beltPlan = new List<BeltTile>();
        private readonly HashSet<int> _dismantleRequested = new HashSet<int>();
        private FactoryInput _input;
        private GhostSet _ghosts;
        private PendingPlacements _pending;
        private ContentDb _content;
        private FirstPersonController _player;
        private bool _hasTarget;
        private Int3 _placeCell;

        private bool _runActive;
        private Int3 _runStart;
        private Dir? _runStartFlow;
        private bool _flipCorner;

        public static BuildTool Local { get; private set; }
        public BuildMode Mode { get; private set; }
        public int SelectedSlot { get; private set; }
        public BuildingDef SelectedDef => _content.Buildable[SelectedSlot];
        public int Rotation { get; private set; }

        /// <summary>Building under the crosshair, if any.</summary>
        public Building TargetBuilding { get; private set; }

        /// <summary>Tiles in the planned belt run. 0 when no run is active.</summary>
        public int BeltRunLength => _runActive ? _beltPlan.Count : 0;
        public bool BeltRunValid { get; private set; }
        public int PendingCount => _pending?.Count ?? 0;

        public override void OnStartClient()
        {
            base.OnStartClient();
            if (!IsOwner)
                return;

            Local = this;
            _player = GetComponent<FirstPersonController>();
            _content = PrototypeContent.Create();
            _input = new FactoryInput();
            _ghosts = new GhostSet();
            _pending = new PendingPlacements();
        }

        public override void OnStopClient()
        {
            base.OnStopClient();
            if (Local != this)
                return;

            Local = null;
            _input.Dispose();
            _input = null;
            _ghosts.Destroy();
            _pending.Clear();
        }

        private void Update()
        {
            if (!IsOwner || _input == null)
                return;

            FactoryNetwork network = FactoryNetwork.Instance;
            FactorySim sim = network != null ? network.ActiveSim : null;
            _pending.Update(sim);

            if (_input.ToggleCursor.WasPressedThisFrame())
                FirstPersonController.SetCursorLocked(Cursor.lockState != CursorLockMode.Locked);

            // A click on an unlocked cursor only re-locks it.
            if (Cursor.lockState != CursorLockMode.Locked)
            {
                if (_input.Primary.WasPressedThisFrame())
                    FirstPersonController.SetCursorLocked(true);
                ClearFrame();
                return;
            }

            HandleModeKeys();
            if (sim == null)
            {
                ClearFrame();
                return;
            }

            UpdateTarget(sim);
            if (_input.Sample.WasPressedThisFrame())
                SampleTarget();

            int ghostCount;
            if (Mode == BuildMode.Dismantle)
                ghostCount = UpdateDismantle(network);
            else if (SelectedDef.Kind == BuildingKind.Belt)
                ghostCount = UpdateBeltTool(network, sim);
            else
                ghostCount = UpdateSingleTool(network, sim);
            _ghosts.HideFrom(ghostCount);
        }

        private void HandleModeKeys()
        {
            if (_input.Dismantle.WasPressedThisFrame())
            {
                Mode = Mode == BuildMode.Build ? BuildMode.Dismantle : BuildMode.Build;
                CancelRun();
            }

            int slot = _input.PressedSlot();
            if (slot >= 0 && slot < _content.Buildable.Count)
            {
                SelectedSlot = slot;
                Mode = BuildMode.Build;
                CancelRun();
            }
        }

        private void UpdateTarget(FactorySim sim)
        {
            _hasTarget = false;
            TargetBuilding = null;

            Camera camera = _player.Camera;
            if (camera == null)
                return;

            Ray ray = camera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            if (!Physics.Raycast(ray, out RaycastHit hit, _reach, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                return;

            _hasTarget = true;
            _placeCell = GridSpace.WorldToCell(hit.point + hit.normal * SurfaceEpsilon);
            Int3 inside = GridSpace.WorldToCell(hit.point - hit.normal * SurfaceEpsilon);
            // Single level for now: buildings live at y = 0, whatever face was hit.
            TargetBuilding = sim.GetBuildingAt(new Int3(inside.X, 0, inside.Z));
        }

        private void SampleTarget()
        {
            if (TargetBuilding == null)
                return;

            for (int i = 0; i < _content.Buildable.Count; i++)
            {
                if (_content.Buildable[i].Id == TargetBuilding.Def.Id)
                    SelectedSlot = i;
            }
            Rotation = TargetBuilding.Rotation;
            Mode = BuildMode.Build;
            CancelRun();
        }

        private int UpdateDismantle(FactoryNetwork network)
        {
            if (!_input.Primary.IsPressed())
                _dismantleRequested.Clear();

            if (TargetBuilding == null)
                return 0;

            _ghosts.Show(0, TargetBuilding.Def, TargetBuilding.Origin, TargetBuilding.Rotation, FactoryPalette.GhostDismantle);
            // Holding LMB removes each building once, while the request travels.
            if (_input.Primary.IsPressed() && _dismantleRequested.Add(TargetBuilding.Id))
                network.RequestRemove(TargetBuilding.Origin);
            return 1;
        }

        private int UpdateSingleTool(FactoryNetwork network, FactorySim sim)
        {
            if (_input.Rotate.WasPressedThisFrame())
                Rotation = (Rotation + 1) & 3;
            if (!_hasTarget)
                return 0;

            bool valid = sim.CanPlace(SelectedDef.Id, _placeCell, Rotation);
            _ghosts.Show(0, SelectedDef, _placeCell, Rotation, valid ? FactoryPalette.GhostValid : FactoryPalette.GhostInvalid);
            if (_input.Primary.WasPressedThisFrame() && valid)
                Request(network, SelectedDef, _placeCell, Rotation);
            return 1;
        }

        private int UpdateBeltTool(FactoryNetwork network, FactorySim sim)
        {
            BuildingDef belt = SelectedDef;
            if (_input.Secondary.WasPressedThisFrame())
                CancelRun();

            if (!_runActive)
            {
                if (_input.Rotate.WasPressedThisFrame())
                    Rotation = (Rotation + 1) & 3;
                if (!_hasTarget)
                    return 0;

                bool hasFeeder = BeltPlanner.TryFindFeeder(sim, _placeCell, out Dir feederFlow);
                int rotation = hasFeeder ? (int)feederFlow : Rotation;
                bool valid = sim.CanPlace(belt.Id, _placeCell, rotation);
                _ghosts.Show(0, belt, _placeCell, rotation, valid ? FactoryPalette.GhostValid : FactoryPalette.GhostInvalid);

                if (_input.Primary.WasPressedThisFrame() && valid)
                {
                    _runActive = true;
                    _runStart = _placeCell;
                    _runStartFlow = hasFeeder ? feederFlow : (Dir?)null;
                    _flipCorner = false;
                }
                return 1;
            }

            if (_input.Rotate.WasPressedThisFrame())
                _flipCorner = !_flipCorner;
            if (!_hasTarget)
                return 0;

            var end = new Int3(_placeCell.X, _runStart.Y, _placeCell.Z);
            Dir? endFlow = BeltPlanner.TryFindConsumer(sim, end, out Dir consumerFlow) ? consumerFlow : (Dir?)null;
            BeltPlanner.Plan(_runStart, end, _runStartFlow, endFlow, _flipCorner, Rotation, _beltPlan);

            BeltRunValid = _beltPlan.Count <= MaxBeltRun;
            for (int i = 0; i < _beltPlan.Count; i++)
            {
                BeltTile tile = _beltPlan[i];
                bool valid = sim.CanPlace(belt.Id, tile.Cell, tile.Rotation);
                BeltRunValid &= valid;
                _ghosts.Show(i, belt, tile.Cell, tile.Rotation, valid ? FactoryPalette.GhostValid : FactoryPalette.GhostInvalid);
            }

            if (_input.Primary.WasPressedThisFrame() && BeltRunValid)
            {
                foreach (BeltTile tile in _beltPlan)
                    Request(network, belt, tile.Cell, tile.Rotation);
                CancelRun();
            }
            return _beltPlan.Count;
        }

        private void Request(FactoryNetwork network, BuildingDef def, Int3 origin, int rotation)
        {
            network.RequestPlace(def.Id, origin, rotation);
            _pending.Add(def, origin, rotation);
        }

        private void CancelRun()
        {
            _runActive = false;
            BeltRunValid = false;
            _beltPlan.Clear();
        }

        private void ClearFrame()
        {
            TargetBuilding = null;
            _ghosts.HideFrom(0);
        }
    }
}
