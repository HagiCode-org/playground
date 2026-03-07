#!/usr/bin/env node
import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';

const rootDir = path.resolve(path.dirname(new URL(import.meta.url).pathname));
const fixturesDir = path.join(rootDir, 'fixtures');

function normalizeStatus(rawStatus, isError = false) {
  if (typeof rawStatus === 'string') {
    const status = rawStatus.trim().toLowerCase();
    if (status === 'running' || status === 'in_progress' || status === 'started') return 'running';
    if (status === 'completed' || status === 'success' || status === 'done') return 'completed';
    if (status === 'failed' || status === 'error' || status === 'timeout') return 'failed';
  }
  return isError ? 'failed' : 'running';
}

function summarizeText(value, max = 120) {
  if (!value) return '';
  const text = String(value).replace(/\r\n/g, '\n');
  return text.length > max ? `${text.slice(0, max)}...` : text;
}

function resolveToolCallId(rawId, turnNumber, toolName, fingerprint, fallbackMap) {
  if (rawId && String(rawId).trim().length > 0) {
    return rawId;
  }

  const key = `${turnNumber}:${toolName}:${fingerprint}`;
  if (fallbackMap.has(key)) {
    return fallbackMap.get(key);
  }

  const hash = crypto.createHash('sha256').update(key).digest('hex').slice(0, 12);
  const fallback = `codex-turn-${turnNumber}-${hash}`;
  fallbackMap.set(key, fallback);
  return fallback;
}

function buildToolEvent(itemEventType, item, turnNumber, fallbackMap) {
  if (!item || item.type !== 'command_execution') {
    return null;
  }

  const fallbackStatus = itemEventType === 'item.completed' ? 'completed' : 'running';
  const status = normalizeStatus(item.status, false) || fallbackStatus;
  const toolCallId = resolveToolCallId(item.id, turnNumber, 'command_execution', item.command || '', fallbackMap);
  const isFailed = status === 'failed';

  return {
    toolCallId,
    toolName: 'command_execution',
    status,
    argumentSummary: summarizeText(JSON.stringify({command: item.command || ''})),
    resultSummary: status === 'completed' ? summarizeText(item.aggregated_output || '') : '',
    errorSummary: isFailed
      ? summarizeText(item.aggregated_output || `exit code ${item.exit_code ?? 'unknown'}`)
      : '',
  };
}

function normalizeEvents(events) {
  const normalized = [];
  const fallbackMap = new Map();
  const activeTools = new Map();
  let turnNumber = 0;

  for (const event of events) {
    if (event.type === 'turn.started') {
      turnNumber += 1;
      continue;
    }

    if (event.type === 'item.started' || event.type === 'item.updated' || event.type === 'item.completed') {
      const normalizedEvent = buildToolEvent(event.type, event.item, turnNumber, fallbackMap);
      if (!normalizedEvent) continue;

      if (normalizedEvent.status === 'running') {
        activeTools.set(normalizedEvent.toolCallId, normalizedEvent);
      } else {
        activeTools.delete(normalizedEvent.toolCallId);
      }

      normalized.push(normalizedEvent);
      continue;
    }

    if (event.type === 'turn.failed') {
      for (const [toolCallId, runningTool] of activeTools.entries()) {
        normalized.push({
          ...runningTool,
          toolCallId,
          status: 'failed',
          resultSummary: '',
          errorSummary: summarizeText(event.error?.message || 'turn.failed'),
        });
      }
      activeTools.clear();
    }
  }

  return normalized;
}

function assertScenario(scenario) {
  const normalized = normalizeEvents(scenario.events || []);
  const terminal = normalized[normalized.length - 1];
  const statuses = normalized.map(e => e.status);
  const expected = scenario.expected || {};

  const errors = [];
  if (expected.statuses) {
    const statusText = JSON.stringify(statuses);
    const expectedText = JSON.stringify(expected.statuses);
    if (statusText !== expectedText) {
      errors.push(`status mismatch: expected=${expectedText}, actual=${statusText}`);
    }
  }

  if (expected.terminal && terminal?.status !== expected.terminal) {
    errors.push(`terminal mismatch: expected=${expected.terminal}, actual=${terminal?.status ?? '(none)'}`);
  }

  if (expected.toolCallId && normalized[0]?.toolCallId !== expected.toolCallId) {
    errors.push(`toolCallId mismatch: expected=${expected.toolCallId}, actual=${normalized[0]?.toolCallId ?? '(none)'}`);
  }

  if (expected.toolCallIdPrefix) {
    const firstId = normalized[0]?.toolCallId || '';
    if (!firstId.startsWith(expected.toolCallIdPrefix)) {
      errors.push(`toolCallId prefix mismatch: expectedPrefix=${expected.toolCallIdPrefix}, actual=${firstId}`);
    }
  }

  if (expected.resultSummaryContains != null && terminal) {
    if (!terminal.resultSummary.includes(expected.resultSummaryContains)) {
      errors.push(
        `result summary mismatch: expected to contain '${expected.resultSummaryContains}', actual='${terminal.resultSummary}'`
      );
    }
  }

  if (expected.errorSummaryContains && terminal) {
    if (!terminal.errorSummary.toLowerCase().includes(String(expected.errorSummaryContains).toLowerCase())) {
      errors.push(
        `error summary mismatch: expected to contain '${expected.errorSummaryContains}', actual='${terminal.errorSummary}'`
      );
    }
  }

  return {
    name: scenario.name || 'unknown',
    ok: errors.length === 0,
    errors,
    normalized,
  };
}

function main() {
  const fixtureFiles = fs.readdirSync(fixturesDir)
    .filter(file => file.endsWith('.json'))
    .sort((a, b) => a.localeCompare(b));

  const results = fixtureFiles.map((file) => {
    const content = fs.readFileSync(path.join(fixturesDir, file), 'utf8');
    const scenario = JSON.parse(content);
    return assertScenario(scenario);
  });

  console.log('=== Codex Tool Event Adapter Validation ===');
  for (const result of results) {
    if (result.ok) {
      console.log(`✓ ${result.name}`);
    } else {
      console.log(`✗ ${result.name}`);
      for (const error of result.errors) {
        console.log(`  - ${error}`);
      }
    }

    result.normalized.forEach((event, index) => {
      console.log(
        `  [${index + 1}] ${event.toolCallId} ${event.status} | args=${event.argumentSummary || '(none)'} | result=${event.resultSummary || '(none)'} | error=${event.errorSummary || '(none)'}`
      );
    });
  }

  const failed = results.filter(r => !r.ok);
  if (failed.length > 0) {
    process.exitCode = 1;
    return;
  }

  console.log('\nAll scenarios passed.');
}

main();
