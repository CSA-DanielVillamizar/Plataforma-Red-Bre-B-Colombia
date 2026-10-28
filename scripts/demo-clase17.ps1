# Clase 17 — Idempotencia desde el cliente
#
# Cada parte usa SU PROPIA cuenta y NO borra retenciones: la saga sigue
# viva con sus timeouts de 15 s, y borrarle filas por debajo hace que el
# demo deje de ser reproducible. Solo se reinician los saldos.

$ErrorActionPreference = 'Stop'

$api   = 'http://localhost:5080'
$ctaA  = '11111111-1111-1111-1111-111111111111'   # Parte 1 — sin llave
$ctaB  = '22222222-2222-2222-2222-222222222222'   # Parte 2 — con llave

function Titulo($texto, $cuenta) {
    Write-Host ''
    Write-Host ('=' * 66) -ForegroundColor DarkCyan
    Write-Host "  $texto" -ForegroundColor Cyan
    Write-Host "  cuenta $($cuenta.Substring(0,8))..." -ForegroundColor DarkGray
    Write-Host ('=' * 66) -ForegroundColor DarkCyan
}

function Saldo($cuenta, $etiqueta) {
    $s = Invoke-RestMethod "$api/cuentas/$cuenta/saldo" -Headers @{ Authorization = "Bearer $token" }
    $color = if ($s.saldoRetenido -gt 0) { 'Yellow' } else { 'Green' }
    Write-Host ("{0,-10} disponible {1,10:N2}    RETENIDO {2,8:N2}" -f `
                $etiqueta, $s.saldoDisponible, $s.saldoRetenido) -ForegroundColor $color
}

function Retener($cuenta, $llave) {
    $url = "$api/cuentas/$cuenta/retener?montoUVB=100"
    if ($llave) { $url += "&transferenciaId=$llave" }

    # Se imprime el JSON CRUDO a proposito. Invoke-RestMethod convierte el
    # numero y se come el decimal: con el texto tal como llega se ve que el
    # primero devuelve 100 (el parametro recibido) y los repetidos 100.00
    # (leido de la base). Ese cero de mas es la prueba de que no insertaron.
    $crudo = (Invoke-WebRequest $url -Method Post `
                -Headers @{ Authorization = "Bearer $token" }).Content
    $color = if ($crudo -match '"repetida":true') { 'Green' } else { 'White' }
    Write-Host "   $crudo" -ForegroundColor $color
}

# ── Saldos en cero. Solo UPDATE: nada de DELETE bajo los pies de la saga ──
docker exec breb-postgres psql -U postgres -d brebcuentas `
    -c 'UPDATE "Cuentas" SET "SaldoDisponible"=5000, "SaldoRetenido"=0;' | Out-Null

$token = (Invoke-RestMethod "$api/token" -Method Post -ContentType 'application/json' `
            -Body '{"usuario":"ana.operadora","clave":"Operadora-2026"}').token

Titulo 'PARTE 1 — SIN LLAVE DEL CLIENTE' $ctaA
Write-Host '  El usuario toca el boton UNA vez. La respuesta se pierde.'
Write-Host '  La app reintenta, porque reintentar es lo correcto.'
Write-Host ''
Saldo $ctaA 'ANTES'
Write-Host ''
Write-Host '  > toque del usuario' -ForegroundColor DarkGray
Retener $ctaA $null
Write-Host '  > reintento de la app' -ForegroundColor DarkGray
Retener $ctaA $null
Write-Host ''
Saldo $ctaA 'DESPUES'
Write-Host ''
Write-Host '  Dos identificadores DISTINTOS para la misma intencion.' -ForegroundColor Red
Write-Host '  Pidio mover 100. La cuenta quedo con 200 retenidos.'   -ForegroundColor Red

Titulo 'PARTE 2 — CON LLAVE DEL CLIENTE' $ctaB
Write-Host '  La app genera la llave ANTES de salir a la red, y la reusa.'
Write-Host ''
Saldo $ctaB 'ANTES'
$llave = [guid]::NewGuid().ToString()
Write-Host ''
Write-Host "  llave generada por la app: $llave" -ForegroundColor Magenta
Write-Host ''
1..3 | ForEach-Object {
    Write-Host "  > intento $_, MISMA llave" -ForegroundColor DarkGray
    Retener $ctaB $llave
}
Write-Host ''
Saldo $ctaB 'DESPUES'
Write-Host ''
Write-Host '  TRES llamadas. UNA sola retencion de 100.' -ForegroundColor Green
Write-Host '  Los intentos 2 y 3 devuelven el monto ORIGINAL, leido de la base'
Write-Host '  (de ahi el 100.00 con dos decimales, contra el 100 del primero).'
Write-Host '  Y responden 200, no 409: para la app el reintento TUVO exito,'
Write-Host '  y por eso puede borrar la operacion de su cola local.'
Write-Host ('=' * 66) -ForegroundColor DarkCyan
Write-Host ''
