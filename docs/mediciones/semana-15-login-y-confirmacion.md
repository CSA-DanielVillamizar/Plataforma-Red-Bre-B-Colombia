# Semana 15 — Login único, dirección por aparato y el actor que confirma

**Fecha:** 4 de noviembre de 2026
**Entorno:** Windows 11 · .NET SDK 10.0.400 · API `Breb.Cuentas` en `http://0.0.0.0:5080` · PostgreSQL 5433 y RabbitMQ 5673 en Docker · emulador Pixel 5, Android 13 (API 33).

> **Por qué existe este documento.** Al revisar el Issue #29 contra el código publicado aparecieron tres huecos: la mitad de la rúbrica del E4 no tenía implementación de referencia. Esto es lo que se construyó para cerrarlos y lo que se midió de cada cosa. PR #68.

---

## 0. El hueco, medido contra la rúbrica

| Criterio del E4 | Peso | Antes |
|---|---|---|
| Comportamiento offline: encola y sincroniza | 30 % | ✅ existía |
| No duplica al reintentar | 20 % | ✅ existía |
| Flujo login → saldo → transferencia → **confirmación** | 25 % | ⚠️ a medias |
| Instalada en **teléfono físico** | 25 % | ❌ imposible |

Los dos primeros ya estaban hechos y medidos. Los otros dos, no — y el de 25 % del teléfono no era «falta probarlo»: era **imposible con el código publicado**.

---

## 1. La dirección de la API estaba clavada en el emulador

`MauiProgram.cs` elegía así:

```csharp
var direccionApi = DeviceInfo.Platform == DevicePlatform.Android
    ? "http://10.0.2.2:5080"       // ← para TODO Android
    : "http://127.0.0.1:5080";
```

`10.0.2.2` es un alias que el **emulador** reserva para hablar con la máquina que lo hospeda. En un teléfono físico no existe y no resuelve. La app de referencia no podía funcionar en un aparato real.

Y el síntoma no ayuda: el paquete sale, nadie contesta, y a los 10 segundos aparece un timeout **que parece lentitud del servidor**. Es el mismo patrón del firewall de la Clase 19 — no rechaza, deja esperando.

### Lo que hay ahora

`ConfiguracionApi` decide con **`DeviceInfo.DeviceType`**, que distingue `Virtual` de `Physical`. Eso es lo que faltaba: antes se preguntaba por plataforma, y la plataforma no sabe si es emulador.

```csharp
public const string IpEnLaWifi = "";   // ← lo UNICO que un squad cambia
```

| Dónde corre | Dirección |
|---|---|
| Windows / macOS | `127.0.0.1` |
| Emulador de Android | `10.0.2.2` |
| Teléfono físico | la IP de la máquina en la WiFi |
| Con `IpEnLaWifi` puesta | esa IP, en los tres casos |

La IP de la LAN gana sobre todo lo demás a propósito: **es la única de las tres que sirve en los tres sitios**. Para la demo del E4 conviene ponerla y no volver a pensar en esto.

### Y avisa en vez de dejar esperando

Cuando corre en un teléfono físico sin IP configurada, `FaltaConfigurar` es `true` y los ViewModels lo dicen **antes** del primer intento. Las dos pantallas muestran además a qué dirección están llamando:

```
http://10.0.2.2:5080 · alias del anfitrión (sólo emulador)
```

Si eso sale en un teléfono, el problema está identificado sin esperar el timeout.

---

## 2. Había dos pantallas sueltas, no una app

Cada pantalla pedía usuario y clave por su cuenta, guardaba su propio token, y **con usuarios distintos**: `luis.consulta` en saldo, `ana.operadora` en transferencia. Además pedía un token nuevo en *cada* operación.

### Un solo token alcanza, y la razón está en la Semana 8

```csharp
o.AddPolicy(Politicas.LecturaCuentas, p => p.RequireRole("operador", "consulta"));
```

Quien puede mover plata también puede leer saldos. Entrando como `ana.operadora` las dos pantallas funcionan. Al revés no: `luis.consulta` lee el saldo y recibe **403** al retener, que no es un error de la app sino la autorización funcionando.

La app avisa de eso al entrar, sin bloquear.

### La trampa de ciclos de vida que casi se cuela

Lo natural era que `SesionUsuario` recibiera el `ClienteBrebApi` y pidiera su propio token. Sería un error:

| Servicio | Ciclo de vida | Por qué |
|---|---|---|
| `ClienteBrebApi` | **transitorio** | lo registra `AddHttpClient`, para que la fábrica rote handlers |
| `SesionUsuario` | **singleton** | es UNA sesión |

Un singleton que inyecta un transitorio lo **captura para siempre** y congela ese handler: deja de ver cambios de DNS y de red. Es un bug que no da la cara en el laboratorio, con una IP fija, y sí la da en producción.

