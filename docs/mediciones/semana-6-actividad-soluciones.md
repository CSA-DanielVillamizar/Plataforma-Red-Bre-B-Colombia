# Clase 6 — Actividad por squads: guía de respuestas
## Para el facilitador · Red Bre-B Colombia (190304014-1)

**Formato:** 12 minutos de trabajo, 1 minuto por squad para exponer.

Todas las consultas de este documento fueron **ejecutadas y verificadas** el 8 de septiembre de 2026 contra la base real. Los números son medidos.

> **Cómo usar esta guía.** No es un solucionario para repartir. Es para que usted pueda evaluar en el momento, saber qué respuesta es buena y cuál se quedó corta, y tener a mano el dato que cierra la discusión. Cada encargo trae **la respuesta esperada**, **el error típico** y **la pregunta de profundización** si el squad va bien.

---

## Squad 1 — Datos, Tuning y Observabilidad

> **Encargo:** Escriban la consulta que detecta retenciones huérfanas — vivas hace más de una hora. ¿Qué índice necesita? ¿Por qué el índice parcial `WHERE "Liberada" = false` y no uno normal?

### Respuesta esperada

```sql
SELECT r."TransferenciaId",
       r."CuentaId",
       r."MontoUVB",
       age(now() AT TIME ZONE 'UTC', r."CreadaEn") AS antiguedad
FROM "Retenciones" r
LEFT JOIN "TransferenciaSagas" s ON s."CorrelationId" = r."TransferenciaId"
WHERE NOT r."Liberada"
  AND s."CorrelationId" IS NULL
  AND r."CreadaEn" < (now() AT TIME ZONE 'UTC') - interval '1 hour';
```

**Las tres condiciones, y por qué las tres hacen falta:**

| Condición | Qué descarta |
|---|---|
| `NOT r."Liberada"` | Las que ya se devolvieron. Son la inmensa mayoría |
| `s."CorrelationId" IS NULL` | Las que **todavía tienen saga viva** — esas no son huérfanas, están en curso |
| `CreadaEn < ahora − 1 hora` | Las recientes. Una retención de hace 30 segundos es normal, no un problema |

> **El `LEFT JOIN` es el corazón.** Sin él, la consulta reporta como huérfana toda transferencia en vuelo, y la alerta se vuelve ruido que nadie mira.

### El índice: por qué parcial

**Medido con 80 060 filas, de las cuales solo 160 vivas:**

| Índice | Tamaño | Tiempo de la consulta |
|---|---|---|
| **Parcial** `(CuentaId, Liberada) WHERE "Liberada" = false` | **16 kB** | 1.490 ms |
| Normal `(CuentaId, Liberada)` | 568 kB | 1.463 ms |

**Treinta y cinco veces más pequeño, con el mismo rendimiento.**

La razón es directa: el índice parcial solo guarda las **160 filas vivas**; el normal guarda las **80 060**. Y como el 99.8 % de las retenciones terminan liberadas para siempre, indexarlas es pagar por filas que nadie va a consultar nunca más.

**Lo que se ahorra no es solo disco:**
- Menos páginas que leer → cabe entero en memoria
- Menos que actualizar en cada `INSERT`
- Cuando una retención se libera, **sale del índice** — el índice no crece con el histórico

### El error típico

Proponer el índice sobre `"Liberada"` sola. No sirve: es una columna booleana con dos valores, y el 99.8 % son `true`. Un índice así tiene **selectividad pésima** y el planificador lo va a ignorar.

### Si el squad va bien, profundice

> *"Corran `EXPLAIN` de su consulta con la tabla como está hoy, con sesenta filas. ¿Usa el índice?"*

**No lo usa** — hace `Seq Scan`, y **está bien**. Con 60 filas, recorrerlas es más barato que abrir un índice. Es la misma lección de la Semana 4: *`Seq Scan` no es un defecto; en tablas pequeñas es la decisión correcta.*

El índice parcial se justifica por lo que la tabla **va a ser**, no por lo que es hoy.

---

## Squad 2 — Fintech & Core

> **Encargo:** `SaldoRetenido` ahora es un total derivado. Escriban la consulta que verifica que cuadra con la suma de retenciones vivas. ¿Cada cuánto la correrían en producción?

### Respuesta esperada

```sql
SELECT c."Id",
       c."SaldoRetenido"                     AS total_en_cuenta,
       COALESCE(SUM(r."MontoUVB"), 0)        AS suma_retenciones_vivas,
       c."SaldoRetenido" - COALESCE(SUM(r."MontoUVB"), 0) AS diferencia
FROM "Cuentas" c
LEFT JOIN "Retenciones" r
       ON r."CuentaId" = c."Id" AND NOT r."Liberada"
GROUP BY c."Id", c."SaldoRetenido"
HAVING c."SaldoRetenido" <> COALESCE(SUM(r."MontoUVB"), 0);
```

**Debe devolver cero filas.** Verificado tras 60 transferencias concurrentes: **0 filas**.

