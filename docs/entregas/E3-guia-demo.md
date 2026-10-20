# Evaluable 3 — Cómo mostrar la app

Esta guía es para el día de la sustentación (miércoles 21 de octubre de 2026). Explica cómo encender todo, con qué usuarios probar y qué tocar en la pantalla para cubrir cada punto de la nota.

La aplicación es la del celular (`src/Breb.App`). Habla con la API de cuentas (`src/Breb.Cuentas`). Tiene tres pantallas, ni una más: **Entrar**, **Saldo** y **Transferir**. No hay barra de pestañas. Al abrir se ve solo Entrar. Saldo y Transferir aparecen con los botones, después de iniciar sesión.

---

## 1. Qué tiene que estar instalado

En el computador de la demo:

- Docker Desktop (enciéndalo y espere a que diga que está corriendo).
- El SDK de .NET 8 (la API) y la carga de trabajo de .NET MAUI para Android (la app).
- Un emulador de Android ya creado y encendido. En Visual Studio: menú Herramientas → Android → Administrador de dispositivos Android.

Clone el repositorio en una **ruta corta**, por ejemplo `C:\breb`. Si la ruta es muy larga, la app compila con errores raros que no mencionan el largo del camino.

---

## 2. Orden para encender

Hágalo en este orden. Si salta un paso, el siguiente no tiene con qué hablar.

**Paso 1. La base de datos y la mensajería**

En la carpeta del repositorio:

```bash
docker compose up -d
```

Espere a que los dos contenedores digan `healthy` (la mensajería tarda cerca de 40 segundos):

```bash
docker compose ps
```

**Paso 2. Las tablas y las cuentas de prueba**

```bash
cd src/Breb.Cuentas
dotnet build
dotnet ef database update
cd ../..
docker exec -i breb-postgres psql -U postgres -d brebcuentas < scripts/cuentas-base.sql
```

**Paso 3. La API, escuchando para el emulador**

```bash
cd src/Breb.Cuentas
dotnet run --urls http://0.0.0.0:5080
```

Tiene que decir `0.0.0.0:5080`, no solo `localhost`. Deje esa ventana abierta.

Compruebe en el navegador del computador: `http://localhost:5080/swagger`. Si carga, la API está viva.

**Paso 4. El emulador**

Ábralo y espere a ver la pantalla de inicio de Android.

**Paso 5. La app**

Desde la carpeta del repositorio, en otra ventana:

```bash
dotnet build src/Breb.App/Breb.App.csproj -f net10.0-android -t:Run
```

La primera vez puede tardar varios minutos. Es normal.

La app ya apunta a `http://10.0.2.2:5080`. Dentro del emulador, `localhost` es el propio emulador, no su computador. `10.0.2.2` es la puerta hacia su computador. No cambie esa dirección para la demo en emulador.

---

## 3. Usuarios y cuentas de prueba

Estas claves están en el material del curso. Sirven solo en el laboratorio.

| Usuario | Clave | Para qué sirve |
|---|---|---|
| `luis.consulta` | `Consulta-2026` | Ver el saldo. Si intenta transferir, la API le dice que no puede. |
| `ana.operadora` | `Operadora-2026` | Ver el saldo **y** transferir. |
| `core-bancario` | `CoreBancario-2026` | Lo usa el banco destino, no la app. No hace falta en esta demo. |

La cuenta que muestran Saldo y Transferir es siempre la misma:

`11111111-1111-1111-1111-111111111111`

Empieza con 5.000 UVB disponibles y 0 retenidos, si acaba de cargar `scripts/cuentas-base.sql`. Al transferir, lo disponible **baja** y lo retenido **sube**: la plata queda apartada. Unos 15 segundos después, si nadie confirma, **vuelve sola**. El detalle está más abajo, en «Qué pasa con el dinero apartado».

Entrar abre con `luis.consulta` / `Consulta-2026`. Transferir abre con un monto de 10.

El token no se queda en la pantalla. Al pulsar **Entrar**, la app lo guarda en el almacén seguro del teléfono (SecureStorage). Saldo y Transferir lo leen de ahí. La clave no se guarda.

Si al abrir la app el token guardado todavía no venció (dura 15 minutos), entra sola a Saldo. Si venció, o no se puede leer, se queda en Entrar y borra lo que había. Si el almacén del teléfono falla, muestra el error y no se cierra.

No se puede saltar a Saldo o a Transferir sin sesión: esas pantallas no están en una barra. Si se llega sin token, la app vuelve a Entrar.

---

## 4. Recorrido de la demo

