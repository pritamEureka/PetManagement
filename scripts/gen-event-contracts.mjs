// Regenerates backend/tests/Pawzaroo.Tests/contracts/integration-events.json from source,
// without needing a dotnet build. Mirrors the formatting used by EventContractTests.
// Usage: node scripts/gen-event-contracts.mjs
// (Alternatively: UPDATE_CONTRACTS=1 dotnet test --filter EventContractTests)
import { readFileSync, writeFileSync, readdirSync, existsSync } from 'node:fs';
import { join } from 'node:path';

const app = 'backend/src/Pawzaroo.Application';
const files = [join(app, 'Common/Messaging/IntegrationEvents.cs')];
for (const m of readdirSync(join(app, 'Modules'))) {
  const dir = join(app, 'Modules', m, 'Events');
  if (!existsSync(dir)) continue;
  for (const f of readdirSync(dir)) if (f.endsWith('.cs')) files.push(join(dir, f));
}

function splitTop(s) {
  const out = []; let depth = 0, cur = '';
  for (const ch of s) {
    if (ch === '<' || ch === '(') depth++;
    if (ch === '>' || ch === ')') depth--;
    if (ch === ',' && depth === 0) { out.push(cur.trim()); cur = ''; } else cur += ch;
  }
  if (cur.trim()) out.push(cur.trim());
  return out;
}

const result = {};
for (const file of files) {
  const text = readFileSync(file, 'utf8').replace(/\/\/.*$/gm, '');
  const ns = /namespace\s+([\w.]+)\s*;/.exec(text)[1];
  const re = /public\s+(?:sealed\s+)?record\s+(\w+)\s*\(([\s\S]*?)\)\s*(?::[^;{]*)?;/g;
  let m;
  while ((m = re.exec(text))) {
    const props = {};
    for (const p of splitTop(m[2])) {
      const withoutDefault = p.split('=')[0].trim();
      const idx = withoutDefault.lastIndexOf(' ');
      const type = withoutDefault.slice(0, idx).trim().replace(/\s+/g, '');
      props[withoutDefault.slice(idx + 1)] = type.replace(/,/g, ', ');
    }
    result[`${ns}.${m[1]}`] = Object.fromEntries(Object.entries(props).sort(([a], [b]) => a < b ? -1 : 1));
  }
}
const sorted = Object.fromEntries(Object.entries(result).sort(([a], [b]) => (a < b ? -1 : 1)));
writeFileSync('backend/tests/Pawzaroo.Tests/contracts/integration-events.json', JSON.stringify(sorted, null, 2) + '\n');
console.log('wrote', Object.keys(sorted).length, 'contracts');
