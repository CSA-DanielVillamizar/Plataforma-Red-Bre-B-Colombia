namespace Breb.App.Services;

public interface ITokenStore
{
    Task GuardarAsync(string token);
    Task<string?> ObtenerAsync();
    Task BorrarAsync();
}