Hable mientras toca. Cada bloque de abajo corresponde a un punto de la rúbrica. El camino es Entrar → Saldo → Transferir, con botones. No hay pestañas abajo.

### La app compila y corre (20 %)

Muestre el emulador con la app abierta. La única pantalla es **Bre-B · Entrar**. No se ve una barra Entrar / Saldo / Transferir.

Si le piden el proyecto: se compiló con el comando del paso 5 y quedó instalada en el emulador.

### Autenticación con JWT (25 %)

1. Deje `luis.consulta` / `Consulta-2026` en **Entrar**.
2. Pulse **Entrar**.
3. Aparece un indicador de espera y el texto "Pidiendo token…".
4. Al terminar, el texto verde dice `Sesión iniciada · rol consulta` y la app abre **Saldo**.

Ese rol sale del token que acaba de emitir la API. La app no se lo inventa. El token quedó en el almacén seguro, no escrito en un campo de la pantalla.

Para mostrar un rechazo hace falta estar en Entrar. Desde Saldo, pulse **Cerrar sesión** (vuelve a Entrar y borra el token; el botón atrás de Android no regresa a Saldo). Cambie la clave por cualquier otra y pulse **Entrar**. En rojo: **Usuario o clave incorrectos.** Se queda en Entrar y no guarda nada.

### Consulta de saldo (parte del 25 %)

Sigue en **Saldo**, con la sesión de `luis.consulta` (arriba se lee el usuario y el rol).

1. Pulse **Consultar saldo**.
2. El saldo disponible se ve en grande (5.000 UVB si la cuenta está recién cargada) y debajo el retenido (0 UVB). El texto verde dice `OK · rol consulta`.

Esta pantalla no vuelve a pedir usuario ni clave. Usa el token que guardó Entrar.

### Disparo de transferencia (el resto del 25 %)

`luis.consulta` no puede transferir. Primero muéstrelo, y después entre con la operadora.

1. En Saldo, pulse **Ir a transferir**. El monto puede quedarse en `10`. Pulse **Transferir**.
2. En rojo, el texto es exactamente **No tienes permisos para transferir, hace falta el rol operador**. Nada más: ni código HTTP ni el nombre del usuario. La API contestó "prohibido" (403): sabe quién es, y no lo deja mover plata. La sesión sigue abierta. Volver a entrar como `luis.consulta` no lo arregla.
3. Pulse **Cerrar sesión**. Vuelve a Entrar y el token queda borrado. El atrás del teléfono no regresa a Saldo ni a Transferir.
4. Ponga `ana.operadora` / `Operadora-2026` y pulse **Entrar**. La app abre Saldo. Arriba se lee el usuario y el rol `operador`.
5. Pulse **Consultar saldo** y anote el disponible.
6. Pulse **Ir a transferir**. Deje el monto en `10` y pulse **Transferir**.
7. La pantalla confirma con la frase **Transferencia aceptada**, el monto y un número de transferencia. También avisa que, si el banco no confirma, el dinero vuelve en unos 15 segundos.
8. En esa misma pantalla el disponible baja 10 y el retenido sube 10. Muéstrelo enseguida: es la prueba de que la transferencia se disparó. **Volver a saldo** (o la flecha atrás) regresa a Saldo sin cerrar la sesión.

Eso es el disparo: la app llamó a la operación real de la API que aparta el dinero (`/cuentas/{id}/retener`) con el token de la operadora. No hay otra operación en el repositorio que inicie la transferencia, ni una que reciba una cuenta destino. La API inventa el número. Pulsar dos veces, cuando el botón ya volvió a encenderse, aparta el dinero dos veces. Mientras la llamada va en curso el botón está apagado, así que un doble toque rápido no dispara dos veces.

No espere a que el retenido se quede así. Siga leyendo antes de refrescar.

### Qué pasa con el dinero apartado

La app no manda la plata a otra cuenta. En este laboratorio no hay una cuenta destino: el otro lado es el banco de afuera.

Lo que hace **Transferir** es apartar el monto de la cuenta de la demo y poner en marcha el reloj de esa transferencia. La pantalla lo muestra al momento: disponible más bajo, retenido más alto.

El banco de afuera tiene su propio usuario, `core-bancario`. Confirmar que el dinero sí llegó es trabajo de ese usuario, no de Ana. La app no entra con esa clave a propósito: si la operadora pudiera dar por hecha la transferencia, podría completar una que el otro banco nunca recibió.

Si pasan unos **15 segundos** y nadie confirma, el sistema devuelve la plata solo. Lo retenido baja, lo disponible sube, y en la base la fila queda marcada como devuelta, no como enviada. En la ventana de la API el aviso dice `Timeout de confirmacion (15s)` y después aparece el `UPDATE` de `Liberada`. Ese reloj es la regla del curso: «si no llega confirmación en 15 segundos, se devuelve».

