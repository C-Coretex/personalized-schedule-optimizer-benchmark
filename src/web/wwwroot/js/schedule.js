// ─── Constants ────────────────────────────────────────────────────────────────

const TASK_TYPES = [
  { value: 'Physical',      label: 'Physical' },
  { value: 'Intellectual',  label: 'Intellectual' },
  { value: 'Creative',      label: 'Creative' },
  { value: 'Social',        label: 'Social' },
  { value: 'Routine',       label: 'Routine' },
  { value: 'DeepWork',      label: 'Deep work' },
  { value: 'Outdoor',       label: 'Outdoor' },
  { value: 'Indoor',        label: 'Indoor' },
  { value: 'Digital',       label: 'Digital' },
  { value: 'Fun',           label: 'Fun' },
  { value: 'Boring',        label: 'Boring' },
  { value: 'Collaborative', label: 'Collaborative' },
  { value: 'Solo',          label: 'Solo' },
  { value: 'HighEnergy',    label: 'High-energy' },
  { value: 'LowEnergy',     label: 'Low-energy' },
  { value: 'Meditative',    label: 'Meditative' },
];

const CATEGORIES = [
  { value: 'Work',      label: 'Work' },
  { value: 'Study',     label: 'Study' },
  { value: 'Home',      label: 'Home' },
  { value: 'Health',    label: 'Health' },
  { value: 'Social',    label: 'Social' },
  { value: 'FreeTime',  label: 'Free time' },
  { value: 'Transport', label: 'Transport' },
  { value: 'Morning',   label: 'Morning' },
  { value: 'Evening',   label: 'Evening' },
  { value: 'Weekend',   label: 'Weekend' },
];

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
    info: full.info,
    paths: { '/schedule/generate': generatePath },
    components: { schemas: filteredSchemas },
  };
}

async function fetchSchema() {
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
        '- priority: integer 1\u20135',
        '- difficulty: integer 1\u201310',
        '- startTime must be strictly before endTime',
        '- Both startTime and endTime must fall within planningHorizon (startDate..endDate)',
        '- startTime and endTime must be on the same calendar day',
        '- Fixed tasks must not overlap each other',
        '- Format: "YYYY-MM-DDTHH:MM:SS"',
        '',
        '### dynamicTasks',
        '- priority: integer 1\u20135',
        '- difficulty: integer 1\u201310',
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
        '"Cluster" \u2014 group hard tasks together in the schedule',
        '"Even"    \u2014 spread hard tasks evenly across available time slots',
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
        'Generate realistic, diverse test data. Return ONLY the raw JSON object \u2014 no explanation, no markdown code fences.',
        'Note that minDays (minimum count of tasks in day) and minWeeks (minimum count of tasks in week) in repeating tasks mean that the task is required to be included minimum this amount of times. If there will be too much such tasks, the schedule will consist only of such tasks, so do not put too many repeats.',
    '',
    '## OpenAPI Schema',
    schemaJson !== null ? JSON.stringify(schemaJson, null, 2) : '(schema not loaded)',
  ];
  return lines.join('\n');
}

function showCopiedFeedback(btn) {
  const original = btn.textContent;
  btn.textContent = 'Copied!';
  btn.disabled = true;
  setTimeout(() => {
    btn.textContent = original;
    btn.disabled = false;
  }, 1500);
}

async function copySchemaToClipboard(e) {
  e.stopPropagation();
  if (schemaJson === null) return;
  try {
    await navigator.clipboard.writeText(JSON.stringify(schemaJson, null, 2));
    showCopiedFeedback(document.getElementById('copy-schema-btn'));
  } catch (err) {
    console.error('Copy schema failed:', err);
  }
}

async function copyPromptToClipboard(e) {
  e.stopPropagation();
  try {
    await navigator.clipboard.writeText(buildLlmPrompt());
    showCopiedFeedback(document.getElementById('copy-prompt-btn'));
  } catch (err) {
    console.error('Copy prompt failed:', err);
  }
}

// ─── Helpers ──────────────────────────────────────────────────────────────────

