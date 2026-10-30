using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace UnityAI.Verification.Editor
{
    /// <summary>
    /// Invalidates the last save confirmation as soon as a scene, prefab or project asset changes.
    /// This prevents the CLI from accepting an old successful save after a later unsaved Editor edit.
    /// </summary>
    [InitializeOnLoad]
    internal static class VerificationSaveStateInvalidator
    {
        private const string SaveStateRelativePath = ".ai/artifacts/editor-save-state.json";

        static VerificationSaveStateInvalidator()
        {
            EditorSceneManager.sceneDirtied += OnSceneDirtied;
            EditorApplication.projectChanged += OnProjectChanged;
            Undo.undoRedoPerformed += OnUndoRedo;
        }

        private static void OnSceneDirtied(Scene scene)
        {
            Invalidate("Scene changed after the last successful Unity save: " + scene.name);
        }

        private static void OnProjectChanged()
        {
            Invalidate("Unity project assets changed after the last successful save.");
        }

        private static void OnUndoRedo()
        {
            Invalidate("Unity Undo/Redo changed Editor state after the last successful save.");
        }

        private static void Invalidate(string reason)
        {
            var path = Path.Combine(ProjectRoot, SaveStateRelativePath);
            if (!File.Exists(path))
                return;

            try
            {
                var state = JsonUtility.FromJson<EditorSaveState>(File.ReadAllText(path));
                if (state == null || !string.Equals(state.status, "saved", StringComparison.Ordinal))
                    return;

                state.status = "invalidated";
                state.scenesSaved = false;
                state.assetsSaved = false;
                state.message = reason + " Run Tools/AI Verification/Save Scene and Assets again before creating a Git checkpoint.";
                File.WriteAllText(path, JsonUtility.ToJson(state, true));
            }
            catch (Exception exception)
            {
                Debug.LogError("[Unity AI Verification] Could not invalidate save confirmation: " + exception.Message);
            }
        }

        private static string ProjectRoot
        {
            get
            {
                var parent = Directory.GetParent(Application.dataPath);
                return parent != null ? parent.FullName : Directory.GetCurrentDirectory();
            }
        }
    }
}
