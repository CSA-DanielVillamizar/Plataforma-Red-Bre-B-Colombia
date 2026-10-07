using MassTransit;
using Microsoft.EntityFrameworkCore;
using Breb.Cuentas.Contratos;
using Breb.Cuentas.Infraestructura;

namespace Breb.Cuentas.Consumidores;

/// <summary>
/// Cierra el camino feliz en el modelo (Issue #46).
///
/// POR QUÉ NO EXISTÍA ESTE CONSUMIDOR
/// La saga publicaba TransferenciaCompletada y nadie la escuchaba. Tenía
/// sentido mientras el evento era solo un aviso: el dinero ya había salido y
/// no quedaba nada que hacer.
///
/// Pero sí quedaba algo que hacer: ANOTARLO. Sin esta anotación, la retención
/// se quedaba viva para siempre y la saga desaparecía al finalizar, de modo
/// que una transferencia perfectamente exitosa era indistinguible de una
/// huérfana — retención sin liberar, sin saga, en los dos casos.
///
/// Esa ambigüedad es la razón por la que el #46 llevaba tanto sin poder
/// arreglarse: no se puede reparar automáticamente lo que no se puede
/// distinguir de lo que está bien.
///
/// Es idempotente por la misma razón que el de compensación: Liquidar() sobre
/// una retención ya liquidada no hace nada.
/// </summary>
public class TransferenciaCompletadaConsumer : IConsumer<TransferenciaCompletada>
{
    private readonly CuentasDbContext _db;
    private readonly ILogger<TransferenciaCompletadaConsumer> _logger;

    public TransferenciaCompletadaConsumer(
        CuentasDbContext db,
        ILogger<TransferenciaCompletadaConsumer> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<TransferenciaCompletada> context)
    {
        var id = context.Message.TransferenciaId;

        Observabilidad.Telemetria.EtiquetarTransferencia(id);

        var retencion = await _db.Retenciones
            .FirstOrDefaultAsync(r => r.TransferenciaId == id);

        if (retencion is null)
        {
            // Puede pasar legítimamente: el laboratorio se reinició entre demos.
            // No es un error del sistema, pero sí algo que conviene ver.
            _logger.LogWarning("No existe retención para la transferencia {Id} completada.", id);
            return;
        }

        var cuenta = await _db.Cuentas.FirstOrDefaultAsync(c => c.Id == retencion.CuentaId);
        if (cuenta is null)
        {
            _logger.LogError("Cuenta {Id} no existe. No se puede liquidar.", retencion.CuentaId);
            return;
        }

        if (cuenta.LiquidarRetencion(retencion))
            _logger.LogInformation("Liquidados {Monto} UVB de la cuenta {Cuenta}: el dinero salió.",
                                   retencion.MontoUVB, retencion.CuentaId);
        else
            _logger.LogWarning("Retención {Id} ya estaba liquidada; aviso duplicado.", id);

        await _db.SaveChangesAsync();
    }
}
