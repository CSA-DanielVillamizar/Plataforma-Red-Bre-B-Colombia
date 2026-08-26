namespace Breb.Cuentas.Contratos;

// Estos campos NO son inventados: salen de
// docs/contracts/orquestacion-saga.yaml (Semana 1)
public record FondosRetenidos
{
    public Guid TransferenciaId { get; init; }
    public Guid CuentaOrigenId { get; init; }   // la saga lo necesita para compensar
    public decimal MontoUVB { get; init; }
    public int TimeoutMs { get; init; }
}