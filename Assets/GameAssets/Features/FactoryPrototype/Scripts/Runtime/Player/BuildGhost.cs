using Friendslop.Features.FactoryPrototype.Simulation;
using UnityEngine;

namespace Friendslop.Features.FactoryPrototype
{
    /// <summary>
    /// One transparent, collider-less preview of a building. Recreated only when the def changes.
    /// </summary>
    public sealed class BuildGhost
    {
        private GameObject _root;
        private BuildingDef _def;
        private Color? _color;

        public void Show(BuildingDef def, Int3 origin, int rotation, Color color)
        {
            if (_def != def || _root == null)
            {
                Destroy();
                _def = def;
                _root = BuildingVisuals.Create(def, origin, rotation, null, true);
                _root.name = $"Ghost {def.Name}";
                _root.transform.localScale = Vector3.one * 1.02f;
            }

            _root.SetActive(true);
            BuildingVisuals.Place(_root.transform, origin, rotation);
            if (_color != color)
            {
                BuildingVisuals.Tint(_root, color);
                _color = color;
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
            _color = null;
        }
    }
}
