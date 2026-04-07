import { renderForm, syncJson, showHistoryBanner, hideHistoryBanner } from './state.js';
import { renderCalendar } from './calendar.js';
import { OPTIMIZER_LABELS } from './constants.js';

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

function buildScenarioInstructions(variant) {
  const today = new Date();
  const pad = n => String(n).padStart(2, '0');
  const fmt = d => `${d.getFullYear()}-${pad(d.getMonth()+1)}-${pad(d.getDate())}`;
  const addDays = (d, n) => { const r = new Date(d); r.setDate(r.getDate()+n); return r; };
  const start = fmt(today);

  const scenarios = {
    'week-light': `Generate a LIGHT one-week scenario (low complexity, easy for the optimizer).
- planningHorizon: ${start} to ${fmt(addDays(today, 6))}
- fixedTasks: 1–3 tasks, spread across different days
- dynamicTasks: 5–8 tasks total; mostly one-time (no repeating), 1–2 with weekly repeating (minWeekCount: 1, optWeekCount: 2)
- categories to use: Work, Morning, Evening (keep it simple — 2–3 categories)
- categoryWindows: ~3–4 windows per day covering Work (09:00–12:00, 13:00–17:00), Morning (07:00–09:00), Evening (18:00–21:00)
- difficultyCapacities: 10–15 per day (comfortable)
- taskTypePreferences: 2–3 days with preferences (e.g., Physical on weekend days)
- Goal: a schedule that the optimizer can solve near-perfectly (few constraint conflicts)`,

    'week-heavy': `Generate a HEAVY one-week scenario (high complexity, challenging for the optimizer).
- planningHorizon: ${start} to ${fmt(addDays(today, 6))}
- fixedTasks: 5–8 tasks filling significant portions of each day
- dynamicTasks: 18–25 tasks total; mix of:
    • One-time tasks with various deadlines throughout the week
    • Day-repeating tasks (e.g., meditation: minDayCount: 1, optDayCount: 1)
    • Week-repeating tasks (e.g., exercise: minWeekCount: 2, optWeekCount: 4)
    • Several isRequired: true tasks (but keep them achievable — ensure matching windows exist)
- categories: use most categories — Work, Study, Home, Health, Social, Morning, Evening
- categoryWindows: dense coverage — multiple windows per day for each category
- difficultyCapacities: 20–28 per day (tight — some days may overflow, which is intentional)
- taskTypePreferences: defined for most days of the week
- Include competing high-priority tasks that may not all fit (priority 4–5 on many tasks)
- Goal: a schedule with tradeoffs — the optimizer must balance multiple competing constraints`,

    'month-light': `Generate a LIGHT one-month scenario (moderate complexity, stretched over 30 days).
- planningHorizon: ${start} to ${fmt(addDays(today, 29))}
- fixedTasks: 4–7 tasks, spread across the month (appointments, meetings, etc.)
- dynamicTasks: 8–14 tasks total; mix of:
    • One-time tasks (some with deadlines 2–3 weeks out)
    • 2–3 weekly repeating tasks (e.g., gym: minWeekCount: 2, optWeekCount: 3)
    • 1–2 daily habits (minDayCount: 1, optDayCount: 1, short duration like 15–30 min)
- categories: Work, Home, Health, Morning, Evening
- categoryWindows: windows for all 30 days — use a repeating weekly pattern (Work Mon–Fri, Morning/Evening every day)
- difficultyCapacities: 12–18 per day for all 30 days
- taskTypePreferences: a few selected days (e.g., weekends: Physical, Outdoor)
- Goal: a realistic personal schedule that a typical person could follow for a month`,

    'month-heavy': `Generate a HEAVY one-month scenario (maximum complexity, stress-test for the optimizer).
- planningHorizon: ${start} to ${fmt(addDays(today, 29))}
- fixedTasks: 12–18 tasks spread across all 4 weeks (recurring meetings, appointments)
- dynamicTasks: 30–40 tasks total; mix of:
    • One-time tasks with staggered deadlines throughout the month
    • Daily habits: 3–5 tasks with minDayCount: 1, optDayCount: 1 (short, 10–20 min each)
    • Weekly recurring tasks: 4–6 tasks with minWeekCount: 1–2, optWeekCount: 3–4
    • Several isRequired: true one-time tasks with tight deadlines
    • Optional tasks with priority 1–2 that may not get scheduled
- categories: use all 10 categories (Work, Study, Home, Health, Social, FreeTime, Transport, Morning, Evening, Weekend)
- categoryWindows: full month coverage — work days have Work/Study windows; weekends have Weekend/FreeTime/Health windows; mornings and evenings every day
- difficultyCapacities: 20–35 per day for all 30 days (varied — harder on weekdays, lighter on weekends)
- taskTypePreferences: most days have preferences (work days: Intellectual/DeepWork; weekends: Physical/Fun/Outdoor)
- Mix of all difficulty levels (1–10) and all priority levels (1–5)
- Goal: a maximum-complexity benchmark that exposes optimizer tradeoffs under real-world load`,
  };

  const desc = scenarios[variant] ?? scenarios['week-light'];
  return `## Scenario Instructions\n\n${desc}`;
}

