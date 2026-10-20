using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Breb.App.Servicios;

namespace Breb.App.ViewModels;

/// <summary>
/// Pantalla 1: Login. Autentica contra /token y guarda el JWT de forma segura.
/// Toda la lógica vive aquí; la vista (LoginPage.xaml) solo hace binding.
/// </summary>
public partial class LoginViewModel(ClienteBrebApi api, SesionSegura sesion, ConsolaLog consola) : ObservableObject
{
    [ObservableProperty] private string usuario = "ana.operadora";
    [ObservableProperty] private string clave = "Operadora-2026";
    [ObservableProperty] private string estado = "Sin sesión";
    [ObservableProperty] private bool cargando;
    [ObservableProperty] private bool haySesion;
    [ObservableProperty] private string rolActual = "—";
    [ObservableProperty] private string quienSoyTexto = string.Empty;
    [ObservableProperty] private bool hayQuienSoy;

    // Usuarios de laboratorio predefinidos: un toque los selecciona.
    public IReadOnlyList<UsuarioDemo> Usuarios { get; } = new[]
    {
        new UsuarioDemo("ana.operadora",  "Operadora-2026",   "operador", "#512BD4"),
        new UsuarioDemo("luis.consulta",  "Consulta-2026",    "consulta", "#0A7EA4"),
        new UsuarioDemo("core-bancario",  "CoreBancario-2026","banco",    "#2E8B57"),
    };

    [RelayCommand]
    private void SeleccionarUsuario(UsuarioDemo u)
    {
        Usuario = u.Usuario;
        Clave = u.Clave;
        Estado = $"Credenciales de «{u.Usuario}» cargadas · rol {u.Rol}";
    }

    [RelayCommand]
    private async Task IniciarSesionAsync()
    {
        if (Cargando) return;
        Cargando = true;
        Estado = "Pidiendo token…";
        try
        {
            var token = await api.ObtenerTokenAsync(Usuario, Clave);
            HaySesion = true;
            RolActual = token.Rol;
            Estado = $"Sesión iniciada · rol {token.Rol} · expira {token.Expira:HH:mm:ss}";
        }
        catch (HttpRequestException ex)
        {
            Estado = $"Sin conexión con la API: {ex.Message}";
            consola.Error($"red: {ex.Message}");
            HaySesion = false;
        }
        catch (Exception ex)
        {
            Estado = ex.Message;
            consola.Error(ex.Message);
            HaySesion = false;
        }
        finally { Cargando = false; }
    }

    [RelayCommand]
    private void CerrarSesion()
    {
        sesion.CerrarSesion();
        HaySesion = false;
        RolActual = "—";
        Estado = "Sesión cerrada · token borrado del SecureStorage";
        consola.Info("sesión cerrada · SecureStorage limpiado");
    }

    // Función extra: ver qué dice el token (decodificado por el servidor).
    [RelayCommand]
    private async Task QuienSoyAsync()
    {
        if (Cargando) return;
        Cargando = true;
        try
        {
            var quien = await api.QuienSoyAsync();

            // Desglosar TODAS las afirmaciones del token con nombres legibles.
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"Autenticado: {(quien.Autenticado ? "sí" : "no")}");
            if (!string.IsNullOrWhiteSpace(quien.Nombre))
                sb.AppendLine($"Nombre: {quien.Nombre}");

            foreach (var (tipo, valor) in quien.Afirmaciones)
            {
                var etiqueta = NombreLegible(tipo);
                var mostrado = valor;

                // iat / nbf / exp vienen como epoch (segundos). Hacerlos legibles.
                if (tipo is "exp" or "iat" or "nbf" && long.TryParse(valor, out var epoch))
                    mostrado = DateTimeOffset.FromUnixTimeSeconds(epoch).LocalDateTime.ToString("yyyy-MM-dd HH:mm:ss");

                sb.AppendLine($"{etiqueta}: {mostrado}");
                consola.Info($"claim · {etiqueta} = {mostrado}");
            }

            QuienSoyTexto = sb.ToString().TrimEnd();
            HayQuienSoy = true;
            Estado = $"Token de {quien.Nombre ?? "—"} · {quien.Afirmaciones.Count} afirmaciones";
        }
        catch (Exception ex)
        {
            Estado = ex.Message;
            consola.Error(ex.Message);
        }
        finally { Cargando = false; }
    }

    // Traduce los nombres técnicos de las afirmaciones JWT a algo legible.
    private static string NombreLegible(string tipo) => tipo switch
    {
        "sub" => "Usuario (sub)",
        "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier" => "Usuario",
        "role" or "http://schemas.microsoft.com/ws/2008/06/identity/claims/role" => "Rol",
        "jti" => "Id del token (jti)",
        "iss" => "Emisor (iss)",
        "aud" => "Audiencia (aud)",
        "iat" => "Emitido (iat)",
        "nbf" => "Válido desde (nbf)",
        "exp" => "Expira (exp)",
        _      => tipo
    };
}

/// <summary>Usuario de laboratorio predefinido para el selector del login.</summary>
public sealed record UsuarioDemo(string Usuario, string Clave, string Rol, string ColorHex);
