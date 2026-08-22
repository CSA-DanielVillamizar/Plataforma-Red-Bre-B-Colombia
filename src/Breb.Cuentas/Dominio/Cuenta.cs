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
}