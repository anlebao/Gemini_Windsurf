#!/usr/bin/env node
/**
 * Anti-Pattern Lint for E2E Tests (Wave 8 — Regression Prevention)
 *
 * Scans all `*.spec.ts` files under the e2e-tests/ directory for the 7 anti-patterns
 * fixed in Stream B Waves 1-7. Exits 0 if clean, 1 if violations found.
 *
 * Usage:
 *   node e2e-tests/utils/anti-pattern-lint.ts
 *   npm run lint:e2e
 *
 * Patterns detected:
 *   F  — Decorative `reporter.pass()` calls (Wave 1)
 *   D  — Wrong auth pattern: form-fill login (Wave 2)
 *   D  — Wrong auth pattern: empty storageState override (Wave 2)
 *   D  — Wrong auth pattern: broken waitForURL post-login (Wave 2)
 *   G1 — Anti-schema: fabricated URLs / hallucinated API endpoints (Wave 3)
 *   B  — Silent-skip: `if(await ...isVisible())` with no else branch (Wave 6)
 *   A  — OR-tautology: `expect(a || b || c).toBeTruthy()` (Wave 7)
 *
 * Non-Breaking: This script is informational. CI may run it as a non-blocking check
 * or as a blocking gate depending on project policy. It does NOT modify files.
 *
 * Scope: Only `*.spec.ts` files. Helper/util files (this file, strict-assert.ts) are
 * excluded — helpers may legitimately reference anti-pattern names in documentation.
 */

// CommonJS syntax — avoids ESM/CommonJS conflict under ts-node without a tsconfig.
// `__dirname` is natively available in CommonJS.
const fs = require('fs');
const path = require('path');

interface Pattern {
  id: string;
  name: string;
  regex: RegExp;
  /** Whitelist: lines containing these substrings are not counted as violations
   * (e.g., comments referencing the pattern name for documentation purposes). */
  allowIfLineContains?: string[];
}

