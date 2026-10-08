using UnityEngine;
using UnityEngine.UI;

namespace IronManHud
{
    /// <summary>
    /// Bends a HUD graphic onto a vertical cylinder so the HUD wraps around the eyes like a helmet visor.
    /// Canvas x becomes arc length; the edges come towards the viewer. Glyphs and brackets are small quads,
    /// so moving their corners is enough (no subdivision needed).
    /// </summary>
    public class CurvedHudVertex : BaseMeshEffect
    {
        /// <summary>The HUD canvas root (its local space is the flat layout).</summary>
        public RectTransform CanvasRoot;
        /// <summary>Cylinder radius in canvas units. The cylinder touches the canvas plane at x = 0.</summary>
        public float RadiusUnits;

        public override void ModifyMesh(VertexHelper vh)
        {
            if (!IsActive() || CanvasRoot == null || RadiusUnits <= 0f)
            {
                return;
            }
            Matrix4x4 toCanvas = CanvasRoot.worldToLocalMatrix * transform.localToWorldMatrix;
            Matrix4x4 fromCanvas = toCanvas.inverse;
            var v = new UIVertex();
            for (int i = 0; i < vh.currentVertCount; i++)
            {
                vh.PopulateUIVertex(ref v, i);
                Vector3 p = toCanvas.MultiplyPoint3x4(v.position);
                float a = p.x / RadiusUnits;
                // The viewer is on the canvas's -z side, so bending towards them is -z.
                p = new Vector3(RadiusUnits * Mathf.Sin(a), p.y, p.z - RadiusUnits * (1f - Mathf.Cos(a)));
                v.position = fromCanvas.MultiplyPoint3x4(p);
                vh.SetUIVertex(v, i);
            }
        }
    }
}
