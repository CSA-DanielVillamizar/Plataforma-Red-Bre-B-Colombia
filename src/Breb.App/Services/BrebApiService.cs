using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Breb.App.Models;

namespace Breb.App.Services;

public sealed class BrebApiService : IBrebApiService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _http;
    private readonly ITokenStore _tokenStore;

    public BrebApiService(HttpClient http, ITokenStore tokenStore)
    {
        _http = http;
        _tokenStore = tokenStore;
    }

    public Task<SaldoResponse> ObtenerSaldoAsync(Guid cuentaId, CancellationToken cancellationToken = default) =>
        EnviarAsync<SaldoResponse>(HttpMethod.Get, $"/cuentas/{cuentaId}/saldo", cancellationToken);

    public Task<RetencionResponse> RetenerAsync(Guid cuentaId, decimal montoUVB, CancellationToken cancellationToken = default)
    {
        var monto = montoUVB.ToString(System.Globalization.CultureInfo.InvariantCulture);
        return EnviarAsync<RetencionResponse>(
            HttpMethod.Post,
            $"/cuentas/{cuentaId}/retener?montoUVB={monto}",
            cancellationToken);
    }

    private async Task<T> EnviarAsync<T>(HttpMethod metodo, string ruta, CancellationToken cancellationToken)
    {
        try
        {
            var token = await _tokenStore.ObtenerAsync();
            if (string.IsNullOrWhiteSpace(token))
                throw new ApiException("No hay sesión. Inicie sesión de nuevo.", 401);

            using var solicitud = new HttpRequestMessage(metodo, ruta);
            solicitud.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            using var respuesta = await _http.SendAsync(solicitud, cancellationToken);

            if (!respuesta.IsSuccessStatusCode)
                throw HttpErrorMapper.FromStatus(respuesta.StatusCode);

            var cuerpo = await respuesta.Content.ReadFromJsonAsync<T>(JsonOptions, cancellationToken);
            return cuerpo ?? throw new ApiException("La API devolvió una respuesta vacía.");
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
