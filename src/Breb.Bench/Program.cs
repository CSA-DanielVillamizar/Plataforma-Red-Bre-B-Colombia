using System.Diagnostics;
using Breb.Dice.Grpc;
using Grpc.Net.Client;

// ─────────────────────────────────────────────────────────────────────────
//  BANCO DE MEDICION — REST vs gRPC contra el servicio DICE
//
//  Mide lo mismo por los dos caminos: resolver una llave a su cuenta. La
//  logica del servidor es IDENTICA; lo unico que cambia es el transporte
//  (HTTP/1.1 vs HTTP/2) y la serializacion (JSON vs Protobuf).
//
//  POR QUE EL ORDEN A-B-B-A:
//  La primera version de este banco corria REST y despues gRPC, siempre en
//  ese orden. Resultado: REST absorbia todo el calentamiento de la maquina
//  y gRPC salia favorecido. Las corridas daban desde 0.39x hasta 3.20x —
//  o sea, no median el protocolo: median el orden.
//
//  Ejecutar A-B-B-A y promediar las dos pasadas de cada uno cancela el
//  efecto del orden: si gRPC gana en las dos posiciones, el resultado es
//  del protocolo y no del calentamiento.
//
//  Uso:  dotnet run -c Release --  <peticiones>  <concurrencia>
// ─────────────────────────────────────────────────────────────────────────

var total = args.Length > 0 ? int.Parse(args[0]) : 2000;
var conc = args.Length > 1 ? int.Parse(args[1]) : 20;

Console.WriteLine($"  {total} peticiones por pasada · concurrencia {conc} · orden A-B-B-A\n");

string Llave(int i) => (3001000000L + (i % 10_000)).ToString();

var http = new HttpClient { DefaultRequestVersion = new Version(1, 1) };

// ── El detalle que casi nos hace sacar la conclusion equivocada ──────────
// gRPC multiplexa TODAS las llamadas sobre UNA sola conexion HTTP/2. Eso es
// una ventaja (una conexion, sin handshakes repetidos) hasta que deja de
// serlo: con concurrencia alta, esa unica conexion se vuelve el cuello y
// aparece una dispersion enorme entre corridas.
//
// HttpClient, en cambio, abre un POOL de conexiones HTTP/1.1 y reparte.
//
// EnableMultipleHttp2Connections le dice al socket que abra mas conexiones
// HTTP/2 cuando la primera se satura. Sin esta linea, la comparacion no es
// justa: estariamos comparando "un pool" contra "una sola conexion".
var manejador = new SocketsHttpHandler
{
    EnableMultipleHttp2Connections = true,
    PooledConnectionIdleTimeout = Timeout.InfiniteTimeSpan,
    KeepAlivePingDelay = TimeSpan.FromSeconds(60),
    KeepAlivePingTimeout = TimeSpan.FromSeconds(30)
};

using var canal = GrpcChannel.ForAddress("http://localhost:5091",
    new GrpcChannelOptions { HttpHandler = manejador });
var cliente = new Directorio.DirectorioClient(canal);

async Task<(double tps, double p50, double p95, double p99, long bytes)> Rest()
{
    var lat = new List<double>(); long bytes = 0; var candado = new object();
    var sw = Stopwatch.StartNew();
    await Parallel.ForEachAsync(Enumerable.Range(0, total),
        new ParallelOptions { MaxDegreeOfParallelism = conc }, async (i, ct) =>
        {
            var t0 = Stopwatch.GetTimestamp();
            var cuerpo = await http.GetByteArrayAsync($"http://localhost:5090/directorio/{Llave(i)}", ct);
            var ms = Stopwatch.GetElapsedTime(t0).TotalMilliseconds;
            lock (candado) { lat.Add(ms); bytes += cuerpo.Length; }
        });
    sw.Stop();
    return Resumen(lat, sw, bytes);
}

async Task<(double tps, double p50, double p95, double p99, long bytes)> Grpc()
{
    var lat = new List<double>(); long bytes = 0; var candado = new object();
    var sw = Stopwatch.StartNew();
    await Parallel.ForEachAsync(Enumerable.Range(0, total),
        new ParallelOptions { MaxDegreeOfParallelism = conc }, async (i, ct) =>
        {
            var t0 = Stopwatch.GetTimestamp();
            var r = await cliente.ResolverAsync(new ResolverRequest { Llave = Llave(i) });
            var ms = Stopwatch.GetElapsedTime(t0).TotalMilliseconds;
            lock (candado) { lat.Add(ms); bytes += r.CalculateSize(); }
        });
    sw.Stop();
    return Resumen(lat, sw, bytes);
}

(double, double, double, double, long) Resumen(List<double> lat, Stopwatch sw, long bytes)
{
    lat.Sort();
    double P(double q) => lat[Math.Min((int)(lat.Count * q), lat.Count - 1)];
    return (total / sw.Elapsed.TotalSeconds, P(0.50), P(0.95), P(0.99), bytes);
}

// ── Calentamiento largo: 500 por protocolo. La primera llamada paga JIT,
//    apertura de conexion y negociacion de protocolo; medirlas seria medir
//    el arranque y no el estado estable.
Console.WriteLine("  Calentando (500 por protocolo)...");
for (var i = 0; i < 500; i++)
    await http.GetByteArrayAsync($"http://localhost:5090/directorio/{Llave(i)}");
for (var i = 0; i < 500; i++)
    await cliente.ResolverAsync(new ResolverRequest { Llave = Llave(i) });

