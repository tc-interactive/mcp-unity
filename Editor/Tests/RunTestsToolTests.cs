using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;
using McpUnity.Services;
using McpUnity.Tools;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.TestTools.TestRunner.Api;
using Newtonsoft.Json.Linq;

namespace McpUnity.Tests
{
    /// <summary>
    /// Tests for RunTestsTool handling of open scenes with unsaved changes, which would otherwise make the
    /// Test Runner show its blocking Save Scene dialog
    /// </summary>
    public class RunTestsToolTests
    {
        private const string SavedScenePath = "Assets/McpUnityRunTestsToolTestSaved.unity";

        /// <summary>
        /// Records the calls instead of starting a real (nested) test run
        /// </summary>
        private class FakeTestRunnerService : ITestRunnerService
        {
            public int ExecuteCalls;
            public bool AnySceneDirtyWhenExecuted;

            public Task<List<ITestAdaptor>> GetAllTestsAsync(string testModeFilter = "")
            {
                return Task.FromResult(new List<ITestAdaptor>());
            }

            public Task<JObject> ExecuteTestsAsync(TestMode testMode, bool returnOnlyFailures, bool returnWithLogs, string testFilter)
            {
                ExecuteCalls++;
                for (int i = 0; i < SceneManager.sceneCount; i++)
                {
                    AnySceneDirtyWhenExecuted |= SceneManager.GetSceneAt(i).isDirty;
                }

                return Task.FromResult(new JObject
                {
                    ["success"] = true,
                    ["type"] = "text",
                    ["message"] = "Fake test run completed"
                });
            }
        }

        private FakeTestRunnerService _service;
        private RunTestsTool _tool;

        [SetUp]
        public void SetUp()
        {
            _service = new FakeTestRunnerService();
            _tool = new RunTestsTool(_service);
        }

        [TearDown]
        public void TearDown()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            AssetDatabase.DeleteAsset(SavedScenePath);
        }

        private JObject Run(JObject parameters)
        {
            var tcs = new TaskCompletionSource<JObject>();
            _tool.ExecuteAsync(parameters, tcs);
            Assert.IsTrue(tcs.Task.IsCompleted, "run_tests did not complete synchronously with the fake service");
            return tcs.Task.Result;
        }

        private static Scene CreateUntitledDirtyScene(NewSceneMode mode)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, mode);
            SceneManager.SetActiveScene(scene);
            new GameObject("RunTestsToolTests_Throwaway");
            EditorSceneManager.MarkSceneDirty(scene);
            return scene;
        }

        private static Scene CreateSavedScene()
        {
            Scene saved = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Assert.IsTrue(EditorSceneManager.SaveScene(saved, SavedScenePath), "Failed to create saved test scene");
            return saved;
        }

        [Test]
        public void Execute_WithoutDiscard_UntitledDirtyScene_RefusesWithoutRunning()
        {
            CreateUntitledDirtyScene(NewSceneMode.Single);

            JObject result = Run(new JObject());

            Assert.AreEqual("unsaved_changes_error", result["error"]?["type"]?.ToString(), result.ToString());
            StringAssert.Contains("discardUnsavedChanges", result["error"]?["message"]?.ToString());
            Assert.AreEqual(0, _service.ExecuteCalls);
            Assert.IsTrue(SceneManager.GetActiveScene().isDirty);
            Assert.IsNotNull(GameObject.Find("RunTestsToolTests_Throwaway"));
        }

        [Test]
        public void Execute_DiscardUnsavedChanges_OnlyUntitledScene_ReplacesItAndRuns()
        {
            CreateUntitledDirtyScene(NewSceneMode.Single);

            JObject result = Run(new JObject { ["discardUnsavedChanges"] = true });

            Assert.IsTrue(result["success"]?.ToObject<bool>() ?? false, result.ToString());
            Assert.AreEqual(1, _service.ExecuteCalls);
            Assert.IsFalse(_service.AnySceneDirtyWhenExecuted, "A scene still had unsaved changes when the tests started");
            Assert.AreEqual(1, ((JArray)result["discardedScenes"]).Count);
            Assert.IsNull(GameObject.Find("RunTestsToolTests_Throwaway"));
        }

        [Test]
        public void Execute_DiscardUnsavedChanges_KeepsSavedSceneOpen()
        {
            CreateSavedScene();
            CreateUntitledDirtyScene(NewSceneMode.Additive);
            Assert.AreEqual(2, SceneManager.sceneCount);

            JObject result = Run(new JObject { ["discardUnsavedChanges"] = true });

            Assert.IsTrue(result["success"]?.ToObject<bool>() ?? false, result.ToString());
            Assert.AreEqual(1, _service.ExecuteCalls);
            Assert.IsFalse(_service.AnySceneDirtyWhenExecuted, "A scene still had unsaved changes when the tests started");
            Assert.AreEqual(1, SceneManager.sceneCount);
            Assert.AreEqual(SavedScenePath, SceneManager.GetSceneAt(0).path);
        }

        [Test]
        public void Execute_WithoutDiscard_SavedDirtyScene_SavesItAndRuns()
        {
            Scene saved = CreateSavedScene();
            new GameObject("RunTestsToolTests_Work");
            EditorSceneManager.MarkSceneDirty(saved);

            JObject result = Run(new JObject());

            Assert.IsTrue(result["success"]?.ToObject<bool>() ?? false, result.ToString());
            Assert.AreEqual(1, _service.ExecuteCalls);
            Assert.IsFalse(_service.AnySceneDirtyWhenExecuted, "A scene still had unsaved changes when the tests started");
            var savedScenes = (JArray)result["savedScenes"];
            Assert.AreEqual(1, savedScenes.Count);
            Assert.AreEqual(SavedScenePath, savedScenes[0]["path"]?.ToString());
            StringAssert.Contains("saved 1 scene(s)", result["message"]?.ToString());

            EditorSceneManager.OpenScene(SavedScenePath, OpenSceneMode.Single);
            Assert.IsNotNull(GameObject.Find("RunTestsToolTests_Work"), "Changes to the saved scene were not saved");
        }

        [Test]
        public void Execute_DiscardUnsavedChanges_SavedDirtyScene_RefusesAndKeepsChanges()
        {
            Scene saved = CreateSavedScene();
            new GameObject("RunTestsToolTests_Work");
            EditorSceneManager.MarkSceneDirty(saved);

            JObject result = Run(new JObject { ["discardUnsavedChanges"] = true });

            Assert.AreEqual("unsaved_changes_error", result["error"]?["type"]?.ToString(), result.ToString());
            Assert.AreEqual(0, _service.ExecuteCalls);
            Assert.IsTrue(SceneManager.GetActiveScene().isDirty);
            Assert.IsNotNull(GameObject.Find("RunTestsToolTests_Work"));
        }
    }
}
