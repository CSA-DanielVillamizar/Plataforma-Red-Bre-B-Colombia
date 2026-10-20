using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Breb.App.Servicios;

namespace Breb.App.ViewModels;

/// <summary>
/// Pantalla de Consola en vivo. Hace binding directo a la colección de la
/// <see cref="ConsolaLog"/>, así que cada petición HTTP aparece sola. Es la
/// prueba visual de que la app hace EXACTAMENTE lo que decimos en la demo.
/// </summary>
public partial class ConsolaViewModel(ConsolaLog consola) : ObservableObject
{
    public ObservableCollection<LineaLog> Lineas => consola.Lineas;

    [RelayCommand]
    private void Limpiar() => consola.Limpiar();
}
