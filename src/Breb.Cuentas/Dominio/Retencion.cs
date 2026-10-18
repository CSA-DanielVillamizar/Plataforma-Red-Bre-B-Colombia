namespace Breb.Cuentas.Dominio;

/// <summary>
/// Una retención concreta: la plata que UNA transferencia aparto de UNA cuenta.
///
/// Por qué existe esta clase (Semana 6):
/// Hasta la Semana 5, la cuenta solo llevaba un número — SaldoRetenido = 300 —
/// y ese número no puede responder la única pregunta que importa al compensar:
/// "¿sigue ahí la retención de la transferencia X?".
///
/// Un total no sabe QUIÉN retuvo. Por eso LiberarRetencion tenía que recibir el
/// monto por parámetro y validarlo contra el total de la cuenta, y por eso una
/// compensación repetida podía consumirse la retención de OTRA transferencia sin
/// que ningún invariante se diera cuenta.
///
/// Con esta entidad, la identidad de la retención es la transferencia misma.
/// </summary>
public class Retencion
{
    /// <summary>
    /// La clave primaria ES el identificador de la transferencia.
    /// No es un atajo: es la identidad natural. Una transferencia retiene
    /// exactamente una vez, y esa unicidad la garantiza la base de datos —
    /// no un chequeo que alguien pueda olvidar escribir.
    /// </summary>
    public Guid TransferenciaId { get; private set; }

    public Guid CuentaId { get; private set; }
    public decimal MontoUVB { get; private set; }
    public DateTime CreadaEn { get; private set; }

    public bool Liberada { get; private set; }
    public DateTime? LiberadaEn { get; private set; }

    /// <summary>
    /// La retención se liquidó: el dinero salió hacia el banco destino.
    ///
    /// POR QUÉ HIZO FALTA AGREGAR ESTO (Issue #46):
    /// Hasta aquí la retención solo sabía registrar UNO de sus dos finales.
    /// Si se compensaba, quedaba Liberada = true. Pero si la transferencia se
    /// completaba bien, el dinero salía de la cuenta y la retención se quedaba
    /// con Liberada = false para siempre — porque no se liberó, se gastó.
    ///
    /// El problema es que la saga se borra al terminar (SetCompletedWhenFinalized),
    /// así que una transferencia EXITOSA quedaba indistinguible de una huérfana:
    /// las dos son "retención viva sin saga". Cualquier proceso de reparación
    /// que se guiara por eso devolvería plata que ya se fue.
    ///
    /// Una retención tiene dos finales posibles y el modelo tiene que saber
    /// cuál ocurrió. Mientras solo registre uno, el otro es invisible, y lo
    /// invisible no se puede reparar.
    /// </summary>
    public bool Liquidada { get; private set; }
    public DateTime? LiquidadaEn { get; private set; }

    /// <summary>
    /// ¿Esta retención sigue pendiente de resolución? Ni devuelta ni gastada.
    /// Es la única definición honesta de "huérfana" cuando además no hay saga.
    /// </summary>
    public bool EstaPendiente => !Liberada && !Liquidada;

    private Retencion() { }   // EF Core lo necesita

    public Retencion(Guid transferenciaId, Guid cuentaId, decimal montoUVB)
    {
        if (montoUVB <= 0)
            throw new InvalidOperationException("El monto debe ser positivo.");

        TransferenciaId = transferenciaId;
        CuentaId = cuentaId;
        MontoUVB = montoUVB;
        CreadaEn = DateTime.UtcNow;
        Liberada = false;
    }

    /// <summary>
    /// Marca esta retención como devuelta.
    ///
    /// Fíjese que NO recibe el monto: el monto lo sabe la retención. Eso hace
    /// imposible por construcción liberar una cantidad distinta de la que se
    /// retuvo — el error deja de ser algo que hay que recordar validar y pasa
    /// a ser algo que no se puede ni escribir.
    ///
    /// Devuelve false si ya estaba liberada. No es un error: bajo entrega
    /// al-menos-una-vez, que la compensación llegue dos veces es lo NORMAL.
    /// La idempotencia deja de necesitar una tabla aparte y queda en el modelo.
    /// </summary>
    public bool Liberar()
    {
        if (Liberada) return false;

        Liberada = true;
        LiberadaEn = DateTime.UtcNow;
        return true;
    }

    /// <summary>
    /// Marca que el dinero salió: la transferencia se completó y el banco
    /// destino acreditó el abono. El saldo retenido deja de estar retenido
    /// porque ya no está, no porque haya vuelto.
    ///
    /// Igual que Liberar(), devuelve false si ya estaba liquidada: bajo entrega
    /// al-menos-una-vez, el aviso de completada también puede llegar dos veces.
    ///
    /// Una retención ya liberada NO puede liquidarse: si el dinero volvió a la
    /// cuenta, no pudo además salir hacia el destino. Que esos dos finales se
    /// excluyan es un invariante del dominio, no una validación que alguien
    /// tenga que acordarse de escribir en el consumidor.
    /// </summary>
    public bool Liquidar()
    {
        if (Liquidada) return false;

        if (Liberada)
            throw new InvalidOperationException(
                $"La retención {TransferenciaId} ya fue liberada: no puede liquidarse. " +
                "El dinero volvió a la cuenta y no pudo salir también hacia el destino.");

        Liquidada = true;
        LiquidadaEn = DateTime.UtcNow;
        return true;
    }
}
