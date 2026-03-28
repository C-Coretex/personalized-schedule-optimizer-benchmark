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
      priority:   parseInt(el.querySelector('.ft-priority').value)   || 1,
      difficulty: parseInt(el.querySelector('.ft-difficulty').value) || 1,
      types:      [...el.querySelectorAll('.ft-type:checked')].map(cb => cb.value),
      startTime:  toDateTime(el.querySelector('.ft-start').value),
      endTime:    toDateTime(el.querySelector('.ft-end').value),
    })),

    dynamicTasks: [...document.querySelectorAll('#dynamic-tasks-list .task-item')].map(el => ({
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
        <label>Min Days
          <input class="dt-min-days" type="number" min="0" value="${esc(data.repeating?.minDayCount ?? 0)}">
        </label>
        <label>Opt Days
          <input class="dt-opt-days" type="number" min="0" value="${esc(data.repeating?.optDayCount ?? 0)}">
        </label>
        <label>Min Weeks
          <input class="dt-min-weeks" type="number" min="0" value="${esc(data.repeating?.minWeekCount ?? 0)}">
        </label>
        <label>Opt Weeks
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
