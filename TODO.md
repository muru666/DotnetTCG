# PokemonTCG (.NET) — TODO

> Estado: F0–F3b ✅ hechos (403 tests, cobertura 90.1%). Leyenda: `[x]` hecho, `[ ]` pendiente.

## F1 — Dominio puro TDD (`src/DotnetTCG.Domain`)

- [x] Tipos base: `Card` (`Pokemon/Energy/Trainer`), `Attack`, `Ability`, `Weakness`, `Resistance`
- [x] Enums (17)
- [x] Estado: `PokemonInPlay` (daño exacto sin truncado, `DCE=2` en un solo sitio),
      `PlayerBoard` (banca ≤5, premios, robo con mazo vacío), `GameState`
- [x] `TurnManager`: `DRAW → MAIN → ATTACK → BETWEEN_TURNS`, primer turno, `ShouldAutoDraw`
- [x] `RuleValidator`: `ValidateDeck` (60 / max 4 / ≥1 básico) + reglas `Can*` (energía/supporter/
      stadium/retreat/habilidad/evolución/ataque, primer turno)
- [x] `DamageCalculator`: orden `boost → debilidad ×2 → resistencia −20 → reducción → floor 0`
      + `Mist Slash` / `Shadow Circle` / `Fur Coat` + tests
- [x] `StatusEffectManager`: 5 estados, exclusión `ASLEEP/CONFUSED/PARA`, `Sweet Veil`,
      `ProcessBetweenTurns`, cura en banca/evolución
- [x] `ModifierManager` (en `PokemonInPlay`/`PlayerBoard`: alta, expiración, consulta)
      + `ICoinFlipService` (`RandomNumberGenerator` en Infra / seeded en tests)
- [x] `VictoryConditionChecker`: premios → mazo vacío → sin Pokémon + sudden death (1 premio)
- [x] `SetupService`: reparto 7, mulligan (rival +1), `PlaceActive/Bench`, `ConfirmSetup`,
      `FinalizeSetup` (6 premios / 1 en sudden)
- [x] `AttackPipeline` CoR (variable → flip-fail → energía → confusión → selecciones →
      prerequisitos → daño → post-efectos → complex, reanudable vía `PendingChoice`)
- [x] `AttackRegistry`: 21 `VAR_DMG` + `Hyper Fang` + 37 `COMPLEX` (fallback a daño base si el
      nombre es desconocido; bloqueadores con alias a verificar en F5)
- [x] `AbilityRegistry`: 6 habilidades + `Fur Coat` pasiva (contador 1/turno en un solo sitio)
- [x] `EffectEngine`: 32 `EffectType` (semántica `BACKEND_REBUILD_SPEC.md §6`)
- [x] `TrainerCardCatalog` / `StadiumCatalog` (constantes + catálogo, sin hardcodes en el engine)
- [x] `GameEngine` fachada + handlers (`Play/Evolve/Energy/Trainer/Retreat/Attack/Ability/Turn/Choice`)
      + KO (premios, EX=2, `Destiny Burst`, promoción) + sudden death + test bot-vs-bot
      (setup + mulligan + premios + KO + victoria)
- [x] Cobertura engine ≥90% (`RuleValidator` 97.7%, `DamageCalculator` ~100%, `StatusEffectManager` 100%)

## F2a — Persistencia + catálogo

- [x] Entidades EF + migración `Initial`: `users`, `cards` (TPH + blobs jsonb), `decks`/`deck_cards`,
      `games` (lobby + `state_snapshot` + `rowversion` xmin), `game_logs`. Aplicada y verificada.
- [x] `PokeApiClient` (`IHttpClientFactory`, reintentos con backoff, `X-Api-Key` opcional) +
      mapeo xy1 (DCE/Rainbow por nombre; subtipos de energía ignorados a propósito).
      Test con stub HTTP de payload realista.
- [x] `CardService` (búsqueda paginada acotada a 100, `ImportSet`) + `DeckService`
      (CRUD con ownership → `NotFound` uniforme, 60 máx. en construcción, `Validate` con
      `RuleValidator` + ACE SPEC ≤1) + validadores FluentValidation.
