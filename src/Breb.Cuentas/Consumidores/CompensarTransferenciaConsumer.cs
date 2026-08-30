using MassTransit;
using Microsoft.EntityFrameworkCore;
using Breb.Cuentas.Contratos;
using Breb.Cuentas.Dominio;
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

        // ── Guarda de idempotencia (Semana 5) ──────────────────────────────
        // RabbitMQ garantiza entrega AL MENOS UNA VEZ: ante un reintento, un
        // reinicio o un ack perdido, este mensaje puede llegar dos veces. Sin
        // esta guarda, la segunda entrega llama LiberarRetencion de nuevo y el
        // invariante de dominio la rechaza.
        // Es el mismo patrón que FondosRetenidosConsumer ya usaba desde la
        // Semana 2: lo que faltaba era aplicarlo también aquí.
        //
        // NOTA HONESTA: esta guarda NO fue la causa de las sagas atascadas que
        // encontramos midiendo. Lo medimos y nunca se disparó (0 duplicados).
        // La causa real era la actualización perdida sobre Cuenta — ver el
        // token de concurrencia en CuentasDbContext. Se deja igual porque la
        // entrega al-menos-una-vez es real y esto es correcto por principio.
        var msgId = context.MessageId ?? Guid.Empty;

        bool yaProcesado = await _db.MensajesProcesados
            .AnyAsync(m => m.MessageId == msgId);

        if (yaProcesado)
        {
            _logger.LogWarning("Compensacion DUPLICADA ignorada: {MessageId}", msgId);
            return;
        }

        var cuenta = await _db.Cuentas.FirstOrDefaultAsync(c => c.Id == msg.CuentaId);
        if (cuenta is null)
        {
            _logger.LogError("Cuenta {Id} no existe. No se puede compensar.", msg.CuentaId);
            return;
        }

        cuenta.LiberarRetencion(msg.MontoUVB);          // ← el reintegro real

        _db.MensajesProcesados.Add(new MensajeProcesado(msgId, DateTime.UtcNow));

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
