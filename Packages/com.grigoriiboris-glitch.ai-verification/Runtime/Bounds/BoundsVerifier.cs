using UnityEngine;

namespace UnityAI.Verification
{
    public static class BoundsVerifier
    {
        public static VerificationResult Verify(
            GameObject target,
            BoundsVerificationPolicy policy,
            string taskId = null,
            string runId = null)
        {
            var result = new VerificationResult
            {
                TaskId = taskId,
                RunId = runId
            };

            if (target == null)
            {
                result.Add(new VerificationAnomaly
                {
                    Id = "BOUNDS-000",
                    Type = "invalid_target",
                    Severity = "error",
                    Message = "Target GameObject is null.",
                    Confidence = 1f
                });
                return result;
            }

            if (policy == null || !policy.Enabled)
                return result;

            var snapshot = RuntimeSnapshot.Capture(target);
            if (!policy.Contains(snapshot.WorldBounds))
            {
                result.Add(VerificationAnomaly.Create(
                    "BOUNDS-001",
                    "outside_allowed_bounds",
                    "error",
                    snapshot,
                    $"Object bounds are outside configured limits {policy.Min}..{policy.Max}.",
                    0.99f));
            }

            return result;
        }
    }
}
