import react from '@vitejs/plugin-react'
import { defineConfig } from 'vite'

// https://vite.dev/config/
export default defineConfig({
  plugins: [react()],
  server: {
    // In production, Vercel's own rewrite (frontend/vercel.json) sends
    // /api/* to the Python function on the same origin. Locally there's
    // no Vercel in front of us, so this proxy fakes the same same-origin
    // setup by forwarding to a Flask dev server run separately (see
    // frontend/api/index.py's __main__ block).
    proxy: {
      '/api': 'http://127.0.0.1:5328',
    },
  },
})
