import tailwindcss from '@tailwindcss/vite'
import react from '@vitejs/plugin-react'
import { defineConfig } from 'vitest/config'

// Dev: the SPA runs on Vite and proxies /api to the Api, so the session cookie stays same-origin.
// Build: output goes to the Api's wwwroot, which serves it with a fallback to index.html (door 9).
export default defineConfig({
  plugins: [react(), tailwindcss()],
  server: {
    proxy: {
      '/api': 'http://localhost:5295',
    },
  },
  build: {
    outDir: '../BuildYourOwnAI.Api/wwwroot',
    emptyOutDir: true,
  },
  test: {
    environment: 'happy-dom',
    setupFiles: ['./src/test/setup.ts'],
  },
})
