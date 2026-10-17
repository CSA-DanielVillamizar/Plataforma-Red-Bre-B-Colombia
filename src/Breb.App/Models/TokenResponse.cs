namespace Breb.App.Models;

public sealed class TokenResponse
{
    public string Token { get; set; } = string.Empty;
    public DateTimeOffset Expira { get; set; }
    public string Tipo { get; set; } = "Bearer";
    public string Rol { get; set; } = string.Empty;
    public double DuracionMinutos { get; set; }
}
