using MassTransit;
using Microsoft.EntityFrameworkCore;
using Breb.Cuentas.Contratos;
using Breb.Cuentas.Infraestructura;

namespace Breb.Cuentas.Conciliacion;

/// <summary>
/// La red de seguridad del sistema (Issue #46).
///
/// EL PROBLEMA QUE RESUELVE
/// FondosRetenidos es el evento que CREA la saga. Si ese mensaje agota todos
/// sus reintentos por un 40001, la saga nunca nace: queda una retención viva
/// que nadie va a compensar nunca, porque el único que compensa es la saga que
/// no existe. El dinero queda retenido para siempre.
///
/// Medido: 2 de 300 en la Semana 6 (0.67 %) y 1 de 900 en la Semana 9.
///
/// POR QUÉ NO BASTA CON REINTENTAR MEJOR
/// La Semana 5 ya cambió los reintentos a espera exponencial con dispersión, y
/// aun así el fallo persistió. La redelivery diferida que se agregó junto con
/// este conciliador baja más la probabilidad, pero no la vuelve cero: cualquier
/// política de reintentos tiene un último intento, y después de ese no hay nada.
///
/// La diferencia entre las dos capas es de naturaleza, no de grado:
///   · Los reintentos son OPTIMISTAS: apuestan a que el problema es pasajero.
///   · La conciliación es PESIMISTA: asume que algo se va a perder igual, y
///     revisa el estado real del mundo para repararlo.
///
/// Un sistema que mueve dinero necesita las dos. La primera evita casi todos
/// los casos; la segunda garantiza que ninguno quede para siempre.
///
/// POR QUÉ ES SEGURO EJECUTARLO
/// Publica CompensarTransferencia, exactamente el mismo evento que publicaría
/// la saga. No tiene una ruta propia para devolver dinero: reusa la del dominio.
/// Y como Retencion.Liberar() es idempotente desde la Semana 6, que la
/// compensación llegue dos veces —una del conciliador y otra de una saga que
/// nació tarde— no devuelve el dinero dos veces.
/// </summary>
public class ConciliadorRetenciones : BackgroundService
{
    // Un número arbitrario pero FIJO: es el nombre del cerrojo, no un valor.
    // Todas las instancias piden el mismo y solo una lo obtiene.
    private const long LlaveCerrojo = 4600460046004600L;

    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<ConciliadorRetenciones> _logger;
    private readonly bool _habilitado;
    private readonly TimeSpan _intervalo;
    private readonly TimeSpan _umbral;
    private readonly int _lote;

