#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
TRAZAS DISTRIBUIDAS - Clase 10 (Red Bre-B Colombia, 190304014-1)

Cuatro experimentos contra la API instrumentada y Jaeger (UI en el 16687):

  1. Camino feliz: ¿una transferencia es UNA traza?
  2. Compensacion: ¿la traza sobrevive al reloj de 15 s de la saga?
  3. Outbox con RabbitMQ caido: ¿la traza sobrevive al Outbox?
  4. ¿Hay secretos dentro de las trazas?

Requisitos:  docker compose --profile observabilidad up -d
             dotnet run --urls http://localhost:5080
Uso:         python trazas-clase10.py [puerto]
Solo biblioteca estandar.
"""
import json
import subprocess
import sys
import time
import urllib.error
import urllib.request

PUERTO = sys.argv[1] if len(sys.argv) > 1 else "5080"
API = "http://localhost:%s" % PUERTO
JAEGER = "http://localhost:16687/api/v3"
CUENTA = "11111111-1111-1111-1111-111111111111"
TIPOS = {1: "interno", 2: "servidor", 3: "cliente", 4: "productor", 5: "consumidor"}


def http(metodo, url, cuerpo=None, token=None):
    datos = json.dumps(cuerpo).encode() if cuerpo is not None else (b"" if metodo == "POST" else None)
    req = urllib.request.Request(url, method=metodo, data=datos)
    if cuerpo is not None:
        req.add_header("Content-Type", "application/json")
    if token:
        req.add_header("Authorization", "Bearer " + token)
    try:
        with urllib.request.urlopen(req, timeout=20) as r:
            return r.status, r.read().decode(), dict(r.headers)
    except urllib.error.HTTPError as e:
        return e.code, e.read().decode(), dict(e.headers)


def verificar():
    for url, que in ((API + "/swagger/index.html", "la API"), (JAEGER + "/services", "Jaeger")):
        try:
            urllib.request.urlopen(url, timeout=4)
        except Exception:
            print("\n[X] No responde %s en %s" % (que, url))
            if que == "Jaeger":
                print("    docker compose --profile observabilidad up -d\n")
            else:
                print("    dotnet run --urls http://localhost:%s   (o pase el puerto)\n" % PUERTO)
            sys.exit(1)


def token(usuario, clave):
    return json.loads(http("POST", API + "/token", {"usuario": usuario, "clave": clave})[1])["token"]


def retener(tok):
    c, cuerpo, cab = http("POST", API + "/cuentas/%s/retener?montoUVB=1" % CUENTA, token=tok)
    return json.loads(cuerpo)["transferenciaId"], cab.get("X-Trace-Id")


def traza(trace_id, esperar_spans=1, intentos=20):
    """Pide la traza a Jaeger. El exportador envia por lotes cada ~5 s."""
    spans = []
    for _ in range(intentos):
        try:
            raw = urllib.request.urlopen("%s/traces/%s" % (JAEGER, trace_id), timeout=10).read().decode()
            spans = []
            for linea in raw.splitlines():
                if not linea.strip():
                    continue
                for rs in json.loads(linea).get("result", {}).get("resourceSpans", []):
                    inst = next((a["value"]["stringValue"] for a in rs["resource"]["attributes"]
                                 if a["key"] == "service.instance.id"), "?")
                    for ss in rs.get("scopeSpans", []):
                        for s in ss.get("spans", []):
                            s["_fuente"] = ss["scope"]["name"]
                            s["_instancia"] = inst
                            spans.append(s)
            if len(spans) >= esperar_spans:
                return spans, raw
        except urllib.error.HTTPError:
            pass
        time.sleep(2)
    return spans, ""


def arbol(spans):
    t0 = min(int(s["startTimeUnixNano"]) for s in spans)
    hijos = {}
    for s in spans:
        hijos.setdefault(s.get("parentSpanId", ""), []).append(s)
    ids = {s["spanId"] for s in spans}

    def imprimir(s, nivel):
        ini = (int(s["startTimeUnixNano"]) - t0) / 1e6
        dur = (int(s["endTimeUnixNano"]) - int(s["startTimeUnixNano"])) / 1e6
        err = "  !! ERROR" if s.get("status", {}).get("code") == 2 else ""
        print("   %s%-44s %-11s %-10s +%8.0f ms  %7.1f ms%s" % (
            "  " * nivel, s["name"][:44 - 2 * nivel], TIPOS.get(s.get("kind"), "?"),
            s["_fuente"][:10], ini, dur, err))
        for h in sorted(hijos.get(s["spanId"], []), key=lambda x: int(x["startTimeUnixNano"])):
            imprimir(h, nivel + 1)

    raices = [s for s in spans if not s.get("parentSpanId") or s["parentSpanId"] not in ids]
    for r in sorted(raices, key=lambda x: int(x["startTimeUnixNano"])):
        imprimir(r, 0)
    fin = max(int(s["endTimeUnixNano"]) for s in spans)
    return len(spans), (fin - t0) / 1e9


def resumen(nombre, spans):
    n, dur = arbol(spans)
    fuentes = {}
    for s in spans:
        fuentes[s["_fuente"]] = fuentes.get(s["_fuente"], 0) + 1
    print("   -> %d spans en %.1f s - %s" % (n, dur, ", ".join("%s %d" % kv for kv in sorted(fuentes.items()))))


def psql(q):
    r = subprocess.run(["docker", "exec", "breb-postgres", "psql", "-U", "postgres", "-d", "brebcuentas",
                        "-t", "-A", "-c", q], capture_output=True, text=True)
    return r.stdout.strip()


verificar()
op = token("ana.operadora", "Operadora-2026")
banco = token("core-bancario", "CoreBancario-2026")

# ── 1. Camino feliz ──────────────────────────────────────────────────────
print("=" * 100)
print("  1. CAMINO FELIZ - retener y confirmar a los 3 s")
print("=" * 100)
tid, t_ret = retener(op)
time.sleep(3)
_, _, cab = http("POST", API + "/transferencias/%s/confirmar-abono" % tid, token=banco)
t_conf = cab.get("X-Trace-Id")
time.sleep(8)
print("  Transferencia %s" % tid)
print("\n  TRAZA de /retener       %s" % t_ret)
s1, _ = traza(t_ret, 3)
resumen("retener", s1)
print("\n  TRAZA de /confirmar-abono %s" % t_conf)
s2, _ = traza(t_conf, 3)
resumen("confirmar", s2)
print("\n  -> Una transferencia = %d trazas distintas (una por cada peticion HTTP que la toca)."
      % len({t_ret, t_conf}))

# ── 2. Compensacion ──────────────────────────────────────────────────────
print("\n" + "=" * 100)
print("  2. COMPENSACION - nadie confirma; la saga compensa a los 15 s")
print("=" * 100)
tid3, t3 = retener(op)
print("  Transferencia %s - traza %s - esperando 25 s..." % (tid3, t3))
time.sleep(25)
s3, _ = traza(t3, 8)
resumen("compensacion", s3)

# ── 3. Outbox con el broker caido ────────────────────────────────────────
print("\n" + "=" * 100)
print("  3. OUTBOX - RabbitMQ caido durante /retener")
print("=" * 100)
subprocess.run(["docker", "stop", "breb-rabbitmq"], capture_output=True)
tid4, t4 = retener(op)
time.sleep(2)
print("  Transferencia %s - traza %s" % (tid4, t4))
print("  OutboxMessage con el broker caido: %s" % psql('SELECT COUNT(*) FROM "OutboxMessage";'))
parado = time.time()
time.sleep(10)
subprocess.run(["docker", "start", "breb-rabbitmq"], capture_output=True)
for _ in range(60):
    time.sleep(2)
    if psql('SELECT COUNT(*) FROM "OutboxMessage";') == "0":
        break
print("  OutboxMessage vacio %.0f s despues de revivir el broker" % (time.time() - parado - 10))
time.sleep(25)
s4, _ = traza(t4, 5)
resumen("outbox", s4)
consumidores = [s for s in s4 if s.get("kind") == 5]
print("  -> Spans de CONSUMIDOR dentro de la traza original: %d" % len(consumidores))

# ── 4. Secretos en las trazas ────────────────────────────────────────────
print("\n" + "=" * 100)
print("  4. SECRETOS DENTRO DE LAS TRAZAS")
print("=" * 100)
_, crudo1 = traza(t_ret, 1)
_, crudo3 = traza(t3, 1)
crudo = crudo1 + crudo3
for buscado in ("dev_only_password", "Password=", "Operadora-2026", "eyJ", "Bearer"):
    print("  %-22s %d apariciones" % (buscado, crudo.count(buscado)))
cadenas = sorted({a["value"]["stringValue"] for s in s1 for a in s.get("attributes", [])
                  if a["key"] == "db.connection_string"})
for c in cadenas:
    print("  db.connection_string = %s" % c)
print("=" * 100)
