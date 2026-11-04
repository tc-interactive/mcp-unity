using NUnit.Framework;
using McpUnity.Tools;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor;
using UnityEditor.SceneManagement;
using Newtonsoft.Json.Linq;

namespace McpUnity.Tests
{
    /// <summary>
    /// Tests for CreateSceneTool handling of the open scenes it replaces
    /// </summary>
    public class CreateSceneToolTests
    {
        private const string SceneName = "McpUnityCreateSceneToolTestNew";
        private const string SavedScenePath = "Assets/McpUnityCreateSceneToolTestSaved.unity";

        private CreateSceneTool _tool;
        private string _createdScenePath;

        [SetUp]
        public void SetUp()
        {
            _tool = new CreateSceneTool();
            _createdScenePath = null;

            // Untitled scene with unsaved changes, like the temporary scenes left by the Test Runner
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            new GameObject("CreateSceneToolTests_Throwaway");
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        }

        [TearDown]
        public void TearDown()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            if (!string.IsNullOrEmpty(_createdScenePath))
            {
                AssetDatabase.DeleteAsset(_createdScenePath);
            }
            AssetDatabase.DeleteAsset(SavedScenePath);
        }

        [Test]
        public void Execute_WithoutDiscard_UntitledDirtyScene_RefusesAndCreatesNothing()
        {
            JObject result = _tool.Execute(new JObject
            {
                ["sceneName"] = SceneName
            });

            Assert.IsFalse(result["success"]?.ToObject<bool>() ?? false, result.ToString());
            Assert.AreEqual("unsaved_changes_error", result["error"]?["type"]?.ToString(), result.ToString());
            Assert.IsNotNull(GameObject.Find("CreateSceneToolTests_Throwaway"));
            Assert.IsEmpty(AssetDatabase.FindAssets($"{SceneName} t:Scene"), "No scene asset should be created");
        }

        [Test]
        public void Execute_DiscardUnsavedChanges_ReplacesUntitledDirtyScene()
        {
            JObject result = _tool.Execute(new JObject
            {
                ["sceneName"] = SceneName,
                ["discardUnsavedChanges"] = true
            });
            _createdScenePath = result["scenePath"]?.ToString();

            Assert.IsTrue(result["success"]?.ToObject<bool>() ?? false, result.ToString());
            Assert.AreEqual(_createdScenePath, SceneManager.GetActiveScene().path);
            Assert.AreEqual(1, ((JArray)result["discardedScenes"]).Count);
        }

        [Test]
        public void Execute_WithoutDiscard_SavedDirtyScene_SavesItFirst()
        {
            Scene saved = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Assert.IsTrue(EditorSceneManager.SaveScene(saved, SavedScenePath), "Failed to create saved test scene");
            new GameObject("CreateSceneToolTests_Work");
            EditorSceneManager.MarkSceneDirty(saved);

            JObject result = _tool.Execute(new JObject
            {
                ["sceneName"] = SceneName
            });
            _createdScenePath = result["scenePath"]?.ToString();

            Assert.IsTrue(result["success"]?.ToObject<bool>() ?? false, result.ToString());
            Assert.AreEqual(1, ((JArray)result["savedScenes"]).Count);

            EditorSceneManager.OpenScene(SavedScenePath, OpenSceneMode.Single);
            Assert.IsNotNull(GameObject.Find("CreateSceneToolTests_Work"), "Changes to the saved scene were not saved");
        }
    }
}
