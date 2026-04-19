import { TASK_TYPES, CATEGORIES, CATEGORY_COLORS, MONTH_ABBR } from './constants.js';
import { esc, fromDateTime, fromTimeOnly, fmtTime } from './utils.js';
import { syncJson } from './state.js';

// ─── Shared helpers ───────────────────────────────────────────────────────────

function makeItemHeader() {
  return `
    <div class="item-header">
      <span class="item-toggle">▶</span>
      <div class="item-summary"></div>
      <button type="button" class="btn-remove">✕</button>
    </div>`;
}

function checkboxGroupHTML(options, cssClass, checkedValues = []) {
  return `<div class="checkbox-group">
    ${options.map(o => `
      <label><input class="${cssClass}" type="checkbox" value="${o.value}"
        ${checkedValues.includes(o.value) ? 'checked' : ''}> ${o.label}</label>
    `).join('')}
  </div>`;
}

/**
 * Attaches input/change listeners for all inputs and selects inside `itemEl`,
 * plus click listeners for the item-header (toggle) and remove button.
 */
function attachItemListeners(itemEl, refreshFn) {
  itemEl.querySelectorAll('input, select').forEach(el => {
    el.addEventListener('input',  () => { syncJson(); refreshFn(itemEl); });
    el.addEventListener('change', () => { syncJson(); refreshFn(itemEl); });
  });
  itemEl.querySelector('.item-header').addEventListener('click', () => toggleItem(itemEl));
  itemEl.querySelector('.item-header .btn-remove').addEventListener('click', e => {
    e.stopPropagation();
    removeItem(itemEl);
  });
}

// ─── Item collapse / remove ───────────────────────────────────────────────────

export function toggleItem(itemEl) {
  itemEl.classList.toggle('collapsed');
}

export function removeItem(itemEl) {
  itemEl.remove();
  syncJson();
}

// ─── Summary refresh helpers ──────────────────────────────────────────────────

function fmtDateTime(v) {
  if (!v) return '';
  const d = new Date(v);
  return `${d.getDate()} ${MONTH_ABBR[d.getMonth()]}, ${fmtTime(d)}`;
}

function refreshFixedSummary(div) {
  const name  = div.querySelector('.ft-name')?.value  || 'Unnamed';
  const start = div.querySelector('.ft-start')?.value || '';
  const end   = div.querySelector('.ft-end')?.value   || '';
  const prio  = div.querySelector('.ft-priority')?.value;
  const diff  = div.querySelector('.ft-difficulty')?.value;
  const types = [...div.querySelectorAll('.ft-type:checked')].map(cb => cb.value);

  let html = `<span class="item-badge item-badge-fixed">Fixed</span>
    <span class="sum-name">${esc(name)}</span>`;

  if (start && end) {
    const s = new Date(start), e = new Date(end);
    const timeChip = s.toDateString() === e.toDateString()
      ? `${fmtDateTime(start)} – ${fmtTime(e)}`
      : `${fmtDateTime(start)} – ${fmtDateTime(end)}`;
    html += `<span class="sum-chip sum-chip-time">${timeChip}</span>`;
  } else if (start) {
    html += `<span class="sum-chip sum-chip-time">${fmtDateTime(start)}</span>`;
  }
  if (prio) html += `<span class="sum-chip">P${prio}</span>`;
  if (diff) html += `<span class="sum-chip sum-chip-diff">D${diff}</span>`;
  types.forEach(t => { html += `<span class="sum-chip">${t}</span>`; });

  div.querySelector('.item-summary').innerHTML = html;
}

function refreshDynamicSummary(div) {
  const name     = div.querySelector('.dt-name')?.value     || 'Unnamed';
  const dur      = div.querySelector('.dt-duration')?.value || '';
  const prio     = div.querySelector('.dt-priority')?.value;
  const diff     = div.querySelector('.dt-difficulty')?.value;
  const required = div.querySelector('.dt-required')?.checked;
  const wStart   = div.querySelector('.dt-window-start')?.value;
  const wEnd     = div.querySelector('.dt-window-end')?.value;
  const deadline = div.querySelector('.dt-deadline')?.value;
  const cats     = [...div.querySelectorAll('.dt-category:checked')].map(cb => cb.value);
  const types    = [...div.querySelectorAll('.dt-type:checked')].map(cb => cb.value);
  const isRep    = div.querySelector('.dt-repeating-toggle')?.checked;
  const minDay   = div.querySelector('.dt-min-days')?.value  || '0';
  const optDay   = div.querySelector('.dt-opt-days')?.value  || '0';
  const minWeek  = div.querySelector('.dt-min-weeks')?.value || '0';
  const optWeek  = div.querySelector('.dt-opt-weeks')?.value || '0';

  let html = `<span class="item-badge item-badge-dynamic">Dynamic</span>
    <span class="sum-name">${esc(name)}</span>`;
  if (dur) html += `<span class="sum-chip">${dur}min</span>`;
  if (wStart || wEnd) {
    const ws = wStart ? wStart.slice(0, 5) : '–';
    const we = wEnd   ? wEnd.slice(0, 5)   : '–';
    html += `<span class="sum-chip sum-chip-time">${ws} – ${we}</span>`;
  }
  if (deadline) html += `<span class="sum-chip sum-chip-deadline">⚑ ${fmtDateTime(deadline)}</span>`;
  cats.forEach(cat => {
    const color = CATEGORY_COLORS[cat] || '#9ca3af';
    html += `<span class="sum-chip sum-chip-cat" style="background:${color}">${cat}</span>`;
  });
  if (required) html += `<span class="sum-chip sum-chip-required">Required</span>`;
  if (prio) html += `<span class="sum-chip">P${prio}</span>`;
  if (diff) html += `<span class="sum-chip sum-chip-diff">D${diff}</span>`;
  types.forEach(t => { html += `<span class="sum-chip">${t}</span>`; });
  if (isRep) html += `<span class="sum-chip sum-chip-repeat">↺ day ${minDay}–${optDay} · wk ${minWeek}–${optWeek}</span>`;

  div.querySelector('.item-summary').innerHTML = html;
}

