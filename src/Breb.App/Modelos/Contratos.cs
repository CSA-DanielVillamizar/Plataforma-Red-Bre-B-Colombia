namespace Breb.App.Modelos;

// Mismos contratos que expone Breb.Cuentas. No hay DTOs compartidos entre el
// backend y la app: se copian a mano, como pasa en cualquier app móvil real
// que consume una API que no controla.

public record SolicitudToken(string Usuario, string Clave);

public record RespuestaToken(string Token, DateTimeOffset Expira, string Tipo, string Rol, double DuracionMinutos);

public record RespuestaSaldo(Guid CuentaId, decimal SaldoDisponible, decimal SaldoRetenido);

// POST /cuentas/{id}/retener responde { transferenciaId, montoUVB }.
// No hay clave de idempotencia en el contrato: el servidor inventa el id.
public record RespuestaRetencion(Guid TransferenciaId, decimal MontoUVB);
