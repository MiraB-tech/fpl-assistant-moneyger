import react from '@vitejs/plugin-react'
import { defineConfig } from 'vite'

// https://vite.dev/config/
export default defineConfig({
  plugins: [react()],
  server: {
    // The .NET backend (backend/) runs separately via `dotnet run` on this
    // port. Proxying makes the browser see everything as same-origin, so
    // the auth cookie works without extra CORS configuration.
    proxy: {
      '/api': 'http://localhost:5251',
    },
  },
})
