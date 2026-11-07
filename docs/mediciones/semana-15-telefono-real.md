# Semana 15 — Puesta en marcha en Android, medida

**Fecha:** 31 de octubre de 2026
**Entorno:** Windows 11 · .NET SDK 10.0.400 · carga `android 36.1.69` · emulador **Pixel 5, Android 13 (API 33), x86_64** · API `Breb.Cuentas` en `http://0.0.0.0:5080` · PostgreSQL 5433 y RabbitMQ 5673 en Docker.

> **Alcance, dicho de entrada.** Esto se midió en un **emulador**, no en un teléfono físico. Lo que el emulador sí prueba: el sistema de archivos de Android, los permisos de la carpeta privada, el ciclo de vida real de un proceso Android y el empaquetado del APK. Lo que **no** prueba: la red WiFi de verdad (el emulador usa `10.0.2.2`, un teléfono necesita la IP de la máquina), la instalación por USB, ni el rendimiento de un ARM real. Esas tres se comprueban en clase el 4 de noviembre.

---

## 1. El APK: tres builds, tres resultados

```bash
dotnet build -f net10.0-android -c Debug                                  # limpio
dotnet build -f net10.0-android -c Debug -p:EmbedAssembliesIntoApk=true
dotnet build -f net10.0-android -c Release
```

| Build | Tamaño | ¿Corre instalado con `adb install`? |
|---|---|---|
| Debug, por omisión (Fast Deployment) | **12,2 MB** | **No** — aborta |
| Debug con `EmbedAssembliesIntoApk=true` | **81,3 MB** | **Sí** |
| Release | **28,9 MB** | Sí (los embebe siempre) |

El abort, textual:

```
F monodroid: No assemblies found in '/data/user/0/com.companyname.breb.app/files/.__override__/x86_64'
             or '<unavailable>'. Assuming this is part of Fast Deployment. Exiting...
F monodroid: Abort at monodroid-glue.cc:757
F libc    : Fatal signal 6 (SIGABRT), code -1 (SI_QUEUE) in tid 16005
```

Reproduce exactamente lo medido en la Semana 12. **La regla del curso sigue vigente.**

---

## 2. ⚠️ Lo nuevo, y es peor que la trampa original: el build incremental la esconde

Preparando esta medición, un `dotnet build -f net10.0-android -c Debug` dio **81,3 MB**, con 656 entradas de ensamblados dentro del APK, y al instalarlo a mano **corrió sin problema**.

La conclusión fácil habría sido: «en .NET 10 ya no hace falta la bandera, la trampa desapareció». Y estuvo a punto de escribirse en el material del curso.

Lo que pasaba era otra cosa. El `obj/` traía residuos de una compilación anterior hecha **con** `EmbedAssembliesIntoApk=true`. Con `obj/` y `bin/` borrados, el mismo comando da **12,2 MB** y aborta.

| Mismo comando, distinto estado previo | Tamaño | Corre |
|---|---|---|
| Sobre `obj/` sucio de un build con la bandera | 81,3 MB | **Sí** |
| Sobre `obj/` limpio | **12,2 MB** | **No** |

### Por qué esto importa más que la trampa original

La trampa original falla **siempre**, así que se descubre rápido. Ésta falla **a veces**:

- El squad compila, instala, prueba: **funciona**. Su `obj/` arrastra una compilación vieja.
- Entregan el APK. Un compañero clona limpio, compila, instala: **aborta**.
- El síntoma aparece **en la máquina de otro**, que es la peor forma de descubrir un problema.

**El tamaño es una señal, no una garantía.** Lo que el tamaño sí dice, con total confianza, es qué hay **dentro** del APK: uno de Debug de ~12 MB no lleva los ensamblados, uno de ~81 MB sí. Lo que el tamaño **no** dice es si va a arrancar, porque eso depende también del aparato.

### 2a. El experimento de una sola variable

Esto lo objetó Copilot al revisar el PR #67, y tenía razón, así que se midió. **El mismo APK de 12 774 811 bytes, el mismo emulador, y lo único que cambia es lo que quedó en el dispositivo:**

| `.__override__/x86_64` del aparato | Al abrir la app |
|---|---|
| 425 ensamblados, dejados por un `dotnet build -t:Run` anterior | **Arranca** (PID 17509) |
| borrado con `run-as … rm -rf`, sin tocar nada más | **SIGABRT** |
| instalación limpia tras `adb uninstall` | **SIGABRT** |

Y el de 81,3 MB arranca en instalación limpia con ese directorio **vacío** —0 entradas—: no lo necesita, porque lleva los ensamblados dentro.

Así que el APK de 12 MB **sí abre en algunas partes**: en cualquier aparato donde Fast Deployment haya dejado los ensamblados. Que es, exactamente, la máquina del squad que lo compiló.

### 2b. La prueba de entrega que no se puede engañar

Mirar el tamaño sigue costando un segundo y sigue valiendo la pena, pero no alcanza. Hay **dos** estados sucios que esconden el fallo, no uno: el `obj/` del anfitrión y el `.__override__` del aparato.

