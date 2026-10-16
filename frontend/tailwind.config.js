/** @type {import('tailwindcss').Config} */
export default {
  darkMode: ['class'],
  content: ['./index.html', './src/**/*.{ts,tsx}'],
  theme: {
    extend: {
      keyframes: {
        'overlay-show': {
          from: { opacity: '0' },
          to: { opacity: '1' },
        },
        'overlay-hide': {
          from: { opacity: '1' },
          to: { opacity: '0' },
        },
        'content-show': {
          from: { opacity: '0', transform: 'translate(-50%, -48%) scale(0.96)' },
          to: { opacity: '1', transform: 'translate(-50%, -50%) scale(1)' },
        },
        'content-hide': {
          from: { opacity: '1', transform: 'translate(-50%, -50%) scale(1)' },
          to: { opacity: '0', transform: 'translate(-50%, -48%) scale(0.96)' },
        },
      },
      animation: {
        'overlay-show': 'overlay-show 0.15s ease',
        'overlay-hide': 'overlay-hide 0.1s ease',
        'content-show': 'content-show 0.18s cubic-bezier(0.16, 1, 0.3, 1)',
        'content-hide': 'content-hide 0.12s ease',
      },
    },
  },
  corePlugins: {
    preflight: false,
  },
  plugins: [],
}
