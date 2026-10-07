using System;
using UnityEngine;

namespace UnityAI.Verification
{
    public enum VisualCaptureSource
    {
        GameView,
        Camera
    }

    [Serializable]
    public sealed class VisualCaptureRequest
    {
        public VisualCaptureSource Source = VisualCaptureSource.GameView;
        public string TargetId;
        public string TargetPath;
        public int Resolution = 384;
        public int MaxBytes = 2 * 1024 * 1024;
        public bool IncludeAlpha = false;

        public void Normalize()
        {
            Resolution = Mathf.Clamp(Resolution, 64, 2048);
            MaxBytes = Mathf.Clamp(MaxBytes, 64 * 1024, 16 * 1024 * 1024);
        }
    }
}
