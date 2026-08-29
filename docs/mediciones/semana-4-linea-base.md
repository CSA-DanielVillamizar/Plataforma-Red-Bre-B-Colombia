# Semana 4 — Línea base medida

> Ejecución real sobre el laboratorio local, 29 de agosto de 2026.
> Cierra el Issue #18. Todos los números de este documento salieron de una corrida
> real; ninguno es estimado. Donde algo salió distinto de lo esperado, está dicho.

**Entorno.** .NET 8 · MassTransit 8.5.2 · RabbitMQ (`masstransit/rabbitmq`) ·
PostgreSQL 16 en Docker, puerto 5433 · aplicación en `http://localhost:5080` ·
todo en una sola máquina Windows 11. Un solo nodo, un solo consumidor.

---

## 1. Prueba de carga

`.\carga-clase4.ps1 -N 200 -Concurrencia 20 -Puerto 5080`

| Métrica | Calentamiento (20/5) | **Medición (200/20)** | Meta |
|---|---|---|---|
| Duración total | 25.7 s | **38.8 s** | — |
| Throughput | 0.8 transf/s | **5.2 transf/s** | — |
| Latencia p50 | 84 ms | **71 ms** | < 2 s |
| Latencia p95 | 1 363 ms | **992 ms** | < 10 s |
| Latencia p99 | 1 363 ms | **2 086 ms** | — |
| Latencia MÁXIMA | 1 363 ms | **2 147 ms** | < 20 s (SLA) |
| Errores | 0 / 20 | **0 / 200** | 0 |

**El SLA se cumple con 9× de margen.** La peor transferencia de doscientas tardó
2.1 segundos contra un compromiso de 20.

Dos lecturas que valen más que los números:

- **La media habría mentido.** El promedio ronda los 300 ms, pero el p99 es
  2 086 ms: *siete veces peor*. Si prometiéramos el SLA sobre el promedio,
  una de cada cien personas viviría una experiencia que nunca medimos.
- **Más carga bajó la latencia.** 200 transferencias dan mejor p50 (71 ms) que
  20 (84 ms), porque la primera corrida paga el arranque en frío: JIT, apertura
  del pool de conexiones, declaración de colas. *Toda medición sin calentamiento
  mide el arranque, no el sistema.* Por eso la corrida de calentamiento existe.

---

## 2. Dónde se va el tiempo

`pg_stat_statements`, las cinco consultas más costosas por tiempo acumulado:

| ms total | llamadas | ms media | Consulta |
|---|---|---|---|
| **8 099.7** | **510** | **15.88** | `UPDATE "Cuentas" SET "SaldoDisponible"=$1, "SaldoRetenido"=$2 WHERE ...` |
| 3 206.6 | 1 | 3 206.59 | `INSERT INTO "TransferenciaSagas" (...)` — carga masiva del experimento de índice |
| 2 276.5 | 5 | 455.29 | `INSERT INTO "TransferenciaSagas" (...)` |
| 1 398.6 | 1 | 1 398.65 | Bloque `DO` del banco de pruebas |
| 1 079.6 | 5 | 215.92 | `DELETE FROM "TransferenciaSagas"` |

El primer renglón es el sistema real; los otros cuatro son el instrumental de
medición. **El cuello de botella es el `UPDATE` sobre `Cuentas`.**

### El hallazgo de la semana

Ese `UPDATE` afecta **una sola fila, localizada por clave primaria**. Medido en
aislamiento:

```
Update on "Cuentas"  (actual time=0.064..0.064 rows=0 loops=1)
  Buffers: shared hit=4
```

**0.064 ms aislado. 15.88 ms bajo carga. Una degradación de 248×.**

Postgres no leyó ni un bloque de más: `shared hit=4`, todo en memoria. El tiempo
no se fue en trabajo — se fue **esperando**. Las 200 transferencias concurrentes
golpean *la misma fila* de la misma cuenta, y una fila solo puede ser modificada
por una transacción a la vez. El log de PostgreSQL lo confirma con
**50 registros de `still waiting for ShareLock`**.

> **Ningún índice arregla esto.** Un índice acelera *encontrar* la fila; aquí
> encontrarla ya cuesta 0.064 ms. El costo es el turno para escribirla.
> Es contención, no lectura. Y es exactamente el problema que abre la Semana 5:
> para escalar hay que **repartir las cuentas**, no afinar la consulta.

