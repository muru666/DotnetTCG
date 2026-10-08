# Plan Rebuild Backend en .NET — TPI Pokémon TCG

> Fuente de verdad funcional: `docs/game-rules-summary.md` (reglas RF/RNF exigidos por la materia) + `docs/BACKEND_REBUILD_SPEC.md` (comportamiento real del Engine Java + falencias).
> Alcance: **solo backend**, reescrito en .NET desde cero en otra carpeta. El frontend Angular se reutiliza sin cambios si mantenemos el contrato.
> Decisión madre: **API-compatible con el BE Java actual** (mismos paths DTOs y eventos), pero con interior limpio. Los fixes que rompen contrato van como `/v2` o flag (ver §3).

## 0. Stack objetivo (.NET)

| Capa | Elección | Por qué |
|---|---|---|
| Runtime | **.NET 10 LTS (`net10.0`), C# 14** | LTS vigente a oct-2026. Fallback aceptable: .NET 8 LTS. No usar .NET 9 STS (fin de soporte may-2026) |
| API | **ASP.NET Core Minimal API o Controllers** (Controllers, para paridad con Spring) | Paridad 1:1 con `GameController`, `DeckController`, etc. |
| Tiempo real | **SignalR (WebSockets)** | Reemplazo de STOMP. Groups por `gameId`, `Clients.User(playerId)` para estado personalizado |
| DB | **PostgreSQL 16 + EF Core 10 (Npgsql)** | Paridad con Supabase actual. Migraciones versionadas, nada de `ddl-auto=update` |
| Auth | **JWT Bearer (`Microsoft.AspNetCore.Authentication.JwtBearer`) + BCrypt.Net** | Paridad con `JwtService` HS256 24h, pero con roles desde DB + refresh (ver §6) |
| Validación | **FluentValidation + DataAnnotations** | Reemplaza Bean Validation `@Valid` (que en Java estaba a medio usar) |
| Mapping | **Mapster o AutoMapper** | Reemplaza `ModelMapper`/`MappersConfig` |
| HTTP externo | **`HttpClient` + `IHttpClientFactory` (pokemontcg.io, ImgBB)** | Reemplaza `RestTemplate` |
| Mail | **MailKit** | Reemplaza `JavaMailSender` |
| Tests | **xUnit + NSubstitute (o Moq) + FluentAssertions + Coverlet + Testcontainers.PostgreSql** | Reemplaza JUnit5+Mockito+JaCoCo. Umbrales: global 80%, engine crítico 90% (paridad RNF-03) |
| Docs API | **Swashbuckle / Scalar** | Reemplaza springdoc `/swagger-ui.html` |
| Obs | **Serilog + OpenTelemetry (opcional)** | Reemplaza `show-sql=true` + `security=DEBUG` (que no van a prod) |
| CI | **GitHub Actions: build + test + analyzers + `dotnet ef migrations --check` + Trivy** | No existe CI hoy (hallazgo TD11). Prohibido tocar `.github/workflows` del repo viejo; el nuevo repo sí lleva CI propio |

Estructura solución (nueva carpeta, ej. `tpi-pokemon-dotnet/`):

```
tpi-pokemon-dotnet/
├── Tcg.sln
├── src/
│   ├── Tcg.Contracts/      # DTOs + enums API (sin lógica). Equivale a dtos/ + models/enums
│   ├── Tcg.Domain/         # Engine puro, CERO dependencias (NET puro). Equivale a game/engine + models
│   ├── Tcg.Application/    # Casos de uso: Lobby, Execution, Decks, Cards, Users, Recovery. Equivale a services/
│   ├── Tcg.Infrastructure/ # EF Core, repos, JWT, mail, ImgBB, PokeApiClient, SignalR backplane, clock, random
│   └── Tcg.Api/            # Controllers REST + Hubs SignalR + Program.cs + appsettings.*
└── tests/
    ├── Tcg.Domain.Tests/       # TDD engine, parametrizados
    ├── Tcg.Application.Tests/  # casos de uso con dobles
    └── Tcg.Api.Tests/          # WebApplicationFactory: REST + SignalR + IDOR + no-leak
```

Regla de dependencias: `Api → Application → Domain ← Contracts`; `Infrastructure → Application/Domain`; `Domain` no referencia a nadie. Esto corrige el DIP roto (`GameEngine` hacía `new` de 9 colaboradores, `EffectEngine` hacía `new CoinFlipService`).

