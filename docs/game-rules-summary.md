# Pokémon TCG — Resumen de Reglas y Requerimientos

> Basado en el TPI de Programación III — Reglamento XY1

---

## 1. Preparación de la Partida (Setup)

1. Ambos jugadores **barajan** sus mazos y **roban 7 cartas** iniciales.
2. **Mulligan**: si un jugador no tiene ningún Pokémon Básico en su mano inicial:
   - Muestra su mano al rival, baraja y vuelve a robar 7.
   - Por cada mulligan que declare, el **oponente roba 1 carta adicional**.
   - Se repite hasta que **ambos tengan al menos un Básico**.
3. Cada jugador coloca su **Pokémon Activo boca abajo** y hasta **5 Básicos en Banca**, también boca abajo.
4. Cada jugador toma las **primeras 6 cartas** de su mazo como **cartas de Premio** (boca abajo).
5. Se lanza una **moneda** para determinar quién comienza.
6. Ambos jugadores **revelan sus Pokémon** y comienza la partida.

---

## 2. Estructura del Turno

### Fase 1 — Robo (DRAW) (obligatorio)
- Robar 1 carta del mazo.
- El jugador que empieza **no roba en su primer turno**.
- Si al robar el mazo está vacío → **pierde**.

### Fase 2 — Acciones (MAIN) (opcionales, cualquier orden)
- Colocar Pokémon Básicos en Banca.
- **Evolucionar** (restricciones: no en el turno que entró en juego, no en el primer turno del jugador).
- Unir **1 carta de Energía** por turno a cualquier Pokémon propio.
- Jugar cartas de Entrenador:
  - **Objetos**: sin límite por turno.
  - **Partidario**: solo 1 por turno.
  - **Estadio**: solo 1 por turno (reemplaza al anterior).
- **Retirar** Pokémon Activo pagando su costo de retirada (1 vez por turno).
- Usar **Habilidades** de Pokémon.

### Fase 3 — Ataque (ATTACK)
- El jugador puede atacar con su Pokémon Activo.
- **No disponible** en el primer turno del jugador que empieza.
- Atacar **finaliza el turno** automáticamente.

### Between Turns (procesamiento entre turnos)
1. Veneno (automático, 1 contador de daño)
2. Quemado (lanzar moneda: cruz → 2 contadores de daño)
3. Dormido (lanzar moneda: cara → despierta)
4. Paralizado (se cura automáticamente)
5. Efectos de Habilidades
6. Verificar si algún Pokémon quedó Fuera de Combate

---

## 3. Sistema de Ataque — Secuencia de Resolución

Pipeline (Chain of Responsibility, 7 pasos):

1. **Anunciar ataque** y verificar que el Pokémon Activo tenga la Energía requerida.
2. Si está **Confundido**: lanzar moneda → cruz → el ataque falla, el atacante recibe **3 contadores de daño**, el turno termina.
3. Realizar **selecciones** del ataque (elegir objetivo, etc.).
4. Ejecutar **requisitos previos** del ataque (lanzamientos de moneda indicados en la carta).
5. Aplicar efectos que **modifiquen o cancelen** el ataque (efectos de turnos anteriores).
6. **Cálculo y aplicación del daño**:
   - Daño base de la carta
   - Modificadores por efectos de Entrenadores/activos sobre el atacante
   - Aplicar **Debilidad** del defensor (×2)
   - Aplicar **Resistencia** del defensor (−20, mínimo 0)
   - Modificadores por efectos activos sobre el defensor
   - Colocar 1 contador de daño por cada 10 puntos de daño
7. **Efectos posteriores** al daño: condiciones especiales, descartes de Energía, daño a Banca, curación, etc.

---

## 4. Proceso de Knockout (KO)

- Un Pokémon queda KO cuando **contadores de daño × 10 ≥ su HP**.
- El Pokémon y **todas las cartas unidas** se descartan.
- El oponente toma **1 carta de Premio** (o **2** si es Pokémon-EX).
- El dueño debe **reemplazar el Activo** con uno de la Banca. Si no tiene Banca → **pierde la partida**.

---

## 5. Condiciones Especiales

| Condición | Efecto | Entre turnos |
|---|---|---|
| **Dormido** | No ataca ni se retira | Moneda: cara → despierta |
| **Quemado** | — | Moneda: cruz → 2 contadores de daño |
| **Confundido** | Al atacar: moneda → cruz → falla + 3 daño a sí mismo | — |
| **Paralizado** | No ataca ni se retira | Se cura automáticamente |
| **Envenenado** | — | 1 contador de daño automático (sin moneda) |

