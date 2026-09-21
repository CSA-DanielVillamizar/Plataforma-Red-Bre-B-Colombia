#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""Que deja HOY la API en el log ante cada 401 y 403 (actividad Clase 9, squad Datos)."""
import base64, hashlib, hmac, json, sys, time, urllib.error, urllib.request

API = "http://localhost:5080"
LOG = sys.argv[1]
CUENTA = "11111111-1111-1111-1111-111111111111"
CLAVE = b"clave-de-laboratorio-no-usar-en-produccion-32+"
ROL = "http://schemas.microsoft.com/ws/2008/06/identity/claims/role"


def b64(b):
    return base64.urlsafe_b64encode(b).rstrip(b"=").decode()


def firmar(cuerpo, clave=CLAVE, alg="HS256"):
    c = b64(json.dumps({"alg": alg, "typ": "JWT"}).encode())
    p = b64(json.dumps(cuerpo).encode())
    if alg == "none":
        return "%s.%s." % (c, p)
    return "%s.%s.%s" % (c, p, b64(hmac.new(clave, ("%s.%s" % (c, p)).encode(), hashlib.sha256).digest()))


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


def tam():
    with open(LOG, "rb") as f:
        f.seek(0, 2)
        return f.tell()


def nuevo(desde):
    with open(LOG, "rb") as f:
        f.seek(desde)
        return f.read().decode("utf-8", "replace")


_, t = pedir("POST", "/token", cuerpo={"usuario": "luis.consulta", "clave": "Consulta-2026"})
consulta = json.loads(t)["token"]
ahora = int(time.time())
base = {"sub": "ana.operadora", ROL: "operador", "iss": "breb-auth", "aud": "breb-api"}
retener = "/cuentas/%s/retener?montoUVB=1" % CUENTA

casos = [
    ("401 sin token", "POST", retener, None, None),
    ("401 token vencido", "POST", retener, firmar(dict(base, exp=ahora - 60)), None),
    ("401 firma de otra clave", "POST", retener, firmar(dict(base, exp=ahora + 600), b"otra-clave-cualquiera-de-32-bytes!!"), None),
    ("401 alg none", "POST", retener, firmar(dict(base, exp=ahora + 600), alg="none"), None),
    ("403 consulta retiene", "POST", retener, consulta, None),
    ("401 /token clave equivocada", "POST", "/token", None, {"usuario": "ana.operadora", "clave": "ClaveEquivocada-XYZ"}),
    ("401 /token usuario inexistente", "POST", "/token", None, {"usuario": "no.existe", "clave": "Otra-XYZ"}),
]

RUIDO = ("MassTransit", "DbCommand", "SELECT", "FROM", "WHERE", "ORDER BY", "LIMIT", "FOR UPDATE",
         "INSERT", "UPDATE", "DELETE", "VALUES", "RETURNING", "Outbox")
for nombre, met, ruta, tok, cuerpo in casos:
    time.sleep(1.2)
    antes = tam()
    c, _ = pedir(met, ruta, tok, cuerpo)
    time.sleep(1.2)
    lineas = [l for l in nuevo(antes).splitlines()
              if l.strip() and not any(r in l for r in RUIDO) and not l.startswith(" ")]
    print("\n=== %s  (HTTP %d)" % (nombre, c))
    for l in lineas[:8]:
        print("   " + l[:230])

todo = open(LOG, encoding="utf-8", errors="replace").read()
print("\n=== FILTRACIONES EN TODO EL LOG (%d lineas)" % todo.count("\n"))
print("   'eyJ' (inicio de un JWT):      %d" % todo.count("eyJ"))
print("   'Bearer ':                     %d" % todo.count("Bearer "))
print("   'ClaveEquivocada-XYZ':         %d" % todo.count("ClaveEquivocada-XYZ"))
print("   'Consulta-2026':               %d" % todo.count("Consulta-2026"))
print("   'luis.consulta' (usuario):     %d" % todo.count("luis.consulta"))
print("   'no.existe' (usuario):         %d" % todo.count("no.existe"))
