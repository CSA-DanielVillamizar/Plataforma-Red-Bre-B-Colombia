# Semana 8 — Autenticación con JWT

**Medido el 14 de septiembre de 2026.** `Breb.Cuentas` en el puerto 5080, una instancia, PostgreSQL 5433, RabbitMQ 5673.

## Qué cambió

| | |
|---|---|
| `src/Breb.Cuentas/Seguridad/Autenticacion.cs` | Nuevo. Validación (firma, emisor, audiencia, vigencia, `ClockSkew = 0`) y emisión de tokens HS256 de 15 min |
| `Program.cs` | `UseAuthentication` → `UseAuthorization`; `POST /token`; `GET /quien-soy`; `.RequireAuthorization()` en `/retener` y `/confirmar-abono` |
| `Breb.Cuentas.csproj` | `Microsoft.AspNetCore.Authentication.JwtBearer` 8.0.11 · `System.IdentityModel.Tokens.Jwt` 8.3.0 |
| `pruebas-jwt-clase8.py` | Nuevo. Batería de 8 ataques, solo biblioteca estándar |
| `demo-clase3.ps1` · `demo-clase3.sh` · `carga-clase5.py` | Piden un token una vez y lo envían como `Authorization: Bearer` |

> **Desde esta semana, `/retener` sin token responde 401.** Si una demo del E1 falla con 401, el script es anterior a este cambio.

Correcciones de paso en los scripts de demo:

- `demo-clase3.sh`: `saldo` y `reset` no filtraban por cuenta; con las 22 cuentas de la Semana 5 imprimía 22 saldos. Ahora filtran por la cuenta de la demo.
- `demo-clase3.ps1`: trae el parámetro `-Puerto` (5051 Visual Studio / 5080 terminal) y el filtro por cuenta que ya existían en la versión de clase.

## Cómo usar la API

```bash
TOKEN=$(curl -s -X POST "http://localhost:5080/token?usuario=ana.gomez&rol=operador" | python -c "import sys,json;print(json.load(sys.stdin)['token'])")
curl -X POST -H "Authorization: Bearer $TOKEN" "http://localhost:5080/cuentas/11111111-1111-1111-1111-111111111111/retener?montoUVB=10"
```

`/token` acepta cualquier usuario sin contraseña. Es deliberado: el tema de la semana es qué hace el token, no cómo se verifica una credencial.

## La batería

```bash
python pruebas-jwt-clase8.py
```

| # | Ataque | Esperado | Obtenido |
|---|---|---|---|
| 1 | Sin token | 401 | 401 |
| 2 | Token legítimo | 200 | 200 |
| 3 | Cuerpo alterado (rol → administrador), sin re-firmar | 401 | 401 |
| 4 | Firmado con otra clave | 401 | 401 |
| 5 | **Fabricado con la clave del repositorio** | **200** | **200** |
| 6 | Vencido hace 60 s (`ClockSkew = 0`) | 401 | 401 |
| 7 | Audiencia `breb-reportes` | 401 | 401 |
| 8 | `alg: none`, sin firma | 401 | 401 |

**8 de 8 según lo esperado.**

La prueba 5 es la lección: una firma garantiza que quien tiene la clave puede firmar. La clave está en `Autenticacion.cs`, versionada en git. `/quien-soy` con ese token responde `nameidentifier: atacante`, `role: administrador`. En producción la clave va en un gestor de secretos — tema de la Semana 8, segunda sesión.

## Observaciones del token

- **335 caracteres.** Unos 60 son el nombre largo del rol (`http://schemas.microsoft.com/ws/2008/06/identity/claims/role`), porque se emite con `ClaimTypes.Role`.
- **El cuerpo se lee sin clave.** Es Base64Url, no cifrado. Nada privado va en el token.
- **`User.Identity.Name` es `null`.** .NET renombra `sub` a `nameidentifier` al leerlo (`MapInboundClaims`), e `Identity.Name` busca otra afirmación. No se corrigió a propósito.

## Rendimiento

```bash
python carga-clase5.py "jwt - 20 ctas" 5080 20 300 40
```

| Corrida | Throughput | Errores |
|---|---|---|
| 1 | 86.81 t/s | 0 |
| 2 | 121.05 t/s | 0 |
| 3 | 57.68 t/s | 0 |

Sin JWT (Semana 6, mismo escenario): ~75 t/s, corridas entre 69 y 102.

| Afirmación | Confianza |
|---|---|
| El JWT no introduce errores bajo carga | Alta — 900 peticiones, 0 errores |
| Cuánto throughput cuesta el JWT | **Ninguna** — la dispersión propia (58-121) es mayor que cualquier diferencia |

Validar el token es un HMAC-SHA256 sobre ~300 bytes (microsegundos); cada petición además toma un `FOR UPDATE` y escribe en el Outbox (milisegundos). Si el JWT cuesta algo aquí, queda escondido en el ruido.

## Abierto para la segunda sesión

1. Autorización por rol: `consulta` no debería retener (403, no 401).
2. La clave fuera del código, y cómo rotarla sin invalidar todos los tokens de golpe.
3. Autenticación entre servicios y en los mensajes de RabbitMQ.
4. Revocación: el `jti` ya viaja; falta decidir si una lista de revocados vale su costo.
