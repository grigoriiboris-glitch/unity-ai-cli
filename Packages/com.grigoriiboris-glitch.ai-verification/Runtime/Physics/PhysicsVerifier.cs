using UnityEngine;

namespace UnityAI.Verification
{
    public static class PhysicsVerifier
    {
        public static VerificationResult Verify(
            GameObject target,
            PhysicsVerificationPolicy policy,
            string taskId = null,
            string runId = null)
        {
            if (target == null)
                return Failure("PHYS-000", "invalid_target", "Target GameObject is null.", taskId, runId);

            policy ??= new PhysicsVerificationPolicy();

            var result = new VerificationResult
            {
                TaskId = taskId,
                RunId = runId
            };

            var snapshot = RuntimeSnapshot.Capture(target);
            var colliders = target.GetComponentsInChildren<Collider>(true);
            var enabledCollider = FindEnabledCollider(colliders);
            var rigidbody = target.GetComponentInChildren<Rigidbody>(true);

            if (policy.RequireCollider && enabledCollider == null)
            {
                result.Add(VerificationAnomaly.Create(
                    "PHYS-002",
                    "missing_collider",
                    "error",
                    snapshot,
                    "No enabled collider was found on the target.",
                    0.99f));
                return result;
            }

            if (policy.RequireRigidbody && rigidbody == null)
            {
                result.Add(VerificationAnomaly.Create(
                    "PHYS-004",
                    "missing_rigidbody",
                    "error",
                    snapshot,
                    "No Rigidbody was found on the target.",
                    0.99f));
            }
            else if (rigidbody != null && !rigidbody.enabled)
            {
                result.Add(VerificationAnomaly.Create(
                    "PHYS-004",
                    "rigidbody_disabled",
                    "error",
                    snapshot,
                    "Rigidbody exists but is disabled.",
                    0.99f));
            }

            if (enabledCollider != null && policy.RejectTriggerCollider && enabledCollider.isTrigger)
            {
                result.Add(VerificationAnomaly.Create(
                    "PHYS-005",
                    "unexpected_trigger",
                    "error",
                    snapshot,
                    "Target collider is configured as a trigger while solid contact is required.",
                    0.96f));
            }

            if (rigidbody != null && rigidbody.enabled && !rigidbody.isKinematic && !rigidbody.detectCollisions)
            {
                result.Add(VerificationAnomaly.Create(
                    "PHYS-005",
                    "rigidbody_collision_disabled",
                    "error",
                    snapshot,
                    "Rigidbody collision detection is disabled.",
                    0.99f));
            }

            var ground = DetectGround(target, snapshot.WorldBounds, policy);
            if (!ground.Found && policy.GroundMask.value != 0)
            {
                var anomaly = VerificationAnomaly.Create(
                    "PHYS-001",
                    "ground_not_detected",
                    "warning",
                    snapshot,
                    "No collider was detected within the configured ground probe distance.",
                    0.76f);
                anomaly.GroundDetected = false;
                result.Add(anomaly);
            }

            if (ground.Found && policy.RequireLayerCollision && enabledCollider != null &&
                Physics.GetIgnoreLayerCollision(target.layer, ground.Collider.gameObject.layer))
            {
                var anomaly = VerificationAnomaly.Create(
                    "PHYS-006",
                    "layer_collision_disabled",
                    "error",
                    snapshot,
                    "Target layer is configured not to collide with the detected ground layer.",
                    0.98f);
                anomaly.GroundDetected = true;
                result.Add(anomaly);
            }

            if (policy.DetectPenetration && enabledCollider != null && HasPenetration(target, enabledCollider))
            {
                var anomaly = VerificationAnomaly.Create(
                    "PHYS-007",
                    "penetration",
                    "error",
                    snapshot,
                    "Target collider overlaps another collider.",
                    0.94f);
                anomaly.GroundDetected = ground.Found;
                result.Add(anomaly);
            }

            return result;
        }

        private static Collider FindEnabledCollider(Collider[] colliders)
        {
            foreach (var collider in colliders)
            {
                if (collider != null && collider.enabled)
                    return collider;
            }

            return null;
        }

        private static (bool Found, Collider Collider, float Distance) DetectGround(
            GameObject target,
            Bounds bounds,
            PhysicsVerificationPolicy policy)
        {
            var origin = bounds.center;
            var maxDistance = bounds.extents.y + Mathf.Max(0f, policy.GroundProbeDistance);
            var hits = Physics.RaycastAll(
                origin,
                Vector3.down,
                maxDistance,
                policy.GroundMask,
                QueryTriggerInteraction.Ignore);

            var bestDistance = float.PositiveInfinity;
            Collider bestCollider = null;

            foreach (var hit in hits)
            {
                var collider = hit.collider;
                if (collider == null)
                    continue;

                if (collider.transform == target.transform ||
                    collider.transform.IsChildOf(target.transform))
                    continue;

                if (hit.distance < bestDistance)
                {
                    bestDistance = hit.distance;
                    bestCollider = collider;
                }
            }

            return (bestCollider != null, bestCollider, bestCollider != null ? bestDistance : 0f);
        }

        private static bool HasPenetration(GameObject target, Collider targetCollider)
        {
            var candidates = Physics.OverlapBox(
                targetCollider.bounds.center,
                targetCollider.bounds.extents,
                targetCollider.transform.rotation,
                ~0,
                QueryTriggerInteraction.Ignore);

            foreach (var candidate in candidates)
            {
                if (candidate == null || candidate == targetCollider)
                    continue;

                if (candidate.transform == target.transform ||
                    candidate.transform.IsChildOf(target.transform))
                    continue;

                if (Physics.ComputePenetration(
                    targetCollider,
                    targetCollider.transform.position,
                    targetCollider.transform.rotation,
                    candidate,
                    candidate.transform.position,
                    candidate.transform.rotation,
                    out _,
                    out _))
                {
                    return true;
                }
            }

            return false;
        }

        private static VerificationResult Failure(
            string id,
            string type,
            string message,
            string taskId,
            string runId)
        {
            var result = new VerificationResult
            {
                TaskId = taskId,
                RunId = runId
            };

            result.Add(new VerificationAnomaly
            {
                Id = id,
                Type = type,
                Severity = "error",
                Message = message,
                Confidence = 1f
            });

            return result;
        }
    }
}
