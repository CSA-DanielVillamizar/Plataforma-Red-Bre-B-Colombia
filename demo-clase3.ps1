# ─────────────────────────────────────────────────────────────
#  DEMO CLASE 3 — Saga de Transferencia con Compensación
#  Red Bre-B Colombia · 190304014-1
#
#  Uso (PowerShell):
#    .\demo-clase3.ps1 feliz        -> camino feliz (confirma a tiempo)
#    .\demo-clase3.ps1 compensar    -> compensación por timeout (15s)
#    .\demo-clase3.ps1 reset        -> restablece el saldo a 5000
#    .\demo-clase3.ps1 estado       -> muestra saldo, sagas y si la app corre
# ─────────────────────────────────────────────────────────────

param([string]$Modo = "")

$API    = "http://localhost:5080"
$CUENTA = "11111111-1111-1111-1111-111111111111"

function Linea { Write-Host ("=" * 62) -ForegroundColor Cyan }

# OJO: PowerShell elimina las comillas dobles al pasar argumentos a un .exe.
# PostgreSQL las necesita para respetar mayúsculas ("Cuentas" != cuentas),
# así que van escapadas como \" dentro del string.
function Sql($query) {
    $out = docker exec breb-postgres psql -U postgres -d brebcuentas -t -c $query 2>$null
    if ($null -eq $out) { return @() }
    return @($out)
}

function Saldo {
    $r = Sql 'SELECT ''   Disponible: '' || \"SaldoDisponible\" || ''   |   Retenido: '' || \"SaldoRetenido\" FROM \"Cuentas\";'
    $t = ($r | Where-Object { $_ -and $_.Trim() -ne "" }) -join "`n"
    if ($t) { Write-Host $t -ForegroundColor White }
    else    { Write-Host "   (sin datos - revisa que la BD este migrada)" -ForegroundColor Red }
}

function Sagas {
    $rn = Sql 'SELECT COUNT(*) FROM \"TransferenciaSagas\";'
    $re = Sql 'SELECT COALESCE(string_agg(\"CurrentState\",'',''),''ninguna'') FROM \"TransferenciaSagas\";'
    $n = if ($rn.Count -gt 0) { "$($rn[0])".Trim() } else { "?" }
    $e = if ($re.Count -gt 0) { "$($re[0])".Trim() } else { "?" }
    Write-Host "   Sagas en vuelo: $n   Estado: $e" -ForegroundColor White
}

function ResetSaldo {
    Sql 'UPDATE \"Cuentas\" SET \"SaldoDisponible\"=5000, \"SaldoRetenido\"=0; DELETE FROM \"MensajesProcesados\"; DELETE FROM \"TransferenciaSagas\";' | Out-Null
}

function ApiViva {
    try {
        $r = Invoke-WebRequest -Uri "$API/swagger/index.html" -TimeoutSec 5 -UseBasicParsing -ErrorAction Stop
        return ($r.StatusCode -eq 200)
    } catch { return $false }
}

# Verifica infraestructura Y aplicación antes de arrancar la demo.
function Preflight {
    $fallo = $false
    $contenedores = docker ps --format "{{.Names}}" 2>$null

    if ($contenedores -notcontains "breb-postgres") {
        Write-Host "`n[X] PostgreSQL no esta corriendo" -ForegroundColor Red
        Write-Host "    Solucion:  docker compose up -d" -ForegroundColor Yellow
        $fallo = $true
    }
    if ($contenedores -notcontains "breb-rabbitmq") {
        Write-Host "`n[X] RabbitMQ no esta corriendo" -ForegroundColor Red
        Write-Host "    Solucion:  docker compose up -d" -ForegroundColor Yellow
        $fallo = $true
    }
    if (-not (ApiViva)) {
        Write-Host "`n[X] La aplicacion NO esta corriendo en $API" -ForegroundColor Red
        Write-Host "    Arrancala en OTRA terminal:" -ForegroundColor Yellow
        Write-Host "       cd ..\Breb.Platform\Breb.Cuentas" -ForegroundColor White
        Write-Host "       dotnet run --urls http://localhost:5080" -ForegroundColor White
        Write-Host "    Espera a ver:  Bus started: rabbitmq://localhost/" -ForegroundColor Yellow
        $fallo = $true
    }

    if ($fallo) {
        Write-Host "`nLa demo no puede continuar hasta resolver lo anterior.`n" -ForegroundColor Yellow
        exit 1
    }
}

function Retener100 {
    try {
        $r = Invoke-RestMethod -Uri "$API/cuentas/$CUENTA/retener?montoUVB=100" -Method Post -TimeoutSec 10
        return $r.transferenciaId
    } catch {
        Write-Host "   [X] La API no respondio: $_" -ForegroundColor Red
        exit 1
    }
}

