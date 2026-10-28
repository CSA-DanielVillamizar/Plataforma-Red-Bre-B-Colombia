# Semana 14 — La pantalla de transferencia, medida

**Fecha:** 28 de octubre de 2026
**Entorno:** Windows 11 · .NET SDK 10 · API `Breb.Cuentas` en `http://0.0.0.0:5080` · PostgreSQL 5433 y RabbitMQ 5673 en Docker

Cuenta de laboratorio `11111111-1111-1111-1111-111111111111`, con 5 000,00 UVB disponibles y 0,00 retenidos al empezar cada corrida.

---

## 1. La llave sobrevive a la caída de la red

Es el escenario completo de la app, no sólo el del endpoint: el usuario toca el botón, la respuesta se pierde, el usuario vuelve a tocar. La diferencia con la medición del 26 de octubre es **dónde vive la llave**: ahora la genera `TransferenciaViewModel` antes de la primera llamada y no la borra si la llamada falla.

```csharp
// TransferenciaViewModel.TransferirAsync()
_llavePendiente ??= Guid.NewGuid();   // ← sólo si no había una viva
var llave = _llavePendiente.Value;
```

**Resultado medido:**

| Momento | Disponible | Retenido |
|---|---|---|
| Antes | 5 000,00 | 0,00 |
| Toque + reintento con la misma llave | **4 900,00** | **100,00** |

```json
{"transferenciaId":"f4429e10-b7c4-4999-90fd-0e85a148951f","montoUVB":100,   "repetida":false}
{"transferenciaId":"f4429e10-b7c4-4999-90fd-0e85a148951f","montoUVB":100.00,"repetida":true}
```

Una sola retención. Dos toques del usuario, **un solo movimiento de plata**.

El `??=` es toda la diferencia. Con un `=` simple, cada toque generaría una llave nueva y volvería el duplicado del 26 de octubre.

---

## 2. La transferencia no termina cuando la app deja de esperar

Esto es lo que una pantalla de banco tiene que saber comunicar, y es la parte que casi nadie modela bien.

Al volver el `200 OK`, los fondos quedaron **retenidos**, no transferidos. Falta que el banco destino confirme el abono por `/transferencias/{id}/confirmar-abono`. Si no lo hace, la saga de la Semana 3 compensa por su `timeout` de 15 s.

**Resultado medido, dos corridas independientes:**

| Momento | Disponible | Retenido |
|---|---|---|
| Tras el `200 OK` | 4 900,00 | 100,00 |
| **18 s después, sin confirmación del banco** | **5 000,00** | **0,00** |

El saldo volvió solo. Las dos veces.

De ahí sale lo que muestra la pantalla: `Esperando confirmación del banco destino…`, y no «Transferencia completada». Decir «completada» sería mentir — y la mentira se descubre quince segundos después, cuando el saldo vuelve.

---

## 3. ⚠️ La trampa de esta semana: el instrumento era once veces más lento que lo medido

La primera medición de latencia se hizo con `Invoke-WebRequest` de PowerShell. Daba esto:

| | `Invoke-WebRequest` |
|---|---|
| `/retener` nueva | 2 790 ms |
| `/retener` repetida | 2 324 ms |

Dos segundos y medio para retener fondos es un número que, en una app móvil, cambia decisiones de diseño: justificaría una pantalla de carga, un *spinner*, quizá reestructurar el flujo.

Antes de escribirlo en una diapositiva se midió lo mismo con `curl`:

| | `Invoke-WebRequest` | `curl` (mediana) |
|---|---|---|
| `/retener` nueva | 2 790 ms | **246 ms** |
| `/retener` repetida | 2 324 ms | **242 ms** |
| `/saldo` | — | **222 ms** |

Cinco corridas para las nuevas, tres para las repetidas, cinco para el saldo. La dispersión fue mínima: 237–255 ms.

**El servidor nunca tardó dos segundos.** Los dos segundos los ponía el cliente con el que se estaba midiendo.

> Es la cuarta vez este semestre: el navegador de la Semana 11, el `uiautomator dump` de 3 792 ms de la Semana 12, el proceso zombi del 26 de octubre, y hoy esto. La regla ya no es una anécdota: **una latencia medida con un solo instrumento no es una medición, es una hipótesis.**

---

## 4. Protegerse del duplicado no cuesta latencia

El dato anterior deja ver algo que no era obvio:

| | Mediana |
|---|---|
| `/retener` creando una retención nueva | 246 ms |
| `/retener` reconociendo una repetida | **242 ms** |

Cuatro milisegundos de diferencia, dentro del ruido de la medición. La comprobación de idempotencia es una búsqueda por clave primaria sobre un índice: no se paga.

Y conviene ser preciso sobre lo que esto **no** dice. La repetida no es más rápida, aunque haga menos trabajo: sigue pagando el `SELECT … FOR UPDATE` de la Semana 5 y los dos viajes a la base. Lo que la medición demuestra no es un ahorro, es que **la corrección fue gratis**.

---

## 5. El layout se adapta por ancho, no por aparato

`TransferenciaPage.xaml` no pregunta en qué aparato corre. Dos paneles dentro de un `FlexLayout`:

```xml
<FlexLayout Direction="Row" Wrap="Wrap">
    <VerticalStackLayout FlexLayout.Grow="1" MinimumWidthRequest="320"> … </VerticalStackLayout>
    <VerticalStackLayout FlexLayout.Grow="1" MinimumWidthRequest="320"> … </VerticalStackLayout>
</FlexLayout>
```

Cuando los dos paneles no caben en 640 puntos, `Wrap` baja el segundo. Por encima de eso quedan en dos columnas.

La razón de hacerlo por ancho y no con `OnIdiom` es que **el ancho cambia sin cambiar de aparato**: al girar el teléfono, al partir la pantalla en Android, al redimensionar la ventana en Windows. Un layout que decide por tipo de aparato se equivoca en los tres casos.

`OnIdiom` sí se usa, para lo único que de verdad depende del aparato: el margen contra el borde de la pantalla. Un teléfono se agarra con la mano y necesita menos margen que una ventana de escritorio.

> **Nota honesta sobre el alcance de esta sección.** Lo verificado es que el proyecto compila para `net10.0-windows10.0.19041.0` sin errores y que el mecanismo es el descrito. El reflujo en dos anchos distintos se comprueba en vivo en clase redimensionando la ventana; no hay captura medida aquí.

---

## 6. Reproducir esto

```bash
docker start breb-postgres breb-rabbitmq
cd src/Breb.Cuentas && dotnet run --urls http://0.0.0.0:5080
```

El escenario de la llave y el timeout de la saga:

```powershell
.\scripts\medir-clase18.ps1
```

Las latencias **con curl**, que son las que valen:

```bash
curl -s -o /dev/null -X POST "$API/cuentas/$CTA/retener?montoUVB=1&transferenciaId=$(uuidgen)" \
     -H "Authorization: Bearer $TOKEN" -w "%{time_total}s\n"
```

Y antes de creerle a cualquier latencia: mídala con un segundo instrumento.
