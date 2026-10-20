namespace Breb.App.Servicios;

public static class Rutas
{
    /// <summary>Absoluta: deja solo Entrar. El atrás de Android no vuelve a Saldo.</summary>
    public const string Login = "//login";

    /// <summary>Empuja Saldo encima de Entrar.</summary>
    public const string Saldo = "saldo";

    /// <summary>Empuja Transferir encima de Saldo.</summary>
    public const string Transferencia = "transferencia";

    /// <summary>Un paso atrás: de Transferir a Saldo.</summary>
    public const string Atras = "..";
}

/// <summary>Cambia de pantalla. La implementación real habla con el Shell.</summary>
public interface INavegacion
{
    Task IrAsync(string ruta);
}