// ── A-B-B-A ──────────────────────────────────────────────────────────────
Console.WriteLine("  Pasada 1/4  REST...");  var r1 = await Rest();
Console.WriteLine("  Pasada 2/4  gRPC...");  var g1 = await Grpc();
Console.WriteLine("  Pasada 3/4  gRPC...");  var g2 = await Grpc();
Console.WriteLine("  Pasada 4/4  REST...");  var r2 = await Rest();

var restTps = (r1.tps + r2.tps) / 2;
var grpcTps = (g1.tps + g2.tps) / 2;
var restP50 = (r1.p50 + r2.p50) / 2;
var grpcP50 = (g1.p50 + g2.p50) / 2;
var restP99 = (r1.p99 + r2.p99) / 2;
var grpcP99 = (g1.p99 + g2.p99) / 2;

// ── Tamano de UNA respuesta: esto SI es determinista ─────────────────────
var unaRest = await http.GetByteArrayAsync($"http://localhost:5090/directorio/{Llave(42)}");
var unaGrpc = (await cliente.ResolverAsync(new ResolverRequest { Llave = Llave(42) })).CalculateSize();

Console.WriteLine("\n  ─────────────────────────────────────────────────────────────");
Console.WriteLine("                        REST (JSON/1.1)   gRPC (Protobuf/2)");
Console.WriteLine("  ─────────────────────────────────────────────────────────────");
Console.WriteLine($"  Throughput pasada 1     {r1.tps,8:F0} req/s    {g1.tps,8:F0} req/s");
Console.WriteLine($"  Throughput pasada 2     {r2.tps,8:F0} req/s    {g2.tps,8:F0} req/s");
Console.WriteLine($"  Throughput PROMEDIO     {restTps,8:F0} req/s    {grpcTps,8:F0} req/s");
Console.WriteLine($"  Latencia p50 promedio   {restP50,8:F2} ms       {grpcP50,8:F2} ms");
Console.WriteLine($"  Latencia p99 promedio   {restP99,8:F2} ms       {grpcP99,8:F2} ms");
Console.WriteLine("  ─────────────────────────────────────────────────────────────");
Console.WriteLine($"  Una respuesta           {unaRest.Length,8} bytes    {unaGrpc,8} bytes   <- determinista");
Console.WriteLine("  ─────────────────────────────────────────────────────────────");

// ── Honestidad sobre la dispersion ───────────────────────────────────────
var dispRest = Math.Abs(r1.tps - r2.tps) / restTps * 100;
var dispGrpc = Math.Abs(g1.tps - g2.tps) / grpcTps * 100;
Console.WriteLine($"\n  Dispersion entre pasadas:  REST {dispRest:F0}%   gRPC {dispGrpc:F0}%");
if (dispRest > 25 || dispGrpc > 25)
    Console.WriteLine("  [!] Dispersion alta: en esta maquina el throughput NO es concluyente.");

Console.WriteLine($"\n  gRPC es {grpcTps / restTps:F2}x en throughput  (promedio A-B-B-A)");
Console.WriteLine($"  gRPC usa {(double)unaRest.Length / unaGrpc:F2}x menos bytes por respuesta");
Console.WriteLine($"\n  Payload REST: {System.Text.Encoding.UTF8.GetString(unaRest)}");

// ─────────────────────────────────────────────────────────────────────────
//  LA COMPARACION QUE SI ES CONCLUYENTE: resolver 100 llaves de una vez
//
//  Este caso no mide micro-latencia (que en un portatil es puro ruido):
//  mide NUMERO DE VIAJES DE IDA Y VUELTA. Y eso es aritmetica, no suerte.
//
//    REST -> 100 peticiones HTTP, 100 round-trips
//    gRPC -> 1 llamada ResolverLote, 1 round-trip
//
//  El contrato .proto ya contemplaba el lote. Con REST habria que inventar
//  un endpoint nuevo, discutir si va por query string o por body, y
//  documentarlo aparte.
// ─────────────────────────────────────────────────────────────────────────
Console.WriteLine("\n  ═════════════════════════════════════════════════════════════");
Console.WriteLine("   LOTE: resolver 100 llaves");
Console.WriteLine("  ═════════════════════════════════════════════════════════════");

const int nLote = 100;
var llavesLote = Enumerable.Range(0, nLote).Select(Llave).ToArray();

// Se repite 20 veces y se promedia: el lote es rapido y una sola medicion
// quedaria dominada por el reloj.
double msRest = 0, msGrpc = 0;
const int repeticiones = 20;

for (var rep = 0; rep < repeticiones; rep++)
{
    var t0 = Stopwatch.GetTimestamp();
    foreach (var k in llavesLote)
        await http.GetByteArrayAsync($"http://localhost:5090/directorio/{k}");
    msRest += Stopwatch.GetElapsedTime(t0).TotalMilliseconds;

    var peticion = new ResolverLoteRequest();
    peticion.Llaves.AddRange(llavesLote);
    var t1 = Stopwatch.GetTimestamp();
    await cliente.ResolverLoteAsync(peticion);
    msGrpc += Stopwatch.GetElapsedTime(t1).TotalMilliseconds;
}

msRest /= repeticiones;
msGrpc /= repeticiones;

Console.WriteLine($"  REST — {nLote} peticiones secuenciales   {msRest,8:F2} ms");
Console.WriteLine($"  gRPC — 1 llamada ResolverLote         {msGrpc,8:F2} ms");
Console.WriteLine($"\n  El lote es {msRest / msGrpc:F1}x mas rapido, y no es por el protocolo:");
Console.WriteLine($"  es por hacer 1 viaje en vez de {nLote}.");
