# Issue #46 — Retención huérfana: medición del arreglo

**Fecha:** 30 de septiembre de 2026
**Entorno:** 1 instancia en el 5080, PostgreSQL 5433, RabbitMQ 5673, 20 cuentas de prueba

---

## El fallo que se arregla

`FondosRetenidos` es el evento que **crea** la saga. Cuando agota sus reintentos por un `40001`, la saga nunca nace y queda una retención viva que nadie compensa: el dinero se queda retenido para siempre.

| Medición previa | Resultado |
|---|---|
| Semana 6 | 2 de 300 (0.67 %) |
| Semana 9 | 1 de 900 |

---

## Lo que se encontró al implementarlo, y que cambió el arreglo

La solución que proponía el issue —barrer las retenciones con `NOT "Liberada"` y sin saga— **habría causado un daño mucho mayor que el fallo que repara.**

Una transferencia que se completa bien deja exactamente esa misma huella:

- La saga se borra al finalizar (`SetCompletedWhenFinalized`).
- La retención **no se libera**, porque el dinero no volvió: salió hacia el banco destino.

De modo que **una transferencia exitosa era indistinguible de una huérfana**. Un conciliador guiado por esa consulta habría compensado las 900 transferencias de cada corrida, devolviendo dinero que ya estaba acreditado en el destino.

El propio issue lo había anotado sin sacar la conclusión: *"Hoy no se distingue de una transferencia completada… la retención queda `Liberada = false` sin saga, igual que una completada."*

**La raíz era del modelo, no del barrido.** Una retención termina de dos maneras —liberada (devuelta) o liquidada (gastada)— y el modelo solo sabía registrar la primera. Lo que no se puede distinguir de lo correcto, no se puede reparar.

---

## Lo que se construyó

**1. El modelo aprende el segundo final.** `Retencion` gana `Liquidada`/`LiquidadaEn` y el método `Liquidar()`. `Cuenta.LiquidarRetencion()` baja el retenido **sin** devolver al disponible: eso es, exactamente, que el dinero se fue. Liberar y liquidar se excluyen mutuamente, y el invariante lo hace cumplir el dominio.

**2. El camino feliz deja huella.** `TransferenciaCompletadaConsumer` consume un evento que antes nadie escuchaba, y marca la retención como liquidada.

**3. Redelivery diferida** (15 s, 45 s, 90 s) como segundo nivel sobre los diez reintentos exponenciales. Declarada **antes** de `UseMessageRetry` para que la envuelva y solo actúe cuando ese bloque se rindió del todo.

**4. `ConciliadorRetenciones`**, que cada minuto busca retenciones **pendientes** —ni liberadas ni liquidadas— de más de cinco minutos y sin saga, y publica `CompensarTransferencia`. Reusa la ruta del dominio en vez de tener una propia. Un cerrojo consultivo de PostgreSQL evita que las tres instancias barran a la vez.

El umbral de cinco minutos es mayor que la ventana completa de redelivery (~2,5 min) a propósito: por debajo, el conciliador cancelaría transferencias cuyo mensaje todavía venía en camino.

---

## Criterio 1: tres corridas de 300 transferencias

`python carga-clase5.py "corrida N" 5080 20 300 40`

| Corrida | Duración | Throughput | p50 | p95 | p99 | máx | ok | err |
|---|---|---|---|---|---|---|---|---|
| 1 | 4.5 s | 67.29 t/s | 398 | 1754 | 1876 | 1963 | 300 | 0 |
| 2 | 1.8 s | 164.01 t/s | 215 | 299 | 348 | 362 | 300 | 0 |
| 3 | 1.8 s | 166.74 t/s | 227 | 333 | 368 | 409 | 300 | 0 |

**Estado tras drenar las 900:**

| Métrica | Resultado |
|---|---|
| Retenciones pendientes | **0** |
| Sagas en vuelo | **0** |
| **R-FAULT** | **0** |
| Errores 40001 en el log | 4748 |
| R-RETRY | 818 |
| Redeliveries diferidas usadas | 0 |
| Conciliaciones necesarias | 0 |

**Cuadre:** `SaldoRetenido` coincide con la suma del detalle pendiente en **todas** las cuentas (la consulta de diferencias devuelve 0 filas).

> **Honestidad sobre lo que esta corrida prueba y lo que no.** Hubo 4748 errores 40001 y los 818 reintentos los absorbieron todos: ni la redelivery ni el conciliador llegaron a dispararse. Así que esta corrida demuestra que **el fallo no se repitió**, no que las dos capas nuevas funcionen. Siendo un fallo de 1 en 900, una sola tanda limpia tampoco probaría gran cosa. Por eso el segundo criterio importa más.

---

## Criterio 2: una huérfana provocada a mano

Se insertó una retención que simula exactamente el fallo —`/retener` escribió la retención y movió el saldo, pero `FondosRetenidos` murió y la saga nunca nació—, con diez minutos de antigüedad:

```sql
INSERT INTO "Retenciones" ("TransferenciaId","CuentaId","MontoUVB","CreadaEn",
                           "Liberada","LiberadaEn","Liquidada","LiquidadaEn")
VALUES ('deadbeef-0000-0000-0000-000000000046',
        'aaaaaaaa-0000-0000-0000-000000000001', 7,
        now() - interval '10 minutes', false, null, false, null);
```

**Sin intervención, 50 segundos después:**

```
[19:27:25] WRN CONCILIACIÓN: 1 retención(es) huérfana(s) sin saga, con más de 00:05:00 de vida.
[19:27:25] INF CONCILIACIÓN: 1 compensación(es) encoladas.
[19:27:25] WRN COMPENSANDO transferencia deadbeef-...-000000000046: Conciliacion: retencion huerfana sin saga (#46)
[19:27:25] INF Reintegrados 7.00 UVB a la cuenta aaaaaaaa-0000-0000-0000-000000000001
```

| Antes | Después |
|---|---|
| Disponible 99 999 993.00 · Retenido 7.00 | Disponible **100 000 000.00** · Retenido **0.00** |

La retención quedó `Liberada = true`. **El dinero volvió solo.**

---

## Criterio 3: el camino feliz sigue intacto, y además se corrigió

`.\demo-clase3.ps1 feliz -Puerto 5080`

```
5. Resultado final:
    Disponible: 4900.00   |   Retenido: 0.00
   OK - La transferencia se completo. El dinero salio como debia.
   OK - NO hubo compensacion.
```

```
[19:26:35] INF Liquidados 100.00 UVB de la cuenta 1111...: el dinero salió.
```

**Antes de este cambio, ese `Retenido` se quedaba en `100.00` para siempre.** El camino feliz no tocaba la cuenta, así que el saldo retenido se inflaba con cada transferencia exitosa y solo se limpiaba reiniciando el laboratorio. Nadie lo notaba porque el laboratorio se reinicia entre demos.

El conciliador **no** tocó esta transferencia, que es justo lo que había que comprobar: ya no confunde una transferencia exitosa con una averiada.

---

## Lo que queda abierto

- **La probabilidad no es cero.** La redelivery baja la frecuencia y la conciliación garantiza que nada quede para siempre, pero una retención huérfana puede seguir apareciendo y vivirá hasta cinco minutos antes de repararse. Eso es una decisión, no un descuido: compensar antes sería arriesgarse a cancelar transferencias vivas.
- **El conciliador no tiene alerta.** Hoy solo escribe en el log. Si empieza a reparar muchas, nadie se entera.
- **Falta probarlo con tres instancias.** El cerrojo consultivo está escrito pero solo se ejercitó con una.
