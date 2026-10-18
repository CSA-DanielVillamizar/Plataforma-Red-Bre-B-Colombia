namespace Breb.App.Servicios;

public static class Rutas
{
    public const string Login = "//login";
    public const string Saldo = "//saldo";
    public const string Transferencia = "//transferencia";
}

/// <summary>Cambia de pestaña. La implementación real habla con el Shell.</summary>
public interface INavegacion
{
    Task IrAsync(string ruta);
}