## 1. Contrato a mantener (compatibilidad FE)

Base URL: FE dev usa `http://localhost:8080` sin `/api` y prod con `/api`. En .NET unificar: **`UsePathBase("/api")` solo en dev/prod via config**, nunca hardcodeado. Mantener paths exactos del spec §3 (incluido el quirk sin `/` inicial normalizado):

- `POST /v1/usuarios/auth`, `POST /v1/usuarios/auth/token → { token }` (**cambio**: envolver en JSON, hoy es `String` raw; mantener raw con flag `?legacy=true` una versión).
- `GET/POST/PUT/DELETE /v1/deck...`, `GET /v1/cartas...`, `POST /v1/cartas/importar` (**cambio**: exigir `ADMIN`, hoy es `USER` — ver §3).
- `POST /v1/games`, `GET /v1/games...`, `POST .../join|initialize|action`, `GET .../state|lobby|logs`, `POST .../check-password`.
- `POST /v1/password-recovery/request|validate|reset`.
- `POST/GET /v1/usuarios/usuario/messages...` + friends.
- Errores: migrar a **RFC 7807 `ProblemDetails`** en vez de `ErrorApi` custom. Mapeo: `ArgumentException→400`, `InvalidOperation→409` solo si es conflicto real (si no, 400), `Security→403`, `NotFound→404`, `FluentValidation→400` con `errors{}`.

SignalR (mapeo STOMP → Hub `GameHub`, ver §5). Nombres de grupos: `game:{id}`, `game:{id}:spectators`. Métodos hub 1:1 con los 11 `@MessageMapping` + 2 de chat.

DTOs: portar campo por campo desde `BACKEND_REBUILD_SPEC.md §5` (incluidos los feos: `BoardDto.hand:object`, `GameActionRequest.type:string`). **No “mejorar” DTOs en v1** — la mejora va a `Contracts.V2` después. Excepción seguridad: partir `GameStateResponse` en `Private + Public + Spectator` en el servidor aunque el JSON siga igual (ver §5.3).

## 2. Dominio puro `Tcg.Domain` (Fase 1, TDD primero)

Sin Spring, sin EF, sin SignalR. `net10.0`, `Nullable enable`, `TreatWarningsAsErrors` en este proyecto.

### 2.1 Tipos base (portar de `models/` + `models/enums/`)
- `Card` jerarquía: `abstract record Card(Id, Name, Supertype, Subtypes, Rules, SetId, ImageUrl)` + `PokemonCard(..., Hp, RetreatCost, EvolvesFrom, PokemonType, Attacks, Abilities, Weaknesses, Resistances)` + `EnergyCard(..., EnergyType, SpecialEnergyType?)` + `TrainerCard(...)`. `Attack(Name, Description, Damage, Cost[])`, `PokemonAbility(Name, Text, Type)`, `Weakness(Type, Multiplier)`, `Resistance(Type, Reduction)`.
- Enums tal cual spec: `GameActionType` (11), `GameStatus` (7 + `Waiting` alias de `IN_LOBBY` para FE), `TurnPhase` (5, **eliminar `END` muerto o marcarlo Obsolete**), `Zone`, `Status`, `EffectType` (**32**, no 31), `EffectTarget` (8), `EffectTiming`, `AbilityType`, `Type` (11), `SuperType`, `SubType`, `SpecialEnergyType`, `ModifierType`, `ModifierDuration`, `FriendshipStatus`. `GameEvent` (13).
- `GameState` como **record mutable controlado** (no 30 setters libres): constructor + métodos `With...`. Campos idénticos al spec §6 (`pendingChoice`, `pendingAttackAction`, `pendingTrainerCardId`, `attackBenchTargetPokemonId`, `attackOptionalDiscardChosen`, `supportersBlockedForPlayerIndex`, timestamps, elapsed...). Invariantes en métodos, no en callers.
- `PlayerBoard` (Active, Bench[≤5], Hand, Deck, Discard, Prizes) + `PokemonInPlay` con `AddDamage`: **documentar el truncado** `dmg/10` (bug compatible) como `DamageCounters += damage / 10` + test que lo fije. `TotalEnergyCount` (DCE=2) en **un solo sitio** (eliminar duplicado `EnergyUtils`).
- `Result<T>` propio (`Success/Failure + ErrorCode + Message`) en vez de `null`/`RuntimeException`. Cero `return null`, cero `orElse(null)`.