---

## 3. El índice: ¿mejoró o no?

Metodología: 100 000 filas sintéticas en `TransferenciaSagas`, consulta por
`CurrentState`, medida con y sin el índice.

| | Plan elegido | Tiempo | Bloques leídos |
|---|---|---|---|
| **Con** `IX_TransferenciaSagas_CurrentState` | `Index Only Scan` | **22.4 ms** | **34** |
| **Sin** índice | `Seq Scan` | 39.4 ms | 1 201 |

**Mejora del 43 % en tiempo y 35× menos bloques leídos.** La segunda cifra es la
que importa: leer 34 bloques en vez de 1 201 es lo que hace que la mejora se
sostenga cuando la tabla crezca.

**El costo de escritura:** insertar 20 000 filas tomó 479.6 ms sin índice, y
497 / 382 / 506 ms con él en tres corridas. Una de las corridas *con* índice fue
más rápida que la corrida sin índice. **A esta escala el ruido de medición es
mayor que el costo del índice: no es medible.** Decirlo así es más honesto que
inventar un porcentaje.

**El costo en disco sí se mide:** 1 360 kB de índice sobre 20 MB de tabla, un
**6.6 % de sobrecosto**. Barato para lo que da.

**Un detalle que nadie esperaba:** el índice **ya existía**. Entró en la migración
de la Semana 3 vía `e.HasIndex(s => s.CurrentState)`, sin que nadie lo decidiera
conscientemente. Hubo que *quitarlo* para poder medir el escenario sin índice.
Lección: EF Core crea índices por su cuenta; si nunca miras el esquema real, no
sabes qué está corriendo en tu base.

**Y el que NO se debe agregar:** el plan de `Cuentas` usa `Seq Scan` — y está
bien. La tabla tiene 2 filas; recorrerlas cuesta menos que abrir un índice. El
planificador tiene razón y nosotros nos equivocaríamos al "corregirlo".
**`Seq Scan` no es un defecto: en tablas pequeñas es la decisión correcta.**

---

## 4. Deadlock: provocado y capturado

Dos sesiones `psql` tomando las mismas dos filas en **orden inverso**.

Resultado real:

```
Sesión A:  ERROR:  deadlock detected
           ROLLBACK
Sesión B:  COMMIT
```

Y en el log del servidor, el aviso previo que dejó `log_lock_waits=on`:

```
LOG:  process 40472 still waiting for ShareLock on transaction 10172 after 1000.147 ms
ERROR:  deadlock detected
```

PostgreSQL esperó 1 segundo (`deadlock_timeout=1s`), detectó el ciclo, **eligió
una víctima y la abortó**. La otra transacción terminó bien. El sistema se
desbloqueó solo, sin intervención.

**Prevención:** el deadlock necesita que dos transacciones tomen los mismos
recursos en orden distinto. Si **todas** ordenan sus accesos por el mismo
criterio — por ejemplo, `ORDER BY "Id"` antes de bloquear — el ciclo es
imposible por construcción. No es una optimización: es una convención de código
que hay que escribir y respetar.

---

## 5. Los tres experimentos de caos

Cada uno con la hipótesis **escrita antes** de ejecutar. Un experimento sin
hipótesis previa no es un experimento: es mirar qué pasa.

### Experimento 1 — Matar RabbitMQ

*Hipótesis: el Outbox retiene los eventos; nada se pierde.*

| Momento | Observación |
|---|---|
| Broker detenido | 5 de 5 transferencias devuelven **HTTP 200** |
| Con el broker caído | **5 filas** esperando en `OutboxMessage` |
| Broker reiniciado | `OutboxMessage` **se vació solo hasta 0** |

**Hipótesis confirmada.** La mensajería se cayó y el usuario nunca se enteró.
Los eventos quedaron en la misma transacción que el cambio de saldo, y salieron
solos al volver el broker. Nadie tocó nada. **Esto es lo que compra el patrón
Outbox, y se acaba de ver funcionando.**

### Experimento 2 — Matar PostgreSQL

*Hipótesis: la app falla rápido y claro, no se cuelga.*

| Intento | Resultado |
|---|---|
| 1 | HTTP 500 en 4 300 ms |
| 2 | HTTP 500 en 4 314 ms |
| 3 | HTTP 500 en 4 315 ms |

