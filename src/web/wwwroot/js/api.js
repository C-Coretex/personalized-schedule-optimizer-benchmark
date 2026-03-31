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

const SAMPLE_JSON = {"planningHorizon":{"startDate":"2026-03-30","endDate":"2026-04-05"},"difficultTaskSchedulingStrategy":"Even","fixedTasks":[{"name":"Team Standup","priority":5,"difficulty":2,"types":["Routine","Collaborative"],"startTime":"2026-03-30T09:00:00","endTime":"2026-03-30T09:30:00"},{"name":"Team Standup","priority":5,"difficulty":2,"types":["Routine","Collaborative"],"startTime":"2026-03-31T09:00:00","endTime":"2026-03-31T09:30:00"},{"name":"Team Standup","priority":5,"difficulty":2,"types":["Routine","Collaborative"],"startTime":"2026-04-01T09:00:00","endTime":"2026-04-01T09:30:00"},{"name":"Team Standup","priority":5,"difficulty":2,"types":["Routine","Collaborative"],"startTime":"2026-04-02T09:00:00","endTime":"2026-04-02T09:30:00"},{"name":"Team Standup","priority":5,"difficulty":2,"types":["Routine","Collaborative"],"startTime":"2026-04-03T09:00:00","endTime":"2026-04-03T09:30:00"},{"name":"Client Presentation","priority":5,"difficulty":8,"types":["Social","Collaborative","HighEnergy"],"startTime":"2026-03-31T14:00:00","endTime":"2026-03-31T15:30:00"},{"name":"Doctor Appointment","priority":5,"difficulty":3,"types":["Routine","Indoor"],"startTime":"2026-04-01T11:00:00","endTime":"2026-04-01T12:00:00"},{"name":"Sprint Retrospective","priority":4,"difficulty":4,"types":["Intellectual","Collaborative"],"startTime":"2026-04-03T15:00:00","endTime":"2026-04-03T16:30:00"},{"name":"Family Dinner","priority":4,"difficulty":1,"types":["Social","Fun"],"startTime":"2026-04-04T19:00:00","endTime":"2026-04-04T21:00:00"}],"dynamicTasks":[{"name":"Feature Implementation","priority":5,"difficulty":8,"types":["Intellectual","DeepWork","Solo"],"isRequired":true,"duration":90,"windowStart":"09:30:00","windowEnd":"13:00:00","deadline":null,"categories":["Work"],"repeating":null},{"name":"Code Review","priority":4,"difficulty":5,"types":["Intellectual","Digital"],"isRequired":true,"duration":60,"windowStart":null,"windowEnd":null,"deadline":"2026-04-03T17:00:00","categories":["Work"],"repeating":null},{"name":"Write Unit Tests","priority":3,"difficulty":5,"types":["Intellectual","Boring","Solo"],"isRequired":false,"duration":75,"windowStart":null,"windowEnd":null,"deadline":null,"categories":["Work"],"repeating":null},{"name":"Update Project Documentation","priority":2,"difficulty":3,"types":["Digital","Boring","Solo"],"isRequired":false,"duration":45,"windowStart":null,"windowEnd":null,"deadline":null,"categories":["Work"],"repeating":null},{"name":"Refactor Legacy Module","priority":3,"difficulty":7,"types":["Intellectual","DeepWork","Solo"],"isRequired":false,"duration":60,"windowStart":null,"windowEnd":null,"deadline":null,"categories":["Work"],"repeating":null},{"name":"Research New Library","priority":2,"difficulty":4,"types":["Intellectual","Digital","Solo"],"isRequired":false,"duration":45,"windowStart":null,"windowEnd":null,"deadline":null,"categories":["Work"],"repeating":null},{"name":"Prepare Sprint Tasks","priority":4,"difficulty":3,"types":["Routine","Digital","Solo"],"isRequired":true,"duration":30,"windowStart":null,"windowEnd":null,"deadline":"2026-04-03T09:00:00","categories":["Work"],"repeating":null},{"name":"Morning Run","priority":4,"difficulty":6,"types":["Physical","Outdoor","HighEnergy"],"isRequired":false,"duration":40,"windowStart":"06:30:00","windowEnd":"08:00:00","deadline":null,"categories":["Health"],"repeating":{"minDayCount":0,"optDayCount":1,"minWeekCount":3,"optWeekCount":5}},{"name":"Stretching","priority":3,"difficulty":2,"types":["Physical","LowEnergy"],"isRequired":false,"duration":15,"windowStart":"06:00:00","windowEnd":"08:30:00","deadline":null,"categories":["Health","Morning"],"repeating":{"minDayCount":1,"optDayCount":1,"minWeekCount":0,"optWeekCount":7}},{"name":"Meditation","priority":3,"difficulty":1,"types":["Solo","LowEnergy","Meditative"],"isRequired":false,"duration":15,"windowStart":"06:00:00","windowEnd":"08:00:00","deadline":null,"categories":["Morning"],"repeating":{"minDayCount":0,"optDayCount":3,"minWeekCount":3,"optWeekCount":6}},{"name":"Gym Session","priority":3,"difficulty":7,"types":["Physical","Solo","HighEnergy"],"isRequired":false,"duration":60,"windowStart":null,"windowEnd":null,"deadline":null,"categories":["Health"],"repeating":null},{"name":"Read Technical Book","priority":3,"difficulty":4,"types":["Intellectual","Indoor","Solo"],"isRequired":false,"duration":40,"windowStart":null,"windowEnd":null,"deadline":null,"categories":["Study"],"repeating":{"minDayCount":0,"optDayCount":0,"minWeekCount":2,"optWeekCount":4}},{"name":"Spanish Language Practice","priority":2,"difficulty":3,"types":["Intellectual","Solo"],"isRequired":false,"duration":20,"windowStart":null,"windowEnd":null,"deadline":null,"categories":["Study"],"repeating":{"minDayCount":0,"optDayCount":1,"minWeekCount":0,"optWeekCount":4}},{"name":"Watch Conference Talk","priority":2,"difficulty":3,"types":["Intellectual","Indoor","Digital"],"isRequired":false,"duration":50,"windowStart":null,"windowEnd":null,"deadline":null,"categories":["Study"],"repeating":null},{"name":"Plan Next Week","priority":3,"difficulty":2,"types":["Routine","Digital","Solo"],"isRequired":false,"duration":30,"windowStart":null,"windowEnd":null,"deadline":"2026-04-05T18:00:00","categories":["Study"],"repeating":null},{"name":"Grocery Shopping","priority":4,"difficulty":2,"types":["Physical","Routine","Indoor"],"isRequired":true,"duration":50,"windowStart":null,"windowEnd":null,"deadline":"2026-04-04T17:00:00","categories":["Home"],"repeating":null},{"name":"Apartment Cleaning","priority":2,"difficulty":3,"types":["Physical","Routine","Boring"],"isRequired":false,"duration":60,"windowStart":null,"windowEnd":null,"deadline":null,"categories":["Home"],"repeating":null},{"name":"Meal Prep","priority":3,"difficulty":2,"types":["Physical","Routine","Indoor"],"isRequired":false,"duration":75,"windowStart":null,"windowEnd":null,"deadline":null,"categories":["Home"],"repeating":null},{"name":"Fix Bike","priority":2,"difficulty":4,"types":["Physical","Indoor","Solo"],"isRequired":false,"duration":40,"windowStart":null,"windowEnd":null,"deadline":null,"categories":["Home"],"repeating":null},{"name":"Evening Walk","priority":3,"difficulty":2,"types":["Outdoor","Solo","LowEnergy"],"isRequired":false,"duration":30,"windowStart":"19:00:00","windowEnd":"21:30:00","deadline":null,"categories":["Evening"],"repeating":{"minDayCount":0,"optDayCount":1,"minWeekCount":0,"optWeekCount":4}},{"name":"Journal Entry","priority":2,"difficulty":1,"types":["Creative","Solo","LowEnergy"],"isRequired":false,"duration":20,"windowStart":null,"windowEnd":null,"deadline":null,"categories":["Evening"],"repeating":null},{"name":"Call a Friend","priority":3,"difficulty":1,"types":["Social","Fun"],"isRequired":false,"duration":30,"windowStart":null,"windowEnd":null,"deadline":null,"categories":["Evening"],"repeating":null},{"name":"Write Blog Post","priority":2,"difficulty":6,"types":["Creative","DeepWork","Digital","Solo"],"isRequired":false,"duration":90,"windowStart":null,"windowEnd":null,"deadline":null,"categories":["FreeTime"],"repeating":null},{"name":"Board Game with Partner","priority":3,"difficulty":1,"types":["Social","Indoor","Fun"],"isRequired":false,"duration":90,"windowStart":null,"windowEnd":null,"deadline":null,"categories":["FreeTime"],"repeating":null},{"name":"Sketch Side Project UI","priority":2,"difficulty":5,"types":["Creative","Digital","Solo"],"isRequired":false,"duration":60,"windowStart":null,"windowEnd":null,"deadline":null,"categories":["FreeTime"],"repeating":null},{"name":"Watch Movie","priority":2,"difficulty":1,"types":["Indoor","Fun","LowEnergy"],"isRequired":false,"duration":120,"windowStart":null,"windowEnd":null,"deadline":null,"categories":["FreeTime"],"repeating":null},{"name":"Walk with dog","priority":1,"difficulty":2,"types":["Physical","Routine","Outdoor"],"isRequired":false,"duration":30,"windowStart":"18:00:00","windowEnd":null,"deadline":null,"categories":["Home","Evening"],"repeating":{"minDayCount":1,"optDayCount":1,"minWeekCount":0,"optWeekCount":7}}],"categoryWindows":[{"category":"Work","startDateTime":"2026-03-30T09:30:00","endDateTime":"2026-03-30T12:30:00"},{"category":"Work","startDateTime":"2026-03-30T13:00:00","endDateTime":"2026-03-30T18:00:00"},{"category":"Work","startDateTime":"2026-03-31T09:30:00","endDateTime":"2026-03-31T12:30:00"},{"category":"Work","startDateTime":"2026-03-31T13:00:00","endDateTime":"2026-03-31T14:00:00"},{"category":"Work","startDateTime":"2026-03-31T15:30:00","endDateTime":"2026-03-31T18:00:00"},{"category":"Work","startDateTime":"2026-04-01T09:30:00","endDateTime":"2026-04-01T11:00:00"},{"category":"Work","startDateTime":"2026-04-01T13:00:00","endDateTime":"2026-04-01T18:00:00"},{"category":"Work","startDateTime":"2026-04-02T09:30:00","endDateTime":"2026-04-02T12:30:00"},{"category":"Work","startDateTime":"2026-04-02T13:00:00","endDateTime":"2026-04-02T18:00:00"},{"category":"Work","startDateTime":"2026-04-03T09:30:00","endDateTime":"2026-04-03T12:30:00"},{"category":"Work","startDateTime":"2026-04-03T13:00:00","endDateTime":"2026-04-03T15:00:00"},{"category":"Health","startDateTime":"2026-03-30T06:30:00","endDateTime":"2026-03-30T08:30:00"},{"category":"Health","startDateTime":"2026-03-31T06:30:00","endDateTime":"2026-03-31T08:30:00"},{"category":"Health","startDateTime":"2026-04-01T06:30:00","endDateTime":"2026-04-01T08:30:00"},{"category":"Health","startDateTime":"2026-04-02T06:30:00","endDateTime":"2026-04-02T08:30:00"},{"category":"Health","startDateTime":"2026-04-03T06:30:00","endDateTime":"2026-04-03T08:30:00"},{"category":"Health","startDateTime":"2026-04-04T07:00:00","endDateTime":"2026-04-04T09:00:00"},{"category":"Health","startDateTime":"2026-04-05T07:00:00","endDateTime":"2026-04-05T09:00:00"},{"category":"Morning","startDateTime":"2026-03-30T06:00:00","endDateTime":"2026-03-30T08:00:00"},{"category":"Morning","startDateTime":"2026-03-31T06:00:00","endDateTime":"2026-03-31T08:00:00"},{"category":"Morning","startDateTime":"2026-04-01T06:00:00","endDateTime":"2026-04-01T08:00:00"},{"category":"Morning","startDateTime":"2026-04-02T06:00:00","endDateTime":"2026-04-02T08:00:00"},{"category":"Morning","startDateTime":"2026-04-03T06:00:00","endDateTime":"2026-04-03T08:00:00"},{"category":"Morning","startDateTime":"2026-04-04T07:00:00","endDateTime":"2026-04-04T09:00:00"},{"category":"Morning","startDateTime":"2026-04-05T07:00:00","endDateTime":"2026-04-05T09:00:00"},{"category":"Study","startDateTime":"2026-03-30T18:00:00","endDateTime":"2026-03-30T20:30:00"},{"category":"Study","startDateTime":"2026-03-31T18:00:00","endDateTime":"2026-03-31T20:30:00"},{"category":"Study","startDateTime":"2026-04-01T18:00:00","endDateTime":"2026-04-01T20:30:00"},{"category":"Study","startDateTime":"2026-04-02T18:00:00","endDateTime":"2026-04-02T20:30:00"},{"category":"Study","startDateTime":"2026-04-03T18:00:00","endDateTime":"2026-04-03T20:30:00"},{"category":"Study","startDateTime":"2026-04-04T10:00:00","endDateTime":"2026-04-04T12:00:00"},{"category":"Study","startDateTime":"2026-04-05T10:00:00","endDateTime":"2026-04-05T12:00:00"},{"category":"Home","startDateTime":"2026-04-02T18:00:00","endDateTime":"2026-04-02T20:00:00"},{"category":"Home","startDateTime":"2026-04-04T10:00:00","endDateTime":"2026-04-04T14:00:00"},{"category":"Home","startDateTime":"2026-04-05T10:00:00","endDateTime":"2026-04-05T13:00:00"},{"category":"Evening","startDateTime":"2026-03-30T19:00:00","endDateTime":"2026-03-30T22:00:00"},{"category":"Evening","startDateTime":"2026-03-31T19:00:00","endDateTime":"2026-03-31T22:00:00"},{"category":"Evening","startDateTime":"2026-04-01T19:00:00","endDateTime":"2026-04-01T22:00:00"},{"category":"Evening","startDateTime":"2026-04-02T19:00:00","endDateTime":"2026-04-02T22:00:00"},{"category":"Evening","startDateTime":"2026-04-03T19:00:00","endDateTime":"2026-04-03T22:00:00"},{"category":"Evening","startDateTime":"2026-04-04T21:00:00","endDateTime":"2026-04-04T23:00:00"},{"category":"Evening","startDateTime":"2026-04-05T19:00:00","endDateTime":"2026-04-05T22:00:00"},{"category":"FreeTime","startDateTime":"2026-04-03T20:00:00","endDateTime":"2026-04-03T22:30:00"},{"category":"FreeTime","startDateTime":"2026-04-04T14:00:00","endDateTime":"2026-04-04T19:00:00"},{"category":"FreeTime","startDateTime":"2026-04-05T13:00:00","endDateTime":"2026-04-05T18:00:00"}],"difficultyCapacities":[{"date":"2026-03-30","capacity":15},{"date":"2026-03-31","capacity":20},{"date":"2026-04-01","capacity":12},{"date":"2026-04-02","capacity":18},{"date":"2026-04-03","capacity":16},{"date":"2026-04-04","capacity":10},{"date":"2026-04-05","capacity":10}],"taskTypePreferences":[{"date":"2026-03-30","preferences":[{"type":"DeepWork","weight":3},{"type":"Intellectual","weight":2}]},{"date":"2026-04-01","preferences":[{"type":"LowEnergy","weight":2},{"type":"Routine","weight":2}]},{"date":"2026-04-04","preferences":[{"type":"Fun","weight":3},{"type":"Physical","weight":2},{"type":"Creative","weight":2}]},{"date":"2026-04-05","preferences":[{"type":"LowEnergy","weight":3},{"type":"Meditative","weight":2},{"type":"Solo","weight":1}]}]};