Por eso `SesionUsuario` sólo **guarda** el resultado. Quien sale a la red es el ViewModel, que es transitorio y puede recibir el cliente sin capturarlo.

### Lo que NO se guarda en disco

El token. Un JWT en un archivo es una credencial en reposo, y éste dura **15 minutos** —medido contra `/token`—, así que no vale el riesgo. Si la app se cierra hay que volver a entrar.

La **cola** sí sobrevive al cierre, porque guarda la *intención* del usuario, no su *identidad*. Son cosas distintas y se tratan distinto. Esa decisión tiene una consecuencia que casi cuesta caro, y está en la sección 4.

---

## 3. La app nunca confirmaba, y no podía

La app llamaba `/token`, `/saldo` y `/retener`. **Nunca** `/confirmar-abono`. Consecuencia: toda transferencia terminaba compensada a los 15 segundos, y el «→ confirmación» del criterio no se podía demostrar.

No era un olvido. Está en la API:

```csharp
app.MapPost("/transferencias/{id}/confirmar-abono", …)
    .RequireAuthorization(Politicas.BancoDestino);   // rol "banco"
```

La app entra con rol `operador`. **Si la app del pagador pudiera confirmar su propio abono, la saga no serviría para nada**: estaría certificando que llegó plata a un banco con el que nunca habló. El que confirma es otro actor, con otra identidad — y por eso es un script, no un botón.

### Los dos finales, medidos

`scripts/confirmar-abono.{ps1,sh}` entra como `core-bancario` y confirma. Cuatro transferencias sobre las cuentas de demo:

```sql
SELECT "TransferenciaId","MontoUVB","Liberada","Liquidada" FROM "Retenciones";
```

| Llave | Monto | `Liberada` | `Liquidada` | Disponible | Final |
|---|---|---|---|---|---|
| `0db3c1c1` | 100,00 | f | **t** | 5 000 → 4 900 | **completada** |
| `68a8f16e` | 100,00 | f | **t** | 4 900 → 4 800 | **completada** |
| `9d441ca0` | 250,00 | **t** | f | 4 750 → **5 000** | compensada |
| `817f087c` | 100,00 | **t** | f | — | compensada (31 oct) |

**Es la primera vez en el semestre que el sistema de referencia llega al camino feliz.** Todas las mediciones anteriores mostraban `Liberada = t`, porque sin segundo actor no había otra salida posible.

### Lo que de verdad distingue los dos finales

**Los dos dejan el retenido en 0.** Mirar el retenido no distingue nada. Lo que se mueve es el **disponible**, y se mueve hacia arriba sólo cuando la plata regresa:

| | retenido | disponible | en la base |
|---|---|---|---|
| completada | → 0 | **se queda** | `Liquidada = true` |
| compensada | → 0 | **sube** | `Liberada = true` |

### El error de línea base que cometió este mismo script

La primera versión del veredicto comparaba el disponible antes de confirmar contra el de después. **No sirve**: cuando se toma la primera foto, `/retener` ya bajó el disponible de 5 000 a 4 900. En el camino feliz no cambia entre las dos fotos.

El resultado fue que el script imprimió **«COMPENSADA»** mientras la base decía `Liquidada = t`. La medición delató al medidor.

Corregido comparando contra `disponible + retenido`. Y se añadió una rama que faltaba: si al empezar no hay retención viva, el script decía «completada» cuando no había hecho absolutamente nada. Ahora dice **«no había nada que confirmar»**.

---

## 4. ⚠️ La regresión que introdujo este mismo cambio

Lo más importante de este documento, porque rompía el criterio que más pesa.

Al cambiar el login, `EnviarAsync` dejó de pedir un token y pasó a leer el de la sesión:

```csharp
var token = _sesion.TokenVigente();   // lanzaba InvalidOperationException
```

Esa excepción caía en el `catch (Exception)` general de `TransferenciaViewModel`, que **descarta la operación** — por diseño, porque un rechazo del servidor no se arregla reintentando.

Pero la falta de sesión **sí se arregla**: entrando. Y como el token no se guarda en disco, **al reabrir la app nunca hay sesión**. Encadenando las dos cosas:

| Paso | Qué pasaba |
|---|---|
| 1 · Transferir sin red | Encolada en disco ✅ |
| 2 · Cerrar la app | El archivo sobrevive ✅ |
| 3 · Reabrir | No hay sesión (el token no se persiste) |
| 4 · Primer reintento | `TokenVigente()` lanza → catch general → **`DescartarAsync`** |
| 5 · Resultado | **La transferencia pendiente se borra** ❌ |

