using System.Collections.Generic;
using UnityEngine;

namespace NangClicker.HexWave
{
    [DisallowMultipleComponent]
    public sealed class HexEnemyMovePreviewView : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        private readonly List<GameObject> spheres = new List<GameObject>();
        private readonly List<Renderer> sphereRenderers = new List<Renderer>();

        private TextMesh countText;
        private MaterialPropertyBlock propertyBlock;
        private Material previewMaterial;
        private Color previewColor;
        private float columnTopLocalY;
        private float sphereDiameter;
        private float sphereGap;
        private float textGap;
        private float currentOpacity;

        public void Initialize(
            Material material,
            Color color,
            float columnTop,
            float diameter,
            float gap,
            float labelGap)
        {
            previewMaterial = material;
            previewColor = color;
            columnTopLocalY = columnTop;
            sphereDiameter = Mathf.Max(0.01f, diameter);
            sphereGap = Mathf.Max(0f, gap);
            textGap = Mathf.Max(0f, labelGap);
            currentOpacity = Mathf.Clamp01(color.a);
            propertyBlock ??= new MaterialPropertyBlock();
            EnsureCountText();
        }

        public void Refresh(int count, bool isGoal)
        {
            int visibleCount = Mathf.Max(0, count);
            while (spheres.Count < visibleCount)
                CreateSphere(spheres.Count);

            for (int i = 0; i < spheres.Count; i++)
            {
                GameObject sphere = spheres[i];
                bool active = i < visibleCount;
                sphere.SetActive(active);
                if (!active)
                    continue;

                float y = columnTopLocalY + sphereDiameter * 0.5f + i * (sphereDiameter + sphereGap);
                sphere.transform.localPosition = new Vector3(0f, y, 0f);
                sphere.transform.localRotation = Quaternion.identity;
                sphere.transform.localScale = Vector3.one * sphereDiameter;
            }

            EnsureCountText();
            countText.text = isGoal
                ? $"-{visibleCount}"
                : visibleCount > 1 ? $"x{visibleCount}" : string.Empty;
            countText.transform.localPosition = new Vector3(
                0f,
                columnTopLocalY + visibleCount * (sphereDiameter + sphereGap) + textGap,
                0f);
            ApplyOpacity();
        }

        public void SetOpacity(float opacity)
        {
            currentOpacity = Mathf.Clamp01(opacity);
            ApplyOpacity();
        }

        public void FaceCamera(Camera targetCamera)
        {
            if (countText == null || targetCamera == null || string.IsNullOrEmpty(countText.text))
                return;

            Vector3 awayFromCamera = countText.transform.position - targetCamera.transform.position;
            if (awayFromCamera.sqrMagnitude <= Mathf.Epsilon)
                return;

            countText.transform.rotation = Quaternion.LookRotation(awayFromCamera, targetCamera.transform.up);
        }

        private void CreateSphere(int index)
        {
            GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.name = $"Planned Enemy Sphere {index}";
            sphere.transform.SetParent(transform, false);

            Collider sphereCollider = sphere.GetComponent<Collider>();
            if (sphereCollider != null)
                Destroy(sphereCollider);

            Renderer sphereRenderer = sphere.GetComponent<Renderer>();
            if (sphereRenderer != null)
            {
                if (previewMaterial != null)
                    sphereRenderer.sharedMaterial = previewMaterial;
                sphereRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                sphereRenderer.receiveShadows = false;
                sphereRenderers.Add(sphereRenderer);
            }

            spheres.Add(sphere);
        }

        private void EnsureCountText()
        {
            if (countText != null)
                return;

            GameObject textObject = new GameObject("Planned Count Text");
            textObject.transform.SetParent(transform, false);
            countText = textObject.AddComponent<TextMesh>();
            countText.anchor = TextAnchor.LowerCenter;
            countText.alignment = TextAlignment.Center;
            countText.fontSize = 48;
            countText.characterSize = 0.07f;

            Font runtimeFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (runtimeFont == null)
                return;

            countText.font = runtimeFont;
            MeshRenderer textRenderer = textObject.GetComponent<MeshRenderer>();
            if (textRenderer != null)
            {
                textRenderer.sharedMaterial = runtimeFont.material;
                textRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                textRenderer.receiveShadows = false;
            }
        }

        private void ApplyOpacity()
        {
            Color color = previewColor;
            color.a = currentOpacity;

            propertyBlock ??= new MaterialPropertyBlock();
            for (int i = 0; i < sphereRenderers.Count; i++)
            {
                Renderer sphereRenderer = sphereRenderers[i];
                if (sphereRenderer == null)
                    continue;

                propertyBlock.Clear();
                propertyBlock.SetColor(BaseColorId, color);
                propertyBlock.SetColor(ColorId, color);
                sphereRenderer.SetPropertyBlock(propertyBlock);
            }

            if (countText != null)
                countText.color = color;
        }
    }
}
