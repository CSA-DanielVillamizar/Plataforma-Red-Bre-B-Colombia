# Clase 18 — ¿la llave sobrevive a la caida de la red?
#
# Reproduce, a nivel de API, exactamente la secuencia que hace
# TransferenciaViewModel cuando el usuario toca el boton, la red se cae y
# el usuario vuelve a tocar. La llave se genera UNA vez, antes de la
# primera llamada, y se reusa en el reintento.

$ErrorActionPreference = 'Continue'

$api  = 'http://localhost:5080'
$cta  = '11111111-1111-1111-1111-111111111111'

function Saldo($token, $etiqueta) {
    $s = Invoke-RestMethod "$api/cuentas/$cta/saldo" -Headers @{ Authorization = "Bearer $token" }
    Write-Host ("{0,-22} disponible {1,10:N2}   RETENIDO {2,8:N2}" -f `
                $etiqueta, $s.saldoDisponible, $s.saldoRetenido)
}

docker exec breb-postgres psql -U postgres -d brebcuentas `
    -c 'UPDATE "Cuentas" SET "SaldoDisponible"=5000, "SaldoRetenido"=0;' | Out-Null

$token = (Invoke-RestMethod "$api/token" -Method Post -ContentType 'application/json' `
            -Body '{"usuario":"ana.operadora","clave":"Operadora-2026"}').token

Write-Host ''
Write-Host '--- La app genera la llave al tocar el boton, ANTES de la red ---' -ForegroundColor Cyan
$llave = [guid]::NewGuid().ToString()
Write-Host "    $llave" -ForegroundColor Magenta
Write-Host ''
Saldo $token 'ANTES'

Write-Host ''
Write-Host '--- Intento 1: la red se cae, la app no sabe si llego ---' -ForegroundColor Cyan
$t = [Diagnostics.Stopwatch]::StartNew()
$r1 = (Invoke-WebRequest "$api/cuentas/$cta/retener?montoUVB=100&transferenciaId=$llave" `
         -Method Post -Headers @{ Authorization = "Bearer $token" }).Content
$ms1 = $t.ElapsedMilliseconds
Write-Host "    $r1"
Write-Host "    $ms1 ms" -ForegroundColor DarkGray

Write-Host ''
Write-Host '--- El usuario vuelve a tocar. MISMA llave, porque no se borro ---' -ForegroundColor Cyan
$t = [Diagnostics.Stopwatch]::StartNew()
$r2 = (Invoke-WebRequest "$api/cuentas/$cta/retener?montoUVB=100&transferenciaId=$llave" `
         -Method Post -Headers @{ Authorization = "Bearer $token" }).Content
$ms2 = $t.ElapsedMilliseconds
Write-Host "    $r2"
Write-Host "    $ms2 ms" -ForegroundColor DarkGray

Write-Host ''
Saldo $token 'DESPUES'

Write-Host ''
Write-Host '--- Lo que la pantalla muestra despues: fondos RETENIDOS ---' -ForegroundColor Cyan
Write-Host '    La transferencia no termino. Falta que el banco destino'
Write-Host '    confirme el abono. Si no lo hace en 15 s, la saga compensa.'
Write-Host ''
Write-Host '--- 18 segundos despues, sin confirmacion del banco destino ---' -ForegroundColor Cyan
Start-Sleep -Seconds 18
Saldo $token 'TRAS EL TIMEOUT'
Write-Host ''
