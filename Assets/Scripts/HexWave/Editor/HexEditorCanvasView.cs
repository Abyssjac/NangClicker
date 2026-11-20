using System;
using UnityEngine;

namespace NangClicker.HexWave.Editor
{
    [Serializable]
    public sealed class HexEditorCanvasView
    {
        public Vector2 pan;
        public float zoom = 1f;
        public Vector2 ToScreen(Vector2 local, Rect rect, float baseScale) => rect.center + pan + local * (baseScale * zoom);
        public Vector2 ToLocal(Vector2 screen, Rect rect, float baseScale) => (screen - rect.center - pan) / (baseScale * zoom);
        public void ZoomAt(Vector2 pointer, Rect rect, float baseScale, float delta)
        {
            Vector2 local = ToLocal(pointer, rect, baseScale);
            zoom = Mathf.Clamp(zoom * Mathf.Exp(-delta * 0.08f), 0.12f, 12f);
            pan = pointer - rect.center - local * (baseScale * zoom);
        }
        public void Reset() { pan = Vector2.zero; zoom = 1f; }
    }
}
