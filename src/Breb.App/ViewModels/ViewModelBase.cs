using CommunityToolkit.Mvvm.ComponentModel;
using Breb.App.Servicios;

namespace Breb.App.ViewModels;

/// <summary>
/// Lo que comparten las tres pantallas: el mensaje al usuario, el indicador de
/// carga y el texto del panel de observabilidad (última llamada a la API).
/// </summary>
public abstract partial class ViewModelBase : ObservableObject
{
    protected ViewModelBase(ClienteBrebApi api) => Api = api;

    protected ClienteBrebApi Api { get; }

    [ObservableProperty]
    private string observabilidad = "Aún no hay llamadas a la API.";

    [ObservableProperty]
    private bool cargando;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HayMensaje))]
    private string mensaje = string.Empty;

    [ObservableProperty]
    private bool mensajeEsError;

    public bool HayMensaje => !string.IsNullOrEmpty(Mensaje);

    protected void MostrarUltimaLlamada() =>
        Observabilidad = Api.Ultima?.ToString() ?? "Aún no hay llamadas a la API.";

    protected void Informar(string texto)
    {
        MensajeEsError = false;
        Mensaje = texto;
    }

    protected void Fallar(string texto)
    {
        MensajeEsError = true;
        Mensaje = texto;
    }
}
