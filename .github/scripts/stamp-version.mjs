// Stamps a release version into every place that reports the package version.
//
// Usage: node .github/scripts/stamp-version.mjs <version>
//
// Each target is replaced in place with a targeted regex so formatting and line endings are kept.
// The script fails if any pattern does not match exactly the expected number of times, so a moved
// or renamed version string breaks the release instead of silently shipping a stale version.
import { readFileSync, writeFileSync } from 'node:fs';
import { join, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';

const version = process.argv[2];
if (!version || !/^[0-9A-Za-z.+-]+$/.test(version)) {
  console.error(`Invalid version: ${version ?? '(missing)'}`);
  process.exit(1);
}

const root = join(dirname(fileURLToPath(import.meta.url)), '..', '..');

const targets = [
  // Unity package manifest (shown by the Package Manager)
  { file: 'package.json', pattern: /("version": ")[^"]+(")/, count: 1 },
  { file: 'Server~/package.json', pattern: /("version": ")[^"]+(")/, count: 1 },
  // Root project entries only; dependency versions further down are left alone
  { file: 'Server~/package-lock.json', pattern: /^(  "name": "mcp-unity-server",\r?\n  "version": ")[^"]+(")/m, count: 1 },
  { file: 'Server~/package-lock.json', pattern: /^(      "name": "mcp-unity-server",\r?\n      "version": ")[^"]+(")/m, count: 1 },
  // MCP server metadata reported to clients
  { file: 'Server~/src/index.ts', pattern: /^(\s*version: ")[^"]+(")/m, count: 1 },
  { file: 'Server~/src/ui/unity-dashboard.html', pattern: /(appInfo: \{ name: 'unity-dashboard', version: ')[^']+(')/, count: 1 },
  // Version shown in the Unity Server Window
  { file: 'Editor/UnityBridge/McpUnitySettings.cs', pattern: /(ServerVersion = ")[^"]+(")/, count: 1 },
];

let failed = false;
for (const { file, pattern, count } of targets) {
  const path = join(root, file);
  const source = readFileSync(path, 'utf8');
  const global = new RegExp(pattern.source, pattern.flags.includes('g') ? pattern.flags : pattern.flags + 'g');
  const matches = source.match(global)?.length ?? 0;
  if (matches !== count) {
    console.error(`${file}: expected ${count} match(es) for ${pattern}, found ${matches}`);
    failed = true;
    continue;
  }
  writeFileSync(path, source.replace(global, (_, prefix, suffix) => `${prefix}${version}${suffix}`));
  console.log(`${file}: stamped ${version}`);
}

process.exit(failed ? 1 : 0);
