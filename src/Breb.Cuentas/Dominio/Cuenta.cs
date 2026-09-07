namespace Breb.Cuentas.Dominio;

public class Cuenta
{
    public Guid Id { get; private set; }
    public decimal SaldoDisponible { get; private set; }

    /// <summary>
    /// Sigue existiendo, pero cambió de naturaleza: ya no es LA verdad sobre lo
    /// retenido, es un TOTAL DERIVADO que se mantiene por conveniencia (para no
    /// tener que sumar todas las retenciones vivas cada vez que alguien consulta
    /// el saldo). La verdad, retención por retención, vive en la tabla Retenciones.
    /// </summary>
    public decimal SaldoRetenido { get; private set; }

    private Cuenta() { }   // EF Core lo necesita

    public Cuenta(Guid id, decimal saldoInicial)
    {
        Id = id;
        SaldoDisponible = saldoInicial;
        SaldoRetenido = 0;
    }

    /// <summary>
    /// Retiene fondos para UNA transferencia y devuelve la retención creada.
    ///
    /// Antes recibía solo el monto y no dejaba rastro de quién retuvo. Ahora la
    /// transferencia es parte de la operación, y el resultado es un objeto que
    /// se puede buscar, auditar y liberar sin ambigüedad.
    /// </summary>
    public Retencion Retener(Guid transferenciaId, decimal monto)
    {
        if (monto <= 0)
            throw new InvalidOperationException("El monto debe ser positivo.");

        if (monto > SaldoDisponible)
            throw new InvalidOperationException("Saldo insuficiente para retener.");

        SaldoDisponible -= monto;
        SaldoRetenido += monto;

        return new Retencion(transferenciaId, Id, monto);
    }

    /// <summary>
    /// La COMPENSACIÓN: el inverso exacto de Retener().
    ///
    /// Compare esta firma con la de la Semana 5:
    ///     ANTES:  LiberarRetencion(decimal monto)   ← un número suelto
    ///     AHORA:  LiberarRetencion(Retencion r)     ← la retención concreta
    ///
    /// El cambio no es cosmético. Antes, liberar dos veces la transferencia A
    /// se comía la retención de la transferencia B, y el único invariante que
    /// existía — "no liberar más que el total" — no lo notaba, porque el total
    /// sí alcanzaba. El error era representable.
    ///
    /// Ahora la retención misma sabe si ya fue liberada, y el monto sale de
    /// ella. Liberar dos veces es un no-op, no una corrupción silenciosa.
    ///
    /// Devuelve false si la retención ya estaba liberada (llegada duplicada).
    /// </summary>
    public bool LiberarRetencion(Retencion retencion)
    {
        if (retencion.CuentaId != Id)
            throw new InvalidOperationException(
                "Esa retención no pertenece a esta cuenta.");

        if (!retencion.Liberar()) return false;   // ya estaba liberada

        SaldoRetenido -= retencion.MontoUVB;
        SaldoDisponible += retencion.MontoUVB;
        return true;
    }
}
