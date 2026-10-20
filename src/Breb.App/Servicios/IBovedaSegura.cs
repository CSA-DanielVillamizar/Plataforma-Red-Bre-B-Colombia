namespace Breb.App.Servicios;

/// <summary>
/// Un cajón cifrado. La implementación de la app usa SecureStorage de MAUI.
/// Las pruebas ponen un diccionario en memoria.
/// </summary>
public interface IBovedaSegura
{
    Task GuardarAsync(string clave, string valor);
    Task<string?> LeerAsync(string clave);
    Task BorrarAsync(string clave);
}
