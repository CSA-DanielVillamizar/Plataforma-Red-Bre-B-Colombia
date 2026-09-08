# Clase 6 — Instructivo Técnico
## Modelado de dominio bajo concurrencia: la retención como entidad
### Red Bre-B Colombia · 190304014-1 · Lunes 7 de septiembre de 2026

Este documento es el **cómo**. El **qué decir** está en `Clase6_Guion_Consolidado.md`.

Todo lo que aparece aquí fue **ejecutado y verificado** el 3 de septiembre de 2026 en el laboratorio real. Los números son medidos, no estimados.

---

## 0. Qué cambia respecto a la Semana 5

| | Semana 5 | Semana 6 |
|---|---|---|
| Retención | un número: `SaldoRetenido` | **entidad `Retencion`** con identidad propia |
| `Retener` | `Retener(decimal monto)` | `Retener(Guid transferenciaId, decimal monto)` → devuelve `Retencion` |
| `LiberarRetencion` | `(decimal monto)` | **`(Retencion retencion)`** → devuelve `bool` |
| Idempotencia de compensación | guarda contra `MensajesProcesados` | **en el modelo**: `Retencion.Liberar()` |
| Tablas | 4 | **5** (nueva: `Retenciones`) |
| Migraciones | — | **`RetencionComoEntidad`** |

**No se toca:** el pool de conexiones, los reintentos exponenciales, el bloqueo pesimista ni la máquina de estados. Todo eso quedó bien en la Semana 5.

---

## 1. Preparación del entorno (30 min antes)

```bash
cd bre-b-lab
docker compose up -d
```

Ambos contenedores en `(healthy)`. Esta clase necesita **una sola instancia**, no tres.

---

## 2. El cambio, paso a paso

### 2.1 La entidad nueva — `Dominio/Retencion.cs`

```csharp
public class Retencion
{
    // La clave primaria ES el id de la transferencia. No es un atajo: es la
    // identidad natural. Con esto, "una transferencia retiene exactamente una
    // vez" deja de ser un chequeo que alguien debe recordar escribir, y pasa a
    // ser una restricción que la base de datos hace cumplir siempre.
    public Guid TransferenciaId { get; private set; }

    public Guid CuentaId { get; private set; }
    public decimal MontoUVB { get; private set; }
    public DateTime CreadaEn { get; private set; }
    public bool Liberada { get; private set; }
    public DateTime? LiberadaEn { get; private set; }

    private Retencion() { }   // EF Core lo necesita

    public Retencion(Guid transferenciaId, Guid cuentaId, decimal montoUVB)
    {
        if (montoUVB <= 0)
            throw new InvalidOperationException("El monto debe ser positivo.");

        TransferenciaId = transferenciaId;
        CuentaId = cuentaId;
        MontoUVB = montoUVB;
        CreadaEn = DateTime.UtcNow;
        Liberada = false;
    }

    // Devuelve false si ya estaba liberada. No es un error: bajo entrega
    // al-menos-una-vez, que la compensación llegue dos veces es lo NORMAL.
    public bool Liberar()
    {
        if (Liberada) return false;
        Liberada = true;
        LiberadaEn = DateTime.UtcNow;
        return true;
    }
}
```

### 2.2 `Cuenta.cs` — las dos firmas que cambian

```csharp
public Retencion Retener(Guid transferenciaId, decimal monto)
{
    if (monto <= 0)
        throw new InvalidOperationException("El monto debe ser positivo.");
    if (monto > SaldoDisponible)
        throw new InvalidOperationException("Saldo insuficiente para retener.");

    SaldoDisponible -= monto;
    SaldoRetenido += monto;

    return new Retencion(transferenciaId, Id, monto);
}

public bool LiberarRetencion(Retencion retencion)
{
    if (retencion.CuentaId != Id)
        throw new InvalidOperationException("Esa retención no pertenece a esta cuenta.");

    if (!retencion.Liberar()) return false;   // ya estaba liberada: no-op

    SaldoRetenido -= retencion.MontoUVB;
    SaldoDisponible += retencion.MontoUVB;
    return true;
}
```

