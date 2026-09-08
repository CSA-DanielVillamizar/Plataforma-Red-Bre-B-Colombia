using Breb.Dice;

var builder = WebApplication.CreateBuilder(args);

// El MISMO directorio para ambos protocolos. La comparacion solo es honesta
// si REST y gRPC ejecutan exactamente la misma logica: lo unico que cambia
// es el transporte y la serializacion.
builder.Services.AddSingleton<DirectorioLlaves>();
builder.Services.AddGrpc();

// Kestrel escucha en DOS puertos con DOS protocolos distintos:
//   5090 -> HTTP/1.1  para REST
//   5091 -> HTTP/2    para gRPC (en texto plano, sin TLS: es laboratorio)
//
// gRPC EXIGE HTTP/2. No es una preferencia: el protocolo se apoya en el
// multiplexado y en las cabeceras comprimidas de HTTP/2, que HTTP/1.1 no
// tiene. Por eso hacen falta dos puertos y no uno.
builder.WebHost.ConfigureKestrel(o =>
{
    o.ListenLocalhost(5090, l => l.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http1);
    o.ListenLocalhost(5091, l => l.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http2);
});

var app = builder.Build();

// ── La cara REST ──────────────────────────────────────────────────────────
// JSON sobre HTTP/1.1. Legible, depurable con curl, consumible desde un
// navegador. Y con un costo que vamos a medir, no a suponer.
app.MapGet("/directorio/{llave}", (string llave, DirectorioLlaves dir) =>
{
    var r = dir.Resolver(llave);
    return r is null
        ? Results.NotFound(new { encontrada = false })
        : Results.Ok(new
        {
            encontrada = true,
            cuentaId = r.CuentaId,
            entidad = r.Entidad,
            tipoLlave = r.TipoLlave
        });
});

app.MapGet("/salud", (DirectorioLlaves dir) => Results.Ok(new { llaves = dir.Total }));

// ── La cara gRPC ──────────────────────────────────────────────────────────
app.MapGrpcService<DirectorioGrpcService>();

app.Run();
