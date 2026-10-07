using System;
using UnityEngine;

namespace UnityAI.Verification
{
    [Serializable]
    public sealed class RuntimeSnapshot
    {
        public string ObjectId;
        public string ObjectPath;

        public Vector3 Position;
        public Quaternion Rotation;
        public Vector3 LocalScale;
        public Vector3 Velocity;

        public bool Active;
        public bool RendererEnabled;
        public bool HasCollider;
        public bool HasRigidbody;

        public Bounds WorldBounds;

        public static RuntimeSnapshot Capture(GameObject target, string objectId = null)
        {
            if (target == null)
                throw new ArgumentNullException(nameof(target));

            var renderer = target.GetComponentInChildren<Renderer>(true);
            var collider = target.GetComponentInChildren<Collider>(true);
            var rigidbody = target.GetComponentInChildren<Rigidbody>(true);

            return new RuntimeSnapshot
            {
                ObjectId = string.IsNullOrWhiteSpace(objectId) ? target.name : objectId,
                ObjectPath = GetObjectPath(target.transform),
                Position = target.transform.position,
                Rotation = target.transform.rotation,
                LocalScale = target.transform.lossyScale,
                Velocity = rigidbody != null ? rigidbody.linearVelocity : Vector3.zero,
                Active = target.activeInHierarchy,
                RendererEnabled = renderer != null && renderer.enabled,
                HasCollider = collider != null && collider.enabled,
                HasRigidbody = rigidbody != null && rigidbody.enabled,
                WorldBounds = CalculateWorldBounds(target)
            };
        }

        private static Bounds CalculateWorldBounds(GameObject target)
        {
            var renderers = target.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length > 0)
            {
                var bounds = renderers[0].bounds;
                for (var i = 1; i < renderers.Length; i++)
                    bounds.Encapsulate(renderers[i].bounds);
                return bounds;
            }

            var colliders = target.GetComponentsInChildren<Collider>(true);
            if (colliders.Length > 0)
            {
                var bounds = colliders[0].bounds;
                for (var i = 1; i < colliders.Length; i++)
                    bounds.Encapsulate(colliders[i].bounds);
                return bounds;
            }

            return new Bounds(target.transform.position, Vector3.zero);
        }

        private static string GetObjectPath(Transform transform)
        {
            var path = transform.name;
            var current = transform.parent;
            while (current != null)
            {
                path = current.name + "/" + path;
                current = current.parent;
            }

            return path;
        }
    }
}
