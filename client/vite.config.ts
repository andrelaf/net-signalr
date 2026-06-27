import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

// Proxy para a API .NET (porta 5185). Inclui ws:true para o WebSocket do SignalR.
// Assim o cliente usa URLs relativas (/api, /hubs) e evitamos CORS no dev.
export default defineConfig({
  plugins: [react()],
  server: {
    port: 5173,
    proxy: {
      '/api': { target: 'http://localhost:5185', changeOrigin: true },
      '/hubs': { target: 'http://localhost:5185', changeOrigin: true, ws: true },
    },
  },
})
