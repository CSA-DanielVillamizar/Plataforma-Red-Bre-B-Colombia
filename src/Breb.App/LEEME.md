# Breb.App — la aplicación móvil de la Red Bre-B

Proyecto .NET MAUI creado en la **Clase 14 (Semana 11)**. Es el esqueleto de lo que se entrega en el **Evaluable 3** (#28) el 21 de octubre.

---

## ⚠️ Antes de compilar: muévalo a una ruta corta

```bash
dotnet build C:\breb\Breb.App -f net10.0-android
```

**No lo compile desde una ruta larga.** `aapt2`, el compilador de recursos de Android, falla con el límite de longitud de ruta de Windows:

```
\\?\C:\Users\...\obj\Debug\net10.0-android\lp\103\jl\res\..\flat\103.flata :
    error APT2000: The device does not recognize the command. (22)
```

Medido: 155 caracteres de ruta → **40 errores**; 20 caracteres → **0 errores**. El texto del error no menciona rutas; la pista es el prefijo `\\?\`.

Si el build ya falló, el `obj/` queda con rutas que ni se pueden borrar con normalidad. Bórrelo desde una ruta corta.

---

## Qué tarda

| Operación | Medido |
|---|---|
| Primer build Android, en frío | **300,2 s** |
| Segundo build, sin cambios | **14,5 s** |
| Primer build Windows, en frío | 44,3 s |

Cinco minutos en el primer build es **normal**. Detalle en [`docs/mediciones/semana-11-maui.md`](../../docs/mediciones/semana-11-maui.md).

---

## Hablar con la API Bre-B desde el emulador

La API debe escuchar en todas las interfaces:

```bash
docker start breb-postgres breb-rabbitmq
cd ../Breb.Cuentas
dotnet run --urls http://0.0.0.0:5080
```

Y desde la app, la dirección **no** es `localhost`:

| Desde el emulador, hacia… | Resultado |
|---|---|
| `http://localhost:5080` | Connection refused |
| `http://10.0.2.2:5080` | **HTTP 200** |

Dentro del emulador, `localhost` es el emulador. La máquina anfitriona está en `10.0.2.2`.

> En un **teléfono real** `10.0.2.2` no sirve: ahí se usa la IP del computador en la red WiFi.

Comprobación rápida:

```bash
adb shell am start -a android.intent.action.VIEW -d "http://10.0.2.2:5080/swagger/index.html"
```

---

## Plataformas

| Target framework | ¿Se compila en Windows? |
|---|---|
| `net10.0-android` | Sí |
| `net10.0-windows10.0.19041.0` | Sí |
| `net10.0-ios` | No — necesita un Mac |
| `net10.0-maccatalyst` | No — necesita un Mac |

En este curso se usa **Android**, porque el Evaluable 4 exige un teléfono real.
