# Clase 5 — Instructivo Técnico
## Escalamiento, particionamiento, competing consumers y el defecto que perdía dinero
### Red Bre-B Colombia · 190304014-1 · Miércoles 2 de septiembre de 2026

Este documento es el **cómo**. El **qué decir** está en `Clase5_Guion_Consolidado.md`.

Todo lo que aparece aquí fue **ejecutado y verificado** el 29 y 30 de agosto de 2026 en el laboratorio real. Los números son medidos, no estimados. Donde algo salió distinto de lo esperado —o donde me equivoqué— está dicho.

---

## 0. Qué cambia respecto a la Semana 4

| | Semana 4 | Semana 5 |
|---|---|---|
| Instancias de la app | 1 | **3** (puertos 5080/5081/5082) |
| Cuentas de prueba | 2 | **22** (2 originales + 20 nuevas) |
| Generador de carga | PowerShell | **Python asincrónico** (`carga-clase5.py`) |
| Cadena de conexión | sin límite de pool | **`Maximum Pool Size=25`** |
| Reintentos del bus | `Interval(3, 2s)` | **`Exponential(10, …)`** |
| Retención de fondos | lectura + escritura | **`SELECT … FOR UPDATE`** |
| Concurrencia en `Cuenta` | sin protección | **token `xmin`** |

---

## 1. Preparación del entorno (40 min antes)

### 1.1 Infraestructura

```bash
cd bre-b-lab
docker compose up -d
```

Ambos contenedores deben decir `(healthy)`.

### 1.2 Crear las 20 cuentas de prueba

Sin esto no se puede hacer el experimento de particionamiento.

```bash
docker exec -i breb-postgres psql -U postgres -d brebcuentas < scripts/cuentas-prueba-semana5.sql
```

Deben quedar 22 cuentas en total.

> **Por qué saldos de 100 000 000:** una corrida de 300 transferencias por 1 UVB retiene 300. El saldo alto evita que la demo se caiga por fondos insuficientes a mitad de clase.

---

## 2. Los cinco cambios de código

Los cinco fueron descubiertos **midiendo**, no razonando. Cada uno viene con el número que lo justifica.

### 2.1 Límite del pool de conexiones

**El síntoma:** al levantar tres instancias aparecen HTTP 500.

```
Npgsql.PostgresException (0x80004005):
53300: sorry, too many clients already
```

**La causa medida:** `max_connections` de PostgreSQL es **100 en total**. Npgsql abre hasta **100 por instancia**. Tres instancias piden 300 contra un cupo de 100.

Compruébelo con las tres instancias arriba y **sin carga**:

```bash
docker exec breb-postgres psql -U postgres -d brebcuentas -c "SELECT count(*) FROM pg_stat_activity WHERE datname='brebcuentas';"
```

**Antes del arreglo: 78 conexiones ociosas.** De 100. En reposo.

**El arreglo**, en `Program.cs`:

```csharp
var connectionString = "Host=localhost;Port=5433;Database=brebcuentas;" +
                       "Username=postgres;Password=dev_only_password;" +
                       "SSL Mode=Disable;Timeout=30;Command Timeout=60;" +
                       "Maximum Pool Size=25;Minimum Pool Size=5";
```

**Después: 7 conexiones ociosas.** Y 0 errores en las seis corridas de la matriz.

**La fórmula para el tablero:**

```
Maximum Pool Size  ≤  (max_connections − reservas) / número_de_instancias
(100 − 15) / 3  ≈  28   →  usamos 25, con margen
```

### 2.2 Reintentos con espera exponencial *(la causa raíz de las sagas atascadas)*

**El síntoma:** cientos de sagas atrapadas en `Compensando`, reintentando para siempre.

**Cómo se encontró:** no razonando, sino leyendo el rastro de **una sola** saga, línea por línea:

