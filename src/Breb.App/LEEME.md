# Breb.App — la aplicación móvil de la Red Bre-B

Proyecto .NET MAUI creado en la **Clase 14 (Semana 11)** y dotado de arquitectura en la **Clase 15**. Es la referencia de lo que se entrega en el **Evaluable 3** (#28) el 21 de octubre.

## Evaluable 3: las tres pantallas

| # | Pantalla | Qué hace | Endpoint | Rol |
|---|---|---|---|---|
| 1 | Login (`Vistas/LoginPage`) | Pide el JWT y lo guarda en `SecureStorage` | `POST /token` | cualquiera |
| 2 | Saldo (`MainPage`) | Saldo disponible y retenido | `GET /cuentas/{id}/saldo` | `operador` o `consulta` |
| 3 | Transferir (`Vistas/TransferenciaPage`) | Retiene fondos y arranca la saga; muestra el id y el saldo después | `POST /cuentas/{id}/retener?montoUVB=` | `operador` |

Usuario para la demo completa: `ana.operadora` / `Operadora-2026`. Con `luis.consulta` la transferencia responde **403**, y la app lo muestra.

| Pieza | Archivo | Responsabilidad |
|---|---|---|
| Vistas | `Vistas/LoginPage.xaml`, `MainPage.xaml`, `Vistas/TransferenciaPage.xaml` | Solo describen qué se ve y a qué se enlaza |
| ViewModels | `ViewModels/LoginViewModel.cs`, `SaldoViewModel.cs`, `TransferenciaViewModel.cs` | Propiedades observables, comandos, validación y mensajes |
| Servicio HTTP | `Servicios/ClienteBrebApi.cs` | Único lugar que conoce rutas, JSON y el `Bearer`; convierte todo fallo en `ErrorApi` con un mensaje legible |
| Sesión | `Servicios/SesionSegura.cs` | Único dueño del JWT: `SecureStorage` (Android Keystore), con expiración |
| Contratos | `Modelos/Contratos.cs` | Los DTOs de la API, copiados a mano |
| Navegación | `AppShell.xaml` | Login primero; después pestañas Saldo y Transferir |
| Composición | `MauiProgram.cs` | Registra `HttpClient`, sesión, ViewModels y páginas |

Errores que la app muestra en pantalla en vez de congelarse: API apagada o URL mala, timeout de 10 s, clave incorrecta (401), token vencido, rol sin permiso (403), cuenta inexistente (404) y cualquier otro HTTP (por ejemplo, 500 por saldo insuficiente).

Dos paquetes que **no** vienen con la plantilla y hacen falta:

```bash
dotnet add package CommunityToolkit.Mvvm
dotnet add package Microsoft.Extensions.Http
```

`AddHttpClient` llega gratis en una API ASP.NET Core, pero **no** en MAUI. Sin `Microsoft.Extensions.Http` el build falla con `CS1061: IServiceCollection does not contain a definition for 'AddHttpClient'`, y falla al final, después de restaurar y de compilar los recursos de Android.

---

El proyecto está en la solución `src/Breb.Platform.slnx`, junto a `Breb.Cuentas`.

> **¿Va a presentar el Evaluable 3?** Empiece por **[`GUIA-E3.md`](GUIA-E3.md)**: los cuatro pasos en orden, con la comprobación de cada uno y qué hacer cuando falla. Este LEEME es la referencia de fondo; la guía es para seguirla en la máquina.

---

## ⚠️ Antes de compilar: clone el repositorio en una ruta corta

```bash
git clone https://github.com/CSA-DanielVillamizar/Plataforma-Red-Bre-B-Colombia.git C:\breb
cd C:\breb
dotnet build .\src\Breb.App\Breb.App.csproj -f net10.0-android
```

**No lo compile desde una ruta larga.** `aapt2`, el compilador de recursos de Android, falla con el límite de longitud de ruta de Windows:

```
\\?\C:\Users\...\obj\Debug\net10.0-android\lp\103\jl\res\..\flat\103.flata :
    error APT2000: The device does not recognize the command. (22)
```

Medido: 155 caracteres de ruta → **40 errores**; 20 caracteres → **0 errores**. El texto del error no menciona rutas; la pista es el prefijo `\\?\`.

Si el build ya falló, el `obj/` queda con rutas que ni se pueden borrar con normalidad. Bórrelo desde una ruta corta.

---

## Qué tarda

| Operación | Medido |
|---|---|
| Primer build Android, en frío | **300,2 s** |
| Segundo build, sin cambios | **14,5 s** |
| Primer build Windows, en frío | 44,3 s |

Cinco minutos en el primer build es **normal**. Detalle en [`docs/mediciones/semana-11-maui.md`](../../docs/mediciones/semana-11-maui.md).

---

## Hablar con la API Bre-B desde el emulador

La API debe escuchar en todas las interfaces:

```bash
docker start breb-postgres breb-rabbitmq
cd ../Breb.Cuentas
dotnet run --urls http://0.0.0.0:5080
```

Y desde la app, la dirección **no** es `localhost`:

| Desde el emulador, hacia… | Resultado |
|---|---|
| `http://localhost:5080` | Connection refused |
| `http://10.0.2.2:5080` | **HTTP 200** |

Dentro del emulador, `localhost` es el emulador. La máquina anfitriona está en `10.0.2.2`.

> En un **teléfono real** `10.0.2.2` no sirve: ahí se usa la IP del computador en la red WiFi.

Comprobación rápida **del camino de red** (no de la app):

```bash
adb shell am start -a android.intent.action.VIEW -d "http://10.0.2.2:5080/swagger/index.html"
```

---

## ⚠️ Y una cuarta trampa: Android bloquea el HTTP sin cifrar

**Que la URL abra en el navegador del emulador NO significa que la app pueda alcanzarla.**

Desde API 28, Android rechaza el tráfico HTTP en claro para las aplicaciones. Nuestra API de laboratorio vive en `http://10.0.2.2:5080` —sin TLS—, así que el `HttpClient` de la app fallaría **aunque el permiso `INTERNET` esté concedido y aunque el navegador cargue la página sin problema**. El navegador tiene su propia política de red; la de la aplicación es independiente.

Ya está resuelto en `Platforms/Android/MainApplication.cs`:

```csharp
#if DEBUG
[Application(UsesCleartextTraffic = true)]
#else
[Application]
#endif
```

**Solo en Debug**, a propósito: una entrega que acepte HTTP en claro en Release estaría exponiendo tokens y saldos en texto plano.

---

## ⚠️ La quinta trampa: el APK de Debug no se puede instalar a mano

Esta es la que más duele, porque la app **compila sin un solo error** y aun así no abre.

Quien tome el `.apk` de `bin/Debug/net10.0-android/` y lo instale con `adb install` —o lo pase a un teléfono y lo abra— ve que la aplicación se cierra de inmediato. En pantalla no hay información: solo "la aplicación se detuvo". En `adb logcat` sí está la razón:

```
F monodroid: No assemblies found in
    '/data/user/0/com.companyname.breb.app/files/.__override__/x86_64'
    or '<unavailable>'. Assuming this is part of Fast Deployment. Exiting...
F monodroid: Abort at monodroid-glue.cc:757
F libc    : Fatal signal 6 (SIGABRT)
```

**Por qué.** En Debug, .NET para Android usa *Fast Deployment*: para que el ciclo de compilar-y-probar sea corto, los ensamblados .NET **no se empaquetan dentro del APK**. Los copia aparte el propio `dotnet build -t:Run`, a una carpeta privada de la aplicación. El APK que queda en `bin/` está incompleto a propósito, y por sí solo no arranca.

**Las dos salidas:**

```bash
# A) Desplegar con la herramienta, que además empuja los ensamblados
dotnet build -f net10.0-android -t:Run

# B) Generar un APK que se baste a sí mismo (para repartir o instalar a mano)
dotnet build -f net10.0-android -c Debug -p:EmbedAssembliesIntoApk=true
```

Para el **Evaluable 3**, si el squad entrega un `.apk`, tiene que ser uno de los de la opción B —o un build de `Release`, que ya embebe los ensamblados—. Un APK de Debug suelto se ve bien, pesa lo mismo, y no abre en la máquina de nadie más.

> Un síntoma que despista: cuando la app aborta así en bucle, Android muestra **"Process system isn't responding"**. Parece que el emulador está lento o dañado; lo que pasa es que el sistema está reiniciando el proceso una y otra vez. El diálogo no habla de la app, pero la causa es la app.

---

## Plataformas

| Target framework | ¿Se compila en Windows? |
|---|---|
| `net10.0-android` | Sí |
| `net10.0-windows10.0.19041.0` | Sí |
| `net10.0-ios` | No — necesita un Mac |
| `net10.0-maccatalyst` | No — necesita un Mac |

En este curso se usa **Android**, porque el Evaluable 4 exige un teléfono real.
