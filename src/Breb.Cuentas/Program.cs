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

        // ── Reintentos con espera exponencial (Semana 5) ───────────────────
        // Antes: r.Interval(3, TimeSpan.FromSeconds(2)) — tres intentos, todos
        // separados exactamente 2 segundos. Dos defectos, y ambos se midieron:
        //
        // 1. TRES INTENTOS NO ALCANZAN. El repositorio de sagas de MassTransit
        //    sobre EF trabaja en aislamiento Serializable. Cuando varias sagas
        //    compensan a la vez sobre la MISMA cuenta, PostgreSQL aborta unas
        //    cuantas con "40001: could not serialize access". Eso NO es un bug:
        //    es la base protegiendo la consistencia, y la documentacion de
        //    PostgreSQL dice explicitamente que el 40001 SE DEBE REINTENTAR.
        //
        // 2. EL INTERVALO FIJO SINCRONIZA LAS COLISIONES. Si todas las
        //    transacciones en conflicto esperan los mismos 2 segundos, vuelven
        //    a chocar todas juntas. El reintento a intervalo fijo no resuelve
        //    la contencion: la repite en tandas.
        //
        // Medido con el intervalo fijo: 146 errores 40001, 18 R-FAULT y 17
        // sagas atrapadas para siempre en Compensando — con UNA instancia y
        // concurrencia 3. Cada R-FAULT es una saga que nunca recibe su
        // FondosReintegrados y jamas termina.
        //
        // La espera exponencial separa los reintentos en el tiempo y el cuarto
        // parametro les agrega dispersion, para que dos transacciones que
        // chocaron no vuelvan a intentarlo en el mismo instante.
        cfg.UseMessageRetry(r => r.Exponential(
            retryLimit:     10,
            minInterval:    TimeSpan.FromMilliseconds(100),
            maxInterval:    TimeSpan.FromSeconds(5),
            intervalDelta:  TimeSpan.FromMilliseconds(300)));

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
        // ── Bloqueo pesimista sobre la fila de la cuenta (Semana 5) ─────────
        // Primero intentamos concurrencia OPTIMISTA (token xmin + reintento con
        // espera exponencial). Lo medimos y no alcanzó: con 30 transferencias
        // concurrentes sobre la MISMA cuenta, algunas peticiones perdían las
        // ocho rondas de reintento y terminaban en HTTP 500.
        //
        // La regla que sale de esa medición:
        //   · conflictos RAROS      → optimista (detectar y reintentar)
        //   · conflictos FRECUENTES → pesimista (hacer fila desde el principio)
        //
        // Aquí los conflictos son la norma, no la excepción: todas las
        // transferencias de una cuenta pelean por su única fila. Reintentar es
        // trabajo desperdiciado; hacer fila es lo correcto.
        //
        // SELECT ... FOR UPDATE bloquea la fila hasta que la transacción
        // termine. Las demás peticiones ESPERAN su turno en vez de fallar.
        // Es exactamente lo que hace el libro mayor de un banco.
        //
        // ¿Y por qué se conserva el token xmin en CuentasDbContext si ya
        // bloqueamos? Porque es la red de seguridad: si algún día alguien
        // escribe en Cuentas por otro camino sin tomar el bloqueo, el token
        // convierte una pérdida silenciosa en un error visible.
        // MEDIDO SIN NINGUNA DE LAS DOS PROTECCIONES: de 60 retenciones solo
        // quedaron 11. Cuarenta y nueve transferencias respondieron HTTP 200
        // al usuario y el dinero nunca se movió.
        await using var tx = await db.Database.BeginTransactionAsync();

        var cuenta = await db.Cuentas
            // OJO: hay que pedir xmin EXPLICITAMENTE. En PostgreSQL "SELECT *"
            // no incluye las columnas de sistema, y como Cuenta usa xmin como
            // token de concurrencia, EF lo busca en el resultado y falla con
            // "42703: column b.xmin does not exist". Es el mismo SELECT que
            // MassTransit genera para sus sagas.
            .FromSql($"SELECT *, xmin FROM \"Cuentas\" WHERE \"Id\" = {cuentaId} FOR UPDATE")
            .FirstOrDefaultAsync();

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

        await db.SaveChangesAsync();                   // 3. saldo + evento juntos
        await tx.CommitAsync();                        // 4. y aquí suelta el bloqueo

        Log.Information("Fondos retenidos: {TransferenciaId} por {Monto} UVB",
                        transferenciaId, montoUVB);

        return Results.Ok(new { transferenciaId, montoUVB });
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