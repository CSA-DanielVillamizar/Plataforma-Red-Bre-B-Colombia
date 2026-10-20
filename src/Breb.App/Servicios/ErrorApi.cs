namespace Breb.App.Servicios;

public enum TipoError
{
    SinConexion,
    CredencialesRechazadas,
    SesionExpirada,
    SinPermiso,
    NoEncontrado,
    Servidor
}

/// <summary>
/// El único error que el servicio deja salir hacia los ViewModels. El mensaje
/// ya está redactado para el usuario: nunca lleva el texto crudo de una
/// excepción ni dice si falló el usuario o la clave.
/// </summary>
public sealed class ErrorApi(TipoError tipo, string mensaje) : Exception(mensaje)
{
    public TipoError Tipo { get; } = tipo;
}