### 2.2 Servicios de dominio (uno por archivo, interfaces + impl)
- `ITurnManager`: `StartTurn, GoToMain, CanGoToAttack, GoToAttack, EndTurn, ShouldAutoDraw, IsFirstTurnForStartingPlayer, SyncWithTurnNumber`. Ciclo `DRAW→MAIN→ATTACK→BETWEEN_TURNS` (spec §6). `END` no se usa.
- `IRuleValidator`: `CanPlayPokemonToBench, CanEvolve (no primer turno, no justPlayed, no STAGE2, evolvesFrom), CanAttachEnergy (1/turno), CanPlayTrainer (supporter/stadium 1, tool sin tool, Forest's Curse, RESTRICT_ITEMS, supportersBlocked), CanRetreat (1/turno, no ASLEEP/PARA, no NO_RETREAT, Fairy Garden gratis), CanAttack, CanUseAbility (1/turno global)`. `ValidateDeck` (60, ≥1 BASIC, 4 copias salvo básica). Tests ≥90%.
- `IDamageCalculator`: orden fijo `boostAtacante → weakness (×mult default 2, ignora Mist Slash, Shadow Circle+DARKNESS anula) → resistance (−default 20, ignora 4) → reductionDefensor → Fur Coat −20 → floor 0`. Tablas `IGNORE_WEAKNESS/IGNORE_RESISTANCE` como `HashSet<string>` con tests. **No portar `TypeChart` muerto** (decisión: eliminarlo).
- `IStatusEffectManager`: 5 estados, exclusión ASLEEP/CONFUSED/PARA, BURNED/POISONED acumulables, Sweet Veil inmune, `ProcessBetweenTurns` (POISON 10, BURN tails 20, ASLEEP heads cura, PARA cura), `CureOnBenchedOrEvolved`. Confusión pipeline 30.
- `IVictoryConditionChecker`: premios→mazo→sin Pokémon + sudden death (1 premio). Pura, testeable.
- `IModifierManager`: CRUD + expiración `ONE_USE` siempre, `UNTIL_NEXT_TURN` si `currentTurn>turnApplied`.
- `ICoinFlipService`: `Flip()`, `FlipN(n)` detrás de interfaz con `RandomNumberGenerator` (prod) + `SeededRandom` (tests). **Prohibido `new Random()` y `Math.random()`** (bugs TD).

### 2.3 `GameEngine` despiezado (corrige God Class 1330 líneas)
No portar `GameEngine.java` tal cual. Reparto:

- `GameEngine` (fachada fina ~200 líneas): `InitializeGame`, `ExecuteAction`. Solo routing + fase + turno + pending-choice + persist hook. Sin literales de cartas.
- `SetupService` (ex-`GameSetupHandler`): `InitializeDeal (copia+shuffle+roba 7)`, `HandleMulligan (rival roba 1 por mulligan)`, `PlaceActive/PlaceBench/ConfirmSetup`, `FinalizeSetup (premios 6 o 1 sudden)`.
- `ActionHandlers/` (uno por tipo): `PlayPokemonHandler, EvolveHandler (MEGA→endTurn), AttachEnergyHandler (RAINBOW self 10), TrainerHandler (delega a EffectEngine, sin hardcodes), RetreatHandler, AttackHandler, AbilityHandler, EndTurnHandler, ConcedeHandler, ChoiceResolutionHandler`.
- `AttackPipeline` CoR real con los 7 pasos **más los 3 que hoy están fuera**: orden canónico `VariableDamage → FlipToFail → EnergyValidation → ConfusionCheck → Selections → Prerequisites → Modifiers(FLIP_COIN) → DamageCalculation → PostEffects → ComplexEffect → Knockout/DestinyBurst/Prizes → EndTurn`. Cada handler `IAttackHandler` con `HandleAsync(ctx, next)`. `AttackContext` inmutable salvo `Damage/Cancelled/CoinFlip` explícitos. Prohibido mutar `attack.Effects` (bug `ModifiersHandler.remove()`).
- `AttackRegistry`: **un archivo por carta** (`Attacks/BlazeBallHandler.cs`...) registrados por DI (`IAttackEffectHandler` keyed por nombre) en vez de 1 archivo 937 líneas con 90 `put`. 21 VAR_DMG + 1 FLIP_FAIL (Hyper Fang) + 37 COMPLEX con la semántica 1-línea del spec §6. IDs por `const`/`enum`, no `string` mágicos.
- `AbilityRegistry`: igual, 6 handlers + `Fur Coat` pasiva como `IPassiveAbility`. Contador `abilitiesUsedThisTurn` en **un solo sitio** (hoy se incrementa en 3 caminos).
- `EffectEngine`: 31 handlers (`EffectType`→handler) + `FLIP_COIN` documentado como pre-paso. `SEARCH_ANY`, `PREVENT 9999`, `RECYCLE`, etc. tal cual spec. Sin `new CoinFlipService` oculto, sin `if handler != null` silencioso (log + `UnknownEffect` explícito).
- `TrainerCardCatalog` / `StadiumCatalog`: `Potion 30`, `Tierno 3`, `Cilan 3`, `Sycamore descarta+7`, `Fairy Garden gratis`, `Shadow Circle anula weakness`... Los 7 hardcodes de `GameEngine:406-497` (Super Potion, Roller Skates, Red Card, Evosoda, Great Ball, Cassius, Max Revive pending) pasan a handlers testeados.

