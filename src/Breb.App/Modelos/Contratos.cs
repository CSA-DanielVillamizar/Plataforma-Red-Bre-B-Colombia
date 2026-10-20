namespace Breb.App.Modelos;

// Contratos de Breb.Cuentas: /token, /cuentas/{id}/saldo y /cuentas/{id}/retener.
// No hay DTOs compartidos con el backend: se declaran aquí, como en cualquier
// app móvil que consume una API que no controla.

public record SolicitudToken(string Usuario, string Clave);

public record RespuestaToken(string Token, DateTimeOffset Expira, string Tipo, string Rol, double DuracionMinutos);

public record RespuestaSaldo(Guid CuentaId, decimal SaldoDisponible, decimal SaldoRetenido);

public record RespuestaRetencion(Guid TransferenciaId, decimal MontoUVB);

/// <summary>Cuentas de laboratorio sembradas por scripts/cuentas-base.sql.</summary>
public static class CuentasLaboratorio
{
    public static readonly Guid Principal = Guid.Parse("11111111-1111-1111-1111-111111111111");
}
