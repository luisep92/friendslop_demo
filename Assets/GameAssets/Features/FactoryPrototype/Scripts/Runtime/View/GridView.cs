using Friendslop.Features.FactoryPrototype.Simulation;
using UnityEngine;

namespace Friendslop.Features.FactoryPrototype
{
    /// <summary>
    /// Generates the buildable ground: a plane covering the sim grid with a 1 m checker texture.
    /// Its collider is what the build tool raycasts against.
    /// </summary>
    public sealed class GridView : MonoBehaviour
    {
        [SerializeField] private Color _colorA = new Color(0.36f, 0.4f, 0.36f);
        [SerializeField] private Color _colorB = new Color(0.32f, 0.36f, 0.32f);

        private Texture2D _texture;
        private Material _material;

        private void Awake()
        {
            int cells = SimConstants.GridMax - SimConstants.GridMin + 1;

            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.SetParent(transform, false);
            // Unity planes are 10 x 10 m, centered on their pivot.
            ground.transform.localPosition = new Vector3(SimConstants.GridMin + cells * 0.5f, 0f, SimConstants.GridMin + cells * 0.5f);
            ground.transform.localScale = new Vector3(cells / 10f, 1f, cells / 10f);

            _texture = new Texture2D(2, 2, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Repeat
            };
            _texture.SetPixels(new[] { _colorA, _colorB, _colorB, _colorA });
            _texture.Apply();

            var renderer = ground.GetComponent<MeshRenderer>();
            _material = new Material(renderer.sharedMaterial)
            {
                mainTexture = _texture,
                mainTextureScale = new Vector2(cells / 2f, cells / 2f)
            };
            _material.SetColor("_BaseColor", Color.white);
            renderer.sharedMaterial = _material;
        }

        private void OnDestroy()
        {
            Destroy(_material);
            Destroy(_texture);
        }
    }
}