function buildLlmPrompt(variant = 'week-light') {
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
    '## Optimizer Constraints',
    '',
    'The optimizer scores solutions using hard constraints (HC, must be zero) and soft constraints (SC, minimized).',
    'Understanding these helps you generate data that produces realistic, challenging schedules.',
    '',
    '### Hard Constraints — violations prevent a valid schedule',
    '- HC1 (No Overlaps): Fixed tasks must not overlap each other. Dynamic tasks placed by the optimizer will not overlap.',
    '- HC2 (Required Tasks): Dynamic tasks with isRequired: true MUST be schedulable. Ensure their categories have windows with enough time and their duration fits.',
    '- HC3 (Time Windows): If windowStart/windowEnd are set on a dynamic task, the task duration must fit within that window (windowEnd - windowStart >= duration minutes).',
    '- HC4 (Deadlines): If a deadline is set, there must be enough category window time before that deadline to fit the task.',
    '- HC5 (Category Windows): Every category listed in a dynamic task\'s "categories" must have at least one categoryWindow entry. Missing windows = task cannot be scheduled.',
    '- HC6 (Week Repeating): For week-repeating tasks, minWeekCount per week must be achievable with available windows per week. Do not set minWeekCount so high that there is not enough window time.',
    '- HC7 (Day Repeating): For day-repeating tasks, minDayCount per day must fit in the category windows available that day. E.g., minDayCount: 2 for a 30-min task requires 60 min of matching windows per day.',
    '- HC8 (Planning Horizon): Fixed tasks must have startTime/endTime within planningHorizon dates.',
    '- HC9 (Non-Repeating Uniqueness): Dynamic tasks without a "repeating" field are scheduled at most once.',
    '',
    '### Soft Constraints — affect schedule quality and score',
    '- SC1 (Priority): Higher-priority tasks (priority 5) are scheduled preferentially. Use priority 4–5 for important tasks.',
    '- SC2 (Daily Difficulty Capacity): difficultyCapacities[date].capacity limits total task difficulty per day. Set capacity ≈ sum of difficulties you expect scheduled that day (10–20 is typical). Exceeding capacity is penalized ×1250.',
    '- SC3 (Difficult Task Strategy): Tasks with difficulty ≥ 4 are affected by difficultTaskSchedulingStrategy. "Cluster" groups them together; "Even" spreads them apart.',
    '- SC4 (Type Preferences): taskTypePreferences boost scheduling of matching task types on given days (e.g., Physical tasks on weekends).',
    '- SC5/SC6 (Opt Counts): optWeekCount / optDayCount = the ideal target AND the hard cap — the optimizer will never schedule a task more times than optWeekCount per week or optDayCount per day. Always set optWeekCount >= minWeekCount and optDayCount >= minDayCount. CRITICAL: if a task has minDayCount: 1 (daily habit), optWeekCount must be at least 7 (one per day × 7 days/week) — otherwise the task cannot be scheduled the required number of times. Rule of thumb: optWeekCount >= minDayCount × 7.',
    '- SC7 (Difficulty Balance): The optimizer tries to keep total daily difficulty balanced across days. Mix task difficulties rather than clustering all hard tasks on one day.',
    '',
    '### Common pitfalls to avoid',
    '- Too many isRequired tasks with minDayCount/minWeekCount will saturate the schedule and leave no room for optional tasks.',
    '- Setting minWeekCount larger than the number of available windows per week causes HC6 violations.',
    '- A dynamic task with categories: ["Work"] but no categoryWindow for "Work" can never be scheduled (HC5 violation).',
    '- A required task (isRequired: true) with a category that has no windows in the planning horizon will always be unscheduled.',
    '- optWeekCount and optDayCount are hard scheduling caps, not just targets. A task with minDayCount: 1 and optWeekCount: 1 can only be scheduled once total across the entire week — it cannot appear daily. For daily habits, set optWeekCount >= 7.',
    '',
    '## Instructions',
    'Generate realistic, diverse test data. Return ONLY the raw JSON object — no explanation, no markdown code fences.',
    'Note that minDays (minimum count of tasks in day) and minWeeks (minimum count of tasks in week) in repeating tasks mean that the task is required to be included minimum this amount of times. If there will be too much such tasks, the schedule will consist only of such tasks, so do not put too many repeats.',
    '',
    buildScenarioInstructions(variant),
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
  const variant = document.getElementById('copy-prompt-variant')?.value ?? 'week-light';
  try {
    await navigator.clipboard.writeText(buildLlmPrompt(variant));
    showCopiedFeedback(document.getElementById('copy-prompt-btn'));
  } catch (err) {
    console.error('Copy prompt failed:', err);
  }
}

