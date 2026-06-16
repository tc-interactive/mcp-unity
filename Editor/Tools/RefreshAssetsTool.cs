using System;
using McpUnity.Unity;
using McpUnity.Utils;
using UnityEditor;
using Newtonsoft.Json.Linq;

namespace McpUnity.Tools
{
    /// <summary>
    /// Tool for refreshing the AssetDatabase to import any changed assets.
    /// </summary>
    public class RefreshAssetsTool : McpToolBase
    {
        public RefreshAssetsTool()
        {
            Name = "refresh_assets";
            Description = "Refreshes the AssetDatabase to import any changed assets, equivalent to clicking 'Assets -> Refresh' in the Editor.";
        }
        
        /// <summary>
        /// Execute the RefreshAssets tool synchronously on the main thread
        /// </summary>
        /// <param name="parameters">Tool parameters as a JObject</param>
        /// <returns>Result status of the asset refresh as a JObject</returns>
        public override JObject Execute(JObject parameters)
        {
            McpLogger.LogInfo("[MCP Unity] Refreshing assets...");
            
            try
            {
                // AssetDatabase.Refresh syncs all changes on the disk to the editor
                AssetDatabase.Refresh();
                
                return new JObject
                {
                    ["success"] = true,
                    ["type"] = "text",
                    ["message"] = "Successfully refreshed Unity AssetDatabase."
                };
            }
            catch (Exception ex)
            {
                McpLogger.LogError($"[MCP Unity] Failed to refresh assets: {ex.Message}");
                return McpUnitySocketHandler.CreateErrorResponse(
                    $"Failed to refresh assets: {ex.Message}", 
                    "tool_execution_error"
                );
            }
        }
    }
}
