import { toDateTime, toTimeOnly } from './utils.js';
import {
  addFixedTask, addDynamicTask, addCategoryWindow,
  addDifficultyCapacity, addTypePreference,
} from './items.js';

// ─── Read state from DOM ──────────────────────────────────────────────────────

export function readState() {
  return {
    planningHorizon: {
      startDate: document.getElementById('horizon-start').value || null,
      endDate:   document.getElementById('horizon-end').value   || null,
    },
    difficultTaskSchedulingStrategy: document.getElementById('strategy').value,
    optimizationTimeInSeconds: parseInt(document.getElementById('opt-time').value) || 15,

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
        minDayCount:  parseInt(el.querySelector('.dt-min-days').value)  || 0,
        optDayCount:  parseInt(el.querySelector('.dt-opt-days').value)  || 0,
        minWeekCount: parseInt(el.querySelector('.dt-min-weeks').value) || 0,
        optWeekCount: parseInt(el.querySelector('.dt-opt-weeks').value) || 0,
      } : null,
    })),

    categoryWindows: [...document.querySelectorAll('#category-windows-list .item')].map(el => ({
      category:      el.querySelector('.cw-category').value,
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

export function syncJson() {
  const ta = document.getElementById('json-preview');
  ta.value = JSON.stringify(readState(), null, 2);
  ta.classList.remove('json-invalid');
}

// ─── Render form from a state object ─────────────────────────────────────────

export function renderForm(s) {
  document.getElementById('horizon-start').value = s.planningHorizon?.startDate ?? '';
  document.getElementById('horizon-end').value   = s.planningHorizon?.endDate   ?? '';
  document.getElementById('strategy').value      = s.difficultTaskSchedulingStrategy ?? 'Cluster';
  const optTime = s.optimizationTimeInSeconds ?? 15;
  document.getElementById('opt-time').value       = optTime;
  document.getElementById('opt-time-value').value = optTime;

  document.getElementById('fixed-tasks-list').innerHTML          = '';
  document.getElementById('dynamic-tasks-list').innerHTML        = '';
  document.getElementById('category-windows-list').innerHTML     = '';
  document.getElementById('difficulty-capacities-list').innerHTML = '';
  document.getElementById('type-preferences-list').innerHTML     = '';

  (s.fixedTasks           ?? []).forEach(t => addFixedTask(t,           true));
  (s.dynamicTasks         ?? []).forEach(t => addDynamicTask(t,         true));
  (s.categoryWindows      ?? []).forEach(w => addCategoryWindow(w,      true));
  (s.difficultyCapacities ?? []).forEach(d => addDifficultyCapacity(d,  true));
  (s.taskTypePreferences  ?? []).forEach(p => addTypePreference(p,      true));
  // Do not call syncJson — the textarea already holds the parsed value.
}

// ─── Default / Clear state ────────────────────────────────────────────────────

export const DEFAULT_STATE = {
  planningHorizon: { startDate: null, endDate: null },
  difficultTaskSchedulingStrategy: 'Cluster',
  optimizationTimeInSeconds: 15,
  fixedTasks: [],
  dynamicTasks: [],
  categoryWindows: [],
  difficultyCapacities: [],
  taskTypePreferences: [],
};

export function clearForm() {
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

export function showHistoryBanner(id) {
  const el = document.getElementById('history-banner');
  el.textContent = `Viewing history entry — ${id}`;
  el.classList.remove('hidden');
}

export function hideHistoryBanner() {
  document.getElementById('history-banner').classList.add('hidden');
}
