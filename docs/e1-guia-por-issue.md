# E1 — Guía por Issue

Cómo demostrar **en vivo** cada Issue que se puede sustentar el miércoles 23 de septiembre. Cada comando de esta guía se ejecutó en un **clon limpio** del repositorio antes de publicarla; los resultados que aparecen son los que se obtuvieron.

**Formato de la sustentación** (detalle en el [Issue #26](../../../issues/26)):

| Brazo | Qué hace |
|---|---|
| **1** | Muestra el ambiente: contenedores `healthy`, `docker port`, la app con `Bus started` |
| **2** | Corre la demo del Issue |
| **3** | Explica **por qué** pasa lo que se ve |

---

## 0. Antes de cualquier Issue

Sigue el [README](../README.md#-levantar-el-proyecto-localmente), pasos 1 a 4. Al final debes tener:

- `docker compose ps` → los dos contenedores `(healthy)`
- `docker port breb-rabbitmq` → `5672/tcp -> 0.0.0.0:5673`
- La app en el **5080** con `Bus started`
- Las cuentas creadas con `scripts/cuentas-base.sql` y `scripts/cuentas-prueba-semana5.sql`

**Tres cosas que le pasan a todo el mundo:**

| Síntoma | Causa | Qué hacer |
|---|---|---|
| `dotnet ef` dice `NETSDK1004` | No compilaste antes | `dotnet build` y repetir |
| Un script dice "Nadie responde en http://localhost:5080" | La app corre en el 5051 (Visual Studio o `dotnet run` a secas) | Pasa el puerto: `… 5051` o `-Puerto 5051` |
| `/retener` responde **401** | Falta el token | Los scripts lo piden solos; a mano, ver el README |

**Para consultar la base**, lo más simple en cualquier terminal es abrir `psql` y pegar el SQL tal cual:

```bash
docker exec -it breb-postgres psql -U postgres -d brebcuentas
```

Para salir: `\q`.

---

## Issue #13 — Outbox

**Qué se demuestra:** con RabbitMQ caído, la API sigue aceptando transferencias; el evento queda guardado en la base y sale solo cuando el broker vuelve.

**1. Pedir un token** (antes de apagar nada):

```powershell
# PowerShell
$cuerpo = @{ usuario = "ana.operadora"; clave = "Operadora-2026" } | ConvertTo-Json
$token = (Invoke-RestMethod -Method Post http://localhost:5080/token -Body $cuerpo -ContentType "application/json").token
```

```bash
# bash
TOKEN=$(curl -s -X POST http://localhost:5080/token -H "Content-Type: application/json" -d '{"usuario":"ana.operadora","clave":"Operadora-2026"}' | python -c "import sys,json;print(json.load(sys.stdin)['token'])")
```

**2. Apagar la mensajería:**

```bash
docker compose stop rabbitmq
```

**3. Retener con el broker caído:**

```powershell
Invoke-RestMethod -Method Post -Headers @{ Authorization = "Bearer $token" } "http://localhost:5080/cuentas/11111111-1111-1111-1111-111111111111/retener?montoUVB=10"
```

```bash
curl -X POST -H "Authorization: Bearer $TOKEN" "http://localhost:5080/cuentas/11111111-1111-1111-1111-111111111111/retener?montoUVB=10"
```

→ **Responde 200** con un `transferenciaId`, aunque RabbitMQ está muerto.

**4. El evento está esperando** (en `psql`):

```sql
SELECT COUNT(*) FROM "OutboxMessage";
```

→ **1** (medido): el evento está guardado, esperando.

**5. Revivir la mensajería:**

```bash
docker compose start rabbitmq
```

Espera a que la consola de la app muestre `Procesando FondosRetenidos` y vuelve a contar:

```sql
SELECT COUNT(*) FROM "OutboxMessage";
```

→ **0** (medido). `Procesando FondosRetenidos` apareció **29 segundos** después de `docker compose start rabbitmq`: RabbitMQ tarda en arrancar y MassTransit en reconectarse. Tengan paciencia en vivo.

**Qué explicar (brazo 3):** el saldo y el evento se guardan en **la misma transacción** de PostgreSQL. Si la base confirma, el evento existe; publicarlo es trabajo de un proceso aparte que reintenta hasta que el broker responde. **Nadie reenvió nada.**

> **Pregunta del E1:** *¿Qué resuelve el Outbox que no resuelve publicar el evento directamente?* — Que la escritura en la base y la publicación no pueden quedar a medias: sin Outbox, si la base confirma y el broker está caído, el saldo cambia y el evento se pierde.

**Cuidado:** si esperas demasiado con RabbitMQ apagado, la saga no existe todavía; al revivir el broker nace y a los 15 s **compensa** (nadie confirmó). Es correcto, no un error.

---

## Issue #16 — Saga

**Qué se demuestra:** los dos caminos de una transferencia. Si el banco destino confirma a tiempo, se completa; si no, la saga devuelve la plata sola a los 15 segundos.

```powershell
# PowerShell
.\demo-clase3.ps1 feliz -Puerto 5080
.\demo-clase3.ps1 compensar -Puerto 5080
```

```bash
# bash
./demo-clase3.sh feliz
./demo-clase3.sh compensar
```

**Qué se debe ver:**

| Demo | Saldo inicial | Durante | Final | Sagas al final |
|---|---|---|---|---|
| `feliz` | 5000 / 0 | 4900 / 100, saga `EsperandoConfirmacion` | **4900 / 100** | 0 |
| `compensar` | 5000 / 0 | 4900 / 100, saga `EsperandoConfirmacion` | **5000 / 0** | 0 |

La demo `feliz` dice **a cuántos segundos** envió la confirmación. Medido en el clon limpio: **6 s** en bash y **5.2 s** en PowerShell. Si alguna vez dice más de 15, la saga ya compensó: es la carrera contra el reloj, y ese número es la explicación.

**Qué explicar (brazo 3):**

- En `feliz` el retenido **queda en 100**: el dinero salió hacia el banco destino. La saga termina y borra su fila.
- En `compensar` nadie confirma, el reloj de 15 s dispara `CompensarTransferencia` y el dinero vuelve. Ese reloj es un mensaje programado en RabbitMQ (`TransferenciaSagaState_delay`).

> **Pregunta del E1:** *Si la compensación llega dos veces, ¿qué pasa y por qué?* — Nada: la retención es una entidad con clave `TransferenciaId` y `Liberar()` sobre una retención ya liberada no hace nada. Es idempotente por diseño (Semana 6).

---

## Issue #18 — Deadlocks

**Qué se demuestra:** dos transacciones que se bloquean mutuamente; PostgreSQL detecta el abrazo y mata a una.

**1. Carga sobre una cuenta** (línea base; PowerShell):

```powershell
.\carga-clase4.ps1 -N 200 -Concurrencia 20 -Puerto 5080
```

→ Medido: **200 de 200, 0 errores**, p95 1 317 ms, máxima 3 204 ms, dentro del SLA de 20 s. (Throughput 3.2 transf/s: el generador de PowerShell es lento por diseño; la Semana 5 explica por qué.)

> En bash no hay `carga-clase4`; el equivalente es `python carga-clase5.py "linea base" 5080 1 200 20`.

**2. El deadlock.** Abre **dos** terminales, cada una con `psql`:

```bash
docker exec -it breb-postgres psql -U postgres -d brebcuentas
```

**Sesión A:**

```sql
BEGIN;
UPDATE "Cuentas" SET "SaldoDisponible" = "SaldoDisponible" - 10 WHERE "Id" = '11111111-1111-1111-1111-111111111111';
```

**Sesión B:**

```sql
BEGIN;
UPDATE "Cuentas" SET "SaldoDisponible" = "SaldoDisponible" - 10 WHERE "Id" = '22222222-2222-2222-2222-222222222222';
```

**Ahora se cruzan.** Sesión A (se queda esperando):

```sql
UPDATE "Cuentas" SET "SaldoDisponible" = "SaldoDisponible" - 10 WHERE "Id" = '22222222-2222-2222-2222-222222222222';
```

Sesión B (cierra el abrazo):

```sql
UPDATE "Cuentas" SET "SaldoDisponible" = "SaldoDisponible" - 10 WHERE "Id" = '11111111-1111-1111-1111-111111111111';
```

→ En aproximadamente un segundo, una de las dos muere:

```
ERROR:  deadlock detected
DETAIL:  Process 6289 waits for ShareLock on transaction 4275; blocked by process 6296.
HINT:  See server log for query details.
```

Después, en **las dos** sesiones:

```sql
ROLLBACK;
```

**Qué explicar (brazo 3):** cada transacción tiene una fila y pide la del otro; ninguna puede avanzar. PostgreSQL revisa cada `deadlock_timeout` (1 s en nuestro `docker-compose`), encuentra el ciclo y aborta una. **Detectarlo está bien; lo malo es el diseño que lo permite.** La prevención: tocar siempre las cuentas en el mismo orden (por `Id`).

> **Pregunta del E1:** *¿Por qué bloqueo pesimista y no concurrencia optimista?* — Porque en `/retener` los conflictos son **la norma**: todas las transferencias de una cuenta pelean por su única fila. Con optimista, muchas fallan y se reintentan (trabajo perdido); con `FOR UPDATE` hacen fila. Optimista es para conflictos raros.

**Experimentos de caos** (si sobra tiempo): `docker compose stop rabbitmq` durante la carga (el Outbox retiene), `docker compose stop postgres` (la app falla, mide cuánto tarda) y `docker pause breb-rabbitmq` 10 s y luego `docker unpause breb-rabbitmq`.

---

## Issue #20 — Escalamiento

**Qué se demuestra:** tres instancias compiten por las mismas colas, y agregar instancias **no** ayuda cuando todas pelean por **una** cuenta.

**1. Compilar una sola vez** y levantar tres instancias, **una terminal cada una**:

```bash
cd src/Breb.Cuentas
dotnet build
```

```bash
dotnet run --no-build --urls http://localhost:5080
```

```bash
dotnet run --no-build --urls http://localhost:5081
```

```bash
dotnet run --no-build --urls http://localhost:5082
```

> **`--no-build` es obligatorio:** tres procesos compilando a la vez se pisan los archivos.

**2. Competing consumers:** panel de RabbitMQ `http://localhost:15673` (`guest`/`guest`) → **Queues** → columna **Consumers**:

| Cola | Consumers |
|---|---|
| `FondosRetenidos` | **3** |
| `CompensarTransferencia` | **3** |
| `TransferenciaSagaState` | **3** |

> El panel actualiza el contador cada pocos segundos: si justo arrancó la tercera instancia y ven **2**, refresquen. Nos pasó en la verificación.

**3. La matriz** (desde la raíz del repo). Durante la carga, la consola de las instancias va a mostrar excepciones `40001: could not serialize access`: **son reintentos, no fallas**. Lo que importa es que no aparezca `R-FAULT`. Entre corrida y corrida, espera a que `SELECT COUNT(*) FROM "TransferenciaSagas";` dé **0**:

```bash
python carga-clase5.py "1 inst - 1 cuenta" 5080 1 300 40
python carga-clase5.py "3 inst - 1 cuenta" 5080,5081,5082 1 300 40
python carga-clase5.py "1 inst - 20 ctas"  5080 20 300 40
python carga-clase5.py "3 inst - 20 ctas"  5080,5081,5082 20 300 40
```

Medido en el clon limpio:

| Escenario | Throughput | p95 | Máxima |
|---|---|---|---|
| 1 instancia · 1 cuenta | 22.63 t/s | 3 893 ms | 6 624 ms |
| **3 instancias · 1 cuenta** | **20.93 t/s** | **6 153 ms** | **13 895 ms** |
| 1 instancia · 20 cuentas | 161.00 t/s | 342 ms | 481 ms |
| 3 instancias · 20 cuentas | 168.57 t/s | 399 ms | 527 ms |

Las cuatro corridas: 300 de 300, **0 errores**, 0 R-FAULT, 0 sagas y 0 retenciones huérfanas al final.

**Qué explicar (brazo 3):** las tres instancias calculan el mismo nombre de cola y RabbitMQ reparte los mensajes. Pero con **una** cuenta, todas las peticiones hacen fila por la misma fila de PostgreSQL (`FOR UPDATE`): más instancias solo agregan competidores por el mismo candado. Con 20 cuentas el trabajo sí se reparte.

> **Pregunta del E1:** *¿Por qué agregar instancias empeoró el rendimiento sobre una sola cuenta?* — Porque el cuello de botella no es la CPU de la API sino el candado de **una fila**. Más instancias = más transacciones esperando el mismo candado y más conexiones compitiendo.

**Los números varían entre máquinas y entre corridas** (en clase vimos dispersiones del doble). Lo que se sustenta es la **tendencia**, no el número exacto.

---

## Issue #21 — El bug de las sagas atascadas

**Qué se demuestra:** la reproducción mínima que antes dejaba sagas zombis, ahora termina con **0**.

**1. Reiniciar el laboratorio** (con 0 sagas en vuelo):

```bash
docker exec -i breb-postgres psql -U postgres -d brebcuentas < scripts/reset-laboratorio.sql
```

```powershell
Get-Content scriptseset-laboratorio.sql | docker exec -i breb-postgres psql -U postgres -d brebcuentas
```

**2. La reproducción mínima:**

```bash
python carga-clase5.py "reproducir" 5080 1 30 3
```

**3.** Espera **60 segundos** y en `psql`:

```sql
SELECT "CurrentState", COUNT(*) FROM "TransferenciaSagas" GROUP BY 1;
```

→ Medido: **0 filas**. Ninguna saga atascada (a los 20 s ya estaba vacía).

**Qué explicar (brazo 3):** el defecto venía de dos cosas medidas: reintentos a intervalo fijo que sincronizaban las colisiones (`40001`) y una máquina de estados que no toleraba eventos fuera de orden. Se corrigió con reintento **exponencial con dispersión** y declarando qué hacer con cada evento en cada estado.

> **Si aparece una retención sin liberar y sin saga:** no contradice el #21, que habla de **sagas** atascadas. Es el fallo residual documentado en la Semana 6 (2 de 300) y en el **Issue #46**: un `FondosRetenidos` que agotó sus reintentos y nunca creó su saga. Consulta en el Issue #23.

---

## Issue #23 — La retención como entidad

**Qué se demuestra:** cada retención deja rastro —quién, cuánto, cuándo se creó y cuándo se liberó— y eso permite encontrar el dinero atascado.

**0. Partir de un estado limpio** — obligatorio si antes corrieron otras demos (con 0 sagas en vuelo):

```bash
docker exec -i breb-postgres psql -U postgres -d brebcuentas < scripts/reset-laboratorio.sql
```

```powershell
Get-Content scriptseset-laboratorio.sql | docker exec -i breb-postgres psql -U postgres -d brebcuentas
```

> **Por qué, medido:** sin este paso, después de las demos del #16 la consulta de cuadre devolvió 1 fila y la de huérfanas 2. No era un error: las demos `feliz` dejan retenciones completadas que nunca se liberan, y los reinicios a mano de los scripts ponen `SaldoRetenido = 0` sin tocar la tabla `Retenciones`. `reset-laboratorio.sql` limpia las dos cosas juntas.

**1. Sesenta transferencias que se compensan solas:**

```bash
python carga-clase5.py "demo semana 6" 5080 1 60 6
```

Espera **40 segundos** (el reloj de la saga es de 15 s).

**2. El rastro** (en `psql`):

```sql
SELECT "TransferenciaId", "MontoUVB", "Liberada", "CreadaEn"::time(0), "LiberadaEn"::time(0)
FROM "Retenciones" ORDER BY "CreadaEn" DESC LIMIT 5;
```

→ Medido: cada fila con `Liberada = t`, `CreadaEn` y `LiberadaEn` unos 17 segundos después (el reloj de 15 s más el procesamiento). Las 60 de 60 quedaron liberadas.

**3. El total cuadra con el detalle** (debe devolver **cero filas**; espera a que no haya sagas vivas):

```sql
SELECT c."Id", c."SaldoRetenido", COALESCE(SUM(r."MontoUVB"),0) AS suma_retenciones_vivas
FROM "Cuentas" c
LEFT JOIN "Retenciones" r ON r."CuentaId" = c."Id" AND NOT r."Liberada"
GROUP BY c."Id", c."SaldoRetenido"
HAVING c."SaldoRetenido" <> COALESCE(SUM(r."MontoUVB"),0);
```

→ Medido: **0 filas**.

**4. Retenciones huérfanas** — vivas, sin saga, de hace más de un minuto:

```sql
SELECT r."TransferenciaId", r."CuentaId", r."MontoUVB", r."CreadaEn"::time(0)
FROM "Retenciones" r
LEFT JOIN "TransferenciaSagas" s ON s."CorrelationId" = r."TransferenciaId"
WHERE NOT r."Liberada" AND s."CorrelationId" IS NULL
  AND r."CreadaEn" < (now() AT TIME ZONE 'UTC') - interval '1 minute';
```

→ Medido: **0 filas**.

**Qué explicar (brazo 3):** antes `SaldoRetenido` era solo un número: si quedaba en 2, nadie sabía de quién eran ni desde cuándo. Ahora cada retención es una fila con `TransferenciaId`, y por eso esa última consulta se puede escribir.

> **Ojo, lo que esta consulta NO distingue:** una transferencia **completada** (camino feliz) también queda `Liberada = false` y sin saga, porque el dinero salió. Si corriste la demo `feliz`, esa aparece aquí. Distinguirlas es parte del **Issue #46** y de la actividad de la Clase 9.

---

## Las cuatro preguntas publicadas

Cada squad responde la de su Issue más una de las otras tres:

1. ¿Por qué bloqueo pesimista y no concurrencia optimista? → *Issue #18*
2. ¿Qué resuelve el Outbox que no resuelve publicar el evento directamente? → *Issue #13*
3. Si la compensación llega dos veces, ¿qué pasa y por qué? → *Issue #16*
4. ¿Por qué agregar instancias empeoró el rendimiento sobre una sola cuenta? → *Issue #20*

> Si el entorno no levanta el día de la sustentación, díganlo y muestren el Plan B. Se penaliza menos un fallo reconocido que una demostración que finge funcionar.