> **Lo importante es lo que NO está.** `LiberarRetencion` ya no recibe un monto. No hay con qué equivocarse: el monto lo sabe la retención. El error dejó de ser algo que hay que validar y pasó a ser algo que no se puede escribir.

`SaldoRetenido` sigue existiendo, pero cambió de naturaleza: ya no es *la verdad* sobre lo retenido, es un **total derivado** que se mantiene por conveniencia. La verdad, retención por retención, vive en la tabla nueva.

### 2.3 `CuentasDbContext.cs`

```csharp
public DbSet<Retencion> Retenciones => Set<Retencion>();
```

```csharp
modelBuilder.Entity<Retencion>(e =>
{
    e.HasKey(r => r.TransferenciaId);
    e.Property(r => r.MontoUVB).HasPrecision(18, 2);

    // Índice PARCIAL: solo indexa las retenciones vivas, que son las únicas
    // por las que se pregunta. Más pequeño y más rápido que uno normal.
    e.HasIndex(r => new { r.CuentaId, r.Liberada })
     .HasFilter("\"Liberada\" = false");

    e.HasOne<Cuenta>()
     .WithMany()
     .HasForeignKey(r => r.CuentaId)
     .OnDelete(DeleteBehavior.Restrict);
});
```

### 2.4 La migración

```bash
cd bre-b-lab/Breb.Platform/Breb.Cuentas
dotnet ef migrations add RetencionComoEntidad
```

```bash
dotnet ef database update
```

Verifique que la tabla existe:

```bash
docker exec breb-postgres psql -U postgres -d brebcuentas -c "\d \"Retenciones\""
```

### 2.5 El endpoint `/retener`

Dentro de la transacción que ya existe (el `FOR UPDATE` de la Semana 5), solo cambian dos líneas:

```csharp
var retencion = cuenta.Retener(transferenciaId, montoUVB);
db.Retenciones.Add(retencion);
```

### 2.6 `CompensarTransferenciaConsumer` — lo que se borra

```csharp
// Búsqueda por clave primaria: una sola fila, sin recorrer nada.
var retencion = await _db.Retenciones
    .FirstOrDefaultAsync(r => r.TransferenciaId == msg.TransferenciaId);

if (retencion is null)
{
    _logger.LogError("No existe retención para {Id}. Nada que compensar.", msg.TransferenciaId);
    return;
}

var cuenta = await _db.Cuentas.FirstOrDefaultAsync(c => c.Id == retencion.CuentaId);
if (cuenta is null) { /* ... */ return; }

var seLibero = cuenta.LiberarRetencion(retencion);   // toda la lógica, en el dominio

if (!seLibero)
    _logger.LogWarning("Retención {Id} ya estaba liberada; se reenvía el evento.",
                       msg.TransferenciaId);

// Se publica en AMBOS casos: si la entrega anterior murió después de liberar
// pero antes de publicar, la saga quedaría esperando para siempre.
await _publishEndpoint.Publish(new FondosReintegrados { ... });
await _db.SaveChangesAsync();
```

**Lo que desapareció del archivo:**

| Semana 5 | Semana 6 |
|---|---|
| Guarda de idempotencia contra `MensajesProcesados` | borrada |
| `cuenta.LiberarRetencion(msg.MontoUVB)` | ya no se pasa el monto |
| 12 líneas de comentario explicando por qué la guarda existe | borradas |
| Rama duplicada que republicaba el evento a mano | simplificada |

**Contar líneas no captura el cambio, y conviene decirlo con el número exacto:**

| | Semana 5 | Semana 6 |
|---|---|---|
| Líneas de código *(sin comentarios ni vacías)* | 58 | **55** |
| Diff crudo | — | 33 insertadas, 45 borradas |

