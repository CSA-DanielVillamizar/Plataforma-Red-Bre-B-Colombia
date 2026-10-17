using Breb.App.Models;

namespace Breb.App.Services;

public interface IAuthService
{
    Task<TokenResponse> LoginAsync(string usuario, string clave, CancellationToken cancellationToken = default);
}
