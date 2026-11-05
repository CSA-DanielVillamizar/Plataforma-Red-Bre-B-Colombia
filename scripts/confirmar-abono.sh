#!/bin/bash
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
#  es la autorizacion de la Semana 8 haciendo su trabajo. Si la app del
#  pagador pudiera confirmar su propio abono, la saga no serviria para nada
#  — estaria certificando que llego plata a un banco con el que nunca
#  hablo. El que confirma es OTRO actor, con otra identidad.
#
#  Los dos finales dejan el RETENIDO en 0. Lo que los distingue es el
#  DISPONIBLE:
#      confirmada  ->  Liquidada = true   disponible 4900   la plata SALIO
#      compensada  ->  Liberada  = true   disponible 5000   la plata VOLVIO
#
#  USO
#      ./scripts/confirmar-abono.sh <transferenciaId> [url-de-la-api]
# ─────────────────────────────────────────────────────────────────────────────

set -euo pipefail

TRANSFERENCIA="${1:?Falta el id de la transferencia (la llave que muestra la app)}"
API="${2:-http://127.0.0.1:5080}"
CUENTA="11111111-1111-1111-1111-111111111111"

titulo() { printf '\n══════ %s \n' "$1"; }

token() {
  curl -s -X POST "$API/token" -H 'Content-Type: application/json' \
       -d "{\"usuario\":\"$1\",\"clave\":\"$2\"}"
}

campo() { grep -oE "\"$2\"[: ]*[^,}]+" <<<"$1" | head -1 | sed -E 's/.*[: ]//; s/"//g'; }

# ── 0 · Token del banco destino (identidad de SERVICIO) ──────────────────────
titulo "0 · token de core-bancario"
RESP_BANCO=$(token "core-bancario" "CoreBancario-2026")
TOKEN_BANCO=$(campo "$RESP_BANCO" token)
ROL=$(campo "$RESP_BANCO" rol)
echo "  rol : $ROL"
[ "$ROL" = "banco" ] || echo "  ⚠ rol inesperado: /confirmar-abono va a dar 403, y hace bien."

# ── 1 · Saldo ANTES ──────────────────────────────────────────────────────────
# Con el token del banco NO se pueden leer saldos: LecturaCuentas admite
# "operador" y "consulta", no "banco". Cada identidad puede lo suyo y nada
# mas, asi que para MIRAR se usa un token de consulta.
titulo "1 · saldo antes"
TOKEN_LECTURA=$(campo "$(token "luis.consulta" "Consulta-2026")" token)
ANTES=$(curl -s "$API/cuentas/$CUENTA/saldo" -H "Authorization: Bearer $TOKEN_LECTURA")
DISP_ANTES=$(campo "$ANTES" saldoDisponible)
RET_ANTES=$(campo "$ANTES" saldoRetenido)
echo "  disponible : $DISP_ANTES"
echo "  retenido   : $RET_ANTES"

if [ "$RET_ANTES" = "0" ] || [ "$RET_ANTES" = "0.00" ]; then
  echo "  ⚠ No hay nada retenido: o la saga ya compenso (pasaron los 15 s), o"
  echo "    esta transferencia nunca se hizo. En la demo hay que correr esto"
  echo "    DENTRO de los 15 segundos."
fi

# ── 2 · Confirmar ────────────────────────────────────────────────────────────
titulo "2 · POST /transferencias/$TRANSFERENCIA/confirmar-abono"
CODIGO=$(curl -s -o /dev/null -w '%{http_code}' \
         -X POST "$API/transferencias/$TRANSFERENCIA/confirmar-abono" \
         -H "Authorization: Bearer $TOKEN_BANCO")
echo "  HTTP $CODIGO"
echo
echo "  202 significa 'recibi el aviso', NO 'ya quedo'. El endpoint publica"
echo "  AbonoConfirmado y devuelve; el trabajo lo hace la saga por el broker,"
echo "  de forma asincrona. Por eso hay que esperar antes de mirar el saldo."

