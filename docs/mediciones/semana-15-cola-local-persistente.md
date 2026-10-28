# Semana 15 — La cola local persistente, medida

**Fecha de la medición:** 28 de octubre de 2026 *(se construye antes de la clase del 4 de noviembre, porque es el 30 % de la rúbrica del E4 y no había código de referencia).*

**Entorno:** Windows 11 · .NET SDK 10 · API `Breb.Cuentas` en `http://0.0.0.0:5080` · PostgreSQL 5433 y RabbitMQ 5673 en Docker. Cuentas de laboratorio `1111…` y `2222…` con 5 000,00 UVB.

---

## 1. El criterio que hay que cumplir

Del Issue #29, rúbrica del E4:

> **Comportamiento offline: encola y sincroniza al recuperar red — 30 %**
> Cola local persistente: **si la app se cierra, la transferencia pendiente sobrevive**.

Lo que había antes de esto: la llave vivía en un campo del ViewModel. Sobrevivía a un error de red, pero no a cerrar la app.

---

## 2. Cómo se comprueba «sobrevive a cerrar la app» sin abrir la app

`ColaOperaciones` se escribió **sin una sola dependencia de MAUI**: recibe la carpeta donde escribir y nada más. Eso permite ejercitarla desde una consola normal, en **procesos separados** — que es justamente lo que significa «la app se cerró».

El arnés es `src/Breb.ColaPrueba`. No referencia `Breb.App`: **compila los mismos archivos fuente**.

```xml
<Compile Include="..\Breb.App\Modelos\OperacionPendiente.cs" />
<Compile Include="..\Breb.App\Servicios\ColaOperaciones.cs" />
```

Lo que se prueba es el código que corre en la app, no una copia parecida.

> Esto es el argumento de la clase del 9 de noviembre, hecho concreto: una clase sin dependencias de interfaz se prueba con una consola. Si la cola hubiera llamado a `FileSystem.AppDataDirectory` por dentro, haría falta un emulador para probarla.

---

## 3. El contrato, punto por punto

### 3a. Sobrevive a que el proceso muera

```
PROCESO 1 · encolar
  encolada   d4393e42-9e1b-4865-a376-a796bb660a3b

PROCESO 2 · leer          ← proceso distinto, el anterior ya no existe
  pendientes 1
    d4393e42-9e1b-4865-a376-a796bb660a3b   100.00 UVB  intentos=0

PROCESO 3 · encolar       ← una segunda
PROCESO 4 · leer
  pendientes 2
```

✅ **Lo encolado sobrevive.**

### 3b. El contador de intentos, y sólo en la que falló

```
intentos=1  d4393e42-…      ← la que falló
intentos=0  1e5d842e-…      ← la otra, intacta
```

### 3c. Confirmar saca una sola

```
antes:   pendientes 2
después: pendientes 1       ← sólo salió la confirmada
```

### 3d. Veinte encolados en paralelo

El `SemaphoreSlim` serializa las escrituras. Sin él, dos operaciones que terminen a la vez harían leer-modificar-escribir sobre el mismo archivo y una perdería el cambio de la otra.

```
1 pendiente + 20 en paralelo  →  pendientes 21
```

✅ **Ninguna escritura perdida.** Es el problema de la Semana 5 a escala de un archivo, y se resuelve igual: haciendo fila.

### 3e. Archivo corrupto

```
corromper → escribe JSON truncado en el archivo
leer      → pendientes 0 · código de salida 0
```

La cola devuelve vacío en vez de tumbar la app. **Perder operaciones pendientes es malo; una app que no abre es peor**, porque el usuario no tiene forma de arreglarla.

Y que esto sea casi imposible depende de la escritura atómica:

```csharp
// Se escribe a un temporal y se MUEVE sobre el definitivo.
await JsonSerializer.SerializeAsync(fs, cola, …);
File.Move(temporal, _ruta, overwrite: true);
```

Si el proceso muere a mitad de un `File.WriteAllText`, el archivo queda con JSON truncado y **se pierde la cola completa**, no sólo la operación que se estaba escribiendo. `File.Move` es atómico: o está el contenido viejo entero, o el nuevo entero. Es la idea del Outbox de la Semana 2 —el saldo y el evento se guardan juntos o no se guardan— aplicada a un archivo.

---

## 4. Punta a punta: sin red, la app se cierra, y vuelve la red

| Paso | Qué pasó |
|---|---|
| 1 · El usuario toca, **no hay red** | Encolada en disco antes de intentar la red |
| 2 · Intento de envío | `HTTP 000` — no hubo conexión. `intentos=1` |
| 3 · **La app se cierra** | Proceso nuevo lee: **la operación está ahí** |
| 4 · Vuelve la red, se reenvía | `200 · repetida:false` |
| 5 · El servidor confirmó | Sale de la cola: `pendientes 0` |
| 6 · Saldo | **4 900,00 / 100,00** |

El `repetida:false` del paso 4 es **la respuesta correcta**: el servidor nunca recibió el primer intento, porque estaba caído. La cola no sirve para que siempre salga `repetida=true`; sirve para que la operación **no se pierda**.

---

## 5. El escenario que justifica todo el diseño

