using UnityEngine;

namespace UnityAI.Verification
{
    public static class UIBoundsVerifier
    {
        public static VerificationResult Verify(
            RectTransform target,
            Camera camera = null,
            UIBoundsVerificationPolicy policy = null,
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
                    Id = "UI-000",
                    Type = "invalid_target",
                    Severity = "error",
                    Message = "Target RectTransform is null.",
                    Confidence = 1f
                });
                return result;
            }

            if (policy == null || !policy.Enabled)
                return result;

            var corners = new Vector3[4];
            target.GetWorldCorners(corners);

            var min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
            var max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);

            foreach (var corner in corners)
            {
                var point = RectTransformUtility.WorldToScreenPoint(camera, corner);
                min = Vector2.Min(min, point);
                max = Vector2.Max(max, point);
            }

            var margin = Mathf.Max(0f, policy.Margin);
            var inside = min.x >= -margin &&
                         min.y >= -margin &&
                         max.x <= Screen.width + margin &&
                         max.y <= Screen.height + margin;

            if (inside)
                return result;

            result.Add(new VerificationAnomaly
            {
                Id = "UI-001",
                Type = "ui_out_of_bounds",
                Severity = "error",
                ObjectId = target.name,
                ObjectPath = GetPath(target),
                Message = $"UI bounds [{min.x:0.##},{min.y:0.##}]..[{max.x:0.##},{max.y:0.##}] exceed screen {Screen.width}x{Screen.height}.",
                Confidence = 0.99f,
                Position = target.position
            });

            return result;
        }

        private static string GetPath(Transform transform)
        {
            var path = transform.name;
            var parent = transform.parent;
            while (parent != null)
            {
                path = parent.name + "/" + path;
                parent = parent.parent;
            }

            return path;
        }
    }
}