**Los dos detalles que separan una buena respuesta de una a medias:**

| Detalle | Por qué |
|---|---|
| `LEFT JOIN`, no `JOIN` | Una cuenta sin retenciones vivas debe aparecer con 0, no desaparecer del resultado |
| `COALESCE(SUM(...), 0)` | Sin esto, `SUM` de cero filas da `NULL`, y `X <> NULL` es `NULL` — la fila **no** se reporta. **La consulta parecería pasar siempre** |

> Ese segundo punto es una trampa real de SQL. Un squad que lo note entendió más que uno que solo escribió el `JOIN`.

### ¿Cada cuánto correrla?

No hay una sola respuesta correcta; lo que se evalúa es el **criterio**. Una buena respuesta distingue frecuencias por propósito:

| Cuándo | Alcance | Para qué |
|---|---|---|
| **Continuo** (cada 1-5 min) | Solo cuentas tocadas en la última hora | Detectar el problema mientras aún es reciente |
| **Diario**, en el cierre | Todas las cuentas | El cierre contable. En banca **no es opcional** |
| **Bajo demanda** | Una cuenta | Cuando un cliente reclama |

**La advertencia que hay que exigirles:** si la corren con transferencias en vuelo, va a reportar diferencias **transitorias que no son errores**. Una retención recién creada existe en `Retenciones` en el mismo instante en que `SaldoRetenido` cambia, pero entre dos consultas separadas puede verse el estado intermedio.

Se resuelve corriéndola en una transacción con instantánea consistente:

```sql
BEGIN ISOLATION LEVEL REPEATABLE READ;
-- … la consulta …
COMMIT;
```

### El error típico

Decir *"la corro cada minuto sobre toda la tabla"*. Con millones de cuentas, eso es un `full scan` cada minuto que compite con el tráfico real. La respuesta madura **acota el alcance** por frecuencia.

### Si el squad va bien, profundice

> *"Si la consulta reporta una diferencia, ¿cuál de los dos números creen: el total o el detalle?"*

**El detalle.** `Retenciones` tiene una fila por hecho ocurrido, con su marca de tiempo; `SaldoRetenido` es un acumulado que se puede desincronizar. El detalle es la fuente de verdad, y el total es una conveniencia — por eso se puede **reconstruir** el total desde el detalle, pero no al revés.

---

## Squad 3 — DevOps, Infra y Cloud

> **Encargo:** La tabla `Retenciones` crece con cada transferencia y nunca se borra. Diseñen la política de retención de datos: ¿qué se archiva, cuándo, y qué NO se puede borrar nunca?

### Respuesta esperada

**El dimensionamiento primero.** Sin un número, la política es opinión:

| | |
|---|---|
| Fila de `Retenciones` | ~60 bytes + índices |
| Si la Red Bre-B mueve 1 000 transf/s | 86.4 millones de filas/día |
| Al año | ~31 500 millones de filas · **varios TB** |

**La política, en tres franjas:**

| Antigüedad | Dónde vive | Por qué |
|---|---|---|
| **0 - 90 días** | Tabla caliente, en la base operativa | Reclamos de clientes, conciliación diaria, soporte |
| **90 días - 5 años** | Archivo frío (partición separada, S3, data warehouse) | Requisito **legal**: en Colombia los soportes de operaciones financieras se conservan **5 años** |
| **> 5 años** | Se puede purgar | Ya cumplió el requisito |

**Lo que NO se puede borrar nunca, pase el tiempo que pase:**

1. **Retenciones vivas** (`Liberada = false`) — sin importar la antigüedad. Una retención viva de hace dos años es **dinero de alguien** que sigue atrapado. Borrarla no arregla el problema: lo esconde.
2. **Retenciones con reclamo o litigio abierto** — la retención de datos se congela mientras haya un proceso en curso.
3. **Cualquier retención cuya saga no haya terminado.**

> **La regla que hay que exigirles:** *nunca se archiva por antigüedad sola.* Siempre `antigüedad AND Liberada = true AND sin_reclamo_abierto`.

### La implementación que conviene mencionar

**Particionamiento por rango de fecha** (`PARTITION BY RANGE ("CreadaEn")`), con una partición por mes. Archivar deja de ser un `DELETE` masivo —que bloquea, infla el WAL y deja la tabla llena de huecos— y pasa a ser:

```sql
ALTER TABLE "Retenciones" DETACH PARTITION "Retenciones_2026_03";
```

Instantáneo, sin bloqueos, y la partición queda disponible para moverla al archivo.

### El error típico

Proponer `DELETE FROM "Retenciones" WHERE "CreadaEn" < now() - interval '90 days'`. Tres problemas: borra retenciones **vivas**, viola el requisito legal de 5 años, y un `DELETE` masivo sobre una tabla caliente es una operación peligrosa en producción.

### Si el squad va bien, profundice

> *"Archivaron una retención liberada de hace un año. Llega un cliente reclamando esa transferencia. ¿Qué le responden?"*