/** Escape a value for use in an HTML attribute */
function esc(v) {
  return String(v ?? '').replace(/&/g, '&amp;').replace(/"/g, '&quot;').replace(/</g, '&lt;').replace(/>/g, '&gt;');
}

/** "2024-01-15T09:00" → "2024-01-15T09:00:00" */
function toDateTime(v) {
  if (!v) return null;
  return v.length === 16 ? v + ':00' : v;
}

/** "09:00" → "09:00:00" */
function toTimeOnly(v) {
  if (!v) return null;
  return v.length === 5 ? v + ':00' : v;
}

/** "2024-01-15T09:00:00" → "2024-01-15T09:00" */
function fromDateTime(v) {
  return v ? String(v).slice(0, 16) : '';
}

/** "09:00:00" → "09:00" */
function fromTimeOnly(v) {
  return v ? String(v).slice(0, 5) : '';
}

// ─── Read state from DOM ──────────────────────────────────────────────────────

function readState() {
  return {
    planningHorizon: {
      startDate: document.getElementById('horizon-start').value || null,
      endDate:   document.getElementById('horizon-end').value   || null,
    },
    difficultTaskSchedulingStrategy: document.getElementById('strategy').value,

    fixedTasks: [...document.querySelectorAll('#fixed-tasks-list .task-item')].map(el => ({
      name:       el.querySelector('.ft-name').value || '',
      priority:   parseInt(el.querySelector('.ft-priority').value)   || 1,
      difficulty: parseInt(el.querySelector('.ft-difficulty').value) || 1,
      types:      [...el.querySelectorAll('.ft-type:checked')].map(cb => cb.value),
      startTime:  toDateTime(el.querySelector('.ft-start').value),
      endTime:    toDateTime(el.querySelector('.ft-end').value),
    })),

    dynamicTasks: [...document.querySelectorAll('#dynamic-tasks-list .task-item')].map(el => ({
      name:        el.querySelector('.dt-name').value || '',
      priority:    parseInt(el.querySelector('.dt-priority').value)   || 1,
      difficulty:  parseInt(el.querySelector('.dt-difficulty').value) || 1,
      types:       [...el.querySelectorAll('.dt-type:checked')].map(cb => cb.value),
      isRequired:  el.querySelector('.dt-required').checked,
      duration:    parseInt(el.querySelector('.dt-duration').value)   || 0,
      windowStart: toTimeOnly(el.querySelector('.dt-window-start').value),
      windowEnd:   toTimeOnly(el.querySelector('.dt-window-end').value),
      deadline:    toDateTime(el.querySelector('.dt-deadline').value),
      categories:  [...el.querySelectorAll('.dt-category:checked')].map(cb => cb.value),
      repeating:   el.querySelector('.dt-repeating-toggle').checked ? {
        minDayCount:  parseInt(el.querySelector('.dt-min-days').value)   || 0,
        optDayCount:  parseInt(el.querySelector('.dt-opt-days').value)   || 0,
        minWeekCount: parseInt(el.querySelector('.dt-min-weeks').value)  || 0,
        optWeekCount: parseInt(el.querySelector('.dt-opt-weeks').value)  || 0,
      } : null,
    })),

    categoryWindows: [...document.querySelectorAll('#category-windows-list .item')].map(el => ({
      category:      el.querySelector('.cw-category').value,  // select → enum string
      startDateTime: toDateTime(el.querySelector('.cw-start').value),
      endDateTime:   toDateTime(el.querySelector('.cw-end').value),
    })),

    difficultyCapacities: [...document.querySelectorAll('#difficulty-capacities-list .item')].map(el => ({
      date:     el.querySelector('.dc-date').value     || null,
      capacity: parseInt(el.querySelector('.dc-capacity').value) || 0,
    })),

    taskTypePreferences: [...document.querySelectorAll('#type-preferences-list .pref-item')].map(el => ({
      date: el.querySelector('.tp-date').value || null,
      preferences: [...el.querySelectorAll('.tw-row')].map(row => ({
        type:   row.querySelector('.tw-type').value,
        weight: parseInt(row.querySelector('.tw-weight').value) || 0,
      })),
    })),
  };
}

// ─── Sync JSON textarea from DOM ─────────────────────────────────────────────

function syncJson() {
  document.getElementById('json-preview').value = JSON.stringify(readState(), null, 2);
  document.getElementById('json-preview').classList.remove('json-invalid');
}

// ─── Remove helpers ───────────────────────────────────────────────────────────

function removeItem(btn) {
  btn.closest('.task-item, .item, .pref-item').remove();
  syncJson();
}

function removeTypeWeight(btn) {
  btn.closest('.tw-row').remove();
  syncJson();
}

// ─── Add Fixed Task ───────────────────────────────────────────────────────────

function addFixedTask(data = {}, suppressSync = false) {
  const div = document.createElement('div');
  div.className = 'task-item';
  div.innerHTML = `
    <div class="item-header">
      <span class="item-label">Fixed Task</span>
      <button type="button" class="btn-remove" onclick="removeItem(this)">Remove</button>
    </div>
    <div class="row">
      <label>Name
        <input class="ft-name" type="text" value="${esc(data.name ?? '')}" required>
      </label>
    </div>
    <div class="row">
      <label>Priority (1–5)
        <input class="ft-priority" type="number" min="1" max="5" value="${esc(data.priority ?? 1)}" required>
      </label>
      <label>Difficulty (1–10)
        <input class="ft-difficulty" type="number" min="1" max="10" value="${esc(data.difficulty ?? 1)}" required>
      </label>
    </div>
    <div class="row">
      <label>Types
        <div class="checkbox-group">
          ${TASK_TYPES.map(t => `
            <label><input class="ft-type" type="checkbox" value="${t.value}"
              ${(data.types ?? []).includes(t.value) ? 'checked' : ''}> ${t.label}</label>
          `).join('')}
        </div>
      </label>
    </div>
    <div class="row">
      <label>Start Time
        <input class="ft-start" type="datetime-local" value="${esc(fromDateTime(data.startTime))}" required>
      </label>
      <label>End Time
        <input class="ft-end" type="datetime-local" value="${esc(fromDateTime(data.endTime))}" required>
      </label>
    </div>
  `;
  div.querySelectorAll('input, select').forEach(el => el.addEventListener('input', syncJson));
  document.getElementById('fixed-tasks-list').appendChild(div);
  if (!suppressSync) syncJson();
}

// ─── Add Dynamic Task ─────────────────────────────────────────────────────────

function addDynamicTask(data = {}, suppressSync = false) {
  const hasRepeating = data.repeating != null;
  const div = document.createElement('div');
  div.className = 'task-item';
  div.innerHTML = `
    <div class="item-header">
      <span class="item-label">Dynamic Task</span>
      <button type="button" class="btn-remove" onclick="removeItem(this)">Remove</button>
    </div>
    <div class="row">
      <label>Name
        <input class="dt-name" type="text" value="${esc(data.name ?? '')}" required>
      </label>
    </div>
    <div class="row">
      <label>Priority (1–5)
        <input class="dt-priority" type="number" min="1" max="5" value="${esc(data.priority ?? 1)}" required>
      </label>
      <label>Difficulty (1–10)
        <input class="dt-difficulty" type="number" min="1" max="10" value="${esc(data.difficulty ?? 1)}" required>
      </label>
      <label class="inline-check">Required
        <input class="dt-required" type="checkbox" ${data.isRequired ? 'checked' : ''}>
      </label>
    </div>
    <div class="row">
      <label>Types
        <div class="checkbox-group">
          ${TASK_TYPES.map(t => `
            <label><input class="dt-type" type="checkbox" value="${t.value}"
              ${(data.types ?? []).includes(t.value) ? 'checked' : ''}> ${t.label}</label>
          `).join('')}
        </div>
      </label>
    </div>
    <div class="row">
      <label>Duration (min)
        <input class="dt-duration" type="number" min="1" value="${esc(data.duration ?? 30)}" required>
      </label>
      <label>Deadline
        <input class="dt-deadline" type="datetime-local" value="${esc(fromDateTime(data.deadline))}">
      </label>
    </div>
    <div class="row">
      <label>Window Start
        <input class="dt-window-start" type="time" value="${esc(fromTimeOnly(data.windowStart))}">
      </label>
      <label>Window End
        <input class="dt-window-end" type="time" value="${esc(fromTimeOnly(data.windowEnd))}">
      </label>
    </div>
    <div class="row">
      <label>Categories
        <div class="checkbox-group">
          ${CATEGORIES.map(c => `
            <label><input class="dt-category" type="checkbox" value="${c.value}"
              ${(data.categories ?? []).includes(c.value) ? 'checked' : ''}> ${c.label}</label>
          `).join('')}
        </div>
      </label>
    </div>
    <div class="row">
      <label class="inline-check">Repeating
        <input class="dt-repeating-toggle" type="checkbox" ${hasRepeating ? 'checked' : ''}
          onchange="toggleRepeating(this)">
      </label>
    </div>
    <div class="repeating-fields" style="display:${hasRepeating ? 'grid' : 'none'}">
      <div class="row">
        <label>Min tasks in Day
          <input class="dt-min-days" type="number" min="0" value="${esc(data.repeating?.minDayCount ?? 0)}">
        </label>
        <label>Opt tasks in Day
          <input class="dt-opt-days" type="number" min="0" value="${esc(data.repeating?.optDayCount ?? 0)}">
        </label>
        <label>Min tasks in Week
          <input class="dt-min-weeks" type="number" min="0" value="${esc(data.repeating?.minWeekCount ?? 0)}">
        </label>
        <label>Opt tasks in Week
          <input class="dt-opt-weeks" type="number" min="0" value="${esc(data.repeating?.optWeekCount ?? 0)}">
        </label>
      </div>
    </div>
  `;
  div.querySelectorAll('input, select').forEach(el => el.addEventListener('input', syncJson));
  document.getElementById('dynamic-tasks-list').appendChild(div);
  if (!suppressSync) syncJson();
}

function toggleRepeating(checkbox) {
  checkbox.closest('.task-item').querySelector('.repeating-fields').style.display =
    checkbox.checked ? 'grid' : 'none';
  syncJson();
}

// ─── Add Category Window ──────────────────────────────────────────────────────

function addCategoryWindow(data = {}, suppressSync = false) {
  const div = document.createElement('div');
  div.className = 'item';
  div.innerHTML = `
    <div class="item-header">
      <button type="button" class="btn-remove" onclick="removeItem(this)">Remove</button>
    </div>
    <div class="row">
      <label>Category
        <select class="cw-category">
          ${CATEGORIES.map(c => `<option value="${c.value}" ${data.category === c.value ? 'selected' : ''}>${c.label}</option>`).join('')}
        </select>
      </label>
      <label>Start
        <input class="cw-start" type="datetime-local" value="${esc(fromDateTime(data.startDateTime))}" required>
      </label>
      <label>End
        <input class="cw-end" type="datetime-local" value="${esc(fromDateTime(data.endDateTime))}" required>
      </label>
    </div>
  `;
  div.querySelectorAll('input, select').forEach(el => el.addEventListener('input', syncJson));
  document.getElementById('category-windows-list').appendChild(div);
  if (!suppressSync) syncJson();
}

// ─── Add Difficulty Capacity ──────────────────────────────────────────────────

function addDifficultyCapacity(data = {}, suppressSync = false) {
  const div = document.createElement('div');
  div.className = 'item';
  div.innerHTML = `
    <div class="item-header">
      <button type="button" class="btn-remove" onclick="removeItem(this)">Remove</button>
    </div>
    <div class="row">
      <label>Date
        <input class="dc-date" type="date" value="${esc(data.date)}" required>
      </label>
      <label>Capacity
        <input class="dc-capacity" type="number" min="0" value="${esc(data.capacity ?? 0)}">
      </label>
    </div>
  `;
  div.querySelectorAll('input').forEach(el => el.addEventListener('input', syncJson));
  document.getElementById('difficulty-capacities-list').appendChild(div);
  if (!suppressSync) syncJson();
}

// ─── Add Task Type Preference ─────────────────────────────────────────────────

function addTypePreference(data = {}, suppressSync = false) {
  const div = document.createElement('div');
  div.className = 'pref-item item';
  div.innerHTML = `
    <div class="item-header">
      <button type="button" class="btn-remove" onclick="removeItem(this)">Remove</button>
    </div>
    <div class="row">
      <label>Date
        <input class="tp-date" type="date" value="${esc(data.date)}" required>
      </label>
      <button type="button" class="btn-add" onclick="addTypeWeight(this)">+ Type Weight</button>
    </div>
    <div class="tw-list">
      ${(data.preferences ?? []).map(p => typeWeightHTML(p)).join('')}
    </div>
  `;
  div.querySelectorAll('input, select').forEach(el => el.addEventListener('input', syncJson));
  document.getElementById('type-preferences-list').appendChild(div);
  if (!suppressSync) syncJson();
}

function typeWeightHTML(data = {}) {
  return `
    <div class="tw-row row">
      <label>Type
        <select class="tw-type">
          ${TASK_TYPES.map(t => `<option value="${t.value}" ${data.type === t.value ? 'selected' : ''}>${t.label}</option>`).join('')}
        </select>
      </label>
      <label>Weight
        <input class="tw-weight" type="number" min="0" value="${esc(data.weight ?? 1)}">
      </label>
      <button type="button" class="btn-remove" onclick="removeTypeWeight(this)">✕</button>
    </div>
  `;
}

function addTypeWeight(btn) {
  const twList = btn.closest('.pref-item').querySelector('.tw-list');
  const tmp = document.createElement('div');
  tmp.innerHTML = typeWeightHTML();
  const row = tmp.firstElementChild;
  row.querySelectorAll('input, select').forEach(el => el.addEventListener('input', syncJson));
  twList.appendChild(row);
  syncJson();
}

// ─── Render form from a state object ─────────────────────────────────────────

function renderForm(s) {
  document.getElementById('horizon-start').value = s.planningHorizon?.startDate ?? '';
  document.getElementById('horizon-end').value   = s.planningHorizon?.endDate   ?? '';
  document.getElementById('strategy').value      = s.difficultTaskSchedulingStrategy ?? 'StrategyA';

  document.getElementById('fixed-tasks-list').innerHTML     = '';
  document.getElementById('dynamic-tasks-list').innerHTML   = '';
  document.getElementById('category-windows-list').innerHTML = '';
  document.getElementById('difficulty-capacities-list').innerHTML = '';
  document.getElementById('type-preferences-list').innerHTML = '';

  (s.fixedTasks          ?? []).forEach(t => addFixedTask(t,             true));
  (s.dynamicTasks        ?? []).forEach(t => addDynamicTask(t,           true));
  (s.categoryWindows     ?? []).forEach(w => addCategoryWindow(w,        true));
  (s.difficultyCapacities ?? []).forEach(d => addDifficultyCapacity(d,   true));
  (s.taskTypePreferences ?? []).forEach(p => addTypePreference(p,        true));
  // Don't call syncJson — textarea already has the parsed value.
}

// ─── Default / Clear state ────────────────────────────────────────────────────

const DEFAULT_STATE = {
  planningHorizon: { startDate: null, endDate: null },
  difficultTaskSchedulingStrategy: 'Cluster',
  fixedTasks: [],
  dynamicTasks: [],
  categoryWindows: [],
  difficultyCapacities: [],
  taskTypePreferences: [],
};

function clearForm() {
  renderForm(DEFAULT_STATE);
  const ta = document.getElementById('json-preview');
  ta.value = JSON.stringify(DEFAULT_STATE, null, 2);
  ta.classList.remove('json-invalid');
  hideHistoryBanner();
  document.querySelectorAll('#generated-ids-list li.ids-active')
    .forEach(li => li.classList.remove('ids-active'));
  document.getElementById('calendar-section').classList.add('hidden');
}

// ─── History banner ───────────────────────────────────────────────────────────

function showHistoryBanner(id) {
  const el = document.getElementById('history-banner');
  el.textContent = `Viewing history entry — ${id}`;
  el.classList.remove('hidden');
}

function hideHistoryBanner() {
  document.getElementById('history-banner').classList.add('hidden');
}

// ─── Generated IDs Panel ─────────────────────────────────────────────────────

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

async function loadGeneratedIds() {
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
      const li = document.createElement('li');
      li.textContent = item.id;
      li.title = 'Click to load into form';
      li.addEventListener('click', async () => {
        try {
          const fresh = await fetch('/schedule/generated');
          if (!fresh.ok) throw new Error();
          const freshItems = await fresh.json();
          const freshItem = freshItems.find(x => x.id === item.id) || item;
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

// ─── Submit ───────────────────────────────────────────────────────────────────

async function submit() {
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
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(payload),
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

function showResponse(message, ok) {
  const el = document.getElementById('response-output');
  el.textContent = message;
  el.className = ok ? 'response-ok' : 'response-error';
}

// ─── Calendar ─────────────────────────────────────────────────────────────────

const CATEGORY_COLORS = {
  Work:      '#3b82f6',
  Study:     '#8b5cf6',
  Home:      '#f97316',
  Health:    '#22c55e',
  Social:    '#ec4899',
  FreeTime:  '#f59e0b',
  Transport: '#64748b',
  Morning:   '#0ea5e9',
  Evening:   '#6366f1',
  Weekend:   '#14b8a6',
};
const FIXED_COLOR   = '#94a3b8';
const CAL_START_HOUR = 6;
const CAL_END_HOUR   = 23;
const CAL_HOUR_PX    = 60;

const DAY_NAMES  = ['Sun', 'Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat'];
const MONTH_ABBR = ['Jan','Feb','Mar','Apr','May','Jun','Jul','Aug','Sep','Oct','Nov','Dec'];

let calEventTasks = [];

function calDateKey(dt) {
  return `${dt.getFullYear()}-${String(dt.getMonth()+1).padStart(2,'0')}-${String(dt.getDate()).padStart(2,'0')}`;
}

function fmtTime(d) {
  return `${String(d.getHours()).padStart(2,'0')}:${String(d.getMinutes()).padStart(2,'0')}`;
}

function buildTooltipHtml(t) {
  const start  = new Date(t.startTime);
  const end    = new Date(t.endTime);
  const durMin = Math.round((end - start) / 60000);
  const durStr = durMin >= 60
    ? `${Math.floor(durMin / 60)}h${durMin % 60 ? ' ' + (durMin % 60) + 'm' : ''}`
    : `${durMin}m`;
  const color  = t.isFixed ? FIXED_COLOR : (CATEGORY_COLORS[t.categories[0]] || '#9ca3af');

  const dots = (val, max) => {
    const filled = '●'.repeat(val);
    const empty  = '○'.repeat(max - val);
    return `<span class="ctt-dots"><span class="filled">${filled}</span>${empty}</span>`;
  };

  let html = `
    <div class="ctt-title">
      <span class="ctt-dot" style="background:${color}"></span>
      ${t.name}
    </div>
    <div class="ctt-row">
      <span class="ctt-lbl">Time</span>
      <span class="ctt-val">${fmtTime(start)} – ${fmtTime(end)}</span>
    </div>
    <div class="ctt-row">
      <span class="ctt-lbl">Duration</span>
      <span class="ctt-val">${durStr}</span>
    </div>
    <div class="ctt-row">
      <span class="ctt-lbl">Kind</span>
      <span class="ctt-val">${t.isFixed ? 'Fixed' : 'Dynamic'}</span>
    </div>`;

  if (t.categories.length) {
    html += `<div class="ctt-row">
      <span class="ctt-lbl">Categories</span>
      <span class="ctt-val">${t.categories.join(', ')}</span>
    </div>`;
  }
  if (t.types.length) {
    html += `<div class="ctt-row">
      <span class="ctt-lbl">Types</span>
      <span class="ctt-val">${t.types.join(', ')}</span>
    </div>`;
  }
  if (t.priority != null) {
    html += `<div class="ctt-row">
      <span class="ctt-lbl">Priority</span>
      <span class="ctt-val">${dots(t.priority, 5)}</span>
    </div>`;
  }
  if (t.difficulty != null) {
    html += `<div class="ctt-row">
      <span class="ctt-lbl">Difficulty</span>
      <span class="ctt-val">${t.difficulty} / 10</span>
    </div>`;
  }
  if (!t.isFixed) {
    if (t.isRequired != null) {
      html += `<div class="ctt-row">
        <span class="ctt-lbl">Required</span>
        <span class="ctt-val">${t.isRequired ? 'Yes' : 'No'}</span>
      </div>`;
    }
    if (t.deadline) {
      const dl = new Date(t.deadline);
      html += `<div class="ctt-row">
        <span class="ctt-lbl">Deadline</span>
        <span class="ctt-val">${dl.getDate()} ${MONTH_ABBR[dl.getMonth()]} ${fmtTime(dl)}</span>
      </div>`;
    }
    if (t.windowStart || t.windowEnd) {
      const ws = t.windowStart ? t.windowStart.slice(0, 5) : '—';
      const we = t.windowEnd   ? t.windowEnd.slice(0, 5)   : '—';
      html += `<div class="ctt-row">
        <span class="ctt-lbl">Window</span>
        <span class="ctt-val">${ws} – ${we}</span>
      </div>`;
    }
    if (t.repeating) {
      const r = t.repeating;
      const parts = [];
      if (r.optWeekCount) parts.push(`${r.minWeekCount}–${r.optWeekCount}×/week`);
      if (r.optDayCount)  parts.push(`${r.minDayCount}–${r.optDayCount}×/day`);
      if (parts.length) {
        html += `<div class="ctt-row">
          <span class="ctt-lbl">Repeating</span>
          <span class="ctt-val">${parts.join(', ')}</span>
        </div>`;
      }
    }
  }
  return html;
}

function showTooltip(e, task) {
  const tip = document.getElementById('cal-tooltip');
  tip.innerHTML = buildTooltipHtml(task);
  tip.style.display = 'block';
  positionTooltip(e, tip);
}

function positionTooltip(e, tip) {
  tip = tip || document.getElementById('cal-tooltip');
  const tw = tip.offsetWidth;
  const th = tip.offsetHeight;
  let x = e.clientX + 14;
  let y = e.clientY + 14;
  if (x + tw > window.innerWidth  - 8) x = e.clientX - tw - 14;
  if (y + th > window.innerHeight - 8) y = e.clientY - th - 14;
  tip.style.left = x + 'px';
  tip.style.top  = y + 'px';
}

function hideTooltip() {
  document.getElementById('cal-tooltip').style.display = 'none';
}

function buildCalLegend(tasks) {
  const cats = new Set();
  let hasFixed = false;
  for (const t of tasks) {
    if (t.isFixed) { hasFixed = true; }
    else { for (const c of t.categories) cats.add(c); }
  }
  const el = document.getElementById('cal-legend');
  let html = '';
  for (const cat of cats) {
    const color = CATEGORY_COLORS[cat] || '#9ca3af';
    html += `<span class="cal-legend-item">
      <span class="cal-legend-swatch" style="background:${color}"></span>${cat}
    </span>`;
  }
  if (hasFixed) {
    html += `<span class="cal-legend-item">
      <span class="cal-legend-swatch" style="background:${FIXED_COLOR}"></span>Fixed
    </span>`;
  }
  el.innerHTML = html;
}

function buildCalGrid(dates, tasks) {
  calEventTasks = [];
  const totalPx = (CAL_END_HOUR - CAL_START_HOUR) * CAL_HOUR_PX;

  // Group tasks by day key
  const byDay = {};
  for (const d of dates) byDay[calDateKey(d)] = [];
  for (const t of tasks) {
    const key = calDateKey(new Date(t.startTime));
    if (byDay[key]) byDay[key].push(t);
  }

  // ── Header ──
  let headerHtml = '<div class="cal-gutter-header"></div>';
  for (const d of dates) {
    headerHtml += `<div class="cal-day-header">
      <div class="cal-day-name">${DAY_NAMES[d.getDay()]}</div>
      <div>${d.getDate()} ${MONTH_ABBR[d.getMonth()]}</div>
    </div>`;
  }
  document.getElementById('cal-header-row').innerHTML = headerHtml;

  // ── Body ──
  // Gutter
  let gutterHtml = '';
  for (let h = CAL_START_HOUR; h < CAL_END_HOUR; h++) {
    gutterHtml += `<div class="cal-hour-label" style="height:${CAL_HOUR_PX}px">${h}:00</div>`;
  }

  // Day columns
  let daysHtml = '';
  for (const d of dates) {
    const key = calDateKey(d);
    const dayTasks = byDay[key] || [];
    let colHtml = `<div class="cal-day-col" style="height:${totalPx}px">`;

    // Hour lines
    for (let h = 0; h < (CAL_END_HOUR - CAL_START_HOUR); h++) {
      const top = h * CAL_HOUR_PX;
      const major = h % 2 === 0 ? ' cal-hour-line-major' : '';
      colHtml += `<div class="cal-hour-line${major}" style="top:${top}px"></div>`;
    }

    // Events
    for (const t of dayTasks) {
      const start = new Date(t.startTime);
      const end   = new Date(t.endTime);
      const topPx = (start.getHours() + start.getMinutes() / 60 - CAL_START_HOUR) * CAL_HOUR_PX;
      const durMin = (end - start) / 60000;
      const heightPx = Math.max((durMin / 60) * CAL_HOUR_PX, 22);
      const color = t.isFixed ? FIXED_COLOR : (CATEGORY_COLORS[t.categories[0]] || '#9ca3af');

      let badges = t.isFixed
        ? '<span class="cal-badge">Fixed</span>'
        : '<span class="cal-badge">Dynamic</span>';
      if (t.repeating) badges += '<span class="cal-badge cal-badge-repeating">↻</span>';

      const idx = calEventTasks.length;
      calEventTasks.push(t);

      colHtml += `<div class="cal-event" data-tidx="${idx}" style="top:${topPx}px;height:${heightPx}px;background:${color}">
        <div class="cal-event-name">${t.name}</div>
        ${heightPx >= 38 ? `<div class="cal-event-badges">${badges}</div>` : ''}
      </div>`;
    }

    colHtml += '</div>';
    daysHtml += colHtml;
  }

  const body = document.getElementById('cal-body');
  body.innerHTML = `<div class="cal-gutter">${gutterHtml}</div><div class="cal-days">${daysHtml}</div>`;

  // Attach tooltip listeners
  body.querySelectorAll('.cal-event').forEach(el => {
    const task = calEventTasks[+el.dataset.tidx];
    el.addEventListener('mouseenter', e => showTooltip(e, task));
    el.addEventListener('mousemove',  e => positionTooltip(e));
    el.addEventListener('mouseleave', hideTooltip);
  });

  // Scroll to first event or 6:00
  const firstTask = tasks.reduce((acc, t) => {
    const s = new Date(t.startTime);
    return (!acc || s < acc) ? s : acc;
  }, null);
  const scrollTop = firstTask
    ? Math.max(0, (firstTask.getHours() + firstTask.getMinutes()/60 - CAL_START_HOUR - 0.5) * CAL_HOUR_PX)
    : 0;
  document.querySelector('.cal-scroll-wrapper').scrollTop = scrollTop;
}

function renderCalendar(item) {
  const section = document.getElementById('calendar-section');
  if (!item.response || !item.response.tasksTimeline) {
    section.classList.add('hidden');
    return;
  }

  const { request, response } = item;
  const fixedMap   = new Map((request.fixedTasks   || []).map(t => [t.id, t]));
  const dynamicMap = new Map((request.dynamicTasks || []).map(t => [t.id, t]));

  const enriched = response.tasksTimeline.map(entry => {
    const fixed   = fixedMap.get(entry.id);
    const dynamic = dynamicMap.get(entry.id);
    const task    = fixed || dynamic;
    return {
      id:          entry.id,
      startTime:   entry.startTime,
      endTime:     entry.endTime,
      name:        task?.name          || 'Unknown',
      isFixed:     !!fixed,
      categories:  dynamic?.categories || [],
      repeating:   dynamic?.repeating  || null,
      types:       task?.types         || [],
      priority:    task?.priority      ?? null,
      difficulty:  task?.difficulty    ?? null,
      isRequired:  dynamic?.isRequired ?? null,
      deadline:    dynamic?.deadline   || null,
      windowStart: dynamic?.windowStart || null,
      windowEnd:   dynamic?.windowEnd   || null,
    };
  });

  // Build dates array from planning horizon
  const start = new Date(request.planningHorizon.startDate + 'T00:00:00');
  const end   = new Date(request.planningHorizon.endDate   + 'T00:00:00');
  const dates = [];
  for (let d = new Date(start); d <= end; d.setDate(d.getDate() + 1)) {
    dates.push(new Date(d));
  }

  buildCalLegend(enriched);
  buildCalGrid(dates, enriched);
  section.classList.remove('hidden');
}

// ─── Bootstrap ────────────────────────────────────────────────────────────────

document.addEventListener('DOMContentLoaded', () => {
  document.getElementById('horizon-start').addEventListener('input', syncJson);
  document.getElementById('horizon-end').addEventListener('input', syncJson);
  document.getElementById('strategy').addEventListener('input', syncJson);

  document.getElementById('add-fixed-task').addEventListener('click', () => addFixedTask());
  document.getElementById('add-dynamic-task').addEventListener('click', () => addDynamicTask());
  document.getElementById('add-category-window').addEventListener('click', () => addCategoryWindow());
  document.getElementById('add-difficulty-capacity').addEventListener('click', () => addDifficultyCapacity());
  document.getElementById('add-type-preference').addEventListener('click', () => addTypePreference());

  document.getElementById('submit-btn').addEventListener('click', submit);
  document.getElementById('clear-btn').addEventListener('click', clearForm);
  document.getElementById('refresh-ids-btn').addEventListener('click', loadGeneratedIds);

  loadGeneratedIds();

  fetchSchema();
  document.getElementById('copy-schema-btn').addEventListener('click', copySchemaToClipboard);
  document.getElementById('copy-prompt-btn').addEventListener('click', copyPromptToClipboard);

  // JSON textarea → form (debounced to avoid cursor jumping while typing)
  let jsonDebounce = null;
  document.getElementById('json-preview').addEventListener('input', (e) => {
    clearTimeout(jsonDebounce);
    jsonDebounce = setTimeout(() => {
      try {
        renderForm(JSON.parse(e.target.value));
        e.target.classList.remove('json-invalid');
      } catch {
        e.target.classList.add('json-invalid');
      }
    }, 600);
  });

  syncJson();
});
