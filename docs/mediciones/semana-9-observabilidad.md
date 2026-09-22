# Semana 9 — Observabilidad: trazas distribuidas y correlación de logs

**Medido el 21 de septiembre de 2026.** `Breb.Cuentas` con OpenTelemetry 1.19, Jaeger 2.21, PostgreSQL 5433, RabbitMQ 5673.

## Qué cambió

| Archivo | Cambio |
|---|---|
| `src/Breb.Cuentas/Observabilidad/Telemetria.cs` | **Nuevo.** Spans de ASP.NET Core, MassTransit y Npgsql, exportados por OTLP a `Otel:Endpoint` (por defecto `http://localhost:4327`). Se apaga con `Otel__Habilitado=false` |
| `Program.cs` | `traza={TraceId}` al final de cada línea de log; cabecera `X-Trace-Id` en cada respuesta; atributo `transferencia.id` |
| Consumidores | `FondosRetenidosConsumer` y `CompensarTransferenciaConsumer` etiquetan su span con `transferencia.id` |
| `Breb.Cuentas.csproj` | OpenTelemetry 1.19 (Hosting, AspNetCore, OTLP) y `Npgsql.OpenTelemetry` 9.0.4 (Npgsql sube a 9.0.4) |
| `docker-compose.yml` | Jaeger 2.21 en el **perfil** `observabilidad`, puertos 4327 / 4328 / 16687 |
| `trazas-clase10.py` · `costo-trazas-clase10.py` | **Nuevos** |

> **Para el E1 no cambia nada.** `docker compose up -d` sigue levantando solo RabbitMQ y PostgreSQL. Verificado: con Jaeger apagado, la app arranca, la demo feliz se completa y el log no tiene **ni un** error del exportador.

## Cómo usarlo

```bash
docker compose --profile observabilidad up -d
docker port breb-jaeger          # 4317/tcp -> 0.0.0.0:4327 · 16686/tcp -> 0.0.0.0:16687
dotnet run --urls http://localhost:5080
```

UI de Jaeger: `http://localhost:16687`. Cada respuesta de la API trae `X-Trace-Id`; ese valor se busca en Jaeger y, como `traza=<TraceId>`, en los logs.

**Por qué 4327 y 16687:** en la máquina donde se preparó la clase, otro proyecto tiene su Jaeger en el 4317/16686. Enviando al 4317, las trazas llegarían al Jaeger ajeno **sin ningún error**. Es la misma trampa que el RabbitMQ de la Semana 6.

**Jaeger 2 no tiene la API v1** (`/api/services` da 404): se consulta con `/api/v3/services` y `/api/v3/traces/{traceId}`.

## Resultados (`python trazas-clase10.py`)

| Pregunta | Resultado |
|---|---|
| ¿Una transferencia es una traza? | **No: 2.** `/retener` = 19 spans; `/confirmar-abono` = 12 spans. Se unen por `transferencia.id` |
| ¿Sobrevive al reloj de 15 s de la saga? | **Sí.** Compensación: 1 traza, 38 spans, 15.2 s |
| ¿Sobrevive al Outbox con RabbitMQ caído? | **Sí.** 1 traza, 38 spans, 78.5 s; `outbox send` a +6 ms y `outbox process` a **+63 108 ms**: el hueco es el mensaje esperando en la base |
| ¿Cruza instancias? | **Sí.** Con 3 instancias, 4 de 6 trazas pasaron de una instancia a otra; los logs de una de ellas quedaron en **3 archivos** (11 + 15 + 7 líneas) |
| ¿Secretos en las trazas? | 0 contraseñas, 0 tokens. `db.connection_string` sin contraseña, pero con host, puerto, base y usuario |

### El 202 que miente

Confirmar el abono de una transferencia **inexistente** responde **202**, la traza sale toda en verde, no hay mensaje en la cola de error ni línea en el log. La confirmación se descarta en silencio. La única señal: el span `TransferenciaSagaState receive` **no tiene hijos** (en una confirmación real tiene un `process` que publica `TransferenciaCompletada`).

### La evidencia del #46

`TransferenciaSagaState_error` guardaba desde el **17 de septiembre** el `FondosRetenidos` de la retención huérfana del #46: `40001`, `MT-Fault-RetryCount = 10`, stack completo. Nadie lo había visto. Al preparar la clase llegaron **dos más** del mismo tipo durante una avalancha de compensaciones.

## ¿Cuánto cuesta trazar? (`python costo-trazas-clase10.py`)

20 cuentas, 300 transferencias, concurrencia 40, orden A-B-B-A, calentamiento descartado:

| | Corridas | Media | Dispersión entre sus corridas |
|---|---|---|---|
| Sin trazas | 95.90 · 188.26 t/s | 142.1 t/s | **65 %** |
| Con trazas | 128.73 · 127.67 t/s | 128.2 t/s | 1 % |

Diferencia de medias −9.8 %, **menor que la dispersión propia (65 %)**: **no se puede atribuir** a las trazas. Lo que sí: 1 200 transferencias trazadas, 0 errores.

## Abierto

1. Métricas (`Meter`), el tercer pilar.
2. Muestreo: hoy se guarda el 100 %, en memoria.
3. Alertas: profundidad de las colas `_error`, tiempo del Outbox, eventos que la saga descarta.
4. La confirmación descartada en silencio es un defecto de diseño de la saga, no de observabilidad.