Que el dato **existe pero no está en línea**. Por eso el archivo debe ser **consultable**, no un `.tar.gz` en un disco. Y por eso hay que definir el **tiempo de recuperación**: ¿minutos, horas, días? Esa cifra es parte de la política, no un detalle operativo.

---

## Squad 4 — Arquitectura, UI y QA

> **Encargo:** Escriban la prueba automatizada que habría detectado el defecto de la Semana 5: compensar dos veces la misma transferencia y verificar que la plata de otra no se toca.

### Respuesta esperada

```csharp
[Fact]
public async Task CompensarDosVeces_NoDebeTocarLaRetencionDeOtraTransferencia()
{
    // ── Preparar: DOS transferencias sobre la MISMA cuenta ──
    // Con una sola no se detecta nada: el defecto era que la segunda
    // compensación de A se comía la retención de B.
    var cuenta = new Cuenta(Guid.NewGuid(), saldoInicial: 1000);
    var retA = cuenta.Retener(transferenciaA, 100);
    var retB = cuenta.Retener(transferenciaB, 100);

    Assert.Equal(800, cuenta.SaldoDisponible);
    Assert.Equal(200, cuenta.SaldoRetenido);

    // ── Actuar: compensar A DOS VECES ──
    var primera = cuenta.LiberarRetencion(retA);
    var segunda = cuenta.LiberarRetencion(retA);   // entrega duplicada

    // ── Verificar ──
    Assert.True(primera);                 // la primera sí libera
    Assert.False(segunda);                // la segunda es un no-op, no un error
    Assert.False(retB.Liberada);          // ← LA ASERCIÓN QUE IMPORTA
    Assert.Equal(100, cuenta.SaldoRetenido);   // solo queda la de B
    Assert.Equal(900, cuenta.SaldoDisponible);
}
```

**Las tres decisiones de diseño que hacen que la prueba sirva:**

| Decisión | Por qué sin ella la prueba no detecta nada |
|---|---|
| **Dos** transferencias, no una | Con una sola, la doble compensación no tiene de quién robar. El defecto era invisible |
| Asertar sobre **`retB`** | Es la víctima. Asertar solo el saldo total deja pasar el caso donde los números cuadran pero B quedó liberada |
| Esperar `false`, no una excepción | Bajo entrega al-menos-una-vez, la segunda entrega es **normal**. Que lance excepción sería el diseño equivocado |

### Por qué esta prueba habría fallado en la Semana 5

Con el modelo viejo, `LiberarRetencion(decimal monto)` solo validaba `monto > SaldoRetenido`:

```
Retenido = 200
Libera A (100) → Retenido = 100   ✓
Libera A otra vez (100) → 100 <= 100, pasa la validación → Retenido = 0   ✗
```

El invariante **no se quejaba** porque el total alcanzaba. La retención de B desapareció sin que nada lo notara. **La prueba de arriba lo habría cazado en la línea de `retB`.**

### El error típico

Escribir la prueba con **una sola** transferencia y asertar únicamente el saldo total. Eso pasa con el modelo viejo y con el nuevo: no distingue nada.

### Si el squad va bien, profundice

> *"Esa es una prueba unitaria, sin base de datos ni mensajería. ¿Qué NO detecta?"*

No detecta los defectos de **concurrencia** — que fueron la mitad de lo que encontramos. Para eso hace falta una prueba de integración que dispare N compensaciones en paralelo contra la base real.

**Y ahí está el criterio que se llevan:** la prueba unitaria protege el **invariante del dominio**; la de integración protege el **comportamiento bajo concurrencia**. Son capas distintas y ninguna sustituye a la otra.

---

## Cierre de la actividad

**Qué decir:**

> "El encargo de Fintech es más profundo de lo que parece. Ahora tenemos la misma información en dos lugares: el total en `Cuenta` y el detalle en `Retenciones`. Eso es **duplicación deliberada por rendimiento**, y toda duplicación deliberada necesita una forma de comprobar que no se desincronizó.
>
> Y fíjense en algo que atraviesa los cuatro encargos: los cuatro solo se pueden responder **porque la retención existe como entidad**. Con un solo número no se puede escribir ninguna de esas cuatro consultas.
>
> Detectar huérfanas, cuadrar el total contra el detalle, decidir qué archivar, probar que no se toca la plata ajena. **Cuatro problemas de producción distintos, y el mismo cambio de modelo los habilita a todos.**"

### Rúbrica rápida para calificar en el momento

| Nivel | Qué se ve |
|---|---|
| **Excelente** | La consulta funciona **y** el squad explica por qué cada cláusula hace falta |
| **Bueno** | La consulta funciona pero no saben justificar alguna condición |
| **Insuficiente** | La consulta correría pero no detecta lo que debía — el error típico de su encargo |

---

*Guía de respuestas de la actividad de la Clase 6. Todas las consultas fueron ejecutadas contra la base real el 8 de septiembre de 2026.*
