using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using McpUnity.Unity;

namespace McpUnity.Utils
{
    /// <summary>
    /// Prepares the open scenes before a tool replaces them (opening or creating a scene in Single mode)
    /// without ever showing Unity's blocking "Save Scene" dialog.
    ///
    /// - Saved scenes with unsaved changes are saved first (they have a path, so no dialog is needed).
    /// - Untitled scenes with unsaved changes (e.g. temporary scenes left by run_tests) would need a Save As
    ///   dialog, so the tool is refused unless the caller passed discardUnsavedChanges to throw them away.
    /// - With discardUnsavedChanges, only untitled scenes are discarded; saved scenes with unsaved changes
    ///   may be someone's work, so the tool is refused instead of silently discarding them.
    /// </summary>
    public static class OpenScenesGuard
    {
        /// <summary>
        /// Saves or discards the open scenes as described above.
        /// </summary>
        /// <param name="discardUnsavedChanges">Whether the caller declared the untitled scenes throwaway</param>
        /// <param name="refusedMessage">Start of the error message when refused, e.g. "Scene was not loaded"</param>
        /// <param name="discardedScenes">Untitled scenes whose unsaved changes will be lost when replaced</param>
        /// <param name="savedScenes">Saved scenes that were saved before being replaced</param>
        /// <returns>Null when the open scenes can be replaced, otherwise an error response</returns>
        public static JObject PrepareToReplace(bool discardUnsavedChanges, string refusedMessage,
            out JArray discardedScenes, out JArray savedScenes)
        {
            discardedScenes = new JArray();
            savedScenes = new JArray();

            var dirtyUntitled = new List<Scene>();
            var dirtySaved = new List<Scene>();
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (!scene.isDirty)
                {
                    continue;
                }

                if (string.IsNullOrEmpty(scene.path))
                {
                    dirtyUntitled.Add(scene);
                }
                else
                {
                    dirtySaved.Add(scene);
                }
            }

            if (discardUnsavedChanges)
            {
                if (dirtySaved.Count > 0)
                {
                    return McpUnitySocketHandler.CreateErrorResponse(
                        $"{refusedMessage}: discardUnsavedChanges only discards untitled scenes, but these saved scenes have unsaved changes: {JoinPaths(dirtySaved)}. " +
                        "Save them with save_scene, or retry without discardUnsavedChanges to save them automatically",
                        "unsaved_changes_error"
                    );
                }

                foreach (Scene scene in dirtyUntitled)
                {
                    discardedScenes.Add(Describe(scene));
                }

                if (dirtyUntitled.Count > 0)
                {
                    McpLogger.LogInfo($"Discarding unsaved changes in {dirtyUntitled.Count} untitled scene(s)");
                }
                return null;
            }

            if (dirtyUntitled.Count > 0)
            {
                // Saving an untitled scene opens a modal Save As dialog that blocks the Editor
                return McpUnitySocketHandler.CreateErrorResponse(
                    $"{refusedMessage}: {dirtyUntitled.Count} untitled scene(s) have unsaved changes and saving them would open a blocking Save dialog. " +
                    "If they are throwaway (e.g. temporary scenes left by run_tests), retry with 'discardUnsavedChanges': true. " +
                    "To keep them, save them first with save_scene and a scene path",
                    "unsaved_changes_error"
                );
            }

            foreach (Scene scene in dirtySaved)
            {
                if (!EditorSceneManager.SaveScene(scene))
                {
                    return McpUnitySocketHandler.CreateErrorResponse(
                        $"{refusedMessage}: failed to save '{scene.path}' before replacing it",
                        "scene_save_error"
                    );
                }
                savedScenes.Add(Describe(scene));
            }

            return null;
        }

        private static JObject Describe(Scene scene)
        {
            return new JObject
            {
                ["name"] = scene.name,
                ["path"] = scene.path
            };
        }

        private static string JoinPaths(List<Scene> scenes)
        {
            var paths = new List<string>();
            foreach (Scene scene in scenes)
            {
                paths.Add(scene.path);
            }
            return string.Join(", ", paths);
        }
    }
}