Criterio de aceptación F1: partida bot-vs-bot completa (setup+mulligan+6 premios+evolución+KO+victoria+SuddenDeath) verde en `Tcg.Domain.Tests`, cobertura engine ≥90%.

## 3. Aplicación `Tcg.Application` (Fase 2)

Casos de uso (nombres = métodos `GameServiceImpl`, pero repartidos):

- `LobbyService`: `CreateGame, JoinLobby (cap 2, password hash verify), SelectDeck (CON ownership check — fix TD02), SetReady (toggle, exige deck, 2 ready→countdown), RemovePlayer (solo creador), LeaveGame (vacío→delete + evento), GetLobby, UpdateSettings, StartCountdown/CancelCountdown`. Countdowns con `IDelayedJob` abstraído (testable), no `ScheduledExecutor` suelto.
  - Password partida: **hash BCrypt** + `CryptographicOperations.FixedTimeEquals` (hoy plaintext `equals`).
- `GameExecutionService`: `ExecuteAction (transacción + lock fila FOR UPDATE + rowversion, valida jugador, bloquea si disconnect timer <15s, parse enum estricto — no `valueOf` libre)`, `GetState (personalizado por jugador)`, `GetLogs`, `ListGames (filtros del spec)`, `GetActive/GetResumable`, `ResumeLobby/SetResumeReady`, `CheckPassword`, `HandleDisconnect (15s→PAUSED) / HandleReconnect (unpause ajustando timestamps)`, `CleanupStale (IN_LOBBY >24h)`.
  - Estado en memoria: solo caché con expiración + `IDistributedCache` opcional. Lobby/decks **persistidos** (hoy se pierden al restart — R05).
  - Concurrencia: `SELECT ... FOR UPDATE` + `rowversion` + idempotency-key por `GameActionRequest.IdempotencyKey` (nuevo campo opcional, compatible). Prohibido `lock(gameId.intern())`.
- `DeckService`: reglas idénticas (60, 4 copias salvo básica, ≥1 BASIC, conteo por nombre, `isValid/version++`, starters `xy1-NNN→qty`).
- `CardService`: `GetAll (paginado, no `todas` sin cota), Search (paginado acotado `size≤100`), GetById, ImportXy1 (solo ADMIN + scheduler + boot, idempotente)`.
- `UserService / FriendshipService / MessageService / RecoveryService (UUID TTL 15min un uso, hash del token en DB + throttle + no enumerar) / EmailService / FileService (ImgBB via HttpClient)`.
- Todos devuelven `Result<T>`, validación FluentValidation en borde, logs sin PII, `CancellationToken` en todo.

## 4. Infraestructura `Tcg.Infrastructure` (Fase 0 en paralelo)

