using UnityEngine;

namespace UnityAI.Verification
{
    public static class VisualTargetFraming
    {
        public static bool TryGetBounds(GameObject target, out Bounds bounds)
        {
            bounds = default;
            if (target == null)
                return false;

            var renderers = target.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
                return false;

            bounds = renderers[0].bounds;
            for (var i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);

            return true;
        }

        public static bool TryFrame(
            Camera camera,
            GameObject target,
            float padding,
            out Vector3 position,
            out Quaternion rotation)
        {
            position = camera != null ? camera.transform.position : Vector3.zero;
            rotation = camera != null ? camera.transform.rotation : Quaternion.identity;

            if (camera == null || !TryGetBounds(target, out var bounds))
                return false;

            padding = Mathf.Max(0.01f, padding);
            var radius = bounds.extents.magnitude * padding;
            var fovRadians = camera.fieldOfView * Mathf.Deg2Rad;
            var distance = radius / Mathf.Max(0.01f, Mathf.Tan(fovRadians * 0.5f));

            var direction = (camera.transform.position - bounds.center).normalized;
            if (direction.sqrMagnitude < 0.001f)
                direction = -camera.transform.forward;

            position = bounds.center + direction * distance;
            rotation = Quaternion.LookRotation(bounds.center - position, Vector3.up);
            return true;
        }
    }
}
