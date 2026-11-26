// Mirrors the design tokens of src/web/src/index.css (AD-017). Keys are the CSS names without `--color-`.
// The only file of the app that may hold a literal color; src/test/guardrails.test.ts enforces it.
export const colors = {
  fog: '#f3f5f8',
  surface: '#ffffff',
  ink: '#16202e',
  muted: '#5e6b7c',
  line: '#d9dfe7',
  route: '#0e7c66',
  'route-soft': '#e2f1ec',
  danger: '#b42318',
  'danger-soft': '#fdecea',
  success: '#15803d',
  'success-soft': '#e7f5ec',
  'lane-1': '#c2410c',
  'lane-2': '#6d28d9',
  'lane-3': '#1d4ed8',
  'lane-4': '#b45309',
  'lane-5': '#be185d',
  'lane-6': '#15803d',
} as const

// Each weight is its own family in React Native; `fontWeight` would fake it on top of the loaded file.
export const fonts = {
  display: 'BricolageGrotesque_700Bold',
  sans: 'InstrumentSans_400Regular',
  sansMedium: 'InstrumentSans_500Medium',
  mono: 'JetBrainsMono_400Regular',
} as const
