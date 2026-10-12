import { McpServer } from '@modelcontextprotocol/sdk/server/mcp.js';
import { McpUnity } from '../unity/mcpUnity.js';
import * as z from 'zod';
import { Logger } from '../utils/logger.js';
import { CallToolResult } from '@modelcontextprotocol/sdk/types.js';

const toolName = 'get_version';
const toolDescription = 'Gets the MCP Unity version of the Node server and of the Unity package, plus the Unity Editor version.';

const paramsSchema = z.object({});

export function registerGetVersionTool(server: McpServer, mcpUnity: McpUnity, logger: Logger, serverVersion: string) {
  logger.info(`Registering tool: ${toolName}`);

  server.tool(
    toolName,
    toolDescription,
    paramsSchema.shape,
    async (params: any) => {
      try {
        logger.info(`Executing tool: ${toolName}`, params);
        const result = await toolHandler(mcpUnity, serverVersion);
        logger.info(`Tool execution successful: ${toolName}`);
        return result;
      } catch (error) {
        logger.error(`Tool execution failed: ${toolName}`, error);
        throw error;
      }
    }
  );
}

async function toolHandler(mcpUnity: McpUnity, serverVersion: string): Promise<CallToolResult> {
  const lines = [`Node server: ${serverVersion}`];
  const data: Record<string, unknown> = { serverVersion };

  // The Node version is always reported, even when Unity cannot be reached
  try {
    const response = await mcpUnity.sendRequest(
      { method: toolName, params: {} },
      { queueIfDisconnected: false }
    );
    if (!response.success) {
      throw new Error(response.message || 'Unity did not report its version');
    }

    data.unityPackageVersion = response.version;
    data.unityVersion = response.unityVersion;
    for (const key of ['packageVersion', 'packageSource', 'gitRevision', 'gitHash']) {
      if (response[key] !== undefined) data[key] = response[key];
    }

    lines.push(`Unity package: ${response.version}`);
    lines.push(`Unity Editor: ${response.unityVersion}`);
    if (response.packageSource) {
      const git = response.gitHash ? ` @ ${response.gitRevision || ''} (${response.gitHash})` : '';
      lines.push(`Installed from: ${response.packageSource}${git}`);
    }
    if (response.version !== serverVersion) {
      data.versionMismatch = true;
      lines.push('Warning: the Node server and Unity package versions differ; rebuild the Node server from the installed package.');
    }
  } catch (error) {
    data.unityError = error instanceof Error ? error.message : String(error);
    lines.push(`Unity package: unavailable (${data.unityError})`);
  }

  return {
    content: [{ type: 'text', text: lines.join('\n') }],
    data
  };
}
