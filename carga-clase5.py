#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
GENERADOR DE CARGA - Clase 5 (Red Bre-B Colombia, 190304014-1)

Por que no usamos PowerShell como en la Clase 4:
  aquel script creaba un PROCESO por peticion. En Windows eso cuesta cientos
  de milisegundos, asi que el generador se saturaba antes que el servidor y
  medimos 5.2 t/s cuando el sistema real hacia 17.9. Aqui cada peticion es una
  corutina sobre un socket: sin procesos, sin hilos, sin dependencias externas.

Uso:
    python carga-clase5.py <etiqueta> <puertos> <n_cuentas> <total> <concurrencia>

Ejemplos:
    python carga-clase5.py "1 inst - 1 cuenta"  5080            1  300 40
    python carga-clase5.py "3 inst - 20 ctas"   5080,5081,5082  20 300 40

Requisito: las cuentas de prueba deben existir. Ver Clase5_Instructivo_Tecnico.md.
"""
import asyncio
import sys
import time

USO = __doc__

if len(sys.argv) != 6:
    print(USO)
    sys.exit(1)

try:
    ETIQUETA = sys.argv[1]
    PUERTOS = [p.strip() for p in sys.argv[2].split(',') if p.strip()]
    NCTAS = int(sys.argv[3])
    TOTAL = int(sys.argv[4])
    CONC = int(sys.argv[5])
except ValueError:
    print("Los ultimos tres argumentos deben ser numeros enteros.\n")
    print(USO)
    sys.exit(1)

if not PUERTOS or NCTAS < 1 or TOTAL < 1 or CONC < 1:
    print("Puertos, cuentas, total y concurrencia deben ser mayores que cero.\n")
    print(USO)
    sys.exit(1)


def cuenta(i):
    """Reparte las peticiones entre las primeras NCTAS cuentas de prueba."""
    return 'aaaaaaaa-0000-0000-0000-%012d' % ((i % NCTAS) + 1)


async def una(i, lat, errs):
    puerto = PUERTOS[i % len(PUERTOS)]
    ruta = '/cuentas/%s/retener?montoUVB=1' % cuenta(i)
    pedido = (
        'POST %s HTTP/1.1\r\n'
        'Host: localhost:%s\r\n'
        'Content-Length: 0\r\n'
        'Connection: close\r\n\r\n' % (ruta, puerto)
    ).encode()
    t0 = time.perf_counter()
    try:
        r, w = await asyncio.open_connection('127.0.0.1', int(puerto))
        w.write(pedido)
        await w.drain()
        cabecera = await r.readuntil(b'\r\n')
        await r.read()                     # drena el cuerpo
        w.close()
        codigo = int(cabecera.split()[1])
    except Exception as e:
        errs.append(repr(e)[:70])
        return
    ms = (time.perf_counter() - t0) * 1000
    if codigo == 200:
        lat.append(ms)
    else:
        errs.append('HTTP %d' % codigo)


async def preflight():
    """Falla temprano y con un mensaje util, no a mitad de la demo."""
    for p in PUERTOS:
        try:
            r, w = await asyncio.wait_for(
                asyncio.open_connection('127.0.0.1', int(p)), timeout=4)
            w.close()
        except Exception:
            print("\n[X] Nadie responde en el puerto %s\n" % p)
            print("    Levante la instancia con:")
            print("       dotnet run --urls http://localhost:%s" % p)
            print("    y espere a ver:  Bus started: rabbitmq://localhost/\n")
            sys.exit(1)


async def main():
    await preflight()

    lat, errs = [], []
    sem = asyncio.Semaphore(CONC)

    async def limitada(i):
        async with sem:
            await una(i, lat, errs)

    print("  Disparando %d transferencias, %d en paralelo, %d cuenta(s), %d instancia(s)..."
          % (TOTAL, CONC, NCTAS, len(PUERTOS)))

    t0 = time.perf_counter()
    await asyncio.gather(*(limitada(i) for i in range(TOTAL)))
    dur = time.perf_counter() - t0

    lat.sort()

    def p(q):
        if not lat:
            return 0
        return int(lat[min(int(len(lat) * q / 100), len(lat) - 1)])

    print('%-30s | %5.1fs | %7.2f t/s | p50 %5d | p95 %5d | p99 %5d | max %5d | ok %3d | err %d'
          % (ETIQUETA, dur, len(lat) / dur if dur else 0,
             p(50), p(95), p(99), int(lat[-1]) if lat else 0, len(lat), len(errs)))

    if errs:
        vistos = {}
        for e in errs:
            vistos[e] = vistos.get(e, 0) + 1
        print('     errores:', ', '.join('%s x%d' % (k, v)
                                         for k, v in sorted(vistos.items(),
                                                            key=lambda kv: -kv[1])[:3]))
        if any('500' in e for e in errs):
            print('     Un 500 masivo al escalar suele ser 53300 (too many clients).')
            print('     Revise Maximum Pool Size en la cadena de conexion.')


asyncio.run(main())
