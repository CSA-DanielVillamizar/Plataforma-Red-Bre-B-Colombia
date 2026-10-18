# Clase 10 — Actividad por squads: entregas, correcciones y cierre

## Observabilidad: trazas distribuidas y correlación de logs · Red Bre-B Colombia (190304014-1)
### Dictada el lunes 28 de septiembre de 2026

Este documento recoge **lo que entregó cada squad**, señala **qué hay que corregir y por qué** —con los números medidos en clase— y deja la respuesta como debió quedar.

**El nivel de las cuatro entregas fue alto.** Las tres primeras convergen en el mismo diagnóstico y la cuarta lo ordena en una arquitectura. Las correcciones que siguen no cambian el rumbo de ninguna: ajustan magnitudes, corrigen un mecanismo que no funciona como se creía, y completan lo que faltaba.

---

## 1 · DevOps, Infra y Cloud — el muestreo

> **Encargo:** una compensación genera 38 spans; 300 transferencias, unos 11 000. Jaeger los guarda en memoria. Diseñen el muestreo y cómo garantizar que las trazas con error se guarden siempre.

### Lo que entregaron

Un criterio de selección: **los errores se guardan siempre**, y de las operaciones que salen bien **se guarda 1 de cada 11 al azar**, porque miles de notas que dicen "todo salió bien" no aportan y llenan la libreta.

**La intención es correcta y está bien argumentada**: el valor de una traza no es uniforme, y el presupuesto se gasta donde hay algo que investigar.

### Lo que hay que corregir

**a) La magnitud.** No son "miles de anotaciones" por transferencia. Medido: **19 spans** en el camino feliz y **38** en una compensación. Trescientas transferencias son unos **11 000 spans**, no millones. Importa porque el umbral se calcula con el número real.

**b) El mecanismo no hace lo que ustedes quieren.** Y esto es lo central: un muestreo probabilístico corriente decide **al comienzo** de la traza, cuando todavía no se sabe si va a fallar. **Es imposible "guardar siempre los errores" decidiendo al inicio.**

| Tipo | Cuándo decide | ¿Puede garantizar los errores? |
|---|---|---|
| **Head sampling** (en la aplicación, probabilístico) | Al crear la traza | **No** |
| **Tail sampling** (en un Collector, tras ver la traza completa) | Al terminar | **Sí** |

La regla que diseñaron —errores siempre, éxitos por muestra— **solo se puede implementar en un Collector con tail sampling**, que es justo lo que propuso el squad de Arquitectura. En la aplicación se deja `AlwaysOn` y la decisión se delega.

**c) La decisión se propaga y debe ser coherente.** El último campo del `traceparent` (`…-01`) es el bit de muestreo. Si cada servicio decide por su cuenta, quedan trazas partidas: unos tramos guardados y otros no. Se usa `ParentBased` para respetar la decisión de quien inició.

**d) El número, con presupuesto en vez de intuición.** "1 de cada 11" es 9.09 %, y no está mal, pero conviene derivarlo:

```
spans/día = transferencias/día × spans por transferencia
p = presupuesto de spans / spans totales
```

Con 300 transferencias × 38 spans = 11 400 spans/día. Para un presupuesto de 2 000 spans/día → **p ≈ 17 %**. Con 300 000 transferencias/día, ese mismo presupuesto pide **p ≈ 0.017 %**. El porcentaje no es una preferencia: sale de la cuenta.

**e) Muestrear trazas no debe tocar métricas ni logs.** Si los percentiles de latencia se calculan contando trazas muestreadas, quedan sesgados. Los números agregados salen de **métricas** (todas las operaciones), y las trazas quedan para investigar casos.

**f) El muestreo no resuelve la pérdida por reinicio.** Jaeger está aquí con almacenamiento **en memoria**: al reiniciar el contenedor se pierde todo, se haya muestreado o no. Para retención hay que cambiar el almacenamiento (Badger, Elasticsearch, Cassandra) o exportar a Tempo.

> **Evidencia del día:** al terminar la clase, la cola `TransferenciaSagaState_error` quedó en **0 mensajes**. RabbitMQ registra `get_no_ack: 3`: los tres mensajes se leyeron con el modo que no los devuelve. **La evidencia se destruyó al mirarla.** Eso refuerza el punto: la prueba de un fallo no puede vivir solo en la cola.

### La respuesta como debió quedar

