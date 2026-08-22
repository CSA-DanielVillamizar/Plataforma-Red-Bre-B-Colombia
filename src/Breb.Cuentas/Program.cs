using MassTransit;
using Microsoft.EntityFrameworkCore;
using Breb.Cuentas.Infraestructura;
using Breb.Cuentas.Consumidores;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// ── Logging legible en consola (para la demo) ──
builder.Host.UseSerilog((ctx, cfg) => cfg
    .WriteTo.Console(outputTemplate:
        "[{Timestamp:HH:mm:ss}] {Level:u3} {Message:lj}{NewLine}{Exception}"));

// ── Base de datos ──
// ⚠️ Port=5433 — debe coincidir con el lado IZQUIERDO del docker-compose
var connectionString = "Host=localhost;Port=5433;Database=brebcuentas;" +
                       "Username=postgres;Password=dev_only_password";

builder.Services.AddDbContext<CuentasDbContext>(o =>
    o.UseNpgsql(connectionString));

// ── MassTransit + Outbox ──
builder.Services.AddMassTransit(x =>
{
    x.AddEntityFrameworkOutbox<CuentasDbContext>(o =>
    {
        o.QueryDelay = TimeSpan.FromSeconds(1);  // cada cuánto revisa la tabla
        o.UsePostgres();
        o.UseBusOutbox();                        // ← intercepta Publish()
    });

    x.AddConsumer<FondosRetenidosConsumer>();

    x.UsingRabbitMq((context, cfg) =>
    {
        cfg.Host("localhost", "/", h =>
        {
            h.Username("guest");
            h.Password("guest");
        });

        cfg.UseMessageRetry(r => r.Interval(3, TimeSpan.FromSeconds(2)));
        cfg.ConfigureEndpoints(context);
    });
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.MapPost("/cuentas/{cuentaId:guid}/retener",
    async (Guid cuentaId, decimal montoUVB,
           CuentasDbContext db, IPublishEndpoint publishEndpoint) =>
    {
        var cuenta = await db.Cuentas.FirstOrDefaultAsync(c => c.Id == cuentaId);
        if (cuenta is null) return Results.NotFound("Cuenta no existe.");

        var transferenciaId = Guid.NewGuid();

        cuenta.Retener(montoUVB);                     // 1. regla de dominio

        await publishEndpoint.Publish(new Breb.Cuentas.Contratos.FondosRetenidos
        {
            TransferenciaId = transferenciaId,
            MontoUVB = montoUVB,
            TimeoutMs = 15000
        });                                            // 2. → va al Outbox

        await db.SaveChangesAsync();                   // 3. UNA transacción

        Log.Information("Fondos retenidos: {TransferenciaId} por {Monto} UVB",
                        transferenciaId, montoUVB);

        return Results.Ok(new { transferenciaId, montoUVB });
    });

app.Run();