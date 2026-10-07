using System;

namespace UnityAI.Verification
{
    [Serializable]
    public sealed class TemporalMonitorPolicy
    {
        public float TeleportDistanceThreshold = 5f;
        public float StuckDurationSeconds = 1.5f;
        public float StuckDisplacementThreshold = 0.05f;
        public float FallDistanceThreshold = 0.25f;
        public bool ExpectedGround = true;
        public int MaxSamples = 256;
    }
}
