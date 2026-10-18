using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Breb.App.Servicios;

namespace Breb.App.ViewModels;

/// <summary>Pide el token y lo deja en el almacén seguro. No consulta ni transfiere.</summary>
public partial class LoginViewModel : ViewModelBase
{
    private readonly ClienteBrebApi _api;
    private readonly IAlmacenSesion _almacen;
    private readonly INavegacion _nav;
    private bool _revisoArranque;

    public LoginViewModel(ClienteBrebApi api, IAlmacenSesion almacen, INavegacion nav)
    {
        _api = api;
        _almacen = almacen;
        _nav = nav;
        Usuario = "luis.consulta";
        Clave = "Consulta-2026";
        Estado = "Escriba usuario y clave.";
    }

    [ObservableProperty]
    private string usuario = string.Empty;

    [ObservableProperty]
    private string clave = string.Empty;

    protected override void AlCambiarCargando() => EntrarCommand.NotifyCanExecuteChanged();

    /// <summary>
    /// Solo al abrir. Si ya hay un JWT vigente, entra a Saldo. Si está vencido
    /// o no se puede leer, se queda en Entrar. Una segunda vez (el atrás
    /// desde Saldo) no vuelve a saltar.
    /// </summary>
    [RelayCommand]
    private async Task PrepararAsync()
    {
        if (_revisoArranque) return;
        _revisoArranque = true;

        try
        {
            var sesion = await _almacen.LeerAsync();
            if (sesion is null) return;

            if (!VigenciaDelToken.SigueVigente(sesion.Token, DateTimeOffset.UtcNow))
            {
                await _almacen.BorrarAsync();
                return;
            }

            Tono = "ok";
            Estado = $"Sesión guardada · rol {sesion.Rol}.";
            await _nav.IrAsync(Rutas.Saldo);
        }
        catch (ExcepcionAlmacenSeguro ex)
        {
            MarcarError(ex.Message);
        }
    }

    [RelayCommand(CanExecute = nameof(NoEstaCargando))]
    private Task EntrarAsync() => EjecutarAsync(async () =>
    {
        if (string.IsNullOrWhiteSpace(Usuario) || string.IsNullOrWhiteSpace(Clave))
        {
            MarcarError("Escriba usuario y clave.");
            return;
        }

        Estado = "Pidiendo token…";
        var token = await _api.ObtenerTokenAsync(Usuario.Trim(), Clave);
        await _almacen.GuardarAsync(token.Token, token.Rol, Usuario.Trim());
        Tono = "ok";
        Estado = $"Sesión iniciada · rol {token.Rol}.";
        await _nav.IrAsync(Rutas.Saldo);
    });
}
