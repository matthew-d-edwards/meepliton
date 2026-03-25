import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
import path from 'path'

// When running under Aspire, it injects the API's actual address via this env var.
// Fall back to the conventional local port for standalone `vite dev` runs.
const apiTarget = process.env['services__api__http__0'] ?? 'http://localhost:5000'

export default defineConfig({
  plugins: [react()],
  resolve: {
    alias: {
      '@meepliton/contracts': path.resolve(__dirname, '../../packages/contracts/src'),
      '@meepliton/ui': path.resolve(__dirname, '../../packages/ui/src'),
    },
  },
  server: {
    port: 5173,
    strictPort: true,   // fail loudly if 5173 is taken rather than silently picking another port
    proxy: {
      '/api': {
        target: apiTarget,
        changeOrigin: true,
      },
      '/hubs': {
        target: apiTarget,
        changeOrigin: true,
        ws: true,
      },
    },
  },
})
