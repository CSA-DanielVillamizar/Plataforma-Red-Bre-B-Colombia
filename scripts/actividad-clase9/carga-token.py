#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""/token bajo concurrencia: cuantos logins por segundo aguanta la API (squad Arquitectura)."""
import json, statistics, time, urllib.request
from concurrent.futures import ThreadPoolExecutor

API = "http://localhost:5080"
CUERPO = json.dumps({"usuario": "ana.operadora", "clave": "Operadora-2026"}).encode()


def login():
    t0 = time.perf_counter()
    req = urllib.request.Request(API + "/token", method="POST", data=CUERPO,
                                 headers={"Content-Type": "application/json"})
    with urllib.request.urlopen(req, timeout=60) as r:
        r.read()
    return (time.perf_counter() - t0) * 1000


login()
print("  %-12s %8s %12s %10s %10s" % ("concurrencia", "logins", "logins/s", "p50 ms", "p95 ms"))
for conc in (1, 2, 4, 8, 16):
    n = max(16, conc * 4)
    t0 = time.perf_counter()
    with ThreadPoolExecutor(conc) as ex:
        lat = sorted(ex.map(lambda _: login(), range(n)))
    dur = time.perf_counter() - t0
    print("  %-12d %8d %12.1f %10.0f %10.0f" % (conc, n, n / dur, statistics.median(lat),
                                                lat[min(int(n * 0.95), n - 1)]))
