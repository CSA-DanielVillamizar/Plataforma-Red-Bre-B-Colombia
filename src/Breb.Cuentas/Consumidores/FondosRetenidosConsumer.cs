using Breb.Cuentas.Contratos;
using Breb.Cuentas.Dominio;
using Breb.Cuentas.Infraestructura;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace Breb.Cuentas.Consumidores;

public class FondosRetenidosConsumer : IConsumer<FondosRetenidos>
{
    private readonly CuentasDbContext _db;
    private readonly ILogger<FondosRetenidosConsumer> _logger;

    public FondosRetenidosConsumer(
        CuentasDbContext db, ILogger<FondosRetenidosConsumer> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<FondosRetenidos> context)
    {
        var msgId = context.MessageId ?? Guid.Empty;

        // ── Chequeo de idempotencia ──
        bool yaProcesado = await _db.MensajesProcesados
            .AnyAsync(m => m.MessageId == msgId);

        if (yaProcesado)
        {
            _logger.LogWarning(
                "Mensaje DUPLICADO ignorado: {MessageId}", msgId);
            return;                       // salimos sin efecto
        }

        _logger.LogInformation(
            "Procesando FondosRetenidos: transferencia {TransferenciaId}, {Monto} UVB",
            context.Message.TransferenciaId, context.Message.MontoUVB);

        // Aquí iría la lógica real de negocio.

        _db.MensajesProcesados.Add(
            new MensajeProcesado(msgId, DateTime.UtcNow));
        await _db.SaveChangesAsync();
    }
}