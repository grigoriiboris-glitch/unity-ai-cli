using NUnit.Framework;
using UnityEngine;

namespace UnityAI.Verification.Tests
{
    public sealed class VisualTargetFramingTests
    {
        [Test]
        public void TryGetBounds_UsesChildRenderers()
        {
            var root = new GameObject("Target");
            var child = GameObject.CreatePrimitive(PrimitiveType.Cube);
            child.transform.SetParent(root.transform);
            child.transform.localPosition = new Vector3(2f, 0f, 0f);

            Assert.That(VisualTargetFraming.TryGetBounds(root, out var bounds), Is.True);
            Assert.That(bounds.center.x, Is.EqualTo(2f).Within(0.01f));

            Object.DestroyImmediate(root);
        }

        [Test]
        public void TryFrame_RejectsTargetWithoutRenderer()
        {
            var cameraObject = new GameObject("Camera");
            var target = new GameObject("Target");
            var camera = cameraObject.AddComponent<Camera>();

            Assert.That(
                VisualTargetFraming.TryFrame(
                    camera,
                    target,
                    1.2f,
                    out _,
                    out _),
                Is.False);

            Object.DestroyImmediate(target);
            Object.DestroyImmediate(cameraObject);
        }
    }
}
