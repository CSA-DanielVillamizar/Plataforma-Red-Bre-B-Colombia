#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
Permisos minimos en RabbitMQ para un servicio de notificaciones que solo LEE
TransferenciaCompletada (actividad Clase 9, squad DevOps).

RabbitMQ decide cada operacion con tres expresiones regulares por usuario:
  configure: declarar/borrar colas y exchanges
  write:     publicar en un exchange · enlazar (bind) una cola  [write sobre la COLA]
  read:      consumir de una cola    · enlazar (bind)           [read sobre el EXCHANGE]
Crea un usuario temporal, prueba 8 operaciones y lo borra al final.
"""
import base64, json, time, urllib.error, urllib.parse, urllib.request, uuid

RABBIT = "http://localhost:15673/api"
API = "http://localhost:5080"
ADMIN = ("guest", "guest")
NOTI = ("breb-notificaciones", "notif-lab-2026")
COLA = "notificaciones.transferencia-completada"
EXC_OK = "Breb.Cuentas.Contratos:TransferenciaCompletada"
EXC_NO = "Breb.Cuentas.Contratos:AbonoConfirmado"
q = lambda s: urllib.parse.quote(s, safe="")


def http(metodo, url, cuerpo=None, cred=None, token=None):
    datos = json.dumps(cuerpo).encode() if cuerpo is not None else (b"" if metodo in ("POST", "PUT") else None)
    req = urllib.request.Request(url, method=metodo, data=datos, headers={"Content-Type": "application/json"})
    if cred:
        req.add_header("Authorization", "Basic " + base64.b64encode(("%s:%s" % cred).encode()).decode())
    if token:
        req.add_header("Authorization", "Bearer " + token)
    try:
        with urllib.request.urlopen(req, timeout=15) as r:
            return r.status, r.read().decode()
    except urllib.error.HTTPError as e:
        return e.code, e.read().decode()


def fila(desc, permitido, res):
    c, cuerpo = res
    ok = (c < 300) == permitido
    razon = ""
    if c >= 300:
        try:
            razon = json.loads(cuerpo).get("reason", "")[:90]
        except Exception:
            razon = cuerpo[:90]
    print("  %-3s %-56s %-9s HTTP %d %s" % ("ok" if ok else "!!", desc,
                                           "permitido" if permitido else "negado", c, razon))
    return ok


print("=" * 110)
print("  USUARIO breb-notificaciones con permisos minimos")
print("=" * 110)
perm = {"configure": r"^notificaciones\..*",
        "write": r"^notificaciones\..*",
        "read": r"^(notificaciones\..*|Breb\.Cuentas\.Contratos:TransferenciaCompletada)$"}
http("PUT", RABBIT + "/users/" + NOTI[0], {"password": NOTI[1], "tags": "management"}, ADMIN)
http("PUT", RABBIT + "/permissions/%2F/" + NOTI[0], perm, ADMIN)
for k, v in perm.items():
    print("  %-9s %s" % (k, v))
print("\n  %-3s %-56s %-9s" % ("", "Operacion", "Esperado"))

bien = 0
bien += fila("Declarar su propia cola", True,
             http("PUT", "%s/queues/%%2F/%s" % (RABBIT, q(COLA)), {"durable": True}, NOTI))
bien += fila("Enlazarla a TransferenciaCompletada", True,
             http("POST", "%s/bindings/%%2F/e/%s/q/%s" % (RABBIT, q(EXC_OK), q(COLA)), {"routing_key": ""}, NOTI))
bien += fila("Enlazarla a AbonoConfirmado (espiar otro evento)", False,
             http("POST", "%s/bindings/%%2F/e/%s/q/%s" % (RABBIT, q(EXC_NO), q(COLA)), {"routing_key": ""}, NOTI))
sobre = {"messageId": str(uuid.uuid4()), "messageType": ["urn:message:" + EXC_NO],
         "message": {"transferenciaId": str(uuid.uuid4()), "confirmadoEn": "2026-09-21T00:00:00Z"}}
bien += fila("Publicar AbonoConfirmado (el ataque de la clase)", False,
             http("POST", "%s/exchanges/%%2F/%s/publish" % (RABBIT, q(EXC_NO)),
                  {"properties": {}, "routing_key": "", "payload": json.dumps(sobre), "payload_encoding": "string"}, NOTI))
bien += fila("Leer la cola de la saga (TransferenciaSagaState)", False,
             http("POST", "%s/queues/%%2F/TransferenciaSagaState/get" % RABBIT,
                  {"count": 1, "ackmode": "ack_requeue_true", "encoding": "auto"}, NOTI))
bien += fila("Declarar una cola ajena (TransferenciaSagaState_espia)", False,
             http("PUT", "%s/queues/%%2F/TransferenciaSagaState_espia" % RABBIT, {"durable": False}, NOTI))
bien += fila("Borrar el exchange AbonoConfirmado", False,
             http("DELETE", "%s/exchanges/%%2F/%s" % (RABBIT, q(EXC_NO)), None, NOTI))

# Una transferencia real que se completa: ¿le llega a notificaciones?
_, t = http("POST", API + "/token", {"usuario": "ana.operadora", "clave": "Operadora-2026"})
op = json.loads(t)["token"]
_, t = http("POST", API + "/token", {"usuario": "core-bancario", "clave": "CoreBancario-2026"})
banco = json.loads(t)["token"]
_, r = http("POST", API + "/cuentas/11111111-1111-1111-1111-111111111111/retener?montoUVB=1", token=op)
tid = json.loads(r)["transferenciaId"]
time.sleep(3)
http("POST", API + "/transferencias/%s/confirmar-abono" % tid, token=banco)
recibido = None
for _ in range(15):
    time.sleep(1)
    c, cuerpo = http("POST", "%s/queues/%%2F/%s/get" % (RABBIT, q(COLA)),
                     {"count": 5, "ackmode": "ack_requeue_false", "encoding": "auto"}, NOTI)
    msgs = json.loads(cuerpo) if c == 200 else []
    if msgs:
        recibido = json.loads(msgs[0]["payload"])
        break
ok = bool(recibido) and recibido["message"]["transferenciaId"] == tid
bien += ok
print("  %-3s %-56s %-9s %s" % ("ok" if ok else "!!", "Recibir TransferenciaCompletada de una transf. real", "permitido",
                               ("recibido: %s" % recibido["messageType"][0]) if recibido else "NADA"))

print("\n  %d de 8 operaciones con el resultado esperado" % bien)
http("DELETE", "%s/queues/%%2F/%s" % (RABBIT, q(COLA)), None, ADMIN)
http("DELETE", RABBIT + "/users/" + NOTI[0], None, ADMIN)
print("  (cola y usuario temporales eliminados)")
