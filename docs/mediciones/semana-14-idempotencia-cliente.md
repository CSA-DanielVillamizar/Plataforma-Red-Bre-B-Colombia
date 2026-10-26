# Semana 14 — Idempotencia desde el cliente, medida

**Fecha:** 26 de octubre de 2026
**Entorno:** Windows 11 · .NET SDK 10 · API `Breb.Cuentas` en `http://0.0.0.0:5080` · PostgreSQL 5433 y RabbitMQ 5673 en Docker

Cuenta de laboratorio `11111111-1111-1111-1111-111111111111`, con 4 900,00 UVB disponibles y 0,00 retenidos al empezar cada corrida.

---

## 1. El problema, reproducido

El escenario no es rebuscado. Es el que ocurre todos los días en un bus con mala señal:

1. La app pide retener 100 UVB.
2. El servidor retiene y responde.
3. **La respuesta se pierde.**
4. La app no sabe si la operación pasó o no, y reintenta.

Reintentar es lo correcto: sin reintento, una transferencia buena se pierde por un bache de red. El problema es otro.

```bash
# Dos llamadas idénticas — el usuario tocó el botón UNA vez
curl -X POST "$API/cuentas/$CUENTA/retener?montoUVB=100" -H "Authorization: Bearer $TOKEN"
curl -X POST "$API/cuentas/$CUENTA/retener?montoUVB=100" -H "Authorization: Bearer $TOKEN"
```

**Resultado medido:**

| Momento | Disponible | Retenido |
|---|---|---|
| Antes | 4 900,00 | 0,00 |
| Después de un toque y un reintento | **4 700,00** | **200,00** |

Y las dos respuestas:

```json
{"transferenciaId":"2176c576-92d5-46db-80e3-a4a1f2f3d074","montoUVB":100}
{"transferenciaId":"7e9ea914-e989-4170-b024-f1cf3a300d1e","montoUVB":100}
```

**Dos identificadores distintos para la misma intención.** El usuario pidió mover 100 UVB y la cuenta quedó con 200 retenidos.

### Por qué pasa

Una sola línea del endpoint:

```csharp
var transferenciaId = Guid.NewGuid();   // ← el SERVIDOR inventa la identidad
```

Para el servidor, los dos intentos son dos peticiones distintas, porque nada en ellas dice que son lo mismo. El cliente no tenía cómo afirmar *"esta es la misma operación que ya te pedí"*.

---

## 2. El arreglo: la llave nace donde nace la intención

La identidad correcta **ya estaba modelada desde la Semana 6**: la clave primaria de `Retencion` es el `TransferenciaId`, así que la base de datos ya garantizaba "una transferencia retiene exactamente una vez".

Lo que faltaba era dejar que esa identidad naciera en el cliente. Una llave que el servidor inventa no puede distinguir dos intentos de lo mismo.

```csharp
async (Guid cuentaId, decimal montoUVB, Guid? transferenciaId, …) =>

var idTransferencia = transferenciaId ?? Guid.NewGuid();

var yaRetenida = await db.Retenciones
    .FirstOrDefaultAsync(r => r.TransferenciaId == idTransferencia);

if (yaRetenida is not null)
    return Results.Ok(new { transferenciaId = idTransferencia,
                            montoUVB = yaRetenida.MontoUVB, repetida = true });
```

**Resultado medido, mismo escenario, tres intentos con la misma llave:**

```json
{"transferenciaId":"954d9fe5-…","montoUVB":100,   "repetida":false}
{"transferenciaId":"954d9fe5-…","montoUVB":100.00,"repetida":true}
{"transferenciaId":"954d9fe5-…","montoUVB":100.00,"repetida":true}
```

| Momento | Disponible | Retenido |
|---|---|---|
| Antes | 4 900,00 | 0,00 |
| Después de **tres** llamadas | **4 800,00** | **100,00** |

### Lado a lado

| | Sin llave del cliente | Con llave del cliente |
|---|---|---|
| Llamadas | 2 | 3 |
| Transferencias creadas | **2** | **1** |
| Retenido al final | **200,00 UVB** | **100,00 UVB** |

---

## 3. Tres decisiones de diseño que no son obvias

### La comprobación va DESPUÉS del bloqueo, no antes