```bash
docker exec breb-postgres psql -U postgres -d brebcuentas -t -c "SELECT \"CorrelationId\" FROM \"TransferenciaSagas\" WHERE \"CurrentState\"='Compensando' LIMIT 1;"
grep "<ese-id>" /tmp/app.log
```

El rastro mostraba la compensación ejecutándose 4 veces y fallando. Y en el detalle:

```
40001: could not serialize access due to read/write dependencies among transactions
```

**La explicación:** el repositorio de sagas de MassTransit sobre EF trabaja en aislamiento **Serializable**. Cuando varias sagas compensan a la vez sobre la misma cuenta, PostgreSQL aborta algunas. **Eso no es un bug** — es la base protegiendo la consistencia, y la documentación de PostgreSQL dice explícitamente que el 40001 **se debe reintentar**.

**La configuración que había:**

```csharp
cfg.UseMessageRetry(r => r.Interval(3, TimeSpan.FromSeconds(2)));
```

Dos defectos:

1. **Tres intentos no alcanzan** bajo esa contención.
2. **El intervalo fijo sincroniza las colisiones.** Todas las transacciones en conflicto esperan exactamente 2 segundos y **vuelven a chocar juntas**. El reintento a intervalo fijo no resuelve la contención: la repite en tandas.

**Medido con el intervalo fijo, con UNA instancia y concurrencia 3:** 146 errores 40001 → 18 `R-FAULT` → **17 sagas atrapadas**.

**El arreglo:**

```csharp
cfg.UseMessageRetry(r => r.Exponential(
    retryLimit:     10,
    minInterval:    TimeSpan.FromMilliseconds(100),
    maxInterval:    TimeSpan.FromSeconds(5),
    intervalDelta:  TimeSpan.FromMilliseconds(300)));
```

**Resultado inmediato: `R-FAULT` pasó de 18 a 0**, y las sagas atascadas de 17 a 1.

### 2.3 Bloqueo pesimista sobre la cuenta *(el defecto que perdía dinero)*

**Este es el hallazgo más grave de la semana.**

**Reprodúzcalo así** — 60 transferencias de 1 UVB sobre la misma cuenta:

```bash
python carga-clase5.py "prueba de perdida" 5080 1 60 6
```

```bash
docker exec breb-postgres psql -U postgres -d brebcuentas -c "SELECT \"SaldoRetenido\" FROM \"Cuentas\" WHERE \"Id\"='aaaaaaaa-0000-0000-0000-000000000001';"
```

Debería dar **60**. Sin el arreglo daba **11**.

**Cuarenta y nueve transferencias devolvieron HTTP 200 al usuario y el dinero nunca se movió.**

**El mecanismo — la actualización perdida:**

```
Petición A lee saldo: Retenido = 10
Petición B lee saldo: Retenido = 10     ← ambas leen lo mismo
Petición A escribe:   Retenido = 11
Petición B escribe:   Retenido = 11     ← pisa a A. La retención de A se perdió.
```

Con **una** transferencia a la vez es imposible. Con tres al tiempo, es inevitable. Por eso ninguna demo de las Semanas 2, 3 y 4 lo mostró.

**El camino hasta el arreglo — con un intento fallido en medio:**

Primero probé concurrencia **optimista**: token `xmin` sobre `Cuenta` más un bucle de reintentos con espera exponencial. **Lo medí y no alcanzó:** con 30 transferencias concurrentes sobre la misma cuenta, algunas peticiones perdían las ocho rondas y morían en HTTP 500.

Entonces cambié a **pesimista**:

```csharp
await using var tx = await db.Database.BeginTransactionAsync();

var cuenta = await db.Cuentas
    // OJO: hay que pedir xmin EXPLICITAMENTE. En PostgreSQL "SELECT *" no
    // incluye columnas de sistema, y como Cuenta usa xmin como token de
    // concurrencia, EF lo busca y falla con "42703: column b.xmin does not exist".
    .FromSql($"SELECT *, xmin FROM \"Cuentas\" WHERE \"Id\" = {cuentaId} FOR UPDATE")
    .FirstOrDefaultAsync();

if (cuenta is null) return Results.NotFound("Cuenta no existe.");

cuenta.Retener(montoUVB);
await publishEndpoint.Publish(new FondosRetenidos { … });
await db.SaveChangesAsync();
await tx.CommitAsync();
```

