namespace Breb.App.Servicios;

/// <summary>
/// El teléfono no pudo guardar o leer el token. No es un fallo de la API:
/// la pantalla lo dice y no se queda congelada.
/// </summary>
public sealed class ExcepcionAlmacenSeguro : Exception
{
    public ExcepcionAlmacenSeguro(string mensaje, Exception? interna = null)
        : base(mensaje, interna) { }
}
