import { renderForm, showHistoryBanner, hideHistoryBanner } from './state.js';
import { renderCalendar } from './calendar.js';

// ─── API Schema ───────────────────────────────────────────────────────────────

let schemaJson = null;

function extractGenerateSchema(full) {
  const generatePath = full?.paths?.['/schedule/generate'];
  if (!generatePath) return full;

  const usedRefs = new Set();

  function collectRefs(node) {
    if (!node || typeof node !== 'object') return;
    if (Array.isArray(node)) { node.forEach(collectRefs); return; }
    for (const [k, v] of Object.entries(node)) {
      if (k === '$ref' && typeof v === 'string') {
        const name = v.replace('#/components/schemas/', '');
        if (!usedRefs.has(name)) {
          usedRefs.add(name);
          collectRefs(full?.components?.schemas?.[name]);
        }
      } else {
        collectRefs(v);
      }
    }
  }

  collectRefs(generatePath);

  const filteredSchemas = {};
  for (const name of usedRefs) {
    if (full?.components?.schemas?.[name]) {
      filteredSchemas[name] = full.components.schemas[name];
    }
  }

  return {
    openapi: full.openapi,
    info:    full.info,
    paths:   { '/schedule/generate': generatePath },
    components: { schemas: filteredSchemas },
  };
}

export async function fetchSchema() {
  const pre = document.getElementById('schema-pre');
  try {
    const res = await fetch('/openapi/v1.json');
    if (!res.ok) throw new Error(`HTTP ${res.status}`);
    schemaJson = extractGenerateSchema(await res.json());
    pre.textContent = JSON.stringify(schemaJson, null, 2);
  } catch (err) {
    pre.textContent = `Failed to load schema: ${err.message}`;
  }
}

function buildLlmPrompt() {
  const lines = [
    'You are a JSON generation assistant for a Schedule Optimizer API.',
    '',
    'Your task is to generate a valid JSON request body for the POST /schedule/generate endpoint.',
    'The complete OpenAPI schema for this endpoint is provided at the end of this prompt.',
    '',
    '## Validation Rules',
    '',
    '### planningHorizon',
    '- startDate must be strictly before endDate',
    '- Format: "YYYY-MM-DD"',
    '',
    '### fixedTasks',
    '- priority: integer 1–5',
    '- difficulty: integer 1–10',
    '- startTime must be strictly before endTime',
    '- Both startTime and endTime must fall within planningHorizon (startDate..endDate)',
    '- startTime and endTime must be on the same calendar day',
    '- Fixed tasks must not overlap each other',
    '- Format: "YYYY-MM-DDTHH:MM:SS"',
    '',
    '### dynamicTasks',
    '- priority: integer 1–5',
    '- difficulty: integer 1–10',
    '- duration: integer > 0 (minutes)',
    '- windowStart and windowEnd: if both present, windowStart < windowEnd; format "HH:MM:SS"',
    '- deadline: if present, must be within planningHorizon; format "YYYY-MM-DDTHH:MM:SS"',
    '- repeating counts (minDayCount, optDayCount, minWeekCount, optWeekCount): each >= 0',
    '',
    '### categoryWindows',
    '- startDateTime must be strictly before endDateTime',
    '- Format: "YYYY-MM-DDTHH:MM:SS"',
    '',
    '### difficultyCapacities',
    '- capacity: integer >= 0',
    '- date: format "YYYY-MM-DD"',
    '',
    '### taskTypePreferences',
    '- weight: number >= 0',
    '- date: format "YYYY-MM-DD"',
    '',
    '## Enum Values',
    '',
    '### TaskType — used in the "types" field of fixedTasks, dynamicTasks, and taskTypePreferences',
    'ONLY these exact string values are valid for "types" and "type" (taskTypePreferences):',
    'Physical, Intellectual, Creative, Social, Routine, DeepWork, Outdoor, Indoor, Digital, Fun, Boring, Collaborative, Solo, HighEnergy, LowEnergy, Meditative',
    '',
    '### Category — used in the "categories" field of dynamicTasks and the "category" field of categoryWindows',
    'ONLY these exact string values are valid for "categories" and "category":',
    'Work, Study, Home, Health, Social, FreeTime, Transport, Morning, Evening, Weekend',
    '',
    'IMPORTANT: TaskType and Category are separate enums. "Work", "Health", "Home" etc. are Categories — do NOT put them in "types". "Physical", "DeepWork" etc. are TaskTypes — do NOT put them in "categories".',
    '',
    '### difficultTaskSchedulingStrategy',
    '"Cluster" — group hard tasks together in the schedule',
    '"Even"    — spread hard tasks evenly across available time slots',
    '',
    '## Date / Time Format Summary',
    '- Date only:   "YYYY-MM-DD"',
    '- Time only:   "HH:MM:SS"',
    '- Date + time: "YYYY-MM-DDTHH:MM:SS"',
    '',
    '## Scheduling Domain Knowledge',
    '',
    '### Dynamic task placement',
    'Dynamic tasks are scheduled ONLY within categoryWindows that match their "categories" list.',
    'If a dynamic task has categories: ["Work", "Morning"], the optimizer will only place it inside',
    'time windows defined for the "Work" or "Morning" category.',
    'Therefore: every category referenced in a dynamic task\'s "categories" must have at least one',
    'corresponding categoryWindow entry, otherwise the task cannot be scheduled.',
    '',
    '### Multiple windows per category',
    'A single category can (and often should) have multiple categoryWindow entries:',
    '- Several windows on the same day (e.g. two "Work" blocks: 09:00-12:00 and 14:00-17:00)',
    '- Windows on different days (repeat the category entry for each day it applies)',
    'There is no restriction on the number of windows per category.',
    '',
    '## Instructions',
    'Generate realistic, diverse test data. Return ONLY the raw JSON object — no explanation, no markdown code fences.',
    'Note that minDays (minimum count of tasks in day) and minWeeks (minimum count of tasks in week) in repeating tasks mean that the task is required to be included minimum this amount of times. If there will be too much such tasks, the schedule will consist only of such tasks, so do not put too many repeats.',
    '',
    '## OpenAPI Schema',
    schemaJson !== null ? JSON.stringify(schemaJson, null, 2) : '(schema not loaded)',
  ];
  return lines.join('\n');
}

