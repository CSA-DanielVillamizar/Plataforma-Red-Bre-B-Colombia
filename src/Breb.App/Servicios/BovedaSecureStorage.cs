namespace Breb.App.Servicios;

/// <summary>
/// SecureStorage de MAUI: en Android el valor queda cifrado con la clave del
/// sistema, no en un archivo de texto. En el emulador funciona. Si el
/// emulador no tiene bloqueo de pantalla, el sistema puede negarse: eso
/// se traduce a un mensaje, no a un cierre de la app.
/// </summary>
public sealed class BovedaSecureStorage : IBovedaSegura
{
    public async Task GuardarAsync(string clave, string valor)
    {
        try
        {
            await SecureStorage.Default.SetAsync(clave, valor);
        }
        catch (Exception ex)
        {
            throw new ExcepcionAlmacenSeguro(
                "No se pudo guardar la sesión en el almacén seguro del teléfono. " +
                "En el emulador, ponga un PIN de prueba si el sistema lo pide.", ex);
        }
    }

    public async Task<string?> LeerAsync(string clave)
    {
        try
        {
            return await SecureStorage.Default.GetAsync(clave);
        }
        catch (Exception ex)
        {
            throw new ExcepcionAlmacenSeguro(
                "No se pudo leer la sesión guardada en el teléfono.", ex);
        }
    }

    public Task BorrarAsync(string clave)
    {
        try
        {
            SecureStorage.Default.Remove(clave);
            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            throw new ExcepcionAlmacenSeguro(
                "No se pudo borrar la sesión guardada en el teléfono.", ex);
        }
    }
}
