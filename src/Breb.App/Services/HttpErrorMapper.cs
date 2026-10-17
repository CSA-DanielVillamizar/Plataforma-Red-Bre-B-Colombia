using System.Net;

namespace Breb.App.Services;

internal static class HttpErrorMapper
{
    public static ApiException FromStatus(HttpStatusCode status, string? contexto = null) =>
        status switch
        {
            HttpStatusCode.Unauthorized => new ApiException(
                contexto ?? "Credenciales inválidas o sesión expirada.", 401),
            HttpStatusCode.Forbidden => new ApiException(
                "No tiene permiso para esta operación.", 403),
            HttpStatusCode.NotFound => new ApiException(
                "La cuenta no existe.", 404),
            HttpStatusCode.InternalServerError => new ApiException(
                "Error interno del servidor. Revise que la API y la base de datos estén al día.", 500),
            _ => new ApiException(
                $"El servidor respondió con error {(int)status}.", (int)status)
        };

    public static ApiException FromNetwork(Exception ex) =>
        ex is TaskCanceledException
            ? new ApiException("La solicitud tardó demasiado. Revise que la API esté en marcha.")
            : new ApiException("No se pudo conectar con el servidor. Revise la red y que la API esté en marcha.");
}
