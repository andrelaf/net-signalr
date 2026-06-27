import { createRoot } from 'react-dom/client'
import { BrowserRouter } from 'react-router-dom'
import './styles.css'
import App from './App.tsx'
import { AuthProvider } from './auth/AuthContext'

// Sem StrictMode de propósito: o duplo-mount de efeitos em dev reabre/encerra a
// conexão SignalR duas vezes, atrapalhando a observação do ciclo de vida na demo.
createRoot(document.getElementById('root')!).render(
  <BrowserRouter>
    <AuthProvider>
      <App />
    </AuthProvider>
  </BrowserRouter>,
)
