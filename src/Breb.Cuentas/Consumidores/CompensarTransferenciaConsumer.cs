using MassTransit;
using Microsoft.EntityFrameworkCore;
using Breb.Cuentas.Contratos;
using Breb.Cuentas.Infraestructura;

namespace Breb.Cuentas.Consumidores;

/// <summary>
/// El pago de cuatro semanas de trabajo: esta clase le devuelve la plata al usuario.
/// Usa la misma técnica de la Semana 2 — saldo y evento en UNA transacción, vía Outbox.
/// </summary>
public class CompensarTransferenciaConsumer : IConsumer<CompensarTransferencia>
{
    private readonly CuentasDbContext _db;
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly ILogger<CompensarTransferenciaConsumer> _logger;

    public CompensarTransferenciaConsumer(
        CuentasDbContext db,
        IPublishEndpoint publishEndpoint,
        ILogger<CompensarTransferenciaConsumer> logger)
    {
        _db = db;
        _publishEndpoint = publishEndpoint;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<CompensarTransferencia> context)
    {
        var msg = context.Message;

        _logger.LogWarning("COMPENSANDO transferencia {Id}: {Motivo}",
                           msg.TransferenciaId, msg.Motivo);

        var cuenta = await _db.Cuentas.FirstOrDefaultAsync(c => c.Id == msg.CuentaId);
        if (cuenta is null)
        {
            _logger.LogError("Cuenta {Id} no existe. No se puede compensar.", msg.CuentaId);
            return;
        }

        cuenta.LiberarRetencion(msg.MontoUVB);          // ← el reintegro real

        await _publishEndpoint.Publish(new FondosReintegrados
        {
            TransferenciaId = msg.TransferenciaId,
            Motivo = msg.Motivo
        });

        await _db.SaveChangesAsync();   // Outbox: saldo + evento, una transacción

        _logger.LogInformation("Reintegrados {Monto} UVB a la cuenta {Cuenta}",
                               msg.MontoUVB, msg.CuentaId);
    }
}