No es el proceso que revisa retenciones viejas. Ese otro proceso (el conciliador) solo toca plata que lleva **más de 5 minutos** apartada y cuya transferencia ni siquiera llegó a nacer. Una fila de 15 o 20 segundos no le corresponde, aunque el `UPDATE` de `Liberada` se parezca. Los 17 segundos que se ven entre la hora de creación y la hora de devolución son el reloj de 15 segundos más un par de segundos de mensajería.

Para mostrarlo en la sustentación:

1. Justo después de transferir, enseñe el retenido en **Transferir**.
2. Espere unos 20 segundos.
3. Pulse **Volver a saldo** y luego **Consultar saldo**. Sigue siendo `ana.operadora`: no hace falta volver a entrar.
4. El dinero volvió. Diga la frase: el banco no confirmó, así que el sistema lo regresó. No se perdió.

Si quiere el caso en el que el dinero **sí se va y no vuelve**, eso lo hace el banco, no la pantalla del celular. Desde la carpeta del repositorio: `./demo-clase3.sh feliz`. Ese recorrido aparta y, a los 5 segundos, confirma con `core-bancario`. Ahí el retenido vuelve a cero y el disponible no recupera ese monto.

### Arquitectura MVVM (20 %)

Si le piden el código, ábralo y señale sin ejecutar nada en la vista:

- `Vistas/LoginPage.xaml`, `SaldoPage.xaml` y `TransferenciaPage.xaml` solo describen textos y a qué dato se pegan. El code-behind asigna el ViewModel. Saldo y Transferir, al aparecer, piden al ViewModel que lea quién está en sesión. No hay lógica de negocio ahí.
- Cada pantalla tiene su ViewModel: `LoginViewModel`, `SaldoViewModel`, `TransferenciaViewModel`. Ahí están los comandos, el estado de carga y los mensajes.
- `Servicios/ClienteBrebApi.cs` es el único que conoce las rutas, el JSON y el encabezado Bearer. Se registra en `MauiProgram.cs` y llega al ViewModel por inyección.
- El token vive en `AlmacenSesion` / `BovedaSecureStorage` (SecureStorage), detrás de una interfaz. No es un campo del ViewModel.
- `AppShell` abre solo Entrar. Saldo y Transferir se empujan con `INavegacion` (para poder probarlo sin el celular). **Cerrar sesión** usa la ruta `//login`, que vacía la pila.

No hay asignaciones del estilo `etiqueta.Text = ...` en el code-behind.

### Errores de red visibles (10 %)

Haga estos, en el orden que le quede cómodo. En todos, el mensaje queda en la pantalla (en rojo) y los botones se pueden volver a pulsar al terminar. Mientras espera, el botón de esa pantalla se apaga y se ve el indicador de carga. La pantalla no se congela.

| Qué hacer | Qué debe decir la pantalla |
|---|---|
| Con la API corriendo, monto `999999`, sesión de `ana.operadora`, en **Transferir** pulsar **Transferir** | **No se pudo transferir: Saldo insuficiente para retener.** |
| Detener la API (Ctrl+C en su ventana) y en **Saldo** pulsar **Consultar saldo** | **No hay conexión con la API…** y menciona `10.0.2.2`. |
| Volver a encender la API. Entrar con `ana.operadora`. Esperar **más de 15 minutos** y pulsar **Consultar saldo** o **Transferir** | **La sesión venció o el token ya no es válido…** El token se borra y la app vuelve a **Entrar**. El atrás no regresa a Saldo. |
| (Si no alcanza el tiempo de los 15 minutos, diga que el token dura 15 minutos y que ese mensaje ya está cubierto por la prueba automática del ViewModel. No invente que lo vio si no lo vio.) | |
| Si la API responde con un error interno (500) | **La API falló del lado del servidor (HTTP 500)…** Es distinto del saldo insuficiente. |

El de "tardó demasiado" aparece si la API se queda callada más de 10 segundos: **La API no respondió a tiempo…** No hace falta provocarlo si el tiempo de la sustentación es corto; el texto está en la app.

**Cerrar sesión** (en Saldo o en Transferir) borra el token del almacén, vuelve a **Entrar** y limpia la pila. El botón atrás de Android no deja ver Saldo otra vez.

---

## 5. Cómo generar un APK que sí se instala

El APK que queda al compilar en modo depuración **no abre** si lo pasa a otra máquina: le faltan los archivos de la app a propósito, para que Visual Studio los copie aparte. Eso se llama Fast Deployment.

