namespace Breb.App.Modelos;

// Mismos contratos que expone Breb.Cuentas en /token y /cuentas/{id}/saldo.
// No hay DTOs compartidos entre el backend y la app: se copian a mano, como
// pasa en cualquier app móvil real que consume una API que no controla.

public record SolicitudToken(string Usuario, string Clave);

public record RespuestaToken(string Token, DateTimeOffset Expira, string Tipo, string Rol, double DuracionMinutos);

public record RespuestaSaldo(Guid CuentaId, decimal SaldoDisponible, decimal SaldoRetenido);

// Clase 18: lo que responde /cuentas/{id}/retener. El campo Repetida es la
// señal de la Semana 14: cuando llega en true, el servidor reconoció la llave
// y NO creó una segunda transferencia. La app lo usa para distinguir "se hizo"
// de "ya estaba hecho" sin tener que adivinarlo.
public record RespuestaRetencion(Guid TransferenciaId, decimal MontoUVB, bool Repetida);
