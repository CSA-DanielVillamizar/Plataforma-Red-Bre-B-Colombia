using MassTransit;

namespace Breb.Cuentas.Sagas;

/// <summary>
/// Una fila por cada transferencia en vuelo. Vive en PostgreSQL, no en memoria:
/// si el servicio se reinicia, las sagas siguen ahí.
/// </summary>
public class TransferenciaSagaState : SagaStateMachineInstance, ISagaVersion
{
    // El hilo que amarra todos los mensajes de UNA transferencia.
    public Guid CorrelationId { get; set; }

    // En qué estado está: EsperandoConfirmacion, Compensando, ...
    public string CurrentState { get; set; } = null!;

    // Concurrencia optimista: si dos eventos llegan a la vez, uno se reintenta.
    public int Version { get; set; }

    public Guid CuentaOrigenId { get; set; }
    public decimal MontoUVB { get; set; }
    public DateTime IniciadaEn { get; set; }
    public string? MotivoCompensacion { get; set; }

    // El "recibo" del reloj programado: sin esto no se puede cancelar.
    public Guid? TimeoutTokenId { get; set; }
}
