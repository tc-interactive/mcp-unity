using System;
using System.Threading;
using System.Threading.Tasks;
using McpUnity.Unity;
using UnityEngine;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using UnityEditor.TestTools.TestRunner.Api;
using McpUnity.Services;
using McpUnity.Utils;

namespace McpUnity.Tools
{
    /// <summary>
    /// Tool for running Unity Test Runner tests
    /// </summary>
    public class RunTestsTool : McpToolBase
    {
        private readonly ITestRunnerService _testRunnerService;

        public RunTestsTool(ITestRunnerService testRunnerService)
        {
            Name = "run_tests";
            Description = "Runs tests using Unity's Test Runner. Never shows the Test Runner's Save Scene dialog: saved scenes with unsaved changes are saved first; untitled scenes with unsaved changes refuse the run unless discardUnsavedChanges is true";
            IsAsync = true;
            _testRunnerService = testRunnerService;
        }
        
        /// <summary>
        /// Executes the RunTests tool asynchronously on the main thread.
        /// </summary>
        /// <param name="parameters">Tool parameters, including optional 'testMode' and 'testFilter'.</param>
        /// <param name="tcs">TaskCompletionSource to set the result or exception.</param>
        public override async void ExecuteAsync(JObject parameters, TaskCompletionSource<JObject> tcs)
        {
            // Parse parameters
            string testModeStr = parameters?["testMode"]?.ToObject<string>() ?? "EditMode";
            string testFilter = parameters?["testFilter"]?.ToObject<string>(); // Optional
            bool returnOnlyFailures = parameters?["returnOnlyFailures"]?.ToObject<bool>() ?? false; // Optional
            bool returnWithLogs = parameters?["returnWithLogs"]?.ToObject<bool>() ?? false; // Optional
            bool discardUnsavedChanges = parameters?["discardUnsavedChanges"]?.ToObject<bool?>() ?? false; // Optional

            TestMode testMode = TestMode.EditMode;
            
            if (Enum.TryParse(testModeStr, true, out TestMode parsedMode))
            {
                testMode = parsedMode;
            }

            McpLogger.LogInfo($"Executing RunTestsTool: Mode={testMode}, Filter={testFilter ?? "(none)"}");

            // The Test Runner asks to save modified scenes before running: save the saved ones, and refuse instead of
            // showing that blocking dialog for untitled ones unless they are declared throwaway
            JArray discardedScenes;
            JArray savedScenes;
            try
            {
                JObject refused = OpenScenesGuard.SaveOrDiscardUnsavedChanges(
                    discardUnsavedChanges, "Tests were not run", out discardedScenes, out savedScenes);
                if (refused != null)
                {
                    tcs.SetResult(refused);
                    return;
                }
            }
            catch (Exception ex)
            {
                tcs.SetResult(McpUnitySocketHandler.CreateErrorResponse(
                    $"Tests were not run: error preparing the open scenes: {ex.Message}",
                    "scene_save_error"
                ));
                return;
            }

            // Call the service to run tests
            JObject result = await _testRunnerService.ExecuteTestsAsync(testMode, returnOnlyFailures, returnWithLogs, testFilter);

            if (result != null)
            {
                if ((result["success"]?.ToObject<bool>() ?? false) && result["message"] != null)
                {
                    string message = result["message"].ToString();
                    if (savedScenes.Count > 0)
                    {
                        message += $"; saved {savedScenes.Count} scene(s) with unsaved changes first";
                    }
                    if (discardedScenes.Count > 0)
                    {
                        message += $"; discarded unsaved changes in {discardedScenes.Count} untitled scene(s) first";
                    }
                    result["message"] = message;
                }
                result["discardedScenes"] = discardedScenes;
                result["savedScenes"] = savedScenes;
            }
            tcs.SetResult(result);
        }
    }
}