function refreshCwSummary(div) {
  const cat   = div.querySelector('.cw-category')?.value || '';
  const start = div.querySelector('.cw-start')?.value    || '';
  const end   = div.querySelector('.cw-end')?.value      || '';
  const color = CATEGORY_COLORS[cat] || '#9ca3af';

  const fmtDt = v => {
    if (!v) return '–';
    const d = new Date(v);
    return `${d.getDate()} ${MONTH_ABBR[d.getMonth()]} ${fmtTime(d)}`;
  };

  let timeStr;
  if (start && end) {
    const s = new Date(start), e = new Date(end);
    const tS = fmtTime(s), tE = fmtTime(e);
    timeStr = s.toDateString() === e.toDateString()
      ? `${s.getDate()} ${MONTH_ABBR[s.getMonth()]}, ${tS} – ${tE}`
      : `${fmtDt(start)} – ${fmtDt(end)}`;
  } else {
    timeStr = `${fmtDt(start)} – ${fmtDt(end)}`;
  }

  div.querySelector('.item-summary').innerHTML = `
    <span class="sum-chip sum-chip-cat" style="background:${color}">${esc(cat)}</span>
    <span class="sum-chip sum-chip-time">${timeStr}</span>`;
}

function refreshDcSummary(div) {
  const date = div.querySelector('.dc-date')?.value     || '–';
  const cap  = div.querySelector('.dc-capacity')?.value || '0';
  div.querySelector('.item-summary').innerHTML =
    `<span class="sum-date">${esc(date)}</span><span class="sum-chip">cap ${esc(cap)}</span>`;
}

function refreshTpSummary(div) {
  const date = div.querySelector('.tp-date')?.value || '–';
  let html = `<span class="sum-date">${esc(date)}</span>`;
  div.querySelectorAll('.tw-row').forEach(row => {
    const type   = row.querySelector('.tw-type')?.value;
    const weight = row.querySelector('.tw-weight')?.value || '1';
    if (type) html += `<span class="sum-chip">${esc(type)} ×${esc(weight)}</span>`;
  });
  div.querySelector('.item-summary').innerHTML = html;
}