- EF Core code-first desde entidades §7 del spec. Tablas: `games, game_players, game_player_cards (zone,position), board_pokemons (position 0=activo), board_pokemon_modifiers, status_effects, game_logs, cards (JOINED→TPH o TPT; recomendado TPH por simplicidad salvo que se quiera paridad), pokemon_cards, energy_cards, trainer_cards, attacks, abilities, weaknesses, resistances, decks, deck_cards, usuarios, friendships (unique requester+addressee), friend_messages, password_recovery`.
- Convenciones: `UseSnakeCase?` No — mantener nombres actuales para migrar datos si hace falta. `ManyToOne` explícito `DeleteBehavior.Restrict`, bloat controlado. `QueryTrackingBehavior.NoTracking` en lecturas. Índices: `(game_id, position)`, `(user_id)`, `(deck_id)`, `(status, started_at)`.
- Migraciones: `dotnet ef migrations add Initial` + CI `--check`. Baseline desde `data-postgresql.sql`. Seed versionado: `HasData` solo para catálogo XY1 (146) + starters; **cero usuarios admin fijos con hash conocido** (se crean por CLI `user create-admin`).
- Persistencia de partida: **upsert diferencial por `(cardId,zone,position)`**, no delete-all/insert-all (fix TD06). `pending_data` JSON (`System.Text.Json`) con `{pendingChoice,pendingAttackAction}`. `GameLog` append-only.
- Reloj y random abstraídos: `ISystemClock`, `ICoinFlipService` (prod `RandomNumberGenerator`, tests seeded).
- `PokeApiClient` (`/v2/cards?q=set.id:xy1`, paginado, caché + reintentos Polly, sin llamadas durante partida — RNF).
- `ImgBbClient`, `SmtpEmailSender` (MailKit), todo secreto por `IConfiguration`/env, nunca en `appsettings.json` commiteado.

## 5. API + SignalR `Tcg.Api` (Fase 3)

- `Program.cs`: `UsePathBase(/api)` por config, CORS `localhost:4200` (prod por env), `AddAuthentication JwtBearer` (HS256, `ValidateIssuer=false` en v1 por compat, issuer/audience en v2), `AddAuthorization` policies `User/Admin/MemberOfGame`, `AddFluentValidation`, `ProblemDetails`, `AddSignalR`, `AddEf`, Swagger/Scalar solo dev.
- Controllers 1:1 spec §3 con `[Authorize(Roles=...)]`. `GameController` sin `SimpMessagingTemplate` muerto. `quantity`, `page/size` con `[Range]`. Todos los bodies con validadores.
- `GameHub : Hub` (mapeo 11+2):
  - `JoinLobby(gameId,password)`, `UpdateSettings`, `SelectDeck`, `SetReady`, `RemovePlayer`, `LeaveGame`, `InitializeGame`, `ExecuteAction(request)`, `JoinAsSpectator`, `SetResumeReady`, `Reconnect` + `SendChat(gameId,content)`, `SendSpectatorChat`.
  - Auth: `[Authorize]` en hub + `GameMembershipRequirement` en `OnConnectedToGroup`/`Subscribe`. Groups `game:{id}`. Estado personalizado via `Clients.User(pid).SendAsync("state", personalState)` + `Clients.Group(...).SendAsync("action-result", sanitized)` + `Clients.Group(spectators).SendAsync("state", spectatorState)`.
  - Sanitización en **capa de proyección** (`StateProjector.ToPrivate/ToSpectator`), no a mano en el hub (fix fragilidad `WsGameController:104-147`). Tests no-leak: espectador nunca recibe `hand`, `deck order`, `prizes content`, `pendingChoice.options`.
- Orden de broadcasts idéntico al flujo §5.3 del spec para no romper FE: `action-result → personal states → topic state`.

## 6. Transversales (Fase 4)

- AuthN/Z: login `401` (no 404), `UserPrincipal` claims `{sub=id, unique_name, roles[]}`; roles **desde DB en cada login + refresh corto** (no del token viejo); `ADMIN` solo por CLI/seed; `MemberOfGameHandler` (player o espectador permitido) en cada `gameId`; `DeckOwnerHandler` en `selectDeck`; `ImportCards` solo `ADMIN`.
- JWT: access 15–60min + refresh rotativo (mejora sobre 24h sin revocación). `jti` + denylist en cache. `FixedTimeEquals` donde compare secretos.
- Validación: FluentValidation en REST y Hub (payloads WS también se validan — hoy ninguno).
- Errores: `ProblemDetails` + `traceId`, sin `ex.Message` crudo, `ValidationException→400`, `NotFound→404`, `Forbidden→403`. `MethodArgumentNotValid` incluido.
- Config: `appsettings.json` (sin secretos) + `appsettings.Development/Production.json` + env `JWT__KEY, DB__, SMTP__, IMGBB__KEY`. `.gitignore` con `appsettings.*.local.json`, `.env`. Arranque falla en claro si falta secreto (fail-fast).
- Actuator: health `/health` solo, sin exponer todo.

