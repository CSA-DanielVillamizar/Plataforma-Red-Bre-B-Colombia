#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
CUANTO CUESTA TRAZAR - Clase 10 (Red Bre-B Colombia, 190304014-1)

Misma aplicacion, misma carga, con y sin OpenTelemetry. Orden A-B-B-A (como
en la Semana 7) para que el calentamiento de la maquina no favorezca a nadie:

    A  trazas ENCENDIDAS
    B  trazas APAGADAS   (Otel__Habilitado=false)
    B  trazas APAGADAS
    A  trazas ENCENDIDAS

Cada arranque descarta una corrida de calentamiento. Antes de cada corrida
espera 0 sagas vivas y reinicia la base con scripts/reset-laboratorio.sql.

Requisitos: la API NO debe estar corriendo (este script la levanta en el 5080),
dotnet build hecho, contenedores arriba (y Jaeger, para la fase A).
Uso:  python costo-trazas-clase10.py
"""
import os
import re
import statistics
import subprocess
import sys
import time
import urllib.request

AQUI = os.path.dirname(os.path.abspath(__file__))
PROYECTO = next(p for p in (os.path.join(AQUI, "Breb.Platform", "Breb.Cuentas"),
                            os.path.join(AQUI, "src", "Breb.Cuentas")) if os.path.isdir(p))
SCRIPTS = os.path.join(AQUI, "scripts")
API = "http://localhost:5080"
CARGA = ["python", os.path.join(AQUI, "carga-clase5.py")]


def psql(q):
    r = subprocess.run(["docker", "exec", "breb-postgres", "psql", "-U", "postgres", "-d",
                        "brebcuentas", "-t", "-A", "-c", q], capture_output=True, text=True)
    return r.stdout.strip()


def reiniciar_base():
    for _ in range(90):
        if psql('SELECT COUNT(*) FROM "TransferenciaSagas";') == "0":
            break
        time.sleep(2)
    with open(os.path.join(SCRIPTS, "reset-laboratorio.sql"), "rb") as f:
        subprocess.run(["docker", "exec", "-i", "breb-postgres", "psql", "-U", "postgres", "-d",
                        "brebcuentas"], stdin=f, capture_output=True)


def arrancar(trazas):
    env = dict(os.environ, Otel__Habilitado="true" if trazas else "false")
    log = open(os.path.join(AQUI, "costo-trazas-clase10.log"), "w", encoding="utf-8", errors="replace")
    proc = subprocess.Popen(["dotnet", "run", "--no-build", "--urls", API], cwd=PROYECTO, env=env,
                            stdout=log, stderr=subprocess.STDOUT,
                            start_new_session=(os.name != "nt"))
    for _ in range(60):
        time.sleep(2)
        try:
            urllib.request.urlopen(API + "/swagger/index.html", timeout=3)
            break
        except Exception:
            pass
    time.sleep(5)          # que el bus termine de conectarse
    return proc


def detener(proc):
    if os.name == "nt":
        subprocess.run(["taskkill", "/F", "/T", "/PID", str(proc.pid)], capture_output=True)
    else:
        import signal
        os.killpg(os.getpgid(proc.pid), signal.SIGTERM)
    proc.wait()
    time.sleep(3)


def corrida(etiqueta):
    reiniciar_base()
    r = subprocess.run(CARGA + [etiqueta, "5080", "20", "300", "40"], capture_output=True, text=True)
    linea = r.stdout.strip().splitlines()[-1]
    tps = float(re.search(r"([\d.]+) t/s", linea).group(1))
    p95 = int(re.search(r"p95\s+(\d+)", linea).group(1))
    err = int(re.search(r"err (\d+)", linea).group(1))
    print("  %s" % linea)
    return tps, p95, err


res = {True: [], False: []}
print("=" * 110)
print("  COSTO DE TRAZAR - 20 cuentas, 300 transferencias, concurrencia 40, orden A-B-B-A")
print("=" * 110)
for fase, trazas, n in (("A", True, 1), ("B", False, 2), ("A", True, 1)):
    proc = arrancar(trazas)
    nombre = "CON trazas" if trazas else "SIN trazas"
    corrida("calentamiento (" + nombre + ")")
    for i in range(n):
        res[trazas].append(corrida("%s %s #%d" % (fase, nombre, len(res[trazas]) + 1)))
    # Detener la app con sagas en vuelo deja 300 compensaciones pendientes en
    # RabbitMQ: las procesa de golpe la siguiente instancia que arranque (nos
    # pasó: la demo siguiente empezó con 212 sagas vivas). Se espera a cero.
    for _ in range(90):
        if psql('SELECT COUNT(*) FROM "TransferenciaSagas";') == "0":
            break
        time.sleep(2)
    detener(proc)

print("\n  RESUMEN")
for trazas in (False, True):
    tps = [x[0] for x in res[trazas]]
    p95 = [x[1] for x in res[trazas]]
    print("  %-11s throughput %s  -> media %.1f t/s   p95 %s ms   errores %d" % (
        "CON trazas" if trazas else "SIN trazas", " / ".join("%.1f" % t for t in tps),
        statistics.mean(tps), " / ".join(str(p) for p in p95), sum(x[2] for x in res[trazas])))
sin = statistics.mean(x[0] for x in res[False])
con = statistics.mean(x[0] for x in res[True])
disp = max(abs(res[False][0][0] - res[False][1][0]) / sin, abs(res[True][0][0] - res[True][1][0]) / con) * 100
print("\n  Diferencia de medias: %+.1f %%   -   dispersion interna maxima: %.0f %%" % ((con - sin) / sin * 100, disp))
if abs(con - sin) / sin * 100 < disp:
    print("  -> La diferencia es MENOR que la dispersion propia: no se puede atribuir a las trazas.")
print("=" * 110)