| Capa | Decisión |
|---|---|
| Aplicación | `AlwaysOn` + `ParentBased`: produce todo y respeta la decisión del que inició |
| Collector | **Tail sampling**: 100 % de trazas con error, con span de más de N segundos, o que tocaron una cola `_error`; el resto, el porcentaje que salga del presupuesto |
| Almacenamiento | Persistente, con retención por política (por ejemplo 7 días lo normal, 90 días lo marcado como incidente) |
| Evidencia de fallos | Exportada fuera de la cola: copia del mensaje fallido con sus cabeceras a almacenamiento duradero |

---

## 2 · Datos, Tuning y Observabilidad — las tres alertas

> **Encargo:** el mensaje del #46 lleva días en la cola de error sin que nadie lo vea. Diseñen tres alertas: qué se mide, qué umbral y por qué ese.

### Lo que entregaron

1. **Presencia en cola de error**: > 0 mensajes durante 5 minutos. *Si llegó ahí, ya fallaron todos los reintentos.*
2. **Antigüedad del mensaje más viejo**: > 1 hora → escala a incidente crítico.
3. **Sagas y dinero atrapado**: transferencias estancadas o en `OutboxMessage` por más de 2 minutos.

**Las tres apuntan al lugar correcto**, y la primera es **exactamente** la alerta que habría avisado el 17 de septiembre. La segunda —escalar por antigüedad— es una idea madura: distingue "falló algo" de "nadie está atendiendo".

### Lo que hay que corregir

**a) "El seguro contra los 46 días".** El **46 es el número del Issue**, no una cantidad de días. El mensaje llevaba **4 días** cuando se preparó la clase y **11** el día de la clase. La alerta sigue siendo válida; el nombre hay que cambiarlo.

**b) La alerta 1 debe cubrir todas las colas de error, no una.** Hoy existe solo `TransferenciaSagaState_error` (medido), pero MassTransit crea una por endpoint: aparecerán `FondosRetenidos_error` y `CompensarTransferencia_error` cuando fallen sus consumidores. La regla se escribe sobre el patrón `*_error`, no sobre un nombre.

**c) Cómo se mide, medido hoy:**

| Necesidad | Estado real |
|---|---|
| Métricas de RabbitMQ por Prometheus | **No disponible**: `/metrics` responde **404**; hay que habilitar el plugin `rabbitmq_prometheus` |
| Antigüedad del mensaje más viejo | **Sí es posible**: el campo `head_message_timestamp` existe en la API de administración (verificado), siempre que los mensajes lleven timestamp |

Sin una de las dos vías, la alerta 2 no es implementable. Eso es parte del diseño, no un detalle.

**d) Los umbrales de la alerta 3 son demasiado apretados.** Medido:

| Situación | Tiempo real medido |
|---|---|
| Compensación completa, sistema en reposo | ~16 s |
| Liberación de retenciones bajo 300 transferencias | ~100 s (máquina de un squad, el día del E1) |
| `OutboxMessage` vaciándose tras revivir el broker | 29 s · 51 s · **98 s** en tres corridas distintas |

Con "> 2 minutos" sobre el Outbox, **cada reinicio de RabbitMQ genera una alarma**, y una alerta que grita cuando todo está bien deja de leerse. Propuesta:

| Alerta | Umbral propuesto | Por qué |
|---|---|---|
| Saga sin cerrar | **> 5 min** | Cuatro veces el peor caso medido bajo carga |
| `OutboxMessage` sin entregar | **> 5 min** | El peor caso medido tras un reinicio fue 98 s |
| Cola `*_error` | > 0 sostenido **5 min** | Correcto tal como lo propusieron |

**e) Los nombres de los estados.** No existe "Iniciada". Los estados son **`EsperandoConfirmacion`** y **`Compensando`**. Una alerta con un nombre de estado inexistente nunca dispara: es el peor tipo de alerta, la que parece que existe.

**f) Falta la que toca el dinero directamente.** Ninguna de las tres detecta una **retención viva sin saga** — el caso del #46, donde el dinero queda retenido para siempre. Esa consulta ya existe desde la Semana 6:

```sql
SELECT COUNT(*) FROM "Retenciones" r
LEFT JOIN "TransferenciaSagas" s ON s."CorrelationId" = r."TransferenciaId"
WHERE NOT r."Liberada" AND s."CorrelationId" IS NULL
  AND r."CreadaEn" < (now() AT TIME ZONE 'UTC') - interval '1 hour';
```

> **Cuidado con el falso positivo:** una transferencia **completada** también queda sin liberar y sin saga. Mientras no exista el estado `Debitada` que propuso Fintech en la Clase 9, esta alerta necesita excluirlas, o alertará por transferencias sanas.

