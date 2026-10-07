using NUnit.Framework;
using UnityEngine;

namespace UnityAI.Verification.Tests
{
    public sealed class PhysicsVerifierTests
    {
        [Test]
        public void MissingCollider_IsReported()
        {
            var go = new GameObject("Player");
            try
            {
                go.AddComponent<Rigidbody>().useGravity = false;

                var result = PhysicsVerifier.Verify(go, new PhysicsVerificationPolicy());

                Assert.That(result.Anomalies.Exists(a => a.Id == "PHYS-002"), Is.True);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void DisabledLayerCollision_IsReported()
        {
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            var oldIgnore = Physics.GetIgnoreLayerCollision(9, 8);

            try
            {
                ground.name = "Ground";
                player.name = "Player";
                ground.layer = 8;
                player.layer = 9;
                ground.transform.position = Vector3.zero;
                player.transform.position = new Vector3(0f, 1f, 0f);
                player.AddComponent<Rigidbody>().useGravity = false;

                Physics.IgnoreLayerCollision(9, 8, true);
                Physics.SyncTransforms();

                var policy = new PhysicsVerificationPolicy
                {
                    GroundMask = 1 << 8,
                    GroundProbeDistance = 0.5f
                };

                var result = PhysicsVerifier.Verify(player, policy);

                Assert.That(result.Anomalies.Exists(a => a.Id == "PHYS-006"), Is.True);
            }
            finally
            {
                Physics.IgnoreLayerCollision(9, 8, oldIgnore);
                Object.DestroyImmediate(player);
                Object.DestroyImmediate(ground);
            }
        }
    }
}
