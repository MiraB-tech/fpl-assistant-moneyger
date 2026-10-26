import react from '@vitejs/plugin-react'
import { defineConfig } from 'vite'

// https://vite.dev/config/
export default defineConfig({
  plugins: [react()],
  server: {
    // Always use 5173, and fail loudly if something else already has it.
    // The backend only accepts requests from http://localhost:5173 (its
    // AllowedOrigin setting), so silently moving to 5174 would make every
    // button that changes data fail with a 403.
    port: 5173,
    strictPort: true,
    // The .NET backend (backend/) runs separately via `dotnet run` on this
    // port. Proxying makes the browser see everything as same-origin, so
    // the auth cookie works without extra CORS configuration.
    proxy: {
      '/api': 'http://localhost:5251',
    },
  },
})
