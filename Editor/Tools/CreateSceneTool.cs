using System;
using UnityEngine;
using UnityEditor;
using Newtonsoft.Json.Linq;
using McpUnity.Unity;
using McpUnity.Utils;

namespace McpUnity.Tools
{
    /// <summary>
    /// Tool for creating and saving a new Unity scene
    /// </summary>
    public class CreateSceneTool : McpToolBase
    {
        public CreateSceneTool()
        {
            Name = "create_scene";
            Description = "Creates a new scene and saves it to the specified path. Replaces the open scenes: saved scenes with unsaved changes are saved first; untitled scenes with unsaved changes refuse the call unless discardUnsavedChanges is true";
        }

        /// <summary>
        /// Execute the CreateScene tool with the provided parameters
        /// </summary>
        /// <param name="parameters">Tool parameters as a JObject</param>
        public override JObject Execute(JObject parameters)
        {
            // Parameters
            string sceneName = parameters["sceneName"]?.ToObject<string>();
            string folderPath = parameters["folderPath"]?.ToObject<string>();
            bool addToBuildSettings = parameters["addToBuildSettings"]?.ToObject<bool?>() ?? false;
            bool makeActive = parameters["makeActive"]?.ToObject<bool?>() ?? true;
            bool discardUnsavedChanges = parameters["discardUnsavedChanges"]?.ToObject<bool?>() ?? false;

            if (string.IsNullOrEmpty(sceneName))
            {
                return McpUnitySocketHandler.CreateErrorResponse(
                    "Required parameter 'sceneName' not provided",
                    "validation_error"
                );
            }

            // Default folder path
            if (string.IsNullOrEmpty(folderPath))
            {
                folderPath = "Assets";
            }

            // Ensure folder exists
            if (!AssetDatabase.IsValidFolder(folderPath))
            {
                // Attempt to create nested folders as needed
                string[] parts = folderPath.Split(new[] {'/'}, StringSplitOptions.RemoveEmptyEntries);
                string current = parts.Length > 0 && parts[0] == "Assets" ? "Assets" : "Assets";
                for (int i = 0; i < parts.Length; i++)
                {
                    if (i == 0 && parts[i] == "Assets") continue;
                    string next = current + "/" + parts[i];
                    if (!AssetDatabase.IsValidFolder(next))
                    {
                        AssetDatabase.CreateFolder(current, parts[i]);
                    }
                    current = next;
                }
            }

            // Create unique path for the scene
            string basePath = folderPath.TrimEnd('/');
            string scenePath = AssetDatabase.GenerateUniqueAssetPath($"{basePath}/{sceneName}.unity");

            try
            {
                // NewScene in Single mode replaces the open scenes: save the saved ones, and refuse instead of
                // losing untitled ones unless they are declared throwaway
                JObject refused = OpenScenesGuard.PrepareToReplace(
                    discardUnsavedChanges, "Scene was not created", out JArray discardedScenes, out JArray savedScenes);
                if (refused != null)
                {
                    return refused;
                }

                var newScene = UnityEditor.SceneManagement.EditorSceneManager.NewScene(UnityEditor.SceneManagement.NewSceneSetup.DefaultGameObjects, UnityEditor.SceneManagement.NewSceneMode.Single);

                bool saved = UnityEditor.SceneManagement.EditorSceneManager.SaveScene(newScene, scenePath);
                if (!saved)
                {
                    return McpUnitySocketHandler.CreateErrorResponse(
                        $"Failed to save scene at '{scenePath}'",
                        "save_error"
                    );
                }

                AssetDatabase.Refresh();

                // Make the scene active if requested
                if (makeActive)
                {
                    UnityEditor.SceneManagement.EditorSceneManager.OpenScene(scenePath, UnityEditor.SceneManagement.OpenSceneMode.Single);
                }

                // Optionally add to build settings
                if (addToBuildSettings)
                {
                    AddSceneToBuildSettings(scenePath);
                }

                McpLogger.LogInfo($"Created scene '{sceneName}' at path '{scenePath}'");

                string message = $"Successfully created scene '{sceneName}' at path '{scenePath}'";
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
                    ["discardedScenes"] = discardedScenes,
                    ["savedScenes"] = savedScenes
                };
            }
            catch (Exception ex)
            {
                return McpUnitySocketHandler.CreateErrorResponse(
                    $"Error creating scene: {ex.Message}",
                    "scene_creation_error"
                );
            }
        }

        private void AddSceneToBuildSettings(string scenePath)
        {
            var scenes = UnityEditor.EditorBuildSettings.scenes;

            // Check if already present
            foreach (var s in scenes)
            {
                if (s.path == scenePath)
                {
                    return;
                }
            }

            var newList = new UnityEditor.EditorBuildSettingsScene[scenes.Length + 1];
            for (int i = 0; i < scenes.Length; i++)
            {
                newList[i] = scenes[i];
            }
            newList[newList.Length - 1] = new UnityEditor.EditorBuildSettingsScene(scenePath, true);
            UnityEditor.EditorBuildSettings.scenes = newList;
        }
    }
}


