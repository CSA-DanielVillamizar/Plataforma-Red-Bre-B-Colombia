#!/usr/bin/env bash
# ─────────────────────────────────────────────────────────────
#  DEMO CLASE 3 — Saga de Transferencia con Compensación
#  Red Bre-B Colombia · 190304014-1
#
#  Uso:
#    ./demo-clase3.sh feliz        → camino feliz (confirma a tiempo)
#    ./demo-clase3.sh compensar    → compensación por timeout (15s)
#    ./demo-clase3.sh reset        → restablece el saldo a 5000
#    ./demo-clase3.sh estado       → muestra saldo y sagas en vuelo
# ─────────────────────────────────────────────────────────────

API="http://localhost:5080"
CUENTA="11111111-1111-1111-1111-111111111111"
PG="docker exec breb-postgres psql -U postgres -d brebcuentas"

# Colores para proyectar
V='\033[1;32m'; R='\033[1;31m'; A='\033[1;33m'; C='\033[1;36m'; N='\033[0m'; B='\033[1m'

linea() { echo -e "${C}════════════════════════════════════════════════════════════${N}"; }

# Filtra por la cuenta de la demo: desde la Clase 4 hay más cuentas (2222...
# para el deadlock y las aaaa... de la carga de la Clase 5), y sin el WHERE
# saldría un saldo por cada una.
saldo() {
    $PG -t -c "SELECT '   Disponible: ' || \"SaldoDisponible\" || '   |   Retenido: ' || \"SaldoRetenido\" FROM \"Cuentas\" WHERE \"Id\"='$CUENTA';" 2>/dev/null | grep -v '^$'
}

sagas() {
    local n=$($PG -t -c "SELECT COUNT(*) FROM \"TransferenciaSagas\";" 2>/dev/null | tr -d ' ')
    local e=$($PG -t -c "SELECT COALESCE(string_agg(\"CurrentState\",','),'ninguna') FROM \"TransferenciaSagas\";" 2>/dev/null | tr -d ' ')
    echo -e "   Sagas en vuelo: ${B}${n}${N}   Estado: ${B}${e}${N}"
}

reset_saldo() {
    $PG -c "UPDATE \"Cuentas\" SET \"SaldoDisponible\"=5000, \"SaldoRetenido\"=0 WHERE \"Id\"='$CUENTA'; DELETE FROM \"MensajesProcesados\"; DELETE FROM \"TransferenciaSagas\";" >/dev/null 2>&1
}

# Verifica que la infraestructura Y la aplicación estén arriba.
# Sin esto, el script "corre" pero no pasa nada: el curl falla en silencio.
preflight() {
    local fallo=0

    if ! docker ps --format '{{.Names}}' 2>/dev/null | grep -q breb-postgres; then
        echo -e "\n${R}✗ PostgreSQL no está corriendo${N}"
        echo -e "  Solución:  ${B}docker compose up -d${N}"
        fallo=1
    fi

    if ! docker ps --format '{{.Names}}' 2>/dev/null | grep -q breb-rabbitmq; then
        echo -e "\n${R}✗ RabbitMQ no está corriendo${N}"
        echo -e "  Solución:  ${B}docker compose up -d${N}"
        fallo=1
    fi

    local code=$(curl -s -o /dev/null -w "%{http_code}" --max-time 5 "$API/swagger/index.html" 2>/dev/null)
    if [ "$code" != "200" ]; then
        echo -e "\n${R}✗ La aplicación NO está corriendo en $API${N}"
        echo -e "  Arráncala en otra terminal:"
        echo -e "     ${B}cd src/Breb.Cuentas${N}"
        echo -e "     ${B}dotnet run --urls http://localhost:5080${N}"
        echo -e "  Espera a ver: ${B}Bus started: rabbitmq://localhost/${N}"
        fallo=1
    fi

    if [ "$fallo" = "1" ]; then
        echo -e "\n${A}La demo no puede continuar hasta resolver lo anterior.${N}\n"
        exit 1
    fi
}

# Desde la Semana 8 la API exige un JWT, y cada operación pide su ROL:
#   /retener          → ana.operadora  (rol operador)
#   /confirmar-abono  → core-bancario  (rol banco: lo confirma el banco destino)
# Usuarios de LABORATORIO, documentados: no son secretos.
pedir_token() {
    local r=$(curl -s -X POST --max-time 10 -H "Content-Type: application/json" \
              -d "{\"usuario\":\"$1\",\"clave\":\"$2\"}" "$API/token")
    T=$(echo "$r" | python -c "import sys,json;print(json.load(sys.stdin)['token'])" 2>/dev/null)
    if [ -z "$T" ]; then
        echo -e "\n${R}✗ No se pudo obtener un token para $1 en $API/token${N}"
        echo -e "  ¿Corre la versión de la Semana 8 (segunda sesión) o posterior?\n"
        exit 1
    fi
}

token() {
    pedir_token "ana.operadora" "Operadora-2026";    TOKEN=$T
    pedir_token "core-bancario" "CoreBancario-2026"; TOKEN_BANCO=$T
}

case "${1:-}" in

