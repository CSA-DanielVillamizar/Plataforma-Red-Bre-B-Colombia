# Clase 5 — Instructivo Técnico
## Escalamiento horizontal, particionamiento y competing consumers
### Red Bre-B Colombia · 190304014-1 · Lunes 31 de agosto de 2026

Este documento es el **cómo**. El **qué decir** está en `Clase5_Guion_Consolidado.md`.

Todo lo que aparece aquí fue **ejecutado y verificado** el 29 de agosto de 2026 en el laboratorio real. Los números son medidos, no estimados. Donde algo salió distinto de lo esperado, está dicho.

---

## 0. Qué cambia respecto a la Semana 4

| | Semana 4 | Semana 5 |
|---|---|---|
| Instancias de la app | 1 | **3** (puertos 5080/5081/5082) |
| Cuentas de prueba | 2 | **22** (2 originales + 20 nuevas) |
| Generador de carga | PowerShell (`carga-clase4.ps1`) | **Python asincrónico** (`carga-clase5.py`) |
| Cadena de conexión | sin límite de pool | **`Maximum Pool Size=25`** |

Solo hay **una** modificación de código obligatoria (el límite del pool). Todo lo demás es montaje y medición.

---

## 1. Preparación del entorno (40 min antes)

### 1.1 Infraestructura

```bash
cd bre-b-lab
docker compose up -d
docker compose ps
```

Ambos contenedores deben decir `(healthy)`. Si RabbitMQ no levanta, revise el instructivo de la Semana 2 — el healthcheck no puede usar `rabbitmq-diagnostics`.

### 1.2 Crear las 20 cuentas de prueba

Sin esto, el experimento de particionamiento no se puede hacer.

```bash
docker exec breb-postgres psql -U postgres -d brebcuentas -c "INSERT INTO \"Cuentas\" (\"Id\",\"SaldoDisponible\",\"SaldoRetenido\") SELECT ('aaaaaaaa-0000-0000-0000-'||lpad(i::text,12,'0'))::uuid, 100000000, 0 FROM generate_series(1,20) i ON CONFLICT (\"Id\") DO UPDATE SET \"SaldoDisponible\"=100000000, \"SaldoRetenido\"=0;"
```

Verifique que quedaron 22 en total:

```bash
docker exec breb-postgres psql -U postgres -d brebcuentas -c "SELECT COUNT(*) FROM \"Cuentas\";"
```

> **Por qué saldos de 100 000 000:** una corrida de 300 transferencias por 1 UVB retiene 300. El saldo alto evita que la demo se caiga por fondos insuficientes a mitad de clase, que es un fallo tonto y muy visible.

---

## 2. El cambio de código obligatorio

### 2.1 El problema, primero

Si levanta tres instancias sin este cambio, aparecerán errores HTTP 500 a los pocos segundos. En el log:

```
Npgsql.PostgresException (0x80004005):
53300: sorry, too many clients already
```

**La causa, medida:** `max_connections` de PostgreSQL es **100 en total**. Npgsql abre hasta **100 por instancia**. Tres instancias piden 300 contra un cupo de 100.

Compruébelo usted mismo con las tres instancias arriba y **sin carga**:

```bash
docker exec breb-postgres psql -U postgres -d brebcuentas -c "SELECT count(*) FROM pg_stat_activity WHERE datname='brebcuentas';"
```

**Medido antes del arreglo: 78 conexiones ociosas.** De 100. En reposo.

### 2.2 El arreglo

En `Breb.Cuentas/Program.cs`, la cadena de conexión:

```csharp
// Maximum Pool Size: Npgsql abre hasta 100 conexiones por instancia si no se
// le dice otra cosa. PostgreSQL acepta 100 EN TOTAL (max_connections). Con tres
// instancias, la demanda es de 300 contra un cupo de 100, y la base responde
// "53300: sorry, too many clients already" — el error que vimos al escalar.
// La base de datos es un recurso COMPARTIDO: el cupo se reparte, no se replica.
var connectionString = "Host=localhost;Port=5433;Database=brebcuentas;" +
                       "Username=postgres;Password=dev_only_password;" +
                       "SSL Mode=Disable;Timeout=30;Command Timeout=60;" +
                       "Maximum Pool Size=25;Minimum Pool Size=5";
```

