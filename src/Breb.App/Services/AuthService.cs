using System.Net.Http.Json;
using System.Text.Json;
using Breb.App.Models;

namespace Breb.App.Services;

public sealed class AuthService : IAuthService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _http;
    private readonly ITokenStore _tokenStore;

    public AuthService(HttpClient http, ITokenStore tokenStore)
    {
        _http = http;
        _tokenStore = tokenStore;
    }

    public async Task<TokenResponse> LoginAsync(string usuario, string clave, CancellationToken cancellationToken = default)
    {
        try
        {
            using var respuesta = await _http.PostAsJsonAsync(
                "/token",
                new LoginRequest(usuario, clave),
                cancellationToken);

            if (!respuesta.IsSuccessStatusCode)
                throw HttpErrorMapper.FromStatus(respuesta.StatusCode, "Usuario o contraseña incorrectos.");

            var cuerpo = await respuesta.Content.ReadFromJsonAsync<TokenResponse>(JsonOptions, cancellationToken)
                ?? throw new ApiException("La API no devolvió un token válido.");

            if (string.IsNullOrWhiteSpace(cuerpo.Token))
                throw new ApiException("La API no devolvió un token válido.");

            await _tokenStore.GuardarAsync(cuerpo.Token);
            return cuerpo;
        }
        catch (ApiException)
        {
            throw;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            throw HttpErrorMapper.FromNetwork(ex);
        }
    }
}
