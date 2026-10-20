using System.Globalization;
using Breb.App.Modelos;

namespace Breb.App.Servicios;

/// <summary>
/// Único dueño del JWT. Lo guarda en SecureStorage (Android Keystore) y
/// respeta su expiración: un token vencido se descarta, nunca se envía.
/// Ni las vistas ni los ViewModels tocan el token; piden sesión a través del
/// ClienteBrebApi.
/// </summary>
public sealed class AlmacenToken
{
    private const string ClaveToken = "breb.token";
    private const string ClaveExpira = "breb.expira";

    private string? _token;
    private DateTimeOffset _expira;

    public string? Usuario { get; private set; }
    public string? Rol { get; private set; }
    public bool HaySesion => _token is not null && _expira > DateTimeOffset.UtcNow;

    public event Action? SesionCambio;

    public async Task GuardarAsync(string usuario, RespuestaToken respuesta)
    {
        _token = respuesta.Token;
        _expira = respuesta.Expira;
        Usuario = usuario;
        Rol = respuesta.Rol;

        try
        {
            await SecureStorage.Default.SetAsync(ClaveToken, respuesta.Token);
            await SecureStorage.Default.SetAsync(ClaveExpira, respuesta.Expira.ToString("O", CultureInfo.InvariantCulture));
        }
        catch (Exception)
        {
            // Sin Keystore disponible el token vive solo en memoria: la app sigue funcionando.
        }

        SesionCambio?.Invoke();
    }

    public async Task<string?> ObtenerTokenAsync()
    {
        if (_token is null)
        {
            try
            {
                _token = await SecureStorage.Default.GetAsync(ClaveToken);
                var expira = await SecureStorage.Default.GetAsync(ClaveExpira);
                if (expira is not null)
                    _expira = DateTimeOffset.Parse(expira, CultureInfo.InvariantCulture);
            }
            catch (Exception)
            {
                _token = null;
            }
        }

        if (_token is not null && _expira > DateTimeOffset.UtcNow.AddSeconds(10))
            return _token;

        Cerrar();
        return null;
    }

    public void Cerrar()
    {
        _token = null;
        Usuario = null;
        Rol = null;

        try
        {
            SecureStorage.Default.Remove(ClaveToken);
            SecureStorage.Default.Remove(ClaveExpira);
        }
        catch (Exception)
        {
            // Nada que borrar si el almacén no estaba disponible.
        }

        SesionCambio?.Invoke();
    }
}