    public ConciliadorRetenciones(
        IServiceScopeFactory scopes,
        IConfiguration configuracion,
        ILogger<ConciliadorRetenciones> logger)
    {
        _scopes = scopes;
        _logger = logger;

        _habilitado = configuracion.GetValue("Conciliacion:Habilitada", true);
        _intervalo  = TimeSpan.FromSeconds(configuracion.GetValue("Conciliacion:IntervaloSegundos", 60));
        _lote       = configuracion.GetValue("Conciliacion:MaximoPorPasada", 200);

        // ⚠️ EL UMBRAL ES EL PARÁMETRO DELICADO.
        // Tiene que ser MAYOR que la ventana completa de redelivery diferida
        // (15s + 45s + 90s ≈ 2.5 min). Si fuera menor, el conciliador podría
        // compensar una retención cuyo mensaje FondosRetenidos todavía está en
        // camino: la saga nacería después, sobre una retención ya liberada.
        // El dinero no se devolvería dos veces —Liberar() es idempotente— pero
        // estaríamos cancelando transferencias que aún tenían posibilidad de
        // completarse. Cinco minutos deja margen de sobra.
        _umbral = TimeSpan.FromMinutes(configuracion.GetValue("Conciliacion:UmbralMinutos", 5.0));
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        if (!_habilitado)
        {
            _logger.LogInformation("Conciliador de retenciones DESHABILITADO por configuración.");
            return;
        }

        _logger.LogInformation(
            "Conciliador de retenciones activo: cada {Intervalo}, umbral {Umbral}.",
            _intervalo, _umbral);

        // Le damos aire a la aplicación para que termine de arrancar antes de
        // la primera pasada: migraciones, bus, todo.
        try { await Task.Delay(TimeSpan.FromSeconds(10), ct); }
        catch (OperationCanceledException) { return; }

        while (!ct.IsCancellationRequested)
        {
            try
            {
                await UnaPasada(ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // Una pasada fallida no puede matar el servicio: es justamente
                // el que repara los fallos de los demás.
                _logger.LogError(ex, "La pasada de conciliación falló. Se reintenta en {Intervalo}.", _intervalo);
            }

            try { await Task.Delay(_intervalo, ct); }
            catch (OperationCanceledException) { break; }
        }
    }

    private async Task UnaPasada(CancellationToken ct)
    {
        using var scope = _scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CuentasDbContext>();
        var publicador = scope.ServiceProvider.GetRequiredService<IPublishEndpoint>();

        // Todo en una transacción: el cerrojo se suelta solo al terminar, y la
        // escritura del Outbox entra en el mismo commit.
        await using var tx = await db.Database.BeginTransactionAsync(ct);

        // ── Cerrojo consultivo (Semana 5: hay TRES instancias) ──────────────
        // Sin esto, las tres barren a la vez y publican la misma compensación
        // tres veces. No rompe nada —el consumidor es idempotente— pero triplica
        // el trabajo y ensucia el log. pg_try_advisory_xact_lock NO espera: si
        // otra instancia ya está barriendo, esta se va y vuelve en la siguiente.
        var barroYo = await db.Database
            .SqlQueryRaw<bool>("SELECT pg_try_advisory_xact_lock({0}) AS \"Value\"", LlaveCerrojo)
            .SingleAsync(ct);

        if (!barroYo)
        {
            await tx.RollbackAsync(ct);
            return;
        }

        var limite = DateTime.UtcNow - _umbral;

        // ⚠️ ESTA CONSULTA NO ES LA DEL ISSUE #46, Y LA DIFERENCIA ES CRÍTICA.
        //
        // El issue proponía: "NOT Liberada AND no hay saga". Eso está MAL, y es
        // la razón por la que había que arreglar el modelo antes que el barrido.
        //
        // Una transferencia que se completa bien deja exactamente esa huella:
        // la saga se borra al finalizar (SetCompletedWhenFinalized) y la
        // retención nunca se libera, porque el dinero no volvió — salió.
        // Con la consulta del issue, este conciliador habría compensado TODAS
        // las transferencias exitosas, devolviendo plata que ya está en el
        // banco destino. Habría sido mucho peor que el fallo que repara.
        //
        // Por eso se agregó Liquidada al modelo: ahora "pendiente" significa
        // ni devuelta ni gastada, que es la única definición honesta de huérfana.
        var huerfanas = await db.Retenciones
            .Where(r => !r.Liberada && !r.Liquidada && r.CreadaEn < limite)
            .Where(r => !db.TransferenciaSagas.Any(s => s.CorrelationId == r.TransferenciaId))
            .OrderBy(r => r.CreadaEn)
            .Take(_lote)
            .ToListAsync(ct);

        if (huerfanas.Count == 0)
        {
            await tx.CommitAsync(ct);
            return;
        }

        _logger.LogWarning(
            "CONCILIACIÓN: {Cuantas} retención(es) huérfana(s) sin saga, con más de {Umbral} de vida.",
            huerfanas.Count, _umbral);

        foreach (var r in huerfanas)
        {
            _logger.LogWarning(
                "  huérfana {Id}: {Monto} UVB retenidos en {Cuenta} desde {Creada:u}",
                r.TransferenciaId, r.MontoUVB, r.CuentaId, r.CreadaEn);

            await publicador.Publish(new CompensarTransferencia
            {
                TransferenciaId = r.TransferenciaId,
                CuentaId        = r.CuentaId,
                MontoUVB        = r.MontoUVB,
                Motivo          = "Conciliacion: retencion huerfana sin saga (#46)"
            }, ct);
        }

        // Outbox: las compensaciones salen en el mismo commit que suelta el
        // cerrojo. Si el proceso muere aquí, no se publicó nada y la siguiente
        // pasada —de esta instancia o de otra— vuelve a encontrarlas.
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        _logger.LogInformation(
            "CONCILIACIÓN: {Cuantas} compensación(es) encoladas.", huerfanas.Count);
    }
}