Para un APK que se instala solo (memoria USB, `adb install`, otro computador):

```bash
dotnet build src/Breb.App/Breb.App.csproj -f net10.0-android -c Debug -p:EmbedAssembliesIntoApk=true
```

El archivo queda en:

`src/Breb.App/bin/Debug/net10.0-android/com.companyname.breb.app-Signed.apk`

Ese es el que se entrega. Hay otro al lado, `com.companyname.breb.app.apk`, sin la firma final: use el que dice `Signed`.

Ojo: ese APK de depuración acepta HTTP sin cifrar para poder hablar con la API del laboratorio. Un APK de Release no lo hace, y contra `http://10.0.2.2:5080` no conectaría.

En un teléfono real, `10.0.2.2` no sirve. Ahí se usa la IP del computador en el WiFi. Para esta sustentación basta el emulador.

---

## 6. Si algo no enciende

| Lo que se ve | Qué revisar |
|---|---|
| La app dice que no hay conexión | La API está en la ventana del paso 3, con `0.0.0.0:5080`. El emulador está encendido. No cambió la dirección dentro de la app. |
| Swagger no abre | `docker compose ps` muestra los dos contenedores en `healthy`. Volvió a correr el paso 3 desde `src/Breb.Cuentas`. |
| Consultar saldo dice que la cuenta no existe | Faltó el comando de `scripts/cuentas-base.sql` del paso 2. |
| La app vuelve sola a Entrar al abrir Saldo o Transferir | No hay token, o el que había ya venció. Pulse **Entrar** de nuevo. |
| Transferir dice que no tiene permiso con la operadora | La sesión sigue siendo la de `luis.consulta`. **Cerrar sesión**, entre con `ana.operadora` y pulse **Ir a transferir**. |
| Al entrar, un mensaje del almacén seguro | El emulador a veces exige un PIN de prueba para cifrar. Póngalo y vuelva a pulsar **Entrar**. La app no se cierra por eso. |
| La app se cierra al abrir un APK copiado a mano | Lo generó sin `-p:EmbedAssembliesIntoApk=true`. Repita el comando de la sección 5. |

---

## 7. Preguntas probables del profesor

Respuestas cortas, con lo que hace este código.

**1. ¿Por qué la app llama a 10.0.2.2 y no a localhost?**

Dentro del emulador, `localhost` es el emulador, no el computador donde corre la API. Android reserva `10.0.2.2` para el computador anfitrión. Por eso `MauiProgram.cs` pone `http://10.0.2.2:5080`. La API tiene que escuchar en `0.0.0.0:5080`, no solo en localhost. En un teléfono real esa dirección no sirve: se usa la IP del computador en el WiFi.

**2. ¿Dónde se guarda el token, y por qué no en una variable?**

En SecureStorage de MAUI, a través de `BovedaSecureStorage`. `AlmacenSesion` escribe tres claves: `breb.token`, `breb.rol` y `breb.usuario`. La clave de la persona no se guarda. En Android el valor queda cifrado con la clave del sistema. Las pantallas lo piden cuando lo necesitan; no hay un campo ni una variable global con el JWT. Se borra al pulsar **Cerrar sesión** y cuando Saldo o Transferir reciben un 401. Si el emulador no puede cifrar (a veces pide un PIN), la excepción se muestra en pantalla y la app sigue abierta. Las pruebas usan un almacén falso, no el del teléfono.

**3. ¿Qué es MVVM en esta app?**

La vista (el XAML) solo muestra datos y botones. El ViewModel tiene el texto, el color del mensaje y los comandos. El servicio `ClienteBrebApi` es el único que hace HTTP. Hay un ViewModel por pantalla: entrar, saldo y transferir. El code-behind no arma el saldo ni decide el error: asigna el ViewModel y, al aparecer Saldo o Transferir, le pide que lea la sesión.

**4. ¿Qué pasa si pulso Transferir dos veces?**

Mientras la llamada va en curso, `Cargando` es verdadero y el botón está apagado (`CanExecute`). Un doble toque rápido no manda dos peticiones. Cuando el botón vuelve a encenderse, un segundo toque sí aparta otra vez: la API no tiene clave de idempotencia y cada `POST /retener` crea otra retención.

**5. ¿Por qué el dinero vuelve a los 15 segundos?**

Transferir solo aparta la plata y arranca la saga. Si nadie llama a confirmar el abono, a los 15 segundos la saga publica la compensación (`Timeout de confirmacion (15s)`) y el consumidor marca la retención como liberada. Por eso el disponible sube y el retenido baja. No es el conciliador: ese solo mira retenciones de más de 5 minutos que se quedaron sin saga. Los ~17 segundos que se ven en la base son el reloj de 15 más la mensajería.

