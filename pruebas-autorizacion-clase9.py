#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
PRUEBAS DE AUTORIZACION - Clase 9 (Red Bre-B Colombia, 190304014-1)

  1. Cuanto cuesta EMITIR un token frente a VALIDARLO.
  2. La matriz de casos 401 / 403 — la de la actividad de la Clase 8, ampliada.

Solo biblioteca estandar. Uso:  python pruebas-autorizacion-clase9.py [puerto]
"""
import base64
import hashlib
import hmac
import json
import os
import statistics
import sys
import time
import urllib.error
import urllib.request

PUERTO = sys.argv[1] if len(sys.argv) > 1 else "5080"
API = "http://localhost:%s" % PUERTO
CUENTA = "11111111-1111-1111-1111-111111111111"
ROL = "http://schemas.microsoft.com/ws/2008/06/identity/claims/role"

# Usuarios de LABORATORIO (documentados, no son secretos)
OPERADOR = ("ana.operadora", "Operadora-2026")
CONSULTA = ("luis.consulta", "Consulta-2026")
BANCO = ("core-bancario", "CoreBancario-2026")

CLAVE = None
for r in ("Breb.Platform/Breb.Cuentas/appsettings.Development.json",
          "src/Breb.Cuentas/appsettings.Development.json"):
    r = os.path.join(os.path.dirname(os.path.abspath(__file__)), r)
    if os.path.exists(r):
        with open(r, encoding="utf-8-sig") as f:
            CLAVE = json.load(f)["Jwt"]["ClaveFirma"].encode()
        break


def b64(b):
    return base64.urlsafe_b64encode(b).rstrip(b"=").decode()


def firmar(cuerpo):
    c = b64(json.dumps({"alg": "HS256", "typ": "JWT"}, separators=(",", ":")).encode())
    p = b64(json.dumps(cuerpo, separators=(",", ":")).encode())
    return "%s.%s.%s" % (c, p, b64(hmac.new(CLAVE, ("%s.%s" % (c, p)).encode(), hashlib.sha256).digest()))


def pedir(metodo, ruta, token=None, cuerpo=None):
    datos = json.dumps(cuerpo).encode() if cuerpo is not None else (b"" if metodo == "POST" else None)
    req = urllib.request.Request(API + ruta, method=metodo, data=datos)
    if cuerpo is not None:
        req.add_header("Content-Type", "application/json")
    if token:
        req.add_header("Authorization", "Bearer " + token)
    try:
        with urllib.request.urlopen(req, timeout=15) as r:
            return r.status, r.read().decode()
    except urllib.error.HTTPError as e:
        return e.code, e.read().decode()


def login(usuario, clave, ruta="/token"):
    return pedir("POST", ruta, cuerpo={"usuario": usuario, "clave": clave})


def token(credencial):
    c, t = login(*credencial)
    if c != 200:
        print("\n[X] /token respondio %d para %s. ¿Corre la version de la Semana 8 (2a sesion)?\n"
              % (c, credencial[0]))
        sys.exit(1)
    return json.loads(t)["token"]


def mediana_ms(fn, n=15):
    fn()  # calentamiento
    tiempos = []
    for _ in range(n):
        t0 = time.perf_counter()
        fn()
        tiempos.append((time.perf_counter() - t0) * 1000)
    return statistics.median(tiempos)


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


print("=" * 80)
print("  PRUEBAS DE AUTORIZACION contra %s" % API)
print("=" * 80)

op, co, ba = token(OPERADOR), token(CONSULTA), token(BANCO)

# ── 1. Emitir contra validar ─────────────────────────────────────────────
print("\n  EMITIR CONTRA VALIDAR (mediana de 15 llamadas)")
m_ok = mediana_ms(lambda: login(*OPERADOR))
m_mal = mediana_ms(lambda: login("ana.operadora", "equivocada"))
m_nadie = mediana_ms(lambda: login("no.existe", "loquesea"))
m_val = mediana_ms(lambda: pedir("GET", "/quien-soy", op))
print("    POST /token  credenciales correctas    %7.1f ms" % m_ok)
print("    POST /token  contrasena equivocada     %7.1f ms" % m_mal)
print("    POST /token  usuario inexistente       %7.1f ms" % m_nadie)
print("    GET  /quien-soy  (solo validar token)  %7.1f ms" % m_val)
print("    -> emitir cuesta %.0fx lo que validar. Y los tres /token tardan lo mismo:"
      % (m_ok / m_val if m_val else 0))
print("       el tiempo no revela si el usuario existe.")

# ── 2. La matriz ─────────────────────────────────────────────────────────
ahora = int(time.time())
vencido = firmar({"sub": "ana.operadora", ROL: "operador", "iss": "breb-auth",
                  "aud": "breb-api", "exp": ahora - 60}) if CLAVE else "x.y.z"
p = co.split(".")
cuerpo_co = json.loads(base64.urlsafe_b64decode(p[1] + "=="))
cuerpo_co[ROL] = "operador"
escalado = "%s.%s.%s" % (p[0], b64(json.dumps(cuerpo_co, separators=(",", ":")).encode()), p[2])

retener = "/cuentas/%s/retener?montoUVB=1" % CUENTA
saldo = "/cuentas/%s/saldo" % CUENTA
_, r1 = pedir("POST", retener, op)
tid1 = json.loads(r1)["transferenciaId"]
_, r2 = pedir("POST", retener, op)
tid2 = json.loads(r2)["transferenciaId"]

# A10: luis.consulta intenta pedirse el rol operador por la URL, como en la Clase 8
c10, t10 = login(*CONSULTA, ruta="/token?rol=operador")
tok10 = json.loads(t10)["token"] if c10 == 200 else None
rol10 = json.loads(t10).get("rol") if c10 == 200 else None

casos = [
    ("A1",  "POST /retener",         "sin token",                      "POST", retener, None, 401),
    ("A2",  "POST /retener",         "token vencido",                  "POST", retener, vencido, 401),
    ("A3",  "POST /retener",         "consulta->operador sin re-firmar", "POST", retener, escalado, 401),
    ("A4",  "POST /retener",         "operador",                       "POST", retener, op, 200),
    ("A5",  "POST /retener",         "consulta",                       "POST", retener, co, 403),
    ("A6",  "POST /confirmar-abono", "banco",                          "POST", "/transferencias/%s/confirmar-abono" % tid1, ba, 202),
    ("A7",  "POST /confirmar-abono", "consulta",                       "POST", "/transferencias/%s/confirmar-abono" % tid2, co, 403),
    ("A8",  "POST /confirmar-abono", "operador",                       "POST", "/transferencias/%s/confirmar-abono" % tid2, op, 403),
    ("A9",  "GET /saldo",            "consulta",                       "GET",  saldo, co, 200),
    ("A10", "POST /retener",         "token de consulta que pidio ?rol=operador", "POST", retener, tok10, 403),
    ("A11", "GET /saldo",            "banco",                          "GET",  saldo, ba, 403),
    ("A12", "GET /saldo",            "sin token",                      "GET",  saldo, None, 401),
]

print("\n  MATRIZ DE CASOS")
print("  %-4s %-22s %-42s %8s %6s" % ("#", "Endpoint", "Quien", "Esperado", "Obtuvo"))
bien = 0
for n, ep, quien, met, ruta, tok, esperado in casos:
    c, _ = pedir(met, ruta, tok)
    ok = c == esperado
    bien += ok
    print("  %-4s %-22s %-42s %8d %6d  %s" % (n, ep, quien, esperado, c, "ok" if ok else "FALLA"))

print("\n  Credenciales en /token:")
for etiqueta, u, cl, esp in [("A13 contrasena equivocada", "ana.operadora", "equivocada", 401),
                             ("A14 usuario inexistente", "no.existe", "loquesea", 401)]:
    c, _ = login(u, cl)
    bien += c == esp
    print("  %-48s esperado %d  obtuvo %d  %s" % (etiqueta, esp, c, "ok" if c == esp else "FALLA"))

print("\n  A10 en detalle: luis.consulta pidio ?rol=operador y el servidor emitio rol = %s" % rol10)
print("\n  %d de %d casos respondieron lo esperado." % (bien, len(casos) + 2))
print("=" * 80)