**Tres líneas menos.** El archivo apenas se acortó, porque entró código nuevo: la búsqueda de la retención, su chequeo de nulo, y la variable `seLibero`.

Lo que desapareció no son líneas: es un **mecanismo completo**.

| Desapareció | Qué era |
|---|---|
| `claveIdempotencia` + consulta a `MensajesProcesados` | Una tabla, una consulta y una rama condicional |
| La rama `if (yaCompensada)` con su `Publish` + `SaveChanges` duplicados | Código repetido para el caso duplicado |
| `using Breb.Cuentas.Dominio` | El consumidor ya ni conoce `MensajeProcesado` |

> **La idempotencia dejó de ser un mecanismo y pasó a ser una propiedad del modelo.** El archivo mide casi lo mismo y hace **una cosa menos**. Ese es el cambio, no el conteo de líneas.

---

## 3. Verificación

### 3.1 La demo de clase

```bash
python carga-clase5.py "demo semana 6" 5080 1 60 6
```

Espere ~40 segundos (el timeout de la saga son 15 s) y consulte el rastro:

```bash
docker exec breb-postgres psql -U postgres -d brebcuentas -c "SELECT \"TransferenciaId\", \"MontoUVB\", \"Liberada\", \"CreadaEn\"::time(0), \"LiberadaEn\"::time(0) FROM \"Retenciones\" ORDER BY \"CreadaEn\" LIMIT 5;"
```

**Salida real medida:**

```
           TransferenciaId            | MontoUVB | Liberada | CreadaEn | LiberadaEn
--------------------------------------+----------+----------+----------+------------
 800dfb77-3b82-466b-ae65-2c92af088497 |     1.00 | t        | 04:13:03 | 04:13:26
 6b351aed-f604-4124-a3d2-bb8031302f7d |     1.00 | t        | 04:13:03 | 04:13:27
 b1fb816d-cf1a-4fb7-8653-20ba5c2d8586 |     1.00 | t        | 04:13:03 | 04:13:22
 9157f7d5-bdc5-4a10-80c5-a2e725c82214 |     1.00 | t        | 04:13:03 | 04:13:36
```

Ese `LiberadaEn` es la columna que justifica todo el cambio.

### 3.2 Integridad tras 300 transferencias

```bash
docker exec breb-postgres psql -U postgres -d brebcuentas -c "SELECT (SELECT COUNT(*) FROM \"TransferenciaSagas\") AS sagas, (SELECT COUNT(*) FROM \"Retenciones\") AS retenciones, (SELECT COUNT(*) FROM \"Retenciones\" WHERE NOT \"Liberada\") AS vivas, (SELECT SUM(\"SaldoRetenido\") FROM \"Cuentas\") AS retenido;"
```

**Resultado medido:**

| sagas atascadas | retenciones | vivas | retenido | cuentas descuadradas |
|---|---|---|---|---|
| **0** | **300** | **0** | **0.00** | **0** |

Trescientas transferencias, trescientas retenciones — ni una más, ni una menos. Cero violaciones de invariante, cero `R-FAULT`.

### 3.3 La consulta que verifica el dato derivado

`SaldoRetenido` ahora está duplicado: el total en `Cuenta`, el detalle en `Retenciones`. Toda duplicación deliberada necesita forma de comprobar que no se desincronizó:

```bash
docker exec breb-postgres psql -U postgres -d brebcuentas -c "SELECT c.\"Id\", c.\"SaldoRetenido\" AS total_en_cuenta, COALESCE(SUM(r.\"MontoUVB\"),0) AS suma_retenciones_vivas, c.\"SaldoRetenido\" - COALESCE(SUM(r.\"MontoUVB\"),0) AS diferencia FROM \"Cuentas\" c LEFT JOIN \"Retenciones\" r ON r.\"CuentaId\" = c.\"Id\" AND NOT r.\"Liberada\" GROUP BY c.\"Id\", c.\"SaldoRetenido\" HAVING c.\"SaldoRetenido\" <> COALESCE(SUM(r.\"MontoUVB\"),0);"
```