**Reglas de incompatibilidad:**
- Dormido, Confundido y Paralizado son **mutuamente excluyentes** (el más reciente reemplaza).
- Quemado y Envenenado son **independientes** y pueden coexistir entre sí y con cualquier otra condición.

**Se eliminan** al retirarse a la Banca o al evolucionar.

---

## 6. Condiciones de Victoria y Derrota

| Condición | Descripción |
|---|---|
| Victoria por Premios | Tomar la última carta de Premio |
| Victoria por KO total | El oponente no tiene Pokémon para reemplazar al KO |
| Derrota por mazo vacío | Al robar al inicio del turno, el mazo está vacío |
| Muerte Súbita | Si ambas condiciones se cumplen simultáneamente → nueva partida con 1 carta de Premio cada uno |

---

## 7. Tipos de Cartas

| Tipo | Subtipo | Características |
|---|---|---|
| **Pokémon** | Básico | Sin evolución previa |
| | Fase 1 | Evolución de Básico |
| | Fase 2 | Evolución de Fase 1 |
| **Pokémon-EX** | Básico | Más HP, más poder. KO → 2 Premios. "-EX" en el nombre |
| **Energía** | Básica | Sin límite de copias. 1 por turno |
| | Especial | Máx. 4 copias. Efectos adicionales |
| **Entrenador** | Objeto | Ilimitadas por turno. Efecto inmediato → descartar |
| | AS TÁCTICO | Solo 1 en todo el mazo. Alto impacto |
| | Partidario | 1 por turno. Efecto inmediato → descartar |
| | Estadio | 1 por turno. Permanece en juego (zona compartida, reemplazable) |
| | Herramienta Pokémon | Se une a un Pokémon (máx. 1). Permanece hasta que el Pokémon se descarta |
| *(Opcional)* **Megaevolución** | Evolución de Pokémon-EX | Termina el turno al megaevolucionar. KO → 2 Premios |

---

## 8. Gestión del Juego (RF-03)

### Estados de la partida
- **WAITING** → esperando segundo jugador
- **SETUP** → preparación (mulligan, colocación, Premios)
- **ACTIVE** → partida en curso (sub-estados: DRAW → MAIN → ATTACK → BETWEEN_TURNS)
- **FINISHED** → partida finalizada

### Reglas clave
- **Backend es la única fuente de verdad.** El frontend solo presenta lo que el servidor indica.
- Log completo e inmutable de todas las acciones (turno, jugador, acción, resultado).
- Persistencia automática después de cada acción relevante (reconstrucción ante desconexión).
- El motor de juego **no llama a la API externa** durante la partida — usa caché local.

---

## 9. Construcción de Mazos (RF-04)

| Regla | Valor |
|---|---|
| Tamaño del mazo | **Exactamente 60 cartas** |
| Copias del mismo nombre | **Máx. 4** (excepto Energía Básica, ilimitada) |
| AS TÁCTICO | **Máx. 1** en todo el mazo |
| Pokémon Básico | **Al menos 1** |
| Expansión base | **XY (xy1)** — 146 cartas |

### Funcionalidades del Deck Builder
- Buscar cartas de `pokemontcg.io` (v2) filtrando por set `xy1`.
- Agregar/quitar cartas al mazo.
- Crear, editar, eliminar, listar mazos por jugador (persistencia en DB).
- Validación visual: contador 0/60, límites de copias, errores descriptivos.

---

## 10. Comunicación en Tiempo Real (RF-06)

- **WebSockets obligatorios** (investigación del equipo).
- Sincronizar estado del juego a ambos jugadores tras cada acción válida.
- Notificar eventos: inicio de turno, KO, toma de Premio, condiciones especiales, fin de partida.
- **Reconexión**: un cliente desconectado debe poder reconectarse y recibir el estado actualizado.

---

## 11. Interfaz de Usuario (RF-07)

### Pantallas
- **Lobby**: crear partidas, unirse a disponibles.
- **Tablero de juego** con zonas:
  - Zona del oponente (Activo, Banca, mazo, Premios, descarte)
  - Zona del jugador (ídem)
  - Zona compartida (Estadio activo)
  - Mano del jugador