---

## 3 · Fintech & Core — el evento que no encuentra su saga

> **Encargo:** una confirmación de una transferencia inexistente responde 202 y se descarta en silencio. Diseñen qué debe hacer la saga con un evento sin instancia, y qué debería responder `/confirmar-abono`.

### Lo que entregaron

Dos casos distintos:
- **Llegó tarde** (la saga ya cerró): se ignora por idempotencia, **pero se deja un log claro**: *"llegó esto tarde, pero la saga ya estaba cerrada"*. Se acabó barrerlo bajo la alfombra.
- **El id no existe** (inventado, ataque de repetición o bug de otro servicio): **no se trata igual**; se registra como **evento huérfano** y dispara alerta.

**Distinguir esos dos casos es exactamente lo correcto**, y el paralelo con las retenciones huérfanas del #23 es acertado: el mismo patrón, aplicado a los eventos.

### Lo que hay que corregir

**a) Hoy el sistema no puede distinguirlos.** La saga borra su fila al terminar (`SetCompletedWhenFinalized()`). Una transferencia que se completó hace un minuto y una que nunca existió **se ven idénticas**: en los dos casos no hay instancia. Su propia distinción exige, primero, **dejar rastro**:

| Opción | Costo |
|---|---|
| El estado `Debitada` en `Retencion` (lo que ustedes mismos propusieron en la Clase 9) | Una columna y una migración |
| Conservar la saga finalizada con un TTL (por ejemplo 24 h) y purgarla después | Crece la tabla; hay que purgar |

Sin una de las dos, "llegó tarde" no es distinguible de "nunca existió".

**b) Hoy no se registra nada, ni siquiera el caso tardío.** Medido en clase: confirmar una transferencia inexistente responde **202**, la traza sale **completamente en verde**, y no hay mensaje en la cola de error ni línea en el log. Lo único que delata el descarte es un span `TransferenciaSagaState receive` **sin hijos**. Su propuesta de log es precisamente lo que falta.

**c) Dónde se configura.** En MassTransit es por evento:

```csharp
Event(() => AbonoConfirmadoEvt, e =>
{
    e.CorrelateById(m => m.Message.TransferenciaId);
    e.OnMissingInstance(m => m.Execute(ctx => /* registrar y decidir */));
    // alternativas: m.Discard()  ·  m.Fault()
});
```

Su respuesta es `Execute(...)`: registrar, clasificar y —en el caso desconocido— fallar para que quede en la cola de error.

**d) Faltó la mitad del encargo: qué responde `/confirmar-abono`.** Hoy responde **202 a cualquier GUID**, incluso inventado. Propuesta:

| Caso | Respuesta | Por qué |
|---|---|---|
| La transferencia existe y está esperando | **202 Accepted** | Correcto: se recibió y se procesará |
| La transferencia no existe | **404 Not Found** | El banco destino debe enterarse ya, no por una alerta interna |
| Existe pero ya cerró | **409 Conflict** (o 200 explícito) | Idempotente, pero honesto: no se aplicó nada |

**202 significa "lo recibí", no "lo apliqué".** Ese es el fondo del problema: el banco destino cuelga sin saber que su confirmación se perdió.

**e) Matiz sobre las alertas.** Con entrega al-menos-una-vez, los eventos duplicados o tardíos **son normales**. Alertar por cada uno genera ruido. La regla correcta: por **tasa** para los tardíos (por ejemplo, un aumento súbito), y por **ocurrencia** para los ids inexistentes, que en operación normal deben ser cero.

---

## 4 · Arquitectura, UI y QA — Transferencia 360

> **Encargo:** una transferencia son dos trazas. Propongan cómo ver su ciclo completo en una sola vista, y cómo encontrar en Jaeger todas sus trazas a partir del `TransferenciaId`.

### Lo que entregaron

Una arquitectura completa: OpenTelemetry con W3C Trace Context para la propagación, `transferencia.id` como correlación de negocio, un **Collector** intermedio con muestreo adaptativo y control de memoria, almacenamiento especializado por tipo de señal, y una vista **Transferencia 360** que recibe un `TransferenciaId` y reconstruye retención, mensajes, estado de la saga, timeout, compensación, liberación, trazas, logs y duración de cada etapa.

**Es la mejor entrega de la actividad.** La distinción entre `TraceId` (operación técnica) y `TransferenciaId` (proceso de negocio) es exactamente la lección del día, y la conclusión —*el problema no se resuelve forzando todo a una sola traza técnica*— es correcta y nada obvia.

