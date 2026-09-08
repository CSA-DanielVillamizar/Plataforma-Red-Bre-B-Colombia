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
}
