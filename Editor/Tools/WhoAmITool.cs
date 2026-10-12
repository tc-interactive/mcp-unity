using System;
using System.Linq;
using Newtonsoft.Json.Linq;
using McpUnity.Unity;
using McpUnity.Utils;

namespace McpUnity.Tools
{
    /// <summary>
    /// Tool that identifies the calling connection among the clients connected to Unity
    /// </summary>
    public class WhoAmITool : McpToolBase
    {
        private readonly McpUnityServer _server;

        public WhoAmITool(McpUnityServer server)
        {
            _server = server;
            Name = "whoami";
            Description = "Identifies this MCP client's connection to Unity (connection ID, client name, remote endpoint) among all connected clients.";
        }

        public override JObject Execute(JObject parameters)
        {
            // Reached when there is no calling connection (e.g. inside batch_execute)
            return Execute(parameters, null);
        }

        public override JObject Execute(JObject parameters, McpConnectionContext connection)
        {
            if (connection == null)
            {
                return McpUnitySocketHandler.CreateErrorResponse(
                    "whoami must be called directly; the calling connection is unknown here (e.g. inside batch_execute)",
                    "connection_unknown"
                );
            }

            try
            {
                string displayName = string.IsNullOrEmpty(connection.ClientName) ? "Unknown" : connection.ClientName;

                var clients = new JArray(
                    _server.Clients
                        .OrderBy(client => client.Key)
                        .Select(client => new JObject
                        {
                            ["connectionId"] = client.Key,
                            ["clientName"] = client.Value,
                            ["isCaller"] = client.Key == connection.ConnectionId
                        })
                );

                var result = new JObject
                {
                    ["success"] = true,
                    ["type"] = "text",
                    ["message"] = $"[{displayName}] {connection.ConnectionId}",
                    ["connectionId"] = connection.ConnectionId,
                    ["clientName"] = connection.ClientName,
                    ["remoteEndPoint"] = connection.RemoteEndPoint,
                    ["connectedClients"] = clients
                };

                McpLogger.LogInfo($"whoami requested by [{displayName}] {connection.ConnectionId}");

                return result;
            }
            catch (Exception ex)
            {
                return McpUnitySocketHandler.CreateErrorResponse(
                    $"Error identifying connection: {ex.Message}",
                    "whoami_error"
                );
            }
        }
    }
}