- [x] Tests: servicios con dobles (12), mapper round-trip + repos contra Postgres real con
      rollback (skip si no hay Docker). Total suite: 143 verdes.

## F2b — Identidad

- [x] `UserService` (registro con validación, BCrypt, `User` por defecto) + login con `Auth.Unauthorized`
      uniforme (401 en API, sin enumerar) + JWT 60min con claims `sub/unique_name/role` desde DB.
- [x] `PasswordRecovery`: token aleatorio hasheado SHA256 (nunca en claro), TTL 15min, un uso,
      throttle 5/hora con historial (bug de `Replace` que borraba el conteo, corregido),
      `IEmailSender` SMTP (MailKit, fail-fast sin config). Migración `AddPasswordRecovery`.
- [x] Tests: servicios con dobles + reloj controlable (22), BCrypt/JWT/token-repos (9),
      persistencia users+recovery contra Postgres. Total suite: 156 verdes.

## F2c — Juego aplicado

- [x] Snapshot JSON bidireccional (`GameSnapshotMapper`: cartas por id, orden de mazo,
      pending choices, `Id` preservado) + `IGameSnapshotSerializer` (el compilador atrapó
      y se corrigió una violación Application→Infrastructure).
- [x] `LobbyService` (crear/unir con password BCrypt, capacidad 2, `SelectDeck` con ownership
      + validez, ready con mazo, solo-creador expulsa/configura, salida con transferencia
      de creador, limpieza de lobbies >24h).
- [x] `GameExecutionService` (start con validación de mazos + coin flip inicial, acciones con
      reintento optimista ×3 ante `rowversion`, `GameLog` por acción, mulligan con reintento,
      pausa/reanudación inmediata — el delay de 15s vive en el hub F3).
- [x] `IGameRepository`/`IGameLogRepository` EF (transacción + sync de jugadores, `rowversion`
      xmin) + migración `AddGamePauseFields`. Sin countdown de 3s: el start es inmediato
      al segundo ready (desvío documentado, cosmético).
- [x] Tests: lobby (9), ejecución con engine real lobby→setup→partida→`FINISHED` + pausa +
      retry de concurrencia, snapshot round-trip mid-game. Total suite: 171 verdes.

## F3a — API REST

- [x] Auth (`POST /v1/auth/register|login`, `GET /v1/users/me`, recovery
      `request/validate/reset`; login → 401 vía `ProblemDetails`), decks CRUD + `validate`,
      cards (búsqueda paginada validada, `GET {id}`), `POST v1/cards/import?set=` solo `ADMIN`.
- [x] Games REST (`/v1/games`: crear/unirse/deck/ready/start/state/actions/mulligan/
      pause/resume/logs/leave/settings/remove-player) + FluentValidation en todos los inputs
      + enums como strings (camelCase) + `StateProjector` compartido (mano rival oculta).
- [x] Tests `WebApplicationFactory` (7): contrato auth, 401, IDOR mazo, paginación acotada,
      `import` 403/ADMIN, flujo de partida completa por HTTP con no-leak de mano rival.

## F3b — SignalR

- [x] `GameHub` (`JoinLobby/LeaveLobby/SelectDeck/SetReady/StartGame/ExecuteAction/Mulligan/`
      `PauseGame/Reconnect/JoinAsSpectator/SendChat`, auth por `?access_token=`,
      `OnDisconnected` + 15s (reloj abstraído) → pausa, evento explícito `game-paused`).
- [x] Broadcast ordenado: `action-result` completo al actor + sanitizado (`PendingChoice=null`)
      al resto, estados personalizados por `Clients.User` (reusa `StateProjector`),
      `game-started/game-paused/lobby/chat`, errores al caller (`"error"` con código).
- [x] Authz por membresía en cada método + grupos por juego. Fix real encontrado por tests:
      el fire-and-forget del disconnect usaba servicios scoped dispuestos → scope propio
      vía `IServiceScopeFactory`.