**Medido después del arreglo: 7 conexiones ociosas.** Y las seis corridas de la matriz terminaron con **0 errores**.

### 2.3 La fórmula para el tablero

```
Maximum Pool Size  ≤  (max_connections − reservas) / número_de_instancias
```

Con `max_connections=100`, 3 instancias y ~15 de reserva para migraciones, `psql` y herramientas:

```
(100 − 15) / 3  ≈  28   →  usamos 25, con margen
```

---

## 3. Levantar tres instancias

### 3.1 En Visual Studio 2026

Visual Studio corre **un** proyecto de inicio por instancia de VS. Para tres, lo práctico es:

1. **Ctrl+F5** en Visual Studio → esa es la instancia del puerto configurado en `launchSettings.json` (5051 por defecto).
2. Las otras dos, desde terminal.

Para la clase es más limpio usar **tres terminales** y dejar Visual Studio solo para mostrar el código.

### 3.2 Desde terminal (recomendado para la demo)

Compile **una sola vez** primero — si tres procesos compilan a la vez se pisan los archivos de salida:

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

> **`--no-build` no es un detalle menor.** Sin él, las tres instancias intentan compilar simultáneamente sobre la misma carpeta `bin/` y fallan con errores de archivo bloqueado. Es el fallo más probable de esta clase.

Espere en las tres:

```
[hh:mm:ss] INF Bus started: rabbitmq://localhost/
```

---

## 4. Verificar competing consumers

Esta es la prueba visual de la clase y **no requiere código nuevo**.

### 4.1 Por el panel de RabbitMQ

Abra `http://localhost:15672` (guest / guest) → pestaña **Queues** → mire la columna **Consumers**:

| Cola | Consumers |
|---|---|
| `FondosRetenidos` | **3** |
| `CompensarTransferencia` | **3** |
| `TransferenciaSagaState` | **3** |

### 4.2 Por línea de comandos

```bash
curl -s -u guest:guest "http://localhost:15672/api/queues/%2F" | tr ',' '\n' | grep -E '"name"|"consumers"' | paste - -
```

**Por qué funciona sin código:** `cfg.ConfigureEndpoints(context)` en `Program.cs` deriva el nombre de la cola por convención a partir del tipo del mensaje. Las tres instancias calculan **el mismo nombre**, se suscriben a **la misma cola**, y RabbitMQ reparte: cada mensaje va a **un solo** consumidor.

Eso es *competing consumers*, y ya estaba implementado desde la Semana 2.

---

## 5. La matriz de escalamiento

### 5.1 Cómo se corre

El generador está en `bre-b-lab/carga-clase5.py`:

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

> **Deje ~8 segundos entre corridas.** Las sagas de la corrida anterior siguen vivas 15 s; si no espera, compiten con la siguiente medición.
>
> **Haga una corrida de calentamiento y deséchela.** La primera paga JIT, apertura del pool y declaración de colas.

### 5.2 Los resultados medidos

300 transferencias por corrida, concurrencia 40, `Maximum Pool Size=25`, **0 errores en las seis**.

| | **1 cuenta** | **20 cuentas** | Ganancia por repartir |
|---|---|---|---|
| **1 instancia** | 17.94 t/s | **160.71 t/s** | **9.0×** |
| **2 instancias** | 13.96 t/s | 144.98 t/s | 10.4× |
| **3 instancias** | 18.21 t/s | 91.32 t/s | 5.0× |
| *Ganancia por instancias* | *1.02×* | *0.57×* | |

Latencias del mejor y del peor caso:

| Configuración | p50 | p95 | p99 | máx |
|---|---|---|---|---|
| 1 instancia · 1 cuenta | 1 623 ms | 4 869 ms | 7 894 ms | 8 358 ms |
| 1 instancia · 20 cuentas | **226 ms** | **322 ms** | **486 ms** | **842 ms** |

**Las dos conclusiones:**

1. **Agregar instancias no sirvió.** De 1 a 3 instancias sobre una cuenta: 17.94 → 18.21 t/s. Un 1.5 %. Y repartido, *empeoró*: 160.71 → 91.32 t/s.
2. **Repartir los datos sí sirvió.** La misma instancia única pasó de 17.94 a 160.71 t/s. **Nueve veces, sin infraestructura nueva.**