# ── 3 · Saldo DESPUES ────────────────────────────────────────────────────────
titulo "3 · saldo despues (3 s para que la saga procese)"
sleep 3
DESPUES=$(curl -s "$API/cuentas/$CUENTA/saldo" -H "Authorization: Bearer $TOKEN_LECTURA")
DISP_DESPUES=$(campo "$DESPUES" saldoDisponible)
RET_DESPUES=$(campo "$DESPUES" saldoRetenido)
echo "  disponible : $DISP_DESPUES"
echo "  retenido   : $RET_DESPUES"

# ── 4 · Veredicto ────────────────────────────────────────────────────────────
# CUIDADO CON LA LINEA BASE — este veredicto estuvo mal escrito y la primera
# medicion lo delato.
#
# El error era comparar "disponible antes de confirmar" contra "despues". No
# sirve: cuando se toma la primera foto, /retener YA bajo el disponible de
# 5000 a 4900. En el camino FELIZ el disponible no cambia entre las dos
# fotos, asi que un script que busca una BAJADA concluye lo contrario de lo
# que paso.
#
# Tomando como base la foto posterior a la retencion:
#     completada  ->  retenido a 0  ·  disponible SE QUEDA igual
#     compensada  ->  retenido a 0  ·  disponible SUBE en el monto retenido
titulo "4 · que paso"

VEREDICTO=$(awk -v da="$DISP_ANTES" -v ra="$RET_ANTES" -v dd="$DISP_DESPUES" -v rd="$RET_DESPUES" '
  BEGIN {
    tol = 0.005
    # Si al empezar no habia retencion viva, este script no hizo NADA: el
    # evento llego y no habia nada que liquidar. Sin esta rama el veredicto
    # diria "completada" —disponible igual, retenido 0— y seria mentira.
    if (ra <= tol)                      { print "nada" }
    else if (rd > tol)                  { print "retenido" }
    else if ((dd-da) < tol && (da-dd) < tol) { print "completada" }
    else if (((da+ra)-dd) < tol && (dd-(da+ra)) < tol) { print "compensada" }
    else                                { print "raro" }
  }')

case "$VEREDICTO" in
  completada)
    echo "  ✓ TRANSFERENCIA COMPLETADA"
    echo "    El retenido paso de $RET_ANTES a 0 y el disponible se QUEDO en"
    echo "    $DISP_DESPUES: la plata salio. En la base: Liquidada = true." ;;
  compensada)
    echo "  ✗ COMPENSADA, no completada"
    echo "    El disponible SUBIO de $DISP_ANTES a $DISP_DESPUES: la plata volvio."
    echo "    Gano el reloj de la saga. En la base: Liberada = true." ;;
  retenido)
    echo "  … todavia hay $RET_DESPUES retenidos: el broker puede ir con retraso." ;;
  nada)
    echo "  ⊘ NO HABIA NADA QUE CONFIRMAR"
    echo "    Al empezar no habia retencion viva, asi que este script no cambio"
    echo "    nada: el evento llego y no habia fondos que liquidar. O la saga ya"
    echo "    compenso —pasaron los 15 s— o esa transferencia nunca existio."
    echo "    Esto NO es una transferencia completada." ;;
  *)
    echo "  ? El saldo no cuadra con ninguno de los dos finales."
    echo "    antes $DISP_ANTES/$RET_ANTES · despues $DISP_DESPUES/$RET_DESPUES"
    echo "    Probablemente hay mas de una transferencia viva sobre la misma cuenta." ;;
esac

echo
echo "  Donde los dos casos se distinguen sin ambiguedad:"
echo "    docker exec breb-postgres psql -U postgres -d brebcuentas \\"
echo "      -c 'SELECT \"TransferenciaId\",\"Liberada\",\"Liquidada\" FROM \"Retenciones\";'"
echo
