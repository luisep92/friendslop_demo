using System.Collections.Generic;
using Friendslop.Features.FactoryPrototype.Simulation;
using UnityEngine;

namespace Friendslop.Features.FactoryPrototype
{
    /// <summary>
    /// Pool of ghosts for multi-tile previews (belt runs). Show by index each frame, then HideFrom(count).
    /// </summary>
    public sealed class GhostSet
    {
        private readonly List<BuildGhost> _ghosts = new List<BuildGhost>();

        public void Show(int index, BuildingDef def, Int3 origin, int rotation, Color color)
        {
            while (_ghosts.Count <= index)
                _ghosts.Add(new BuildGhost());
            _ghosts[index].Show(def, origin, rotation, color);
        }

        public void HideFrom(int index)
        {
            for (int i = index; i < _ghosts.Count; i++)
                _ghosts[i].Hide();
        }

        public void Destroy()
        {
            foreach (BuildGhost ghost in _ghosts)
                ghost.Destroy();
            _ghosts.Clear();
        }
    }
}