- [x] Tests (7): 401 sin token, flujo miembros+espectador con no-leak, no-miembro rechazado,
      disconnect→pausa con `FakeTimeProvider`, ciclo lobby por hub, espectador privado
      rechazado, chat inválido. Total suite: 403 verdes, global 90.1%.

## F4 — Frontend

> Skill de capa: `src/DotnetTCG.Frontend/.opencode/skills/angular-22-engineering/SKILL.md`
> (sin tests: regla del proyecto, ver nota al inicio de la skill).

> Adaptación: el contrato REST está congelado y testeado (395 tests). Los flujos
> `register→login`, `deck builder→validate`, `lobby→ready→start`, `setup con mulligan`
> y `attach/attack/pendingChoice` ya están ejecutados end-to-end en `Api.Tests`
> → sirven de guion de implementación de UI y de fuente de verdad de payloads.

> Contrato visual: estética TCG moderna y limpia, sin reproducir una interfaz comercial.
> Fondo oscuro (carbón/azul grisáceo), paneles por variación sutil de superficie,
> acento azul eléctrico/medio solo en acciones principales. El color es dato
> (energía/tipo/condición especial), nunca decoración; el resto monocromático.
> Sans-serif legible, bordes/sombras/radios consistentes, cartas distinguibles
> de sus paneles. Estados hover/focus/selected/disabled/loading coherentes.
> Sin gradientes excesivos, vidrio, partículas ni animaciones complejas
> (transiciones funcionales cortas + `prefers-reduced-motion`).
> Layout en grid que colapsa a una columna en móvil, mano siempre visible.
> Stack: CSS vanilla con custom properties (sin frameworks CSS).

- [ ] F4.0 Base: tokens en `styles.css`, `provideHttpClient()` + interceptor JWT
      (token en `localStorage`: pragmático para proyecto personal sin refresh;
      documentado como tradeoff) + mapeo central de errores, `core/auth` (sesión),
      rutas (`/login /register /recovery /decks /decks/:id /lobby /games/:id` + 404)
      con guards solo-UX, shell/layout. Paquete nuevo: `@microsoft/signalr`.
- [ ] F4.1 Auth: login/register/recovery con Reactive Forms + validación de UX
      (el backend manda), estados loading/error explícitos.
- [ ] F4.2 Mazos: listar/crear/editar/borrar, builder (buscar `/v1/cards`,
      agregar/quitar, contador 0/60, límites visibles, `POST validate`).
- [ ] F4.3 Lobby por SignalR (`/hubs/game` + `?access_token=`): crear/unirse
      (password), seleccionar mazo, ready, start → navegar a `/games/:id`.
- [ ] F4.4 Tablero por SignalR: zonas en grid (activo/banca/mano/premios/descarte/
      estadio rival y propio), mano propia completa (la rival solo conteo, ya lo
      garantiza el proyector), panel de acciones por fase + `pendingChoice`,
      mulligan, conceder, pausa, log y chat.
- [ ] DoD F4: `ng build` + lint verdes, walkthrough manual de cada flujo contra
      backend con catálogo cargado (prerrequisito: `POST /v1/cards/import?set=xy1`
      con usuario ADMIN), sin `any`/casts inseguros, teclado + foco revisados.

## F5 — Hardening

- [ ] Seed XY1 146 versionado (sin users fijos, CLI `create-admin`).
- [ ] Verificar registry vs datos reales xy1: test de integración que importe el set
      y afirme que los nombres de ataques/habilidades/estadios xy1 mapean a behaviors
      (los `Aliases`/fallback quedan medidos, no asumidos).
- [ ] Post-MVP aparcado: refresh rotativo, amigos/DMs, ImgBB, ranking/historial.

## Gate de cierre (todas las fases)

- `dotnet test DotnetTCG.sln` verde.
- `python3 scripts/coverage.py /tmp/cov` con Postgres de test arriba
  (`docker start dotnettcp-test-pg`, puerto 5433): global ≥90%.
  Sin Docker los tests con DB se omiten en silencio (queda verde pero cae la
  cobertura) — si el global baja sin cambios de código, revisar el container primero.
