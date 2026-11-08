# Manual de entrega en iPhone

Guía para demostrar los requisitos de los Issues #29 y #66 usando un iPhone
físico, un Mac y la API local.

## Credenciales de laboratorio

| Usuario | Clave | Rol | Uso |
|---|---|---|---|
| `ana.operadora` | `Operadora-2026` | `operador` | Login, saldo y transferencias |
| `luis.consulta` | `Consulta-2026` | `consulta` | Login y saldo; transferir debe dar 403 |
| `core-bancario` | `CoreBancario-2026` | `banco` | Confirmar abonos con el script |

Cuenta de demostración:

```text
11111111-1111-1111-1111-111111111111
```

## 1. Preparar la API

Desde la raíz del repositorio:

```bash
cd src/Breb.Cuentas
dotnet run --urls http://0.0.0.0:5080
```

Obtén la IP Wi-Fi del Mac:

```bash
ipconfig getifaddr en0
```

Configúrala en `src/Breb.App/Servicios/ConfiguracionApi.cs`:

```csharp
public const string IpEnLaWifi = "192.168.x.x";
```

Desde Safari del iPhone prueba:

```text
http://IP_DEL_MAC:5080/swagger
```

Recompila y reinstala la app después de cambiar la IP.

## 2. Flujo normal

1. Entra como `ana.operadora`.
2. Consulta el saldo.
3. Realiza una transferencia de `100`.
4. Copia la llave de idempotencia mostrada por la app.
5. Confirma el abono antes de 15 segundos:

```bash
bash scripts/confirmar-abono.sh LLAVE http://IP_DEL_MAC:5080
```

6. Espera unos segundos y refresca el saldo.

El script debe mostrar `HTTP 202` y el saldo debe quedar sin fondos retenidos.

## 3. Prueba offline deteniendo el backend

Esta prueba reemplaza el modo avión.

1. Detén `Breb.Cuentas` con `Ctrl+C`.
2. En el iPhone crea una transferencia de `100`.
3. La app debe informar que quedó guardada y mostrarla como pendiente.
4. Cierra completamente la app desde el selector de aplicaciones.
5. Ábrela, inicia sesión y vuelve a **Transferir**.
6. Comprueba que la operación sigue pendiente y conserva la misma llave.
7. Reinicia la API:

```bash
cd src/Breb.Cuentas
dotnet run --urls http://0.0.0.0:5080
```

8. Pulsa **Reenviar lo pendiente**.
9. Comprueba que la operación desaparece de la cola después del `200`.

La cola se guarda en `FileSystem.AppDataDirectory`, por lo que cerrar la app
no debe eliminar la intención pendiente.

## 4. Demostrar idempotencia sin consultar la base de datos

La evidencia se obtiene repitiendo la misma solicitud con la misma llave.

```bash
API=http://127.0.0.1:5080
TOKEN=$(curl -sS -X POST "$API/token" \
  -H 'Content-Type: application/json' \
  -d '{"usuario":"ana.operadora","clave":"Operadora-2026"}' \
  | python3 -c 'import json,sys; print(json.load(sys.stdin)["token"])')
ID=$(uuidgen | tr '[:upper:]' '[:lower:]')
```

Primera solicitud:

```bash
curl -sS -X POST \
  "$API/cuentas/11111111-1111-1111-1111-111111111111/retener?montoUVB=100&transferenciaId=$ID" \
  -H "Authorization: Bearer $TOKEN"
```

Debe responder con:

```json
{"repetida":false}
```

Repite exactamente el mismo comando. Debe responder:

```json
{"repetida":true}
```

La misma llave y `repetida:true` demuestran que el segundo intento no creó
otra retención. Después confirma el abono con `confirmar-abono.sh`.

También se puede demostrar desde la app: después de enviar una transferencia,
pulsa **Reintentar la misma transferencia** antes de que termine la saga. La app
reenvía exactamente la misma llave y el backend debe mostrar:

```text
IDEMPOTENCIA: misma llave=...; no se retiene nuevamente.
```

La respuesta debe indicar `repetida:true` y el saldo retenido no debe aumentar.

## 5. Logs de dispositivo e IP

La app envía `X-Client-Platform` y `X-Client-Device`. La API obtiene la IP
real de la conexión y muestra esos datos en las retenciones:

```text
Retención solicitada: llave=... monto=100 UVB dispositivo=iPhone... plataforma=iOS ip=...
Fondos retenidos: llave=... monto=100 UVB dispositivo=iPhone... plataforma=iOS ip=...
Retención repetida ignorada: llave=... dispositivo=iPhone... plataforma=iOS ip=...
```

La confirmación ejecutada por el script aparece como `dispositivo=script`.
No se registran contraseñas ni tokens.

## 6. Bandeja del rol banco

El usuario `core-bancario` ahora tiene una pantalla propia. Al iniciar sesión,
la app abre **Bandeja del banco** y consulta las retenciones pendientes de la
API. Una transferencia aparece allí mientras no haya sido liquidada ni
compensada por la saga. Cuando la saga devuelve los fondos (`Liberada=true`),
la transferencia deja de aparecer y el endpoint rechaza cualquier aprobación
tardía.

1. Crea una transferencia con `ana.operadora`.
2. Inicia sesión como `core-bancario` con la misma API.
3. Pulsa **Actualizar transferencias**.
4. Verifica la llave y el monto de la transferencia.
5. Pulsa **Aprobar**.
6. Confirma que desaparece de la bandeja.
7. Refresca el saldo de la cuenta origen con el usuario operador.

La lista está protegida por el rol `banco`. Un usuario `operador` o `consulta`
recibe `403` si intenta consultar o aprobar la bandeja. La aprobación usa el
mismo endpoint protegido que el script:

```text
GET  /transferencias/pendientes
POST /transferencias/{transferenciaId}/confirmar-abono
```

## Evidencias para entregar

- Login exitoso en el iPhone.
- Saldo real obtenido desde la API.
- Dirección `http://IP_DEL_MAC:5080` visible en la app.
- Transferencia pendiente con la API detenida.
- Misma operación pendiente después de cerrar y abrir la app.
- Reenvío exitoso después de reiniciar la API.
- Respuestas `repetida:false` y `repetida:true`.
- Logs con la llave, dispositivo e IP.
- Confirmación del abono y saldo final sin retención.
