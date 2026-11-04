using System;
using UnityEditor;
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
            Description = "Loads a scene by path or name. Supports additive loading (default: false). Non-additive loads save saved scenes with unsaved changes first; untitled scenes with unsaved changes refuse the load unless discardUnsavedChanges is true";
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
                var savedScenes = new JArray();

                if (!additive)
                {
                    // Single mode replaces the open scenes: save the saved ones, and refuse instead of
                    // showing a blocking Save dialog for untitled ones unless they are declared throwaway
                    JObject refused = OpenScenesGuard.PrepareToReplace(
                        discardUnsavedChanges, "Scene was not loaded", out discardedScenes, out savedScenes);
                    if (refused != null)
                    {
                        return refused;
                    }
                }

                var mode = additive
                    ? UnityEditor.SceneManagement.OpenSceneMode.Additive
                    : UnityEditor.SceneManagement.OpenSceneMode.Single;

                var openedScene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(scenePath, mode);

                // For non-additive, scene becomes active automatically. For additive, we do not change active scene.

                McpLogger.LogInfo($"Loaded scene at path '{scenePath}' (additive={additive})");

                string message = $"Successfully loaded scene at path '{scenePath}' (additive={additive.ToString().ToLower()})";
                if (savedScenes.Count > 0)
                {
                    message += $"; saved {savedScenes.Count} scene(s) with unsaved changes first";
                }
                if (discardedScenes.Count > 0)
                {
                    message += $"; discarded unsaved changes in {discardedScenes.Count} untitled scene(s)";
                }

                return new JObject
                {
                    ["success"] = true,
                    ["type"] = "text",
                    ["message"] = message,
                    ["scenePath"] = scenePath,
                    ["additive"] = additive,
                    ["discardUnsavedChanges"] = discardUnsavedChanges,
                    ["discardedScenes"] = discardedScenes,
                    ["savedScenes"] = savedScenes
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
    }
}


