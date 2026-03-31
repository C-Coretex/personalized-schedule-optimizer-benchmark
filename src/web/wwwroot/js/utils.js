// ─── HTML helpers ─────────────────────────────────────────────────────────────

/** Escape a value for use inside an HTML attribute */
export function esc(v) {
  return String(v ?? '').replace(/&/g, '&amp;').replace(/"/g, '&quot;').replace(/</g, '&lt;').replace(/>/g, '&gt;');
}

// ─── Date / time conversion ───────────────────────────────────────────────────

/** "2024-01-15T09:00" → "2024-01-15T09:00:00" */
export function toDateTime(v) {
  if (!v) return null;
  return v.length === 16 ? v + ':00' : v;
}

/** "09:00" → "09:00:00" */
export function toTimeOnly(v) {
  if (!v) return null;
  return v.length === 5 ? v + ':00' : v;
}

/** "2024-01-15T09:00:00" → "2024-01-15T09:00" (for datetime-local inputs) */
export function fromDateTime(v) {
  return v ? String(v).slice(0, 16) : '';
}

/** "09:00:00" → "09:00" (for time inputs) */
export function fromTimeOnly(v) {
  return v ? String(v).slice(0, 5) : '';
}

/** Format a Date object as "HH:MM" */
export function fmtTime(d) {
  return `${String(d.getHours()).padStart(2, '0')}:${String(d.getMinutes()).padStart(2, '0')}`;
}
