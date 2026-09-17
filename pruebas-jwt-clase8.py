#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
PRUEBAS DE SEGURIDAD JWT - Clase 8 (Red Bre-B Colombia, 190304014-1)

Ataca la API de siete formas distintas y muestra que responde a cada una.
Solo usa la libreria estandar de Python: nada que instalar.

Uso:
    python pruebas-jwt-clase8.py            (puerto 5080)
    python pruebas-jwt-clase8.py 5051       (otro puerto)
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

# La clave de firma de DESARROLLO. Desde la segunda sesion de la Semana 8 ya
# no esta en el codigo: esta en appsettings.Development.json. Este script la
# LEE DE AHI, del repositorio — que es justamente la leccion de la prueba 5:
# quien tiene la clave, fabrica tokens.
import os
CLAVE = None
for ruta in ("Breb.Platform/Breb.Cuentas/appsettings.Development.json",
             "src/Breb.Cuentas/appsettings.Development.json"):
    ruta = os.path.join(os.path.dirname(os.path.abspath(__file__)), ruta)
    if os.path.exists(ruta):
        with open(ruta, encoding="utf-8-sig") as f:
            CLAVE = json.load(f)["Jwt"]["ClaveFirma"].encode()
        break
if CLAVE is None:
    print("[X] No se encontro appsettings.Development.json con Jwt:ClaveFirma")
    sys.exit(1)


def b64url_decodificar(s):
    return base64.urlsafe_b64decode(s + "=" * (-len(s) % 4))


def b64url_codificar(b):
    return base64.urlsafe_b64encode(b).rstrip(b"=").decode()


def firmar(cabecera, cuerpo, clave):
    """Arma un JWT HS256 a mano: base64(cabecera).base64(cuerpo).firma"""
    c = b64url_codificar(json.dumps(cabecera, separators=(",", ":")).encode())
    p = b64url_codificar(json.dumps(cuerpo, separators=(",", ":")).encode())
    firma = hmac.new(clave, ("%s.%s" % (c, p)).encode(), hashlib.sha256).digest()
    return "%s.%s.%s" % (c, p, b64url_codificar(firma))


def pedir(metodo, ruta, token=None):
    req = urllib.request.Request(API + ruta, method=metodo, data=b"" if metodo == "POST" else None)
    if token:
        req.add_header("Authorization", "Bearer " + token)
    try:
        with urllib.request.urlopen(req, timeout=10) as r:
            return r.status, r.read().decode()
    except urllib.error.HTTPError as e:
        return e.code, e.read().decode()


def fila(n, nombre, esperado, obtenido):
    ok = "OK " if obtenido == esperado else "!! "
    print("  %s %d. %-46s esperado %d  ->  obtuvo %d" % (ok, n, nombre, esperado, obtenido))
    return obtenido == esperado


print("=" * 78)
print("  PRUEBAS DE SEGURIDAD JWT contra %s" % API)
print("=" * 78)

# ── Conseguir un token legitimo ──────────────────────────────────────────
req = urllib.request.Request(API + "/token", method="POST",
                             data=b'{"usuario":"ana.operadora","clave":"Operadora-2026"}',
                             headers={"Content-Type": "application/json"})
try:
    with urllib.request.urlopen(req, timeout=10) as r:
        codigo, texto = r.status, r.read().decode()
except urllib.error.HTTPError as e:
    codigo, texto = e.code, e.read().decode()
if codigo != 200:
    print("\n[X] /token respondio %d. ¿Corre la version de la Semana 8?\n" % codigo)
    sys.exit(1)
TOKEN = json.loads(texto)["token"]
cab_b64, cuerpo_b64, firma_b64 = TOKEN.split(".")

# ── La leccion central: el token se LEE sin ninguna clave ────────────────
print("\n  EL TOKEN (%d caracteres):" % len(TOKEN))
print("    %s..." % TOKEN[:70])
print("\n  SU CUERPO, DECODIFICADO SIN NINGUNA CLAVE:")
cuerpo = json.loads(b64url_decodificar(cuerpo_b64))
for k, v in cuerpo.items():
    print("    %-58s = %s" % (k, v))
print("\n  -> Cualquiera que intercepte el token lee todo esto. NO esta cifrado.")

