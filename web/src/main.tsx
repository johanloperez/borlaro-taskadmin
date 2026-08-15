import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import './index.css'
import App from './App.tsx'
import { I18nProvider } from '@/lib/i18n'

const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      staleTime: 30_000,
      // SignalR va a empujar los cambios en tiempo real, así que reconsultar al volver
      // a la pestaña solo agrega ruido.
      refetchOnWindowFocus: false,
      retry: 1,
    },
  },
})

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <QueryClientProvider client={queryClient}>
      {/* Envuelve todo, incluidas las pantallas públicas: quien todavía no tiene cuenta también
          tiene idioma, y es justamente a quien peor le cae encontrarse una app en otro. */}
      <I18nProvider>
        <App />
      </I18nProvider>
    </QueryClientProvider>
  </StrictMode>,
)
