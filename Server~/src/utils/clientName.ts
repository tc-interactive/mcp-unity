export const CLIENT_NAME_ENV = 'MCP_UNITY_CLIENT_NAME';
export const UNKNOWN_CLIENT_NAME = 'Unknown MCP Client';

/**
 * Resolves the name shown for this bridge in Unity's connected clients list.
 * MCP_UNITY_CLIENT_NAME (set per client config) wins, so several instances of the same agent
 * can be told apart; otherwise the name the MCP client reported in its initialize request is used.
 */
export function resolveClientName(environment: NodeJS.ProcessEnv, reportedName?: string): string {
  const configuredName = environment[CLIENT_NAME_ENV]?.trim();
  if (configuredName) {
    return configuredName;
  }

  return reportedName?.trim() || UNKNOWN_CLIENT_NAME;
}
