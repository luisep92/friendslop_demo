using System.Collections.Generic;
using FishNet.Object;
using Friendslop.Features.FactoryPrototype.Simulation;
using UnityEngine;

namespace Friendslop.Features.FactoryPrototype
{
    public enum BuildMode
    {
        /// <summary>Hands free: no preview, LMB does nothing.</summary>
        None,
        Build,
        Dismantle
    }

    /// <summary>
    /// Owner-only build gun. Raycasts from the screen center: the place target is the cell in front of the
    /// hit face, the remove target is the cell behind it.
    /// - Mode None by default. 1-6 enter Build, F toggles Dismantle, MMB samples into Build.
    ///   RMB steps back: cancels a belt run / clears dismantle marks first, then exits to None.
    /// - Build: LMB places. Belts use two clicks (start, end) with an L-shaped path that snaps to ports.
    ///   R flips the corner, or rotates the last tile of a straight / single-tile run.
    ///   Other buildings: R rotates.
    /// - Dismantle, Satisfactory style: click marks/unmarks, Ctrl sweeps the aim to mark,
    ///   hold LMB to dismantle the marked buildings (or the aimed one).
    /// - E opens / closes the inspector of the aimed building, in any mode.
    /// Requests go through FactoryNetwork; the server validates them with the same Footprint rules.
    /// </summary>
    [RequireComponent(typeof(FirstPersonController))]
    public sealed class BuildTool : NetworkBehaviour
    {
        private const float SurfaceEpsilon = 0.01f;
        private const int MaxBeltRun = 64;
        private const int MaxSelection = 100;
        private const float HoldToDismantleSeconds = 0.5f;
        private const float ClickMaxSeconds = 0.25f;

        [SerializeField] private float _reach = 12f;

        private readonly List<BeltTile> _beltPlan = new List<BeltTile>();
        private readonly List<int> _selection = new List<int>();
        private float _holdStart = -1f;
        private Dir? _endOverride;
        private Int3 _endOverrideCell;
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

        /// <summary>Buildings marked for dismantling.</summary>
        public int SelectionCount => _selection.Count;

        /// <summary>0-1 while LMB is held in dismantle mode.</summary>
        public float DismantleProgress { get; private set; }

        /// <summary>Building shown in the inspector panel (E), 0 if closed.</summary>
        public int InspectedBuildingId { get; private set; }

        /// <summary>Why the current preview cannot be placed. Null when it can (or nothing is previewed).</summary>
        public string PlacementMessage { get; private set; }

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
            UpdateInspector(sim);
            PlacementMessage = null;

            int ghostCount = 0;
            if (Mode == BuildMode.Dismantle)
                ghostCount = UpdateDismantle(network, sim);
            else if (Mode == BuildMode.Build)
                ghostCount = SelectedDef.Kind == BuildingKind.Belt ? UpdateBeltTool(network, sim) : UpdateSingleTool(network, sim);
            _ghosts.HideFrom(ghostCount);
        }

        private void HandleModeKeys()
        {
            if (_input.Dismantle.WasPressedThisFrame())
                SetMode(Mode == BuildMode.Dismantle ? BuildMode.None : BuildMode.Dismantle);

            int slot = _input.PressedSlot();
            if (slot >= 0 && slot < _content.Buildable.Count)
            {
                SelectedSlot = slot;
                SetMode(BuildMode.Build);
            }

            // RMB steps back one level: pending work first, then the mode itself.
            if (_input.Secondary.WasPressedThisFrame())
            {
                if (Mode == BuildMode.Build && _runActive)
                    CancelRun();
                else if (Mode == BuildMode.Dismantle && _selection.Count > 0)
                    _selection.Clear();
                else
                    SetMode(BuildMode.None);
            }
        }