**La regla que sale de esto, y es la más útil del día:**

| Frecuencia de conflictos | Estrategia | Por qué |
|---|---|---|
| **Raros** | Optimista (detectar y reintentar) | El reintento casi nunca ocurre; no se paga bloqueo |
| **Frecuentes** | **Pesimista** (`FOR UPDATE`) | Reintentar es trabajo desperdiciado; hacer fila es lo correcto |

Aquí los conflictos son la norma: todas las transferencias de una cuenta pelean por su única fila. Es exactamente lo que hace el libro mayor de un banco.

**El token `xmin` se conserva** en `CuentasDbContext` como red de seguridad: si alguien escribe en `Cuentas` por otro camino sin tomar el bloqueo, el token convierte una pérdida silenciosa en un error visible.

**Resultado medido: `SaldoRetenido` = 60/60 exactos, y 300/300 con concurrencia 40.**

### 2.4 La saga tolera eventos fuera de orden

**El síntoma:**

```
MassTransit.NotAcceptedStateMachineException:
    Saga exception on receipt of FondosReintegrados:
    Not accepted in state EsperandoConfirmacion
```

**Cómo puede pasar:** el timeout **sí** se disparó y publicó la compensación, pero la transacción que además movía la saga a `Compensando` fue abortada con 40001. La compensación ya estaba en el Outbox y salió igual; la saga se quedó en `EsperandoConfirmacion`.

**El arreglo**, en `TransferenciaSaga.cs`:

```csharp
During(EsperandoConfirmacion,
    …
    When(FondosReintegradosEvt)          // llegada fuera de orden
        .Unschedule(TimeoutConfirmacion)
        .Finalize());

During(Compensando,
    When(FondosReintegradosEvt).Finalize(),
    When(TimeoutConfirmacion.Received)   // timeout tardío: ignorar
        .Then(ctx => ctx.Saga.MotivoCompensacion ??= "Timeout duplicado ignorado"));
```

> **El principio:** bajo entrega al-menos-una-vez y con reintentos, los mensajes **no llegan en el orden que uno dibujó en el tablero**. Una máquina de estados debe tolerar **todo** evento que físicamente pueda llegarle en ese estado. Si no, lanza excepción, el mensaje se reintenta hasta agotarse, y la saga queda zombi.

**Resultado medido: de 12 excepciones de estado inválido a 0.**

### 2.5 Idempotencia por transferencia, no por mensaje

La guarda usaba `context.MessageId`. No servía: si la saga **republica** `CompensarTransferencia` (porque su transacción anterior se abortó), el mensaje nuevo trae un `MessageId` distinto y la guarda no lo reconoce como duplicado.

El hecho que importa no es *"ya procesé este mensaje"* sino **"esta transferencia ya fue compensada"**:

```csharp
var claveIdempotencia = msg.TransferenciaId;   // estable a través de republicaciones

bool yaCompensada = await _db.MensajesProcesados
    .AnyAsync(m => m.MessageId == claveIdempotencia);

if (yaCompensada)
{
    // No es error: republicamos el evento para que la saga pueda cerrar.
    await _publishEndpoint.Publish(new FondosReintegrados { … });
    await _db.SaveChangesAsync();
    return;
}
```

**Resultado medido: 272 duplicados frenados correctamente en una corrida de 300**, y 0 violaciones del invariante de dominio.

---

## 3. Levantar tres instancias

Compile **una sola vez** — si tres procesos compilan a la vez se pisan los archivos de salida:

```bash
cd bre-b-lab/Breb.Platform/Breb.Cuentas
dotnet build
```

Después, una terminal por instancia:

```bash
dotnet run --no-build --urls http://localhost:5080
```

```bash
dotnet run --no-build --urls http://localhost:5081
```

```bash
dotnet run --no-build --urls http://localhost:5082
```

> **`--no-build` no es un detalle menor.** Es el fallo más probable de esta clase.

Espere `Bus started: rabbitmq://localhost/` en las tres.

---

## 4. Verificar competing consumers

No requiere código nuevo. Panel de RabbitMQ (`http://localhost:15672`, guest/guest) → **Queues** → columna **Consumers**:

| Cola | Consumers |
|---|---|
| `FondosRetenidos` | **3** |
| `CompensarTransferencia` | **3** |
| `TransferenciaSagaState` | **3** |

**Por qué funciona:** `cfg.ConfigureEndpoints(context)` deriva el nombre de la cola por convención. Las tres instancias calculan **el mismo nombre**, se suscriben a **la misma cola**, y RabbitMQ reparte.

---

## 5. La matriz de escalamiento

### 5.1 Higiene entre corridas — no es opcional

**Esto me costó una conclusión falsa.** En una medición creí que quedaban 186 sagas rotas; eran mensajes viejos de corridas anteriores que nunca purgué de RabbitMQ.

Antes de **cada** corrida:

1. Espere a que no queden sagas en vuelo:

```bash
docker exec breb-postgres psql -U postgres -d brebcuentas -c "SELECT COUNT(*) FROM \"TransferenciaSagas\";"
```

2. Purgue las colas:

```bash
for q in FondosRetenidos CompensarTransferencia TransferenciaSagaState FondosRetenidos_error CompensarTransferencia_error TransferenciaSagaState_error; do curl -s -u guest:guest -X DELETE "http://localhost:15672/api/queues/%2F/$q/contents" -o /dev/null; done
```

3. Limpie la base y descarte una corrida de calentamiento.

### 5.2 Cómo se corre

```bash
python carga-clase5.py <etiqueta> <puertos> <n_cuentas> <total> <concurrencia>
```

**Bloque A — toda la carga sobre UNA cuenta:**

```bash
python carga-clase5.py "1 inst - 1 cuenta" 5080 1 300 40
```

```bash
python carga-clase5.py "2 inst - 1 cuenta" 5080,5081 1 300 40
```

```bash
python carga-clase5.py "3 inst - 1 cuenta" 5080,5081,5082 1 300 40
```

**Bloque B — carga repartida en 20 cuentas:**

```bash
python carga-clase5.py "1 inst - 20 ctas" 5080 20 300 40
```

```bash
python carga-clase5.py "2 inst - 20 ctas" 5080,5081 20 300 40
```

```bash
python carga-clase5.py "3 inst - 20 ctas" 5080,5081,5082 20 300 40
```

### 5.3 Los resultados medidos

300 transferencias por corrida, concurrencia 40, **0 errores en las seis**, integridad verificada `300/300`.

| | **1 cuenta** | **20 cuentas** | Ganancia por repartir |
|---|---|---|---|
| **1 instancia** | 34.06 t/s | 66.90 t/s | 2.0× |
| **2 instancias** | 32.91 t/s | 81.11 t/s | 2.5× |
| **3 instancias** | **9.25 t/s** | **128.16 t/s** | **13.9×** |
| *Ganancia por instancias* | *0.27× (peor)* | *1.9× (mejor)* | |

Latencias del mejor y del peor caso:

| Configuración | p50 | p95 | p99 | máx |
|---|---|---|---|---|
| 3 instancias · 1 cuenta | 1 012 ms | 18 287 ms | **26 367 ms** | 32 371 ms |
| 3 instancias · 20 cuentas | **257 ms** | **550 ms** | **838 ms** | 1 607 ms |

