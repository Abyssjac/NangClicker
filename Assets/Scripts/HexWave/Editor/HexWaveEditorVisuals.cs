using UnityEditor;

namespace NangClicker.HexWave.Editor
{
    internal static class HexWaveEditorVisuals
    {
        public static void RefreshBoundaryPreview(HexWaveManager manager)
        {
            if (manager == null)
                return;

            HexWaveRenderer waveRenderer = manager.GetComponent<HexWaveRenderer>();
            if (waveRenderer == null)
                return;

            waveRenderer.RefreshBoundaryPreview();
            EditorUtility.SetDirty(waveRenderer);
        }
    }
}