**Debe devolver cero filas.** Si devuelve algo, el total y el detalle se desincronizaron.

> Ojo al correrla: si hay transferencias en vuelo, va a mostrar diferencias transitorias que no son errores. Espere a que `TransferenciaSagas` esté vacía.

### 3.4 Retenciones huérfanas — el hallazgo que justifica todo el cambio

En la corrida limpia de 300 transferencias, **2 quedaron sin compensar**. No es un defecto del modelo nuevo: son mensajes que agotaron los 10 reintentos con `40001` **al crear la saga**. La saga nunca existió, así que nadie iba a disparar su compensación.

```
R-FAULT ×2  en  rabbitmq://localhost/TransferenciaSagaState
40001: could not serialize access due to read/write dependencies
```

**Esto es normal y hay que decirlo en clase:** los reintentos nunca alcanzan del todo. Con suficiente concurrencia, algún mensaje siempre agota su presupuesto y cae en la cola de error. Un sistema de producción **no se apoya solo en reintentos** — necesita un proceso de reconciliación que encuentre a los huérfanos y los arregle.

Y aquí está el punto: **reconciliar exige un modelo reconciliable.**

```bash
docker exec breb-postgres psql -U postgres -d brebcuentas -c "SELECT r.\"TransferenciaId\", r.\"CuentaId\", r.\"MontoUVB\", r.\"CreadaEn\"::time(0) AS creada, age(now() AT TIME ZONE 'UTC', r.\"CreadaEn\")::interval(0) AS antiguedad FROM \"Retenciones\" r LEFT JOIN \"TransferenciaSagas\" s ON s.\"CorrelationId\" = r.\"TransferenciaId\" WHERE NOT r.\"Liberada\" AND s.\"CorrelationId\" IS NULL AND r.\"CreadaEn\" < (now() AT TIME ZONE 'UTC') - interval '1 minute';"
```

**Salida real medida:**

```
           TransferenciaId            |               CuentaId               | MontoUVB |  creada  | antiguedad
--------------------------------------+--------------------------------------+----------+----------+------------
 31aed149-6dca-4df0-a849-95ef4974bfc7 | aaaaaaaa-0000-0000-0000-000000000008 |     1.00 | 04:29:01 | 00:06:10
 06cb28de-195c-47d5-813f-485e839a4743 | aaaaaaaa-0000-0000-0000-000000000007 |     1.00 | 04:29:05 | 00:06:06
```

Dos filas: **qué** transferencia, **de qué** cuenta, **cuánto**, y **hace cuánto** está atascada. Con eso se puede alertar, auditar y reparar.

> **Con el modelo de la Semana 5 esta consulta no se puede escribir.** No hay fila con la cual hacer el `JOIN`. Lo único disponible sería `SaldoRetenido = 2` — un número que no dice de quién es, ni desde cuándo, ni a qué cuenta devolvérselo.
>
> El modelo nuevo no elimina los fallos residuales. Los vuelve **visibles y reparables**, que es lo máximo que un sistema distribuido puede prometer.

**Tasa medida: 2 de 300 = 0.67 %.** Vale la pena que los estudiantes discutan si ese número es aceptable para una plataforma de pagos, y qué haría un banco al respecto.

---

## 4. El costo, medido

300 transferencias por corrida, concurrencia 40, 0 errores en todas.

| | Semana 5 (un número) | Semana 6 (entidad) | Cambio |
|---|---|---|---|
| **1 cuenta** (contención alta) | 34.06 t/s | **~19 t/s** *(14.45 · 17.86 · 20.76 · 25.57)* | **–44 %** |
| **20 cuentas** (repartido) | 66.90 t/s | **~75 t/s** *(68.50 · 74.96 · 101.83)* | sin costo medible |

