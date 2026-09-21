# Herramientas de la actividad de la Clase 9

Reproducen las mediciones de `docs/mediciones/semana-8-sesion-2-actividad-soluciones.md`.
Requieren la API corriendo (`dotnet run --urls http://localhost:5080`) y los contenedores arriba.

| Herramienta | Squad | Uso |
|---|---|---|
| `auditoria-log.py` | Datos | La salida de la API debe ir a un archivo: `dotnet run --urls http://localhost:5080 > api.log 2>&1`, y luego `python auditoria-log.py ruta/a/api.log` |
| `permisos-rabbit.py` | DevOps | `python permisos-rabbit.py` — crea un usuario temporal `breb-notificaciones`, prueba 8 operaciones y lo borra |
| `carga-token.py` | Arquitectura | `python carga-token.py` — `/token` con concurrencia 1, 2, 4, 8 y 16 |
| `pbkdf2/` | Arquitectura | `cd pbkdf2 && dotnet run -c Release` — costo de PBKDF2 con 1 000, 100 000 y 600 000 iteraciones |

Los números dependen de la máquina: lo que se compara son las **proporciones**.
