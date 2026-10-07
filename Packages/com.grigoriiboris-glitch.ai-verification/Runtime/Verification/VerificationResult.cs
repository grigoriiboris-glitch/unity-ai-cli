using System;
using System.Collections.Generic;

namespace UnityAI.Verification
{
    [Serializable]
    public sealed class VerificationResult
    {
        public string Status = "passed";
        public string TaskId;
        public string RunId;
        public double DurationMs;
        public List<VerificationAnomaly> Anomalies = new();

        public bool Passed => Anomalies.Count == 0;

        public void Add(VerificationAnomaly anomaly)
        {
            if (anomaly == null)
                return;

            Anomalies.Add(anomaly);
            Status = "failed";
        }
    }
}
