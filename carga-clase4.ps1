# ─────────────────────────────────────────────────────────────
#  GENERADOR DE CARGA — Clase 4
#  Dispara N transferencias concurrentes y mide la latencia.
#
#  Uso:
#    .\carga-clase4.ps1                      -> 50 transferencias, 10 en paralelo
#    .\carga-clase4.ps1 -N 200               -> 200 transferencias
#    .\carga-clase4.ps1 -N 200 -Concurrencia 20
#
#  PUERTO: por defecto 5051 (Visual Studio con Ctrl+F5).
#  Si arrancaste con 'dotnet run --urls http://localhost:5080':
#    .\carga-clase4.ps1 -N 200 -Puerto 5080
# ─────────────────────────────────────────────────────────────

param(
    [int]$N = 50,
    [int]$Concurrencia = 10,
    [int]$MontoUVB = 1,
    [int]$Puerto = 5051
)

$Api = "http://localhost:$Puerto"

$CUENTA = "11111111-1111-1111-1111-111111111111"

function Linea { Write-Host ("=" * 62) -ForegroundColor Cyan }

function Sql($q) {
    $out = docker exec breb-postgres psql -U postgres -d brebcuentas -t -c $q 2>$null
    if ($null -eq $out) { return @() }
    return @($out)
}

# ── Verificación previa ──
try {
    $r = Invoke-WebRequest -Uri "$Api/swagger/index.html" -TimeoutSec 5 -UseBasicParsing -ErrorAction Stop
} catch {
    Write-Host "`n[X] La aplicacion NO responde en $Api" -ForegroundColor Red
    Write-Host ""
    Write-Host "    Si usas VISUAL STUDIO:" -ForegroundColor Yellow
    Write-Host "       Ctrl+F5 y espera:  Bus started: rabbitmq://localhost/" -ForegroundColor White
    Write-Host "       El puerto de VS es 5051 (perfil 'http')." -ForegroundColor White
    Write-Host ""
    Write-Host "    Si usas TERMINAL:" -ForegroundColor Yellow
    Write-Host "       dotnet run --urls http://localhost:5080" -ForegroundColor White
    Write-Host "       y luego:  .\carga-clase4.ps1 -N $N -Puerto 5080" -ForegroundColor White
    Write-Host ""
    exit 1
}

# Saldo alto para que 200 transferencias no lo agoten
Sql 'UPDATE \"Cuentas\" SET \"SaldoDisponible\"=1000000, \"SaldoRetenido\"=0; DELETE FROM \"MensajesProcesados\"; DELETE FROM \"TransferenciaSagas\";' | Out-Null

Linea
Write-Host "  GENERADOR DE CARGA — $N transferencias, $Concurrencia en paralelo" -ForegroundColor Cyan
Linea
Write-Host ""

$url = "$Api/cuentas/$CUENTA/retener?montoUVB=$MontoUVB"

# Desde la Semana 8 /retener exige un JWT. Se pide UNO y se reparte a
# todos los jobs: validar un token es barato, emitirlo no.
try {
    $cuerpo = @{ usuario = "ana.operadora"; clave = "Operadora-2026" } | ConvertTo-Json
    $tok = (Invoke-RestMethod -Uri "$Api/token" -Method Post -Body $cuerpo -ContentType "application/json" -TimeoutSec 10).token
} catch {
    Write-Host "`n[X] No se pudo obtener un token de $Api/token" -ForegroundColor Red
    Write-Host "    Corre la version de la Semana 8 o posterior?`n" -ForegroundColor Yellow
    exit 1
}

$inicio = Get-Date

# Cada job dispara un lote y devuelve la latencia de cada llamada
$bloque = {
    param($url, $cuantas, $token)
    $res = @()
    for ($i = 0; $i -lt $cuantas; $i++) {
        $t0 = Get-Date
        try {
            Invoke-RestMethod -Uri $url -Method Post -Headers @{ Authorization = "Bearer $token" } -TimeoutSec 60 | Out-Null
            $ok = $true
        } catch {
            $ok = $false
        }
        $res += [PSCustomObject]@{
            Ms = ((Get-Date) - $t0).TotalMilliseconds
            Ok = $ok
        }
    }
    return $res
}

$porJob = [math]::Ceiling($N / $Concurrencia)
Write-Host "  Disparando..." -ForegroundColor Yellow

$jobs = @()
for ($j = 0; $j -lt $Concurrencia; $j++) {
    $jobs += Start-Job -ScriptBlock $bloque -ArgumentList $url, $porJob, $tok
}

$jobs | Wait-Job | Out-Null
$todas = $jobs | Receive-Job
$jobs | Remove-Job

$duracion = ((Get-Date) - $inicio).TotalSeconds

# ── Resultados ──
$oks     = @($todas | Where-Object { $_.Ok })
$errores = @($todas | Where-Object { -not $_.Ok })
$lat     = @($oks | ForEach-Object { $_.Ms } | Sort-Object)

function Pct($arr, $p) {
    if ($arr.Count -eq 0) { return 0 }
    $i = [math]::Min([math]::Floor($arr.Count * $p / 100), $arr.Count - 1)
    return [math]::Round($arr[$i], 0)
}

Write-Host ""
Linea
Write-Host "  RESULTADOS" -ForegroundColor Cyan
Linea
Write-Host ""
Write-Host ("  Total disparadas      : {0}" -f $todas.Count) -ForegroundColor White
Write-Host ("  Exitosas              : {0}" -f $oks.Count) -ForegroundColor Green
if ($errores.Count -gt 0) {
    Write-Host ("  Con error             : {0}" -f $errores.Count) -ForegroundColor Red
} else {
    Write-Host ("  Con error             : 0") -ForegroundColor Green
}
Write-Host ("  Duracion total        : {0} s" -f [math]::Round($duracion,1)) -ForegroundColor White
if ($duracion -gt 0) {
    Write-Host ("  Throughput            : {0} transf/s" -f [math]::Round($todas.Count/$duracion,1)) -ForegroundColor White
}
Write-Host ""
Write-Host "  LATENCIA (ms)" -ForegroundColor Yellow
Write-Host ("    p50 (mediana)       : {0}" -f (Pct $lat 50)) -ForegroundColor White
Write-Host ("    p95                 : {0}" -f (Pct $lat 95)) -ForegroundColor White
Write-Host ("    p99                 : {0}" -f (Pct $lat 99)) -ForegroundColor White

$max = if ($lat.Count -gt 0) { [math]::Round($lat[-1],0) } else { 0 }
$color = if ($max -gt 20000) { "Red" } else { "Green" }
Write-Host ("    MAXIMA              : {0}" -f $max) -ForegroundColor $color

if ($max -gt 20000) {
    Write-Host ""
    Write-Host "  [!] La latencia maxima SUPERA el SLA de 20 segundos." -ForegroundColor Red
} else {
    Write-Host ""
    Write-Host "  OK - Dentro del SLA de 20 segundos." -ForegroundColor Green
}

# ── Estado de la base ──
Start-Sleep -Seconds 3
$sagas = (Sql 'SELECT COUNT(*) FROM \"TransferenciaSagas\";')
$n = if ($sagas.Count -gt 0) { "$($sagas[0])".Trim() } else { "?" }
Write-Host ""
Write-Host ("  Sagas en vuelo ahora  : {0}" -f $n) -ForegroundColor White
Write-Host "  (en ~15s deberian compensarse todas)" -ForegroundColor DarkGray
Write-Host ""
Write-Host "  Anota estos numeros en el tablero: son tu linea base." -ForegroundColor Cyan
Write-Host ""