### Interacción
- **Drag & drop** para: colocar Básicos en Banca, unir Energías, equipar Herramientas, jugar Entrenadores.
- **Panel de acciones** con botones habilitados/deshabilitados según fase: Evolucionar, Unir Energía, Jugar Entrenador, Retirar, Atacar, Finalizar Turno.
- **Log de acciones** visible durante la partida.
- **Notificaciones visuales** para eventos relevantes.
- Visualización de Habilidades de Pokémon.

### Información en tiempo real
- HP actual vs HP máximo, contadores de daño, Energías unidas, Herramienta equipada.
- Condición especial activa (rotación de carta para Dormido/Confundido/Paralizado; marcador para Quemado/Envenenado).
- Cartas de Premio restantes de cada jugador.
- Cantidad de cartas en la mano del oponente (sin revelar contenido).

---

## 12. Patrones de Diseño Requeridos (RNF-04)

| Patrón | Aplicación |
|---|---|
| **State** | Estados del turno (DRAW, MAIN, ATTACK, BETWEEN_TURNS) y de la partida (WAITING, SETUP, ACTIVE, FINISHED) |
| **Strategy** | Efectos de cartas de Entrenador y efectos de cada ataque |
| **Chain of Responsibility** | Pipeline de resolución del ataque (7 pasos de RF-01c) |
| **Observer** | Notificar eventos de juego a clientes vía WebSocket |
| **Repository** | Capa de acceso a datos (cartas, mazos, estado de partida) |
| **Facade** | GameEngine: API interna simple que oculta la complejidad del motor de reglas |

---

## 13. Componentes del Game Engine (Separación)

| Componente | Responsabilidad |
|---|---|
| **RuleValidator** | Valida que las acciones sean legales según las reglas |
| **DamageCalculator** | Calcula daño (base, debilidad, resistencia, modificadores) |
| **StatusEffectManager** | Gestiona condiciones especiales y procesamiento entre turnos |
| **VictoryConditionChecker** | Verifica condiciones de victoria/derrota |
| **TurnManager** | Gestiona el ciclo de turnos y fases |

---

## 14. Cobertura de Tests Requerida (RNF-03)

- Cobertura global ≥ **80%** (JaCoCo)
- RuleValidator, DamageCalculator, StatusEffectManager ≥ **90%**
- Tests de integración: partida completa, mulligan múltiple, evolución, knockout, victoria
- Al menos 1 test E2E en frontend: crear mazo → unirse a partida → ejecutar turno

---

## 15. Seguridad (RNF-05)

- Todas las acciones validadas en **backend** (frontend es complementario).
- La **mano del oponente nunca** se envía al cliente — solo la cantidad de cartas.
- El **orden del mazo** y el contenido de las **cartas de Premio** permanecen ocultos hasta que las reglas indiquen.
- *(Opcional)* Autenticación JWT.

---

## 16. Evaluación — Puntajes

### Obligatorios (100 pts)
| Categoría | Puntos |
|---|---|
| Funcionalidad (reglas, partida completa, validaciones, condiciones especiales, daño) | 40 |
| Arquitectura y código (separación capas, game engine, patrones, código limpio, errores) | 25 |
| Base de datos | 10 |
| Frontend (UI, WebSockets, diseño, responsive) | 15 |
| Testing | 10 |

### Opcionales (+15 pts bonus)
| Mejora | Puntos |
|---|---|
| Mazo temático funcional como seed data (válido, xy1) | +10 |
| Animaciones y efectos visuales | +2 |
| Sistema de ranking/historial | +1 |
| Chat entre jugadores | +2 |

---

## 17. Tecnologías Obligatorias

| Capa | Tecnología |
|---|---|
| Backend | Java 21+, Spring Boot 3.x, Maven |
| Frontend | Angular 21+, TypeScript |
| Base de datos | PostgreSQL o MySQL |
| API externa | pokemontcg.io (v2) — con caché local |
| Tiempo real | WebSockets |
| Testing | JUnit 5, Mockito, JaCoCo, Karma/Jasmine |
| Documentación | Swagger/OpenAPI |
| Control de versiones | Git + GitHub (GitFlow) |

---

## 18. Material de Referencia

- **Reglamento oficial**: XY Rulebook (español) — adjunto al TPI
- **API de cartas**: https://docs.pokemontcg.io — endpoint: `https://api.pokemontcg.io/v2/` — set: `xy1`
- **Cliente de referencia**: Pokémon TCG Live (https://tcg.pokemon.com/es-es/tcgl/)
- **Tecnologías**: Java 21, Spring Boot 3.x, Angular 21+, PostgreSQL, WebSockets, JUnit 5, Mockito, JaCoCo, Swagger
