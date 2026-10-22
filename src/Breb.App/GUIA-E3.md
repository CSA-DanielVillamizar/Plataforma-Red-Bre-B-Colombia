# Guía para el Evaluable 3 — poner la app a funcionar

## Para los squads · Red Bre-B Colombia (190304014-1)

**Esto es para seguirlo en la máquina, paso por paso.** No es lectura.

Si algo no le sale, no salte adelante: cada paso tiene una comprobación, y el siguiente no funciona sin ella. Todas las cifras que aparecen aquí están medidas en una máquina del curso; sirven para que usted sepa qué es normal y qué no.

Referencia completa de las trampas de MAUI: [`LEEME.md`](LEEME.md).

---

## 0. Antes de empezar

| Necesita | Comprobación |
|---|---|
| Docker corriendo | `docker ps` muestra `breb-postgres` y `breb-rabbitmq` en `healthy` |
| El repositorio clonado **en ruta corta** | `C:\breb`, no en `C:\Users\...\Documentos\...` |
| El SDK de MAUI | `dotnet workload list` incluye `maui-android` |
| El emulador creado | `emulator -list-avds` muestra al menos uno |

> **La ruta corta no es capricho.** `aapt2` falla con el límite de longitud de ruta de Windows: medido, 155 caracteres de ruta → 40 errores `APT2000`; 20 caracteres → 0 errores. El texto del error no menciona rutas; la única pista es el prefijo `\\?\`.

---

## 1. Los cuatro pasos, en orden

**Nadie pasa al siguiente sin ver el resultado del anterior.** Lanzar los cuatro en paralelo hizo que un build pasara de 300 s a 3 055 s (medido en la Semana 11).

### Paso 1 — Infraestructura

```bash
docker start breb-postgres breb-rabbitmq
docker ps
```

✅ **Siga cuando** los dos contenedores digan `healthy`. No antes.

### Paso 2 — La API, escuchando en todas las interfaces

```bash
cd src/Breb.Cuentas
dotnet run --urls http://0.0.0.0:5080
```

✅ **Siga cuando** aparezca `Now listening on: http://0.0.0.0:5080`.

> `0.0.0.0` y no `localhost`: si la API solo escucha en `localhost`, el emulador no la alcanza aunque la dirección de la app esté bien.

Comprobación rápida, en otra terminal:

```bash
curl -X POST http://localhost:5080/token -H "Content-Type: application/json" \
     -d '{"usuario":"luis.consulta","clave":"Consulta-2026"}'
```

Debe devolver un JSON con `token`, `rol` y `expira`.

### Paso 3 — El emulador, arrancado del todo

```bash
emulator -avd pixel_5_-_api_33
adb wait-for-device shell getprop sys.boot_completed
```

✅ **Siga cuando** imprima `1`. Mientras diga `device offline`, el emulador todavía está arrancando.

**Cuánto tarda, medido:**

| Operación | Tiempo |
|---|---|
| Arranque en frío | 215 s |
| `adb reboot` | 76 s |

Tres minutos y medio es normal. No lo mate pensando que se colgó.

### Paso 4 — Recién ahora, la app

```bash
cd src/Breb.App
dotnet build -f net10.0-android -t:Run
```

✅ **Listo cuando** la app aparezca en el emulador mostrando "Bre-B · Saldo de la cuenta".

---

## 2. La prueba que vale el 35 %

1. Toque **Consultar saldo**.
2. Debe aparecer **4.900,00 UVB** y, debajo, `OK · rol consulta · retenido 0.00 UVB`.
3. En la terminal de la API deben verse **dos** peticiones:

```
INF Executing endpoint 'HTTP: POST /token'                      traza=a7c657…
INF Executing endpoint 'HTTP: GET /cuentas/{cuentaId:guid}/saldo'  traza=952bf0…
```

**Las dos, con trazas distintas, con un segundo de diferencia.** Si solo ve la primera, el token se pidió pero no se usó.

**Cuánto tardan, medido (5 corridas, mediana):**

| Llamada | Mediana |
|---|---|
| `POST /token` | 430 ms |
| `GET /cuentas/{id}/saldo` | 224 ms |

El token cuesta casi el doble porque firma el JWT. El saldo es una consulta indexada.

---

## 3. ⚠️ El APK que compila y no abre

**Esto es lo que más entregas va a arruinar el miércoles.**

Si toma el `.apk` de `bin/Debug/net10.0-android/` y lo instala a mano —`adb install`, o pasándolo a un teléfono— la app se cierra al abrir. En pantalla solo dice *"la aplicación se detuvo"*. El build había terminado con **0 errores y 0 advertencias**.

La causa está en `adb logcat`:

```
F monodroid: No assemblies found in
    '/data/user/0/com.companyname.breb.app/files/.__override__/x86_64'
    or '<unavailable>'. Assuming this is part of Fast Deployment. Exiting...
F monodroid: Abort at monodroid-glue.cc:757
F libc    : Fatal signal 6 (SIGABRT)
```

En Debug, .NET para Android usa **Fast Deployment**: los ensamblados .NET **no van dentro del APK**, los empuja aparte `dotnet build -t:Run`. El archivo de `bin/` está incompleto a propósito.

### Medido, mismo código, misma máquina

| Forma de compilar | APK | ¿Abre instalado a mano? |
|---|---|---|
| Debug, por defecto | **13 MB** | **No** |
| Debug con `-p:EmbedAssembliesIntoApk=true` | **81 MB** | **Sí** |
| Release | similar | Sí — ya los embebe |

