import { useState } from 'react'
import { useAuth } from '../auth/AuthContext'

const DEMO_USERS = [
  { user: 'manager', label: 'Marina — Manager' },
  { user: 'ana', label: 'Ana — Agente' },
  { user: 'bruno', label: 'Bruno — Agente' },
  { user: 'carla', label: 'Carla — Cliente' },
  { user: 'diego', label: 'Diego — Cliente' },
]

export function LoginPage() {
  const { login } = useAuth()
  const [userName, setUserName] = useState('ana')
  const [password, setPassword] = useState('demo123')
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)

  const submit = async (e: React.FormEvent) => {
    e.preventDefault()
    setBusy(true)
    setError(null)
    try {
      await login(userName, password)
    } catch (err) {
      setError((err as Error).message)
    } finally {
      setBusy(false)
    }
  }

  return (
    <div className="login">
      <form className="card login-card" onSubmit={submit}>
        <h1>Help Desk — SignalR Demo</h1>
        <p className="muted">Entre com um usuário de demonstração (senha: demo123).</p>

        <label>Usuário</label>
        <input value={userName} onChange={(e) => setUserName(e.target.value)} autoFocus />

        <label>Senha</label>
        <input type="password" value={password} onChange={(e) => setPassword(e.target.value)} />

        {error && <div className="error">{error}</div>}

        <button disabled={busy} type="submit">
          {busy ? 'Entrando…' : 'Entrar'}
        </button>

        <div className="demo-users">
          {DEMO_USERS.map((u) => (
            <button
              type="button"
              key={u.user}
              className="chip"
              onClick={() => {
                setUserName(u.user)
                setPassword('demo123')
              }}
            >
              {u.label}
            </button>
          ))}
        </div>
      </form>
    </div>
  )
}
