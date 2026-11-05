using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Breb.App.Servicios;

namespace Breb.App.ViewModels;

/// <summary>
/// Clase 15: el ViewModel no toca un solo control. Expone propiedades
/// observables y comandos; el binding del XAML hace el resto.
///
/// <para>
/// Clase 19: ya no pide usuario ni clave. Lee el token de
/// <see cref="SesionUsuario"/>, que lo obtuvo una sola vez en la pantalla
/// de entrada. Antes esta pantalla pedía su propio token en CADA consulta
/// —un login por botonazo— y además con un usuario distinto al de la
/// pantalla de transferencia. Dos tokens para un solo usuario no era una
/// app: eran dos pantallas sueltas que hablaban con la misma API.
/// </para>
/// </summary>
public partial class SaldoViewModel : ObservableObject
{
    // La misma cuenta de demo de las Semanas 2-9 (scripts/cuentas-base.sql).
    private static readonly Guid CuentaDemo = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private readonly ClienteBrebApi _api;
    private readonly SesionUsuario _sesion;

    public SaldoViewModel(ClienteBrebApi api, SesionUsuario sesion)
    {
        _api = api;
        _sesion = sesion;
    }

    [ObservableProperty]
    private string saldoTexto = "—";

    [ObservableProperty]
    private string estado = "Sin consultar";

    [ObservableProperty]
    private string quienEntro = "—";

    [ObservableProperty]
    private string direccionApi = string.Empty;

    [ObservableProperty]
    private bool cargando;

    /// <summary>
    /// Al aparecer la pantalla: mostrar quién entró y a dónde apunta la app.
    /// No consulta el saldo por su cuenta, a propósito — en la demo conviene
    /// que pedirlo sea un acto visible.
    /// </summary>
    [RelayCommand]
    private void Aparecer()
    {
        DireccionApi = ConfiguracionApi.Descripcion;
        QuienEntro = _sesion.EstaAutenticado
            ? $"{_sesion.Usuario} · rol {_sesion.Rol}"
            : "sin sesión";
    }

    [RelayCommand]
    private async Task ConsultarSaldoAsync()
    {
        if (Cargando) return;
        Cargando = true;
        Estado = "Consultando saldo…";
        try
        {
            // El token ya existe: lo trajo la pantalla de entrada. Si se
            // venció, TokenVigente() lanza con un mensaje que el usuario
            // entiende en vez de un 401 crudo.
            var token = _sesion.TokenVigente();

            var saldo = await _api.ConsultarSaldoAsync(CuentaDemo, token);

            SaldoTexto = saldo.SaldoDisponible.ToString("N2", CultureInfo.GetCultureInfo("es-CO")) + " UVB";
            Estado = $"OK · rol {_sesion.Rol} · retenido {saldo.SaldoRetenido:N2} UVB";
        }
        catch (HttpRequestException ex)
        {
            // Este es el error que aparece cuando la dirección de la API no
            // corresponde a dónde corre la app: la trampa 3 de la Clase 14
            // en el emulador, y el teléfono físico de la Clase 19.
            Estado = ConfiguracionApi.FaltaConfigurar
                ? ConfiguracionApi.ComoConfigurar
                : $"Sin conexión con la API en {ConfiguracionApi.Direccion}: {ex.Message}";
            SaldoTexto = "—";
        }
        catch (Exception ex)
        {
            Estado = ex.Message;
            SaldoTexto = "—";
        }
        finally
        {
            Cargando = false;
        }
    }

    /// <summary>
    /// Cerrar sesión y volver a la entrada. Borra el token de memoria y
    /// <b>no</b> toca la cola local: el token es identidad, la cola es
    /// intención del usuario. Perder la sesión no debe perder una
    /// transferencia que el usuario ya pidió.
    /// </summary>
    [RelayCommand]
    private async Task SalirAsync()
    {
        _sesion.CerrarSesion();
        SaldoTexto = "—";
        Estado = "Sin consultar";
        await Shell.Current.GoToAsync("//LoginPage");
    }
}
