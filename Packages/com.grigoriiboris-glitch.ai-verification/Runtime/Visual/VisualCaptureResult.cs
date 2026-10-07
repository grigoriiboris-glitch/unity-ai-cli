using System;

namespace UnityAI.Verification
{
    [Serializable]
    public sealed class VisualCaptureResult
    {
        public string Status;
        public string Error;
        public VisualEvidence Evidence;

        public bool Success => Status == "ok";

        public static VisualCaptureResult Failed(string error)
        {
            return new VisualCaptureResult
            {
                Status = "failed",
                Error = error
            };
        }
    }
}
