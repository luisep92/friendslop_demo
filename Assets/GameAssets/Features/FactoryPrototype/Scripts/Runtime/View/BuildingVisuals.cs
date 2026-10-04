using Friendslop.Features.FactoryPrototype.Simulation;
using UnityEngine;
using UnityEngine.Rendering;

namespace Friendslop.Features.FactoryPrototype
{
    /// <summary>
    /// Builds greybox visuals for a building def from primitives: body, forward arrow, port markers.
    /// Root sits at the origin cell center, rotated like the building, so local offsets match Footprint.
    /// Ghost style: transparent, no colliders, no shadows; only the body is tinted (see Tint).
    /// </summary>
    public static class BuildingVisuals
    {
        private const string BodyName = "Body";
        private const float GhostDetailAlpha = 0.6f;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static MaterialPropertyBlock _block;
        private static Material _ghostMaterial;

        public static GameObject Create(BuildingDef def, Int3 origin, int rotation, Transform parent, bool ghost)
        {
            var root = new GameObject(def.Name);
            root.transform.SetParent(parent, false);
            Place(root.transform, origin, rotation);

            bool isBelt = def.Kind == BuildingKind.Belt;
            float height = isBelt ? 0.1f : def.Kind == BuildingKind.Crafter ? 1.2f : 0.8f;
            float inset = isBelt ? 0.96f : 0.9f;
            var size = new Vector3(def.SizeX - (1f - inset), height, def.SizeZ - (1f - inset));
            var center = new Vector3((def.SizeX - 1) * 0.5f, height * 0.5f, (def.SizeZ - 1) * 0.5f);
            AddBox(root.transform, BodyName, center, size, FactoryPalette.Building(def), ghost, !ghost);

            // Forward arrow on top of the origin cell.
            float arrowY = height + 0.02f;
            AddBox(root.transform, "Arrow", new Vector3(0f, arrowY, 0.2f), new Vector3(0.12f, 0.04f, 0.45f), FactoryPalette.Arrow, ghost, false);
            AddBox(root.transform, "ArrowTip", new Vector3(0f, arrowY, 0.38f), new Vector3(0.3f, 0.04f, 0.1f), FactoryPalette.Arrow, ghost, false);

            for (int i = 0; i < def.Ports.Count; i++)
            {
                PortDef port = def.Ports[i];
                Vector3 side = GridSpace.ToVector(port.Side);
                var position = new Vector3(port.Cell.X, 0.15f, port.Cell.Z) + side * 0.47f;
                Color color = port.Type == PortType.Input ? FactoryPalette.InputPort : FactoryPalette.OutputPort;
                AddBox(root.transform, port.Type == PortType.Input ? "In" : "Out", position, new Vector3(0.3f, 0.2f, 0.3f), color, ghost, false);
            }

            return root;
        }

        public static void Place(Transform root, Int3 origin, int rotation)
        {
            root.SetPositionAndRotation(GridSpace.CellCenter(origin), GridSpace.ToRotation(rotation));
        }

        /// <summary>Tints the body of a visual (ghost valid / invalid / pending / dismantle).</summary>
        public static void Tint(GameObject root, Color color)
        {
            Transform body = root.transform.Find(BodyName);
            if (body != null)
                SetColor(body.GetComponent<Renderer>(), color);
        }

        private static void AddBox(Transform parent, string name, Vector3 localPosition, Vector3 localScale, Color color, bool ghost, bool withCollider)
        {
            GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            if (!withCollider)
                Object.Destroy(box.GetComponent<Collider>());

            box.transform.SetParent(parent, false);
            box.transform.localPosition = localPosition;
            box.transform.localScale = localScale;

            var renderer = box.GetComponent<Renderer>();
            if (ghost)
            {
                renderer.sharedMaterial = GetGhostMaterial(renderer.sharedMaterial);
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                color.a = GhostDetailAlpha;
            }
            SetColor(renderer, color);
        }

        private static void SetColor(Renderer renderer, Color color)
        {
            _block ??= new MaterialPropertyBlock();
            _block.SetColor(BaseColorId, color);
            renderer.SetPropertyBlock(_block);
        }

        /// <summary>Transparent copy of the default URP Lit material. Shader variant is available in the editor.</summary>
        private static Material GetGhostMaterial(Material source)
        {
            if (_ghostMaterial != null)
                return _ghostMaterial;

            _ghostMaterial = new Material(source) { name = "BuildGhost (runtime)" };
            _ghostMaterial.SetFloat("_Surface", 1f);
            _ghostMaterial.SetFloat("_Blend", 0f);
            _ghostMaterial.SetOverrideTag("RenderType", "Transparent");
            _ghostMaterial.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            _ghostMaterial.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            _ghostMaterial.SetInt("_SrcBlendAlpha", (int)BlendMode.One);
            _ghostMaterial.SetInt("_DstBlendAlpha", (int)BlendMode.OneMinusSrcAlpha);
            _ghostMaterial.SetInt("_ZWrite", 0);
            _ghostMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            _ghostMaterial.renderQueue = (int)RenderQueue.Transparent;
            return _ghostMaterial;
        }
    }
}