## 7. Datos y seed (Fase 5)

- Migración `Initial` + `SeedXy1` (146 `xy1-001..146`, 11 energías, 15 trainers, subtypes, `retreat=0` en 5 ids, 2 abilities + 2 attacks fallback + costs, starters). Importador idempotente por `cardId` (upsert, no duplicar).
- Usuarios: **ningún seed con password conocida**. `dotnet run -- user create-admin` interactivo.
- `data.sql`/`data-postgresql.sql` viejos quedan como referencia, no se portan.

## 8. Testing (Fase 6, continua desde F1)

- `Tcg.Domain.Tests` (xUnit, ~200 casos): setup/mulligan, turnos/primer turno/MEGA, cada `GameActionType`, pipeline orden, daño (weak×2, res−20, FurCoat, 9999), estados 5 + Sweet Veil, KO (EX=2 premios, reemplazo, derrota sin banca), victoria/sudden, 21 VAR_DMG + 37 COMPLEX + 6 habilidades + 31 efectos parametrizados, truncado `dmg/10` fijado, DCE=2.
- `Tcg.Application.Tests`: lobby countdowns (fake clock), disconnect/reconnect, resume, IDOR (deck ajeno denegado, partida privada denegada), recovery throttle.
- `Tcg.Api.Tests` (`WebApplicationFactory` + Testcontainers): contrato REST (paths del spec), `ProblemDetails`, paginación acotada, SignalR no-leak (3 clientes: P0/P1/espectador), `importar` solo ADMIN, login 401.
- Cobertura: global ≥80%, `RuleValidator/DamageCalculator/StatusEffectManager` ≥90% (Coverlet, gate en CI).

## 9. Ops (Fase 7)

- `Dockerfile` multistage + `compose.yml` (api + postgres 16). Healthchecks. Nada de credenciales en compose commiteado (`.env`).
- CI nuevo repo: `dotnet build -warnaserror, dotnet test --collect coverage, dotnet ef migrations bundle --check, analyzers, trivy`. Badge y branch protection (el repo viejo no tiene CI — no tocarlo por regla global).

## 10. Orden de ejecución sugerido

1. **F0** esqueleto sln + `Contracts` (DTOs/enums copiados del spec) + `Api` health + EF `Initial` vacía + CI mínima. (1–2 días)
2. **F1** dominio TDD por este orden: tipos → TurnManager → Validator → Damage → Status → Victory → Modifiers → Setup → pipeline → registries (1 carta por PR) → Engine fachada → bot-vs-bot verde. (grueso del trabajo)
3. **F2** Application + Infra persistencia diferencial + lobby/execution con lock fila. (medio)
4. **F3** REST + SignalR + proyectores + authz por recurso. FE apunta al nuevo BE sin cambios. (medio)
5. **F4–F7** transversales, seed XY1, tests contrato, docker/CI, hardening (throttle, refresh, hash partida).

## 11. Riesgos y decisiones abiertas

- R01 secretos viejos: rotar antes de encender el nuevo BE contra la misma DB.
- R04 multi-instancia: SignalR necesita backplane (Redis) si hay >1 réplica — arrancar con 1 réplica + sticky, dejar backplane para F7.
- `GameActionRequest.type:string` se mantiene en v1 por compat FE; en dominio usar `enum` + parse estricto con `400` si es desconocido.
- `BoardDto.hand:object` y `password` en `UpdateGameSettings` se mantienen en v1, se tipan en v2.
- Duda: `youMayChoices` y `previousStatus` (spec §15) — portar como campo opaco hasta observarlos en runtime con FE real.

---
*Plan derivado de `docs/game-rules-summary.md` (qué exige la materia) y `docs/BACKEND_REBUILD_SPEC.md` (qué hace el Java real). Si hay conflicto, manda la regla TCG oficial y el contrato FE vigente, no el código viejo.*