```bash
rm -rf obj bin                      # limpia el ANFITRION
dotnet build -f net10.0-android -c Debug -p:EmbedAssembliesIntoApk=true
ls -l bin/Debug/net10.0-android/*-Signed.apk     # ~81 MB, no ~12

adb uninstall com.companyname.breb.app           # limpia el APARATO  <-- el paso que faltaba
adb install -r bin/Debug/net10.0-android/com.companyname.breb.app-Signed.apk
adb shell monkey -p com.companyname.breb.app -c android.intent.category.LAUNCHER 1
adb shell pidof com.companyname.breb.app         # un numero = arranco
```

`rm -rf obj bin` limpia el primero; **`adb uninstall` limpia el segundo**, porque borra el directorio de datos y con él `.__override__`.

> Un aviso que se deriva de lo mismo: `adb uninstall` también borra `operaciones-pendientes.json`. Para probar la cola, desinstale **antes** de encolar, no después.

---

## 3. La app alcanza la API desde Android

Con la API corriendo en el anfitrión y la app en el emulador, pestaña **Saldo**:

```
5.000,00 UVB
OK · rol consulta · retenido 0.00 UVB
```

Esto confirma `10.0.2.2` como la dirección del anfitrión **vista desde el emulador**. En un teléfono físico no existe: hay que poner la IP de la máquina en la WiFi, y eso es lo que se hace en clase.

---

## 4. La cola local, medida en Android

Hasta ahora sólo se había medido en Windows, con procesos de consola. Esto es el aparato de verdad.

### 4a. Sin red, la operación se guarda

Con la API **detenida**, se toca `Transferir`. La pantalla:

> *«Sin conexión. La operación quedó guardada y se reenviará con la misma llave cuando vuelva la red.»*

Y aparecen los botones `Reenviar lo pendiente` y `Descartar lo pendiente`.

El archivo, leído con `run-as` desde el dispositivo:

```bash
adb shell run-as com.companyname.breb.app cat files/operaciones-pendientes.json
```

```json
[
  {
    "Llave": "817f087c-ddd3-4c42-91ce-8a430211b502",
    "CuentaId": "11111111-1111-1111-1111-111111111111",
    "MontoUVB": 100,
    "CreadaEn": "2026-10-31T19:31:12.3878624Z",
    "Intentos": 1
  }
]
```

Vive en `/data/user/0/com.companyname.breb.app/files/`, que es lo que `FileSystem.AppDataDirectory` devuelve en Android: la carpeta privada que el sistema respalda y **no** borra al limpiar caché.

### 4b. La app se cierra de verdad

```bash
adb shell am force-stop com.companyname.breb.app
adb shell pidof com.companyname.breb.app     # → vacío: el proceso murió
```

El archivo queda **idéntico**: misma llave, mismo `Intentos: 1`.

> `force-stop` no es cerrar con el botón de atrás ni deslizar la app de recientes: mata el proceso sin avisarle, sin darle oportunidad de guardar nada. Es el caso que la rúbrica pide.

### 4b-bis. `force-stop` no es lo que hace Android cuando le falta memoria

Esta también la objetó Copilot en el PR #67, y también se midió. `force-stop` hace **dos** cosas: mata el proceso **y** marca el paquete como detenido.

```bash
adb shell dumpsys package com.companyname.breb.app | grep -o 'stopped=[a-z]*'
```

| Momento | `stopped=` |
|---|---|
| App abierta | `false` |
| Después de `am force-stop` | **`true`** |
| Después de volver a abrirla a mano | `false` |
| Después de `am kill` | **`false`** |

Esa marca impide que el sistema reactive la app por su cuenta hasta que alguien la abra explícitamente. Cuando Android mata un proceso **por memoria** no la pone. El equivalente de verdad es `am kill`:

```bash
adb shell input keyevent KEYCODE_HOME     # al fondo primero
adb shell am kill com.companyname.breb.app
```

> `am kill` **no hace nada si la app está en primer plano** —medido: el PID 15333 siguió vivo—. Sólo mata procesos en segundo plano, que es justo lo que hace el sistema al recuperar memoria.

Y la cola aguanta las dos. Medido con la llave `a557bd01-eea5-4d8b-9b76-629e8fa357fe`: tras `am kill` el archivo quedó intacto con `Intentos: 1`, y al reabrir, la app mostró *«Quedaron 1 operación(es) de una sesión anterior»*.

Para el E4 conviene saber la diferencia: `force-stop` es la prueba **más dura** y la que pide la rúbrica; `am kill` es la **más parecida** a lo que pasa en un teléfono real con poca memoria. La cola pasa las dos.

### 4c. Al reabrir, la app lo dice

> *«Quedaron 1 operación(es) de una sesión anterior. Se reenvían solas cuando haya red.»*

Leyó el disco al aparecer la pantalla. No hubo que decirle nada.

### 4d. Vuelve la red y se reenvía con la misma llave

Se levanta la API y se toca `Reenviar lo pendiente`. La pantalla:

