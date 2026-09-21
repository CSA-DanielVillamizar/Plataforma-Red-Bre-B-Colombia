# Clase 9 — Actividad por squads: resuelta

## Seguridad II: autorización, secretos y la puerta que el token no cuida · Red Bre-B Colombia (190304014-1)

Así debió quedar cada encargo. **Todo lo que aparece como medido se ejecutó el 21 de septiembre de 2026** contra la API de `main` (PR #44), con PostgreSQL 5433 y RabbitMQ 5673. Las herramientas están en `scripts/actividad-clase9/`.

---

## 1 · Fintech & Core — darse cuenta de que alguien entró igual

> **Encargo:** el ataque completó una transferencia sin que el banco destino la confirmara. Aunque el broker quede perfecto, ¿cómo detectaría el sistema que una transferencia "completada" nunca llegó? Diseñen el mecanismo y digan cada cuánto corre.

### Antes de diseñar: hoy no hay nada que conciliar

Medido en la base del laboratorio. Retenciones que no se liberaron y que ya no tienen saga:

| TransferenciaId | Monto | Qué es en realidad |
|---|---|---|
| `9ced8a84…` | 100 | Camino feliz legítimo (demo) |
| `8ebfe611…` · `6dd1fcae…` | 100 · 100 | **Las dos transferencias A del ataque al broker** |
| `d8b34533…` | 1 | **La retención huérfana**: la saga nunca nació |
| … 8 más | 1 – 100 | Completadas de la matriz, las demos y las cargas… **o eso creemos**. Dos son del 15 de septiembre y no hay forma de saber cuál fue su destino |

**Las 12 filas son indistinguibles.** Tienen las mismas columnas (`TransferenciaId`, `CuentaId`, `MontoUVB`, `CreadaEn`, `Liberada = false`, `LiberadaEn = null`). Una transferencia bien completada, una completada con una confirmación falsa y una retención atrapada se ven exactamente igual.

Y hay tres agujeros más, también medidos:

| Hallazgo | Evidencia |
|---|---|
| **El evento de cierre se publica al vacío** | El exchange `Breb.Cuentas.Contratos:TransferenciaCompletada` tiene **0 bindings**: nadie lo escucha |
| **La saga borra su rastro** | `SetCompletedWhenFinalized()` elimina la fila al completar |
| **La confirmación no trae evidencia** | `AbonoConfirmado` solo tiene `TransferenciaId` y `ConfirmadoEn`. Ninguna referencia del banco destino que se pueda verificar |

> **La primera respuesta correcta no es un mecanismo de detección: es dejar rastro.** No se puede conciliar lo que no quedó escrito.

### El mecanismo, en tres piezas

**Pieza 1 — Que la confirmación traiga algo verificable.**

```csharp
public record AbonoConfirmado
{
    public Guid TransferenciaId { get; init; }
    public DateTime ConfirmadoEn { get; init; }
    public string BancoDestino { get; init; } = "";       // nuevo
    public string ReferenciaAbono { get; init; } = "";    // nuevo: id del abono EN EL CORE DEL BANCO DESTINO
}
```

**Pieza 2 — Que la completación quede escrita.** La retención deja de ser "liberada o no" y pasa a tener un estado:

| Estado | Significa |
|---|---|
| `Retenida` | En tránsito |
| `Liberada` | Compensada: el dinero volvió |
| `Debitada` | **Completada**, con `ReferenciaAbono`, `BancoDestino` y `ConfirmadaEn` |
| `EnDisputa` | La conciliación no encontró el abono |

Con eso, las 12 filas de arriba se separarían solas: las completadas legítimas quedarían `Debitada` con una referencia que el banco reconoce, las dos del ataque `Debitada` con una referencia que no existe —o sin ninguna—, y la huérfana `Retenida` sin saga.

**Pieza 3 — Verificar contra alguien que el atacante no controla.** Este es el punto central: **si el sistema se revisa a sí mismo, el ataque es invisible**, porque el atacante escribió en nuestro propio broker. La verificación tiene que ir contra una fuente independiente.

| Ciclo | Qué verifica | Contra quién | Cada cuánto |
|---|---|---|---|
| **A · Verificación por evento** | Cada transferencia `Debitada`: ¿existe el abono `ReferenciaAbono` por ese monto? | API de consulta del **banco destino** | **Un consumidor de `TransferenciaCompletada`, 1–5 min después** de cada completación |
| **B · Conciliación de ciclo** | Suma de `Debitada` por banco contra lo que el **MOL** liquidó | Archivo de liquidación del MOL | **Cada ciclo de liquidación**, al menos diario |
| **C · Huérfanas** (Semana 6) | Retenciones `Retenida` sin saga hace más de 1 hora | Nuestra base | **Cada 5 minutos** |

Una discrepancia en A o B pasa la retención a `EnDisputa` y dispara una alerta. **No se revierte sola**: revertir automáticamente una transferencia que el banco sí recibió crea el error opuesto.

### Por qué esas frecuencias

La frecuencia es la **ventana de exposición**: el tiempo que un fraude pasa sin que nadie lo vea.

- **A corre por evento y con retraso**, no en el momento: la consulta al banco no puede frenar la transferencia (el SLA de Bre-B es de 20 s) y el abono puede tardar en aparecer en el core destino. De 1 a 5 minutos es la ventana razonable.
- **B atrapa lo que A no vio**: el banco destino estaba caído, respondió mal, o alguien falsificó también la respuesta de su API. Corre al ritmo en que el MOL entrega su verdad.
- **C no es de fraude, es del hallazgo de la Clase 9**: la retención huérfana de 1 UVB que dejó un 40001 en `FondosRetenidos`.

### Qué cuesta

| Costo | Tamaño |
|---|---|
| Una llamada extra al banco destino **por transferencia** | Asíncrona: no toca el SLA de 20 s |
| Cambio de contrato de `AbonoConfirmado` | Todos los que lo publican y consumen se actualizan juntos |
| Una tabla o columna de estado y una migración | Bajo |
| Un proceso de disputas (humano) | El costo real: alguien tiene que resolverlas |

> **Error típico:** "comparar `TransferenciaSagas` contra `Retenciones`". Es el sistema revisándose a sí mismo: el atacante que publicó en el broker ya dejó las dos tablas consistentes entre sí.
>
> **Pregunta de profundización:** ¿y si el atacante también inventa una `ReferenciaAbono`? Por eso el ciclo A **consulta al banco**. La referencia no se cree: se verifica.

---

## 2 · Datos, Tuning y Observabilidad — el registro de auditoría

> **Encargo:** diseñen el registro de auditoría de la autorización: qué campos se guardan de cada 401 y 403, qué nunca se guarda, y dos alertas que dispararían con esos datos.

### Primero: qué registra hoy la API — medido

`scripts/actividad-clase9/auditoria-log.py` dispara siete peticiones que fallan y lee lo que cada una dejó en el log:

| Petición | Lo que quedó en el log |
|---|---|
| 401 sin token | URL, `Authorization failed…(operador)`, `Bearer was challenged` |
| 401 token vencido | Lo anterior + **`IDX10223: Lifetime validation failed`** con la hora de vencimiento |
| 401 firma de otra clave | Lo anterior + **`IDX10517: Signature validation failed. The token's kid is missing`** |
| 401 `alg: none` | Lo anterior + **`IDX10504: token does not have a signature`** |
| **403** consulta retiene | URL, `Authorization failed…(operador)`, `Bearer was forbidden` — **sin decir quién** |
| **401 `/token` contraseña equivocada** | `Setting HTTP status code 401` — **sin usuario ni motivo** |
| **401 `/token` usuario inexistente** | Idéntico al anterior |

Y buscando en las 3 515 líneas del log:

| Búsqueda | Apariciones |
|---|---|
| `eyJ` (el inicio de cualquier JWT) | **0** |
| La contraseña equivocada que se envió | **0** |
| `Consulta-2026` | **0** |
| Los nombres de usuario intentados | **0** |

**Lo bueno:** nada sensible se filtra. .NET oculta el token por defecto (`[PII of type 'JsonWebToken' is hidden]`).

**Lo malo:**

1. **Un 403 no dice quién.** Se sabe que alguien sin rol operador intentó retener, pero no quién.
2. **Un login fallido no deja nada útil.** Ni el usuario ni el motivo. **La fuerza bruta contra `/token` hoy es invisible.**
3. **Es texto libre mezclado con todo lo demás**: consultas SQL, MassTransit, arranque. No se puede contar ni alertar sobre él sin parsear frases en inglés.

### El registro: un evento estructurado por decisión

Un evento JSON por cada 401, cada 403 y **cada** intento en `/token`, en un flujo propio —no mezclado con el log de la aplicación— y de solo agregar:

| Campo | Ejemplo | Por qué |
|---|---|---|
| `ts` | `2026-09-21T18:55:57.412Z` | UTC, siempre |
| `resultado` | `401` · `403` · `token_emitido` | |
| `motivo` | `sin_token` · `vencido` · `firma_invalida` · `sin_firma` · `audiencia` · `rol_insuficiente` · `credenciales` | Enumerado, no texto: se puede contar |
| `ruta` | `POST /cuentas/{cuentaId}/retener` | La **plantilla**, no la URL: agrupa |
| `recurso` | `11111111-…` | Qué cuenta se intentó tocar |
| `sub` · `rol` | `luis.consulta` · `consulta` | **Solo si el token fue validado.** De un token con firma inválida no se registra el `sub` como identidad: lo escribió el atacante |
| `jti` · `kid` | `6207f805…` · `9f9a7028` | Permite rastrear o revocar **ese** token sin guardarlo |
| `usuario_intentado` | `ana.operadora` o `h:3fa1c9…` | En `/token`: el nombre si existe en el directorio; si no, **solo un hash** |
| `usuario_existe` | `true` / `false` | **Interno**: al cliente se le oculta, a auditoría no |
| `ip` · `agente` | `10.0.4.17` · `python-urllib/3.11` | El agente, truncado |
| `traza` | `00-4bf92f35…-01` | Para cruzarlo con la traza distribuida (Clase 10) |

**¿Por qué el hash del usuario inexistente?** Porque es común que alguien escriba **la contraseña en el campo de usuario**. Guardar en claro los usuarios que no existen es guardar contraseñas.

### Lo que nunca se guarda

| Nunca | Por qué |
|---|---|
| El token completo, ni la cabecera `Authorization` | Es una credencial *bearer*: quien lo lee del log, entra |
| Ninguna contraseña, **tampoco la equivocada** | Suele ser la correcta con un error de tipeo, o la de otro sistema |
| El cuerpo de `/token` | Contiene la contraseña |
| Afirmaciones personales (cédula, nombre, saldo) | Datos protegidos (Ley 1581 de 2012) que la auditoría no necesita |
| La clave de firma, ni `ShowPII = true` en producción | Hoy .NET oculta el token: que siga así |

La IP también es dato personal: se guarda, pero con un **tiempo de retención definido**.

### Las alertas

**Alerta 1 — Fuerza bruta y relleno de credenciales:**

```
motivo = credenciales, en 5 minutos:
  ≥ 10 fallos para el mismo usuario_intentado         → ataque dirigido a una cuenta
  ≥ 30 fallos desde la misma ip con ≥ 10 usuarios     → relleno de credenciales (listas filtradas)
```

Hoy no se puede disparar: **el log no tiene ni el usuario ni el motivo** de un login fallido.

**Alerta 2 — Alguien está fabricando tokens:**

```
motivo ∈ {firma_invalida, sin_firma}  →  cualquier ocurrencia
```

En operación normal esto es **cero**: un cliente legítimo nunca manda un token con otra firma o sin firma. Una sola ocurrencia significa que alguien está probando, o que una rotación de clave salió mal. Las dos merecen que alguien se despierte.

**Una tercera, si sobra tiempo:** `≥ 3` respuestas 403 del mismo `sub` en rutas de dinero en 10 minutos → alguien explorando qué puede hacer con su rol.

> **Cómo se implementa en .NET:** `JwtBearerEvents.OnAuthenticationFailed` (motivo del 401), `OnChallenge` y `OnForbidden`, un `IAuthorizationMiddlewareResultHandler` para el 403 con el usuario, y un registro explícito en `/token`.
>
> **Error típico:** "guardar el token para poder investigar después". Es guardar la llave junto a la cerradura. Para investigar alcanza el `jti`.

---

## 3 · DevOps, Infra y Cloud — usuarios y permisos del broker

> **Encargo:** hoy todos los servicios usan `guest/guest`. Diseñen los usuarios y permisos de RabbitMQ para `Breb.Cuentas` y para un futuro servicio de notificaciones que solo lee `TransferenciaCompletada`. Y cómo llega esa contraseña a cada app.

### Cómo decide RabbitMQ

Tres expresiones regulares por usuario y *vhost*:

| Permiso | Controla |
|---|---|
| `configure` | Declarar y borrar colas y exchanges |
| `write` | Publicar en un exchange · enlazar una cola (*write* sobre la **cola**) |
| `read` | Consumir de una cola · enlazar (*read* sobre el **exchange**) |

### La topología real — medida

Lo que `Breb.Cuentas` declara en el broker:

| Exchanges | Colas |
|---|---|
| `Breb.Cuentas.Contratos:` + `FondosRetenidos` · `AbonoConfirmado` · `CompensarTransferencia` · `FondosReintegrados` · `TimeoutConfirmacion` · `TransferenciaCompletada` | `FondosRetenidos` |
| `FondosRetenidos` · `CompensarTransferencia` · `TransferenciaSagaState` | `CompensarTransferencia` |
| `TransferenciaSagaState_delay` (*x-delayed-message*) · `TransferenciaSagaState_error` | `TransferenciaSagaState` |
| `MassTransit:Fault` · `MassTransit:Fault--…FondosRetenidos--` | `TransferenciaSagaState_error` |

### Usuario `breb-cuentas` — verificado con la aplicación real

```
configure = write = read =
^(Breb\.Cuentas\..*|FondosRetenidos(_error|_skipped)?|CompensarTransferencia(_error|_skipped)?|TransferenciaSagaState(_delay|_error|_skipped)?|MassTransit:Fault.*)$
```

**Cómo se verificó:** una copia de la aplicación que lee usuario y contraseña del broker desde configuración, conectada como `breb-cuentas`:

| Prueba | Resultado |
|---|---|
| Arranque | `Bus started`, **0 `ACCESS_REFUSED`** |
| Usuario de la conexión según RabbitMQ | `breb-cuentas` |
| `demo-clase3.sh feliz` | Completada, 0 sagas |
| `demo-clase3.sh compensar` | Compensada, 100 UVB devueltos |
| Declarar la cola `notificaciones.transferencia-completada` | **Negado** |
| Declarar una cola cualquiera `auditoria.espia` | **Negado** |
| Declarar un exchange de otro contexto `Breb.Pagos.Contratos:Pago` | **Negado** |
| **Publicar `AbonoConfirmado`** | **Permitido** ⚠️ |

**La última fila es el límite honesto.** `breb-cuentas` **necesita** publicar `AbonoConfirmado`, porque `/confirmar-abono` vive dentro de `Breb.Cuentas` (una simplificación del laboratorio). Si alguien roba **esa** contraseña, el ataque de la clase sigue funcionando. La solución de fondo es de arquitectura: `AbonoConfirmado` debería publicarlo **solo un adaptador del banco destino**, con su propio usuario, y entonces se quita de la regex de Cuentas. Los permisos solo pueden ser tan finos como la separación de responsabilidades.

**Un detalle que distingue una buena respuesta:** la regex funciona porque los contratos siguen una convención de nombres (`Breb.Cuentas.Contratos:…`). **La convención de nombres es una herramienta de seguridad**: sin ella, los permisos mínimos no se pueden escribir.

### Usuario `breb-notificaciones` — verificado, 8 de 8

```
configure = ^notificaciones\..*
write     = ^notificaciones\..*
read      = ^(notificaciones\..*|Breb\.Cuentas\.Contratos:TransferenciaCompletada)$
```

| Operación como `breb-notificaciones` | Esperado | Obtenido |
|---|---|---|
| Declarar su cola `notificaciones.transferencia-completada` | Permitido | **201** |
| Enlazarla a `TransferenciaCompletada` | Permitido | **201** |
| Enlazarla a `AbonoConfirmado` (espiar) | Negado | **401** |
| Publicar `AbonoConfirmado` (el ataque) | Negado | **403 ACCESS_REFUSED** |
| Leer la cola de la saga | Negado | **403 ACCESS_REFUSED** |
| Declarar una cola ajena | Negado | **401** |
| Borrar el exchange `AbonoConfirmado` | Negado | **401** |
| Recibir el `TransferenciaCompletada` de una transferencia real | Permitido | **Recibido** |

De paso, este servicio sería **el primer binding** que tiene `TransferenciaCompletada`: hoy nadie lo escucha (ver squad Fintech).

### Cómo llega la contraseña a cada app

El mismo patrón de la clave JWT. Medido en la copia: **son dos líneas** de `Program.cs`:

```csharp
h.Username(builder.Configuration["RabbitMq:Usuario"] ?? "guest");
h.Password(builder.Configuration["RabbitMq:Clave"] ?? "guest");
```

| Ambiente | De dónde sale |
|---|---|
| Development | `guest/guest`: público, solo local |
| Cualquier otro | `RabbitMq__Usuario` / `RabbitMq__Clave` por variable de entorno o gestor de secretos, **una credencial distinta por servicio** — y fail-fast si falta, como la clave JWT |

Del lado del broker, los usuarios no se crean a mano: van en un archivo de **definiciones** (`load_definitions`) que el `docker-compose` monta al arrancar, con las contraseñas inyectadas desde fuera del repositorio. Y `guest` se elimina en cualquier ambiente que no sea el portátil.

> **Por qué este cambio no se hizo en el repo:** es el reto 2 de la Clase 9, y cambiar `guest/guest` antes del E1 rompe el entorno de todos los squads.

---

## 4 · Arquitectura, UI y QA — 1 000, 100 000 o 600 000 iteraciones

> **Encargo:** `/token` tarda ~190 ms por PBKDF2 con 100 000 iteraciones. OWASP recomienda 600 000. Alguien propone bajar a 1 000 "para que el login sea rápido". Evalúen las tres opciones con números y digan qué harían contra la fuerza bruta además de las iteraciones.

### Los números — medidos en .NET 8, Release

`Rfc2898DeriveBytes.Pbkdf2`, la misma llamada de `DirectorioUsuarios`. Mediana de 7, en un i7-7500U (2 núcleos, 4 hilos):

| Iteraciones | Un hash | Intentos por segundo, por núcleo |
|---|---|---|
| 1 000 | **1.51 ms** | 661 |
| 100 000 | **211 ms** | 4.7 |
| 600 000 | **1 619 ms** | 0.6 |

Y `/token` bajo concurrencia, con las 100 000 de hoy:

| Concurrencia | Logins/s | p50 | p95 |
|---|---|---|---|
| 1 | 4.3 | 225 ms | 323 ms |
| 4 | 14.9 | 249 ms | 353 ms |
| 8 | 15.1 | 467 ms | 580 ms |
| 16 | 12.2 | 1 225 ms | 1 487 ms |

**La API se satura en unos 15 logins por segundo**: los 4 hilos del procesador calculando PBKDF2. Más concurrencia solo agrega espera.

### Las tres opciones, del lado del atacante y del lado propio

Atacante que robó la tabla y prueba una lista de ~14 millones de contraseñas (el tamaño de la lista filtrada *rockyou*, la que usa cualquiera), **por usuario, en un núcleo**:

| Iteraciones | Atacante: recorrer la lista | Nosotros: un login | Nosotros: capacidad de `/token` en esta máquina |
|---|---|---|---|
| **1 000** | **~6 horas** | 1.5 ms | Cientos por segundo |
| **100 000** | **~34 días** | 211 ms | ~15/s (medido) |
| **600 000** | **~262 días** | 1.6 s | **~2/s** (estimado: 7.7× el costo de 100 000) |

Con tarjetas gráficas el atacante divide esos tiempos por mucho, pero **la proporción entre filas se mantiene**: cada iteración que quitamos se la regalamos.

### Evaluación

- **1 000 — no.** El login pasa de 211 a 1.5 ms, algo que ningún usuario nota porque ya era imperceptible, y la tabla robada se recorre **140 veces más rápido**. Es optimizar lo que no dolía y pagarlo en lo que sí.
- **100 000 — aceptable para el laboratorio**, por debajo de la recomendación.
- **600 000 — correcto, pero no en esta API.** A 1.6 s por hash, **dos logins por segundo saturan la máquina**: cualquiera tumba `/token` con un script. 600 000 exige que la emisión viva en un **servicio de identidad aparte**, con capacidad propia y limitador de tasa delante. La alternativa que OWASP pone primero es **Argon2id**, que además consume memoria y le quita ventaja a las tarjetas gráficas (en .NET requiere una biblioteca externa).

**Y un defecto de diseño que el encargo destapa:** en `DirectorioUsuarios` las iteraciones son una **constante**. Si mañana se suben a 600 000, ningún hash guardado se puede verificar. Lo correcto es guardar las iteraciones **con cada usuario** y re-calcular el hash con el valor nuevo **la próxima vez que ese usuario inicie sesión bien**. Así se migra sin pedirle a nadie que cambie su contraseña.

### Contra la fuerza bruta, además de las iteraciones

| Medida | Qué detiene | Cuidado con |
|---|---|---|
| **Limitador de tasa en `/token`**, por IP **y** por usuario | Fuerza bruta en línea y el tumbado de `/token` | Es el reto 1 de la clase |
| **Espera progresiva por usuario** (1 s, 2 s, 4 s…) | Ataque dirigido a una cuenta | **No** bloqueo permanente: sería una forma gratis de bloquearle la cuenta a otro |
| **Rechazar contraseñas filtradas** | Relleno de credenciales | Se consulta por k-anonimato: se envían solo los primeros 5 caracteres del SHA-1, nunca la contraseña |
| **Segundo factor** para `operador` y `banco` | Contraseña robada | Las identidades que mueven plata primero |
| **La alerta 1 del squad Datos** | Todo lo anterior, cuando falla | Hoy no se puede: el log no guarda el usuario intentado |

### Casos de prueba que agregarían

| Caso | Esperado |
|---|---|
| 11 contraseñas equivocadas seguidas del mismo usuario | El intento 11 se limita, no se evalúa |
| El usuario legítimo desde otra IP, durante ese bloqueo | Entra |
| Usuario inexistente contra usuario con contraseña equivocada | **Mismo tiempo** (medido en la Clase 9: 191 contra 189 ms) y misma respuesta |
| Hash guardado con 100 000; se configuran 600 000; el usuario entra | Entra, y su hash queda recalculado con 600 000 |

> **Error típico:** "subir a 600 000 y listo". Sin limitador de tasa, eso convierte el login en la forma más barata de tumbar la API.

---

## El cierre, completo

> "El de Fintech es el más importante del día. Todo lo que hicimos hoy son candados. Lo que ellos diseñan es cómo darse cuenta de que alguien entró igual.
>
> Y lo primero que encontraron es que hoy no podríamos: una transferencia completada, una falsificada y una atrapada **se ven idénticas** en la base, y el evento que anuncia que una transferencia terminó se publica a una cola vacía. Detectar exige dejar rastro, y verificar contra alguien que el atacante no controla.
>
> Los otros tres encargos convergen en lo mismo: Datos encontró que hoy un ataque de fuerza bruta no deja huella; DevOps demostró que los permisos mínimos funcionan con la aplicación real, pero que no alcanzan mientras el que confirma el abono sea el mismo servicio que retiene; y Arquitectura mostró que subir las iteraciones sin limitar la tasa cambia un riesgo por otro. Ninguna capa sola alcanza."

---

## Resumen de lo medido

| Afirmación | Evidencia | Confianza |
|---|---|---|
| Completadas, falsificadas y huérfanas son indistinguibles en la base | 12 filas con las mismas columnas y valores | **Alta** |
| `TransferenciaCompletada` no tiene suscriptores | 0 bindings en el exchange | **Alta** |
| El log no filtra tokens ni contraseñas | 0 apariciones de `eyJ`, contraseñas y usuarios | **Alta** |
| Un 403 no registra quién; un login fallido no registra usuario ni motivo | Salida del log por caso | **Alta** |
| `Breb.Cuentas` funciona con permisos mínimos | Copia de la app como `breb-cuentas`: demos completas, 0 `ACCESS_REFUSED` | **Alta** |
| Esos permisos todavía permiten publicar `AbonoConfirmado` | HTTP 200 `routed:true` | **Alta** |
| `breb-notificaciones` con permisos mínimos | 8 de 8 operaciones | **Alta** |
| PBKDF2 en .NET: 1 000 · 100 000 · 600 000 | 1.51 · 211 · 1 619 ms | **Alta** |
| `/token` se satura en ~15 logins/s | Concurrencia 1–16 | **Media** — un portátil de 4 hilos |
| 600 000 iteraciones → ~2 logins/s | Proporción del benchmark, no medido en la API | **Media** |