**6. ¿Por qué el APK de Debug no abre si lo copio a otro lado?**

En Debug, .NET para Android usa Fast Deployment: los ensamblados no van dentro del APK. Los copia `dotnet build -t:Run`. El APK suelto de `bin/Debug` se cierra al abrirlo. Para uno que sí se instala solo: `dotnet build src/Breb.App/Breb.App.csproj -f net10.0-android -c Debug -p:EmbedAssembliesIntoApk=true`. El archivo es `com.companyname.breb.app-Signed.apk`.

**7. ¿En qué se diferencia un 401 de un 403?**

401: no está autenticado. Clave mala en **Entrar**, o el token ya no sirve (venció a los 15 minutos). En Saldo y Transferir, un 401 borra la sesión y vuelve a **Entrar** con la pila vacía. 403: sí está autenticado, pero el rol no alcanza. `luis.consulta` puede ver el saldo y no puede retener, porque esa ruta exige el rol `operador`. Entrar otra vez como Luis no lo arregla: hay que **Cerrar sesión** y entrar como `ana.operadora`.

**8. ¿Por qué el HTTP sin cifrar solo está en Debug?**

La API del laboratorio es `http://`, sin TLS. Desde Android 8 el sistema bloquea ese tráfico en las apps. `MainApplication.cs` pone `UsesCleartextTraffic = true` solo dentro de `#if DEBUG`. En Release la app no hablaría con `http://10.0.2.2:5080`. Que el navegador del emulador abra la URL no demuestra que la app pueda: el navegador tiene otra política de red.

**9. ¿Por qué hay tres pantallas y no una?**

El Evaluable 3 pide exactamente tres: Entrar (pide el JWT), Saldo (disponible y retenido) y Transferir (dispara y muestra el resultado). No hay una cuarta ni una barra para saltar de una a otra. Al abrir solo está Entrar. Un login correcto empuja Saldo. **Ir a transferir** empuja Transferir, y **Volver a saldo** (o la flecha atrás) regresa. **Cerrar sesión** borra el token y vuelve a Entrar vaciando la pila, para que el atrás de Android no reabra Saldo. Si Saldo o Transferir aparecen sin un token vigente, vuelven a Entrar. Si al abrir ya hay un token que no venció, la app entra sola a Saldo.

**10. ¿Por qué la app no confirma el abono?**

Confirmar es `POST /transferencias/{id}/confirmar-abono` y exige el rol `banco` (`core-bancario`). Ese usuario es el banco destino. Si la operadora pudiera confirmar, daría por hecha una transferencia que el otro banco nunca recibió. El caso en el que el dinero sí se va y no vuelve está en `./demo-clase3.sh feliz`, no en el celular.

---

## 8. Guion de 2 minutos (video o demo en vivo)

Hable mirando el emulador. La API ya está en `0.0.0.0:5080` y la app ya está instalada. No explique Docker en estos dos minutos.

**0:00 — Entrar.** "No hay pestañas: al abrir solo está Entrar. No se puede ir a Saldo sin sesión. Entro con luis.consulta. La API me devuelve un JWT de 15 minutos. No lo dejo en una variable: queda cifrado en el almacén seguro del teléfono." Pulse **Entrar**. Muestre el salto a Saldo.

**0:25 — Saldo.** "Esta pantalla no pide la clave otra vez. Usa el token guardado." Pulse **Consultar saldo**. "Acá está lo disponible y lo retenido. Si la API estuviera apagada, vería un mensaje y la pantalla no se congelaría."

**0:45 — 403.** Pulse **Ir a transferir** y luego **Transferir**. "Luis sí está autenticado, pero transferir exige el rol operador. Esto es un 403, no un 401. La sesión sigue. Un 401 borra el token y vuelve a Entrar."

**1:05 — Operadora.** Pulse **Cerrar sesión**. "El token se borra y la pila queda vacía: el atrás del teléfono no vuelve a Saldo." Ponga `ana.operadora` / `Operadora-2026` y pulse **Entrar**.

**1:25 — Transferir.** Pulse **Ir a transferir**, monto 10, **Transferir**. "Mientras espera, el botón está apagado. La API apartó 10 UVB. Si el banco no confirma, a los 15 segundos la saga devuelve la plata sola. **Volver a saldo** regresa sin cerrar la sesión."

**1:50 — Cierre.** "Tres pantallas en fila, un servicio para el HTTP, el token en SecureStorage, y los errores se leen en la misma pantalla."
