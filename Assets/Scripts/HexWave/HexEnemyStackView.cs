using System.Collections.Generic;
using UnityEngine;

namespace NangClicker.HexWave
{
    [DisallowMultipleComponent]
    public sealed class HexEnemyStackView : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        private readonly List<GameObject> spheres = new List<GameObject>();

        private TextMesh healthText;
        private MaterialPropertyBlock propertyBlock;
        private Color enemyColor;
        private Color textColor;
        private float columnTopLocalY;
        private float sphereDiameter;
        private float sphereGap;
        private float textGap;

        public int StackId { get; private set; } = -1;

        public void Initialize(
            int stackId,
            Color sphereColor,
            Color labelColor,
            float columnTop,
            float diameter,
            float gap,
            float labelGap)
        {
            StackId = stackId;
            enemyColor = sphereColor;
            textColor = labelColor;
            columnTopLocalY = columnTop;
            sphereDiameter = Mathf.Max(0.01f, diameter);
            sphereGap = Mathf.Max(0f, gap);
            textGap = Mathf.Max(0f, labelGap);
            propertyBlock ??= new MaterialPropertyBlock();
            EnsureHealthText();
        }

        public void Refresh(int count, int totalHealth)
        {
            int visibleCount = Mathf.Max(0, count);
            while (spheres.Count < visibleCount)
                spheres.Add(CreateSphere(spheres.Count));

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

            EnsureHealthText();
            healthText.text = $"HP {Mathf.Max(0, totalHealth)}";
            healthText.color = textColor;
            healthText.transform.localPosition = new Vector3(
                0f,
                columnTopLocalY + visibleCount * (sphereDiameter + sphereGap) + textGap,
                0f);
        }

        public void FaceCamera(Camera targetCamera)
        {
            if (healthText == null || targetCamera == null)
                return;

            Vector3 awayFromCamera = healthText.transform.position - targetCamera.transform.position;
            if (awayFromCamera.sqrMagnitude <= Mathf.Epsilon)
                return;

            healthText.transform.rotation = Quaternion.LookRotation(awayFromCamera, targetCamera.transform.up);
        }

        private GameObject CreateSphere(int index)
        {
            GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.name = $"Enemy Sphere {index}";
            sphere.transform.SetParent(transform, false);

            Collider sphereCollider = sphere.GetComponent<Collider>();
            if (sphereCollider != null)
                Destroy(sphereCollider);

            Renderer sphereRenderer = sphere.GetComponent<Renderer>();
            if (sphereRenderer != null)
            {
                propertyBlock.Clear();
                propertyBlock.SetColor(BaseColorId, enemyColor);
                propertyBlock.SetColor(ColorId, enemyColor);
                sphereRenderer.SetPropertyBlock(propertyBlock);
            }

            return sphere;
        }

        private void EnsureHealthText()
        {
            if (healthText != null)
                return;

            GameObject textObject = new GameObject("Health Text");
            textObject.transform.SetParent(transform, false);
            healthText = textObject.AddComponent<TextMesh>();
            healthText.anchor = TextAnchor.LowerCenter;
            healthText.alignment = TextAlignment.Center;
            healthText.fontSize = 48;
            healthText.characterSize = 0.08f;
            healthText.color = textColor;

            Font runtimeFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (runtimeFont == null)
                return;

            healthText.font = runtimeFont;
            MeshRenderer textRenderer = textObject.GetComponent<MeshRenderer>();
            if (textRenderer != null)
                textRenderer.sharedMaterial = runtimeFont.material;
        }
    }
}
