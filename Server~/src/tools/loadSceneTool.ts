import { McpServer } from '@modelcontextprotocol/sdk/server/mcp.js';
import { McpUnity } from '../unity/mcpUnity.js';
import { McpUnityError, ErrorType } from '../utils/errors.js';
import * as z from 'zod';
import { Logger } from '../utils/logger.js';

const toolName = 'load_scene';
const toolDescription =
  'Loads a scene by path or name. Supports additive loading (default: false). ' +
  'Non-additive loading replaces the open scenes: saved scenes with unsaved changes are saved first, and if an ' +
  'untitled scene has unsaved changes (e.g. a temporary scene left by run_tests) the load is refused with an ' +
  'unsaved_changes_error instead of showing a blocking "Save Scene" dialog. Retry with discardUnsavedChanges: true ' +
  'to throw such untitled scenes away.';

const paramsSchema = z.object({
  scenePath: z.string().optional().describe("Full asset path to the scene (e.g., 'Assets/Scenes/MyScene.unity')"),
  sceneName: z.string().optional().describe('Scene name without extension (used if scenePath not provided)'),
  folderPath: z.string().optional().describe("Optional folder scope to resolve sceneName under 'Assets'"),
  additive: z.boolean().optional().describe('Load additively if true; default false'),
  discardUnsavedChanges: z.boolean().optional().describe(
    'If true, untitled scenes with unsaved changes are replaced WITHOUT saving (ignored when additive). ' +
    'Use ONLY for throwaway scenes: temporary/untitled scenes left by run_tests or scenes created just for testing. ' +
    'Only untitled scenes are discarded: if a saved scene (one with an asset path) has unsaved changes, the load ' +
    'is refused with an error and nothing is lost; save it with save_scene first or leave this false. ' +
    'Default false: saved scenes with unsaved changes are saved first, and untitled scenes with unsaved changes ' +
    'refuse the load (unsaved_changes_error) so no blocking Save dialog is ever shown.'
  )
});

export function registerLoadSceneTool(server: McpServer, mcpUnity: McpUnity, logger: Logger) {
  logger.info(`Registering tool: ${toolName}`);

  server.tool(
    toolName,
    toolDescription,
    paramsSchema.shape,
    async (params: any) => {
      try {
        logger.info(`Executing tool: ${toolName}`, params);
        const result = await toolHandler(mcpUnity, params);
        logger.info(`Tool execution successful: ${toolName}`);
        return result;
      } catch (error) {
        logger.error(`Tool execution failed: ${toolName}`, error);
        throw error;
      }
    }
  );
}

async function toolHandler(mcpUnity: McpUnity, params: any) {
  if (!params.scenePath && !params.sceneName) {
    throw new McpUnityError(
      ErrorType.VALIDATION,
      "Either 'scenePath' or 'sceneName' must be provided"
    );
  }

  const response = await mcpUnity.sendRequest({
    method: toolName,
    params
  });

  if (!response.success) {
    throw new McpUnityError(
      ErrorType.TOOL_EXECUTION,
      response.message || 'Failed to load scene'
    );
  }

  return {
    content: [{
      type: response.type,
      text: response.message || 'Successfully loaded scene'
    }],
    data: {
      scenePath: response.scenePath,
      additive: response.additive,
      discardedScenes: response.discardedScenes,
      savedScenes: response.savedScenes
    }
  };
}


