using System;
using System.IO;
using McpUnity.Utils;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEditor;

namespace McpUnity.Unity
{
    /// <summary>
    /// Handles persistence of MCP Unity settings
    /// </summary>
    [Serializable]
    public class McpUnitySettings
    {
        // Constants
        public const string ServerVersion = "1.5.0-tci-97b2b7a";
        public const string PackageName = "com.gamelovers.mcp-unity";
        public const int RequestTimeoutMinimum = 10;
        
        // Paths
        private const string SettingsPath = "ProjectSettings/McpUnitySettings.json";

        // Optional per-user override; UserSettings/ is excluded by Unity's standard .gitignore.
        // Fields present here replace the shared project values and are saved back here, keeping them local.
        // WARNING: The Node server applies the same override. See unityConnectionConfig.ts
        public const string UserSettingsPath = "UserSettings/McpUnitySettings.json";

        private static McpUnitySettings _instance;

        [Tooltip("Port number for MCP server")]
        public int Port = 8090;
        
        [Tooltip("Timeout in seconds for tool request")]
        public int RequestTimeoutSeconds = RequestTimeoutMinimum;
        
        [Tooltip("Whether to automatically start the MCP server when Unity opens")]
        public bool AutoStartServer = true;

        [Tooltip("Allow the MCP WebSocket server to run in Unity -batchmode. Node package installation is still skipped in batch mode. This is disabled by default so CI and cloud builds keep the existing behavior.")]
        public bool AllowBatchModeServer = false;
        
        [Tooltip("Whether to show info logs in the Unity console")]
        public bool EnableInfoLogs = false;

        [Tooltip("Optional: Full path to the npm executable (e.g., /Users/user/.asdf/shims/npm or C:\\path\\to\\npm.cmd). If not set, 'npm' from the system PATH will be used.")]
        public string NpmExecutablePath = string.Empty;
        
        [Tooltip("Allow connections from remote MCP bridges. When disabled, only localhost connections are allowed (default).")]
        public bool AllowRemoteConnections = false;

        [Tooltip("Allow MCP clients to install Unity packages. Disabled by default because installed packages can execute Editor code.")]
        public bool AllowPackageInstallation = false;

        /// <summary>
        /// Singleton instance of settings
        /// </summary>
        public static McpUnitySettings Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new McpUnitySettings();
                }
                return _instance;
            }
        }

        /// <summary>
        /// Returns whether a project settings file already exists without creating one.
        /// This lets batch-mode CI keep its no-initialization behavior unless it has explicitly opted in.
        /// </summary>
        internal static bool HasPersistedSettings => File.Exists(SettingsPath);

        /// <summary>
        /// Returns whether a local UserSettings override file exists
        /// </summary>
        public static bool HasUserSettingsOverride => File.Exists(UserSettingsPath);

        /// <summary>
        /// Private constructor for singleton
        /// </summary>
        private McpUnitySettings() 
        { 
            LoadSettings();
        }

        /// <summary>
        /// Load settings from disk
        /// </summary>
        public void LoadSettings()
        {
            try
            {
                // Load settings from McpUnitySettings.json
                if (File.Exists(SettingsPath))
                {
                    string json = File.ReadAllText(SettingsPath);
                    JsonUtility.FromJsonOverwrite(json, this);
                }
                else
                {
                    // Create default settings file on the first time initialization
                    SaveSettings();
                }

                // Apply the local override on top of the shared project settings
                if (HasUserSettingsOverride)
                {
                    JsonUtility.FromJsonOverwrite(File.ReadAllText(UserSettingsPath), this);
                }
            }
            catch (Exception ex)
            {
                // Can't use LoggerService here as it depends on settings
                Debug.LogError($"[MCP Unity] Failed to load settings: {ex.Message}");
            }
        }

        /// <summary>
        /// Save settings to disk
        /// </summary>
        /// <remarks>
        /// WARNING: This file is also read by the MCP server. Changes here will require updates to it. See mcpUnity.ts
        /// </remarks>
        public void SaveSettings()
        {
            try
            {
                // Save settings to McpUnitySettings.json
                string json = JsonUtility.ToJson(this, true);
                if (!HasUserSettingsOverride)
                {
                    File.WriteAllText(SettingsPath, json);
                    return;
                }

                // Overridden fields go back to the local override; the rest go to the shared project file,
                // which keeps its own values for overridden fields so local values never reach the repository
                JObject current = JObject.Parse(json);
                JObject userSettings = JObject.Parse(File.ReadAllText(UserSettingsPath));
                JObject projectSettings = File.Exists(SettingsPath) ? JObject.Parse(File.ReadAllText(SettingsPath)) : new JObject();

                foreach (JProperty property in current.Properties())
                {
                    JObject target = userSettings.ContainsKey(property.Name) ? userSettings : projectSettings;
                    target[property.Name] = property.Value;
                }

                File.WriteAllText(UserSettingsPath, ToIndentedJson(userSettings));
                File.WriteAllText(SettingsPath, ToIndentedJson(projectSettings));
            }
            catch (Exception ex)
            {
                // Can't use LoggerService here as it might create circular dependency
                Debug.LogError($"[MCP Unity] Failed to save settings: {ex.Message}");
            }
        }

        /// <summary>
        /// Creates the local UserSettings override seeded with the current port, so later port changes stay local.
        /// Returns false if the override already exists or could not be written.
        /// </summary>
        public bool CreateUserSettingsOverride()
        {
            if (HasUserSettingsOverride)
            {
                return false;
            }

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(UserSettingsPath));
                File.WriteAllText(UserSettingsPath, ToIndentedJson(new JObject { ["Port"] = Port }));
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[MCP Unity] Failed to create local settings override: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Serializes with the same 4-space indentation as JsonUtility to avoid formatting-only diffs
        /// </summary>
        private static string ToIndentedJson(JObject json)
        {
            using (var stringWriter = new StringWriter())
            using (var jsonWriter = new JsonTextWriter(stringWriter) { Formatting = Formatting.Indented, Indentation = 4 })
            {
                json.WriteTo(jsonWriter);
                jsonWriter.Flush();
                return stringWriter.ToString();
            }
        }
    }
}
