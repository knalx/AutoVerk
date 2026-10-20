using UnityEngine;

namespace AutoVerk.Game
{
    /// <summary>
    /// Coloured poles a skill's preview stands on the ground, like on a field's spots. Plain GameObjects with no
    /// collider and no ZNetView, so they never touch physics or the world save. They go with the scene, on Clear,
    /// when a run starts, and on unload.
    /// </summary>
    public static class Markers
    {
        /// <summary>Ready to work.</summary>
        public static readonly Color Free = new Color(0.3f, 0.9f, 0.3f, 0.9f);
        /// <summary>Something to clear first.</summary>
        public static readonly Color Clear = new Color(1f, 0.85f, 0.2f, 0.9f);
        /// <summary>It will be skipped.</summary>
        public static readonly Color Blocked = new Color(1f, 0.25f, 0.2f, 0.9f);
        /// <summary>Already done.</summary>
        public static readonly Color Done = new Color(0.4f, 0.6f, 1f, 0.9f);

        private static GameObject _root;
        private static Material _material;

        /// <summary>Unity's == treats a root destroyed with the scene as null.</summary>
        public static bool Showing => _root != null;

        /// <summary>A pole 0.8 m tall standing on a point, or on the water over it so it can be seen.</summary>
        public static void Pole(Vector3 at, Color color, string name = "marker")
        {
            at.y = Mathf.Max(at.y, Floating.GetLiquidLevel(at, 1f));
            if (_root == null) _root = new GameObject("AutoVerkMarkers");
            var pole = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pole.name = name;
            Object.Destroy(pole.GetComponent<Collider>());
            pole.transform.SetParent(_root.transform, false);
            // A unit cylinder is 2 m tall around its centre: 0.08 m wide, 0.8 m tall.
            pole.transform.localScale = new Vector3(0.08f, 0.4f, 0.08f);
            pole.transform.position = at + Vector3.up * 0.4f;
            var renderer = pole.GetComponent<MeshRenderer>();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.sharedMaterial = Material(renderer.sharedMaterial);
            var block = new MaterialPropertyBlock();
            block.SetColor("_Color", color);
            renderer.SetPropertyBlock(block);
        }

        /// <summary>Takes all the poles away.</summary>
        public static void Hide()
        {
            if (_root != null) Object.Destroy(_root);
            _root = null;
        }

        /// <summary>Unload: the poles and the shared material.</summary>
        public static void Destroy()
        {
            Hide();
            if (_material != null) Object.Destroy(_material);
            _material = null;
        }

        /// <summary>Unlit, so the colour reads the same at night. Falls back to the primitive's own material.</summary>
        private static Material Material(Material fallback)
        {
            if (_material != null) return _material;
            var shader = Shader.Find("Sprites/Default");
            _material = shader != null ? new Material(shader) : new Material(fallback);
            return _material;
        }
    }
}
