import {
  HubConnection,
  HubConnectionBuilder,
  HubConnectionState,
  LogLevel,
} from '@microsoft/signalr'
import { MessagePackHubProtocol } from '@microsoft/signalr-protocol-msgpack'
import { createContext, useContext, useEffect, useRef, useState, type ReactNode } from 'react'
import { tokenStore } from '../api/client'
import type { UploadMetadata } from './uploads'

// Flip para true para trafegar em MessagePack (binário) em vez de JSON.
// O servidor aceita ambos (AddMessagePackProtocol). JSON é mais fácil de inspecdr no DevTools.
const USE_MESSAGEPACK = false

interface WorkspaceHub {
  conn: HubConnection | null
  state: HubConnectionState
}

const Ctx = createContext<WorkspaceHub | null>(null)

export function WorkspaceHubProvider({ children }: { children: ReactNode }) {
  const connRef = useRef<HubConnection | null>(null)
  const [conn, setConn] = useState<HubConnection | null>(null)
  const [state, setState] = useState<HubConnectionState>(HubConnectionState.Disconnected)

  useEffect(() => {
    let builder = new HubConnectionBuilder()
      .withUrl('/hubs/workspace', {
        // O token vai como ?access_token=... no handshake (lido pelo backend).
        accessTokenFactory: () => tokenStore.get() ?? '',
      })
      // Reconexão automática com backoff customizado.
      .withAutomaticReconnect([0, 2000, 5000, 10000])
      // Stateful reconnect: o servidor mantém um buffer e retoma sem perder mensagens.
      .withStatefulReconnect({ bufferSize: 100_000 })
      .configureLogging(LogLevel.Information)

    if (USE_MESSAGEPACK) {
      builder = builder.withHubProtocol(new MessagePackHubProtocol())
    }

    const connection = builder.build()

    // Client result: o servidor invoca este método e AGUARDA o retorno booleano.
    connection.on('ConfirmAction', (prompt: string) => window.confirm(prompt))

    const sync = () => setState(connection.state)
    connection.onreconnecting(sync)
    connection.onreconnected(sync)
    connection.onclose(sync)

    connRef.current = connection
    connection
      .start()
      .then(() => {
        setConn(connection)
        setState(connection.state)
      })
      .catch((err) => console.error('Falha ao conectar no hub:', err))

    return () => {
      connRef.current = null
      connection.stop()
    }
  }, [])

  return <Ctx value={{ conn, state }}>{children}</Ctx>
}

export function useWorkspaceHub() {
  const ctx = useContext(Ctx)
  if (!ctx) throw new Error('useWorkspaceHub deve ser usado dentro de WorkspaceHubProvider')
  return ctx
}

export type { UploadMetadata }
