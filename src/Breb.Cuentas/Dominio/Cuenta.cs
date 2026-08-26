namespace Breb.Cuentas.Dominio;

public class Cuenta
{
    public Guid Id { get; private set; }
    public decimal SaldoDisponible { get; private set; }
    public decimal SaldoRetenido { get; private set; }

    private Cuenta() { }   // EF Core lo necesita

    public Cuenta(Guid id, decimal saldoInicial)
    {
        Id = id;
        SaldoDisponible = saldoInicial;
        SaldoRetenido = 0;
    }

    // Regla de negocio: no se puede retener más de lo disponible
    public void Retener(decimal monto)
    {
        if (monto <= 0)
            throw new InvalidOperationException("El monto debe ser positivo.");

        if (monto > SaldoDisponible)
            throw new InvalidOperationException("Saldo insuficiente para retener.");

        SaldoDisponible -= monto;
        SaldoRetenido += monto;
    }

    // La COMPENSACIÓN: el inverso exacto de Retener().
    // Toda acción de una saga necesita su inversa escrita desde el diseño.
    public void LiberarRetencion(decimal monto)
    {
        if (monto <= 0)
            throw new InvalidOperationException("El monto debe ser positivo.");

        if (monto > SaldoRetenido)
            throw new InvalidOperationException(
                "No se puede liberar más de lo retenido.");

        SaldoRetenido -= monto;
        SaldoDisponible += monto;
    }
}