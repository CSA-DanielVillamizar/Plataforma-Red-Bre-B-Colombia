# Clase 7 — Instructivo Técnico
## Comunicación entre servicios: REST, gRPC y contratos
### Red Bre-B Colombia · 190304014-1 · Miércoles 9 de septiembre de 2026

Este documento es el **cómo**. El **qué decir** está en `Clase7_Guion_Consolidado.md`.

Todo lo que aparece aquí fue **ejecutado y medido** el 8 de septiembre de 2026. Los números vienen con su **nivel de confianza**: donde la medición no fue concluyente, está dicho.

---

## 0. Qué se agrega esta semana

| | |
|---|---|
| Proyecto nuevo | **`Breb.Dice`** — el directorio, con cara REST y cara gRPC |
| Proyecto nuevo | **`Breb.Bench`** — el banco de medición comparativo |
| Contrato | **`Protos/dice.proto`** |
| Infraestructura | **Ninguna.** DICE es en memoria: no hace falta Docker |

Es la clase más liviana de montar del semestre. `Breb.Cuentas` no se toca.

---

## 1. Preparación (25 min antes)

```bash
cd bre-b-lab/Breb.Platform/Breb.Dice
dotnet build -c Release
```

```bash
dotnet run -c Release --no-build
```

Verificar:

```bash
curl http://localhost:5090/salud
```

Debe responder `{"llaves":10000}`.

> **`-c Release` no es opcional.** La primera medición se hizo sobre un build Debug y los resultados fueron inservibles. Un build Debug no tiene las optimizaciones del compilador: medirlo es medir otra cosa.

---

## 2. El servicio DICE

### 2.1 Por qué DICE y no otro

DICE resuelve una **llave** (celular, correo, documento) a la cuenta bancaria destino. Es la primera llamada de toda transferencia.

Cumple las **cuatro condiciones** donde gRPC tiene sentido:

| Condición | DICE |
|---|---|
| La llamada es síncrona de verdad | Sí — sin la cuenta destino no hay transferencia |
| Volumen altísimo | Una consulta por cada transferencia del país |
| **Es interna** | La consume nuestro backend, no un navegador |
| Contrato estable | Resolver una llave cambia poco con los años |

> Si falta alguna —**sobre todo la tercera**— REST suele ser la respuesta correcta.

### 2.2 Los dos puertos

```csharp
builder.WebHost.ConfigureKestrel(o =>
{
    o.ListenLocalhost(5090, l => l.Protocols = HttpProtocols.Http1);   // REST
    o.ListenLocalhost(5091, l => l.Protocols = HttpProtocols.Http2);   // gRPC
});
```

**gRPC exige HTTP/2.** No es preferencia: se apoya en el multiplexado y en las cabeceras comprimidas que HTTP/1.1 no tiene. Por eso hacen falta dos puertos.

### 2.3 La misma lógica por los dos caminos

Ambas caras usan el mismo `DirectorioLlaves` inyectado como singleton. **La comparación solo es honesta si ejecutan exactamente la misma lógica**: lo único que cambia es el transporte y la serialización, que es lo que se está comparando.

### 2.4 Un tropiezo que van a tener

El `.proto` genera una clase `Directorio`. Si su clase de dominio también se llama `Directorio` y está en el mismo espacio de nombres, **choca**:

```
error CS0426: The type name 'DirectorioBase' does not exist in the type 'Directorio'
```

Se resuelve renombrando la clase de dominio (aquí: `DirectorioLlaves`). Vale la pena mencionarlo en clase — le pasa a todo el mundo la primera vez.

---

## 3. El contrato

```protobuf
service Directorio {
  rpc Resolver (ResolverRequest) returns (ResolverResponse);
  rpc ResolverLote (ResolverLoteRequest) returns (ResolverLoteResponse);
}

message ResolverResponse {
  bool encontrada = 1;
  string cuenta_id = 2;
  string entidad = 3;
  string tipo_llave = 4;
}
```

**Los números son la identidad real del campo.** El nombre no viaja por el cable; viaja el número. Consecuencias prácticas:

