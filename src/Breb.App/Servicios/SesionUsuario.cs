using Breb.App.Modelos;

namespace Breb.App.Servicios;

/// <summary>
/// Clase 19: la sesión del usuario — un solo token para toda la app.
///
/// <para>
/// <b>Por qué existe.</b> Hasta ahora cada pantalla pedía usuario y clave
/// por su cuenta y guardaba su propio token: la de saldo entraba como
/// <c>luis.consulta</c> y la de transferencia como <c>ana.operadora</c>.
/// Funcionaba, pero no era una app: eran dos pantallas que casualmente
/// hablaban con la misma API. El usuario escribía sus credenciales dos
/// veces, cada pantalla pedía un token nuevo en cada operación, y nada
/// sabía si había alguien autenticado.
/// </para>
///
/// <para>
/// <b>Un solo token alcanza</b> porque la política <c>LecturaCuentas</c> de
/// la Semana 8 admite los roles <c>operador</c> y <c>consulta</c>: quien
/// puede mover plata también puede leer saldos. Así que entrando como
/// <c>ana.operadora</c> las dos pantallas funcionan. Al revés no:
/// <c>luis.consulta</c> lee el saldo pero recibe <b>403</b> al retener, y
/// eso no es un error de la app sino la autorización haciendo su trabajo.
/// </para>
///
/// <para>
/// <b>Singleton</b>, porque es UNA sesión. Dos instancias serían dos
/// usuarios distintos dentro de la misma app.
/// </para>
///
/// <para>
/// <b>Y por eso NO recibe el <see cref="ClienteBrebApi"/>.</b> Sería lo
/// natural —«la sesión pide su propio token»— y sería un error de ciclos de
/// vida: <c>AddHttpClient</c> registra el cliente como <b>transitorio</b> a
/// propósito, porque el <c>HttpClient</c> que trae debe ser de vida corta
/// para que la fábrica pueda rotar sus handlers. Un singleton que lo
/// inyecta lo <b>captura para siempre</b> y congela ese handler: deja de
/// ver cambios de DNS y de red. Es un bug que no da la cara en el
/// laboratorio, con una IP fija, y sí la da en producción.
/// </para>
///
/// <para>
/// Así que aquí sólo se <b>guarda</b> el resultado. Quien llama a la API es
/// el ViewModel, que es transitorio y por tanto puede recibir el cliente
/// sin capturarlo. La sesión es un dato, no un colaborador con red.
/// </para>
///
/// <para>
/// <b>Lo que NO hace, a propósito:</b> no guarda el token en disco. Un JWT
/// en un archivo es una credencial en reposo, y el de la Semana 8 dura
/// pocos minutos: no vale la pena el riesgo. Si la app se cierra hay que
/// volver a entrar. La <b>cola</b> sí sobrevive al cierre, porque guarda la
/// intención del usuario, no su identidad. Son dos cosas distintas y se
/// tratan distinto.
/// </para>
/// </summary>
public sealed class SesionUsuario
{
    public string? Token { get; private set; }
    public string Usuario { get; private set; } = string.Empty;
    public string Rol { get; private set; } = string.Empty;
    public DateTimeOffset Expira { get; private set; }

    /// <summary>Hay token y todavía no se venció.</summary>
    public bool EstaAutenticado => Token is not null && Expira > DateTimeOffset.UtcNow;

    /// <summary>
    /// Hubo sesión y el token se venció. Se distingue de «nunca entró»
    /// porque el mensaje que merece el usuario es otro: «su sesión expiró,
    /// vuelva a entrar» en vez de «entre».
    /// </summary>
    public bool Expiro => Token is not null && Expira <= DateTimeOffset.UtcNow;

    /// <summary>Puede retener fondos (rol operador, Semana 8).</summary>
    public bool PuedeTransferir => Rol == "operador";

    /// <summary>
    /// Guarda lo que devolvió <c>/token</c>. Lo llama el
    /// <c>LoginViewModel</c> después de una respuesta buena; si las
    /// credenciales estaban mal, el ViewModel nunca llega aquí y la sesión
    /// anterior queda intacta — un login fallido no debe borrar una sesión
    /// que ya servía.
    /// </summary>
    public void Guardar(string usuario, RespuestaToken respuesta)
    {
        Token = respuesta.Token;
        Usuario = usuario;
        Rol = respuesta.Rol;
        Expira = respuesta.Expira;
    }

    public void CerrarSesion()
    {
        Token = null;
        Usuario = string.Empty;
        Rol = string.Empty;
        Expira = default;
    }

    /// <summary>
    /// El token, o una excepción con un mensaje que el usuario entienda.
    /// Los ViewModels llaman esto en vez de leer <see cref="Token"/> para
    /// no repetir la comprobación de nulo ni la de vencimiento.
    /// </summary>
    public string TokenVigente() =>
        EstaAutenticado
            ? Token!
            : throw new InvalidOperationException(
                Expiro ? "Su sesión expiró. Vuelva a entrar."
                       : "No hay sesión abierta. Entre primero.");
}