export async function loadSampleJson() {
  const res  = await fetch('/sample.json');
  const data = await res.json();
  const ta   = document.getElementById('json-preview');
  ta.value = JSON.stringify(data, null, 2);
  ta.classList.remove('json-invalid');
  renderForm(data);
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

  payload.optimizer = document.getElementById('optimizer-select')?.value ?? 'Specialized';

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

let activeScheduleId = null;

function loadHistoryEntry(item, li) {
  document.querySelectorAll('#generated-ids-list li.ids-active')
    .forEach(el => el.classList.remove('ids-active'));
  li.classList.add('ids-active');

  const meta = item.scheduleJobMetadata;
  const ta = document.getElementById('json-preview');
  renderForm(meta.request);
  syncJson();
  ta.classList.remove('json-invalid');
  showHistoryBanner(meta.id);
  renderCalendar({ ...meta, unscheduledDynamicTasks: item.unscheduledDynamicTasks });

  const optimizerKey = meta.optimizer;
  const calOptimizerLabel = document.getElementById('cal-optimizer-label');
  if (calOptimizerLabel) {
    calOptimizerLabel.textContent = OPTIMIZER_LABELS[optimizerKey] ?? optimizerKey;
    calOptimizerLabel.classList.remove('hidden');
  }

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

const CONSTRAINT_INFO = {
  HC3: { fmt: s => `÷60 → ${Math.ceil(s / 60)}` },
  HC4: { fmt: s => `÷60 → ${Math.ceil(s / 60)}` },
  HC8: { fmt: s => `÷60 → ${Math.ceil(s / 60)}` },
  SC1: { fmt: s => `×100 → ${100 * s}` },
  SC2: { fmt: s => `×500 → ${500 * s}` },
  SC5: { fmt: s => `×50 → ${50 * s}` },
  SC6: { fmt: s => `×50 → ${50 * s}` },
};

function buildScoreTooltipHtml(score) {
  const hardRows = score.hardConstraintScores.map(c => {
    const info = CONSTRAINT_INFO[c.constraintName];
    const ann  = info ? `<span class="ctt-ann">(${info.fmt(c.score)})</span>` : '';
    return `<div class="ctt-row"><span class="ctt-lbl">${c.constraintName}</span><span class="ctt-val">${c.score}${ann}</span></div>`;
  }).join('');
  const softRows = score.softConstraintScores.map(c => {
    const info = CONSTRAINT_INFO[c.constraintName];
    const ann  = info ? `<span class="ctt-ann">(${info.fmt(c.score)})</span>` : '';
    return `<div class="ctt-row"><span class="ctt-lbl">${c.constraintName}</span><span class="ctt-val">${c.score}${ann}</span></div>`;
  }).join('');
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

function tooltipMove(tip, e) {
  const tw = tip.offsetWidth, th = tip.offsetHeight;
  let x = e.clientX + 14, y = e.clientY + 14;
  if (x + tw > window.innerWidth  - 8) x = e.clientX - tw - 14;
  if (y + th > window.innerHeight - 8) y = e.clientY - th - 14;
  tip.style.left = x + 'px';
  tip.style.top  = y + 'px';
}

function attachScoreTooltips(container, score) {
  const tip = document.getElementById('cal-tooltip');
  const html = buildScoreTooltipHtml(score);
  const badges = container.querySelectorAll('.cal-score-item');
  const [hardBadge, softBadge] = badges;

  function show(e, content) {
    tip.innerHTML = content;
    tip.style.display = 'block';
    tooltipMove(tip, e);
  }
  function hide() { tip.style.display = 'none'; }

  hardBadge.addEventListener('mouseenter', e => show(e, html.hard));
  hardBadge.addEventListener('mousemove',  e => tooltipMove(tip, e));
  hardBadge.addEventListener('mouseleave', hide);
  softBadge.addEventListener('mouseenter', e => show(e, html.soft));
  softBadge.addEventListener('mousemove',  e => tooltipMove(tip, e));
  softBadge.addEventListener('mouseleave', hide);
}

function attachCombinedScoreTooltip(el, score) {
  const tip = document.getElementById('cal-tooltip');
  const { hard, soft } = buildScoreTooltipHtml(score);
  const combined = hard + `<div class="ctt-sep"></div>` + soft;

  el.addEventListener('mouseenter', e => {
    tip.innerHTML = combined;
    tip.style.display = 'block';
    tooltipMove(tip, e);
  });
  el.addEventListener('mousemove',  e => tooltipMove(tip, e));
  el.addEventListener('mouseleave', () => { tip.style.display = 'none'; });
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

      const optimizerKey = item.scheduleJobMetadata.optimizer;
      const optimizerBadge = document.createElement('span');
      optimizerBadge.className = 'ids-optimizer';
      optimizerBadge.textContent = OPTIMIZER_LABELS[optimizerKey] ?? optimizerKey;
      li.appendChild(optimizerBadge);

      if (item.score) {
        const badge = document.createElement('span');
        badge.className = 'ids-score';
        badge.textContent = `H: ${item.score.score.hardScore} | S: ${item.score.score.softScore}`;
        li.appendChild(badge);
        attachCombinedScoreTooltip(badge, item.score);
      } else {
        const spinner = document.createElement('span');
        spinner.className = 'ids-spinner';
        li.appendChild(spinner);
      }
      li.dataset.scheduleId = item.scheduleJobMetadata.id;
      li.addEventListener('click', async () => {
        activeScheduleId = item.scheduleJobMetadata.id;
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

    if (activeScheduleId) {
      const activeLi = [...list.querySelectorAll('li')].find(
        li => li.dataset.scheduleId === activeScheduleId
      );
      if (activeLi) activeLi.classList.add('ids-active');
    }
  } catch {
    list.innerHTML = '<li class="ids-empty">Failed to load IDs.</li>';
  }
}
