# ─────────────────────────────────────────────────────────────────────────────
#  El BANCO DESTINO confirma el abono.  ·  Clase 19 / Evaluable 4
# ─────────────────────────────────────────────────────────────────────────────
#
#  POR QUE ESTE SCRIPT EXISTE, Y POR QUE NO ES UN BOTON EN LA APP
#
#  La app de referencia llama tres endpoints: /token, /saldo y /retener.
#  Nunca llama /confirmar-abono, y no es un olvido: no puede.
#
#      app.MapPost("/transferencias/{id}/confirmar-abono", ...)
#          .RequireAuthorization(Politicas.BancoDestino);   // rol "banco"
#
#  La app entra con rol "operador". El 403 que recibiria no es un defecto:
#  es la autorizacion de la Semana 8 haciendo exactamente su trabajo. Si la
#  app del pagador pudiera confirmar su propio abono, la saga no serviria
#  para nada — estaria certificando que llego plata a un banco con el que
#  nunca hablo. El que confirma es OTRO actor, y por eso esto es un script
#  aparte, con otra identidad.
#
#  QUE SE VE AL CORRERLO
#
#  Despues de /retener hay 15 segundos de carrera entre este script y el
#  reloj de la saga. Los dos finales dejan el RETENIDO en 0, asi que mirar
#  el retenido no distingue nada. Lo que distingue es el DISPONIBLE:
#
#      confirmada  ->  Liquidada = true   disponible 4.900   la plata SALIO
#      compensada  ->  Liberada  = true   disponible 5.000   la plata VOLVIO
#
#  USO
#      .\scripts\confirmar-abono.ps1 -TransferenciaId <guid>
#      .\scripts\confirmar-abono.ps1 -TransferenciaId <guid> -Api http://192.168.20.29:5080
#
#  La llave que pide es la que la app muestra en pantalla bajo "Llave de
#  idempotencia de esta operacion". Es la misma que nacio al tocar el boton.
# ─────────────────────────────────────────────────────────────────────────────

param(
    [Parameter(Mandatory = $true)]
    [string]$TransferenciaId,

    [string]$Api = "http://127.0.0.1:5080",

    # Identidad de SERVICIO del core bancario destino. No es un usuario
    # humano: ningun operador deberia poder confirmar abonos.
    [string]$Usuario = "core-bancario",
    [string]$Clave   = "CoreBancario-2026",

    # La cuenta origen de la demo, para mostrar el saldo antes y despues.
    [string]$CuentaOrigen = "11111111-1111-1111-1111-111111111111"
)

$ErrorActionPreference = "Stop"

function Titulo($t) {
    Write-Host ""
    Write-Host "══════ $t " -ForegroundColor Cyan
}

function Saldo($token) {
    # Invoke-WebRequest y no Invoke-RestMethod: el segundo parsea el JSON y
    # se come la diferencia entre 100 y 100.00, que es justo lo que a veces
    # hay que mirar. Leccion del 28 de octubre.
    $r = Invoke-WebRequest -Uri "$Api/cuentas/$CuentaOrigen/saldo" `
                           -Headers @{ Authorization = "Bearer $token" } `
                           -UseBasicParsing
    return ($r.Content | ConvertFrom-Json)
}

# ── 0 · Token del banco destino ──────────────────────────────────────────────
Titulo "0 · token de $Usuario"

$tokenBanco = (Invoke-RestMethod -Uri "$Api/token" -Method Post `
                 -ContentType "application/json" `
                 -Body (@{ usuario = $Usuario; clave = $Clave } | ConvertTo-Json))

Write-Host ("  rol        : {0}" -f $tokenBanco.rol)
Write-Host ("  vence en   : {0} min" -f $tokenBanco.duracionMinutos)

if ($tokenBanco.rol -ne "banco") {
    Write-Host ""
    Write-Host "  ⚠ Este usuario NO tiene rol banco. /confirmar-abono va a devolver 403." -ForegroundColor Yellow
    Write-Host "    Y esta bien que lo haga: confirmar abonos no es trabajo de un operador." -ForegroundColor Yellow
}

# ── 1 · Saldo ANTES ──────────────────────────────────────────────────────────
# Con el token del banco no se puede leer saldos: la politica LecturaCuentas
# admite "operador" y "consulta", no "banco". Es la misma idea en el otro
# sentido — cada identidad puede lo suyo y nada mas. Asi que para MIRAR se
# usa un token de consulta.
Titulo "1 · saldo antes de confirmar"

$tokenLectura = (Invoke-RestMethod -Uri "$Api/token" -Method Post `
                   -ContentType "application/json" `
                   -Body (@{ usuario = "luis.consulta"; clave = "Consulta-2026" } | ConvertTo-Json))

$antes = Saldo $tokenLectura.token
Write-Host ("  disponible : {0}" -f $antes.saldoDisponible)
Write-Host ("  retenido   : {0}" -f $antes.saldoRetenido)

if ($antes.saldoRetenido -eq 0) {
    Write-Host ""
    Write-Host "  ⚠ No hay nada retenido. O la saga ya compenso (pasaron los 15 s)," -ForegroundColor Yellow
    Write-Host "    o esta transferencia nunca se hizo. Confirmar ahora no va a" -ForegroundColor Yellow
    Write-Host "    cambiar ningun saldo: el evento llegara y no habra retencion" -ForegroundColor Yellow
    Write-Host "    viva que liquidar. En la demo hay que correr esto DENTRO de" -ForegroundColor Yellow
    Write-Host "    los 15 segundos." -ForegroundColor Yellow
}

