import { describe, expect, it } from '@jest/globals';
import { resolveClientName, UNKNOWN_CLIENT_NAME } from '../utils/clientName.js';

describe('resolveClientName', () => {
  it('prefers MCP_UNITY_CLIENT_NAME over the name reported by the MCP client', () => {
    expect(resolveClientName({ MCP_UNITY_CLIENT_NAME: ' claude-worktree-2 ' }, 'claude-code')).toBe('claude-worktree-2');
  });

  it('uses the name reported by the MCP client when no name is configured', () => {
    expect(resolveClientName({}, 'codex-mcp-client')).toBe('codex-mcp-client');
  });

  it('ignores a blank configured name', () => {
    expect(resolveClientName({ MCP_UNITY_CLIENT_NAME: '  ' }, 'claude-code')).toBe('claude-code');
  });

  it('falls back to the unknown client name', () => {
    expect(resolveClientName({}, undefined)).toBe(UNKNOWN_CLIENT_NAME);
  });
});
