# Usuarios de prueba

Cuentas para testing manual contra el entorno de desarrollo (`./run-dev.sh`).
**Solo para desarrollo**: son credenciales débiles a propósito y no deben existir jamás en producción.

## Cuentas

| Usuario | Contraseña | Email | Rol | Para qué sirve |
|---|---|---|---|---|
| `player1` | `password1` | `player1@dotnettcg.test` | User | Jugador 1: mazos, lobby, partida |
| `player2` | `password1` | `player2@dotnettcg.test` | User | Jugador 2: unirse y jugar contra player1 |
| `admin` | `password1` | `admin@dotnettcg.test` | **Admin** | `POST /v1/cards/import` y otros endpoints `Roles = "Admin"` |

Cada cuenta tiene su **"Mazo inicial"** válido (catálogo XY1 con146 cartas ya seedeado en `dotnettcp_dev`), así que se puede crear lobby y arrancar partida de inmediato.

## Cómo fueron creadas (y cómo recrearlas)

1. Postgres de dev (contenedor `dotnettcp-dev-pg`, puerto 5432, DB `dotnettcp_dev`):

   ```bash
   docker start dotnettcp-dev-pg 2>/dev/null || docker run -d --name dotnettcp-dev-pg \
     -e POSTGRES_PASSWORD=postgres -e POSTGRES_DB=dotnettcp_dev \
     -p 5432:5432 postgres:16
   ```

2. Backend de dev corriendo (`./run-dev.sh`, o solo `dotnet run --project src/DotnetTCG.Api`) y catálogo seedeado:

   ```bash
   dotnet run --project src/DotnetTCG.Api -- seed-cards "$PWD/data/xy1.json"
   ```

3. Registro por API (minimo 8 caracteres de contraseña):

   ```bash
   curl -X POST http://localhost:5280/v1/auth/register \
     -H 'Content-Type: application/json' \
     -d '{"username":"player1","email":"player1@dotnettcg.test","password":"password1"}'
   ```

4. Promover a admin (no hay CLI `create-admin` todavía — ver `TODO.md`):

   ```bash
   docker exec dotnettcp-dev-pg psql -U postgres -d dotnettcp_dev \
     -c "UPDATE users SET \"Role\"='Admin' WHERE \"Username\"='admin';"
   ```

## Notas

- El registro crea automáticamente el mazo inicial si el catálogo está seedeado; sin catálogo el usuario se crea igual pero sin mazo.
- El login acepta **usuario o email**.
- Cambiar estas contraseñas solo afecta la base local de desarrollo (`dotnettcp_dev`); la de tests (`dotnettcp_test`, puerto 5433) es otra base y la usan los tests automatizados.