# ── 2 · Confirmar ────────────────────────────────────────────────────────────
Titulo "2 · POST /transferencias/$TransferenciaId/confirmar-abono"

$respuesta = Invoke-WebRequest -Uri "$Api/transferencias/$TransferenciaId/confirmar-abono" `
                               -Method Post `
                               -Headers @{ Authorization = "Bearer $($tokenBanco.token)" } `
                               -UseBasicParsing

Write-Host ("  HTTP {0} {1}" -f [int]$respuesta.StatusCode, $respuesta.StatusDescription)
Write-Host ""
Write-Host "  202 Accepted significa 'recibi el aviso', NO 'ya quedo'. El endpoint"
Write-Host "  publica AbonoConfirmado y devuelve; el trabajo lo hace la saga por"
Write-Host "  el broker, de forma asincrona. Esa es la diferencia entre aceptar y"
Write-Host "  completar, y es la razon de esperar antes de mirar el saldo."

# ── 3 · Saldo DESPUES ────────────────────────────────────────────────────────
Titulo "3 · saldo despues (tras 3 s para que la saga procese)"

Start-Sleep -Seconds 3
$despues = Saldo $tokenLectura.token
Write-Host ("  disponible : {0}" -f $despues.saldoDisponible)
Write-Host ("  retenido   : {0}" -f $despues.saldoRetenido)

# ── 4 · Veredicto ────────────────────────────────────────────────────────────
Titulo "4 · que paso"

# CUIDADO CON LA LINEA BASE — este veredicto estuvo mal escrito y la primera
# medicion lo delato.
#
# El error era comparar "disponible antes de confirmar" contra "disponible
# despues". No sirve: cuando se toma la primera foto, /retener YA bajo el
# disponible de 5.000 a 4.900. Asi que en el camino FELIZ el disponible no
# cambia entre las dos fotos —se queda en 4.900— y un script que busca una
# BAJADA concluye exactamente lo contrario de lo que paso.
#
# Lo que de verdad distingue los dos finales, tomando como base la foto
# posterior a la retencion:
#
#     completada  ->  retenido a 0  ·  disponible SE QUEDA igual
#     compensada  ->  retenido a 0  ·  disponible SUBE en el monto retenido
#
# Los dos dejan el retenido en 0. El que se mueve es el disponible, y se
# mueve HACIA ARRIBA solo cuando la plata regresa.
$tolerancia  = 0.005
$seLibero    = $despues.saldoRetenido -eq 0
$seQuedoIgual = [math]::Abs($despues.saldoDisponible - $antes.saldoDisponible) -lt $tolerancia
$volvioLaPlata = [math]::Abs($despues.saldoDisponible - ($antes.saldoDisponible + $antes.saldoRetenido)) -lt $tolerancia

if ($antes.saldoRetenido -eq 0) {
    # Sin esta rama el veredicto diria "completada" —disponible igual,
    # retenido 0— y seria mentira: no habia nada que liquidar.
    Write-Host "  ⊘ NO HABIA NADA QUE CONFIRMAR" -ForegroundColor Yellow
    Write-Host "    Al empezar no habia retencion viva, asi que este script no cambio"
    Write-Host "    nada. O la saga ya compenso (pasaron los 15 s), o esa"
    Write-Host "    transferencia nunca existio. Esto NO es una transferencia"
    Write-Host "    completada."
}
elseif (-not $seLibero) {
    Write-Host "  … todavia hay fondos retenidos. Espere un segundo y vuelva a" -ForegroundColor Yellow
    Write-Host "    consultar el saldo; el broker puede ir con retraso." -ForegroundColor Yellow
}
elseif ($seQuedoIgual) {
    Write-Host "  ✓ TRANSFERENCIA COMPLETADA" -ForegroundColor Green
    Write-Host ("    El retenido paso de {0} a 0 y el disponible se QUEDO en {1}:" -f $antes.saldoRetenido, $despues.saldoDisponible)
    Write-Host "    la plata salio de la cuenta. En la base: Liquidada = true."
}
elseif ($volvioLaPlata) {
    Write-Host "  ✗ COMPENSADA, no completada" -ForegroundColor Red
    Write-Host ("    El disponible SUBIO de {0} a {1}: la plata regreso." -f $antes.saldoDisponible, $despues.saldoDisponible)
    Write-Host "    Gano el reloj de la saga — la confirmacion llego despues de"
    Write-Host "    los 15 s. En la base: Liberada = true."
}
else {
    Write-Host "  ? El saldo no cuadra con ninguno de los dos finales." -ForegroundColor Yellow
    Write-Host ("    antes {0}/{1} · despues {2}/{3}" -f $antes.saldoDisponible, $antes.saldoRetenido, $despues.saldoDisponible, $despues.saldoRetenido)
    Write-Host "    Probablemente hay mas de una transferencia viva sobre la misma"
    Write-Host "    cuenta. Mire la tabla de Retenciones."
}

Write-Host ""
Write-Host "  Para verlo en la base, que es donde los dos casos se distinguen sin ambiguedad:"
Write-Host '    docker exec breb-postgres psql -U postgres -d brebcuentas -c ' -NoNewline
Write-Host '"SELECT \"TransferenciaId\",\"MontoUVB\",\"Liberada\",\"Liquidada\" FROM \"Retenciones\";"'
Write-Host ""
