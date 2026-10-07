using NUnit.Framework;
using UnityEngine;

namespace UnityAI.Verification.Tests
{
    public sealed class RuntimeSnapshotTests
    {
        [Test]
        public void Capture_ReportsCoreRuntimeState()
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                go.name = "Player";
                go.transform.position = new Vector3(1f, 2f, 3f);

                var body = go.AddComponent<Rigidbody>();
                body.useGravity = false;
                body.linearVelocity = new Vector3(0f, 2f, 0f);

                var snapshot = RuntimeSnapshot.Capture(go, "PLAYER-1");

                Assert.That(snapshot.ObjectId, Is.EqualTo("PLAYER-1"));
                Assert.That(snapshot.ObjectPath, Is.EqualTo("Player"));
                Assert.That(snapshot.Position, Is.EqualTo(new Vector3(1f, 2f, 3f)));
                Assert.That(snapshot.HasCollider, Is.True);
                Assert.That(snapshot.HasRigidbody, Is.True);
                Assert.That(snapshot.RendererEnabled, Is.True);
                Assert.That(snapshot.Velocity, Is.EqualTo(new Vector3(0f, 2f, 0f)));
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }
    }
}