**Las tres conclusiones:**

1. **Con contención, agregar instancias empeora.** De 34.06 a 9.25 t/s — casi cuatro veces peor. Y el p99 llega a **26 segundos**, rompiendo el SLA de 20 que prometimos. Es un *lock convoy*: todos hacen fila por el candado de la misma fila.
2. **Sin contención, agregar instancias funciona.** De 66.90 a 128.16 t/s — 1.9× con 3 instancias. **Esto es escalamiento horizontal real.**
3. **Las mismas tres instancias: 9.25 o 128.16 t/s.** Trece veces de diferencia, y lo único que cambia es dónde cae la carga.

> **La lección:** escalar horizontalmente no es bueno ni malo. Es la **segunda** decisión. La primera es eliminar el punto de contención — y si se salta ese paso, agregar servidores empeora las cosas y encima cuesta más.

---

## 6. Por qué la línea base de la Semana 4 estaba mal

Dos motivos independientes, y ambos importan.

### 6.1 El generador estaba saturado

Con el script de PowerShell (un proceso por petición), subiendo **solo** la concurrencia:

| Concurrencia | Throughput | p50 |
|---|---|---|
| 20 | 4.50 t/s | 2 458 ms |
| 40 | 4.65 t/s | 4 133 ms |
| 60 | 4.59 t/s | 4 603 ms |

**Throughput plano, latencia creciendo lineal.** La cola estaba **en el cliente**.

> **La prueba que cualquiera puede aplicar:** suba la concurrencia. Si el throughput **no sube** pero la latencia **sí**, está midiendo su propio generador.

### 6.2 El sistema no estaba haciendo el trabajo

Más incómodo todavía: aquellas mediciones corrían sobre el sistema **que perdía el 80 % de las retenciones**. Era rápido porque no hacía el trabajo. Al arreglarlo, el throughput bajó — y eso está bien.

> **La velocidad de un sistema incorrecto no es un dato: es una ilusión.**

### 6.3 Qué se corrige y qué no

| De la Semana 4 | ¿Sigue válido? |
|---|---|
| SLA de 20 s cumplido | **Sí** con 1 instancia; **no** con 3 sin particionar (p99 de 26 s) |
| p50/p95/p99 y la lección sobre promedios | **Sí** |
| Experimentos de caos (Outbox, fallo rápido, timeouts) | **Sí** |
| Deadlock provocado y prevención por orden | **Sí** |
| Contención de fila 248× | **Sí** — y esta clase la explota |
| **Techo de 5.2 transferencias/segundo** | **No** — generador saturado y sistema incorrecto |

---

## 7. Verificación de que todo quedó bien

### 7.1 Sin regresión en las demos anteriores

Verificado tras los cinco cambios, con una sola transferencia:

| Demo de la Semana 3 | Resultado |
|---|---|
| Camino feliz | `Disp=4900 · Ret=100` · saga finalizada |
| Compensación por timeout | `Disp=5000 · Ret=0` · saga finalizada |

> **Cuidado al probar el camino feliz a mano:** el timeout es de 15 segundos y cada `docker exec psql` tarda varios. Si consulta el saldo antes de confirmar, la confirmación puede llegar tarde y el sistema compensará — correctamente. **Confirme primero, consulte después.**

### 7.2 Bajo carga

Corrida limpia de 300 transferencias, 20 cuentas, concurrencia 40:

| Métrica | Antes | Después |
|---|---|---|
| Retenciones perdidas | **49 de 60** | **0** |
| Sagas atascadas | 17 (con 30 transf.) | **0** (con 300) |
| `R-FAULT` | 18 | **0** |
| Eventos en estado inválido | 12 | **0** |
| Invariante de dominio violado | 86 | **0** |
| Cuentas descuadradas | 3 | **0** |
| Errores 40001 | 146 (fatales) | 1 602 (**todos absorbidos**) |