export function loadSampleJson() {
  const ta = document.getElementById('json-preview');
  ta.value = JSON.stringify(SAMPLE_JSON, null, 2);
  ta.classList.remove('json-invalid');
  renderForm(SAMPLE_JSON);
  hideHistoryBanner();
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

  const meta = item.scheduleJobMetadata;
  const ta = document.getElementById('json-preview');
  ta.value = JSON.stringify(meta.request, null, 2);
  ta.classList.remove('json-invalid');
  renderForm(meta.request);
  showHistoryBanner(meta.id);
  renderCalendar(meta);

  const calScore = document.getElementById('cal-score');
  if (item.score) {
    const { hardScore, softScore } = item.score.score;
    calScore.innerHTML =
      `<span class="cal-score-item hard"><span class="cal-score-label">HARD</span><span class="cal-score-value">${hardScore}</span></span>` +
      `<span class="cal-score-item soft"><span class="cal-score-label">SOFT</span><span class="cal-score-value">${softScore}</span></span>`;
    calScore.classList.remove('hidden');
    attachScoreTooltips(calScore, item.score);
  } else {
    calScore.classList.add('hidden');
  }
}

function buildScoreTooltipHtml(score) {
  const hardRows = score.hardConstraintScores.map(c =>
    `<div class="ctt-row"><span class="ctt-lbl">${c.constraintName}</span><span class="ctt-val">${c.score}</span></div>`
  ).join('');
  const softRows = score.softConstraintScores.map(c =>
    `<div class="ctt-row"><span class="ctt-lbl">${c.constraintName}</span><span class="ctt-val">${c.score}</span></div>`
  ).join('');
  return {
    hard: `
      <div class="ctt-title"><span class="ctt-dot" style="background:#dc2626"></span>Hard Constraints</div>
      <div class="ctt-row"><span class="ctt-lbl">Total</span><span class="ctt-val score-total hard">${score.score.hardScore}</span></div>
      ${hardRows}`,
    soft: `
      <div class="ctt-title"><span class="ctt-dot" style="background:#0284c7"></span>Soft Constraints</div>
      <div class="ctt-row"><span class="ctt-lbl">Total</span><span class="ctt-val score-total soft">${score.score.softScore}</span></div>
      ${softRows}`,
  };
}

