using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Pilgrimage.UI
{
    /// <summary>
    /// Flat convex polygon in normalized rect space (0..1). Used by the menu skin for slanted
    /// red / white / black plates. <see cref="Fill"/> clips the polygon from the left edge so a
    /// hover highlight can "fill up" across a slanted button.
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class MenuShape : MaskableGraphic
    {
        [SerializeField] private Vector2[] points = { Vector2.zero, Vector2.up, Vector2.one, Vector2.right };
        [SerializeField, Range(0, 1)] private float fill = 1;

        public Vector2[] Points { get => points; set { points = value; SetVerticesDirty(); } }
        public float Fill
        {
            get => fill;
            set { value = Mathf.Clamp01(value); if (Mathf.Approximately(value, fill)) return; fill = value; SetVerticesDirty(); }
        }

        public static Vector2[] Rectangle => new[] { Vector2.zero, Vector2.up, Vector2.one, Vector2.right };
        /// <summary>Parallelogram leaning right; skew is the horizontal offset as a fraction of width.</summary>
        public static Vector2[] Slant(float skew) => new[] { new Vector2(0, 0), new Vector2(skew, 1), new Vector2(1, 1), new Vector2(1 - skew, 0) };
        public static Vector2[] Triangle => new[] { new Vector2(0, 0), new Vector2(.5f, 1), new Vector2(1, 0) };

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (points == null || points.Length < 3 || fill <= 0) return;
            Rect r = GetPixelAdjustedRect();
            var polygon = new List<Vector2>(points.Length + 2);
            foreach (Vector2 p in points) polygon.Add(new Vector2(r.x + p.x * r.width, r.y + p.y * r.height));
            if (fill < 1) polygon = ClipRight(polygon, r.x + fill * r.width);
            if (polygon.Count < 3) return;
            Color32 c = color;
            foreach (Vector2 p in polygon) vh.AddVert(p, c, Vector2.zero);
            for (int i = 1; i < polygon.Count - 1; i++) vh.AddTriangle(0, i, i + 1);
        }

        // Sutherland–Hodgman against the half-plane x <= limit.
        private static List<Vector2> ClipRight(List<Vector2> input, float limit)
        {
            var output = new List<Vector2>(input.Count + 2);
            for (int i = 0; i < input.Count; i++)
            {
                Vector2 a = input[i], b = input[(i + 1) % input.Count];
                bool aIn = a.x <= limit, bIn = b.x <= limit;
                if (aIn) output.Add(a);
                if (aIn != bIn)
                {
                    float t = (limit - a.x) / (b.x - a.x);
                    output.Add(new Vector2(limit, Mathf.Lerp(a.y, b.y, t)));
                }
            }
            return output;
        }
    }
}
