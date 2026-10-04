using Friendslop.Features.FactoryPrototype.Simulation;
using UnityEngine;
using UnityEngine.Rendering;

namespace Friendslop.Features.FactoryPrototype
{
    /// <summary>
    /// Builds greybox visuals for a building def from primitives: body, forward arrow, port arrows
    /// (input points in, output points out) and, for machines, an emissive status lamp.
    /// Root sits at the origin cell center, rotated like the building, so local offsets match Footprint.
    /// Ghost style: transparent, no colliders, no shadows, no lamp; only the body is tinted (see Tint).
    /// </summary>
    public static class BuildingVisuals
    {
        public const string LampName = "Lamp";

        private const string BodyName = "Body";
        private const float GhostDetailAlpha = 0.6f;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
        private static MaterialPropertyBlock _block;
        private static Material _defaultMaterial;
        private static Material _ghostMaterial;
        private static Material _lampMaterial;

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
            AddArrow(root.transform, "Forward", new Vector3(0f, height + 0.03f, 0.05f), Quaternion.identity,
                isBelt ? 1.4f : 1f, FactoryPalette.Arrow, ghost);

            for (int i = 0; i < def.Ports.Count; i++)
                AddPortArrow(root.transform, def.Ports[i], ghost);

            if (!ghost && def.Kind != BuildingKind.Belt)
            {
                var lampPosition = new Vector3(center.x, height + 0.08f, center.z - 0.3f);
                AddBox(root.transform, LampName, lampPosition, new Vector3(0.18f, 0.12f, 0.18f), FactoryPalette.Status(BuildingStatus.None), false, false);
                Renderer lamp = root.transform.Find(LampName).GetComponent<Renderer>();
                lamp.sharedMaterial = GetLampMaterial(lamp.sharedMaterial);
                SetStatusColor(lamp, FactoryPalette.Status(BuildingStatus.None));
            }

            return root;
        }

        public static void SetStatusColor(Renderer lamp, Color color)
        {
            _block ??= new MaterialPropertyBlock();
            _block.SetColor(BaseColorId, color);
            _block.SetColor(EmissionColorId, color * 2f);
            lamp.SetPropertyBlock(_block);
        }

        /// <summary>Flat arrow just outside the port face. Input points into the building, output points out.</summary>
        private static void AddPortArrow(Transform root, PortDef port, bool ghost)
        {
            Vector3 side = GridSpace.ToVector(port.Side);
            bool isInput = port.Type == PortType.Input;
            Vector3 direction = isInput ? -side : side;

            Vector3 position = new Vector3(port.Cell.X, 0.16f, port.Cell.Z) + side * 0.62f;
            Color color = isInput ? FactoryPalette.InputPort : FactoryPalette.OutputPort;
            AddArrow(root, isInput ? "In" : "Out", position, Quaternion.LookRotation(direction, Vector3.up), 0.8f, color, ghost);
        }

        private static void AddArrow(Transform parent, string name, Vector3 localPosition, Quaternion localRotation, float scale, Color color, bool ghost)
        {
            var arrow = new GameObject(name);
            arrow.transform.SetParent(parent, false);
            arrow.transform.localPosition = localPosition;
            arrow.transform.localRotation = localRotation;
            arrow.transform.localScale = Vector3.one * scale;
            arrow.AddComponent<MeshFilter>().sharedMesh = ArrowMesh.Get();

            var renderer = arrow.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = ghost ? GetGhostMaterial(_defaultMaterial) : _defaultMaterial;
            if (ghost)
            {
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                color.a = GhostDetailAlpha;
            }
            SetColor(renderer, color);
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
            // The body box is always created first, so arrows can reuse the pipeline's default material.
            _defaultMaterial ??= renderer.sharedMaterial;
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

        /// <summary>Emissive copy of the default URP Lit material; color set per renderer.</summary>
        private static Material GetLampMaterial(Material source)
        {
            if (_lampMaterial != null)
                return _lampMaterial;

            _lampMaterial = new Material(source) { name = "StatusLamp (runtime)" };
            _lampMaterial.EnableKeyword("_EMISSION");
            _lampMaterial.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            return _lampMaterial;
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