function attachScoreTooltips(container, score) {
  const tip = document.getElementById('cal-tooltip');
  const html = buildScoreTooltipHtml(score);
  const badges = container.querySelectorAll('.cal-score-item');
  const [hardBadge, softBadge] = badges;

  function show(e, content) {
    tip.innerHTML = content;
    tip.style.display = 'block';
    move(e);
  }
  function move(e) {
    const tw = tip.offsetWidth, th = tip.offsetHeight;
    let x = e.clientX + 14, y = e.clientY + 14;
    if (x + tw > window.innerWidth  - 8) x = e.clientX - tw - 14;
    if (y + th > window.innerHeight - 8) y = e.clientY - th - 14;
    tip.style.left = x + 'px';
    tip.style.top  = y + 'px';
  }
  function hide() { tip.style.display = 'none'; }

  hardBadge.addEventListener('mouseenter', e => show(e, html.hard));
  hardBadge.addEventListener('mousemove',  move);
  hardBadge.addEventListener('mouseleave', hide);
  softBadge.addEventListener('mouseenter', e => show(e, html.soft));
  softBadge.addEventListener('mousemove',  move);
  softBadge.addEventListener('mouseleave', hide);
}

function buildScoreTitle(score) {
  if (!score) return 'Click to load into form';
  const hard = score.hardConstraintScores.map(c => `${c.constraintName}: ${c.score}`).join(', ');
  const soft = score.softConstraintScores.map(c => `${c.constraintName}: ${c.score}`).join(', ');
  return `Hard constraints: ${hard}\nSoft constraints: ${soft}`;
}

