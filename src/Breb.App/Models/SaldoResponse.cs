namespace Breb.App.Models;

public sealed class SaldoResponse
{
    public Guid CuentaId { get; set; }
    public decimal SaldoDisponible { get; set; }
    public decimal SaldoRetenido { get; set; }
}
