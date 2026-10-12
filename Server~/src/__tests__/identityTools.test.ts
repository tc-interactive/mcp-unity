import { jest, describe, it, expect, beforeEach } from '@jest/globals';
import { ErrorType, McpUnityError } from '../utils/errors.js';
import { registerGetVersionTool } from '../tools/getVersionTool.js';
import { registerWhoAmITool } from '../tools/whoAmITool.js';

const mockSendRequest = jest.fn<(...args: any[]) => Promise<any>>();
const mockMcpUnity = {
  sendRequest: mockSendRequest,
  endpoint: { host: 'localhost', port: 8090 }
};

const mockLogger = {
  info: jest.fn(),
  debug: jest.fn(),
  warn: jest.fn(),
  error: jest.fn()
};

const mockServerTool = jest.fn();
const mockServer = {
  tool: mockServerTool,
  server: {
    getClientVersion: () => ({ name: 'claude-code', version: '2.1.0' })
  }
};

function registeredHandler(): (params: any) => Promise<any> {
  return mockServerTool.mock.calls[0][3] as (params: any) => Promise<any>;
}

describe('Identity Tools', () => {
  beforeEach(() => {
    jest.clearAllMocks();
  });

  describe('get_version', () => {
    it('registers the tool with the MCP server', () => {
      registerGetVersionTool(mockServer as any, mockMcpUnity as any, mockLogger as any, '1.5.0-tci-abc1234');

      expect(mockServerTool).toHaveBeenCalledWith('get_version', expect.any(String), expect.any(Object), expect.any(Function));
    });

    it('reports the Node and Unity versions without queueing when disconnected', async () => {
      registerGetVersionTool(mockServer as any, mockMcpUnity as any, mockLogger as any, '1.5.0-tci-abc1234');
      mockSendRequest.mockResolvedValue({
        success: true,
        version: '1.5.0-tci-abc1234',
        unityVersion: '2022.3.40f1',
        packageVersion: '1.5.0-tci-abc1234',
        packageSource: 'Git',
        gitRevision: '1.5.0-tci-abc1234',
        gitHash: 'deadbeef'
      });

      const result = await registeredHandler()({});

      expect(mockSendRequest).toHaveBeenCalledWith(
        { method: 'get_version', params: {} },
        { queueIfDisconnected: false }
      );
      expect(result.data).toEqual({
        serverVersion: '1.5.0-tci-abc1234',
        unityPackageVersion: '1.5.0-tci-abc1234',
        unityVersion: '2022.3.40f1',
        packageVersion: '1.5.0-tci-abc1234',
        packageSource: 'Git',
        gitRevision: '1.5.0-tci-abc1234',
        gitHash: 'deadbeef'
      });
      expect(result.content[0].text).toContain('Node server: 1.5.0-tci-abc1234');
      expect(result.content[0].text).not.toContain('Warning');
    });

    it('flags a version mismatch between Node and Unity', async () => {
      registerGetVersionTool(mockServer as any, mockMcpUnity as any, mockLogger as any, '1.5.0');
      mockSendRequest.mockResolvedValue({ success: true, version: '1.5.0-tci-abc1234', unityVersion: '6000.0.1f1' });

      const result = await registeredHandler()({});

      expect(result.data.versionMismatch).toBe(true);
      expect(result.content[0].text).toContain('Warning');
    });

    it('still reports the Node version when Unity is unreachable', async () => {
      registerGetVersionTool(mockServer as any, mockMcpUnity as any, mockLogger as any, '1.5.0');
      mockSendRequest.mockRejectedValue(new McpUnityError(ErrorType.CONNECTION, 'Not connected to Unity'));

      const result = await registeredHandler()({});

      expect(result.data).toEqual({ serverVersion: '1.5.0', unityError: 'Not connected to Unity' });
      expect(result.content[0].text).toContain('Unity package: unavailable');
    });
  });

  describe('whoami', () => {
    it('registers the tool with the MCP server', () => {
      registerWhoAmITool(mockServer as any, mockMcpUnity as any, mockLogger as any);

      expect(mockServerTool).toHaveBeenCalledWith('whoami', expect.any(String), expect.any(Object), expect.any(Function));
    });

    it('combines the Unity connection with the MCP client identity', async () => {
      registerWhoAmITool(mockServer as any, mockMcpUnity as any, mockLogger as any);
      const connectedClients = [
        { connectionId: 'a1', clientName: 'claude-code', isCaller: true },
        { connectionId: 'b2', clientName: 'cursor', isCaller: false }
      ];
      mockSendRequest.mockResolvedValue({
        success: true,
        connectionId: 'a1',
        clientName: 'claude-code',
        remoteEndPoint: '127.0.0.1:54321',
        connectedClients
      });

      const result = await registeredHandler()({});

      expect(mockSendRequest).toHaveBeenCalledWith(
        { method: 'whoami', params: {} },
        { queueIfDisconnected: false }
      );
      expect(result.data).toEqual({
        connectionId: 'a1',
        clientName: 'claude-code',
        remoteEndPoint: '127.0.0.1:54321',
        mcpClient: { name: 'claude-code', version: '2.1.0' },
        unityEndpoint: { host: 'localhost', port: 8090 },
        processId: process.pid,
        connectedClients
      });
      expect(result.content[0].text).toContain('You are [claude-code] a1');
      expect(result.content[0].text).toContain('* [claude-code] a1');
      expect(result.content[0].text).toContain('- [cursor] b2');
    });

    it('throws a tool execution error when Unity reports failure', async () => {
      registerWhoAmITool(mockServer as any, mockMcpUnity as any, mockLogger as any);
      mockSendRequest.mockResolvedValue({ success: false, message: 'connection unknown' });

      await expect(registeredHandler()({})).rejects.toMatchObject({
        type: ErrorType.TOOL_EXECUTION,
        message: 'connection unknown'
      } as Partial<McpUnityError>);
    });
  });
});
