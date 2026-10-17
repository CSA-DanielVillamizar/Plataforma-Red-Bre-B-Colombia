# Breb.App — la aplicación móvil de la Red Bre-B

Proyecto .NET MAUI del **Evaluable 3** (#28): tres pantallas (Login, Saldo, Transferencia) con **MVVM**, JWT en `SecureStorage` y llamadas HTTP en servicios inyectados.

---

## Estructura

```
Breb.App/
  Models/          LoginRequest, TokenResponse, SaldoResponse, RetencionResponse
  Services/        AuthService, BrebApiService, SecureTokenStore, ApiConfig
  ViewModels/      LoginViewModel, SaldoViewModel, TransferenciaViewModel
  Views/           LoginPage, SaldoPage, TransferenciaPage
```

El proyecto está en la solución `src/Breb.Platform.slnx`, junto a `Breb.Cuentas`.

---

## Credenciales y cuenta de prueba

| Usuario | Contraseña | Rol |
|---|---|---|
| `ana.operadora` | `Operadora-2026` | operador (saldo + retener) |
| `luis.consulta` | `Consulta-2026` | consulta (solo saldo) |

Cuenta de laboratorio (prellenada en la UI):

```
11111111-1111-1111-1111-111111111111
```

---

## Hablar con la API desde el emulador

La API debe escuchar en todas las interfaces:

```bash
docker start breb-postgres breb-rabbitmq
cd ../Breb.Cuentas
dotnet run --urls http://0.0.0.0:5080
```

| Desde el emulador, hacia… | Resultado |
|---|---|
| `http://localhost:5080` | Connection refused |
| `http://10.0.2.2:5080` | **HTTP 200** |

`ApiConfig` ya usa `10.0.2.2` en Android y `localhost` en Windows.

El tráfico HTTP en claro en Debug está permitido en `Platforms/Android/MainApplication.cs` (`UsesCleartextTraffic`).

---

## Compilar y ejecutar

Requisitos en la máquina: carga de trabajo `maui-android` y **Android SDK** (Visual Studio → workload "Desarrollo para dispositivos móviles con .NET", o Android Studio).

```bash
dotnet build .\src\Breb.App\Breb.App.csproj -f net10.0-android
```

Para validar lógica sin emulador (Windows):

```bash
dotnet build .\src\Breb.App\Breb.App.csproj -f net10.0-windows10.0.19041.0
dotnet run --project .\src\Breb.App\Breb.App.csproj -f net10.0-windows10.0.19041.0
```

El primer build Android puede tardar varios minutos. Detalle en [`docs/mediciones/semana-11-maui.md`](../../docs/mediciones/semana-11-maui.md).

---

## Flujo de la demo

1. Login con `ana.operadora` → JWT guardado en SecureStorage.
2. Saldo → consultar la cuenta `11111111-…`.
3. Transferencia → retener un monto (p. ej. 10 UVB) y ver el `transferenciaId`.
4. Apagar la API y repetir una acción → mensaje de error visible, sin congelar la UI.
