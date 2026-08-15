import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
import tailwindcss from '@tailwindcss/vite'
import path from 'node:path'

export default defineConfig({
  plugins: [react(), tailwindcss()],
  resolve: {
    alias: {
      // import.meta.dirname y no __dirname: el cargador nativo de config de Vite 8 no
      // soporta las globales de CommonJS.
      '@': path.resolve(import.meta.dirname, './src'),
    },
  },
  server: {
    port: 5173,
    proxy: {
      // El backend corre en el perfil http (5102) para no lidiar con el certificado
      // autofirmado en el proxy de desarrollo.
      '/api': {
        target: 'http://localhost:5102',
        changeOrigin: true,
      },
      // El hub de SignalR necesita WebSocket: sin ws:true el handshake cae a long polling.
      '/hubs': {
        target: 'http://localhost:5102',
        changeOrigin: true,
        ws: true,
      },
    },
  },
})
