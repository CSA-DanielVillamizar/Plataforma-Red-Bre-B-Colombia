#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
ACTIVIDAD CLASE 8 - mediciones que respaldan las respuestas de los squads.

  Fintech      -> donde se corta exactamente un token al vencer (ClockSkew = 0)
  Datos        -> cuanto pesa el token si se le mete todo
  Arquitectura -> los casos de prueba 401/403, contra la API tal como esta HOY

Solo biblioteca estandar. Uso:  python actividad-clase8.py [puerto]
"""
import base64
import hashlib
import hmac
import json
import sys
import time
import urllib.error
import urllib.request

PUERTO = sys.argv[1] if len(sys.argv) > 1 else "5080"
API = "http://localhost:%s" % PUERTO
CUENTA = "11111111-1111-1111-1111-111111111111"
CLAVE = b"clave-de-laboratorio-no-usar-en-produccion-32+"
ROL = "http://schemas.microsoft.com/ws/2008/06/identity/claims/role"


def b64(b):
    return base64.urlsafe_b64encode(b).rstrip(b"=").decode()


def firmar(cuerpo):
    c = b64(json.dumps({"alg": "HS256", "typ": "JWT"}, separators=(",", ":")).encode())
    p = b64(json.dumps(cuerpo, separators=(",", ":"), ensure_ascii=False).encode())
    f = hmac.new(CLAVE, ("%s.%s" % (c, p)).encode(), hashlib.sha256).digest()
    return "%s.%s.%s" % (c, p, b64(f))


def pedir(metodo, ruta, token=None):
    req = urllib.request.Request(API + ruta, method=metodo, data=b"" if metodo == "POST" else None)
    if token:
        req.add_header("Authorization", "Bearer " + token)
    try:
        with urllib.request.urlopen(req, timeout=10) as r:
            return r.status, r.read().decode()
    except urllib.error.HTTPError as e:
        return e.code, e.read().decode()


def token_de(rol, usuario="prueba"):
    c, t = pedir("POST", "/token?usuario=%s&rol=%s" % (usuario, rol))
    return json.loads(t)["token"]


# ── FINTECH: el corte exacto ──────────────────────────────────────────────
print("=" * 78)
print("  FINTECH - donde se corta un token al vencer")
print("=" * 78)
c, t = pedir("POST", "/token?usuario=operador.despedido&rol=operador")
emitido = json.loads(t)
cuerpo = json.loads(base64.urlsafe_b64decode(emitido["token"].split(".")[1] + "=="))
print("  Token real de /token: vence %d s despues de emitido" % (cuerpo["exp"] - int(time.time())))

base = {"sub": "operador.despedido", ROL: "operador", "iss": "breb-auth", "aud": "breb-api"}
ahora = int(time.time())
corto = firmar(dict(base, exp=ahora + 3))
ruta = "/cuentas/%s/retener?montoUVB=1" % CUENTA
print("  Token que vence en 3 s:")
for seg in range(0, 7):
    objetivo = ahora + seg
    while time.time() < objetivo:
        time.sleep(0.05)
    c, _ = pedir("POST", ruta, corto)
    print("    t=%+ds respecto a la emision  (exp - t = %+d s)  ->  %d" % (seg, 3 - seg, c))

# ── DATOS: el token con todo adentro ─────────────────────────────────────
print("\n" + "=" * 78)
print("  DATOS - cuanto pesa el token si se le mete todo")
print("=" * 78)
minimo = dict(base, jti="6c1f2b1e-0000-4000-8000-000000000000", exp=ahora + 900)
todo = dict(minimo,
            cedula="1037612345",
            cuenta_id="11111111-1111-1111-1111-111111111111",
            nombre="Ana María Gómez Restrepo",
            saldo="4900.00",
            banco="NEQUI",
            llave="3001234567")
t_min, t_todo = firmar(minimo), firmar(todo)
print("  Token actual (sub, rol, jti, exp, iss, aud) : %d caracteres" % len(t_min))
print("  Con cedula, cuenta, nombre, saldo, banco, llave: %d caracteres (+%d)"
      % (len(t_todo), len(t_todo) - len(t_min)))
leido = json.loads(base64.urlsafe_b64decode(t_todo.split(".")[1] + "=="))
print("  Leido SIN clave por cualquiera:", {k: leido[k] for k in ("cedula", "saldo", "llave")})
c, _ = pedir("POST", ruta, t_todo)
print("  Y la API lo acepta igual (firma valida): %d" % c)

# ── ARQUITECTURA: la matriz de casos ─────────────────────────────────────
print("\n" + "=" * 78)
print("  ARQUITECTURA - casos de prueba contra la API de HOY")
print("=" * 78)
op, co = token_de("operador", "ana.operadora"), token_de("consulta", "luis.consulta")
vencido = firmar(dict(base, exp=int(time.time()) - 60))
partes = co.split(".")
cuerpo_co = json.loads(base64.urlsafe_b64decode(partes[1] + "=="))
cuerpo_co[ROL] = "operador"
escalado = "%s.%s.%s" % (partes[0], b64(json.dumps(cuerpo_co, separators=(",", ":")).encode()), partes[2])

_, r_op = pedir("POST", ruta, op)
tid_op = json.loads(r_op)["transferenciaId"]
_, r_x = pedir("POST", ruta, op)
tid_x = json.loads(r_x)["transferenciaId"]

casos = [
    ("A1", "POST /retener", "sin token", None, "POST", ruta, 401),
    ("A2", "POST /retener", "token vencido", vencido, "POST", ruta, 401),
    ("A3", "POST /retener", "consulta -> operador sin re-firmar", escalado, "POST", ruta, 401),
    ("A4", "POST /retener", "operador", op, "POST", ruta, 200),
    ("A5", "POST /retener", "consulta", co, "POST", ruta, 403),
    ("A6", "POST /confirmar-abono", "operador", op, "POST", "/transferencias/%s/confirmar-abono" % tid_op, 202),
    ("A7", "POST /confirmar-abono", "consulta", co, "POST", "/transferencias/%s/confirmar-abono" % tid_x, 403),
    ("A8", "GET /quien-soy", "consulta", co, "GET", "/quien-soy", 200),
    ("A9", "GET /quien-soy", "sin token", None, "GET", "/quien-soy", 401),
    ("A10", "POST /token", "pide rol administrador", None, "POST", "/token?usuario=cualquiera&rol=administrador", 400),
]
fallan = 0
print("  %-4s %-22s %-36s %8s %5s" % ("#", "Endpoint", "Quien", "Esperado", "Hoy"))
for n, ep, quien, tok, met, r, esperado in casos:
    c, _ = pedir(met, r, tok)
    marca = "ok" if c == esperado else "FALLA"
    fallan += c != esperado
    print("  %-4s %-22s %-36s %8d %5d  %s" % (n, ep, quien, esperado, c, marca))
print("\n  %d de %d casos fallan hoy." % (fallan, len(casos)))
print("  Los que fallan son exactamente la autorizacion por rol que falta.")
print("=" * 78)
