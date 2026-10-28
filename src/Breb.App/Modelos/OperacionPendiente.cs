namespace Breb.App.Modelos;

/// <summary>
/// Una transferencia que el usuario ya pidió y que todavía no fue confirmada
/// por el servidor.
/// <para>
/// Esto es lo que se guarda en disco. Fíjese en lo que <b>no</b> tiene: no
/// guarda el resultado de la operación, ni si tuvo éxito, ni la respuesta del
/// servidor. Guarda la <b>intención</b> del usuario. El resultado lo decide
/// el servidor, y mientras no lo haya dicho, lo único que tenemos es esta
/// intención con su llave.
/// </para>
/// <para>
/// Es el mismo principio del Outbox de la Semana 2: no se guarda «el evento
/// se publicó», se guarda «hay que publicar este evento», y se borra cuando
/// alguien confirma que salió.
/// </para>
/// </summary>
/// <param name="Llave">
/// La llave de idempotencia. Nace cuando el usuario toca el botón y vive
/// mientras viva esta operación. Es la razón de todo lo demás: sin ella,
/// reenviar sería crear una transferencia nueva.
/// </param>
/// <param name="CuentaId">Cuenta origen.</param>
/// <param name="MontoUVB">Monto que el usuario pidió mover.</param>
/// <param name="CreadaEn">
/// Cuándo tocó el botón, en UTC. Sirve para dos cosas: mostrarle al usuario
/// hace cuánto está pendiente, y descartar operaciones demasiado viejas
/// (una transferencia encolada hace tres días probablemente ya no se quiere).
/// </param>
/// <param name="Intentos">
/// Cuántas veces se intentó enviar. No se usa para decidir si reintentar
/// —eso lo decide la red— sino para no reintentar infinitamente algo que
/// falla siempre, y para poder decirle al usuario la verdad.
/// </param>
public record OperacionPendiente(
    Guid Llave,
    Guid CuentaId,
    decimal MontoUVB,
    DateTime CreadaEn,
    int Intentos = 0);
