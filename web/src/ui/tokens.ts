/**
 * Typed mirror of tokens.css (D1 Phase 1) — for components that need token
 * VALUES (charts, canvas, computed styles). CSS should reference the custom
 * properties directly; keep the two files in lockstep.
 */
export const tokens = {
  color: {
    teal: '#336374',
    tealDark: '#27505f',
    tealNav: '#25586b',
    accent: '#2c5c6e',
    cream: '#f3efe9',
    card: '#f6f4ef',
    ink: '#2b2b2b',
    muted: '#7d8a93',
    line: '#e4ded2',
    soft: '#f7f5f1',
    clay: '#b5836a',
    clayBg: '#f4e9e2',
    sage: '#7e9b6b',
    sageBg: '#e9efe2',
    velvet: '#8a7298',
    velvetBg: '#ece6f1',
    labelInk: '#5a6670',
  },
  /** Status tones: dot (badge), bg (pill/ribbon wash), ink (text on wash). */
  tone: {
    amber: { base: '#e08a3c', dot: '#d2952f', bg: '#fbefdf', ink: '#9a5a1c', line: '#f0d9bf' },
    green: { base: '#3f8f5f', dot: '#2f9e6e', bg: '#e7f2eb', ink: '#256b41', line: '#cfe6d6' },
    blue: { dot: '#3f78c2', bg: '#e8f0f8', ink: '#2d557f' },
    red: { base: '#c5564b', dot: '#cf5149', bg: '#fbe9e7', ink: '#9c3a30', line: '#f0c9c4' },
    grey: { dot: '#aab0b6', bg: '#eee9e1', ink: '#6f6a60' },
    teal: { base: '#336374', dot: '#336374', bg: '#e3edf0', ink: '#27505f' },
  },
  font: {
    sans: "'Inter', -apple-system, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif",
    serif: "Georgia, 'Times New Roman', serif",
    mono: "'JetBrains Mono', ui-monospace, SFMono-Regular, Menlo, monospace",
    sizeBody: 14,
    sizeControl: 13,
    sizeLabel: 11.5,
    sizeBadge: 12,
    sizePill: 11,
  },
  radius: { card: 0, control: 9, modal: 16, pill: 20 },
  shadow: {
    card: '0 2px 10px rgba(51, 99, 119, 0.08)',
    modal: '0 8px 30px rgba(51, 99, 119, 0.16)',
    focusRing: '0 0 0 3px rgba(51, 99, 119, 0.13)',
  },
  space: { field: 14, gridGap: 16 },
} as const

export type Tone = keyof typeof tokens.tone
