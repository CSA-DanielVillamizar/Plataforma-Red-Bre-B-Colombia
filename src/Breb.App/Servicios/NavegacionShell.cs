namespace Breb.App.Servicios;

public sealed class NavegacionShell : INavegacion
{
    public Task IrAsync(string ruta) =>
        MainThread.InvokeOnMainThreadAsync(() => Shell.Current.GoToAsync(ruta));
}
