namespace Breb.App.Servicios;

/// <summary>
/// Clase 19: dónde está la API, según dónde corre la app.
///
/// <para>
/// Esta clase existe porque la dirección de la API es el error número uno
/// del día de la demo, y porque hasta hoy el código de referencia la tenía
/// <b>fija</b> en <c>10.0.2.2</c> para todo Android. Esa dirección es un
/// alias que el <b>emulador</b> reserva para hablar con la máquina que lo
/// hospeda: en un teléfono físico no existe, no resuelve, y ninguna llamada
/// llega nunca.
/// </para>
///
/// <para>
/// El síntoma de equivocarse aquí es cruel: no sale «dirección inválida».
/// El paquete sale, nadie contesta, y a los 10 segundos aparece un timeout
/// que parece lentitud del servidor. Se pierde media hora buscando el error
/// en el código que está bien. Por eso esta clase no sólo elige la
/// dirección: también sabe decir <b>cuándo le falta un dato</b>, para que la
/// app lo diga de entrada en vez de dejar al usuario esperando.
/// </para>
/// </summary>
public static class ConfiguracionApi
{
    /// <summary>
    /// ⬅ <b>LO ÚNICO que un squad cambia para su demo en teléfono físico.</b>
    ///
    /// <para>
    /// Ponga aquí la IP de SU máquina en la red WiFi — la que sale en
    /// <c>ipconfig</c>, algo como <c>192.168.20.29</c>. Déjelo vacío para
    /// trabajar en el escritorio o en el emulador.
    /// </para>
    ///
    /// <para>
    /// Cuando tiene valor, gana sobre todo lo demás, y a propósito: la IP de
    /// la máquina en la LAN es alcanzable desde el escritorio, desde el
    /// emulador <b>y</b> desde el teléfono. Es la única de las tres
    /// direcciones que sirve en los tres sitios, así que para la demo del
    /// E4 conviene ponerla y no volver a pensar en esto.
    /// </para>
    ///
    /// <para>
    /// Sí, es una constante en el código y no un archivo de configuración.
    /// Es deliberado para el laboratorio: un solo lugar, visible, que se
    /// cambia en segundos delante de todos. En una app de verdad esto
    /// vendría de <c>appsettings</c> o de una pantalla de ajustes.
    /// </para>
    /// </summary>
    public const string IpEnLaWifi = "192.168.11.140";

    public const int Puerto = 5080;

    /// <summary>El alias del anfitrión visto DESDE DENTRO del emulador.</summary>
    private const string AliasDelEmulador = "10.0.2.2";

    /// <summary>
    /// En el escritorio se usa <c>127.0.0.1</c> y NO <c>localhost</c>, a
    /// propósito: en Windows <c>localhost</c> resuelve primero a <c>::1</c>
    /// por IPv6, y la API escucha en <c>0.0.0.0</c>, que es sólo IPv4.
    /// Nombrar la IP evita depender de ese orden de resolución.
    /// </summary>
    private const string MismaMaquina = "127.0.0.1";

    private static bool EsAndroid => DeviceInfo.Platform == DevicePlatform.Android;
    private static bool EsIos => DeviceInfo.Platform == DevicePlatform.iOS;

    /// <summary>
    /// ¿Es un emulador o un aparato de verdad? MAUI lo sabe, y es justo lo
    /// que hacía falta para no tener que adivinar por plataforma.
    /// </summary>
    private static bool EsEmulador => DeviceInfo.DeviceType == DeviceType.Virtual;

    /// <summary>Hay IP de WiFi configurada.</summary>
    private static bool TieneIpDeWifi => !string.IsNullOrWhiteSpace(IpEnLaWifi);

    /// <summary>
    /// <b>true</b> cuando la app corre en un teléfono <b>físico</b> y nadie
    /// puso <see cref="IpEnLaWifi"/>. En ese caso cualquier llamada va a
    /// fallar con timeout, y los ViewModels lo dicen ANTES de intentarlo.
    /// </summary>
    public static bool FaltaConfigurar =>
        (EsAndroid || EsIos) && !EsEmulador && !TieneIpDeWifi;

    /// <summary>
    /// La dirección base. Siempre devuelve una URI válida, incluso cuando
    /// falta configurar: el <c>HttpClient</c> necesita una al construirse, y
    /// quien avisa del problema es <see cref="FaltaConfigurar"/>, no una
    /// excepción en el arranque. Una app que no abre es peor que una app que
    /// explica qué le falta.
    /// </summary>
    public static string Direccion =>
        TieneIpDeWifi ? $"http://{IpEnLaWifi}:{Puerto}"
        : EsAndroid && EsEmulador ? $"http://{AliasDelEmulador}:{Puerto}"
                                 : $"http://{MismaMaquina}:{Puerto}";

    /// <summary>
    /// Para mostrar en pantalla. Que la app diga a dónde está llamando
    /// convierte media hora de depuración en una mirada: si dice
    /// <c>10.0.2.2</c> y usted está en un teléfono, ya sabe qué pasa.
    /// </summary>
    public static string Descripcion
    {
        get
        {
            if (TieneIpDeWifi)
                return $"{Direccion} · IP en la WiFi";
            if (!EsAndroid && !EsIos)
                return $"{Direccion} · esta misma máquina";
            if (EsAndroid && EsEmulador)
                return $"{Direccion} · alias del anfitrión (sólo emulador)";
            if (EsIos && EsEmulador)
                return $"{Direccion} · simulador en esta misma máquina";
            return $"{Direccion} · ⚠️ teléfono físico sin IP configurada";
        }
    }

    /// <summary>
    /// La URL que hay que abrir en el navegador DEL TELÉFONO para saber si
    /// el problema es la red y no el código. Si <see cref="IpEnLaWifi"/>
    /// está vacío no hay nada que sugerir, así que se deja el hueco a la
    /// vista en vez de inventar una dirección.
    /// </summary>
    public static string SwaggerParaProbar =>
        TieneIpDeWifi ? $"http://{IpEnLaWifi}:{Puerto}/swagger"
                      : $"http://SU-IP-EN-LA-WIFI:{Puerto}/swagger";

    /// <summary>Qué hacer, en una frase, cuando falta la IP.</summary>
    public const string ComoConfigurar =
        "Esta app corre en un teléfono físico y necesita la IP de su Mac en la WiFi. " +
        "10.0.2.2 sólo vale dentro del emulador Android. Ponga la IP de su máquina en " +
        "ConfiguracionApi.IpEnLaWifi (la que sale en ipconfig), vuelva a compilar, y " +
        "antes de culpar al código abra http://esa-ip:5080/swagger en el navegador DEL " +
        "TELÉFONO: si esa página no carga, el problema es la red o el firewall.";
}
