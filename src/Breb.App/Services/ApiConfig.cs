namespace Breb.App.Services;

public static class ApiConfig
{
    /// <summary>
    /// En el emulador Android, localhost es el propio emulador.
    /// La máquina anfitriona se alcanza por 10.0.2.2.
    /// </summary>
    public static string BaseUrl =>
#if ANDROID
        "http://10.0.2.2:5080";
#else
        "http://localhost:5080";
#endif
}
