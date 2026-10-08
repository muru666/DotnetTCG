# PokemonTCG (.NET) — TODO

> F0 ✅ hecho. F1 ✅ hecho (127 tests, engine ≥90%). Leyenda: `[x]` hecho, `[ ]` pendiente.

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

- [ ] Entidades EF + migración `Initial`: `users`, `cards` (TPH), `decks`/`deck_cards`,
      `games` (lobby + `state_snapshot` JSON + `rowversion`), `game_logs` (append-only).
      Decisión: snapshot JSON versionado en vez de sync relacional del tablero
      (mismas garantías con `rowversion`, sin el `delete-all/insert-all` del Java).
- [ ] `PokeApiClient` (`IHttpClientFactory`, caché, reintentos) + importador idempotente xy1
      + seed local (se necesita ahora para probar mazos; estaba en F5).
- [ ] `CardService` (búsqueda paginada acotada) + `DeckService`
      (60 / max 4 / ≥1 básico / AS TÁCTICO / ownership por usuario).
- [ ] Tests: servicios con dobles + persistencia contra Postgres real cuando hay Docker
      disponible (skip explícito si no), nunca `InMemory`.

## F2b — Identidad (antes que juego: el juego exige `[Authorize]`)

- [ ] `UserService` (BCrypt) + login 401 + access JWT 60min.
      Refresh rotativo: diferido a post-MVP (superficie grande para el valor que aporta ahora).
- [ ] `PasswordRecovery` (token hasheado, TTL 15min, un uso, throttle, sin enumerar).
- [ ] Recorte MVP: amigos/DMs fuera (no puntúan; el bonus de chat se cubre con chat
      in-game en F3). Recovery de archivos/ImgBB: diferido (foto de perfil no es core).

## F2c — Juego aplicado

- [ ] `LobbyService` (crear/unir con password hasheada, `SelectDeck` con ownership,
      ready toggle, countdown 3s con reloj/delay abstraídos, solo creador expulsa/configura).
- [ ] `GameExecutionService`: carga snapshot → `GameEngine` → guarda snapshot + `rowversion`
      (lock optimista, sin `lock(intern())`), `GameLog` por acción, `Victory`→`FINISHED`.
- [ ] Desconexión 15s → `PAUSED` + reconexión restaura snapshot. Timers abstraídos (testeables).

## F3 — API + SignalR

- [ ] REST `/api/v1/...` (auth, decks, cards, games) + FluentValidation + `ProblemDetails`.
      `importar` solo `ADMIN`.
- [ ] `GameHub` (lobby + acciones + chat in-game) + `StateProjector`
      (`Private/Public/Spectator`, no-leak: sin mano rival, orden ni premios ocultos)
      + authz por recurso (`MemberOfGame`, `DeckOwner`).

## F4 — Frontend

- [ ] Auth, deck builder (buscar xy1, 0/60, validación), lobby, tablero (zonas + mano +
      premios + estadio), panel por fase, log. Sin tests FE.

## F5 — Hardening

- [ ] Verificar registry vs datos reales xy1 (nombres, flips, bloqueadores con alias).
- [ ] Seed XY1 146 versionado (sin users fijos, CLI `create-admin`), cobertura global ≥80%.
- [ ] Post-MVP aparcado: refresh rotativo, amigos/DMs, ImgBB, ranking/historial.
