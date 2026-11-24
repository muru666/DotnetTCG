# DotnetTCG

**Juego de cartas Pokémon (TCG) online** con partidas en tiempo real: backend en **.NET 10** con Clean Architecture y frontend en **Angular 22** comunicándose por REST + SignalR.

## Por qué existe este proyecto

El juego nació originalmente como un proyecto en **Java Spring Boot** que terminó siendo poco sólido: quedó "con alambres y cinta", con falencias acumuladas y decisiones de diseño que hicieron el código difícil de mantener y de corregir.

Este repositorio es la **reescritura y refactorización** de ese original, con dos objetivos explícitos:

1. **Entender qué salió mal**: qué reglas quedaron implícitas, qué atajos se pagaron después y qué habría que corregir de raíz.
2. **Reconstruirlo sobre bases sólidas**: arquitectura por capas con reglas de dependencia verificables, reglas de negocio protegidas en el dominio (no en los controllers ni en el cliente), contrato de API explícito y una red de tests que haga visible cada regresión antes que el jugador.

El aprendizaje queda documentado en el proceso: una especificación de reconstrucción que fija el comportamiento en paridad con el original, un plan por fases, el TODO con el estado real del trabajo, y una **auditoría completa** (`docs/AUDIT-2026-11.md`) que encontró los defectos del código con evidencia reproducible y dejó registrado su estado de corrección.

## Qué incluye

- **Identidad**: registro, login (usuario o email), recuperación de contraseña por token hasheado (SHA-256, TTL 15 min, un uso, con throttle), JWT de 60 min y roles (`User`/`Admin`).
- **Catálogo de cartas**: búsqueda paginada con filtros, detalle por id, importación desde la API externa (solo `Admin`) y seed local versionado del set **XY1** (`data/xy1.json`, 146 cartas).
- **Mazos**: CRUD con ownership (los ajenos se ven como `404`), construcción hasta 60 cartas y validación de reglas (60 exactas, máx. 4 copias por nombre, ≥1 básico, ≤1 ACE SPEC).
- **Lobbies**: partidas públicas/privadas con contraseña (hash BCrypt), capacidad 2, selección de mazo, ready, expulsión del creador, transferencia de creador al salir y limpieza de lobbies abandonados.
- **Partidas**: setup con mulligan, motor de juego completo (turnos, ataques, habilidades, estados especiales, evoluciones, premios, sudden death), acciones por REST y por hub, pausa/reanudación, desconexión con gracia de 15 s que pausa la partida, logs por acción y chat.
- **Espectadores**: unirse como espectador (con decisión del creador), con proyección de estado que **oculta la mano rival**.
- **Frontend**: auth, buscador de cartas, deck builder, lista de lobbies con filtros, salas de espera, tablero con drag&drop, diálogo de elección, overlays de pausa/desconexión, página de espectador y sonido.

## Stack

| Capa | Tecnología |
|---|---|
| API | ASP.NET Core (.NET 10), controllers + hub SignalR, JWT Bearer, FluentValidation, rate limiting, OpenAPI + Scalar (dev) |
| Dominio | C# puro, sin dependencias de framework (motor de juego, reglas, efectos) |
| Persistencia | Entity Framework Core 10 + PostgreSQL (Npgsql), migraciones, concurrencia optimista con `xmin` |
| Infraestructura | BCrypt.Net, MailKit (SMTP), cliente de pokemontcg.io con reintentos |
| Frontend | Angular 22 (signals, CDK), `@microsoft/signalr`, CSS con custom properties, Vitest |
| Tests | xUnit (4 suites) + Vitest (frontend), `WebApplicationFactory`, Postgres real de test |

## Arquitectura

```
                     ┌──────────────────────────────┐
                     │   Frontend (Angular)         │
                     └──────────────┬───────────────┘
                              HTTP + WebSocket
                     ┌──────────────▼───────────────┐
                     │            Api               │  controllers, hub, JWT,
                     │                              │  mapeo error→HTTP, rate limit
                     └───────┬──────────────┬───────┘
                             │              │
              ┌──────────────▼──┐    ┌──────▼────────────────┐
              │   Application   │    │   Infrastructure      │  EF Core/PostgreSQL,
              │  casos de uso,  │◄───┤  implementa las       │  BCrypt, JWT, SMTP,
              │  orquestación,  │    │  abstracciones de     │  PokeAPI, snapshots
              │  interfaces     │    │  Application/Domain   │
              └───────┬─────────┘    └───────────────────────┘
                      │
              ┌───────▼─────────┐
              │     Domain      │  entidades, reglas, motor,
              │  (sin deps)     │  invariantes de negocio
              └─────────────────┘
```

**Reglas que se verifican** (y que la auditoría comprobó por proyecto, no por carpetas): el Dominio no referencia Application/Infrastructure/Api; Application no conoce EF Core ni ASP.NET; Infrastructure implementa abstracciones definidas por las capas internas; la Api no contiene reglas de negocio; los errores de dominio se traducen a HTTP en un solo lugar (`ApiHelpers.ErrorMapping`).