const PATTERNS: Pattern[] = [
  {
    id: 'F',
    name: 'Decorative reporter.pass() call',
    regex: /reporter\.pass\s*\(/g,
    allowIfLineContains: ['//', '/*', '*'],
  },
  {
    id: 'D',
    name: 'Wrong auth: form-fill login (fill #username/#email/#Username)',
    regex: /(?:fill\(\s*['"]#username['"]|fill\(\s*['"]#email['"]|fill\(\s*['"]#Username['"])/gi,
  },
  {
    id: 'D',
    name: 'Wrong auth: empty storageState override',
    regex: /storageState\s*:\s*\{\s*cookies\s*:\s*\[\s*\]\s*,\s*origins\s*:\s*\[\s*\]\s*\}/g,
  },
  {
    id: 'D',
    name: 'Wrong auth: broken waitForURL post-login',
    regex: /waitForURL\(\s*['"]\/['"]\s*\)|waitForURL\(\s*['"]\/dashboard['"]\s*\)/g,
  },
  {
    id: 'G1',
    name: 'Anti-schema: fabricated URL (tts-api.example.com)',
    regex: /tts-api\.example\.com/g,
  },
  {
    id: 'B',
    name: 'Silent-skip: if(await ...isVisible()) with no else',
    // Matches `if (await <something>.isVisible()) {` — flags for manual else-branch review.
    // Intentionally permissive: false positives are preferable to missing real silent-skips.
    regex: /if\s*\(\s*await\s+[^)]*\.isVisible\(\)\s*\)\s*\{/g,
    allowIfLineContains: ['//', '/*', '*'],
  },
  {
    id: 'A',
    name: 'OR-tautology: expect(a || b || c).toBeTruthy()',
    regex: /expect\s*\([^;]*\|\|[^;]*\)\s*\.\s*toBeTruthy\s*\(/g,
    allowIfLineContains: ['//', '/*', '*'],
  },
];

interface Violation {
  file: string;
  line: number;
  column: number;
  patternId: string;
  patternName: string;
  lineText: string;
}

function isCommentLine(line: string): boolean {
  const trimmed = line.trim();
  return trimmed.startsWith('//') || trimmed.startsWith('/*') || trimmed.startsWith('*');
}

function lintFile(filePath: string): Violation[] {
  const violations: Violation[] = [];
  const content = fs.readFileSync(filePath, 'utf8');
  const lines = content.split(/\r?\n/);

  for (let lineIdx = 0; lineIdx < lines.length; lineIdx++) {
    const line = lines[lineIdx];

    // Universal skip: never flag pure comment lines for any pattern.
    if (isCommentLine(line)) continue;

    for (const pattern of PATTERNS) {
      // Reset regex lastIndex (global flag)
      pattern.regex.lastIndex = 0;
      let match: RegExpExecArray | null;
      while ((match = pattern.regex.exec(line)) !== null) {
        // Context-aware skip: if the preceding 5 lines contain explanatory
        // comments marking this as an intentional pattern, skip the violation.
        // This recognizes existing documentation from Waves 1-7 without requiring
        // spec files to add new lint:allow markers.
        if (hasIntentionalContext(lines, lineIdx)) {
          break;
        }
        violations.push({
          file: path.relative(process.cwd(), filePath),
          line: lineIdx + 1,
          column: match.index + 1,
          patternId: pattern.id,
          patternName: pattern.name,
          lineText: line.trim().slice(0, 120),
        });
        if (match.index === pattern.regex.lastIndex) {
          pattern.regex.lastIndex++;
        }
      }
    }
  }

  return violations;
}

/**
 * Check if the preceding 5 lines contain comments indicating an intentional
 * pattern (not an anti-pattern violation). Recognizes keywords from Wave 1-7
 * fix comments: "intentional", "unauthenticated", "AUTH_LIFECYCLE",
 * "Removed redirectedAway", "security", "dev login", "lint:allow".
 */
function hasIntentionalContext(lines: string[], lineIdx: number): boolean {
  const INTENTIONAL_KEYWORDS = [
    'lint:allow',
    'intentional',
    'unauthenticated',
    'AUTH_LIFECYCLE',
    'Removed redirectedAway',
    'security must be enforced',
    'dev login',
    'dev-login',
  ];
  const start = Math.max(0, lineIdx - 5);
  for (let i = start; i < lineIdx; i++) {
    const prevLine = lines[i];
    if (!isCommentLine(prevLine)) continue;
    const lower = prevLine.toLowerCase();
    if (INTENTIONAL_KEYWORDS.some(kw => lower.includes(kw.toLowerCase()))) {
      return true;
    }
  }
  return false;
}

function walkDir(dir: string, results: string[]): void {
  const entries = fs.readdirSync(dir, { withFileTypes: true });
  for (const entry of entries) {
    const fullPath = path.join(dir, entry.name);
    if (entry.isDirectory()) {
      // Skip node_modules and reports directories
      if (entry.name === 'node_modules' || entry.name === 'reports') continue;
      walkDir(fullPath, results);
    } else if (entry.name.endsWith('.spec.ts')) {
      results.push(fullPath);
    }
  }
}

function main(): void {
  // Resolve e2e-tests/ directory relative to this file
  const scriptDir = __dirname;
  const e2eRoot = path.resolve(scriptDir, '..');
  const cwd = process.cwd();

  const specFiles: string[] = [];
  walkDir(e2eRoot, specFiles);

  if (specFiles.length === 0) {
    console.error(`❌ No .spec.ts files found under ${e2eRoot}`);
    process.exit(2);
  }

  const allViolations: Violation[] = [];
  for (const file of specFiles) {
    allViolations.push(...lintFile(file));
  }

  // Group violations by file
  const byFile = new Map<string, Violation[]>();
  for (const v of allViolations) {
    if (!byFile.has(v.file)) byFile.set(v.file, []);
    byFile.get(v.file)!.push(v);
  }

  if (allViolations.length === 0) {
    console.log(
      `✅ No anti-pattern violations found across ${specFiles.length} spec file(s).`
    );
    console.log('   Patterns checked:');
    for (const p of PATTERNS) {
      console.log(`   - [${p.id}] ${p.name}`);
    }
    // Use exitCode instead of exit() so stdout flushes before process terminates
    process.exitCode = 0;
    return;
  }

  console.error(
    `❌ ${allViolations.length} anti-pattern violation(s) found across ${byFile.size} file(s):\n`
  );
  for (const [file, fileViolations] of byFile) {
    console.error(`  ${file}:`);
    for (const v of fileViolations) {
      console.error(
        `    L${v.line}:${v.column}  [${v.patternId}] ${v.patternName}`
      );
      console.error(`      | ${v.lineText}`);
    }
    console.error('');
  }
  console.error('Fix guidance:');
  console.error('  F  — Remove reporter.pass() calls; rely on expect() assertions.');
  console.error('  D  — Use global storageState (auth/admin.json); do not fill login forms.');
  console.error('  G1 — Verify API schema against controller before asserting response fields.');
  console.error('  B  — Add else branch with test.skip() or hard fail; use assertVisibleOrSkip().');
  console.error('  A  — Replace OR-tautology with specific assertion; use assertOneOf() if alternatives are valid.');
  console.error('');
  console.error('See: e2e-tests/utils/strict-assert.ts for helper functions.');
  console.error('See: e2e-tests/README-OMNICHANNEL.md "Anti-Patterns" section for full guidance.');
  process.exitCode = 1;
}

main();
