using NUnit.Framework;
using UnityEngine;

namespace UnityAI.Verification.Tests
{
    public sealed class BoundsAndVisibilityTests
    {
        [Test]
        public void ObjectOutsideBounds_IsReported()
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                go.transform.position = new Vector3(10f, 0f, 0f);

                var policy = new BoundsVerificationPolicy
                {
                    Min = new Vector3(-1f, -1f, -1f),
                    Max = new Vector3(1f, 1f, 1f)
                };

                var result = BoundsVerifier.Verify(go, policy);

                Assert.That(result.Anomalies.Exists(a => a.Id == "BOUNDS-001"), Is.True);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void CameraPointingAway_ReportsRequiredVisibilityFailure()
        {
            var player = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var cameraGo = new GameObject("Camera");

            try
            {
                player.transform.position = Vector3.forward * 5f;
                cameraGo.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
                var camera = cameraGo.AddComponent<Camera>();

                var result = VisibilityVerifier.Verify(
                    player,
                    camera,
                    new VisibilityVerificationPolicy { Expected = VisibilityExpectation.Required });

                Assert.That(result.Anomalies.Exists(a => a.Id == "VIS-001"), Is.True);
            }
            finally
            {
                Object.DestroyImmediate(cameraGo);
                Object.DestroyImmediate(player);
            }
        }
    }
}
