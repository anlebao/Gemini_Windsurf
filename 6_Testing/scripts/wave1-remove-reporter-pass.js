#!/usr/bin/env node
/**
 * Stream B - Wave 1: Remove decorative reporter.pass() calls.
 *
 * Pattern-based batch fix:
 *  - Removes every `reporter.pass(...)` statement (single + multi-line, brace-balanced).
 *  - Does NOT touch comments or other reporter methods (reporter.log, reporter.setArchitectDecision).
 *  - After removal, if `reporter` is no longer referenced anywhere in the file,
 *    removes the `import { TestReporter } from '../utils/test-reporter';` line
 *    and the `const reporter = new TestReporter(...);` line.
 *
 * Usage: node scripts/wave1-remove-reporter-pass.js <file1> [file2 ...]
 * Dry-run: set env DRY_RUN=1 to print changes without writing.
 */
const fs = require('fs');

function removeReporterPassCalls(src) {
  // Scan char-by-char, skip line comments and block comments,
  // and remove statements of the form `reporter.pass(...);`
  // (the call may span multiple lines and contain nested braces/parens).
  let out = '';
  let i = 0;
  const n = src.length;
  let removed = 0;

  while (i < n) {
    // Line comment
    if (src[i] === '/' && src[i + 1] === '/') {
      const end = src.indexOf('\n', i);
      const stop = end === -1 ? n : end;
      out += src.slice(i, stop);
      i = stop;
      continue;
    }
    // Block comment
    if (src[i] === '/' && src[i + 1] === '*') {
      const end = src.indexOf('*/', i + 2);
      const stop = end === -1 ? n : end + 2;
      out += src.slice(i, stop);
      i = stop;
      continue;
    }
    // String literal (single, double, backtick)
    if (src[i] === '"' || src[i] === "'" || src[i] === '`') {
      const quote = src[i];
      out += src[i++];
      while (i < n) {
        if (src[i] === '\\') {
          out += src[i++];
          if (i < n) out += src[i++];
          continue;
        }
        if (src[i] === quote) {
          out += src[i++];
          break;
        }
        out += src[i++];
      }
      continue;
    }
    // Match `reporter.pass(` as a statement start.
    // To avoid matching `something.reporter.pass(` or `.reporter.pass(` chains,
    // ensure the match is preceded by a non-identifier char (or start of line/whitespace).
    if (src.startsWith('reporter.pass(', i)) {
      const prev = out.length > 0 ? out[out.length - 1] : '\n';
      // Only treat as a statement if preceded by whitespace, newline, or start.
      // (Inside expressions we wouldn't typically see reporter.pass, but guard anyway.)
      if (/\s|\n/.test(prev) || out.length === 0) {
        // Find the matching closing paren for the call.
        let j = i + 'reporter.pass('.length; // position after `(`
        let depth = 1;
        while (j < n && depth > 0) {
          // skip strings inside the call args
          if (src[j] === '"' || src[j] === "'" || src[j] === '`') {
            const q = src[j];
            j++;
            while (j < n) {
              if (src[j] === '\\') { j += 2; continue; }
              if (src[j] === q) { j++; break; }
              j++;
            }
            continue;
          }
          if (src[j] === '(') depth++;
          else if (src[j] === ')') depth--;
          if (depth === 0) break;
          j++;
        }
        // j now points at the closing `)`. The statement ends at `);`
        if (j < n && src[j] === ')') {
          // consume `)` and trailing `;`
          let k = j + 1;
          // skip whitespace between `)` and `;`
          while (k < n && /\s/.test(src[k]) && src[k] !== '\n') k++;
          if (src[k] === ';') {
            k++;
            // Also consume the trailing newline if the line is now empty.
            // Skip the rest of the line's whitespace + newline.
            let nl = k;
            while (nl < n && (src[nl] === ' ' || src[nl] === '\t')) nl++;
            if (src[nl] === '\r') nl++;
            if (src[nl] === '\n') {
              // Only swallow the newline if nothing else was on the line before `reporter.pass`.
              // Find start of the original line in `out`.
              let lineStart = out.length - 1;
              while (lineStart >= 0 && out[lineStart] !== '\n') lineStart--;
              const linePrefix = out.slice(lineStart + 1);
              if (linePrefix.trim() === '') {
                // The whole line was just this statement — drop the now-empty line.
                out = out.slice(0, lineStart + 1);
                i = nl + 1;
                removed++;
                continue;
              }
            }
            // Otherwise just drop the statement, keep the newline.
            i = k;
            removed++;
            continue;
          }
        }
      }
    }
    out += src[i++];
  }
  return { src: out, removed };
}

function removeUnusedReporterDecl(src) {
  // If `reporter` is still referenced (other than the declaration itself),
  // keep everything. Otherwise remove the import + const declaration lines.
  // We detect "still referenced" by removing the declaration lines temporarily
  // and searching for `reporter.` in the remainder.

  // Find the TestReporter import line.
  const importRe = /^import\s*\{[^}]*\bTestReporter\b[^}]*\}\s*from\s*['"][^'"]*test-reporter['"];\s*$/m;
  const importMatch = src.match(importRe);
  // Find the `const reporter = new TestReporter(...);` line (may span multiple lines).
  const declRe = /const\s+reporter\s*=\s*new\s+TestReporter\([^)]*\)\s*;/;
  const declMatch = src.match(declRe);

  if (!importMatch && !declMatch) return { src, removedImport: false, removedDecl: false };

  // Build a version without the import + decl, then check for remaining `reporter` usage.
  let without = src;
  if (importMatch) without = without.replace(importMatch[0], '');
  if (declMatch) without = without.replace(declMatch[0], '');

  // If `reporter` still appears as a word in `without`, keep both.
  const stillUsed = /\breporter\b/.test(without);

  let removedImport = false, removedDecl = false;
  if (!stillUsed) {
    if (importMatch) {
      src = src.replace(importMatch[0], '');
      removedImport = true;
    }
    if (declMatch) {
      src = src.replace(declMatch[0], '');
      removedDecl = true;
    }
    // Clean up: collapse 3+ consecutive blank lines into 2.
    src = src.replace(/\n{3,}/g, '\n\n');
  }
  return { src, removedImport, removedDecl };
}

function main() {
  const files = process.argv.slice(2);
  if (files.length === 0) {
    console.error('Usage: node wave1-remove-reporter-pass.js <file1> [file2 ...]');
    process.exit(1);
  }
  const dry = !!process.env.DRY_RUN;
  let totalRemoved = 0;
  let totalImportRemoved = 0;
  let totalDeclRemoved = 0;
  for (const f of files) {
    const src = fs.readFileSync(f, 'utf8');
    const r1 = removeReporterPassCalls(src);
    const r2 = removeUnusedReporterDecl(r1.src);
    totalRemoved += r1.removed;
    if (r2.removedImport) totalImportRemoved++;
    if (r2.removedDecl) totalDeclRemoved++;
    const before = src.length, after = r2.src.length;
    if (dry) {
      console.log(`[DRY] ${f}: removed ${r1.removed} pass-calls, import=${r2.removedImport}, decl=${r2.removedDecl}, ${before}->${after}`);
    } else {
      fs.writeFileSync(f, r2.src, 'utf8');
      console.log(`[OK]  ${f}: removed ${r1.removed} pass-calls, import=${r2.removedImport}, decl=${r2.removedDecl}, ${before}->${after}`);
    }
  }
  console.log(`\nSummary: ${totalRemoved} reporter.pass() calls removed, ${totalImportRemoved} imports removed, ${totalDeclRemoved} decls removed${dry ? ' (DRY RUN)' : ''}`);
}

main();