### El APK que sí se puede entregar

```bash
dotnet build -f net10.0-android -c Debug -p:EmbedAssembliesIntoApk=true
```

El archivo queda en `bin/Debug/net10.0-android/com.companyname.breb.app-Signed.apk`.

> **Tarda más.** Medido: 1 464,9 s contra 593,7 s del build normal. Hágalo **una sola vez, al final**, cuando ya va a entregar. Para desarrollar, use `-t:Run`.

### El síntoma que despista

Mientras la app aborta, Android la reinicia en bucle:

```
W ActivityManager: Process com.companyname.breb.app has crashed too many times,
                   killing! Reason: crashed quickly
```

y el sistema entero se ahoga, mostrando **"Process system isn't responding"**. El diálogo habla del sistema; la causa es la app. Medido: la carga del emulador llegó a **11,37** durante el bucle y bajó a **1,98** cuatro minutos después de matar la app.

**Regla: mirar `logcat` antes de culpar al emulador.**

---

## 4. Las otras trampas, por si aparecen

| Síntoma | Causa | Salida |
|---|---|---|
| `Connection refused` desde la app | `BaseAddress` en `localhost` | `http://10.0.2.2:5080` |
| `CS1061: AddHttpClient` | Falta `Microsoft.Extensions.Http` | `dotnet add package Microsoft.Extensions.Http` |
| `error APT2000` | Ruta demasiado larga | Clonar en `C:\breb` |
| El binding no actualiza nada | Falta `[ObservableProperty]` o el `BindingContext` | Revisar el ViewModel y `MainPage.xaml.cs` |
| La app no alcanza la API aunque el navegador sí | Android bloquea HTTP en claro desde API 28 | `UsesCleartextTraffic` en `MainApplication.cs`, **solo en Debug** |

> **Sobre la última.** Que la URL abra en el navegador del emulador **no** demuestra que la app pueda alcanzarla: el navegador tiene su propia política de red, independiente de la de la aplicación.

### Por qué `10.0.2.2` y no otra cosa

| Desde el emulador, hacia… | Resultado |
|---|---|
| `http://localhost:5080` | Connection refused |
| `http://10.0.2.2:5080` | HTTP 200 |

Dentro del emulador, `localhost` es el emulador. La máquina anfitriona está en `10.0.2.2`.

En un **teléfono real** eso no sirve: ahí va la IP del computador en la red WiFi. Pero eso es el Evaluable 4.

---

## 5. La arquitectura que se evalúa

| Pieza | Archivo | Qué sabe | Qué NO debe saber |
|---|---|---|---|
| Contratos | `Modelos/Contratos.cs` | Los DTOs de la API | — |
| Servicio | `Servicios/ClienteBrebApi.cs` | Rutas, JSON, `Bearer` | Que existe una pantalla |
| ViewModel | `ViewModels/SaldoViewModel.cs` | Estado y comandos | Qué control muestra qué |
| Vista | `MainPage.xaml` | Qué se ve y a qué se enlaza | De dónde salen los datos |

**La prueba de que quedó bien:** abra `MainPage.xaml.cs`. Debe tener solo el constructor:

```csharp
public MainPage(SaldoViewModel viewModel)
{
    InitializeComponent();
    BindingContext = viewModel;
}
```

Si ahí hay un `if`, un `try`, o un `saldoLabel.Text = ...`, la arquitectura no está hecha.

---

## 6. La prueba de error, que vale el 10 %

Dos minutos, y es la que más squads fallan:

1. Con la app corriendo, **apague la API** (`Ctrl+C` en su terminal).
2. Vuelva a tocar **Consultar saldo**.

| Lo que debe pasar | Lo que no debe pasar |
|---|---|
| Aparece "Sin conexión con la API" | La app se cierra |

Repítalo con una clave equivocada: debe decir que el login fue rechazado, **sin decir si falló el usuario o la clave**. Ese es el criterio de seguridad de la Semana 8: decir cuál de los dos falló le regala información a quien está probando claves.

---

## 7. Qué entregar el miércoles 21

Un comentario en el **Issue #28**, antes de que empiece la clase:

1. **El enlace al repositorio del squad**, con la rama y el commit que van a sustentar.
2. **La captura de la app** mostrando un saldo real traído de la API.
3. **El fragmento del log del servidor** con `POST /token` y `GET /saldo`, con sus trazas.
4. **Una línea sobre qué les falló** y cómo salieron.

> El punto 4 no es relleno. Un squad que diga "el APK no abría y era Fast Deployment" demuestra más que uno que entregue sin una sola cicatriz.

---

## 8. Credenciales y datos de laboratorio

```
luis.consulta  / Consulta-2026     rol consulta
ana.operadora  / Operadora-2026    rol operador
core-bancario  / CoreBancario-2026 rol servicio

Cuenta de demo:  11111111-1111-1111-1111-111111111111  →  4 900,00 UVB
```

| Servicio | Puerto |
|---|---|
| API `Breb.Cuentas` | 5080 |
| PostgreSQL | 5433 |
| RabbitMQ | 5673 *(UI 15673)* |

---

## 9. Para leer después

- `src/Breb.App/LEEME.md` — las cinco trampas de MAUI, con sus mediciones.
- `docs/mediciones/semana-12-app-jwt.md` — todo lo medido esta semana, incluida una medición que hubo que descartar porque el instrumento resultó más lento que lo medido.
