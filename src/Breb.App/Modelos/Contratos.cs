namespace Breb.App.Modelos;

// Mismos contratos que expone Breb.Cuentas en /token, /cuentas/{id}/saldo,
// /cuentas/{id}/retener y /transferencias/{id}/confirmar-abono.
// No hay DTOs compartidos entre el backend y la app: se copian a mano, como
// pasa en cualquier app móvil real que consume una API que no controla.

public record SolicitudToken(string Usuario, string Clave);

public record RespuestaToken(string Token, DateTimeOffset Expira, string Tipo, string Rol, double DuracionMinutos);

public record RespuestaSaldo(Guid CuentaId, decimal SaldoDisponible, decimal SaldoRetenido);

// Lo que devuelve POST /cuentas/{id}/retener: el id de la transferencia creada.
public record RespuestaRetencion(Guid TransferenciaId, decimal MontoUVB);

// Diagnóstico de /quien-soy: qué dice realmente el token con el que llamamos.
public record RespuestaQuienSoy(bool Autenticado, string? Nombre, Dictionary<string, string> Afirmaciones);