**Por qué cuesta donde cuesta:** el `INSERT` en `Retenciones` ocurre **dentro** de la transacción que ya tiene bloqueada la fila de la cuenta. Eso alarga la sección crítica. Si nadie hace fila por ese candado, el trabajo extra es invisible; si hay fila, cada milisegundo extra lo pagan todos los que esperan.

> **Sobre los rangos:** las mediciones individuales van de 14.45 a 25.57 t/s en el caso contendido. La dispersión es real y viene de la contención misma. Dar un solo número con dos decimales sería precisión falsa — por eso se reporta la mediana y el rango.

**La conclusión que une la unidad:** el costo de la corrección se concentra exactamente donde hay contención. Es el tercer argumento independiente a favor de repartir los datos.

---

## 5. Las cinco preguntas del negocio

La prueba de si un modelo sirve no es si compila, sino si responde lo que el negocio pregunta:

| Pregunta | Modelo viejo | Modelo nuevo |
|---|---|---|
| ¿Cuánto tiene retenido esta cuenta? | Sí | Sí |
| ¿Sigue viva la retención de la transferencia X? | **No** | Sí |
| ¿Cuándo se le devolvió la plata al cliente? | **No** | Sí |
| ¿Qué retenciones llevan más de una hora vivas? | **No** | Sí |
| ¿Se compensó dos veces la misma transferencia? | **No** | Sí, y es inofensivo |

---

## 6. Diagnóstico rápido

| Síntoma | Causa probable | Qué hacer |
|---|---|---|
| `relation "Retenciones" does not exist` | Falta aplicar la migración | `dotnet ef database update` |
| `No existe retención para {id}` | La retención se borró o nunca se creó | Verifique que `/retener` haga el `Add` dentro de la transacción |
| `Esa retención no pertenece a esta cuenta` | El mensaje trae otra `CuentaId` | Revise el contrato `CompensarTransferencia` |
| La consulta de cuadre da diferencias | Hay transferencias en vuelo | Espere a que `TransferenciaSagas` esté vacía y repita |
| Throughput muy por debajo de lo esperado | Toda la carga sobre una cuenta | Es lo esperado: mida también con 20 cuentas |
| `duplicate key value violates unique constraint` en `Retenciones` | Dos retenciones con el mismo `TransferenciaId` | **Es la restricción funcionando.** Investigue quién publicó dos veces |

---

## 7. Entregables de la semana

- [ ] `Dominio/Retencion.cs` con la entidad y su método `Liberar()`.
- [ ] `Cuenta.cs` con las dos firmas nuevas.
- [ ] Migración `RetencionComoEntidad` aplicada y versionada.
- [ ] `CompensarTransferenciaConsumer` simplificado, con la guarda vieja borrada.
- [ ] La consulta de cuadre entre `SaldoRetenido` y la suma de retenciones vivas.
- [ ] La tabla de las cinco preguntas del negocio, publicada en el Issue de Semana 6.
- [ ] El costo medido, con rango y no con un solo número.

---

## 8. Lo que queda abierto

1. **Particionamiento real** (`UsePartitioner`): que todos los mensajes de una cuenta vayan siempre al mismo consumidor. Conserva el orden por cuenta sin candado global y mantiene el paralelismo entre cuentas.
2. **El agregado.** `Retencion` es tabla aparte y se busca por clave primaria. La alternativa de manual sería cargarla dentro del agregado `Cuenta` — que no funciona con un millón de retenciones históricas. Vale la pena que lo argumenten.
3. **¿Mantener `SaldoRetenido`?** Es un dato derivado. Podría calcularse con un `SUM` cada vez. Midan las dos opciones antes de decidir.

---

*Instructivo técnico de la Clase 6. Todos los números fueron medidos el 3 de septiembre de 2026 en el laboratorio local.*
