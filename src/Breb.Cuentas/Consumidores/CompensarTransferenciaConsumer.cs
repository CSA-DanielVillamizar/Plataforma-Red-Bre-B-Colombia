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
        // LA CLAVE ES LA TRANSFERENCIA, NO EL MENSAJE.
        //
        // Al principio usamos context.MessageId, y no sirvio: si la saga vuelve
        // a publicar CompensarTransferencia (porque su transaccion anterior se
        // abortó con 40001), el mensaje nuevo trae un MessageId DISTINTO. La
        // guarda no lo reconocia como duplicado, LiberarRetencion se ejecutaba
        // por segunda vez sobre una retencion ya devuelta, y el invariante de
        // dominio lo rechazaba.
        //
        // El hecho que de verdad importa no es "ya procesé este mensaje" sino
        // "esta transferencia YA fue compensada". Por eso la clave es el
        // TransferenciaId, que es estable a traves de reintentos y republicaciones.
        var claveIdempotencia = msg.TransferenciaId;

        bool yaCompensada = await _db.MensajesProcesados
            .AnyAsync(m => m.MessageId == claveIdempotencia);

        if (yaCompensada)
        {
            // No es un error: la compensación ya se aplicó. Republicamos el
            // evento para que la saga pueda cerrar, porque quizá quedó
            // esperandolo cuando la entrega anterior fallo.
            _logger.LogWarning("Compensacion YA APLICADA para {TransferenciaId}; se reenvia el evento",
                               msg.TransferenciaId);

            await _publishEndpoint.Publish(new FondosReintegrados
            {
                TransferenciaId = msg.TransferenciaId,
                Motivo = msg.Motivo
            });
            await _db.SaveChangesAsync();
            return;
        }

        var cuenta = await _db.Cuentas.FirstOrDefaultAsync(c => c.Id == msg.CuentaId);
        if (cuenta is null)
        {
            _logger.LogError("Cuenta {Id} no existe. No se puede compensar.", msg.CuentaId);
            return;
        }

        cuenta.LiberarRetencion(msg.MontoUVB);          // ← el reintegro real

        _db.MensajesProcesados.Add(new MensajeProcesado(claveIdempotencia, DateTime.UtcNow));

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
