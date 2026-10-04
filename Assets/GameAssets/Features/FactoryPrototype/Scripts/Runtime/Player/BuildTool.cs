using FishNet.Object;
using Friendslop.Features.FactoryPrototype.Simulation;
using UnityEngine;

namespace Friendslop.Features.FactoryPrototype
{
    /// <summary>
    /// Owner-only building. Raycasts from the screen center: the place target is the cell in front of the
    /// hit face, the remove target is the cell behind it. Requests go through FactoryNetwork; the server
    /// validates them with the same Footprint rules used for the ghost.
    /// </summary>
    [RequireComponent(typeof(FirstPersonController))]
    public sealed class BuildTool : NetworkBehaviour
    {
        private const float SurfaceEpsilon = 0.01f;

        [SerializeField] private float _reach = 12f;

        private FactoryInput _input;
        private BuildGhost _ghost;
        private ContentDb _content;
        private FirstPersonController _player;
        private Int3 _placeCell;
        private Int3 _removeCell;
        private bool _canPlace;

        public static BuildTool Local { get; private set; }
        public int SelectedSlot { get; private set; }
        public BuildingDef SelectedDef => _content.Buildable[SelectedSlot];
        public int Rotation { get; private set; }

        /// <summary>Building under the crosshair, if any.</summary>
        public Building TargetBuilding { get; private set; }

        public override void OnStartClient()
        {
            base.OnStartClient();
            if (!IsOwner)
                return;

            Local = this;
            _player = GetComponent<FirstPersonController>();
            _content = PrototypeContent.Create();
            _input = new FactoryInput();
            _ghost = new BuildGhost();
        }

        public override void OnStopClient()
        {
            base.OnStopClient();
            if (Local != this)
                return;

            Local = null;
            _input.Dispose();
            _input = null;
            _ghost.Destroy();
        }

        private void Update()
        {
            if (!IsOwner || _input == null)
                return;

            if (_input.ToggleCursor.WasPressedThisFrame())
                FirstPersonController.SetCursorLocked(Cursor.lockState != CursorLockMode.Locked);

            // A click on an unlocked cursor only re-locks it.
            if (Cursor.lockState != CursorLockMode.Locked)
            {
                if (_input.Place.WasPressedThisFrame())
                    FirstPersonController.SetCursorLocked(true);
                _ghost.Hide();
                TargetBuilding = null;
                return;
            }

            int slot = _input.PressedSlot();
            if (slot >= 0 && slot < _content.Buildable.Count)
                SelectedSlot = slot;
            if (_input.Rotate.WasPressedThisFrame())
                Rotation = (Rotation + 1) & 3;

            FactoryNetwork network = FactoryNetwork.Instance;
            FactorySim sim = network != null ? network.ActiveSim : null;
            if (sim == null || !UpdateTarget(sim))
            {
                _ghost.Hide();
                TargetBuilding = null;
                return;
            }

            _ghost.Show(SelectedDef, _placeCell, Rotation, _canPlace);

            if (_input.Place.WasPressedThisFrame() && _canPlace)
                network.RequestPlace(SelectedDef.Id, _placeCell, Rotation);
            else if (_input.Remove.WasPressedThisFrame() && TargetBuilding != null)
                network.RequestRemove(_removeCell);
        }

        private bool UpdateTarget(FactorySim sim)
        {
            Camera camera = _player.Camera;
            if (camera == null)
                return false;

            Ray ray = camera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            if (!Physics.Raycast(ray, out RaycastHit hit, _reach, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                return false;

            _placeCell = GridSpace.WorldToCell(hit.point + hit.normal * SurfaceEpsilon);
            Int3 inside = GridSpace.WorldToCell(hit.point - hit.normal * SurfaceEpsilon);
            // Single level for now: buildings live at y = 0, whatever face was hit.
            _removeCell = new Int3(inside.X, 0, inside.Z);

            TargetBuilding = sim.GetBuildingAt(_removeCell);
            _canPlace = sim.CanPlace(SelectedDef.Id, _placeCell, Rotation);
            return true;
        }
    }
}
