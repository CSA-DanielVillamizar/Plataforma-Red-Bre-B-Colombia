namespace Breb.App.Servicios;

/// <summary>El mismo texto en las tres pantallas. La vista solo lo muestra.</summary>
public static class MensajesDeApi
{
    public static string Para(ExcepcionDeApi ex) => ex.Codigo switch
    {
        401 when ex.Operacion == "login" =>
            "Usuario o clave incorrectos.",
        401 =>
            "La sesión venció o el token ya no es válido. Vaya a Entrar e inicie sesión otra vez.",
        403 when ex.Operacion == "transferencia" =>
            "No tienes permisos para transferir, hace falta el rol operador",
        403 =>
            "No tiene permiso para esta operación.",
        404 =>
            string.IsNullOrWhiteSpace(ex.Detalle) ? "No se encontró la cuenta." : ex.Detalle,
        >= 400 and < 500 when ex.Operacion == "transferencia" =>
            string.IsNullOrWhiteSpace(ex.Detalle)
                ? $"No se pudo transferir (HTTP {ex.Codigo})."
                : $"No se pudo transferir: {ex.Detalle}",
        >= 400 and < 500 =>
            string.IsNullOrWhiteSpace(ex.Detalle) ? $"La API rechazó la operación (HTTP {ex.Codigo})." : ex.Detalle,
        >= 500 =>
            $"La API falló del lado del servidor (HTTP {ex.Codigo}). No se completó la operación. Intente más tarde.",
        _ => string.IsNullOrWhiteSpace(ex.Detalle) ? $"Error HTTP {ex.Codigo}." : ex.Detalle
    };

    public const string SinRed =
        "No hay conexión con la API. En el emulador la dirección es 10.0.2.2 puerto 5080, no localhost.";

    public const string Timeout =
        "La API no respondió a tiempo (más de 10 segundos). Compruebe que sigue encendida e intente otra vez.";

    public const string SinSesion =
        "No hay sesión. Vuelva a Entrar.";
}
