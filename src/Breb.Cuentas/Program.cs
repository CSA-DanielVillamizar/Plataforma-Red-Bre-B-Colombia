using MassTransit;
using Microsoft.EntityFrameworkCore;
using Breb.Cuentas.Infraestructura;
using Breb.Cuentas.Consumidores;
using Breb.Cuentas.Contratos;
using Breb.Cuentas.Sagas;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// ── Logging legible en consola (para la demo) ──
builder.Host.UseSerilog((ctx, cfg) => cfg
    .WriteTo.Console(outputTemplate:
        "[{Timestamp:HH:mm:ss}] {Level:u3} {Message:lj}{NewLine}{Exception}"));

// ── Base de datos ──
// ⚠️ Port=5433 — debe coincidir con el lado IZQUIERDO del docker-compose
// SSL Mode=Disable: es una BD local en Docker; sin esto Npgsql intenta un
// handshake SSL que Docker Desktop en Windows aborta (SocketException 10053).
// Maximum Pool Size: Npgsql abre hasta 100 conexiones por instancia si no se
// le dice otra cosa. PostgreSQL acepta 100 EN TOTAL (max_connections). Con tres
// instancias, la demanda es de 300 contra un cupo de 100, y la base responde
// "53300: sorry, too many clients already" — el error que vimos al escalar.
// La base de datos es un recurso COMPARTIDO: el cupo se reparte, no se replica.
var connectionString = "Host=localhost;Port=5433;Database=brebcuentas;" +
                       "Username=postgres;Password=dev_only_password;" +
                       "SSL Mode=Disable;Timeout=30;Command Timeout=60;" +
                       "Maximum Pool Size=25;Minimum Pool Size=5";

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
    x.AddConsumer<CompensarTransferenciaConsumer>();

    // Registra el scheduler que la saga usa para sus timeouts.
    x.AddDelayedMessageScheduler();

    // ── La Saga (Semana 3) ──
    x.AddSagaStateMachine<TransferenciaSaga, TransferenciaSagaState>()
        .EntityFrameworkRepository(r =>
        {
            r.ExistingDbContext<CuentasDbContext>();
            r.UsePostgres();
        });

    x.UsingRabbitMq((context, cfg) =>
    {
        cfg.Host("localhost", "/", h =>
        {
            h.Username("guest");
            h.Password("guest");
        });

        // ⚠️ El motor del reloj. Sin esto el Schedule de la saga NUNCA
        // se dispara, y es un fallo SILENCIOSO: todo compila y corre igual.
        // Requiere el plugin rabbitmq_delayed_message_exchange, que viene
        // incluido en la imagen masstransit/rabbitmq del docker-compose.
        cfg.UseDelayedMessageScheduler();

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
        // ── Reintento por concurrencia optimista (Semana 5) ─────────────────
        // El token xmin de Cuenta hace que dos retenciones simultáneas sobre la
        // misma cuenta NO se pisen: la segunda recibe DbUpdateConcurrencyException
        // en vez de perder silenciosamente la actualización.
        // Detectar el conflicto es solo la mitad; hay que RESOLVERLO. Aquí se
        // relee el saldo fresco y se vuelve a intentar. Sin este bucle, el
        // usuario recibiría un 500 por algo que el sistema puede resolver solo.
        const int maxIntentos = 5;

        for (var intento = 1; ; intento++)
        {
            var cuenta = await db.Cuentas.FirstOrDefaultAsync(c => c.Id == cuentaId);
            if (cuenta is null) return Results.NotFound("Cuenta no existe.");

            var transferenciaId = Guid.NewGuid();

            cuenta.Retener(montoUVB);                     // 1. regla de dominio

            await publishEndpoint.Publish(new FondosRetenidos
            {
                TransferenciaId = transferenciaId,
                CuentaOrigenId = cuentaId,                 // la saga lo usa para compensar
                MontoUVB = montoUVB,
                TimeoutMs = 15000
            });                                            // 2. → va al Outbox

            try
            {
                await db.SaveChangesAsync();               // 3. UNA transacción

                Log.Information("Fondos retenidos: {TransferenciaId} por {Monto} UVB",
                                transferenciaId, montoUVB);

                return Results.Ok(new { transferenciaId, montoUVB });
            }
            catch (DbUpdateConcurrencyException) when (intento < maxIntentos)
            {
                // Otra petición modificó la fila entre nuestra lectura y nuestra
                // escritura. Limpiamos el rastreo de EF para releer datos frescos.
                Log.Warning("Conflicto de concurrencia en cuenta {Cuenta}, intento {Intento}",
                            cuentaId, intento);
                foreach (var entry in db.ChangeTracker.Entries().ToList())
                    entry.State = EntityState.Detached;
            }
        }
    });

// Simula la confirmación del core bancario destino.
// En producción este evento llegaría del banco receptor, no de un endpoint.
app.MapPost("/transferencias/{transferenciaId:guid}/confirmar-abono",
    async (Guid transferenciaId,
           CuentasDbContext db, IPublishEndpoint publishEndpoint) =>
    {
        await publishEndpoint.Publish(new AbonoConfirmado
        {
            TransferenciaId = transferenciaId,
            ConfirmadoEn = DateTime.UtcNow
        });

        // ⚠️ OBLIGATORIO con el Outbox activo: sin este SaveChanges el
        // mensaje se queda en la transacción y NUNCA sale. Todo Publish
        // necesita su SaveChanges, incluso si no hay cambios de datos.
        await db.SaveChangesAsync();

        Log.Information("Abono confirmado para {TransferenciaId}", transferenciaId);
        return Results.Accepted();
    });

app.Run();