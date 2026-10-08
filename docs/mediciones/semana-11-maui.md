# Semana 11 — MAUI I: el primer proyecto, medido

**Fecha:** 5 de octubre de 2026
**Entorno:** Windows 11 · .NET SDK 10.0.400 · cargas `android`, `ios`, `maccatalyst`, `maui-windows` · emulador `pixel_5_-_api_33` (Android 33)

Todas las cifras son de **una corrida en una máquina**. Sirven para dar orden de magnitud, no para comparar equipos.

---

## 1. Cuánto tarda el primer proyecto

```bash
dotnet new maui -n Breb.App -o C:\breb\Breb.App
dotnet build C:\breb\Breb.App -f net10.0-android
dotnet build C:\breb\Breb.App -f net10.0-windows10.0.19041.0
```

| Operación | Tiempo | Confianza |
|---|---|---|
| `dotnet new maui`, incluido el `restore` | **87,7 s** | Alta · una corrida |
| Primer build Android, en frío | **300,2 s** | Alta · una corrida |
| Segundo build Android, sin cambios | **14,5 s** | Alta · una corrida |
| Primer build Windows, en frío | **44,3 s** | Alta · una corrida |

**La relación que importa: 300 → 14,5 s, veinte veces.** El primer build descarga el SDK de Android, compila recursos con `aapt2`, compila el código una vez por target framework y empaqueta el APK. Solo se paga una vez.

> Contexto: la caché de NuGet se había vaciado días antes, así que los 87,7 s del `restore` son un caso desfavorable realista, no el mejor caso.

### Consumo de disco

| Momento | Libre |
|---|---|
| Antes de crear el proyecto | 13,2 GB |
| Después de compilar Android y Windows | 10,6 GB |

**~2,6 GB** por el ciclo completo en una sola máquina.

---

## 2. El fallo de la ruta larga — `APT2000`

**Reproducido:** el mismo proyecto, dos ubicaciones distintas.

| Longitud de la ruta | Resultado |
|---|---|
| 155 caracteres | **40 errores `APT2000`**, falla a los 27 s |
| 20 caracteres (`C:\maui-clase14\App2`) | **0 errores**, compila bien |

```
\\?\C:\Users\...\scratchpad\clase14\src\Breb.App\obj\Debug\net10.0-android\lp\103\jl\res\..\flat\103.flata :
    error APT2000: The device does not recognize the command. (22)
```

**Diagnóstico.** `aapt2`, el compilador de recursos de Android, no tolera el límite de longitud de ruta de Windows. El prefijo `\\?\` que aparece al inicio del mensaje es la forma en que Windows representa rutas que superan ese límite, y es la única pista: **el texto del error no menciona rutas en ningún momento**.

**Regla práctica:** crear los proyectos en `C:\breb`, nunca bajo `Documentos\Universidad\Semestre 7\...`.

**Síntoma secundario:** una vez que el build falló, la carpeta `obj/` queda con rutas tan largas que ni `Copy-Item` ni `Remove-Item` pueden recorrerla. Hay que borrarla desde una ruta corta.

---

## 3. El emulador y la API: `localhost` contra `10.0.2.2`

La API corriendo en la máquina anfitriona:

```bash
dotnet run --urls http://0.0.0.0:5080
```

| Desde el emulador, hacia… | Resultado |
|---|---|
| `http://localhost:5080` | **Connection refused** |
| `http://10.0.2.2:5080` | **HTTP 200** |

Evidencia del lado del servidor — el log de la API:

```
INF Request finished HTTP/1.0 GET http://10.0.2.2/swagger/index.html - 200 null text/html;charset=utf-8 5.5456ms
```

**Por qué.** Dentro del emulador, `localhost` es el propio emulador, que es un sistema Android completo con su propia pila de red. Android reserva `10.0.2.2` como alias de la máquina anfitriona.

**En un teléfono real `10.0.2.2` no sirve:** ahí hay que usar la IP del computador en la red WiFi. Eso entra en el Bloque C, el 4 de noviembre.

### Dos detalles de herramienta

- **La API debe escuchar en `0.0.0.0`.** Con `--urls http://localhost:5080` solo escucha en loopback.
- **Esa imagen de Android no trae `curl`.** Sí trae `nc` y `ping`:

  ```bash
  adb shell "printf 'GET /swagger/index.html HTTP/1.0\r\n\r\n' | nc -w 6 10.0.2.2 5080 | head -1"
  ```

- **Las capturas del emulador se sacan con `adb pull`.** La redirección de PowerShell corrompe el PNG:

  ```powershell
  adb shell screencap -p /sdcard/shot.png
  adb pull /sdcard/shot.png .\shot.png
  ```

---

## 4. La cuarta trampa: el navegador miente

**Encontrada en la revisión del PR #53, no en las pruebas.** Vale la pena dejarlo escrito tal cual, porque es un fallo de método.

La verificación de la §3 se hizo **con el navegador del emulador**, y de ahí se concluyó que la app podría alcanzar la API. **Esa conclusión no se sigue.**

Desde API 28, Android rechaza el tráfico HTTP en claro **para las aplicaciones**. El navegador tiene su propia política de red y no está sujeto a la de la app. Nuestra API de laboratorio vive en `http://10.0.2.2:5080`, sin TLS, de modo que el `HttpClient` de `Breb.App` habría fallado aunque:

- el permiso `INTERNET` estuviera concedido (lo está, por plantilla),
- y el Swagger abriera perfectamente en Chrome dentro del emulador (abría).

Es decir: la prueba que se hizo **no probaba lo que se creía que probaba**. El síntoma habría aparecido en la Semana 12, al escribir el primer `HttpClient`, y habría costado una sesión entera.

**Corrección aplicada** en `Platforms/Android/MainApplication.cs`:

```csharp
#if DEBUG
[Application(UsesCleartextTraffic = true)]
#else
[Application]
#endif
```

Solo en `Debug`: una app que acepte HTTP en claro en `Release` expondría tokens y saldos en texto plano.

> **La lección de método**, que es la misma de la Semana 9: la prueba tiene que ejercitar **el mismo camino** que el código real. Probar con otro cliente —un navegador, un `curl`, un `nc`— demuestra que la red funciona, no que la aplicación funcione. Pendiente de verificar con un `HttpClient` real en la Semana 12.

---

## 5. Lo que queda abierto

- **Los tiempos son de una sola máquina.** La actividad de la Semana 11 (#52) pide a los squads medir en las suyas, para saber si 300 s es representativo o si el rango es amplio.
- **El consumo de disco se midió una vez.** Falta saber cuánto crece con varios proyectos en paralelo.
- **Nada de esto se ha probado en un teléfono real.** Es el Bloque C.
