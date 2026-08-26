using MassTransit;
using Breb.Cuentas.Contratos;

namespace Breb.Cuentas.Sagas;

/// <summary>
/// La política que el equipo puso en un post-it lila durante el Event Storming
/// de la Semana 1: "si no llega confirmación en 15 segundos → compensar".
/// </summary>
public class TransferenciaSaga : MassTransitStateMachine<TransferenciaSagaState>
{
    // ── Estados ──
    public State EsperandoConfirmacion { get; private set; } = null!;
    public State Compensando { get; private set; } = null!;

    // ── Eventos que mueven la máquina ──
    public Event<FondosRetenidos> FondosRetenidosEvt { get; private set; } = null!;
    public Event<AbonoConfirmado> AbonoConfirmadoEvt { get; private set; } = null!;
    public Event<FondosReintegrados> FondosReintegradosEvt { get; private set; } = null!;

    // ── El reloj ──
    public Schedule<TransferenciaSagaState, TimeoutConfirmacion> TimeoutConfirmacion
        { get; private set; } = null!;

    public TransferenciaSaga()
    {
        InstanceState(x => x.CurrentState);

        // Correlación: cómo cada mensaje encuentra SU saga entre todas las en vuelo.
        Event(() => FondosRetenidosEvt,
              e => e.CorrelateById(m => m.Message.TransferenciaId));
        Event(() => AbonoConfirmadoEvt,
              e => e.CorrelateById(m => m.Message.TransferenciaId));
        Event(() => FondosReintegradosEvt,
              e => e.CorrelateById(m => m.Message.TransferenciaId));

        Schedule(() => TimeoutConfirmacion, x => x.TimeoutTokenId, s =>
        {
            s.Delay = TimeSpan.FromSeconds(15);
            s.Received = r => r.CorrelateById(m => m.Message.TransferenciaId);
        });

        // ── Nace la saga ──
        Initially(
            When(FondosRetenidosEvt)
                .Then(ctx =>
                {
                    ctx.Saga.MontoUVB = ctx.Message.MontoUVB;
                    ctx.Saga.CuentaOrigenId = ctx.Message.CuentaOrigenId;
                    ctx.Saga.IniciadaEn = DateTime.UtcNow;
                })
                .Schedule(TimeoutConfirmacion,               // ⏰ arranca el reloj
                    ctx => new TimeoutConfirmacion
                    {
                        TransferenciaId = ctx.Saga.CorrelationId
                    })
                .TransitionTo(EsperandoConfirmacion));

        // ── La carrera: confirmación contra reloj ──
        During(EsperandoConfirmacion,
            When(AbonoConfirmadoEvt)
                .Unschedule(TimeoutConfirmacion)             // ⏰ apaga el reloj
                .Publish(ctx => new TransferenciaCompletada
                {
                    TransferenciaId = ctx.Saga.CorrelationId
                })
                .Finalize(),

            When(TimeoutConfirmacion.Received)               // ⏰ el reloj ganó
                .Then(ctx => ctx.Saga.MotivoCompensacion = "Timeout de confirmacion (15s)")
                .Publish(ctx => new CompensarTransferencia
                {
                    TransferenciaId = ctx.Saga.CorrelationId,
                    CuentaId = ctx.Saga.CuentaOrigenId,
                    MontoUVB = ctx.Saga.MontoUVB,
                    Motivo = "Timeout de confirmacion (15s)"
                })
                .TransitionTo(Compensando));

        // ── Cierre de la compensación ──
        During(Compensando,
            When(FondosReintegradosEvt)
                .Finalize());

        // Borra la fila cuando la saga termina.
        SetCompletedWhenFinalized();
    }
}