| Campo | Valor |
|---|---|
| Estado | `Esperando confirmación del banco destino…` |
| Llave | **`817f087c-ddd3-4c42-91ce-8a430211b502`** |
| Cola local | `Sin operaciones pendientes` |

La llave es **la misma** que se generó antes de matar la app. Y el archivo quedó en `[]`.

### 4e. Lo que llegó a la base de datos

```sql
SELECT "TransferenciaId","MontoUVB","Liberada" FROM "Retenciones";
```

```
           TransferenciaId            | MontoUVB | Liberada
--------------------------------------+----------+----------
 817f087c-ddd3-4c42-91ce-8a430211b502 |   100.00 | t
(1 row)
```

**Una sola retención**, con la llave que nació antes del cierre de la app. Y `Liberada = t`: la saga de la Semana 3 compensó por su timeout de 15 s, porque el banco destino nunca confirmó.

### Resumen del ciclo

| Paso | Qué pasó |
|---|---|
| 1 · Toca Transferir, sin red | Guardada en disco antes de la red |
| 2 · Falla el envío | Sigue en la cola, `Intentos: 1` |
| 3 · **`force-stop`** | El proceso muere · **el archivo sobrevive** |
| 4 · Reabre la app | *«Quedaron 1 operación(es) de una sesión anterior»* |
| 5 · Vuelve la red, reenvía | Misma llave `817f087c-…` |
| 6 · El servidor responde | Cola a `[]` |
| 7 · En la base | **Exactamente 1 retención** |
| 8 · 15 s después | `Liberada = t` — la saga compensó |

---

## 5. ⚠️ La trampa de medición de esta semana: el anfitrión, no la app

A mitad de las pruebas la app mostró *«Breb.App isn't responding»*, y después *«System UI isn't responding»*. La carga del emulador estaba en **20,41**.

La lectura fácil: la app es demasiado pesada para el emulador.

El dato que lo desmiente:

```
uso CPU del anfitrion: 100%
```

Había un `dotnet build -c Release` de Android corriendo en paralelo, con enlazado y AOT. El emulador no estaba lento por culpa de la app: **estaba sin CPU que repartir**. Al terminar la compilación, la carga bajó a `2,43` y la app respondió normal.

| Momento | Carga del emulador | CPU del anfitrión |
|---|---|---|
| Durante el build de Release | **20,41** | **100 %** |
| Con la CPU libre | **2,43** | normal |

> Es la quinta vez este semestre, y el patrón ya no cambia: **antes de culpar a lo medido, mire qué más está corriendo en la máquina que mide.** El navegador de la Semana 11, el `uiautomator dump` de 3 792 ms de la 12, el proceso zombi del 26 de octubre, el `Invoke-WebRequest` del 28, y hoy un compilador comiéndose la CPU.

Recomendación práctica para el 4 de noviembre: **no compilar mientras se demuestra.** El emulador y el build no caben juntos.

---

## 6. Reproducir esto

```bash
# 1. APK que SI funciona instalado a mano
cd src/Breb.App
rm -rf obj bin                                   # limpia el ANFITRION
dotnet build -f net10.0-android -c Debug -p:EmbedAssembliesIntoApk=true
ls -l bin/Debug/net10.0-android/*-Signed.apk     # ~81 MB, no ~12

# 2. Instalar LIMPIO y lanzar
adb uninstall com.companyname.breb.app           # limpia el APARATO
adb install -r bin/Debug/net10.0-android/com.companyname.breb.app-Signed.apk
adb shell monkey -p com.companyname.breb.app -c android.intent.category.LAUNCHER 1
adb shell pidof com.companyname.breb.app         # un numero = arranco

# 3. Ver la cola del dispositivo (solo en builds de Debug)
adb shell run-as com.companyname.breb.app cat files/operaciones-pendientes.json

# 4a. Cerrar la app DE VERDAD: la prueba mas dura
adb shell am force-stop com.companyname.breb.app

# 4b. Lo que hace Android cuando le falta memoria
adb shell input keyevent KEYCODE_HOME            # am kill no toca el primer plano
adb shell am kill com.companyname.breb.app
adb shell dumpsys package com.companyname.breb.app | grep -o 'stopped=[a-z]*'
```

Y para ver la trampa del aparato, el experimento de una sola variable:

```bash
# el mismo APK de 12 MB, y solo cambia el dispositivo
dotnet build -f net10.0-android -c Debug -t:Run     # Fast Deployment llena .__override__
adb shell run-as com.companyname.breb.app ls 'files/.__override__/x86_64' | wc -l   # 425
                                                    # -> la app de 12 MB ARRANCA

adb shell am force-stop com.companyname.breb.app
adb shell run-as com.companyname.breb.app rm -rf 'files/.__override__'
adb shell monkey -p com.companyname.breb.app -c android.intent.category.LAUNCHER 1
adb logcat -d | grep "No assemblies found"          # -> el MISMO APK aborta
```

Y si algo va lento, antes de culpar al código:

```bash
adb shell cat /proc/loadavg
```
