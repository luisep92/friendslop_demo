using Friendslop.Features.FactoryPrototype.Simulation;
using UnityEngine;

namespace Friendslop.Features.FactoryPrototype
{
    /// <summary>
    /// Builds greybox visuals for a building def from primitives: body, forward arrow, port markers.
    /// Root sits at the origin cell center, rotated like the building, so local offsets match Footprint.
    /// </summary>
    public static class BuildingVisuals
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static MaterialPropertyBlock _block;

        public static GameObject Create(BuildingDef def, Int3 origin, int rotation, Transform parent, bool withColliders)
        {
            var root = new GameObject($"{def.Name}");
            root.transform.SetParent(parent, false);
            Place(root.transform, origin, rotation);

            bool isBelt = def.Kind == BuildingKind.Belt;
            float height = isBelt ? 0.1f : def.Kind == BuildingKind.Crafter ? 1.2f : 0.8f;
            float inset = isBelt ? 0.96f : 0.9f;
            var size = new Vector3(def.SizeX - (1f - inset), height, def.SizeZ - (1f - inset));
            var center = new Vector3((def.SizeX - 1) * 0.5f, height * 0.5f, (def.SizeZ - 1) * 0.5f);
            AddBox(root.transform, "Body", center, size, FactoryPalette.Building(def), withColliders);

            // Forward arrow on top of the origin cell.
            float arrowY = height + 0.02f;
            AddBox(root.transform, "Arrow", new Vector3(0f, arrowY, 0.2f), new Vector3(0.12f, 0.04f, 0.45f), FactoryPalette.Arrow, false);
            AddBox(root.transform, "ArrowTip", new Vector3(0f, arrowY, 0.38f), new Vector3(0.3f, 0.04f, 0.1f), FactoryPalette.Arrow, false);

            for (int i = 0; i < def.Ports.Count; i++)
            {
                PortDef port = def.Ports[i];
                Vector3 side = GridSpace.ToVector(port.Side);
                var position = new Vector3(port.Cell.X, 0.15f, port.Cell.Z) + side * 0.47f;
                Color color = port.Type == PortType.Input ? FactoryPalette.InputPort : FactoryPalette.OutputPort;
                AddBox(root.transform, port.Type == PortType.Input ? "In" : "Out", position, new Vector3(0.3f, 0.2f, 0.3f), color, false);
            }

            return root;
        }

        public static void Place(Transform root, Int3 origin, int rotation)
        {
            root.SetPositionAndRotation(GridSpace.CellCenter(origin), GridSpace.ToRotation(rotation));
        }

        /// <summary>Tints every renderer under root (ghost valid/invalid).</summary>
        public static void Tint(GameObject root, Color color)
        {
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>())
                SetColor(renderer, color);
        }

        private static void AddBox(Transform parent, string name, Vector3 localPosition, Vector3 localScale, Color color, bool withCollider)
        {
            GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            if (!withCollider)
                Object.Destroy(box.GetComponent<Collider>());

            box.transform.SetParent(parent, false);
            box.transform.localPosition = localPosition;
            box.transform.localScale = localScale;
            SetColor(box.GetComponent<Renderer>(), color);
        }

        private static void SetColor(Renderer renderer, Color color)
        {
            _block ??= new MaterialPropertyBlock();
            _block.SetColor(BaseColorId, color);
            renderer.SetPropertyBlock(_block);
        }
    }
}
