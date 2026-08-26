namespace Breb.Cuentas.Contratos;

// Llega desde el core bancario destino: confirma que el abono se acreditó.
// Canal del contrato: transferencia.abono-confirmado
public record AbonoConfirmado
{
    public Guid TransferenciaId { get; init; }
    public DateTime ConfirmadoEn { get; init; }
}

// Lo publica la saga cuando el reloj de 15s vence sin confirmación.
// Canal del contrato: transferencia.falla-detectada
public record CompensarTransferencia
{
    public Guid TransferenciaId { get; init; }
    public Guid CuentaId { get; init; }
    public decimal MontoUVB { get; init; }
    public string Motivo { get; init; } = string.Empty;
}

// Confirma que el dinero volvió a la cuenta del usuario.
public record FondosReintegrados
{
    public Guid TransferenciaId { get; init; }
    public string Motivo { get; init; } = string.Empty;
}

// Cierre feliz de la transferencia.
// Canal del contrato: transferencia.confirmada
public record TransferenciaCompletada
{
    public Guid TransferenciaId { get; init; }
}

// El mensaje del reloj. No viene de ningún servicio: lo programa la saga
// contra sí misma para despertarse dentro de 15 segundos.
public record TimeoutConfirmacion
{
    public Guid TransferenciaId { get; init; }
}