export async function loadGeneratedIds() {
  const list = document.getElementById('generated-ids-list');
  try {
    const res = await fetch('/schedule/generated');
    if (!res.ok) throw new Error(`HTTP ${res.status}`);
    const { data: items } = await res.json();

    list.innerHTML = '';
    if (!items || items.length === 0) {
      list.innerHTML = '<li class="ids-empty">No schedules generated yet.</li>';
      return;
    }

    // Render newest first
    for (let i = items.length - 1; i >= 0; i--) {
      const item = items[i];
      const li   = document.createElement('li');

      const idSpan = document.createElement('span');
      idSpan.className = 'ids-id';
      idSpan.textContent = item.scheduleJobMetadata.id;
      li.appendChild(idSpan);

      if (item.score) {
        const badge = document.createElement('span');
        badge.className = 'ids-score';
        badge.textContent = `H: ${item.score.score.hardScore} | S: ${item.score.score.softScore}`;
        li.appendChild(badge);
      }

      li.title = buildScoreTitle(item.score);
      li.addEventListener('click', async () => {
        try {
          const fresh = await fetch('/schedule/generated');
          if (!fresh.ok) throw new Error();
          const { data: freshItems } = await fresh.json();
          const freshItem = freshItems.find(x => x.scheduleJobMetadata.id === item.scheduleJobMetadata.id) || item;
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
