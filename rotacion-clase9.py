#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
SECRETOS Y ROTACION - Clase 9 (Red Bre-B Colombia, 190304014-1)

Arranca Breb.Cuentas en ambiente PRODUCTION en el puerto 5085, varias veces,
con distintas configuraciones de clave, y mide que pasa:

  1. Sin clave                         -> debe negarse a arrancar
  2. Con la clave de desarrollo        -> debe negarse a arrancar
  3. Con una clave de 16 bytes         -> debe negarse a arrancar
  4. Rotacion en tres fases con claves A y B

Requiere haber compilado (dotnet build). La API normal en el 5080 no se toca.
Uso:  python rotacion-clase9.py
"""
import json
import os
import secrets
import subprocess
import sys
import time
import urllib.error
import urllib.request

AQUI = os.path.dirname(os.path.abspath(__file__))
PROYECTO = next(p for p in (os.path.join(AQUI, "Breb.Platform", "Breb.Cuentas"),
                            os.path.join(AQUI, "src", "Breb.Cuentas")) if os.path.isdir(p))
PUERTO = 5085
API = "http://localhost:%d" % PUERTO


def arrancar(clave=None, anterior=None):
    env = dict(os.environ, ASPNETCORE_ENVIRONMENT="Production")
    env.pop("Jwt__ClaveFirma", None)
    env.pop("Jwt__ClaveAnterior", None)
    if clave:
        env["Jwt__ClaveFirma"] = clave
    if anterior:
        env["Jwt__ClaveAnterior"] = anterior
    # La salida va a un ARCHIVO, no a un pipe: si nadie vacia el pipe, la app
    # se bloquea al escribir sus logs y deja de responder.
    salida = open(os.path.join(AQUI, "rotacion-clase9.log"), "w+", encoding="utf-8", errors="replace")
    proc = subprocess.Popen(
        ["dotnet", "run", "--no-build", "--no-launch-profile", "--urls", API],
        cwd=PROYECTO, env=env, stdout=salida, stderr=subprocess.STDOUT,
        # En Mac/Linux, un grupo de procesos propio para poder detener
        # 'dotnet run' Y la aplicacion que lanza (son dos procesos).
        start_new_session=(os.name != "nt"))
    proc.salida = salida
    return proc


def esperar(proc, segundos=60):
    """Devuelve (True, "") si levanto, o (False, motivo) si murio."""
    fin = time.time() + segundos
    jwt = ""
    while time.time() < fin:
        if proc.poll() is not None:
            proc.salida.seek(0)
            salida = proc.salida.read()
            motivo = [l for l in salida.splitlines() if "InvalidOperationException" in l or "Falta" in l]
            return False, (motivo[0].strip() if motivo else salida.strip().splitlines()[-1])
        try:
            urllib.request.urlopen(API + "/swagger/index.html", timeout=2)
            return True, jwt
        except Exception:
            time.sleep(1)
    return False, "timeout"


def detener(proc):
    """Detiene 'dotnet run' y la aplicacion hija, en Windows, Mac o Linux."""
    if os.name == "nt":
        subprocess.run(["taskkill", "/F", "/T", "/PID", str(proc.pid)], capture_output=True)
    else:
        import signal
        try:
            os.killpg(os.getpgid(proc.pid), signal.SIGTERM)
        except ProcessLookupError:
            pass
    proc.wait()


def login():
    req = urllib.request.Request(API + "/token", method="POST",
                                 data=b'{"usuario":"ana.operadora","clave":"Operadora-2026"}',
                                 headers={"Content-Type": "application/json"})
    with urllib.request.urlopen(req, timeout=15) as r:
        return json.loads(r.read())["token"]


def quien(token):
    req = urllib.request.Request(API + "/quien-soy", headers={"Authorization": "Bearer " + token})
    try:
        with urllib.request.urlopen(req, timeout=10) as r:
            return r.status
    except urllib.error.HTTPError as e:
        return e.code


def kid(token):
    import base64
    cab = token.split(".")[0]
    return json.loads(base64.urlsafe_b64decode(cab + "=" * (-len(cab) % 4))).get("kid")


print("=" * 78)
print("  SECRETOS Y ROTACION - Breb.Cuentas en ambiente Production, puerto %d" % PUERTO)
print("=" * 78)

for etiqueta, clave in [("1. Sin Jwt__ClaveFirma", None),
                        ("2. Con la clave de desarrollo del repositorio",
                         "clave-de-laboratorio-no-usar-en-produccion-32+"),
                        ("3. Con una clave de 16 bytes", "a" * 16)]:
    p = arrancar(clave)
    levanto, motivo = esperar(p)
    if levanto:
        detener(p)
    print("\n  %s\n    arranco: %s\n    motivo:  %s" % (etiqueta, "SI" if levanto else "NO", motivo[:150]))

A = secrets.token_urlsafe(32)
B = secrets.token_urlsafe(32)

print("\n  4. ROTACION (claves aleatorias de 256 bits, generadas en esta corrida)")
p = arrancar(A)
ok, m = esperar(p)
t_a = login()
print("    FASE 0  firma A              token A kid=%s -> /quien-soy %d" % (kid(t_a), quien(t_a)))
detener(p)

p = arrancar(B, anterior=A)
ok, m = esperar(p)
t_b = login()
print("    FASE 1  firma B, acepta A    token A -> %d   token B kid=%s -> %d"
      % (quien(t_a), kid(t_b), quien(t_b)))
detener(p)

p = arrancar(B)
ok, m = esperar(p)
print("    FASE 2  solo B               token A -> %d   token B -> %d" % (quien(t_a), quien(t_b)))
detener(p)

print("\n  Nadie tuvo que volver a iniciar sesion entre la fase 0 y la 1.")
print("  El token A deja de servir solo cuando se retira la clave A.")
print("=" * 78)