La cola sobrevivía a cerrar la app y **la mataba el reintento**. Es exactamente lo contrario de lo que pide el 30 % de la rúbrica.

### La corrección

Un tipo propio, `SesionNoDisponibleException`, para que quien llama pueda distinguir dos cosas opuestas:

| Situación | ¿Se arregla reintentando? | Qué hacer con la operación |
|---|---|---|
| El servidor la rechazó | No | descartar |
| Falta la sesión | **Sí, entrando** | **conservar** |

Con su propia rama antes del `catch` general. No cuenta el intento: `Intentos` cuenta intentos de **red**, y esto ni siquiera llegó a salir.

### Medido, no leído

`SesionUsuario` tampoco tiene dependencias de MAUI, así que entra en el arnés de consola por la misma puerta que `ColaOperaciones`:

```bash
cd src/Breb.ColaPrueba && dotnet run -- sesion /tmp/sesion
```

```
1 · recién creada, sin entrar
    EstaAutenticado = False   (esperado False)
  ✓ TokenVigente(): SesionNoDisponibleException · "No hay sesión abierta. Entre primero."
2 · con un token YA VENCIDO
    Expiro          = True   (esperado True)
  ✓ TokenVigente(): SesionNoDisponibleException · "Su sesión expiró. Vuelva a entrar."
3 · con un token vigente
    EstaAutenticado = True · PuedeTransferir = True
4 · rol consulta: puede leer, no puede mover plata
    PuedeTransferir = False   (esperado False)
5 · cerrar sesión
    EstaAutenticado = False · Expiro = False

TODO BIEN
```

El caso 5 distingue dos estados que es fácil confundir: **no hay sesión** no es lo mismo que **la sesión venció**, y el mensaje que merece el usuario es distinto.

> La lección metodológica es la de siempre, en una variante nueva: el cambio que arregla un criterio rompió otro, y lo rompió **en silencio**. No falla el build, no falla la pantalla; sólo desaparece una transferencia. Lo encontró releer el camino de errores preguntando «¿qué pasa aquí si no hay sesión?», no una prueba que ya existiera.

---

## 5. El APK

```bash
cd src/Breb.App
rm -rf obj bin
dotnet build -f net10.0-android -c Debug -p:EmbedAssembliesIntoApk=true
```

**81,4 MB**, build limpio, con el código del login dentro. Confirma que todo lo anterior compila para Android y que no cayó en la trampa de los 12 MB de la Clase 19.

---

## 6. Qué quedó sin verificar

Dicho de entrada, porque la mitad del valor de un documento de mediciones es decir dónde se acaba lo medido.

| | Estado |
|---|---|
| Login y navegación en **Windows** | ✅ visto en pantalla: entra, navega y comparte la sesión |
| Los dos finales de la saga | ✅ medidos contra la base |
| El contrato de la sesión | ✅ cinco casos en consola |
| El APK de Android | ✅ compila, 81,4 MB |
| El escenario completo del E4 **en Android** | ❌ **sin verificar** |
| `Consultar saldo` en instancia limpia de Windows | ❌ sin verificar |
| **Teléfono físico** | ❌ sin aparato disponible |

Sobre el `Consultar saldo`: dio timeout una vez, pero en una instancia lanzada como **hija de otra aplicación**, que hereda el AppContainer de Windows que bloquea loopback — la falla del 28 de octubre, no un defecto de este cambio. La API responde `/saldo` en **71 ms** con ese mismo token. Falta repetirlo en una instancia lanzada desde una consola normal.

El escenario que más importa verificar en el aparato es el de la sección 4: encolar sin red, matar la app, reabrir **sin entrar**, y comprobar que la operación **sigue ahí**.

---

## 7. Reproducir esto

```bash
# el contrato de la sesion, sin abrir la app
cd src/Breb.ColaPrueba && dotnet run -- sesion /tmp/sesion

# una transferencia que SE COMPLETA (hay que confirmar dentro de los 15 s)
TOK=$(curl -s -X POST http://127.0.0.1:5080/token -H 'Content-Type: application/json' \
      -d '{"usuario":"ana.operadora","clave":"Operadora-2026"}' | jq -r .token)
LLAVE=$(python -c "import uuid;print(uuid.uuid4())")
curl -s -X POST "http://127.0.0.1:5080/cuentas/11111111-1111-1111-1111-111111111111/retener?montoUVB=100&transferenciaId=$LLAVE" \
     -H "Authorization: Bearer $TOK"
./scripts/confirmar-abono.sh "$LLAVE"

# y una que se COMPENSA: lo mismo, pero sin confirmar y esperando 20 s
docker exec breb-postgres psql -U postgres -d brebcuentas \
  -c 'SELECT "TransferenciaId","Liberada","Liquidada" FROM "Retenciones";'
```