> El 40001 no desapareció — **aumentó**, porque ahora hay más trabajo concurrente real. La diferencia es que ahora **todos se reintentan con éxito**. Un sistema sano no es el que no tiene conflictos: es el que los resuelve solo.

---

## 8. Diagnóstico rápido

| Síntoma | Causa probable | Qué hacer |
|---|---|---|
| Errores de archivo bloqueado al arrancar | Tres `dotnet run` compilando a la vez | `dotnet build` una vez, luego `--no-build` |
| HTTP 500 masivos al escalar | `53300: too many clients` | `Maximum Pool Size=25` |
| `42703: column b.xmin does not exist` | `SELECT *` no trae columnas de sistema | Usar `SELECT *, xmin … FOR UPDATE` |
| `consumers: 1` en RabbitMQ | Solo levantó una instancia | Verifique `Bus started` en las tres |
| Throughput plano al subir concurrencia | El generador está saturado | Use `carga-clase5.py`, no PowerShell |
| `Cuenta no existe` | Faltan las 20 cuentas de prueba | Corra el script de la sección 1.2 |
| Sagas que no bajan de cientos | Colas sin purgar entre corridas | Purgue RabbitMQ **y** espere reposo |
| Números muy distintos entre corridas | Sin calentamiento o sin higiene | Descarte la primera; aplique la sección 5.1 |

---

## 9. Comandos de verificación

**Conexiones en uso:**

```bash
docker exec breb-postgres psql -U postgres -d brebcuentas -c "SELECT count(*) FROM pg_stat_activity WHERE datname='brebcuentas';"
```

**Estado de las sagas (debe quedar vacío):**

```bash
docker exec breb-postgres psql -U postgres -d brebcuentas -c "SELECT \"CurrentState\", COUNT(*) FROM \"TransferenciaSagas\" GROUP BY 1;"
```

**Integridad del dinero (`retenido` debe ser 0 al final):**

```bash
docker exec breb-postgres psql -U postgres -d brebcuentas -c "SELECT SUM(\"SaldoRetenido\") AS retenido, SUM(\"SaldoDisponible\") AS disponible FROM \"Cuentas\";"
```

**Dejar el laboratorio limpio:**

```bash
docker exec breb-postgres psql -U postgres -d brebcuentas -c "TRUNCATE \"TransferenciaSagas\"; TRUNCATE \"MensajesProcesados\"; UPDATE \"Cuentas\" SET \"SaldoDisponible\"=5000, \"SaldoRetenido\"=0 WHERE \"Id\" IN ('11111111-1111-1111-1111-111111111111','22222222-2222-2222-2222-222222222222');"
```

---

## 10. Entregables de la semana

- [ ] Los cinco cambios de código, cada uno con el comentario que explica **el número** que lo justifica.
- [ ] `carga-clase5.py` en el repositorio.
- [ ] Las 20 cuentas de prueba como script SQL versionado.
- [ ] La matriz de escalamiento (6 corridas) publicada en el Issue de Semana 5.
- [ ] La corrección de la línea base de la Semana 4, con las dos razones.
- [ ] La tabla de antes/después del defecto, con integridad verificada.

---

## 11. Lo que queda abierto

**El modelo de dominio sigue siendo débil.** `Cuenta.LiberarRetencion` valida contra el **total** retenido de la cuenta, no contra la retención de **esa** transferencia:

```csharp
if (monto > SaldoRetenido)
    throw new InvalidOperationException("No se puede liberar más de lo retenido.");
```

Un solo número `SaldoRetenido` no puede responder *"¿está todavía la retención de la transferencia X?"*. Hoy funciona porque la idempotencia por transferencia lo compensa, pero lo correcto sería modelar cada **retención como una entidad propia**.

Es el reto de investigación de la semana.

---

*Instructivo técnico de la Clase 5. Todos los números fueron medidos el 29 y 30 de agosto de 2026 en el laboratorio local.*
