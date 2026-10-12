using System;
using Newtonsoft.Json.Linq;
using UnityEngine;
using McpUnity.Unity;
using McpUnity.Utils;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace McpUnity.Tools
{
    /// <summary>
    /// Tool for getting the MCP Unity package version and the Unity Editor version
    /// </summary>
    public class GetVersionTool : McpToolBase
    {
        public GetVersionTool()
        {
            Name = "get_version";
            Description = "Gets the MCP Unity package version and the Unity Editor version.";
        }

        public override JObject Execute(JObject parameters)
        {
            try
            {
                var result = new JObject
                {
                    ["success"] = true,
                    ["type"] = "text",
                    ["message"] = $"MCP Unity {McpUnitySettings.ServerVersion} (Unity {Application.unityVersion})",
                    ["version"] = McpUnitySettings.ServerVersion,
                    ["unityVersion"] = Application.unityVersion
                };

                // How the package is installed, as seen by the Package Manager
                PackageInfo packageInfo = PackageInfo.FindForAssembly(typeof(McpUnitySettings).Assembly);
                if (packageInfo != null)
                {
                    result["packageVersion"] = packageInfo.version;
                    result["packageSource"] = packageInfo.source.ToString();
                    if (packageInfo.git != null && !string.IsNullOrEmpty(packageInfo.git.hash))
                    {
                        result["gitRevision"] = packageInfo.git.revision;
                        result["gitHash"] = packageInfo.git.hash;
                    }
                }

                McpLogger.LogInfo($"Version requested: {McpUnitySettings.ServerVersion}");

                return result;
            }
            catch (Exception ex)
            {
                return McpUnitySocketHandler.CreateErrorResponse(
                    $"Error getting version: {ex.Message}",
                    "version_error"
                );
            }
        }
    }
}
