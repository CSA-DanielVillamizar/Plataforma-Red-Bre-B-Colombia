# Evaluable 3 — Cómo mostrar la app

Esta guía es para el día de la sustentación (miércoles 21 de octubre de 2026). Explica cómo encender todo, con qué usuarios probar y qué tocar en la pantalla para cubrir cada punto de la nota.

La aplicación es la del celular (`src/Breb.App`). Habla con la API de cuentas (`src/Breb.Cuentas`).

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

La cuenta que muestra la app es siempre la misma:

`11111111-1111-1111-1111-111111111111`

Empieza con 5.000 UVB disponibles y 0 retenidos, si acaba de cargar `scripts/cuentas-base.sql`. Al transferir, lo disponible **baja** y lo retenido **sube**: la plata queda apartada. Unos 15 segundos después, si nadie confirma, **vuelve sola**. El detalle está más abajo, en «Qué pasa con el dinero apartado».

La app abre con `luis.consulta` y un monto de 10.

---

## 4. Recorrido de la demo

Hable mientras toca. Cada bloque de abajo corresponde a un punto de la rúbrica.

### La app compila y corre (20 %)

Muestre el emulador con la app abierta: título **Bre-B · Cuenta**, campos de usuario y clave, botones **Consultar saldo** y **Transferir**.

Si le piden el proyecto: se compiló con el comando del paso 5 y quedó instalada en el emulador.

### Autenticación con JWT (25 %)

1. Deje `luis.consulta` / `Consulta-2026`.
2. Pulse **Consultar saldo**.
3. Aparece un indicador de espera y el texto "Pidiendo token…".
4. Al terminar, el saldo se ve en grande y el texto verde dice `OK · rol consulta`.

Ese rol sale del token que acaba de emitir la API. La app no se lo inventa.

Para mostrar un rechazo: cambie la clave por cualquier otra y pulse **Consultar saldo**. En rojo: **Usuario o clave incorrectos.** El saldo vuelve a la raya.

### Consulta de saldo y disparo de transferencia (25 %)

1. Cambie el usuario a `ana.operadora` y la clave a `Operadora-2026`.
2. Pulse **Consultar saldo**. El texto debe decir `rol operador`. Anote el saldo disponible.
3. Deje el monto en `10` y pulse **Transferir**.
4. La pantalla confirma con la frase **Transferencia aceptada**, el monto y un número de transferencia. También avisa que, si el banco no confirma, el dinero vuelve en unos 15 segundos.
5. En ese momento el saldo disponible baja 10 y el saldo retenido sube 10. Muéstrelo enseguida: es la prueba de que la transferencia se disparó.

Eso es el disparo: la app llamó a la operación real de la API que aparta el dinero (`/cuentas/{id}/retener`) con el token de la operadora. No hay otra operación en el repositorio que inicie la transferencia, ni una que reciba una cuenta destino. La API inventa el número. Pulsar dos veces aparta el dinero dos veces.

No espere a que el retenido se quede así. Siga leyendo antes de refrescar.

### Qué pasa con el dinero apartado

La app no manda la plata a otra cuenta. En este laboratorio no hay una cuenta destino: el otro lado es el banco de afuera.

Lo que hace **Transferir** es apartar el monto de la cuenta de la demo y poner en marcha el reloj de esa transferencia. La pantalla lo muestra al momento: disponible más bajo, retenido más alto.

El banco de afuera tiene su propio usuario, `core-bancario`. Confirmar que el dinero sí llegó es trabajo de ese usuario, no de Ana. La app no entra con esa clave a propósito: si la operadora pudiera dar por hecha la transferencia, podría completar una que el otro banco nunca recibió.

Si pasan unos **15 segundos** y nadie confirma, el sistema devuelve la plata solo. Lo retenido baja, lo disponible sube, y en la base la fila queda marcada como devuelta, no como enviada. En la ventana de la API el aviso dice `Timeout de confirmacion (15s)` y después aparece el `UPDATE` de `Liberada`. Ese reloj es la regla del curso: «si no llega confirmación en 15 segundos, se devuelve».

