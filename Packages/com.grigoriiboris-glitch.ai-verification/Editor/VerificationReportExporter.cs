using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace UnityAI.Verification.Editor
{
    [Serializable]
    public sealed class EditorSceneSaveEntry
    {
        public string name;
        public string path;
        public bool wasDirty;
        public bool isDirtyAfterSave;
    }

    [Serializable]
    public sealed class EditorSaveState
    {
        public string status;
        public string savedAt;
        public string unityVersion;
        public bool scenesSaved;
        public bool assetsSaved;
        public bool prefabStageSaved;
        public int unsavedSceneCount;
        public List<EditorSceneSaveEntry> scenes = new();
        public string message;
    }

    [Serializable]
    public sealed class VerificationSuiteState
    {
        public string status;
        public string message;
        public int checkedObjects;
    }

    [Serializable]
    public sealed class VerificationReport
    {
        public string status;
        public string generatedAt;
        public string unityVersion;
        public bool isPlaying;
        public int selectedCount;
        public List<string> checkedSuites = new();
        public VerificationSuiteState physics = new();
        public VerificationSuiteState ui = new();
        public VerificationSuiteState visibility = new();
        public List<VerificationAnomaly> anomalies = new();
        public List<string> skipped = new();
    }

    /// <summary>
    /// File-based bridge between the existing Unity MCP and unity-ai CLI.
    /// This assembly does not open sockets, start an HTTP server or register another MCP server.
    /// </summary>
    public static class VerificationReportExporter
    {
        private const string SaveStateRelativePath = ".ai/artifacts/editor-save-state.json";
        private const string ReportRelativePath = ".ai/artifacts/verification-report.json";

        [MenuItem("Tools/AI Verification/Save Scene and Assets")]
        public static void SaveSceneAndAssetsMenu()
        {
            var state = SaveSceneAndAssets();
            if (state.status == "saved")
                Debug.Log("[Unity AI Verification] Scene and assets saved.");
            else
                Debug.LogError("[Unity AI Verification] Save blocked: " + state.message);
        }

        [MenuItem("Tools/AI Verification/Run Selected Checks")]
        public static void RunSelectedChecksMenu()
        {
            var json = RunSelectedChecks();
            Debug.Log("[Unity AI Verification] Report written: " + ReportPath);
            Debug.Log(json);
        }

        public static EditorSaveState SaveSceneAndAssets()
        {
            var state = new EditorSaveState
            {
                status = "blocked",
                savedAt = DateTime.UtcNow.ToString("o"),
                unityVersion = Application.unityVersion,
                scenesSaved = false,
                assetsSaved = false,
                prefabStageSaved = true
            };

            for (var i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                state.scenes.Add(new EditorSceneSaveEntry
                {
                    name = scene.name,
                    path = scene.path,
                    wasDirty = scene.isDirty,
                    isDirtyAfterSave = scene.isDirty
                });

                if (scene.isDirty && string.IsNullOrEmpty(scene.path))
                {
                    state.message = "An open scene has unsaved changes but has never been saved to an asset path. Save it manually in Unity before creating a Git checkpoint.";
                    WriteJson(SaveStatePath, JsonUtility.ToJson(state, true));
                    return state;
                }
            }

            try
            {
                var saveScenesSucceeded = EditorSceneManager.SaveOpenScenes();
                if (!saveScenesSucceeded)
                {
                    state.message = "Unity EditorSceneManager.SaveOpenScenes returned false.";
                    WriteJson(SaveStatePath, JsonUtility.ToJson(state, true));
                    return state;
                }

                AssetDatabase.SaveAssets();
                state.assetsSaved = true;

                var prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
                if (prefabStage != null && prefabStage.prefabContentsRoot != null)
                {
                    var savedPrefab = PrefabUtility.SavePrefabAsset(
                        prefabStage.prefabContentsRoot,
                        out var prefabSavedSuccessfully);
                    state.prefabStageSaved = prefabSavedSuccessfully && savedPrefab != null;
                    if (!state.prefabStageSaved)
                        throw new InvalidOperationException("Unity could not save the open Prefab Stage.");
                    AssetDatabase.SaveAssets();
                }

                var unsaved = 0;
                for (var i = 0; i < SceneManager.sceneCount; i++)
                {
                    var scene = SceneManager.GetSceneAt(i);
                    var entry = state.scenes[i];
                    entry.isDirtyAfterSave = scene.isDirty;
                    if (scene.isDirty)
                        unsaved++;
                }

                state.unsavedSceneCount = unsaved;
                state.scenesSaved = unsaved == 0;
                state.status = state.scenesSaved && state.assetsSaved && state.prefabStageSaved ? "saved" : "blocked";
                state.savedAt = DateTime.UtcNow.ToString("o");
                state.message = state.status == "saved"
                    ? "All open scenes and assets were saved."
                    : "One or more scenes remain dirty after Unity save.";
            }
            catch (Exception exception)
            {
                state.status = "blocked";
                state.message = exception.GetType().Name + ": " + exception.Message;
            }

            WriteJson(SaveStatePath, JsonUtility.ToJson(state, true));
            return state;
        }

        /// <summary>
        /// Executes deterministic checks against the current Unity selection.
        /// Call through Coplay MCP execute_code or the menu item above.
        /// UI pixel bounds require Play Mode and a live Game camera.
        /// </summary>
        public static string RunSelectedChecks(string taskId = null, string runId = null)
        {
            var report = new VerificationReport
            {
                status = "passed",
                generatedAt = DateTime.UtcNow.ToString("o"),
                unityVersion = Application.unityVersion,
                isPlaying = Application.isPlaying
            };
            var selected = Selection.gameObjects;
            report.selectedCount = selected != null ? selected.Length : 0;

            report.checkedSuites.Add("physics");
            report.checkedSuites.Add("ui");
            report.checkedSuites.Add("visibility");

            if (report.selectedCount == 0)
            {
                report.status = "blocked";
                report.physics.status = "blocked";
                report.physics.message = "Select one or more GameObjects in the active scene before running verification.";
                report.ui.status = "blocked";
                report.ui.message = report.physics.message;
                report.visibility.status = "blocked";
                report.visibility.message = report.physics.message;
                report.skipped.Add("No GameObjects are selected.");
                return WriteReport(report);
            }

            Physics.SyncTransforms();
            var physicsTargets = 0;
            var uiTargets = 0;
            var visibilityTargets = 0;
            var uniqueRectTransforms = new HashSet<int>();
            var camera = Application.isPlaying ? Camera.main : null;

            foreach (var target in selected)
            {
                if (target == null)
                    continue;

                var colliders = target.GetComponentsInChildren<Collider>(true);
                var rigidbodies = target.GetComponentsInChildren<Rigidbody>(true);
                if (colliders.Length > 0 || rigidbodies.Length > 0)
                {
                    physicsTargets++;
                    var policy = new PhysicsVerificationPolicy
                    {
                        RequireCollider = true,
                        RequireRigidbody = rigidbodies.Length > 0,
                        RejectTriggerCollider = false,
                        RequireLayerCollision = true,
                        DetectPenetration = true
                    };
                    Merge(report.anomalies, PhysicsVerifier.Verify(target, policy, taskId, runId));
                }

                var rectTransforms = target.GetComponentsInChildren<RectTransform>(true);
                foreach (var rectTransform in rectTransforms)
                {
                    if (rectTransform == null || rectTransform.GetComponentInParent<Canvas>() == null)
                        continue;
                    if (!uniqueRectTransforms.Add(rectTransform.GetInstanceID()))
                        continue;

                    if (!Application.isPlaying)
                        continue;
                    uiTargets++;
                    Merge(report.anomalies, UIBoundsVerifier.Verify(
                        rectTransform,
                        camera,
                        new UIBoundsVerificationPolicy { Enabled = true, Margin = 0f },
                        taskId,
                        runId));
                }

                    if (camera != null)
                {
                    visibilityTargets++;
                    Merge(report.anomalies, VisibilityVerifier.Verify(
                        target,
                        camera,
                        new VisibilityVerificationPolicy { Expected = VisibilityExpectation.Required },
                        taskId,
                        runId));
                }
            }

            report.physics.checkedObjects = physicsTargets;
            report.physics.status = physicsTargets == 0 ? "not_applicable" : "passed";
            report.physics.message = physicsTargets == 0
                ? "No selected object has a Collider or Rigidbody."
                : "Physics checks completed for selected physical objects.";

            report.ui.checkedObjects = uiTargets;
            if (!Application.isPlaying && uniqueRectTransforms.Count > 0)
            {
                report.ui.status = "requires_play_mode";
                report.ui.message = "Enter Play Mode and run the checks again to validate UI pixel bounds against the live Game View.";
                report.skipped.Add("UI bounds: Play Mode is required for Game View pixel dimensions.");
            }
            else
            {
                report.ui.status = uiTargets == 0 ? "not_applicable" : "passed";
                report.ui.message = uiTargets == 0
                    ? "No selected Canvas RectTransform targets were found."
                    : "UI bounds checks completed against the live Game View.";
            }

            report.visibility.checkedObjects = visibilityTargets;
            if (!Application.isPlaying)
            {
                report.visibility.status = "requires_play_mode";
                report.visibility.message = "Enter Play Mode and ensure an active Main Camera before checking visibility.";
                report.skipped.Add("Visibility checks: Play Mode and Camera.main are required.");
            }
            else if (camera == null)
            {
                report.visibility.status = "blocked";
                report.visibility.message = "Camera.main is missing. Tag the active Game Camera as MainCamera and rerun verification.";
                report.skipped.Add("Visibility checks: Camera.main was not found.");
            }
            else
            {
                report.visibility.status = visibilityTargets == 0 ? "not_applicable" : "passed";
                report.visibility.message = visibilityTargets == 0
                    ? "No selected GameObjects were available for visibility checks."
                    : "Visibility checks completed against Camera.main.";
            }

            var hasErrors = false;
            foreach (var anomaly in report.anomalies)
            {
                if (anomaly != null && string.Equals(anomaly.Severity, "error", StringComparison.OrdinalIgnoreCase))
                {
                    hasErrors = true;
                    break;
                }
            }

            if (hasErrors)
                report.status = "failed";
            else if (
                report.ui.status == "requires_play_mode" ||
                report.visibility.status == "requires_play_mode" ||
                report.visibility.status == "blocked")
                report.status = "blocked";

            return WriteReport(report);
        }

        private static void Merge(List<VerificationAnomaly> destination, VerificationResult source)
        {
            if (source == null || source.Anomalies == null)
                return;
            destination.AddRange(source.Anomalies);
        }

        private static string WriteReport(VerificationReport report)
        {
            report.generatedAt = DateTime.UtcNow.ToString("o");
            var json = JsonUtility.ToJson(report, true);
            WriteJson(ReportPath, json);
            return json;
        }

        private static void WriteJson(string absolutePath, string json)
        {
            var directory = Path.GetDirectoryName(absolutePath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);
            File.WriteAllText(absolutePath, json);
        }

        private static string ProjectRoot
        {
            get
            {
                var parent = Directory.GetParent(Application.dataPath);
                return parent != null ? parent.FullName : Directory.GetCurrentDirectory();
            }
        }

        private static string SaveStatePath => Path.Combine(ProjectRoot, SaveStateRelativePath);
        private static string ReportPath => Path.Combine(ProjectRoot, ReportRelativePath);
    }
}
