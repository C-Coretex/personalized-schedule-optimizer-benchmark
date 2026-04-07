// ─── Task / Category enums ────────────────────────────────────────────────────

export const TASK_TYPES = [
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

export const CATEGORIES = [
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

// ─── Optimizer labels ─────────────────────────────────────────────────────────

export const OPTIMIZER_LABELS = {
  Specialized: 'Specialized',
  OrTools:     'OR-Tools',
  Timefold:    'Timefold',
};

// ─── Calendar colors & display constants ──────────────────────────────────────

export const CATEGORY_COLORS = {
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

export const FIXED_COLOR    = '#94a3b8';
export const CAL_START_HOUR = 6;
export const CAL_END_HOUR   = 23;
export const CAL_HOUR_PX    = 60;

export const DAY_NAMES  = ['Sun', 'Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat'];
export const MONTH_ABBR = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'];