// ─── Clipboard helpers ────────────────────────────────────────────────────────

function showCopiedFeedback(btn) {
  const original = btn.textContent;
  btn.textContent = 'Copied!';
  btn.disabled = true;
  setTimeout(() => {
    btn.textContent = original;
    btn.disabled = false;
  }, 1500);
}

export async function copySchemaToClipboard(e) {
  e.stopPropagation();
  if (schemaJson === null) return;
  try {
    await navigator.clipboard.writeText(JSON.stringify(schemaJson, null, 2));
    showCopiedFeedback(document.getElementById('copy-schema-btn'));
  } catch (err) {
    console.error('Copy schema failed:', err);
  }
}

export async function copyPromptToClipboard(e) {
  e.stopPropagation();
  try {
    await navigator.clipboard.writeText(buildLlmPrompt());
    showCopiedFeedback(document.getElementById('copy-prompt-btn'));
  } catch (err) {
    console.error('Copy prompt failed:', err);
  }
}

// ─── Submit ───────────────────────────────────────────────────────────────────

function showResponse(message, ok) {
  const el = document.getElementById('response-output');
  el.textContent = message;
  el.className = ok ? 'response-ok' : 'response-error';
}

export async function submit() {
  const output = document.getElementById('response-output');
  output.textContent = '';
  output.className = '';

  const invalid = document.querySelector('.form-panel :invalid');
  if (invalid) {
    invalid.scrollIntoView({ behavior: 'smooth', block: 'center' });
    invalid.reportValidity();
    return;
  }

  let payload;
  try {
    payload = JSON.parse(document.getElementById('json-preview').value);
  } catch {
    showResponse('Invalid JSON in preview pane.', false);
    return;
  }

  try {
    const res = await fetch('/schedule/generate', {
      method:  'POST',
      headers: { 'Content-Type': 'application/json' },
      body:    JSON.stringify(payload),
    });

    const text = await res.text();
    if (res.ok) {
      showResponse(`Schedule generated. ID: ${text.replace(/"/g, '')}`, true);
      hideHistoryBanner();
      document.querySelectorAll('#generated-ids-list li.ids-active')
        .forEach(li => li.classList.remove('ids-active'));
      await loadGeneratedIds();
      const firstItem = document.querySelector('#generated-ids-list li:first-child');
      if (firstItem && !firstItem.classList.contains('ids-empty')) {
        firstItem.classList.add('ids-new');
        firstItem.addEventListener('animationend', () => firstItem.classList.remove('ids-new'), { once: true });
      }
    } else {
      showResponse(`Error: ${text}`, false);
    }
  } catch (err) {
    showResponse(`Request failed: ${err.message}`, false);
  }
}

// ─── Generated IDs panel ─────────────────────────────────────────────────────

function loadHistoryEntry(item, li) {
  document.querySelectorAll('#generated-ids-list li.ids-active')
    .forEach(el => el.classList.remove('ids-active'));
  li.classList.add('ids-active');

  const ta = document.getElementById('json-preview');
  ta.value = JSON.stringify(item.request, null, 2);
  ta.classList.remove('json-invalid');
  renderForm(item.request);
  showHistoryBanner(item.id);
  renderCalendar(item);
}

export async function loadGeneratedIds() {
  const list = document.getElementById('generated-ids-list');
  try {
    const res = await fetch('/schedule/generated');
    if (!res.ok) throw new Error(`HTTP ${res.status}`);
    const items = await res.json();

    list.innerHTML = '';
    if (!items || items.length === 0) {
      list.innerHTML = '<li class="ids-empty">No schedules generated yet.</li>';
      return;
    }

    // Render newest first
    for (let i = items.length - 1; i >= 0; i--) {
      const item = items[i];
      const li   = document.createElement('li');
      li.textContent = item.id;
      li.title = 'Click to load into form';
      li.addEventListener('click', async () => {
        try {
          const fresh = await fetch('/schedule/generated');
          if (!fresh.ok) throw new Error();
          const freshItems = await fresh.json();
          const freshItem  = freshItems.find(x => x.id === item.id) || item;
          loadHistoryEntry(freshItem, li);
        } catch {
          loadHistoryEntry(item, li);
        }
      });
      list.appendChild(li);
    }
  } catch {
    list.innerHTML = '<li class="ids-empty">Failed to load IDs.</li>';
  }
}
