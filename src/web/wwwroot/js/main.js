import { syncJson, renderForm, clearForm } from './state.js';
import {
  addFixedTask, addDynamicTask, addCategoryWindow,
  addDifficultyCapacity, addTypePreference,
} from './items.js';
import {
  submit, loadGeneratedIds, fetchSchema,
  copySchemaToClipboard, copyPromptToClipboard, loadSampleJson,
} from './api.js';

function calcDefaultOptTime(startDate, endDate) {
  if (!startDate || !endDate) return 15;
  const days = Math.round((new Date(endDate) - new Date(startDate)) / 86400000) + 1;
  return days >= 30 ? 30 : 15;
}

document.addEventListener('DOMContentLoaded', () => {
  // Planning horizon & strategy
  let userOverrodeOptTime = false;

  function onHorizonChange() {
    if (!userOverrodeOptTime) {
      const start = document.getElementById('horizon-start').value;
      const end   = document.getElementById('horizon-end').value;
      const t = calcDefaultOptTime(start, end);
      document.getElementById('opt-time').value = t;
      document.getElementById('opt-time-value').textContent = `${t}s`;
    }
    syncJson();
  }

  document.getElementById('horizon-start').addEventListener('input', onHorizonChange);
  document.getElementById('horizon-end').addEventListener('input', onHorizonChange);
  document.getElementById('strategy').addEventListener('input', syncJson);

  // Optimization time slider
  document.getElementById('opt-time').addEventListener('input', e => {
    const val = parseInt(e.target.value);
    document.getElementById('opt-time-value').textContent = `${val}s`;
    const start = document.getElementById('horizon-start').value;
    const end   = document.getElementById('horizon-end').value;
    userOverrodeOptTime = val !== calcDefaultOptTime(start, end);
    syncJson();
  });

  // Reset optimization time to auto-calculated default
  document.getElementById('opt-time-reset').addEventListener('click', () => {
    const start = document.getElementById('horizon-start').value;
    const end   = document.getElementById('horizon-end').value;
    const t = calcDefaultOptTime(start, end);
    document.getElementById('opt-time').value = t;
    document.getElementById('opt-time-value').textContent = `${t}s`;
    userOverrodeOptTime = false;
    syncJson();
  });

  // Add item buttons
  document.getElementById('add-fixed-task').addEventListener('click',        () => addFixedTask());
  document.getElementById('add-dynamic-task').addEventListener('click',      () => addDynamicTask());
  document.getElementById('add-category-window').addEventListener('click',   () => addCategoryWindow());
  document.getElementById('add-difficulty-capacity').addEventListener('click', () => addDifficultyCapacity());
  document.getElementById('add-type-preference').addEventListener('click',   () => addTypePreference());

  // Form actions
  document.getElementById('submit-btn').addEventListener('click',      submit);
  document.getElementById('clear-btn').addEventListener('click',       clearForm);
  document.getElementById('refresh-ids-btn').addEventListener('click', loadGeneratedIds);

  // Schema / clipboard
  fetchSchema();
  document.getElementById('load-sample-btn').addEventListener('click', loadSampleJson);
  document.getElementById('copy-schema-btn').addEventListener('click', copySchemaToClipboard);
  document.getElementById('copy-prompt-btn').addEventListener('click', copyPromptToClipboard);

  // JSON textarea → form (debounced to avoid cursor jumping while typing)
  let jsonDebounce = null;
  document.getElementById('json-preview').addEventListener('input', e => {
    clearTimeout(jsonDebounce);
    jsonDebounce = setTimeout(() => {
      try {
        renderForm(JSON.parse(e.target.value));
        syncJson();
        e.target.classList.remove('json-invalid');
      } catch {
        e.target.classList.add('json-invalid');
      }
    }, 600);
  });

  loadGeneratedIds();
  setInterval(loadGeneratedIds, 1000);
  syncJson();
});