> **Matiz honesto que hay que decir en clase:** las tres instancias corren en **la misma máquina**, compitiendo por la misma CPU y la misma base. En producción, con máquinas separadas, la degradación no sería tan marcada. Pero la conclusión de fondo no cambia: **si el cuello de botella no está en la capa que usted multiplicó, multiplicarla no le da nada.**

---

## 6. Por qué la línea base de la Semana 4 estaba mal

No es un detalle: es la lección metodológica de la clase.

### 6.1 La evidencia

Con el generador viejo (PowerShell, un proceso por petición), subiendo solo la concurrencia:

| Concurrencia | Throughput | p50 |
|---|---|---|
| 20 | 4.50 t/s | 2 458 ms |
| 40 | 4.65 t/s | 4 133 ms |
| 60 | 4.59 t/s | 4 603 ms |

**Throughput plano, latencia creciendo en proporción.** Esa firma significa que la cola está **en el cliente**.

Con el generador asincrónico, misma app, mismo momento: **37.44 t/s**. Ocho veces y media más.

### 6.2 La prueba que cualquiera puede aplicar

> Suba la concurrencia. Si el throughput **no sube** pero la latencia **sí**, usted está midiendo su propio generador.

### 6.3 Qué se corrige y qué no

| De la Semana 4 | ¿Sigue válido? |
|---|---|
| SLA de 20 s cumplido | **Sí** — con más margen aún |
| p50/p95/p99 y la lección sobre promedios | **Sí** |
| Experimentos de caos (Outbox, fallo rápido, timeouts) | **Sí** — no dependen del throughput |
| Deadlock provocado y prevención por orden | **Sí** |
| Contención de fila 248× | **Sí** — y esta clase la explota |
| **Techo de 5.2 transferencias/segundo** | **No** — era el generador |

---

## 7. El bug que la concurrencia destapó

> **Este defecto está SIN RESOLVER y es deliberado que así llegue a clase.** Es el reto de investigación de la semana.

### 7.1 Cómo reproducirlo

```bash
docker exec breb-postgres psql -U postgres -d brebcuentas -c "TRUNCATE \"TransferenciaSagas\"; TRUNCATE \"MensajesProcesados\"; UPDATE \"Cuentas\" SET \"SaldoDisponible\"=100000000,\"SaldoRetenido\"=0;"
```

```bash
python carga-clase5.py "reproducir el bug" 5080 1 30 3
```

Espere **60 segundos** (los timeouts son de 15 s) y consulte:

```bash
docker exec breb-postgres psql -U postgres -d brebcuentas -c "SELECT \"CurrentState\", COUNT(*) FROM \"TransferenciaSagas\" GROUP BY 1;"
```

**Resultado medido: 15 sagas atascadas de 30 transferencias.** Con **una** instancia y concurrencia **3**.

### 7.2 Qué se observa

```
     CurrentState      | count
-----------------------+-------
 Compensando           |   725      ← en la corrida grande
 EsperandoConfirmacion |    36
```

En el log de la aplicación:

```
System.InvalidOperationException: No se puede liberar más de lo retenido.
MassTransit.NotAcceptedStateMachineException:
    Saga exception on receipt of FondosReintegrados:
    Not accepted in state EsperandoConfirmacion
```

**El dinero está bien.** Los saldos cuadran al centavo: `SaldoRetenido = 0` y cada cuenta vuelve a su saldo original. Lo que queda roto es la **máquina de estados**, con cientos de sagas que nunca van a terminar.

### 7.3 Dos hipótesis ya descartadas — no las repita

Ambas parecían obvias. Ambas se midieron. Ambas fallaron.

| # | Hipótesis | Cambio aplicado | Resultado medido | Veredicto |
|---|---|---|---|---|
| 1 | Mensajes duplicados (entrega al-menos-una-vez) | Guarda de idempotencia en `CompensarTransferenciaConsumer` | **0 duplicados detectados**; la guarda nunca se disparó | **Descartada** |
| 2 | Actualizaciones perdidas sobre `Cuenta` | Token de concurrencia `xmin` + reintento en `/retener` | **0 conflictos detectados** | **Descartada** |

> **Los dos cambios se dejaron en el código igual.** No son la causa, pero son correctos por principio: la entrega al-menos-una-vez es real, y la saga ya tenía token de concurrencia desde la Semana 3 mientras la cuenta bancaria no. Que un arreglo no resuelva *este* problema no lo vuelve incorrecto.

