import {
  CATEGORY_COLORS, FIXED_COLOR,
  CAL_START_HOUR, CAL_END_HOUR, CAL_HOUR_PX,
  DAY_NAMES, MONTH_ABBR,
} from './constants.js';
import { fmtTime } from './utils.js';

// Lookup table from event index → task object, rebuilt on each render
let calEventTasks = [];

// ─── Tooltip ──────────────────────────────────────────────────────────────────

function buildTooltipHtml(t) {
  const start  = new Date(t.startTime);
  const end    = new Date(t.endTime);
  const durMin = Math.round((end - start) / 60000);
  const durStr = durMin >= 60
    ? `${Math.floor(durMin / 60)}h${durMin % 60 ? ' ' + (durMin % 60) + 'm' : ''}`
    : `${durMin}m`;
  const color = t.isFixed ? FIXED_COLOR : (CATEGORY_COLORS[t.categories[0]] || '#9ca3af');

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

// ─── Legend ───────────────────────────────────────────────────────────────────

function buildCalLegend(tasks) {
  const cats = new Set();
  let hasFixed = false;
  for (const t of tasks) {
    if (t.isFixed) { hasFixed = true; }
    else { for (const c of t.categories) cats.add(c); }
  }
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
  document.getElementById('cal-legend').innerHTML = html;
}

// ─── Grid ─────────────────────────────────────────────────────────────────────

function calDateKey(dt) {
  return `${dt.getFullYear()}-${String(dt.getMonth() + 1).padStart(2, '0')}-${String(dt.getDate()).padStart(2, '0')}`;
}

function hexToRgba(hex, alpha) {
  const r = parseInt(hex.slice(1, 3), 16);
  const g = parseInt(hex.slice(3, 5), 16);
  const b = parseInt(hex.slice(5, 7), 16);
  return `rgba(${r},${g},${b},${alpha})`;
}

function buildCalGrid(dates, tasks, categoryWindows = []) {
  calEventTasks = [];
  const totalPx = (CAL_END_HOUR - CAL_START_HOUR) * CAL_HOUR_PX;

  // Group tasks and category windows by day key
  const byDay = {};
  const cwByDay = {};
  for (const d of dates) {
    const key = calDateKey(d);
    byDay[key]   = [];
    cwByDay[key] = [];
  }
  for (const t  of tasks)           { const key = calDateKey(new Date(t.startTime));       if (byDay[key])   byDay[key].push(t); }
  for (const cw of categoryWindows) { const key = calDateKey(new Date(cw.startDateTime));  if (cwByDay[key]) cwByDay[key].push(cw); }

  // ── Header ──
  let headerHtml = '<div class="cal-gutter-header"></div>';
  for (const d of dates) {
    headerHtml += `<div class="cal-day-header">
      <div class="cal-day-name">${DAY_NAMES[d.getDay()]}</div>
      <div>${d.getDate()} ${MONTH_ABBR[d.getMonth()]}</div>
    </div>`;
  }
  document.getElementById('cal-header-row').innerHTML = headerHtml;

  // ── Gutter (hour labels) ──
  let gutterHtml = '';
  for (let h = CAL_START_HOUR; h < CAL_END_HOUR; h++) {
    gutterHtml += `<div class="cal-hour-label" style="height:${CAL_HOUR_PX}px">${h}:00</div>`;
  }

  // ── Day columns ──
  let daysHtml = '';
  for (const d of dates) {
    const key      = calDateKey(d);
    const dayTasks = byDay[key] || [];
    let colHtml    = `<div class="cal-day-col" style="height:${totalPx}px">`;

    // Hour lines
    for (let h = 0; h < (CAL_END_HOUR - CAL_START_HOUR); h++) {
      const major = h % 2 === 0 ? ' cal-hour-line-major' : '';
      colHtml += `<div class="cal-hour-line${major}" style="top:${h * CAL_HOUR_PX}px"></div>`;
    }

    // Category window backgrounds — stagger labels that share the same top offset
    const labelCountByTop = {};
    for (const cw of (cwByDay[key] || [])) {
      const cwS   = new Date(cw.startDateTime);
      const cwE   = new Date(cw.endDateTime);
      const topPx = Math.max(0, (cwS.getHours() + cwS.getMinutes() / 60 - CAL_START_HOUR) * CAL_HOUR_PX);
      const botPx = Math.min(totalPx, (cwE.getHours() + cwE.getMinutes() / 60 - CAL_START_HOUR) * CAL_HOUR_PX);
      const hPx   = botPx - topPx;
      if (hPx <= 0) continue;

      const color  = CATEGORY_COLORS[cw.category] || '#9ca3af';
      const bg     = hexToRgba(color, 0.09);
      const border = hexToRgba(color, 0.55);

      const topKey   = String(Math.round(topPx));
      const labelIdx = labelCountByTop[topKey] ?? 0;
      labelCountByTop[topKey] = labelIdx + 1;

      const label = hPx >= 22
        ? `<span class="cal-cat-label" style="color:${border};top:${3 + labelIdx * 13}px">${cw.category}</span>`
        : '';
      colHtml += `<div class="cal-cat-bg" style="top:${topPx}px;height:${hPx}px;background:${bg};border-left:3px solid ${border}">${label}</div>`;
    }

    // Events
    for (const t of dayTasks) {
      const start     = new Date(t.startTime);
      const end       = new Date(t.endTime);
      const topPx     = (start.getHours() + start.getMinutes() / 60 - CAL_START_HOUR) * CAL_HOUR_PX;
      const heightPx  = Math.max(((end - start) / 60000 / 60) * CAL_HOUR_PX, 22);
      const color     = t.isFixed ? FIXED_COLOR : (CATEGORY_COLORS[t.categories[0]] || '#9ca3af');

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

  // Scroll to just before the first event
  const firstTask = tasks.reduce((acc, t) => {
    const s = new Date(t.startTime);
    return (!acc || s < acc) ? s : acc;
  }, null);
  const scrollTop = firstTask
    ? Math.max(0, (firstTask.getHours() + firstTask.getMinutes() / 60 - CAL_START_HOUR - 0.5) * CAL_HOUR_PX)
    : 0;
  document.querySelector('.cal-scroll-wrapper').scrollTop = scrollTop;
}

// ─── Public entry point ───────────────────────────────────────────────────────

export function renderCalendar(item) {
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
      name:        task?.name           || 'Unknown',
      isFixed:     !!fixed,
      categories:  dynamic?.categories  || [],
      repeating:   dynamic?.repeating   || null,
      types:       task?.types          || [],
      priority:    task?.priority       ?? null,
      difficulty:  task?.difficulty     ?? null,
      isRequired:  dynamic?.isRequired  ?? null,
      deadline:    dynamic?.deadline    || null,
      windowStart: dynamic?.windowStart || null,
      windowEnd:   dynamic?.windowEnd   || null,
    };
  });

  // Build dates array spanning the planning horizon
  const start = new Date(request.planningHorizon.startDate + 'T00:00:00');
  const end   = new Date(request.planningHorizon.endDate   + 'T00:00:00');
  const dates = [];
  for (let d = new Date(start); d <= end; d.setDate(d.getDate() + 1)) {
    dates.push(new Date(d));
  }

  buildCalLegend(enriched);
  buildCalGrid(dates, enriched, request.categoryWindows || []);
  section.classList.remove('hidden');
}