// ─── Type weight row ──────────────────────────────────────────────────────────

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
      <button type="button" class="btn-remove">✕</button>
    </div>`;
}

function createTypeWeightRow(data, prefItemEl) {
  const tmp = document.createElement('div');
  tmp.innerHTML = typeWeightHTML(data);
  const row = tmp.firstElementChild;
  row.querySelectorAll('input, select').forEach(el => {
    el.addEventListener('input',  () => { syncJson(); refreshTpSummary(prefItemEl); });
    el.addEventListener('change', () => { syncJson(); refreshTpSummary(prefItemEl); });
  });
  row.querySelector('.btn-remove').addEventListener('click', () => {
    row.remove();
    syncJson();
    refreshTpSummary(prefItemEl);
  });
  return row;
}

export function addTypeWeight(prefItemEl) {
  prefItemEl.querySelector('.tw-list').appendChild(createTypeWeightRow({}, prefItemEl));
  syncJson();
  refreshTpSummary(prefItemEl);
}

// ─── Add Fixed Task ───────────────────────────────────────────────────────────

export function addFixedTask(data = {}, suppressSync = false) {
  const div = document.createElement('div');
  div.className = 'task-item' + (suppressSync ? ' collapsed' : '');
  div.innerHTML = `
    ${makeItemHeader()}
    <div class="item-body">
      <div class="row">
        <label>Name
          <input class="ft-name" type="text" value="${esc(data.name ?? '')}" required>
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
          ${checkboxGroupHTML(TASK_TYPES, 'ft-type', data.types ?? [])}
        </label>
      </div>
    </div>`;

  attachItemListeners(div, refreshFixedSummary);
  refreshFixedSummary(div);
  document.getElementById('fixed-tasks-list').appendChild(div);
  if (!suppressSync) syncJson();
}

// ─── Add Dynamic Task ─────────────────────────────────────────────────────────

export function addDynamicTask(data = {}, suppressSync = false) {
  const hasRepeating = data.repeating != null;
  const div = document.createElement('div');
  div.className = 'task-item' + (suppressSync ? ' collapsed' : '');
  div.innerHTML = `
    ${makeItemHeader()}
    <div class="item-body">
      <div class="row">
        <label>Name
          <input class="dt-name" type="text" value="${esc(data.name ?? '')}" required>
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
          ${checkboxGroupHTML(CATEGORIES, 'dt-category', data.categories ?? [])}
        </label>
      </div>
      <div class="row">
        <label class="inline-check">Required
          <input class="dt-required" type="checkbox" ${data.isRequired ? 'checked' : ''}>
        </label>
        <label>Priority (1–5)
          <input class="dt-priority" type="number" min="1" max="5" value="${esc(data.priority ?? 1)}" required>
        </label>
        <label>Difficulty (1–10)
          <input class="dt-difficulty" type="number" min="1" max="10" value="${esc(data.difficulty ?? 1)}" required>
        </label>
      </div>
      <div class="row">
        <label>Types
          ${checkboxGroupHTML(TASK_TYPES, 'dt-type', data.types ?? [])}
        </label>
      </div>
      <div class="row">
        <label class="inline-check">Repeating
          <input class="dt-repeating-toggle" type="checkbox" ${hasRepeating ? 'checked' : ''}>
        </label>
      </div>
      <div class="repeating-fields" style="display:${hasRepeating ? 'grid' : 'none'}">
        <div class="row">
          <label>Min/day
            <input class="dt-min-days" type="number" min="0" value="${esc(data.repeating?.minDayCount ?? 0)}">
          </label>
          <label>Opt/day
            <input class="dt-opt-days" type="number" min="0" value="${esc(data.repeating?.optDayCount ?? 0)}">
          </label>
          <label>Min/week
            <input class="dt-min-weeks" type="number" min="0" value="${esc(data.repeating?.minWeekCount ?? 0)}">
          </label>
          <label>Opt/week
            <input class="dt-opt-weeks" type="number" min="0" value="${esc(data.repeating?.optWeekCount ?? 0)}">
          </label>
        </div>
      </div>
    </div>`;

  div.querySelector('.dt-repeating-toggle').addEventListener('change', function () {
    div.querySelector('.repeating-fields').style.display = this.checked ? 'grid' : 'none';
    syncJson();
  });

  attachItemListeners(div, refreshDynamicSummary);
  refreshDynamicSummary(div);
  document.getElementById('dynamic-tasks-list').appendChild(div);
  if (!suppressSync) syncJson();
}

// ─── Add Category Window ──────────────────────────────────────────────────────

export function addCategoryWindow(data = {}, suppressSync = false) {
  const div = document.createElement('div');
  div.className = 'item' + (suppressSync ? ' collapsed' : '');
  div.innerHTML = `
    ${makeItemHeader()}
    <div class="item-body">
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
    </div>`;

  attachItemListeners(div, refreshCwSummary);
  refreshCwSummary(div);
  document.getElementById('category-windows-list').appendChild(div);
  if (!suppressSync) syncJson();
}

// ─── Add Difficulty Capacity ──────────────────────────────────────────────────

export function addDifficultyCapacity(data = {}, suppressSync = false) {
  const div = document.createElement('div');
  div.className = 'item' + (suppressSync ? ' collapsed' : '');
  div.innerHTML = `
    ${makeItemHeader()}
    <div class="item-body">
      <div class="row">
        <label>Date
          <input class="dc-date" type="date" value="${esc(data.date ?? '')}" required>
        </label>
        <label>Capacity
          <input class="dc-capacity" type="number" min="0" value="${esc(data.capacity ?? 0)}">
        </label>
      </div>
    </div>`;

  attachItemListeners(div, refreshDcSummary);
  refreshDcSummary(div);
  document.getElementById('difficulty-capacities-list').appendChild(div);
  if (!suppressSync) syncJson();
}

// ─── Add Task Type Preference ─────────────────────────────────────────────────

export function addTypePreference(data = {}, suppressSync = false) {
  const div = document.createElement('div');
  div.className = 'pref-item item' + (suppressSync ? ' collapsed' : '');
  div.innerHTML = `
    ${makeItemHeader()}
    <div class="item-body">
      <div class="row">
        <label>Date
          <input class="tp-date" type="date" value="${esc(data.date ?? '')}" required>
        </label>
        <button type="button" class="btn-add">+ Weight</button>
      </div>
      <div class="tw-list"></div>
    </div>`;

  // Wire general listeners (before adding tw-rows so they don't get double listeners)
  attachItemListeners(div, refreshTpSummary);

  // Add "+" weight button listener
  div.querySelector('.btn-add').addEventListener('click', e => {
    e.stopPropagation();
    addTypeWeight(div);
  });

  // Add initial type weight rows (each gets its own listeners via createTypeWeightRow)
  const twList = div.querySelector('.tw-list');
  (data.preferences ?? []).forEach(p => twList.appendChild(createTypeWeightRow(p, div)));

  refreshTpSummary(div);
  document.getElementById('type-preferences-list').appendChild(div);
  if (!suppressSync) syncJson();
}
