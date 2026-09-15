# Clase 8 — Actividad por squads: resuelta

## Seguridad I: autenticación con JWT · Red Bre-B Colombia (190304014-1)

Así debió quedar cada encargo. **Todo lo que aparece como medido se ejecutó el 14 de septiembre de 2026** contra la API de `main` (PR #41). Las mediciones se reproducen con:

```bash
python actividad-clase8.py
```

---

## 1 · Fintech & Core — el operador despedido

> **Encargo:** un operador es despedido a las 10:00. Su token se emitió a las 9:58. ¿Hasta qué hora puede seguir reteniendo fondos? Propongan **una** forma de cortarlo antes y digan qué cuesta.

### La respuesta honesta: hasta las 10:12:59

El token dura 15 minutos: 9:58 + 15 = **10:13**. Con `ClockSkew = 0`, el corte es exacto. Medido con un token de 3 segundos de vida:

| Segundos antes del `exp` | Respuesta |
|---|---|
| 3 · 2 · 1 | **200** |
| 0 | **401** |
| −1 · −2 · −3 | **401** |

El token sirve **hasta el segundo anterior** a `exp` y se rechaza **en** `exp`. Si hubiéramos dejado el `ClockSkew` por defecto, el operador despedido habría tenido **hasta las 10:18**: cinco minutos más, regalados.

**Trece minutos de exposición.** Despedirlo en el sistema de nómina, borrarlo de la base de usuarios o "cerrarle la sesión" no cambia nada: **ninguno de esos lugares se consulta cuando llega el token.** Esa es exactamente la propiedad por la que se eligió JWT.

### Las formas de cortarlo antes, y lo que cuesta cada una

| Mitigación | Cómo corta | Qué cuesta |
|---|---|---|
| **Lista de revocados por `jti`** | Al despedirlo se anota el `jti` de su token; cada petición lo consulta | **Una consulta en cada petición**, a Redis o a la base. Y una decisión incómoda: si la lista no responde, ¿se deja pasar a todos o se bloquea a todos? |
| **"Revocado desde" por usuario** | Se guarda `sub → 10:00`; se rechaza todo token de ese usuario emitido antes | Lo mismo: una consulta por petición. **Y hoy no se puede:** nuestro token no trae `iat` (hora de emisión). Medido: sus campos son `sub`, rol, `jti`, `exp`, `iss`, `aud` |
| **Token de 5 min + token de renovación** | El token corto se renueva contra el servicio de identidad, que **sí** consulta si el usuario sigue activo | La consulta pasa de "cada petición" a "cada 5 minutos por usuario". El despedido conserva **hasta 5 minutos**, no 13. Triplica las emisiones |
| **Rotar la clave** | Todos los tokens del sistema dejan de servir | Saca a **todos** los usuarios para cortar a uno. Solo se justifica si la clave se filtró |

### Cuál es la respuesta buena

**La tercera, combinada con la primera para las operaciones que mueven plata.**

- El token corto con renovación es el estándar de la industria: pone el punto de control donde ya hay una consulta (la renovación) en vez de agregar una nueva.
- Para `/retener` —que mueve dinero— una lista de revocados es defendible: es pequeña (una entrada solo vive hasta el `exp` del token, **15 minutos como máximo**), y el costo de consultarla es mínimo comparado con la transacción, el `FOR UPDATE` y el Outbox que esa petición ya paga.

> **Error típico:** "al despedirlo se le cierra la sesión y listo". Cerrar sesión borra el token **del cliente que se porta bien**. El que se está yendo molesto guarda una copia.
>
> **Pregunta de profundización:** si la lista de revocados está caída, ¿qué hacen? Para una API de consulta: dejar pasar. Para `/retener`: rechazar. **La misma falla se resuelve distinto según lo que está en juego.**

---

## 2 · Datos, Tuning y Observabilidad — qué va en el token

> **Encargo:** clasifiquen qué puede ir en el token de un usuario Bre-B y qué nunca: cédula, id de cuenta, rol, nombre, saldo, banco, llave. Justifiquen cada una con "el token se lee sin clave".

### El criterio: tres preguntas

1. **¿Lo necesita la autorización?** Si ningún servicio decide nada con ese dato, no tiene nada que hacer en el token.
2. **¿Puede ser público?** El token se lee sin clave: en los registros del proxy, en las herramientas del navegador, en un pantallazo pegado en un chat de soporte.
3. **¿Puede cambiar en los próximos 15 minutos?** El token es una foto firmada. Si el dato cambia, el token sigue diciendo el valor viejo hasta que vence.

### La clasificación

| Dato | ¿En el token? | Por qué |
|---|---|---|
| **Rol** | ✅ **Sí** | Es para lo que existe el token: decidir qué puede hacer. Saber que alguien es "operador" no le sirve a un atacante |
| **Banco** | ⚠️ **Solo si autoriza** | Si un operador de NEQUI solo puede actuar sobre cuentas de NEQUI, el banco es parte de la autorización y va. Si solo es un dato del usuario final, no |
| **Nombre** | ❌ **No** | Dato personal que ninguna decisión de autorización usa. La pantalla lo pide a un endpoint `/perfil`, por HTTPS y con token |
| **Id de cuenta** | ❌ **No** | Un usuario puede tener varias cuentas, y **quién es dueño de qué** se verifica en el servidor contra la base. Ponerlo en el token invita a confiar en el cliente |
| **Cédula** | ❌ **Nunca** | Dato personal protegido por la Ley 1581 de 2012 (habeas data). Con cédula y nombre se suplanta a alguien en muchos trámites. Y el token **se lee sin clave** |
| **Llave** (celular, correo) | ❌ **Nunca** | Es exactamente lo que necesita un estafador para llamar o escribirle a la víctima. Y cambia: el usuario puede registrar otra llave |
| **Saldo** | ❌ **Nunca** | Doble motivo: es de lo más sensible que tiene un banco **y** es falso a los pocos segundos. Tras la primera transferencia, el token sigue diciendo el saldo viejo durante 15 minutos |

**Una nota sobre `sub`:** hoy vale `ana.gomez`. En producción debe ser un **identificador opaco** —un GUID—, no un nombre de usuario ni una cédula. Identifica sin revelar.

### Medido: lo que pesa meterlo todo

| Token | Tamaño |
|---|---|
| Actual (`sub`, rol, `jti`, `exp`, `iss`, `aud`) | **347** caracteres |
| Con cédula, cuenta, nombre, saldo, banco y llave | **568** caracteres (**+221, +64 %**) |

Esos 221 caracteres viajan **en cada petición**. Y la API acepta ese token sin protestar (**200**): la firma es válida, y la biblioteca no tiene forma de saber que adentro va algo que no debería. **Nadie los va a detener: la disciplina es del que emite.**

Con ese token, cualquiera lee sin ninguna clave `{'cedula': '1037612345', 'saldo': '4900.00', 'llave': '3001234567'}`.

> **Error típico:** "el nombre sí, para mostrarlo en la app sin hacer otra llamada". Ahorra una llamada y deja el nombre en cada registro de cada proxy por el que pase la petición.
>
> **Pregunta de profundización:** ¿y el `saldo` en un **ID token** —el que usa solo la app, no la API? Sigue estando mal: sigue siendo un dato que caduca en segundos. La app tiene que pedirlo cuando lo muestra.

---

## 3 · DevOps, Infra y Cloud — sacar la clave del código

> **Encargo:** la clave está en el repo. Diseñen cómo sale del código: dónde vive, cómo llega a la app, y cómo se cambia sin sacar a todos los usuarios de golpe.

### Primero: la clave actual ya está quemada

Está en `Autenticacion.cs` y en la historia de git. **Borrarla en un commit nuevo no la saca de los anteriores**, y reescribir la historia tampoco sirve: cualquiera que haya clonado ya la tiene. **Lo único que la neutraliza es dejar de aceptarla**, es decir, rotarla.

### Dónde vive

| Ambiente | Dónde | Por qué |
|---|---|---|
| Portátil del desarrollador | `dotnet user-secrets` | Queda en el perfil del usuario, **fuera** de la carpeta del repo. No hay forma de hacerle commit por error |
| Pruebas y producción | **Gestor de secretos** (Azure Key Vault, AWS Secrets Manager, HashiCorp Vault) | Acceso auditado, permisos por servicio, rotación y versiones |
| Mínimo aceptable | Variable de entorno `Jwt__ClaveFirma` | Mejor que el código; peor que un gestor: la ve cualquiera que liste los procesos o el despliegue |

**Y la clave no es una frase.** La de hoy es texto legible. La real son 32 bytes aleatorios o más (`RandomNumberGenerator.GetBytes(32)`), generados por el gestor, que ninguna persona necesita ver.

### Cómo llega a la app

```csharp
var clave = builder.Configuration["Jwt:ClaveFirma"]
    ?? throw new InvalidOperationException("Falta Jwt:ClaveFirma");

if (Encoding.UTF8.GetByteCount(clave) < 32)
    throw new InvalidOperationException("Jwt:ClaveFirma debe tener 256 bits o más");
```

`IConfiguration` ya lee, en orden, `appsettings.json`, los user-secrets (solo en `Development`), las variables de entorno y —con su proveedor— el gestor de secretos. **El código no cambia entre ambientes: cambia de dónde sale el valor.**

Dos detalles que distinguen una buena respuesta:

- **Fallar al arrancar**, no en la primera petición. Una API sin clave que arranca "bien" y falla al emitir el primer token es un incidente a las 3 a. m.
- **Para leer el gestor no hace falta otra clave.** En la nube se usa una **identidad administrada**: la plataforma le da identidad al servicio y el gestor le da permiso a esa identidad. Si hace falta una contraseña para leer la bóveda de contraseñas, el problema solo se movió de lugar.

### Cómo se cambia sin sacar a nadie: rotación en tres fases

Cada clave lleva un identificador (`kid`) que viaja en la cabecera del token. El validador acepta **varias claves a la vez** (`IssuerSigningKeys`). **Verificado en .NET 8 con `System.IdentityModel.Tokens.Jwt` 8.3.0:**

| Fase | El validador acepta | El emisor firma con | Token clave vieja | Token clave nueva |
|---|---|---|---|---|
| **0** — hoy | vieja | vieja | ✅ válido | ❌ `SignatureKeyNotFound` |
| **1** — convivencia | **vieja y nueva** | **nueva** | ✅ válido | ✅ válido |
| **2** — tras 15 min | nueva | nueva | ❌ `SignatureKeyNotFound` | ✅ válido |

- **La fase 1 dura lo mismo que el token: 15 minutos.** Pasado eso, ningún token firmado con la vieja sigue vigente, y retirarla no afecta a nadie. **Nadie tuvo que volver a iniciar sesión.**
- **Verificado también:** los tokens de hoy, que **no traen `kid`**, son válidos en la fase 1. Sin `kid`, el validador prueba todas las claves. La transición desde el estado actual funciona.
- **El orden importa con varias instancias.** Primero se despliega la clave nueva **en todos los validadores** y solo después se cambia el emisor. Al revés, un token recién firmado cae en una instancia que todavía no conoce la clave y recibe 401: es la fila "fase 0, token nueva" de la tabla.

> **La excepción:** si la clave se **filtró**, la fase 1 no existe. Durante esos 15 minutos el atacante también firmaría tokens válidos. Se retira de inmediato y **todos vuelven a iniciar sesión**. Ese es el precio, y se paga.
>
> **Pregunta de profundización:** con diez servicios validando, ¿los diez necesitan la clave? Con HS256 sí, y los diez pueden fabricar tokens. Con **RS256** los validadores solo tienen la clave **pública**, y la privada vive en un solo lugar. Es el reto 2 de la clase.

---

## 4 · Arquitectura, UI y QA — los casos de prueba 401 / 403

> **Encargo:** hoy `/retener` acepta cualquier token válido. Un usuario con rol `consulta` no debería poder retener. Escriban los casos de prueba: qué endpoint, qué rol, y si esperan 401 o 403 en cada uno.

### La regla que ordena todos los casos

- **401** — no sé quién eres: sin token, token vencido, firma inválida.
- **403** — sé quién eres, y **no puedes**: token válido con el rol equivocado.

**Un 403 exige un token válido.** Si el token está alterado, el resultado es 401 aunque el rol pedido sea el correcto, porque la autenticación falla primero.

### La matriz, ejecutada contra la API de hoy

| # | Endpoint | Quién | Esperado | **Hoy** |
|---|---|---|---|---|
| A1 | `POST /retener` | sin token | 401 | 401 ✅ |
| A2 | `POST /retener` | token vencido | 401 | 401 ✅ |
| A3 | `POST /retener` | `consulta` que se cambió a `operador` sin re-firmar | **401** | 401 ✅ |
| A4 | `POST /retener` | `operador` | 200 | 200 ✅ |
| A5 | `POST /retener` | `consulta` | **403** | **200** ❌ |
| A6 | `POST /confirmar-abono` | `operador` | 202 | 202 ✅ |
| A7 | `POST /confirmar-abono` | `consulta` | **403** | **202** ❌ |
| A8 | `GET /quien-soy` | `consulta` | 200 | 200 ✅ |
| A9 | `GET /quien-soy` | sin token | 401 | 401 ✅ |
| A10 | `POST /token` | pide el rol `administrador` | **rechazo** | **200** ❌ |

**3 de 10 fallan hoy.** Que fallen es justamente para lo que sirven: fijan el comportamiento correcto **antes** de implementarlo.

### Lo que la matriz destapa: el caso A10

A5 y A7 eran los esperados. **A10 es el hallazgo.** `/token` emite **el rol que el cliente le pide**. Aunque el miércoles se agregue `RequireRole("operador")` a `/retener`, un usuario `consulta` puede pedir:

```bash
curl -X POST "http://localhost:5080/token?usuario=luis&rol=operador"
```

…y obtener un token de operador, **firmado por nosotros y perfectamente válido**. A5 pasaría a dar 403 y el sistema seguiría abierto.

> **El rol lo asigna el servidor a partir de quién es el usuario, nunca el cliente.** Probar solo el lado que valida y olvidar el que emite es probar la mitad de la puerta.

### Cómo se escriben de verdad

La matriz corre hoy como script (`actividad-clase8.py`). En el proyecto se vuelve **una prueba de integración por fila**, con `WebApplicationFactory` y xUnit, que corre en cada PR:

```csharp
[Theory]
[InlineData("operador", HttpStatusCode.OK)]
[InlineData("consulta", HttpStatusCode.Forbidden)]
public async Task Retener_segun_rol(string rol, HttpStatusCode esperado)
{
    var cliente = _fabrica.CreateClient();
    cliente.DefaultRequestHeaders.Authorization =
        new("Bearer", EmitirTokenDePrueba(rol));

    var r = await cliente.PostAsync($"/cuentas/{CuentaDemo}/retener?montoUVB=1", null);

    Assert.Equal(esperado, r.StatusCode);
}
```

> **Error típico:** esperar **401** para `consulta` en `/retener`. El sistema sí sabe quién es; lo que no hace es dejarlo. Confundirlos rompe al cliente: ante un 401 la app pide iniciar sesión otra vez, y el usuario entra en un ciclo sin salida porque volver a entrar no le da el permiso.
>
> **Pregunta de profundización:** ¿qué debería responder `/retener` a un `operador` sobre una cuenta que **no le corresponde**? 403 filtra que la cuenta existe; **404** no. Es la misma decisión que toma GitHub con los repositorios privados.

---

## El cierre, completo

> "El de Fintech es el que más duele: la respuesta honesta es 'hasta las 10:13' —medido: el último segundo válido es 10:12:59—, y cualquier forma de cortarlo antes obliga a consultar algo en cada petición, que es justo lo que el JWT venía a evitar. La salida razonable es mover esa consulta a la renovación y dejarla por petición solo donde se mueve plata.
>
> El de Arquitectura es la puerta al miércoles. Escribieron diez casos, tres fallan, y uno de los tres —que `/token` deja escoger el rol— no lo arregla ninguna política de autorización. Eso es lo que hace una buena prueba: encuentra el problema que nadie estaba buscando."

---

## Resumen de lo medido

| Afirmación | Evidencia | Confianza |
|---|---|---|
| Un token de las 9:58 sirve hasta las 10:12:59 | Corte exacto en `exp` con `ClockSkew = 0` | **Alta** |
| El token no trae `iat` | Campos decodificados: `sub`, rol, `jti`, `exp`, `iss`, `aud` | **Alta** |
| Meter los 6 datos personales agrega 221 caracteres (+64 %) | 347 → 568 | **Alta** |
| La API acepta un token con datos sensibles sin protestar | 200 | **Alta** |
| La rotación en tres fases no saca a nadie | Consola .NET 8, tres fases | **Alta** |
| Los tokens sin `kid` sobreviven a la fase 1 | Validado con la clave vieja sin `kid` junto a la nueva | **Alta** |
| 3 de 10 casos de autorización fallan hoy (A5, A7, A10) | `actividad-clase8.py` | **Alta** |
