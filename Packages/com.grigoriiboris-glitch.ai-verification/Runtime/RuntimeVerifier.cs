using UnityEngine;

namespace UnityAI.Verification
{
    public sealed class RuntimeVerifier : MonoBehaviour
    {
        [SerializeField] private PhysicsVerificationPolicy _physicsPolicy = new();
        [SerializeField] private BoundsVerificationPolicy _boundsPolicy = new();
        [SerializeField] private VisibilityVerificationPolicy _visibilityPolicy = new();

        public VerificationResult Verify(
            GameObject target,
            Camera camera = null,
            string taskId = null,
            string runId = null)
        {
            var result = new VerificationResult
            {
                TaskId = taskId,
                RunId = runId
            };

            Merge(result, PhysicsVerifier.Verify(target, _physicsPolicy, taskId, runId));
            Merge(result, BoundsVerifier.Verify(target, _boundsPolicy, taskId, runId));

            if (camera != null)
                Merge(result, VisibilityVerifier.Verify(target, camera, _visibilityPolicy, taskId, runId));

            return result;
        }

        public static RuntimeSnapshot Snapshot(GameObject target, string objectId = null)
        {
            return RuntimeSnapshot.Capture(target, objectId);
        }

        private static void Merge(VerificationResult destination, VerificationResult source)
        {
            foreach (var anomaly in source.Anomalies)
                destination.Add(anomaly);
        }
    }
}
