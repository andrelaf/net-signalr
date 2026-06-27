import { createContext, useContext, useState, type ReactNode } from 'react'
import { api, tokenStore } from '../api/client'
import type { LoginResponse } from '../types'

type User = Omit<LoginResponse, 'token' | 'expiresAt'>

interface AuthState {
  user: User | null
  login: (userName: string, password: string) => Promise<void>
  logout: () => void
}

const AuthContext = createContext<AuthState | null>(null)

const USER_KEY = 'signalrdemo.user'

function loadUser(): User | null {
  const raw = localStorage.getItem(USER_KEY)
  return raw ? (JSON.parse(raw) as User) : null
}

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<User | null>(loadUser)

  const login = async (userName: string, password: string) => {
    const res = await api.login(userName, password)
    tokenStore.set(res.token)
    const u: User = {
      userId: res.userId,
      userName: res.userName,
      displayName: res.displayName,
      role: res.role,
    }
    localStorage.setItem(USER_KEY, JSON.stringify(u))
    setUser(u)
  }

  const logout = () => {
    tokenStore.clear()
    localStorage.removeItem(USER_KEY)
    setUser(null)
  }

  return <AuthContext value={{ user, login, logout }}>{children}</AuthContext>
}

export function useAuth() {
  const ctx = useContext(AuthContext)
  if (!ctx) throw new Error('useAuth deve ser usado dentro de AuthProvider')
  return ctx
}
