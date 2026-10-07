using System;
using UnityEngine;

namespace UnityAI.Verification
{
    [Serializable]
    public sealed class VisualEvidence
    {
        public string Id;
        public string Source;
        public string TargetId;
        public string TargetPath;
        public int Width;
        public int Height;
        public string ArtifactPath;
        public string TaskId;
        public string RunId;
        public string CreatedAtUtc;

        public int EncodedByteCount;\n\n        [NonSerialized] public Texture2D Texture;\n        [NonSerialized] public byte[] PngData;
    }
}
