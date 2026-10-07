using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace UnityAI.Verification.Tests
{
    public sealed class RuntimeRegressionScenarioTests
    {
        private static Scene CreateScenarioScene(string name)
        {
            return SceneManager.CreateScene(name);
        }

        [UnityTest]
        public IEnumerator Test_FallThroughGround()
        {
            var scene = CreateScenarioScene(nameof(Test_FallThroughGround));
            var previousScene = SceneManager.GetActiveScene();
            SceneManager.SetActiveScene(scene);

            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.layer = 8;
            ground.transform.position = Vector3.zero;
            ground.transform.localScale = new Vector3(10f, 1f, 10f);

            var player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            player.layer = 9;
            player.transform.position = new Vector3(0f, 1.5f, 0f);
            player.AddComponent<Rigidbody>();

            Physics.IgnoreLayerCollision(9, 8, false);
            Physics.SyncTransforms();
            yield return new WaitForFixedUpdate();

            var monitor = new TemporalMonitor("Player", "Player");
            monitor.AddSample(new TemporalSample(
                Time.time,
                player.transform.position,
                player.GetComponent<Rigidbody>().linearVelocity,
                true,
                true,
                true,
                false));

            Physics.IgnoreLayerCollision(9, 8, true);
            for (var i = 0; i < 3; i++)
                yield return new WaitForFixedUpdate();

            monitor.AddSample(new TemporalSample(
                Time.time,
                player.transform.position,
                player.GetComponent<Rigidbody>().linearVelocity,
                true,
                true,
                false,
                false));

            yield return new WaitForFixedUpdate();

            monitor.AddSample(new TemporalSample(
                Time.time,
                player.transform.position,
                player.GetComponent<Rigidbody>().linearVelocity,
                true,
                true,
                false,
                false));

            var result = monitor.Evaluate("TASK-REGRESSION", "RUN-FALL");
            Assert.That(result.Anomalies.Exists(a => a.Id == "TEMP-004"), Is.True);

            Physics.IgnoreLayerCollision(9, 8, false);
            Object.Destroy(player);
            Object.Destroy(ground);
            SceneManager.SetActiveScene(previousScene);
            SceneManager.UnloadSceneAsync(scene);
        }

        [UnityTest]
        public IEnumerator Test_MissingCollider()
        {
            var scene = CreateScenarioScene(nameof(Test_MissingCollider));
            var player = new GameObject("Player");
            player.AddComponent<Rigidbody>();

            yield return null;

            var result = PhysicsVerifier.Verify(player, new PhysicsVerificationPolicy());
            Assert.That(result.Anomalies.Exists(a => a.Id == "PHYS-002"), Is.True);

            Object.Destroy(player);
            SceneManager.UnloadSceneAsync(scene);
        }

        [UnityTest]
        public IEnumerator Test_WrongLayer()
        {
            var scene = CreateScenarioScene(nameof(Test_WrongLayer));
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.layer = 8;
            ground.transform.position = Vector3.zero;

            var player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            player.layer = 9;
            player.transform.position = new Vector3(0f, 1.1f, 0f);
            player.AddComponent<Rigidbody>();

            Physics.IgnoreLayerCollision(9, 8, true);
            Physics.SyncTransforms();
            yield return new WaitForFixedUpdate();

            var result = PhysicsVerifier.Verify(
                player,
                new PhysicsVerificationPolicy { GroundMask = 1 << 8, GroundProbeDistance = 0.5f });

            Assert.That(result.Anomalies.Exists(a => a.Id == "PHYS-006"), Is.True);

            Physics.IgnoreLayerCollision(9, 8, false);
            Object.Destroy(player);
            Object.Destroy(ground);
            SceneManager.UnloadSceneAsync(scene);
        }

        [UnityTest]
        public IEnumerator Test_PlayerInvisible()
        {
            var scene = CreateScenarioScene(nameof(Test_PlayerInvisible));
            var player = GameObject.CreatePrimitive(PrimitiveType.Cube);
            player.transform.position = Vector3.forward * 5f;

            var cameraObject = new GameObject("Camera");
            cameraObject.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
            var camera = cameraObject.AddComponent<Camera>();

            yield return null;

            var result = VisibilityVerifier.Verify(
                player,
                camera,
                new VisibilityVerificationPolicy { Expected = VisibilityExpectation.Required });

            Assert.That(result.Anomalies.Exists(a => a.Id == "VIS-001"), Is.True);

            Object.Destroy(player);
            Object.Destroy(cameraObject);
            SceneManager.UnloadSceneAsync(scene);
        }

        [UnityTest]
        public IEnumerator Test_ObjectOutOfBounds()
        {
            var scene = CreateScenarioScene(nameof(Test_ObjectOutOfBounds));
            var player = GameObject.CreatePrimitive(PrimitiveType.Cube);
            player.transform.position = new Vector3(100f, 0f, 0f);

            yield return null;

            var result = BoundsVerifier.Verify(
                player,
                new BoundsVerificationPolicy
                {
                    Min = new Vector3(-10f, -10f, -10f),
                    Max = new Vector3(10f, 10f, 10f)
                });

            Assert.That(result.Anomalies.Exists(a => a.Id == "BOUNDS-001"), Is.True);

            Object.Destroy(player);
            SceneManager.UnloadSceneAsync(scene);
        }

        [UnityTest]
        public IEnumerator Test_Teleport()
        {
            var scene = CreateScenarioScene(nameof(Test_Teleport));
            var monitor = new TemporalMonitor("Player", "Player");
            monitor.AddSample(new TemporalSample(0d, Vector3.zero, Vector3.zero, true, true, true, false));
            monitor.AddSample(new TemporalSample(0.2d, new Vector3(10f, 0f, 0f), Vector3.zero, true, true, true, false));

            yield return null;

            var result = monitor.Evaluate("TASK-REGRESSION", "RUN-TELEPORT");
            Assert.That(result.Anomalies.Exists(a => a.Id == "TEMP-001"), Is.True);

            SceneManager.UnloadSceneAsync(scene);
        }

        [UnityTest]
        public IEnumerator Test_Stuck()
        {
            var scene = CreateScenarioScene(nameof(Test_Stuck));
            var monitor = new TemporalMonitor("Player", "Player");
            monitor.AddSample(new TemporalSample(0d, Vector3.zero, Vector3.zero, true, true, false, true));
            monitor.AddSample(new TemporalSample(2d, new Vector3(0.01f, 0f, 0f), Vector3.zero, true, true, false, true));

            yield return null;

            var result = monitor.Evaluate("TASK-REGRESSION", "RUN-STUCK");
            Assert.That(result.Anomalies.Exists(a => a.Id == "TEMP-003"), Is.True);

            SceneManager.UnloadSceneAsync(scene);
        }

        [UnityTest]
        public IEnumerator Test_UIOutOfBounds()
        {
            var scene = CreateScenarioScene(nameof(Test_UIOutOfBounds));
            var panelObject = new GameObject("InventoryPanel");
            var panel = panelObject.AddComponent<RectTransform>();
            panel.sizeDelta = new Vector2(400f, 100f);
            panel.position = new Vector3(Screen.width + 200f, Screen.height / 2f, 0f);

            yield return null;

            var result = UIBoundsVerifier.Verify(panel);
            Assert.That(result.Anomalies.Exists(a => a.Id == "UI-001"), Is.True);

            Object.Destroy(panelObject);
            SceneManager.UnloadSceneAsync(scene);
        }

        [UnityTest]
        public IEnumerator Test_CameraMismatch()
        {
            var scene = CreateScenarioScene(nameof(Test_CameraMismatch));
            var player = GameObject.CreatePrimitive(PrimitiveType.Cube);
            player.transform.position = Vector3.forward * 5f;

            var cameraObject = new GameObject("Camera");
            cameraObject.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
            var camera = cameraObject.AddComponent<Camera>();

            yield return null;

            var result = VisibilityVerifier.Verify(
                player,
                camera,
                new VisibilityVerificationPolicy { Expected = VisibilityExpectation.Required });

            Assert.That(result.Anomalies.Exists(a => a.Id == "VIS-001"), Is.True);

            Object.Destroy(player);
            Object.Destroy(cameraObject);
            SceneManager.UnloadSceneAsync(scene);
        }
    }
}
