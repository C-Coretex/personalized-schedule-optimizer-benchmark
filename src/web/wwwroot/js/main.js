import { syncJson, renderForm, clearForm } from './state.js';
import {
  addFixedTask, addDynamicTask, addCategoryWindow,
  addDifficultyCapacity, addTypePreference,
} from './items.js';
import {
  submit, loadGeneratedIds, fetchSchema,
  copySchemaToClipboard, copyPromptToClipboard,
} from './api.js';

document.addEventListener('DOMContentLoaded', () => {
  // Planning horizon & strategy
  document.getElementById('horizon-start').addEventListener('input', syncJson);
  document.getElementById('horizon-end').addEventListener('input', syncJson);
  document.getElementById('strategy').addEventListener('input', syncJson);

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
  document.getElementById('copy-schema-btn').addEventListener('click', copySchemaToClipboard);
  document.getElementById('copy-prompt-btn').addEventListener('click', copyPromptToClipboard);

  // JSON textarea → form (debounced to avoid cursor jumping while typing)
  let jsonDebounce = null;
  document.getElementById('json-preview').addEventListener('input', e => {
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

  loadGeneratedIds();
  syncJson();
});
