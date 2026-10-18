using MassTransit;
using Microsoft.EntityFrameworkCore;
using Breb.Cuentas.Contratos;
using Breb.Cuentas.Infraestructura;

namespace Breb.Cuentas.Consumidores;

/// <summary>
/// El pago de cinco semanas de trabajo: esta clase le devuelve la plata al usuario.
/// Usa la misma técnica de la Semana 2 — saldo y evento en UNA transacción, vía Outbox.
///
/// Compare esta versión con la de la Semana 5. Antes hacían falta:
///   · una guarda de idempotencia contra la tabla MensajesProcesados,
///   · pasarle el monto a LiberarRetencion,
///   · confiar en que nadie liberara de más.
/// Ahora nada de eso existe. No porque se haya escrito código más astuto, sino
/// porque el MODELO cambió: la retención es una entidad que sabe su monto y sabe
/// si ya fue liberada. La idempotencia dejó de ser un mecanismo y pasó a ser una
/// propiedad del dominio.
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

        Observabilidad.Telemetria.EtiquetarTransferencia(msg.TransferenciaId);
        _logger.LogWarning("COMPENSANDO transferencia {Id}: {Motivo}",
                           msg.TransferenciaId, msg.Motivo);

        // Búsqueda por clave primaria: una sola fila, sin recorrer nada.
        var retencion = await _db.Retenciones
            .FirstOrDefaultAsync(r => r.TransferenciaId == msg.TransferenciaId);

        if (retencion is null)
        {
            // Nunca existió esa retención. En la Semana 5 esto habría intentado
            // liberar plata ajena y habría reventado el invariante de la cuenta.
            _logger.LogError("No existe retención para {Id}. Nada que compensar.",
                             msg.TransferenciaId);
            return;
        }

        var cuenta = await _db.Cuentas.FirstOrDefaultAsync(c => c.Id == retencion.CuentaId);
        if (cuenta is null)
        {
            _logger.LogError("Cuenta {Id} no existe. No se puede compensar.", retencion.CuentaId);
            return;
        }

        // Toda la lógica de idempotencia vive aquí dentro, en el dominio.
        var seLibero = cuenta.LiberarRetencion(retencion);

        if (!seLibero)
        {
            // Entrega duplicada. Bajo at-least-once esto es lo NORMAL, no un fallo.
            _logger.LogWarning("Retención {Id} ya estaba liberada; se reenvía el evento.",
                               msg.TransferenciaId);
        }

        // Se publica en ambos casos: si la entrega anterior murió después de
        // liberar pero antes de publicar, la saga quedaría esperando para siempre.
        await _publishEndpoint.Publish(new FondosReintegrados
        {
            TransferenciaId = msg.TransferenciaId,
            Motivo = msg.Motivo
        });

        await _db.SaveChangesAsync();   // Outbox: saldo + evento, una transacción

        if (seLibero)
            _logger.LogInformation("Reintegrados {Monto} UVB a la cuenta {Cuenta}",
                                   retencion.MontoUVB, retencion.CuentaId);
    }
}
