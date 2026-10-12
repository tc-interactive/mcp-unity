using System;
using UnityEditor;
using UnityEngine.SceneManagement;
using Newtonsoft.Json.Linq;
using McpUnity.Unity;
using McpUnity.Utils;

namespace McpUnity.Tools
{
    /// <summary>
    /// Tool for loading a Unity scene, optionally additively
    /// </summary>
    public class LoadSceneTool : McpToolBase
    {
        public LoadSceneTool()
        {
            Name = "load_scene";
            Description = "Loads a scene by path or name. Supports additive loading (default: false) and replacing throwaway open scenes without saving (discardUnsavedChanges, default: false)";
        }

        /// <summary>
        /// Execute the LoadScene tool with the provided parameters
        /// </summary>
        /// <param name="parameters">Tool parameters as a JObject</param>
        public override JObject Execute(JObject parameters)
        {
            string scenePath = parameters["scenePath"]?.ToObject<string>();
            string sceneName = parameters["sceneName"]?.ToObject<string>();
            string folderPath = parameters["folderPath"]?.ToObject<string>();
            bool additive = parameters["additive"]?.ToObject<bool?>() ?? false;
            bool discardUnsavedChanges = parameters["discardUnsavedChanges"]?.ToObject<bool?>() ?? false;

            if (string.IsNullOrEmpty(scenePath))
            {
                if (string.IsNullOrEmpty(sceneName))
                {
                    return McpUnitySocketHandler.CreateErrorResponse(
                        "Provide either 'scenePath' or 'sceneName'",
                        "validation_error"
                    );
                }

                // Resolve scene path by name (optionally within folderPath)
                string filter = $"{sceneName} t:Scene";
                string[] searchInFolders = null;
                if (!string.IsNullOrEmpty(folderPath))
                {
                    if (!AssetDatabase.IsValidFolder(folderPath))
                    {
                        return McpUnitySocketHandler.CreateErrorResponse(
                            $"Folder '{folderPath}' does not exist",
                            "not_found_error"
                        );
                    }
                    searchInFolders = new[] { folderPath };
                }

                var guids = AssetDatabase.FindAssets(filter, searchInFolders);
                foreach (var guid in guids)
                {
                    var path = AssetDatabase.GUIDToAssetPath(guid);
                    if (System.IO.Path.GetFileNameWithoutExtension(path) == sceneName)
                    {
                        scenePath = path;
                        break;
                    }
                }

                if (string.IsNullOrEmpty(scenePath))
                {
                    return McpUnitySocketHandler.CreateErrorResponse(
                        $"Scene named '{sceneName}' not found",
                        "not_found_error"
                    );
                }
            }

            try
            {
                var discardedScenes = new JArray();

                if (!additive)
                {
                    if (discardUnsavedChanges)
                    {
                        // Caller declared the open scenes throwaway (e.g. temporary scenes left by tests):
                        // OpenScene in Single mode replaces them without saving or prompting
                        discardedScenes = GetDirtyOpenScenes();
                        if (discardedScenes.Count > 0)
                        {
                            McpLogger.LogInfo($"Discarding unsaved changes in {discardedScenes.Count} scene(s) before loading '{scenePath}'");
                        }
                    }
                    else if (!UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes())
                    {
                        // Save open scenes before replacing them; untitled scenes may show a save dialog
                        return McpUnitySocketHandler.CreateErrorResponse(
                            "Failed to save open scenes (save cancelled or failed); scene was not loaded. " +
                            "If the open scenes are throwaway, retry with 'discardUnsavedChanges': true",
                            "scene_save_error"
                        );
                    }
                }

                var mode = additive
                    ? UnityEditor.SceneManagement.OpenSceneMode.Additive
                    : UnityEditor.SceneManagement.OpenSceneMode.Single;

                var openedScene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(scenePath, mode);

                // For non-additive, scene becomes active automatically. For additive, we do not change active scene.

                McpLogger.LogInfo($"Loaded scene at path '{scenePath}' (additive={additive})");

                string message = $"Successfully loaded scene at path '{scenePath}' (additive={additive.ToString().ToLower()})";
                if (discardedScenes.Count > 0)
                {
                    message += $"; discarded unsaved changes in {discardedScenes.Count} scene(s)";
                }

                return new JObject
                {
                    ["success"] = true,
                    ["type"] = "text",
                    ["message"] = message,
                    ["scenePath"] = scenePath,
                    ["additive"] = additive,
                    ["discardUnsavedChanges"] = discardUnsavedChanges,
                    ["discardedScenes"] = discardedScenes
                };
            }
            catch (Exception ex)
            {
                return McpUnitySocketHandler.CreateErrorResponse(
                    $"Error loading scene: {ex.Message}",
                    "scene_load_error"
                );
            }
        }

        /// <summary>
        /// Lists the open scenes with unsaved changes (name and path; path is empty for untitled scenes)
        /// </summary>
        private static JArray GetDirtyOpenScenes()
        {
            var dirtyScenes = new JArray();
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (scene.isDirty)
                {
                    dirtyScenes.Add(new JObject
                    {
                        ["name"] = scene.name,
                        ["path"] = scene.path
                    });
                }
            }
            return dirtyScenes;
        }
    }
}