| Cambio | ¿Rompe clientes viejos? |
|---|---|
| Renombrar un campo | **No** — el número no cambió |
| Cambiar el número de un campo | **Sí** — a todos |
| Agregar un campo con número nuevo | **No** — los viejos lo ignoran |
| Eliminar un campo y reusar su número | **Sí, y en silencio** — usar `reserved` |

En los `.csproj` hace falta declarar el `.proto`:

```xml
<ItemGroup>
  <Protobuf Include="Protos\dice.proto" GrpcServices="Server" />   <!-- o Client -->
</ItemGroup>
```

> **Cuidado al automatizar esto:** un script que busque la cadena `Protobuf` en el `.csproj` para no duplicar la encuentra dentro de `Google.Protobuf` y no agrega nada. Pasó al preparar esta clase.

---

## 4. Cómo se midió — y las tres correcciones

Esta sección es el corazón de la clase. **La primera medición estuvo mal tres veces seguidas.**

### 4.1 El resultado inicial, y por qué no había que creerle

Build Debug, REST siempre primero:

| Corrida | gRPC vs REST |
|---|---|
| 1 | **2.09×** |
| 2 | 0.61× |
| 3 | 0.39× |
| 4 | 0.93× |

De "el doble de rápido" a "dos veces y media más lento". Mismo código, misma máquina.

### 4.2 Corrección 1 — build Release

Se estaba midiendo un build Debug. Se corrigió con `-c Release` en ambos proyectos.

### 4.3 Corrección 2 — el sesgo de orden

REST corría **siempre primero**, así que absorbía el calentamiento de la máquina y gRPC llegaba a una máquina ya caliente.

Se corrigió ejecutando **A-B-B-A** y promediando las dos pasadas de cada protocolo:

```
Pasada 1  REST
Pasada 2  gRPC
Pasada 3  gRPC
Pasada 4  REST
```

Si un protocolo gana en **las dos posiciones**, el resultado es del protocolo. Si no, es del orden.

Resultado tras esta corrección: **1.09×**, con dispersión de **2 % en REST** y **69 % en gRPC**.

### 4.4 Corrección 3 — la comparación no era justa

`HttpClient` abre un **pool** de conexiones HTTP/1.1. gRPC multiplexa **todo sobre una sola** conexión HTTP/2. Se comparaba un pool contra una conexión.

```csharp
var manejador = new SocketsHttpHandler
{
    EnableMultipleHttp2Connections = true,
    PooledConnectionIdleTimeout = Timeout.InfiniteTimeSpan,
    KeepAlivePingDelay = TimeSpan.FromSeconds(60),
    KeepAlivePingTimeout = TimeSpan.FromSeconds(30)
};

using var canal = GrpcChannel.ForAddress("http://localhost:5091",
    new GrpcChannelOptions { HttpHandler = manejador });
```

**Con esa línea el throughput se triplicó** — de ~950 a ~2 900 req/s. Es el hallazgo más aplicable del día: **gRPC por defecto usa una sola conexión**, y con concurrencia alta esa conexión es el cuello de botella.

### 4.5 El banco se autodiagnostica

```csharp
var dispRest = Math.Abs(r1.tps - r2.tps) / restTps * 100;
var dispGrpc = Math.Abs(g1.tps - g2.tps) / grpcTps * 100;
if (dispRest > 25 || dispGrpc > 25)
    Console.WriteLine("  [!] Dispersion alta: en esta maquina el throughput NO es concluyente.");
```

> Un banco de medición que **avisa cuando no se le puede creer** vale más que uno que siempre da un número.

---

## 5. Los resultados, con su nivel de confianza

```bash
cd bre-b-lab/Breb.Platform/Breb.Bench
```

```bash
dotnet run -c Release --no-build -- 2000 20
```

| Métrica | REST | gRPC | Confianza |
|---|---|---|---|
| **Bytes por respuesta** | **109** | **56** | **Alta** — idéntico en toda corrida |
| **Lote de 100 llaves** | 154-324 ms | **5-8 ms** | **Alta** — 28.8× / 40.5× / 33.8× |
| Throughput | ~2 900 req/s | ~2 800 req/s | **Ninguna** — dispersión 29-66 % |
| Latencia p50 | 5.55 ms | 5.22 ms | **Ninguna** — dentro del ruido |