El caso anterior es el fácil. El difícil es cuando el envío **sí llega** y se pierde la respuesta — la app no tiene forma de saber que tuvo éxito.

| Paso | Qué pasó |
|---|---|
| 1 · Toca el botón | Encolada en disco antes de la red |
| 2 · El envío **llega al servidor** | `200 · repetida:false` — pero la respuesta se pierde |
| 3 · La app nunca la ve | No confirma → sigue en la cola, `intentos=1` |
| 4 · **La app se cierra y vuelve a abrir** | Proceso nuevo: la operación está ahí |
| 5 · Reenvía con la **misma** llave | `200 · repetida:true` — el servidor la reconoció |
| 6 · Ahora sí confirma | `pendientes 0` |
| 7 · Saldo final | **4 900,00 / 100,00** |

**La operación se envió dos veces, con un cierre completo de la app en medio, y quedó una sola retención de 100 UVB.**

Ahí se ve que son dos piezas distintas trabajando juntas:

| Pieza | Qué garantiza | Sin ella |
|---|---|---|
| La **cola en disco** | La intención no se pierde | Se pierde la transferencia al cerrar la app |
| La **llave** del cliente | La intención no se duplica | El reenvío crea una segunda transferencia |

La cola sin llave duplica. La llave sin cola pierde. Hacen falta las dos.

---

## 6. El orden de los tres pasos, que es lo único que importa

```csharp
// 1. La llave nace AQUI, antes de todo
var op = new OperacionPendiente(Guid.NewGuid(), CuentaOrigen, montoUVB, DateTime.UtcNow);

// 2. A DISCO, antes de intentar la red
await _cola.EncolarAsync(op);

// 3. Y ahora si, la red
await EnviarAsync(op, esReintento: false);
```

Guardar **después** de la llamada no serviría de nada: el hueco peligroso es justamente el de la llamada. Entre el paso 2 y la respuesta del servidor puede pasar cualquier cosa —se cierra la app, se acaba la batería, el sistema la mata por memoria— y la intención ya está a salvo.

### Y cuándo sale de la cola

```csharp
await _cola.ConfirmarAsync(op.Llave);   // SOLO aqui, con respuesta del servidor
```

Da igual si el servidor respondió `repetida=true` o `repetida=false`: las dos significan «esto ya está hecho».

Aquí se cierra el círculo con la decisión del servidor de responder `200` y no `409`. Un `409` lo leeríamos como fallo, no borraríamos la entrada, y la operación quedaría atrapada reintentándose para siempre algo que ya estaba hecho. **La forma de la respuesta del servidor y la lógica de la cola del cliente son la misma decisión vista desde dos lados.**

### El único caso en que se descarta sin éxito

```csharp
catch (Exception ex)   // clave mala, rol equivocado, monto invalido
{
    await _cola.DescartarAsync(op.Llave);
}
```

Un rechazo que no es de red no se arregla reintentando. Dejarlo encolado sería reintentar para siempre algo que el servidor nunca va a aceptar.

---

## 7. Dos decisiones de configuración que no son obvias

### `AppDataDirectory`, no `CacheDirectory`

```csharp
builder.Services.AddSingleton(new ColaOperaciones(FileSystem.AppDataDirectory));
```

`AppDataDirectory` es la carpeta privada que el sistema respalda y no borra. Si esto apuntara a `CacheDirectory`, **Android podría borrar las transferencias pendientes del usuario cuando necesitara espacio**.

### Singleton, no transient

Es **una** cola. Dos instancias escribiendo el mismo archivo se pisarían, y el candado que serializa las escrituras vive dentro de la instancia.

---

## 8. Sobre el code-behind

La regla de la Clase 15 es «sin **lógica** en el code-behind», no «sin una sola línea». Esta pantalla necesita enterarse de que apareció, para mostrar lo que quedó pendiente y suscribirse a los cambios de red:

```csharp
protected override void OnAppearing()
{
    base.OnAppearing();
    _viewModel.AparecerCommand.Execute(null);
}
```

Cuatro líneas, ninguna decide nada: reenvían un evento del ciclo de vida al ViewModel. `OnAppearing` es un método de la página, no un evento al que el ViewModel pueda suscribirse.

Quitarlo del todo requeriría el `EventToCommandBehavior` del `CommunityToolkit.Maui` — una dependencia más para ahorrar cuatro líneas sin lógica. No vale la pena, y conviene saber por qué.

---

## 9. Reproducir esto

```bash
cd src/Breb.ColaPrueba && dotnet build

EXE=bin/Debug/net8.0/Breb.ColaPrueba.exe
C=/tmp/cola

$EXE encolar     $C            # proceso 1
$EXE leer        $C            # proceso 2: ve lo del proceso 1
$EXE intento     $C <llave>
$EXE confirmar   $C <llave>
$EXE concurrente $C 20         # 20 en paralelo
$EXE corromper   $C            # y despues: leer
```

Lo que falta y es honesto decirlo: **esto se midió en Windows, no en Android.** El mecanismo es el mismo —`File.Move` es atómico en los dos— pero la carpeta y los permisos cambian. La comprobación en un teléfono real es el 4 de noviembre, y es exactamente para lo que sirve esa clase.