resultados = []
print("\n  ATAQUES:")

# 1. Sin token
c, _ = pedir("POST", "/cuentas/%s/retener?montoUVB=1" % CUENTA)
resultados.append(fila(1, "Sin token", 401, c))

# 2. Token legitimo
c, _ = pedir("POST", "/cuentas/%s/retener?montoUVB=1" % CUENTA, TOKEN)
resultados.append(fila(2, "Token legitimo", 200, c))

# 3. Alterar el cuerpo sin re-firmar: subirse el rol a administrador
cuerpo_alterado = dict(cuerpo)
rol_clave = [k for k in cuerpo if k.endswith("/role") or k == "role"][0]
cuerpo_alterado[rol_clave] = "administrador"
nuevo_cuerpo = b64url_codificar(json.dumps(cuerpo_alterado, separators=(",", ":")).encode())
token_alterado = "%s.%s.%s" % (cab_b64, nuevo_cuerpo, firma_b64)
c, _ = pedir("POST", "/cuentas/%s/retener?montoUVB=1" % CUENTA, token_alterado)
resultados.append(fila(3, "Cuerpo alterado (rol -> administrador)", 401, c))

# 4. Firmado con OTRA clave
falso = firmar({"alg": "HS256", "typ": "JWT"}, cuerpo, b"otra-clave-cualquiera-de-32-caracteres!!")
c, _ = pedir("POST", "/cuentas/%s/retener?montoUVB=1" % CUENTA, falso)
resultados.append(fila(4, "Firmado con otra clave", 401, c))

# 5. Firmado con LA clave correcta: el atacante tiene el secreto
ahora = int(time.time())
fabricado = dict(cuerpo, sub="atacante", exp=ahora + 600)
# Con la clave, el atacante se pone el rol que la ruta exige. Desde que
# /retener pide "operador", eso es lo que se pone.
fabricado[rol_clave] = "operador"
token_fabricado = firmar({"alg": "HS256", "typ": "JWT"}, fabricado, CLAVE)
c, _ = pedir("POST", "/cuentas/%s/retener?montoUVB=1" % CUENTA, token_fabricado)
resultados.append(fila(5, "FABRICADO con la clave robada", 200, c))

# 6. Vencido hace 60 segundos (ClockSkew = 0)
vencido = dict(cuerpo, exp=ahora - 60)
token_vencido = firmar({"alg": "HS256", "typ": "JWT"}, vencido, CLAVE)
c, _ = pedir("POST", "/cuentas/%s/retener?montoUVB=1" % CUENTA, token_vencido)
resultados.append(fila(6, "Vencido hace 60 s (ClockSkew = 0)", 401, c))

# 7. Audiencia equivocada: token emitido para otro servicio
otra_aud = dict(cuerpo, aud="breb-reportes", exp=ahora + 600)
token_otra_aud = firmar({"alg": "HS256", "typ": "JWT"}, otra_aud, CLAVE)
c, _ = pedir("POST", "/cuentas/%s/retener?montoUVB=1" % CUENTA, token_otra_aud)
resultados.append(fila(7, "Emitido para otro servicio (aud)", 401, c))

# 8. alg = none: el ataque clasico, sin firma
sin_firma = "%s.%s." % (
    b64url_codificar(json.dumps({"alg": "none", "typ": "JWT"}).encode()), cuerpo_b64)
c, _ = pedir("POST", "/cuentas/%s/retener?montoUVB=1" % CUENTA, sin_firma)
resultados.append(fila(8, "alg: none (sin firma)", 401, c))

# ── Lo que el servidor cree que es el atacante ───────────────────────────
c, texto = pedir("GET", "/quien-soy", token_fabricado)
print("\n  /quien-soy CON EL TOKEN FABRICADO:")
print("    %s" % texto)

print("\n  %d de %d respondieron lo esperado." % (sum(resultados), len(resultados)))
print("\n  La prueba 5 NO es un fallo del JWT: es lo que pasa cuando la clave")
print("  se filtra. La clave de DESARROLLO esta en appsettings.Development.json,")
print("  en este repositorio, a proposito. Fuera de Development la app se niega")
print("  a arrancar con ella: la real llega por variable de entorno o gestor de secretos.")
print("=" * 78)