### 7.4 Por dónde seguir

Pistas para quien tome el reto:

- El `Schedule` / `Unschedule` del `TimeoutConfirmacion` en `TransferenciaSaga.cs`.
- Qué ocurre cuando `CompensarTransferencia` **falla** y MassTransit reintenta: la saga sigue en `Compensando` y nunca recibe `FondosReintegrados`.
- El error dice `Not accepted in state EsperandoConfirmacion`, o sea que una saga que **aún no había expirado** recibió un `FondosReintegrados`. ¿De quién era ese mensaje?
- `During(Compensando, When(FondosReintegradosEvt).Finalize())` no contempla que el reintegro **falle**. No hay camino de salida para una compensación que no se puede aplicar.

### 7.5 Por qué nunca lo vimos

Porque **todas** las demos de las Semanas 2, 3 y 4 usaban **una sola transferencia**.

Verificado hoy: con una transferencia, ambos caminos siguen perfectos.

| Demo | Resultado |
|---|---|
| Camino feliz | `Disp=4900 · Ret=100` · saga finalizada |
| Compensación por timeout | `Disp=5000 · Ret=0` · saga finalizada |

> **La lección:** un sistema probado con un solo usuario está sin probar. La concurrencia no es un caso extremo — es el caso normal.

---

## 8. Diagnóstico rápido

| Síntoma | Causa probable | Qué hacer |
|---|---|---|
| Errores de archivo bloqueado al arrancar | Tres `dotnet run` compilando a la vez | `dotnet build` una vez, luego `--no-build` |
| HTTP 500 masivos al escalar | `53300: too many clients` | Poner `Maximum Pool Size=25` |
| `consumers: 1` en RabbitMQ | Solo levantó una instancia | Verifique `Bus started` en las tres |
| Throughput plano al subir concurrencia | El generador está saturado | Use `carga-clase5.py`, no PowerShell |
| `Cuenta no existe` | Faltan las 20 cuentas de prueba | Corra el INSERT de la sección 1.2 |
| Sagas que no bajan de cientos | **Es el bug de la sección 7** | `TRUNCATE "TransferenciaSagas"` entre corridas |
| Números muy distintos entre corridas | Sin calentamiento, o sagas anteriores vivas | Descarte la primera; espere 8 s entre corridas |

---

## 9. Comandos de verificación

**Conexiones en uso:**

```bash
docker exec breb-postgres psql -U postgres -d brebcuentas -c "SELECT count(*) FROM pg_stat_activity WHERE datname='brebcuentas';"
```

**Estado de las sagas:**

```bash
docker exec breb-postgres psql -U postgres -d brebcuentas -c "SELECT \"CurrentState\", COUNT(*) FROM \"TransferenciaSagas\" GROUP BY 1;"
```

**Consistencia del dinero (debe dar `retenido = 0`):**

```bash
docker exec breb-postgres psql -U postgres -d brebcuentas -c "SELECT SUM(\"SaldoRetenido\") AS retenido, SUM(\"SaldoDisponible\") AS disponible FROM \"Cuentas\";"
```

**Dejar el laboratorio limpio al terminar:**

```bash
docker exec breb-postgres psql -U postgres -d brebcuentas -c "TRUNCATE \"TransferenciaSagas\"; TRUNCATE \"MensajesProcesados\"; UPDATE \"Cuentas\" SET \"SaldoDisponible\"=5000, \"SaldoRetenido\"=0 WHERE \"Id\" IN ('11111111-1111-1111-1111-111111111111','22222222-2222-2222-2222-222222222222');"
```

---

## 10. Entregables de la semana

- [ ] `Maximum Pool Size` en la cadena de conexión, con el comentario que explica el porqué.
- [ ] `carga-clase5.py` en el repositorio.
- [ ] Las 20 cuentas de prueba documentadas como script SQL.
- [ ] La matriz de escalamiento (6 corridas) publicada en el Issue de Semana 5.
- [ ] La corrección de la línea base de la Semana 4, con la evidencia del generador saturado.
- [ ] **Issue propio abierto para el bug de las sagas atascadas**, con las dos hipótesis descartadas documentadas para que nadie repita el camino.

---

*Instructivo técnico de la Clase 5. Todos los números fueron medidos el 29 de agosto de 2026 en el laboratorio local.*