switch ($Modo) {

# ═══════════════════════════════════════════════════════════
"feliz" {
    Preflight; ResetSaldo
    Linea
    Write-Host "  CAMINO FELIZ - el abono llega a tiempo" -ForegroundColor Green
    Linea

    Write-Host "`n1. Saldo inicial:" -ForegroundColor White; Saldo

    Write-Host "`n2. El usuario inicia una transferencia de 100 UVB..." -ForegroundColor White
    $tid = Retener100
    Write-Host "   Transferencia: $tid" -ForegroundColor Yellow

    Start-Sleep -Seconds 3
    Write-Host "`n3. Saldo tras retener - se retuvieron 100 UVB:" -ForegroundColor White
    Saldo; Sagas
    Write-Host "   ^ 100 UVB salieron de Disponible y pasaron a Retenido." -ForegroundColor Cyan
    Write-Host "     El dinero esta EN TRANSITO: ni en origen ni en destino." -ForegroundColor Cyan

    Write-Host "`n4. El banco destino confirma el abono (dentro de los 15s)..." -ForegroundColor White
    try {
        Invoke-RestMethod -Uri "$API/transferencias/$tid/confirmar-abono" -Method Post -TimeoutSec 10 | Out-Null
        Write-Host "   Confirmacion enviada" -ForegroundColor Green
    } catch {
        Write-Host "   [X] Error al confirmar: $_" -ForegroundColor Red
    }

    Write-Host "`n   Esperando cierre de la saga..." -ForegroundColor DarkGray
    Start-Sleep -Seconds 6

    Write-Host "`n5. Resultado final:" -ForegroundColor White; Saldo; Sagas
    Write-Host "`n   OK - La transferencia se completo. El dinero salio como debia." -ForegroundColor Green
    Write-Host "   OK - La saga termino y su fila desaparecio." -ForegroundColor Green
    Write-Host "   OK - NO hubo compensacion.`n" -ForegroundColor Green
}

# ═══════════════════════════════════════════════════════════
"compensar" {
    Preflight; ResetSaldo
    Linea
    Write-Host "  COMPENSACION - el banco destino nunca confirma" -ForegroundColor Red
    Linea

    Write-Host "`n1. Saldo inicial:" -ForegroundColor White; Saldo

    Write-Host "`n2. El usuario inicia una transferencia de 100 UVB..." -ForegroundColor White
    $tid = Retener100
    Write-Host "   Transferencia: $tid" -ForegroundColor Yellow

    Start-Sleep -Seconds 3
    Write-Host "`n3. El MOL retuvo los fondos - 100 UVB en transito:" -ForegroundColor White
    Saldo; Sagas
    Write-Host "   ^ Este es el dinero que puede quedar ATRAPADO." -ForegroundColor Cyan

    Write-Host "`n4. Y ahora... no hacemos NADA." -ForegroundColor Red
    Write-Host "   El banco destino no responde. El reloj corre.`n" -ForegroundColor White

    foreach ($i in 15..1) {
        Write-Host ("`r     {0,2} segundos..." -f $i) -NoNewline -ForegroundColor Yellow
        Start-Sleep -Seconds 1
    }
    Write-Host "`r     TIMEOUT - la saga decide compensar        " -ForegroundColor Red

    Start-Sleep -Seconds 5
    Write-Host "`n5. Saldo final - nadie toco nada:" -ForegroundColor White; Saldo; Sagas
    Write-Host "`n   OK - El sistema devolvio los 100 UVB automaticamente." -ForegroundColor Green
    Write-Host "   OK - El dinero NO quedo atrapado." -ForegroundColor Green
    Write-Host "   OK - Esto es el post-it lila del Event Storming, funcionando.`n" -ForegroundColor Green
}

# ═══════════════════════════════════════════════════════════
"reset" {
    ResetSaldo
    Write-Host "`nSaldo restablecido:" -ForegroundColor Green; Saldo; Write-Host ""
}

"estado" {
    Linea; Write-Host "  ESTADO ACTUAL" -ForegroundColor Cyan; Linea
    Write-Host "`nSaldo:" -ForegroundColor White; Saldo
    Write-Host ""; Sagas
    if (ApiViva) {
        Write-Host "   Aplicacion: corriendo en $API`n" -ForegroundColor Green
    } else {
        Write-Host "   Aplicacion: NO responde  ->  dotnet run --urls $API`n" -ForegroundColor Red
    }
}

default {
    Write-Host ""
    Write-Host "  DEMO CLASE 3 - Saga de Transferencia" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "  .\demo-clase3.ps1 feliz       " -NoNewline -ForegroundColor White
    Write-Host "Camino feliz (confirma a tiempo)"
    Write-Host "  .\demo-clase3.ps1 compensar   " -NoNewline -ForegroundColor White
    Write-Host "Compensacion por timeout"
    Write-Host "  .\demo-clase3.ps1 reset       " -NoNewline -ForegroundColor White
    Write-Host "Restablece el saldo a 5000"
    Write-Host "  .\demo-clase3.ps1 estado      " -NoNewline -ForegroundColor White
    Write-Host "Muestra saldo, sagas y estado de la app"
    Write-Host ""
}

}