```csharp
.FromSql($"SELECT *, xmin FROM \"Cuentas\" WHERE \"Id\" = {cuentaId} FOR UPDATE")
…
var yaRetenida = await db.Retenciones.FirstOrDefaultAsync(…);
```

Dos reintentos simultáneos sobre la misma cuenta hacen fila por el `FOR UPDATE` de la Semana 5, así que el segundo ve lo que el primero escribió. Si la consulta se hiciera **antes** del bloqueo, ambos verían "no existe", ambos intentarían insertar, y uno chocaría contra la clave primaria y respondería HTTP 500.

### Se responde 200, no 409

Para el cliente, el reintento **tuvo éxito**. Eso es justamente lo que le permite dejar de reintentar y borrar la operación de su cola local. Un 409 lo dejaría pensando que algo salió mal.

### Se devuelve el monto ORIGINAL

Si un cliente con un error reusara la misma llave con otro monto, se devuelve el de la retención que ya existe. La llave identifica la operación, no el cuerpo de la petición.

---

## 4. ⚠️ La trampa de esta semana: «la API respondió» no significa «mi cambio está activo»

Al medir el arreglo, los tres intentos devolvieron identificadores distintos y 300 UVB retenidos. El código estaba bien. Lo que fallaba era la medición.

El proceso anterior de la API nunca murió. Tenía bloqueado el ejecutable, así que el `dotnet run` nuevo **falló al compilar**:

```
error MSB3027: Could not copy "obj\Debug\net8.0\apphost.exe" to
  "bin\Debug\net8.0\Breb.Cuentas.exe". Exceeded retry count of 10. Failed.
  The file is locked by: "Breb.Cuentas (17128)"
```

Y como el proceso viejo **seguía escuchando en el 5080**, `curl` respondía con normalidad. Todo se veía bien: el servidor contestaba, los JSON tenían la forma esperada, no había ningún error a la vista. Simplemente era el código anterior.

**La pista que lo delató** fue que las respuestas no traían el campo `repetida`, que el código nuevo siempre incluye. Sin ese detalle, la conclusión habría sido "el arreglo no sirve".

```bash
netstat -ano | grep ":5080.*LISTENING"    # → PID 17128, el proceso viejo
taskkill /PID 17128 /F
```

> Es la misma familia de errores que el navegador de la Semana 11 y el instrumento lento de la Semana 12: **antes de creerle a una medición, verifique que midió lo que cree que midió.** Que el servidor responda prueba que hay *un* servidor, no que sea *el suyo*.

---

## 5. Lo que la saga ya hacía bien

Entre una corrida y la siguiente, el saldo volvió solo a 4 900,00. Las retenciones huérfanas de la primera medición fueron compensadas por el `timeout` de 15 s de la saga (Semana 3) y por el conciliador del Issue #46.

Eso matiza el problema pero no lo resuelve:

- La compensación llega **tarde**. Mientras tanto, el usuario vio un saldo equivocado.
- Y si el abono del banco destino se hubiera confirmado antes del timeout, las dos transferencias se habrían completado: **la plata sale dos veces, y ninguna compensación la devuelve.**

La idempotencia no reemplaza a la saga. Evita que la saga tenga que limpiar un desastre que nunca debió existir.

---

## 6. Reproducir esto

```bash
docker start breb-postgres breb-rabbitmq
cd src/Breb.Cuentas && dotnet run --urls http://0.0.0.0:5080

# Token de operador (retener exige rol operador, Semana 8)
curl -X POST http://localhost:5080/token -H "Content-Type: application/json" \
     -d '{"usuario":"ana.operadora","clave":"Operadora-2026"}'

# SIN llave: cada llamada crea una transferencia nueva
curl -X POST "http://localhost:5080/cuentas/$CUENTA/retener?montoUVB=100" \
     -H "Authorization: Bearer $TOKEN"

# CON llave: la segunda y la tercera devuelven repetida=true
LLAVE=$(uuidgen)
curl -X POST "http://localhost:5080/cuentas/$CUENTA/retener?montoUVB=100&transferenciaId=$LLAVE" \
     -H "Authorization: Bearer $TOKEN"
```

Antes de creerle al resultado: compruebe que está hablando con el proceso que acaba de compilar.
