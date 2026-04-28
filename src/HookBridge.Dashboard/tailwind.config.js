/** @type {import('tailwindcss').Config} */
export default {
  content: ['./index.html', './src/**/*.{ts,tsx}'],
  theme: {
    extend: {
      colors: {
        primary: '#2563eb',
        'primary-dark': '#1d4ed8',
        'primary-soft': '#eff6ff',
        'primary-border': '#bfdbfe',
        background: '#f4f7fb',
        surface: '#ffffff',
        border: '#dbe3ef',
        text: '#0f172a',
        'text-muted': '#475569',
        success: '#166534',
        'success-bg': '#dcfce7',
        'success-border': '#86efac',
        warning: '#92400e',
        'warning-bg': '#fef3c7',
        'warning-border': '#fde68a',
        error: '#b91c1c',
        'error-bg': '#fee2e2',
        'error-border': '#fca5a5',
        brand: {
          50: '#eff6ff',
          600: '#2563eb',
          700: '#1d4ed8'
        }
      },
      boxShadow: {
        soft: '0 8px 24px -18px rgba(15, 23, 42, 0.35)'
      }
    }
  },
  plugins: []
};