# ═══════════════════════════════════════════════════════════
feliz)
    preflight
    reset_saldo
    linea
    echo -e "${V}  CAMINO FELIZ — el abono llega a tiempo${N}"
    linea
    echo -e "\n${B}1. Saldo inicial:${N}"; saldo

    echo -e "\n${B}2. El usuario inicia una transferencia de 100 UVB...${N}"
    token
    RESP=$(curl -s -X POST -H "Authorization: Bearer $TOKEN" "$API/cuentas/$CUENTA/retener?montoUVB=100")
    TID=$(echo "$RESP" | python -c "import sys,json;print(json.load(sys.stdin)['transferenciaId'])" 2>/dev/null)

    if [ -z "$TID" ]; then
        echo -e "   ${R}✗ La API no devolvió una transferencia.${N}"
        echo -e "   Respuesta recibida: ${A}${RESP:-(vacía)}${N}\n"
        exit 1
    fi
    echo -e "   Transferencia: ${A}$TID${N}"

    # La confirmación se PROGRAMA ya, a los 5 s de retener, en segundo plano.
    # Antes se enviaba después de consultar el saldo, y cada 'docker exec'
    # tarda 1-1.6 s (medido); con Docker lento, la confirmación llegaba pasados
    # los 15 s y la saga compensaba en pleno "camino feliz".
    T0=$(date +%s)
    CONF=$(mktemp)
    ( sleep 5
      code=$(curl -s -o /dev/null -w "%{http_code}" -X POST \
             -H "Authorization: Bearer $TOKEN_BANCO" "$API/transferencias/$TID/confirmar-abono")
      echo "$code $(( $(date +%s) - T0 ))" > "$CONF" ) &

    sleep 2
    echo -e "\n${B}3. Saldo tras retener — ${A}se retuvieron 100 UVB${N}${B}:${N}"; saldo; sagas
    echo -e "   ${C}↑ 100 UVB salieron de Disponible y pasaron a Retenido.${N}"
    echo -e "   ${C}  El dinero está EN TRÁNSITO: ni en origen ni en destino.${N}"

    echo -e "\n${B}4. El banco destino confirma el abono (dentro de los 15s)...${N}"
    wait
    read CODIGO SEG < "$CONF"; rm -f "$CONF"
    echo -e "   Respuesta: HTTP $CODIGO — enviada a los ${SEG} s de retener (límite: 15 s)"

    echo -e "\n   Esperando cierre de la saga..."
    sleep 6

    echo -e "\n${B}5. Resultado final:${N}"; saldo; sagas
    echo -e "\n${V}   ✓ La transferencia se completó. El dinero salió como debía.${N}"
    echo -e "${V}   ✓ La saga terminó y su fila desapareció.${N}"
    echo -e "${V}   ✓ NO hubo compensación.${N}\n"
    ;;

# ═══════════════════════════════════════════════════════════
compensar)
    preflight
    reset_saldo
    linea
    echo -e "${R}  COMPENSACIÓN — el banco destino nunca confirma${N}"
    linea
    echo -e "\n${B}1. Saldo inicial:${N}"; saldo

    echo -e "\n${B}2. El usuario inicia una transferencia de 100 UVB...${N}"
    token
    RESP=$(curl -s -X POST -H "Authorization: Bearer $TOKEN" "$API/cuentas/$CUENTA/retener?montoUVB=100")
    TID=$(echo "$RESP" | python -c "import sys,json;print(json.load(sys.stdin)['transferenciaId'])" 2>/dev/null)

    if [ -z "$TID" ]; then
        echo -e "   ${R}✗ La API no devolvió una transferencia.${N}"
        echo -e "   Respuesta recibida: ${A}${RESP:-(vacía)}${N}\n"
        exit 1
    fi
    echo -e "   Transferencia: ${A}$TID${N}"

    sleep 3
    echo -e "\n${B}3. El MOL retuvo los fondos — ${A}100 UVB en tránsito${N}${B}:${N}"; saldo; sagas
    echo -e "   ${C}↑ Este es el dinero que puede quedar ATRAPADO.${N}"

    echo -e "\n${R}${B}4. Y ahora... no hacemos NADA.${N}"
    echo -e "   El banco destino no responde. El reloj corre.\n"

    for i in $(seq 15 -1 1); do
        printf "\r   ${A}${B}  ⏰  %2d segundos...${N}  " "$i"
        sleep 1
    done
    printf "\r   ${R}${B}  ⏰  TIMEOUT — la saga decide compensar${N}          \n"

    sleep 5
    echo -e "\n${B}5. Saldo final — nadie tocó nada:${N}"; saldo; sagas
    echo -e "\n${V}   ✓ El sistema devolvió los 100 UVB automáticamente.${N}"
    echo -e "${V}   ✓ El dinero NO quedó atrapado.${N}"
    echo -e "${V}   ✓ Esto es el post-it lila del Event Storming, funcionando.${N}\n"
    ;;

# ═══════════════════════════════════════════════════════════
reset)
    reset_saldo
    echo -e "\n${V}Saldo restablecido:${N}"; saldo; echo ""
    ;;

estado)
    linea; echo -e "${C}  ESTADO ACTUAL${N}"; linea
    echo -e "\n${B}Saldo:${N}"; saldo
    echo ""; sagas
    CODE=$(curl -s -o /dev/null -w "%{http_code}" --max-time 5 "$API/swagger/index.html" 2>/dev/null)
    if [ "$CODE" = "200" ]; then
        echo -e "   Aplicación: ${V}corriendo en $API${N}\n"
    else
        echo -e "   Aplicación: ${R}NO responde${N}  →  ${B}dotnet run --urls $API${N}\n"
    fi
    ;;

*)
    echo ""
    echo -e "${C}  DEMO CLASE 3 — Saga de Transferencia${N}"
    echo ""
    echo -e "  ${B}./demo-clase3.sh feliz${N}       Camino feliz (confirma a tiempo)"
    echo -e "  ${B}./demo-clase3.sh compensar${N}   Compensación por timeout"
    echo -e "  ${B}./demo-clase3.sh reset${N}       Restablece el saldo a 5000"
    echo -e "  ${B}./demo-clase3.sh estado${N}      Muestra saldo y sagas"
    echo ""
    ;;
esac