No es el proceso que revisa retenciones viejas. Ese otro proceso (el conciliador) solo toca plata que lleva **más de 5 minutos** apartada y cuya transferencia ni siquiera llegó a nacer. Una fila de 15 o 20 segundos no le corresponde, aunque el `UPDATE` de `Liberada` se parezca. Los 17 segundos que se ven entre la hora de creación y la hora de devolución son el reloj de 15 segundos más un par de segundos de mensajería.

Para mostrarlo en la sustentación:

1. Justo después de transferir, enseñe el retenido arriba.
2. Espere unos 20 segundos.
3. Pulse **Consultar saldo** otra vez, con `ana.operadora`.
4. El dinero volvió. Diga la frase: el banco no confirmó, así que el sistema lo regresó. No se perdió.

Si quiere el caso en el que el dinero **sí se va y no vuelve**, eso lo hace el banco, no la pantalla del celular. Desde la carpeta del repositorio: `./demo-clase3.sh feliz`. Ese recorrido aparta y, a los 5 segundos, confirma con `core-bancario`. Ahí el retenido vuelve a cero y el disponible no recupera ese monto.

Ahora el caso en el que **no** deja:

1. Vuelva a `luis.consulta` / `Consulta-2026`.
2. Pulse **Consultar saldo** (entra bien: puede ver).
3. Pulse **Transferir**.

En rojo, la pantalla explica que hace falta `ana.operadora`. La API contestó "prohibido" (403): sabe quién es, y no lo deja mover plata. Volver a escribir la misma clave no lo arregla.

### Arquitectura MVVM (20 %)

Si le piden el código, ábralo y señale sin ejecutar nada en la vista:

- `MainPage.xaml` solo describe textos y a qué dato se pegan. El code-behind (`MainPage.xaml.cs`) únicamente recibe el ViewModel y lo asigna.
- `ViewModels/SaldoViewModel.cs` tiene los comandos Consultar y Transferir, el estado de carga y los mensajes.
- `Servicios/ClienteBrebApi.cs` es el único que conoce las rutas, el JSON y el token. Se registra en `MauiProgram.cs` y llega al ViewModel por inyección.

No hay asignaciones del estilo `etiqueta.Text = ...` en el code-behind.

### Errores de red visibles (10 %)

Haga estos, en el orden que le quede cómodo. En todos, el mensaje queda en la pantalla (en rojo) y los botones se pueden volver a pulsar al terminar. Mientras espera, los dos botones se apagan y se ve el indicador de carga.

| Qué hacer | Qué debe decir la pantalla |
|---|---|
| Con la API corriendo, monto `999999`, usuario `ana.operadora`, pulsar **Transferir** | **No se pudo transferir: Saldo insuficiente para retener.** El saldo anterior no se borra. |
| Detener la API (Ctrl+C en su ventana) y pulsar **Consultar saldo** | **No hay conexión con la API…** y menciona `10.0.2.2`. |
| Volver a encender la API. Consultar con `ana.operadora`. Esperar **más de 15 minutos** sin consultar de nuevo y pulsar **Transferir** | **La sesión venció o el token ya no es válido…** Después, **Consultar saldo** y se puede transferir otra vez. |
| (Si no alcanza el tiempo de los 15 minutos, diga que el token dura 15 minutos y que ese mensaje ya está cubierto por la prueba automática del ViewModel. No invente que lo vio si no lo vio.) | |
| Si la API responde con un error interno (500) | **La API falló del lado del servidor (HTTP 500)…** Es distinto del saldo insuficiente. |

El de "tardó demasiado" aparece si la API se queda callada más de 10 segundos: **La API no respondió a tiempo…** No hace falta provocarlo si el tiempo de la sustentación es corto; el texto está en la app.

---

## 5. Cómo generar un APK que sí se instala

El APK que queda al compilar en modo depuración **no abre** si lo pasa a otra máquina: le faltan los archivos de la app a propósito, para que Visual Studio los copie aparte.

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
| Transferir con `ana.operadora` dice que no tiene permiso | La clave quedó mal escrita, o no pulsó consultar después de cambiar el usuario y el token viejo sigue siendo el de consulta. Pulse **Consultar saldo** con la operadora y luego **Transferir**. |
| La app se cierra al abrir un APK copiado a mano | Lo generó sin `-p:EmbedAssembliesIntoApk=true`. Repita el comando de la sección 5. |
