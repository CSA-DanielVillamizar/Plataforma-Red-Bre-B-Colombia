namespace Breb.App.Services;

public sealed class SecureTokenStore : ITokenStore
{
    private const string Clave = "breb_jwt";

    public Task GuardarAsync(string token) =>
        SecureStorage.Default.SetAsync(Clave, token);

    public Task<string?> ObtenerAsync() =>
        SecureStorage.Default.GetAsync(Clave);

    public Task BorrarAsync()
    {
        SecureStorage.Default.Remove(Clave);
        return Task.CompletedTask;
    }
}
