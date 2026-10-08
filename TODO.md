# PokemonTCG (.NET) — TODO

> F0 ✅ hecho (commit `d53d617`). F1 en curso. Leyenda: `[x]` hecho, `[ ]` pendiente.

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
- [ ] `AttackPipeline` CoR (7 pasos + VAR_DMG/FLIP_FAIL/COMPLEX integrados, `AttackContext`
      sin mutación de efectos)
- [ ] `AttackRegistry`: 21 `VAR_DMG` (tabla de fórmulas) + `Hyper Fang` + 37 `COMPLEX`
- [ ] `AbilityRegistry`: 6 habilidades + `Fur Coat` pasiva (contador 1/turno en un solo sitio)
- [ ] `EffectEngine`: 32 `EffectType` (semántica `BACKEND_REBUILD_SPEC.md §6`)
- [ ] `TrainerCardCatalog` / `StadiumCatalog` (sin hardcodes en el engine)
- [ ] `GameEngine` fachada fina (~200 líneas) + test bot-vs-bot
      (setup + mulligan + premios + evolución + KO + victoria + sudden)
- [ ] Cobertura engine ≥90% (`RuleValidator`, `DamageCalculator`, `StatusEffectManager`)

## F2 — Aplicación + Infra (pendiente)

- [ ] Entidades EF + migración `Initial` (nada de `ddl-auto=update`)
- [ ] `DeckService` (60/4/1-básico/AS TÁCTICO/xy1) + `CardService` + `PokeApiClient` con caché
- [ ] `User/Auth/Recovery` (login 401, refresh corto, throttle, no enumerar)
- [ ] `LobbyService` separado de `GameExecutionService` (lock fila + `rowversion`,
      lobby persistido, disconnect 15s → `PAUSED`)
- [ ] Persistencia diferencial + `GameLog` append-only

## F3 — API + SignalR (pendiente)

- [ ] REST limpia `/api/v1/...` + FluentValidation + `ProblemDetails`
- [ ] `GameHub` + `StateProjector` (`Private/Public/Spectator`, no-leak) + authz por recurso
- [ ] `importar` solo `ADMIN`

## F4 — Frontend (pendiente)

- [ ] Auth, deck builder (buscar xy1, 0/60), lobby, tablero, panel por fase, log

## F5 — Hardening (pendiente)

- [ ] Seed XY1 146 versionado (sin users fijos, CLI `create-admin`), cobertura global ≥80%
