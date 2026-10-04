using Friendslop.Features.FactoryPrototype.Simulation;
using UnityEngine;

namespace Friendslop.Features.FactoryPrototype
{
    /// <summary>
    /// Collider-less placement preview, tinted green or red by placement validity.
    /// </summary>
    public sealed class BuildGhost
    {
        private GameObject _root;
        private BuildingDef _def;
        private bool? _valid;

        public void Show(BuildingDef def, Int3 origin, int rotation, bool valid)
        {
            if (_def != def || _root == null)
            {
                Destroy();
                _def = def;
                _root = BuildingVisuals.Create(def, origin, rotation, null, false);
                _root.name = "BuildGhost";
                _root.transform.localScale = Vector3.one * 1.02f;
                _valid = null;
            }

            _root.SetActive(true);
            BuildingVisuals.Place(_root.transform, origin, rotation);
            if (_valid != valid)
            {
                BuildingVisuals.Tint(_root, valid ? FactoryPalette.GhostValid : FactoryPalette.GhostInvalid);
                _valid = valid;
            }
        }

        public void Hide()
        {
            if (_root != null)
                _root.SetActive(false);
        }

        public void Destroy()
        {
            if (_root != null)
                Object.Destroy(_root);
            _root = null;
            _def = null;
        }
    }
}
