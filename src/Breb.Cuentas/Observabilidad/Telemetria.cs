using System.Diagnostics;
using Npgsql;                    // AddNpgsql() de trazas (no el de EF Core)
using OpenTelemetry.Exporter;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Breb.Cuentas.Observabilidad;

/// <summary>
/// Trazas distribuidas con OpenTelemetry (Semana 9).
///
/// Una traza es el recorrido completo de UNA operación a través del sistema:
/// la petición HTTP, las consultas a PostgreSQL, el mensaje que sale a
/// RabbitMQ, el consumidor que lo procesa en otra instancia, la saga. Cada
/// tramo es un "span"; todos comparten el mismo TraceId.
///
/// Tres fuentes de spans:
///   · ASP.NET Core  → cada petición HTTP que entra
///   · MassTransit   → cada mensaje publicado, enviado y consumido
///   · Npgsql        → cada comando a PostgreSQL
///
/// El TraceId viaja de un servicio a otro dentro del propio mensaje (la
/// cabecera "traceparent" del estándar W3C): así un consumidor en otra
/// instancia sabe a qué traza pertenece lo que está procesando.
/// </summary>
public static class Telemetria
{
    public const string NombreServicio = "breb-cuentas";

    /// <summary>
    /// Fuente propia para etiquetar spans con datos del negocio.
    /// </summary>
    public static readonly ActivitySource Fuente = new(NombreServicio);

    public static void AgregarTelemetria(this WebApplicationBuilder builder)
    {
        var config = builder.Configuration;

        // Se puede apagar sin recompilar (Otel__Habilitado=false). Sirve para
        // medir cuánto cuesta trazar: misma aplicación, con y sin.
        if (!config.GetValue("Otel:Habilitado", true))
        {
            Console.WriteLine("[OTEL] Trazas DESHABILITADAS");
            return;
        }

        // ⚠️ Puerto 4327, no el 4317 por defecto de OTLP. En esta máquina otro
        // proyecto tiene su propio Jaeger en el 4317: si enviáramos ahí, las
        // trazas llegarían al Jaeger AJENO y no habría ningún error. El
        // exportador no avisa si nadie lo escucha, ni si lo escucha otro.
        var destino = config["Otel:Endpoint"] ?? "http://localhost:4327";

        builder.Services.AddOpenTelemetry()
            .ConfigureResource(r => r.AddService(
                serviceName: NombreServicio,
                // Con 3 instancias, esto dice en cuál corrió cada span.
                serviceInstanceId: $"{Environment.MachineName}:{Environment.ProcessId}"))
            .WithTracing(t => t
                .AddSource(NombreServicio)
                .AddSource("MassTransit")
                .AddAspNetCoreInstrumentation(o =>
                    // Swagger no es negocio: no ensucia las trazas.
                    o.Filter = ctx => !ctx.Request.Path.StartsWithSegments("/swagger"))
                .AddNpgsql()
                .AddOtlpExporter(o =>
                {
                    o.Endpoint = new Uri(destino);
                    o.Protocol = OtlpExportProtocol.Grpc;
                }));

        Console.WriteLine($"[OTEL] Trazas -> {destino} (servicio {NombreServicio})");
    }

    /// <summary>
    /// Etiqueta el span actual con el id de la transferencia. Una transferencia
    /// NO es una sola traza (la confirmación llega en otra petición HTTP), así
    /// que esta etiqueta es lo que permite encontrar todas sus trazas juntas.
    /// </summary>
    public static void EtiquetarTransferencia(Guid transferenciaId) =>
        Activity.Current?.SetTag("transferencia.id", transferenciaId.ToString());
}
