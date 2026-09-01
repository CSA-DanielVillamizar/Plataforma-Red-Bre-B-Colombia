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
                .TransitionTo(Compensando),

            // ── Llegada fuera de orden (Semana 5) ──────────────────────────
            // ¿Cómo puede llegar un reintegro si esta saga todavía no expiró?
            // Asi: el timeout SI se disparo y publico la compensacion, pero la
            // transaccion que ademas movia la saga a Compensando fue abortada
            // por PostgreSQL con 40001. La compensacion ya estaba en el Outbox
            // y salio igual; la saga, en cambio, se quedo en este estado.
            //
            // Bajo entrega al-menos-una-vez y con reintentos, los mensajes NO
            // llegan en el orden que uno dibujo en el tablero. Una maquina de
            // estados debe tolerar TODO evento que fisicamente pueda llegarle
            // en ese estado; si no, lanza NotAcceptedStateMachineException, el
            // mensaje se reintenta hasta agotarse y la saga queda zombi.
            //
            // El dinero ya volvio a la cuenta: lo correcto es apagar el reloj
            // y cerrar.
            When(FondosReintegradosEvt)
                .Then(ctx => ctx.Saga.MotivoCompensacion ??= "Reintegro recibido fuera de orden")
                .Unschedule(TimeoutConfirmacion)
                .Finalize());

        // ── Cierre de la compensación ──
        During(Compensando,
            When(FondosReintegradosEvt)
                .Finalize(),

            // Un timeout que llega tarde, cuando ya estamos compensando, no
            // tiene nada que hacer — pero si no se declara, MassTransit lo
            // trata como error. Ignorarlo explicitamente es la diferencia
            // entre un sistema tolerante y uno que se atasca.
            When(TimeoutConfirmacion.Received)
                .Then(ctx => ctx.Saga.MotivoCompensacion ??= "Timeout duplicado ignorado"));

        // Borra la fila cuando la saga termina.
        SetCompletedWhenFinalized();
    }
}
