namespace Breb.App.Servicios;

/// <summary>Lo que quedó de un login. La clave no se guarda.</summary>
public sealed record SesionGuardada(string Token, string Rol, string Usuario);

/// <summary>
/// Dónde vive el JWT. No es un campo del ViewModel ni una variable global:
/// cada pantalla lo pide aquí cuando lo necesita.
/// </summary>
public interface IAlmacenSesion
{
    Task GuardarAsync(string token, string rol, string usuario);
    Task<SesionGuardada?> LeerAsync();
    Task BorrarAsync();
}
