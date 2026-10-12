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
    /// Tests for LoadSceneTool handling of unsaved (throwaway) scenes
    /// </summary>
    public class LoadSceneToolTests
    {
        private const string TargetScenePath = "Assets/McpUnityLoadSceneToolTestTarget.unity";

        private LoadSceneTool _tool;

        [SetUp]
        public void SetUp()
        {
            _tool = new LoadSceneTool();

            Scene target = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Assert.IsTrue(EditorSceneManager.SaveScene(target, TargetScenePath), "Failed to create target test scene");

            // Untitled scene with unsaved changes, like the temporary scenes left by the Test Runner
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            new GameObject("LoadSceneToolTests_Throwaway");
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        }

        [TearDown]
        public void TearDown()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            AssetDatabase.DeleteAsset(TargetScenePath);
        }

        [Test]
        public void Execute_DiscardUnsavedChanges_ReplacesUntitledDirtySceneWithoutSaving()
        {
            Assert.IsTrue(SceneManager.GetActiveScene().isDirty);
            Assert.IsTrue(string.IsNullOrEmpty(SceneManager.GetActiveScene().path));

            JObject result = _tool.Execute(new JObject
            {
                ["scenePath"] = TargetScenePath,
                ["discardUnsavedChanges"] = true
            });

            Assert.IsTrue(result["success"]?.ToObject<bool>() ?? false, result.ToString());
            Assert.AreEqual(TargetScenePath, SceneManager.GetActiveScene().path);
            Assert.AreEqual(1, SceneManager.sceneCount);

            var discarded = (JArray)result["discardedScenes"];
            Assert.AreEqual(1, discarded.Count);
            Assert.AreEqual(string.Empty, discarded[0]["path"]?.ToString());
        }

        [Test]
        public void Execute_DiscardUnsavedChanges_Additive_KeepsOpenScenes()
        {
            Scene throwaway = SceneManager.GetActiveScene();

            JObject result = _tool.Execute(new JObject
            {
                ["scenePath"] = TargetScenePath,
                ["additive"] = true,
                ["discardUnsavedChanges"] = true
            });

            Assert.IsTrue(result["success"]?.ToObject<bool>() ?? false, result.ToString());
            Assert.AreEqual(2, SceneManager.sceneCount);
            Assert.IsTrue(throwaway.isLoaded);
            Assert.IsTrue(throwaway.isDirty);
            Assert.AreEqual(0, ((JArray)result["discardedScenes"]).Count);
        }
    }
}
