# Semana 8, segunda sesión — Autorización, secretos y el broker

**Medido el 16 de septiembre de 2026.** `Breb.Cuentas` en el 5080 (Development) y en el 5085 (Production, para arranque y rotación). PostgreSQL 5433, RabbitMQ 5673.

## Qué cambió

| Archivo | Cambio |
|---|---|
| `Seguridad/Autenticacion.cs` | Clave desde configuración; la app **no arranca** sin clave, con menos de 256 bits, o con la clave de desarrollo fuera de Development. Rotación con `Jwt:ClaveAnterior`. `kid` en la cabecera, `iat` en el cuerpo. Tres políticas |
| `Seguridad/DirectorioUsuarios.cs` | **Nuevo.** Usuarios con PBKDF2-SHA256 (100 000 iteraciones, sal aleatoria), comparación en tiempo constante, hash calculado aunque el usuario no exista |
| `appsettings.Development.json` | La clave de desarrollo, **pública a propósito** |
| `Program.cs` | `/token` recibe usuario y contraseña **en el cuerpo** y el rol lo decide el servidor; `RequireAuthorization(Politica)` en cada endpoint; **nuevo** `GET /cuentas/{id}/saldo` |
| `Breb.Cuentas.http` | Peticiones de ejemplo (reemplaza el `weatherforecast` de la plantilla) |
| `demo-clase3.ps1` · `.sh` | Credenciales nuevas; **la confirmación se programa a los 5 s** |
| `carga-clase5.py` · `pruebas-jwt-clase8.py` | Credenciales nuevas |
| `pruebas-autorizacion-clase9.py` · `rotacion-clase9.py` · `ataque-broker-clase9.py` | **Nuevos** |

## Usuarios de laboratorio

Documentados a propósito: **no son secretos**.

| Usuario | Contraseña | Rol |
|---|---|---|
| `ana.operadora` | `Operadora-2026` | operador |
| `luis.consulta` | `Consulta-2026` | consulta |
| `core-bancario` | `CoreBancario-2026` | banco (identidad de servicio) |

```bash
curl -X POST http://localhost:5080/token -H "Content-Type: application/json" -d '{"usuario":"ana.operadora","clave":"Operadora-2026"}'
```

> Un script que pida el token por la URL, como en la primera sesión, recibe **400**. Sin `Content-Type: application/json`, **415**.

## Autorización

| Endpoint | Roles |
|---|---|
| `POST /cuentas/{id}/retener` | operador |
| `POST /transferencias/{id}/confirmar-abono` | banco |
| `GET /cuentas/{id}/saldo` | operador, consulta |

```bash
python pruebas-autorizacion-clase9.py
```

**14 de 14.** Incluye los tres casos que fallaban en la actividad de la primera sesión: A5 y A7 (consulta retiene o confirma → 403) y A10 (consulta pide `?rol=operador` → el servidor emite `consulta`). Cambio de criterio: A8, operador confirma abono, pasa de 202 a **403**.

Batería `pruebas-jwt-clase8.py` contra la API nueva: **8 de 8**. El token mide 408 caracteres (antes 335): `kid`, `iat`, `nbf`.

### Emitir contra validar (mediana de 15)

| Petición | Tiempo |
|---|---|
| `/token` correcto | 193.0 ms |
| `/token` contraseña equivocada | 189.1 ms |
| `/token` usuario inexistente | 191.1 ms |
| `/quien-soy` (validar) | 4.4 ms |

Emitir cuesta 44 veces lo que validar. Los tres `/token` tardan lo mismo: el tiempo no revela si el usuario existe.

PBKDF2 en Python, mediana de 5: 1 000 it. → 1.5 ms · 100 000 → 364 ms · 600 000 → 2 507 ms. Lineal. OWASP recomienda 600 000; el laboratorio usa 100 000.

## Secretos

```bash
python rotacion-clase9.py
```

| Arranque en Production | Resultado |
|---|---|
| Sin `Jwt__ClaveFirma` | No arranca: `Falta Jwt:ClaveFirma…` |
| Con la clave de desarrollo | No arranca: `…es la clave de desarrollo, publicada en el repositorio…` |
| Con 16 bytes | No arranca: `…tiene 128 bits. HS256 exige 256 o más.` |

| Fase | Configuración | Token A | Token B |
|---|---|---|---|
| 0 | `ClaveFirma=A` | 200 | — |
| 1 | `ClaveFirma=B`, `ClaveAnterior=A` | 200 | 200 |
| 2 | `ClaveFirma=B` | **401** | 200 |

## El broker

```bash
python ataque-broker-clase9.py
```

| | Resultado |
|---|---|
| Confirmar A por la API sin token | 401 |
| Publicar `AbonoConfirmado` de A en RabbitMQ con `guest/guest` | 200 `{"routed":true}` |
| A tras 22 s | **No liberada: la saga la dio por completada** |
| B (control) tras 22 s | Liberada: compensada |
| Saldo antes → después | 4905/95 → 4805/195 (−100 / +100) |
| Usuario de broker con `write: ^$` | `403 ACCESS_REFUSED` |

**El JWT protege la API, no el broker.** Una transferencia se completó sin que el banco destino la confirmara. `guest/guest` no se cambió en esta sesión para no romper el entorno de los squads a una semana del E1.

## La demo del camino feliz

`demo-clase3.sh feliz` terminó **compensando**: la confirmación llegó a los 20 s de retener, después de tres `docker exec psql` (medido: 1.0–1.6 s cada uno). Ahora la confirmación se programa a los 5 s en segundo plano y la demo informa a cuántos segundos salió.

| Versión | Confirmación a los |
|---|---|
| Original | 20 s → compensó |
| PowerShell con `Start-Job` | 11.2 s |
| PowerShell con runspace | 5.1 – 5.3 s |
| bash | 6 s |

## Carga

`python carga-clase5.py "clase9 - 20 ctas" 5080 20 300 40`, esperando 0 sagas antes de cada corrida: **117.81 · 129.60 · 137.09 t/s, 0 errores HTTP**.

## Hallazgo abierto: una retención huérfana

Tras las 900 transferencias de carga: 0 sagas vivas, **1 R-FAULT** (`40001` en `FondosRetenidos`) y **1 retención huérfana** de 1 UVB en la cuenta `aaaaaaaa-…-000000000012`. El evento que crea la saga agotó sus 10 reintentos; la saga nunca nació y nadie va a compensar ese dinero.

No lo causa la autorización (no toca la mensajería). **No se sabe si ya ocurría:** en la carga de la primera sesión no se revisaron huérfanas. Se detecta con la consulta de reconciliación de la Semana 6.