**Hipótesis confirmada, con un reparo.** No se colgó, no agotó el timeout de
90 s, y falló de forma idéntica y predecible las tres veces. Pero **4.3 segundos
es lento para un fallo**: sin base de datos no hay nada que intentar, y el
usuario espera cuatro segundos para recibir un error inevitable. Con el SLA en
20 s no duele; bajo un presupuesto más estricto, ese `Timeout=30` de la cadena
de conexión sería lo primero a revisar. *Queda anotado como deuda, no como
defecto.*

### Experimento 3 — Pausar RabbitMQ 10 s en medio de una saga viva

*Hipótesis: los timeouts de la saga siguen siendo correctos.*

Cronología real:

```
t=0s    Transferencia d2cffd73 iniciada (100 UVB retenidos)
t=2s    RabbitMQ PAUSADO
t=12s   RabbitMQ REANUDADO
t=12s   Confirmación de abono -> HTTP 202
t=32s   Saga finalizada (0 filas) · Disponible=4900 · Retenido=100
```

**Hipótesis confirmada.** La saga completó el **camino feliz**: no hubo
compensación. El rastro del log lo prueba — `FondosRetenidos` → `Procesando` →
`Abono confirmado`, y ni un solo `CompensarTransferencia` para esa transferencia.

Esto no era obvio. El timeout de la saga es de 15 s y el broker estuvo caído 10 de
esos 15. Funcionó porque **el reloj de la saga es lógico, no de pared**: el
`Schedule` de MassTransit se cuenta desde que la saga *procesa* el evento, y la
pausa retrasó por igual al mensaje y a su temporizador. Un timeout atado al reloj
del sistema habría compensado una transferencia perfectamente válida.

> El saldo final `Disponible=4900 · Retenido=100` **es el resultado correcto del
> camino feliz**: los 100 UVB salieron de la cuenta origen y viajan hacia el banco
> destino. Solo la compensación los devuelve a `Disponible`.

---

## 6. Un fallo real que apareció sin buscarlo

Durante la corrida aparecieron excepciones que **no formaban parte de ningún
experimento**:

```
System.InvalidOperationException: No se puede liberar más de lo retenido.
   at Breb.Cuentas.Dominio.Cuenta.LiberarRetencion(Decimal monto)
```

**Causa:** se reinició `SaldoRetenido = 0` mientras **98 sagas seguían en vuelo**.
Al vencer sus timeouts, esas sagas intentaron devolver dinero que ya no existía.

Fue un error del banco de pruebas, no del sistema. Pero deja dos lecciones que no
estaban en el plan de clase:

1. **El invariante de dominio hizo su trabajo.** `Cuenta.LiberarRetencion` se negó
   a devolver más de lo retenido, y MassTransit reintentó y luego marcó
   `R-FAULT`. El dinero imposible **nunca se creó**. Un modelo anémico —con
   `SaldoRetenido` como propiedad pública— habría dejado un saldo negativo sin
   avisar a nadie.
2. **No se toca el estado de un sistema que está corriendo.** Ni siquiera en
   laboratorio. Especialmente en un sistema asíncrono, donde "ya terminó" y
   "no hay filas en la tabla" no significan lo mismo.

---

## 7. Tabla de línea base

| Métrica | Medido | Meta | Estado |
|---|---|---|---|
| Latencia p50 | 71 ms | < 2 s | Cumple |
| Latencia p95 | 992 ms | < 10 s | Cumple |
| Latencia p99 | 2 086 ms | — | Registrado |
| Latencia MÁXIMA | 2 147 ms | < 20 s (SLA) | Cumple, 9× de margen |
| Errores / 200 | 0 | 0 | Cumple |
| Deadlocks en producción | 0 | 0 | Cumple (el provocado fue deliberado) |
| Throughput | 5.2 transf/s | — | **Techo identificado** |

## 8. Lo que esta línea base deja abierto

**5.2 transferencias por segundo.** La Red Bre-B real mueve miles por segundo en
quincena. Estamos tres órdenes de magnitud por debajo — y ahora sabemos
exactamente por qué: **una fila, un candado, 248× de degradación**.

Ese número no es un fracaso. Es el punto de partida contra el que se medirá todo
lo que sigue: repartir las cuentas, competir por los mensajes, escalar en
horizontal.

> Sin línea base no hay optimización posible: solo opiniones.
> Ahora tenemos línea base.
