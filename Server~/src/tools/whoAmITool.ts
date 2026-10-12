import { McpServer } from '@modelcontextprotocol/sdk/server/mcp.js';
import { McpUnity } from '../unity/mcpUnity.js';
import { McpUnityError, ErrorType } from '../utils/errors.js';
import * as z from 'zod';
import { Logger } from '../utils/logger.js';
import { CallToolResult } from '@modelcontextprotocol/sdk/types.js';

const toolName = 'whoami';
const toolDescription =
  "Identifies this MCP client's connection to Unity: the client name and connection ID Unity shows in its Server Window, the remote endpoint, and the other connected clients.";

const paramsSchema = z.object({});

interface ConnectedClient {
  connectionId: string;
  clientName: string;
  isCaller: boolean;
}

export function registerWhoAmITool(server: McpServer, mcpUnity: McpUnity, logger: Logger) {
  logger.info(`Registering tool: ${toolName}`);

  server.tool(
    toolName,
    toolDescription,
    paramsSchema.shape,
    async (params: any) => {
      try {
        logger.info(`Executing tool: ${toolName}`, params);
        const result = await toolHandler(server, mcpUnity);
        logger.info(`Tool execution successful: ${toolName}`);
        return result;
      } catch (error) {
        logger.error(`Tool execution failed: ${toolName}`, error);
        throw error;
      }
    }
  );
}

async function toolHandler(server: McpServer, mcpUnity: McpUnity): Promise<CallToolResult> {
  const response = await mcpUnity.sendRequest(
    { method: toolName, params: {} },
    { queueIfDisconnected: false }
  );

  if (!response.success) {
    throw new McpUnityError(
      ErrorType.TOOL_EXECUTION,
      response.message || 'Failed to identify the connection'
    );
  }

  const mcpClient = server.server.getClientVersion();
  const { host, port } = mcpUnity.endpoint;
  const clients: ConnectedClient[] = response.connectedClients ?? [];
  const displayName = response.clientName || 'Unknown';

  const lines = [
    `You are [${displayName}] ${response.connectionId}`,
    `MCP client: ${mcpClient ? `${mcpClient.name} ${mcpClient.version}` : 'unknown'}`,
    `Unity endpoint: ws://${host}:${port} (seen by Unity from ${response.remoteEndPoint ?? 'unknown'})`,
    `Node server process: ${process.pid}`,
    `Connected clients (${clients.length}):`,
    ...clients.map(client => `  ${client.isCaller ? '*' : '-'} [${client.clientName || 'Unknown'}] ${client.connectionId}`)
  ];

  return {
    content: [{ type: 'text', text: lines.join('\n') }],
    data: {
      connectionId: response.connectionId,
      clientName: response.clientName,
      remoteEndPoint: response.remoteEndPoint,
      mcpClient: mcpClient ? { name: mcpClient.name, version: mcpClient.version } : null,
      unityEndpoint: { host, port },
      processId: process.pid,
      connectedClients: clients
    }
  };
}
