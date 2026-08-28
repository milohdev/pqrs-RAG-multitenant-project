import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

// El proxy evita depender del CORS del backend en desarrollo: el navegador habla
// siempre con localhost:5173 y Vite reenvía /api y /hubs (incl. WebSocket de
// SignalR) al backend en localhost:8080.
export default defineConfig({
  plugins: [react()],
  server: {
    proxy: {
      '/api': { target: 'http://localhost:8080', changeOrigin: true },
      '/hubs': { target: 'http://localhost:8080', ws: true, changeOrigin: true },
    },
  },
})