        private void SetMode(BuildMode mode)
        {
            Mode = mode;
            CancelRun();
            _selection.Clear();
            _holdStart = -1f;
            DismantleProgress = 0f;
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

        /// <summary>E toggles the inspector on the aimed building. Closes when the building disappears.</summary>
        private void UpdateInspector(FactorySim sim)
        {
            if (InspectedBuildingId != 0 && sim.GetBuilding(InspectedBuildingId) == null)
                InspectedBuildingId = 0;

            if (!_input.Interact.WasPressedThisFrame())
                return;

            if (InspectedBuildingId != 0 && (TargetBuilding == null || TargetBuilding.Id == InspectedBuildingId))
                InspectedBuildingId = 0;
            else if (TargetBuilding != null)
                InspectedBuildingId = TargetBuilding.Id;
        }

        private static string Describe(PlacementResult result)
        {
            switch (result)
            {
                case PlacementResult.Ok: return null;
                case PlacementResult.Occupied: return "Occupied";
                case PlacementResult.OutOfBounds: return "Outside the build area";
                case PlacementResult.WrongLevel: return "Ground level only";
                default: return "Unknown building";
            }
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
            SetMode(BuildMode.Build);
        }

        private int UpdateDismantle(FactoryNetwork network, FactorySim sim)
        {
            // Marked buildings can disappear (removed by anyone, or after a resync).
            for (int i = _selection.Count - 1; i >= 0; i--)
            {
                if (sim.GetBuilding(_selection[i]) == null)
                    _selection.RemoveAt(i);
            }

            bool sweeping = _input.Modifier.IsPressed();
            if (sweeping && TargetBuilding != null && !_selection.Contains(TargetBuilding.Id) && _selection.Count < MaxSelection)
                _selection.Add(TargetBuilding.Id);

            UpdateDismantleHold(network, sim, sweeping);

            int count = 0;
            Color marked = FactoryPalette.GhostDismantle;
            marked.a = Mathf.Lerp(marked.a, 0.9f, DismantleProgress);
            foreach (int id in _selection)
            {
                Building building = sim.GetBuilding(id);
                _ghosts.Show(count++, building.Def, building.Origin, building.Rotation, marked);
            }

            if (TargetBuilding != null && !_selection.Contains(TargetBuilding.Id))
            {
                // With nothing marked, a hold dismantles the aimed building: show its progress.
                Color hover = _selection.Count == 0 && DismantleProgress > 0f ? marked : FactoryPalette.GhostDismantleHover;
                _ghosts.Show(count++, TargetBuilding.Def, TargetBuilding.Origin, TargetBuilding.Rotation, hover);
            }
            return count;
        }

        /// <summary>Short click toggles the mark on the aimed building. A full hold dismantles.</summary>
        private void UpdateDismantleHold(FactoryNetwork network, FactorySim sim, bool sweeping)
        {
            DismantleProgress = 0f;
            if (_input.Primary.WasPressedThisFrame())
                _holdStart = Time.unscaledTime;
            if (_holdStart < 0f)
                return;

            float held = Time.unscaledTime - _holdStart;
            if (_input.Primary.IsPressed())
            {
                DismantleProgress = Mathf.Clamp01(held / HoldToDismantleSeconds);
                if (held < HoldToDismantleSeconds)
                    return;

                DismantleMarkedOrTarget(network, sim);
                _holdStart = -1f;
                DismantleProgress = 0f;
                return;
            }

            if (held <= ClickMaxSeconds && !sweeping && TargetBuilding != null)
            {
                if (!_selection.Remove(TargetBuilding.Id) && _selection.Count < MaxSelection)
                    _selection.Add(TargetBuilding.Id);
            }
            _holdStart = -1f;
        }

        private void DismantleMarkedOrTarget(FactoryNetwork network, FactorySim sim)
        {
            if (_selection.Count == 0)
            {
                if (TargetBuilding != null)
                    network.RequestRemove(TargetBuilding.Origin);
                return;
            }

            foreach (int id in _selection)
                network.RequestRemove(sim.GetBuilding(id).Origin);
            _selection.Clear();
        }

        private int UpdateSingleTool(FactoryNetwork network, FactorySim sim)
        {
            if (_input.Rotate.WasPressedThisFrame())
                Rotation = (Rotation + 1) & 3;
            if (!_hasTarget)
                return 0;

            bool valid = Check(sim, SelectedDef.Id, _placeCell, Rotation);
            _ghosts.Show(0, SelectedDef, _placeCell, Rotation, valid ? FactoryPalette.GhostValid : FactoryPalette.GhostInvalid);
            if (_input.Primary.WasPressedThisFrame() && valid)
                Request(network, SelectedDef, _placeCell, Rotation);
            return 1;
        }

        /// <summary>CanPlace that also records the first rejection reason of the frame.</summary>
        private bool Check(FactorySim sim, ushort defId, Int3 origin, int rotation)
        {
            PlacementResult result = sim.CheckPlacement(defId, origin, rotation);
            if (result != PlacementResult.Ok && PlacementMessage == null)
                PlacementMessage = Describe(result);
            return result == PlacementResult.Ok;
        }

        private int UpdateBeltTool(FactoryNetwork network, FactorySim sim)
        {
            BuildingDef belt = SelectedDef;
            if (!_runActive)
            {
                if (_input.Rotate.WasPressedThisFrame())
                    Rotation = (Rotation + 1) & 3;
                if (!_hasTarget)
                    return 0;

                bool hasFeeder = BeltPlanner.TryFindFeeder(sim, _placeCell, out Dir feederFlow);
                int rotation = hasFeeder ? (int)feederFlow : Rotation;
                bool valid = Check(sim, belt.Id, _placeCell, rotation);
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

            if (!_hasTarget)
                return 0;

            var end = new Int3(_placeCell.X, _runStart.Y, _placeCell.Z);
            // The player's choice for the last tile only holds while aiming at the same end cell.
            if (_endOverride.HasValue && end != _endOverrideCell)
                _endOverride = null;

            if (_input.Rotate.WasPressedThisFrame())
            {
                bool hasCorner = end.X != _runStart.X && end.Z != _runStart.Z;
                if (hasCorner)
                {
                    _flipCorner = !_flipCorner;
                }
                else if (_beltPlan.Count > 0)
                {
                    _endOverride = ((Dir)_beltPlan[_beltPlan.Count - 1].Rotation).Rotate(1);
                    _endOverrideCell = end;
                }
            }

            BeltPlanner.PlanRun(sim, _runStart, end, _runStartFlow, _endOverride, _flipCorner, Rotation, _beltPlan);

            BeltRunValid = _beltPlan.Count <= MaxBeltRun;
            if (!BeltRunValid)
                PlacementMessage = $"Run too long (max {MaxBeltRun})";
            for (int i = 0; i < _beltPlan.Count; i++)
            {
                BeltTile tile = _beltPlan[i];
                bool valid = Check(sim, belt.Id, tile.Cell, tile.Rotation);
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
            _endOverride = null;
            _beltPlan.Clear();
        }

        private void ClearFrame()
        {
            TargetBuilding = null;
            PlacementMessage = null;
            _ghosts.HideFrom(0);
        }
    }
}