## Estructura

```
DotnetTCG.sln
├── src/
│   ├── DotnetTCG.Api/              # HTTP + SignalR + composición
│   ├── DotnetTCG.Application/      # casos de uso e interfaces
│   ├── DotnetTCG.Domain/           # motor y reglas del juego
│   ├── DotnetTCG.Infrastructure/   # EF, auth, email, PokeAPI
│   └── DotnetTCG.Frontend/         # Angular
├── tests/                          # Domain/Application/Infrastructure/Api.Tests
├── data/xy1.json                   # seed del catálogo (146 cartas)
├── docs/                           # spec, plan, reglas y auditoría
├── scripts/coverage.py             # gate de cobertura
└── compose.yml / Dockerfile / run-dev.sh
```

## Puesta en marcha

**Requisitos:** .NET SDK 10, Node 20+, Docker.

```bash
# Desarrollo completo: Postgres (docker) + seed del catálogo + backend + frontend
./run-dev.sh
# backend → http://localhost:5280   frontend → http://localhost:4200
```

```bash
# Solo backend (necesita una DB y Jwt__Key de 32+ caracteres)
dotnet run --project src/DotnetTCG.Api

# Seed del catálogo (migra + upsert idempotente)
dotnet run --project src/DotnetTCG.Api -- seed-cards data/xy1.json

# Crea el mazo inicial a los usuarios que no tienen
dotnet run --project src/DotnetTCG.Api -- backfill-starter-decks
```

**Producción (compose):** copiá `.env.example` a `.env` (definí `POSTGRES_PASSWORD` y `JWT_KEY` de 32+ caracteres) y `docker compose up --build`. El servidor **aplica las migraciones al arrancar** y `/health` chequea la base real.

**Variables relevantes:** `ConnectionStrings__Default`, `Jwt__Key` (obligatoria, arranque falla si falta), `Cors:AllowedOrigins` (default `http://localhost:4200`), `RateLimiting:AuthPermitLimit` (por cliente), `Smtp__*` (necesaria solo para recuperación de contraseña por email).

## Tests y calidad

```bash
dotnet test DotnetTCG.sln            # backend: 467 tests (2026-11-24)
cd src/DotnetTCG.Frontend && npm test # frontend: 31 tests (Vitest)
cd src/DotnetTCG.Frontend && npm run build
```

- Los tests que tocan PostgreSQL **se auto-omiten y se reportan como omitidos** si no hay contenedor de test: `docker start dotnettcp-test-pg` (puerto 5433) para correrlos. Un verde sin esa base **no** significa que la persistencia esté probada.
- Gate de cobertura global ≥90%: `python3 scripts/coverage.py /tmp/cov` (ver `TODO.md`).
- La auditoría de 2026-11 dejó **tests de regresión** para cada defecto corregido (`AuditFixesTests`, `AttackRegistryXy1CoverageTests`, specs del frontend, etc.).

## Documentación

| Documento | Qué contiene |
|---|---|
| `TODO.md` | Plan por fases (F0–F5) y estado real del avance |
| `docs/BACKEND_REBUILD_SPEC.md` | Especificación de reconstrucción: semántica en paridad con el original Java |
| `docs/DOTNET_REBUILD_PLAN.md` | Plan de migración/reescritura |
| `docs/game-rules-summary.md` | Reglas del juego implementadas |
| `docs/AUDIT-2026-11.md` | Auditoría completa (hallazgos con evidencia, severidad y estado de remediación) |

## Flujo de ramas

Git flow: `main` (producción, solo avanza con `release/*` o `hotfix/*`), `develop` (integración, rama por defecto), `feature/*`, `fix/*`, `release/*`, `hotfix/*` en kebab-case. Los PRs van contra `develop` (los de hotfix, contra `main`). Detalle en la skill `.opencode/skills/project-rules/SKILL.md`.

## Skills del proyecto

Hay skills en `.opencode/skills/` que los agentes cargan automáticamente al trabajar acá:

- **`project-rules`** — reglas de colaboración obligatorias (preguntar ante la duda, tests en el frontend, git flow).
- **`dotnet-clean-architecture`** — convención de arquitectura y ubicación de cada responsabilidad.
- **`clean-architecture-auditor`** — flujo de auditoría de calidad usado en `docs/AUDIT-2026-11.md`.

## Estado y pendientes

- F0–F3b completadas (dominio, persistencia, identidad, juego aplicado, API REST, SignalR).
- F4 (frontend) en su etapa de DoD: flujos implementados y verificados contra el backend, falta el walkthrough manual completo.
- F5 (hardening) parcial.
- De la auditoría: corregidos los grupos 1–4 y los bugs sueltos; quedan **decisiones de producto** abiertas (efectos de cartas contra su texto, elección de objetivo en pociones, reemplazo elegido por el dueño en el KO, eventos de estado especiales) documentadas en `docs/AUDIT-2026-11.md` §H.