### Lo que hay que afinar

**a) El tail sampling tiene un requisito operativo fuerte.** Para decidir viendo la traza completa, **todos sus spans deben llegar al mismo Collector**. Al escalar a varios Collectors hace falta un `loadbalancing exporter` que enrute por TraceId. Sin eso, cada instancia decide con media traza.

**b) La ventana de decisión debe superar la traza más larga, y las nuestras son largas.** Medido:

| Traza | Duración |
|---|---|
| Camino feliz | 0.1 s |
| Compensación por timeout | **15.2 s** |
| Outbox con RabbitMQ caído | **78.5 s** y **121.8 s** en dos corridas |

La ventana típica de un tail sampling es de 10 a 30 segundos. **Nuestra traza del Outbox la desborda por mucho.** Hay que subir `decision_wait`, asumiendo más memoria en el Collector, o aceptar que esas trazas se decidan incompletas. Es un límite real del diseño, no un detalle de configuración.

**c) En el laboratorio, Jaeger ya es el Collector.** Jaeger 2 es una distribución del OpenTelemetry Collector: hoy `Breb.Cuentas → Jaeger` ya cumple ese salto. El Collector aparte se justifica al agregar más servicios, políticas de muestreo o varios destinos — no antes.

**d) La vista 360 ya se puede consultar hoy, sin construir UI.** El atributo `transferencia.id` está en los spans de la API y de los consumidores (medido). Buscando por esa etiqueta en Jaeger aparecen las dos trazas de la transferencia. Ese es el primer entregable, y es de horas; la UI unificada viene después.

**e) Un vacío en la tabla de almacenamiento:** no dice **dónde vive la evidencia de los fallos**. Hoy vive en la cola `_error`… y hoy mismo se perdió al leerla (`get_no_ack: 3`). Esa evidencia necesita su propio destino duradero.

**f) Para que Loki o Elasticsearch sirvan**, el log debe traer el TraceId. Ya lo trae: cada línea termina con `traza=<TraceId>`.

---

## El cierre

> "Los cuatro encargos, leídos juntos, dicen lo mismo desde cuatro lugares.
>
> Infra descubrió que **no se puede prometer 'los errores siempre' decidiendo al principio**: hay que decidir al final, y eso obliga a un Collector. Arquitectura ya lo había puesto en su diagrama, y además encontró el límite: nuestra traza del Outbox dura dos minutos y desborda cualquier ventana razonable de decisión.
>
> Datos diseñó las alertas correctas, y al bajarlas a números descubrimos que los umbrales apretados **habrían sonado en cada reinicio del broker**. Una alerta que grita cuando todo está bien deja de leerse: es la forma más común de quedarse sin alertas teniéndolas.
>
> Fintech acertó en lo más difícil —distinguir 'llegó tarde' de 'nunca existió'— y con eso destapó que **hoy el sistema no puede distinguirlos**, porque la saga borra su rastro al terminar.
>
> Y la clase terminó con una demostración que no estaba planeada: los tres mensajes de la cola de error, la evidencia que llevaba once días guardada, **desaparecieron al leerlos**. La observabilidad no es solo producir señales: es que sobrevivan a quien las consulta."

---

## Resumen de lo medido en clase

| Afirmación | Evidencia | Confianza |
|---|---|---|
| Spans por transferencia | 19 (feliz) · 38 (compensación) | **Alta** |
| Duración de las trazas | 0.1 s · 15.2 s · 78.5 s y 121.8 s (Outbox) | **Alta** |
| Confirmación de transferencia inexistente | 202, traza verde, sin error ni log | **Alta** |
| Estados reales de la saga | `EsperandoConfirmacion`, `Compensando` | **Alta** |
| Vaciado del Outbox tras revivir el broker | 29 s · 51 s · 98 s | **Alta** — varía por corrida |
| Liberación bajo carga de 300 transferencias | ~100 s | **Media** — otra máquina |
| Colas `*_error` existentes | Solo `TransferenciaSagaState_error` | **Alta** |
| Métricas Prometheus en RabbitMQ | **No habilitadas** (`/metrics` → 404) | **Alta** |
| `head_message_timestamp` en la API de administración | **Existe** (sirve para la alerta de antigüedad) | **Alta** |
| La evidencia de la cola de error | Se perdió al leerla: `get_no_ack: 3`, cola en 0 | **Alta** |