### 5.1 Lo que sí se puede afirmar

**Protobuf pesa la mitad.** 109 bytes contra 56, siempre. Porque no manda los nombres de los campos:

```json
{"encontrada":true,"cuentaId":"cc1e76e6-749d-470f-8dc4-a32136e4a67c","entidad":"NEQUI","tipoLlave":"CELULAR"}
```

Todo lo que ve entre comillas del lado izquierdo de cada `:` **no viaja** en Protobuf.

### 5.2 Lo que NO se puede afirmar

**Que gRPC sea más rápido.** Con cliente y servidor en el mismo portátil, compitiendo por los mismos núcleos, la dispersión llegó al 66 %.

Para medirlo en serio harían falta: máquinas distintas, red real, muchas repeticiones y reporte de dispersión.

> **"No se pudo concluir" es un resultado, no un fracaso.** Lo deshonesto habría sido quedarse con la primera corrida.

### 5.3 La comparación que sí fue concluyente: el lote

```
REST — 100 peticiones secuenciales    154.29 ms
gRPC — 1 llamada ResolverLote           5.35 ms
```

Reproducible: **28.8× / 40.5× / 33.8×** en tres corridas.

**Y la lección no es la que parece.** Esa ventaja **no es del protocolo**: es de hacer **un viaje en vez de cien**. Un endpoint REST `/directorio/lote` daría prácticamente lo mismo.

La diferencia real es que **en gRPC el lote ya estaba en el contrato** — un `rpc` más. En REST hay que inventarlo, decidir si va por query string o por body, y documentarlo aparte.

> **Antes de cambiar de protocolo, cuenten los viajes.** La mayoría de los problemas de latencia entre servicios no son del protocolo: son *chatty interfaces*.

---

## 6. Diagnóstico rápido

| Síntoma | Causa | Qué hacer |
|---|---|---|
| `CS0426: DirectorioBase does not exist` | Choque de nombres con la clase generada | Renombrar la clase de dominio |
| `CS0246: Breb could not be found` | El `.proto` no se declaró en el `.csproj` | Agregar el `<Protobuf Include=…>` |
| `curl` no funciona contra el 5091 | gRPC exige HTTP/2 y un cliente con el `.proto` | Usar `Breb.Bench`, no `curl` |
| Resultados que varían 3× entre corridas | Debug, sesgo de orden, o una sola conexión | Aplicar las tres correcciones de §4 |
| Throughput sospechosamente bajo | Falta `EnableMultipleHttp2Connections` | Agregarlo al `SocketsHttpHandler` |
| El primer resultado es siempre el peor | Falta calentamiento | 500 llamadas por protocolo antes de medir |

---

## 7. Entregables de la semana

- [ ] Proyecto `Breb.Dice` con las dos caras sobre la misma lógica.
- [ ] `Protos/dice.proto` con `Resolver` y `ResolverLote`.
- [ ] Proyecto `Breb.Bench` con orden A-B-B-A y autodiagnóstico de dispersión.
- [ ] Tabla de resultados **con su nivel de confianza**, no solo con números.
- [ ] La tabla de dispersión que muestra por qué el throughput no fue concluyente.

---

## 8. Lo que queda abierto

1. **Medir desde otra máquina.** Cliente y servidor en equipos distintos, red real. Predicción: ahí el tamaño del payload empieza a pesar de verdad.
2. **gRPC-Web.** El navegador no habla gRPC directo. ¿Qué pierde y por qué hace falta un proxy?
3. **Versionado de contratos.** ¿Qué pasa si eliminan un campo del `.proto` con clientes viejos en producción? Investigar `reserved`.

---

*Instructivo técnico de la Clase 7. Los números fueron medidos el 8 de septiembre de 2026, y cada uno viene con el nivel de confianza que la medición permite sostener.*
