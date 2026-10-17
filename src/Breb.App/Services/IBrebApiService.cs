using Breb.App.Models;

namespace Breb.App.Services;

public interface IBrebApiService
{
    Task<SaldoResponse> ObtenerSaldoAsync(Guid cuentaId, CancellationToken cancellationToken = default);
    Task<RetencionResponse> RetenerAsync(Guid cuentaId, decimal montoUVB, CancellationToken cancellationToken = default);
}
