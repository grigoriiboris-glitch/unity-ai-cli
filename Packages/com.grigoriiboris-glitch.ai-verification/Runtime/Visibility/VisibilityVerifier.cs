using UnityEngine;

namespace UnityAI.Verification
{
    public static class VisibilityVerifier
    {
        public static VerificationResult Verify(
            GameObject target,
            Camera camera,
            VisibilityVerificationPolicy policy,
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
                    Id = "VIS-000",
                    Type = "invalid_target",
                    Severity = "error",
                    Message = "Target GameObject is null.",
                    Confidence = 1f
                });
                return result;
            }

            if (camera == null || policy == null || policy.Expected == VisibilityExpectation.Optional)
                return result;

            var snapshot = RuntimeSnapshot.Capture(target);
            var rendererEnabled = false;

            foreach (var renderer in target.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer != null && renderer.enabled)
                {
                    rendererEnabled = true;
                    break;
                }
            }

            var layerVisible = (camera.cullingMask & (1 << target.layer)) != 0;
            var planes = GeometryUtility.CalculateFrustumPlanes(camera);
            var inFrustum = GeometryUtility.TestPlanesAABB(planes, snapshot.WorldBounds);

            if (policy.Expected == VisibilityExpectation.Required &&
                (!target.activeInHierarchy || !rendererEnabled || !layerVisible || !inFrustum))
            {
                result.Add(VerificationAnomaly.Create(
                    "VIS-001",
                    "required_object_not_visible",
                    "error",
                    snapshot,
                    $"Required visibility failed: active={target.activeInHierarchy}, renderer={rendererEnabled}, layer={layerVisible}, frustum={inFrustum}.",
                    0.97f));
            }
            else if (policy.Expected == VisibilityExpectation.Hidden &&
                     rendererEnabled && layerVisible && inFrustum)
            {
                result.Add(VerificationAnomaly.Create(
                    "VIS-002",
                    "object_visible_but_expected_hidden",
                    "warning",
                    snapshot,
                    "Object is renderable and inside the camera frustum while hidden visibility was expected.",
                    0.92f));
            }

            return result;
        }
    }
}
