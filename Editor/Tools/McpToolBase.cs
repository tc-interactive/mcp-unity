using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using Newtonsoft.Json.Linq;

namespace McpUnity.Tools
{
    /// <summary>
    /// Base class for MCP Unity tools that interact with the Unity Editor
    /// </summary>
    public abstract class McpToolBase
    {
        /// <summary>
        /// The name of the tool as used in API calls
        /// </summary>
        public string Name { get; protected set; }
        
        /// <summary>
        /// Description of the tool's functionality
        /// </summary>
        public string Description { get; protected set; }

        /// <summary>
        /// Flag indicating if the tool executes asynchronously on the main thread.
        /// If true, ExecuteAsync should be overridden.
        /// If false, Execute should be overridden.
        /// </summary>
        public bool IsAsync { get; protected set; } = false;
        
        /// <summary>
        /// Execute the tool asynchronously with the provided parameters.
        /// This should be overridden by tools that need to run on the Unity main thread 
        /// or perform long-running operations without blocking the WebSocket handler.
        /// </summary>
        /// <param name="parameters">Tool parameters as a JObject</param>
        /// <param name="tcs">TaskCompletionSource to set the result or exception of the execution</param>
        public virtual void ExecuteAsync(JObject parameters, TaskCompletionSource<JObject> tcs)
        {
            // Default implementation for tools that don't override this.
            // Indicate that this method should have been overridden if IsAsync is true.
            tcs.TrySetException(new NotImplementedException("ExecuteAsync must be overridden if IsAsync is true."));
        }
        
        /// <summary>
        /// Execute the tool synchronously on behalf of a specific client connection.
        /// Tools that need to know who is calling (e.g. whoami) override this; the default ignores the connection.
        /// </summary>
        /// <param name="parameters">Tool parameters as a JObject</param>
        /// <param name="connection">The calling connection, or null when the tool runs outside a direct client request (e.g. inside batch_execute)</param>
        /// <returns>The result of the tool execution as a JObject, or an error JObject</returns>
        public virtual JObject Execute(JObject parameters, McpConnectionContext connection)
        {
            return Execute(parameters);
        }
        
        /// <summary>
        /// Execute the tool synchronously with the provided parameters.
        /// This should be overridden by tools that can execute quickly and directly 
        /// within the WebSocket message handler thread.
        /// </summary>
        /// <param name="parameters">Tool parameters as a JObject</param>
        /// <returns>The result of the tool execution as a JObject, or an error JObject</returns>
        public virtual JObject Execute(JObject parameters)
        {
            // Default implementation for tools that don't override this.
            // Indicate that this method should have been overridden if IsAsync is false.
            return McpUnity.Unity.McpUnitySocketHandler.CreateErrorResponse(
                "Execute must be overridden if IsAsync is false.", 
                "implementation_error"
            );
        }
    }

    /// <summary>
    /// Identifies the WebSocket connection that sent a tool request
    /// </summary>
    public class McpConnectionContext
    {
        /// <summary>
        /// WebSocket session ID, as listed in the Server Window
        /// </summary>
        public string ConnectionId { get; }

        /// <summary>
        /// Client name sent in the X-Client-Name header (empty when the client sent none)
        /// </summary>
        public string ClientName { get; }

        /// <summary>
        /// Remote address and port of the connection
        /// </summary>
        public string RemoteEndPoint { get; }

        public McpConnectionContext(string connectionId, string clientName, string remoteEndPoint)
        {
            ConnectionId = connectionId;
            ClientName = clientName ?? string.Empty;
            RemoteEndPoint = remoteEndPoint;
        }
    }
}
