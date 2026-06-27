# net-signalr — Demo completa de ASP.NET Core SignalR

Projeto de demonstração de um **Help Desk / Workspace colaborativo** em tempo real, construído para
exercitar **todas** as features do SignalR — das básicas às mais avançadas (streaming bidirecional,
client results, hub filters, autenticação sobre WebSocket, stateful reconnect e backplane Redis para
scale-out horizontal).

**Stack:** .NET 10 · ASP.NET Core SignalR · EF Core 10 + SQLite (portátil) · React 19 + Vite + TypeScript
(`@microsoft/signalr`).

---

## Como rodar (modo portátil — sem Docker)

Pré-requisitos: **.NET 10 SDK** e **Node 20+**.

**1. Backend** (cria e popula `workspace.db` automaticamente na primeira execução):

```bash
dotnet run --project src/SignalRDemo.Api --launch-profile http
# API em http://localhost:5185  ·  Swagger em http://localhost:5185/swagger
```

**2. Frontend** (em outro terminal):

```bash
cd client
npm install
npm run dev
# App em http://localhost:5173  (faz proxy de /api e /hubs para a API)
```

**3. Login** — usuários de demonstração (senha **`demo123`** para todos):

| Usuário   | Papel    | Para demonstrar                                   |
|-----------|----------|---------------------------------------------------|
| `manager` | Manager  | Dashboard ao vivo, comandos de staff              |
| `ana`     | Agente   | Atender tickets, resolver, client results         |
| `bruno`   | Agente   | Segundo agente (presença, DMs)                    |
| `carla`   | Cliente  | Abrir tickets, conversar                          |
| `diego`   | Cliente  | Segundo cliente                                   |

> Dica: abra duas abas (ex.: `ana` e `carla`) no mesmo ticket para ver chat, presença, typing,
> upload e DMs em tempo real.

---

## Arquitetura

```
src/
  SignalRDemo.Domain/          Entidades, enums (sem dependências)
  SignalRDemo.Infrastructure/  EF Core DbContext, SQLite, migrations, seed, hashing
  SignalRDemo.Api/             Host: Hubs, Controllers, Auth JWT, DI, configuração
client/                        React + Vite + TS (consumidor SignalR)
docker-compose.yml             Perfil "scaleout": Redis + 2× API + nginx
deploy/nginx.conf              Load balancer com sticky sessions
```

---

## Mapa de features SignalR → onde estão no código

| Feature | Arquivo principal |
|---|---|
| Hub fortemente tipado `Hub<IWorkspaceClient>` | `Api/Hubs/WorkspaceHub.cs`, `IWorkspaceClient.cs` |
| Server→client / client→server | métodos do `WorkspaceHub` |
| **Groups** (salas de ticket) | `WorkspaceHub.JoinTicket/LeaveTicket/SendMessage` |
| **User-based messaging** + `IUserIdProvider` | `WorkspaceHub.SendDirectMessage`, `Auth/CustomUserIdProvider.cs` |
| **Ciclo de vida + presença + typing** | `WorkspaceHub.OnConnected/OnDisconnected`, `Hubs/PresenceTracker.cs` |
| **Streaming server→client** (`IAsyncEnumerable`) | `Api/Hubs/DashboardHub.cs` (`StreamMetrics`) |
| **Streaming client→server** (chunks) | `Api/Hubs/WorkspaceHub.Upload.cs`, `client/src/signalr/uploads.ts` |
| **Client results** (servidor invoca e aguarda) | `WorkspaceHub.Admin.cs` (`RequestCloseConfirmation`) + `ConfirmAction` no cliente |
| **Auth JWT** + token via `access_token` (WebSocket) | `Program.cs` (`JwtBearerEvents.OnMessageReceived`) |
| **Authorization** por método/policy/role | `[Authorize(Policy=…)]` em `WorkspaceHub.Admin.cs` e `DashboardHub` |
| **Hub filters** (cross-cutting) | `Api/Hubs/Filters/HubLoggingFilter.cs`, `HubExceptionFilter.cs` |
| **IHubContext** fora do hub | `Controllers/TicketsController.cs`, `Services/SlaMonitorService.cs` |
| **MessagePack** (além de JSON) | `Program.cs` (`AddMessagePackProtocol`), flag em `client/src/signalr/WorkspaceHubContext.tsx` |
| **Automatic + stateful reconnect** | `Program.cs` (`AllowStatefulReconnects`) + `withStatefulReconnect` no cliente |
| **Transportes** (WS → SSE → Long Polling) | `Program.cs` (`MapHub` → `Transports`) |
| **Keep-alive / timeouts / max message size** | `Program.cs` (`AddSignalR` → `HubOptions`) |
| **Error handling** (`HubException`) | filtros + métodos do hub |
| **Rate limiting** | `Program.cs` (`AddRateLimiter` + `RequireRateLimiting`) |
| **Backplane Redis (scale-out)** | `Program.cs` (condicional `SignalR:Backplane`) + `docker-compose.yml` |

### Concorrência otimista (EF + SignalR)
`ResolveTicket` usa o `RowVersion` do ticket como token de concorrência: se outro agente alterar o
ticket simultaneamente, o `DbUpdateConcurrencyException` vira uma `HubException` amigável. Ver
`WorkspaceHub.Admin.cs` e a configuração em `Infrastructure/Data/AppDbContext.cs`.

---

## Scale-out horizontal (opcional, com Docker)

O app roda single-node por padrão. Para demonstrar o **backplane Redis** distribuindo mensagens entre
múltiplas instâncias atrás de um load balancer:

```bash
docker compose --profile scaleout up --build
# App em http://localhost:8080 (nginx -> api1/api2)
```

Conecte duas abas: o nginx (com `ip_hash`) fixa cada cliente em um nó (**sticky session**, exigida pelo
SignalR), e o **Redis** propaga as mensagens entre os nós. Mensagens publicadas em `api1` chegam aos
clientes conectados em `api2`.

> **Nota sobre o SQLite no scale-out:** as instâncias compartilham um único `workspace.db` por volume
> Docker, o que é suficiente para a demo, mas não é recomendado em produção (limitações de escrita
> concorrente do SQLite). Em um cenário real, use um banco compartilhado de verdade (PostgreSQL,
> SQL Server, etc.). A presença em `PresenceTracker` é por nó — em produção, consolide-a no backplane.

---

## Notas

- **MessagePack:** o servidor aceita JSON **e** MessagePack. O cliente usa JSON por padrão (mais fácil
  de inspecionar no DevTools); mude `USE_MESSAGEPACK = true` em `client/src/signalr/WorkspaceHubContext.tsx`
  para trafegar binário.
- **Alerta de SLA:** um dos tickets do seed nasce "atrasado", então o `SlaMonitorService` envia um push
  de SLA poucos segundos após o login do agente/manager responsável (toast no canto da tela).
- **Migrations:** geradas via `dotnet ef`. Para recriar do zero, apague `workspace.db` (recriado no boot).
- **Chave JWT:** `Jwt:SigningKey` em `appsettings.json` é apenas para desenvolvimento.
```
