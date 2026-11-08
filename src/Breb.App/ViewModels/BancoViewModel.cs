using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Breb.App.Modelos;
using Breb.App.Servicios;

namespace Breb.App.ViewModels;

public partial class BancoViewModel : ObservableObject
{
    private readonly ClienteBrebApi _api;
    private readonly SesionUsuario _sesion;

    public BancoViewModel(ClienteBrebApi api, SesionUsuario sesion)
    {
        _api = api;
        _sesion = sesion;
        QuienEntro = $"{sesion.Usuario} · rol {sesion.Rol}";
    }

    public ObservableCollection<TransferenciaPendienteViewModel> Transferencias { get; } = [];

    [ObservableProperty]
    private string quienEntro = "—";

    [ObservableProperty]
    private string estado = "Sin consultar";

    [ObservableProperty]
    private bool cargando;

    public bool PuedeCargar => !Cargando;

    partial void OnCargandoChanged(bool value) => OnPropertyChanged(nameof(PuedeCargar));

    [RelayCommand]
    private void Aparecer()
    {
        if (!Cargando)
            _ = CargarAsync();
    }

    [RelayCommand]
    private async Task CargarAsync()
    {
        if (Cargando) return;

        Cargando = true;
        Estado = "Consultando transferencias pendientes…";
        try
        {
            var pendientes = await _api.ConsultarPendientesAsync(_sesion.TokenVigente());
            Transferencias.Clear();
            foreach (var pendiente in pendientes)
                Transferencias.Add(new TransferenciaPendienteViewModel(pendiente));

            Estado = $"{Transferencias.Count} transferencia(s) pendiente(s).";
        }
        catch (Exception ex)
        {
            Estado = ex.Message;
        }
        finally
        {
            Cargando = false;
        }
    }

    [RelayCommand]
    private async Task AprobarAsync(TransferenciaPendienteViewModel? transferencia)
    {
        if (transferencia is null || Cargando) return;

        Cargando = true;
        Estado = $"Aprobando {transferencia.TransferenciaId}…";
        try
        {
            await _api.ConfirmarAbonoAsync(transferencia.TransferenciaId, _sesion.TokenVigente());
            Transferencias.Remove(transferencia);
            Estado = $"Abono aprobado. Quedan {Transferencias.Count} pendiente(s).";
        }
        catch (Exception ex)
        {
            Estado = ex.Message;
        }
        finally
        {
            Cargando = false;
        }
    }
}

public sealed class TransferenciaPendienteViewModel(TransferenciaBancaria transferencia)
{
    public Guid TransferenciaId => transferencia.TransferenciaId;
    public string Detalle => $"{transferencia.MontoUVB.ToString("N2", CultureInfo.GetCultureInfo("es-CO"))} UVB";
    public string CreadaTexto => $"Creada {transferencia.CreadaEn.ToLocalTime():g}";
}
